using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiperPicker.Proxies
{
    public class MopidyProxy : IDisposable
    {
        private JsonSerializerOptions _serialiserOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private string MopidyEndpoint;
        private HttpClient _client = default!;
        private Random _random = new Random();

        public event MopidyNotificationEventHandler OnMopidyNotification;
        public event MopidyEpisodeListNotificationEventHandler OnMopidyEpisodeListNotification;

        public IConfiguration Configuration;
        public ILogger<MopidyProxy> Logger;
        private bool disposedValue;
        private Task _monitorWebSocketTask;
        private CancellationTokenSource _cancellationTokenSource;
        private MopidyNowPlayingState _mopidyNowPlayingState;


        public MopidyProxy(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MopidyProxy> logger)
        {
            _client = httpClientFactory.CreateClient();
            Configuration = configuration;
            Logger = logger;
            Logger.LogInformation($"{nameof(MopidyProxy)} starting");
            MopidyEndpoint = Configuration["Mopidy:Endpoint"];

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _monitorWebSocketTask = Task.Run(() => MonitorWebSocket(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                MonitorEpisodeListPath();

            }
            catch (Exception ex)
            {
                throw new ApplicationException($"MopidyProxy could not connect to mopidy websocket on {MopidyEndpoint}", ex);
            }
        }

        private async void MonitorWebSocket(CancellationToken cancellationToken)
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                _mopidyNowPlayingState = await GetNowPlaying();

                try
                {
                    var cws = new ClientWebSocket();
                    cws.Options.CollectHttpResponseDetails = true;
                    await cws.ConnectAsync(new Uri("ws://hunchcorn:6680/mopidy/ws"), CancellationToken.None);

                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var messageJson = "{}";
                        try
                        {
                            var array = new byte[16000];
                            var buffer = new ArraySegment<byte>(array);
                            var received = await cws.ReceiveAsync(buffer, cancellationToken);
                            if (received.EndOfMessage)
                            {
                                messageJson = UTF8Encoding.UTF8.GetString(buffer);
                                if (messageJson.Contains('\0'))
                                {
                                    messageJson = messageJson.Substring(0, messageJson.IndexOf('\0'));
                                }
                                Console.WriteLine(messageJson); // xyzzy remove
                                var message = JsonSerializer.Deserialize<MopidyBaseMessage>(messageJson, _serialiserOptions);

                                if (message != null)
                                {
                                    if (message.Event == "playback_state_changed")
                                    {
                                        var playbackStateChangedMessage = JsonSerializer.Deserialize<MopidyPlaybackStateChangedMessage>(messageJson, _serialiserOptions);

                                        _mopidyNowPlayingState = _mopidyNowPlayingState with { IsPlaying = playbackStateChangedMessage!.NewState == "playing" };
                                        RaiseEvent(_mopidyNowPlayingState);
                                    }
                                    else if (new[] { "track_playback_started", "track_playback_paused", "track_playback_resumed" }.Contains(message.Event))
                                    {
                                        var playbackStartedMessage = JsonSerializer.Deserialize<MopidyTrackPlaybackStartedMessage>(messageJson, _serialiserOptions);
                                        var nowPlayingDisplayName = GetNowPlayingDisplayName(playbackStartedMessage?.TlTrack.Track);

                                        _mopidyNowPlayingState = _mopidyNowPlayingState with { TrackName = nowPlayingDisplayName, TrackDescription = playbackStartedMessage?.TlTrack.Track.Comment ?? "", TrackUri = playbackStartedMessage?.TlTrack.Track.Uri ?? "file:///" };
                                        RaiseEvent(_mopidyNowPlayingState);
                                    }
                                    else if (message.Event == "stream_title_changed")
                                    {
                                        var streamTitleChangedMessage = JsonSerializer.Deserialize<MopidyStreamTitleChangedMessage>(messageJson, _serialiserOptions);

                                        _mopidyNowPlayingState = _mopidyNowPlayingState with { TrackDescription = streamTitleChangedMessage!.Title };
                                        RaiseEvent(_mopidyNowPlayingState);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, $"Error processing websocket message from mopidy: '{messageJson}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error in websocket communication with mopidy");
                    await Task.Delay(5000);
                }
            }
        }

        private record MopidyTrack(string Uri, string Name, string Comment);
        private record MopidyTlTrack(MopidyTrack Track);
        private record MopidyTrackPlaybackStartedMessage(string Event) : MopidyBaseMessage(Event)
        {
            [JsonPropertyName("tl_track")]
            public MopidyTlTrack TlTrack { get; init; } = default!;
        }
        private record MopidyPlaybackStateChangedMessage(string Event) : MopidyBaseMessage(Event)
        {
            [JsonPropertyName("new_state")]
            public string NewState { get; init; } = default!;
        }
        private record MopidyStreamTitleChangedMessage(string Event, string Title) : MopidyBaseMessage(Event);
        private record MopidyBaseMessage(string Event);

        public async Task<StateDto> GetState()
        {
            var responseContent = await MopidyPost("core.playback.get_state");
            return JsonSerializer.Deserialize<StateDto>(responseContent, _serialiserOptions) ?? new StateDto();
        }

        public async Task<MopidyNowPlayingState> GetNowPlaying(bool refreshCache = false)
        {
            if (refreshCache || _mopidyNowPlayingState == null)
            {
                _mopidyNowPlayingState = await GetNowPlayingActual();
            }

            return _mopidyNowPlayingState;
        }

        private async Task<MopidyNowPlayingState> GetNowPlayingActual()
        {
            try
            {
                var currentTrackResponseTask = MopidyPost("core.playback.get_current_track");
                var stateResponseTask = MopidyPost("core.playback.get_state");

                var currentTrackResponse = await currentTrackResponseTask;
                var nowPlaying = JsonSerializer.Deserialize<NowPlayingResponse>(currentTrackResponse, _serialiserOptions);

                var state = await stateResponseTask;
                var stateResponse = JsonSerializer.Deserialize<StateDto>(state, _serialiserOptions);
                var isPlaying = (stateResponse?.Result ?? "") == "playing";
                var nowPlayingName = GetNowPlayingDisplayName(nowPlaying);

                return new MopidyNowPlayingState(isPlaying, nowPlayingName, nowPlaying?.Result?.Comment ?? "", nowPlaying?.Result?.Uri ?? "file:///");
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, "Error in GetNowPlaying");
                return new MopidyNowPlayingState(false, "", "", "file:///");
            }
        }

        private string GetNowPlayingDisplayName(NowPlayingResponse? nowPlayingResponse)
        {
            var nowPlayingName = nowPlayingResponse?.Result?.Name ?? "";
            if (string.IsNullOrWhiteSpace(nowPlayingName))
            {
                nowPlayingName = nowPlayingResponse?.Result?.Uri ?? "";
                nowPlayingName = nowPlayingName?.Split('/').Last().Split('.').First().Split('%').First().Replace('_', ' ').Replace('-', ' ') ?? "";
            }
            return nowPlayingName;
        }

        private string GetNowPlayingDisplayName(MopidyTrack? mopidyTrack)
        {
            var nowPlayingName = mopidyTrack?.Name ?? "";
            if (string.IsNullOrWhiteSpace(nowPlayingName))
            {
                nowPlayingName = mopidyTrack?.Uri ?? "";
                nowPlayingName = nowPlayingName?.Split('/').Last().Split('.').First().Split('%').First().Replace('_', ' ').Replace('-', ' ') ?? "";
            }
            return nowPlayingName;
        }

        private FileSystemWatcher _fileSystemWatcher;
        private void MonitorEpisodeListPath()
        {
            var directoryToMonitor = Configuration["Mopidy:EpisodeList:Path"];

            _fileSystemWatcher = new FileSystemWatcher(directoryToMonitor!);
            _fileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _fileSystemWatcher.Filter = "";
            _fileSystemWatcher.IncludeSubdirectories = true;
            _fileSystemWatcher.EnableRaisingEvents = true;
            _fileSystemWatcher.InternalBufferSize = 1048576;

            _fileSystemWatcher.Created += (object sender, FileSystemEventArgs e) => { RaiseEpisodeListEvent(); };
            _fileSystemWatcher.Renamed += (object sender, RenamedEventArgs e) => { RaiseEpisodeListEvent(); };
            _fileSystemWatcher.Deleted += (object sender, FileSystemEventArgs e) => { RaiseEpisodeListEvent(); };

            Logger.LogInformation($"Monitoring directory '{directoryToMonitor}'");
        }

        public async Task<IList<MopidyItem>> GetEpisodes()
        {
            var pathInMopidyFormat = $"file:///{Configuration["Mopidy:EpisodeList:Path"]}";
            var responseContent = await MopidyPost("core.library.browse", pathInMopidyFormat);
            var mopidyItems = JsonSerializer.Deserialize<MopidyItems>(responseContent, _serialiserOptions);
            return mopidyItems.Result;
        }

        public async Task PlayEpisode(string episodeUri)
        {
            await ClearQueue();
            await MopidyPost("core.tracklist.add", new string[] { episodeUri });
            await Play();
        }

        public async Task PlayRandomEpisode()
        {
            var episodes = await GetEpisodes();
            var randomEpisode = episodes.ElementAt(_random.Next(0, episodes.Count()));
            await PlayEpisode(randomEpisode.Uri);
        }

        public async Task ClearQueue()
        {
            await MopidyPost("core.tracklist.clear");
        }

        public async Task Play()
        {
            await MopidyPost("core.playback.play");
        }

        public async Task<string> TogglePause()
        {
            var state = await GetState();
            if (state.Result == "playing")
            {
                await MopidyPost("core.playback.pause");
                return "paused";
            }
            else
            {
                await MopidyPost("core.playback.play");
                return "playing";
            }
        }

        private async Task<string> MopidyPost(string method, string[] targetUris)
        {
            var requestContent = $"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"{method}\", \"params\":{{\"uris\":{JsonSerializer.Serialize(targetUris, _serialiserOptions)}}} }}";
            var content = new StringContent(requestContent);
            content.Headers.Clear();
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await _client.PostAsync($"{MopidyEndpoint}", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        private async Task<string> MopidyPost(string method, string targetUri = null)
        {
            var requestContent = $"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"{method}\" {(string.IsNullOrEmpty(targetUri) ? "" : $", \"params\":{{\"uri\":\"{targetUri}\"}}")} }}";
            var content = new StringContent(requestContent);
            content.Headers.Clear();
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await _client.PostAsync($"{MopidyEndpoint}", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        private void RaiseEvent(MopidyNowPlayingState mopidyNowPlayingState)
        {
            OnMopidyNotification?.Invoke(null!, new MopidyNotificationEventArgs(mopidyNowPlayingState));
        }

        private void RaiseEpisodeListEvent()
        {
            OnMopidyEpisodeListNotification?.Invoke(null!, new MopidyEpisodeListNotificationEventArgs());
        }

        public class StateDto
        {
            public string Result { get; set; }
        }

        public class NowPlayingResponse
        {
            public NowPlayingDto Result { get; set; }
        }

        public class NowPlayingDto
        {
            public string State { get; set; }

            public string Name { get; set; }

            public string Uri { get; set; }

            public string Comment { get; set; }

            public string Date { get; set; }
        }

        public class MopidyItems
        {
            public IList<MopidyItem> Result { get; set; }
        }
        public class MopidyItem
        {
            public string Name { get; set; }
            public string Uri { get; set; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource.Cancel();
                    _fileSystemWatcher.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public delegate void MopidyNotificationEventHandler(object source, MopidyNotificationEventArgs e);
        public delegate void MopidyEpisodeListNotificationEventHandler(object source, MopidyEpisodeListNotificationEventArgs e);

        public record MopidyNowPlayingState(bool IsPlaying, string TrackName, string TrackDescription, string TrackUri);

        public class MopidyNotificationEventArgs : EventArgs
        {
            private MopidyNowPlayingState _mopidyNowPlayingState = null!;
            public MopidyNotificationEventArgs(MopidyNowPlayingState mopidyNowPlayingState)
            {
                _mopidyNowPlayingState = mopidyNowPlayingState;
            }
            public MopidyNowPlayingState GetInfo()
            {
                return _mopidyNowPlayingState;
            }
        }

        public class MopidyEpisodeListNotificationEventArgs : EventArgs
        {
            public MopidyEpisodeListNotificationEventArgs()
            {
            }
        }
    }
}