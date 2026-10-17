using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PiperPicker.Models;

namespace PiperPicker.Proxies
{
    /// <summary>
    /// Runs background tasks to track Mopidy state by poll and websocket.
    /// Provides a mopidy remote procedure caller to Blazor components.
    /// </summary>
    public partial class MopidyProxy : IDisposable
    {
        /// <summary>
        /// Mopidy Remote Procedure Caller.
        /// </summary>
        public readonly MopidyCaller Rpc;

        public event MopidyNotificationEventHandler? OnMopidyNotification;
        public event MopidyEpisodeListNotificationEventHandler? OnMopidyEpisodeListNotification;

        private readonly JsonSerializerOptions _serialiserOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly string _mopidyWebSocketEndpoint;
        private readonly HttpClient _client;
        private readonly IConfiguration _configuration;
        private readonly FfmpegStreamService _streamService;
        private readonly ILogger<MopidyProxy> _logger;
        private bool _disposedValue;
        private readonly RadioSelectorOptions _radioSelectorOptions;
        private static CancellationTokenSource? _cancellationTokenSource;
        private MopidyNowPlayingState _mopidyNowPlayingState;
        private MopidyTrack? _mopidyCurrentTrack = null;

        private Task _monitorWebSocketTask;
        private Task _monitorTrackTimePositionTask;
        private Task _monitorEpisodeListPathTask;
        private Task _monitorBbcTrackInformationTask;

        public MopidyProxy(IHttpClientFactory httpClientFactory, FfmpegStreamService streamService, IConfiguration configuration, IOptions<RadioSelectorOptions> radioSelectorOptions, ILogger<MopidyProxy> logger)
        {
            _streamService = streamService;
            _configuration = configuration;
            _radioSelectorOptions = radioSelectorOptions.Value;
            _logger = logger;
            _logger.LogInformation($"{nameof(MopidyProxy)} starting");
            _client = httpClientFactory.CreateClient();
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            _mopidyWebSocketEndpoint = _configuration["Mopidy:WebSocketEndpoint"] ?? throw new ApplicationException("Mopidy:WebSocketEndpoint not configured");
            _mopidyNowPlayingState = MopidyNowPlayingState.Default;
            Rpc = new MopidyCaller(httpClientFactory, _configuration, logger);

            try
            {
                if (_cancellationTokenSource is not null)
                    _cancellationTokenSource.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                _monitorWebSocketTask = Task.Run(() => MonitorWebSocket(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                _monitorTrackTimePositionTask = Task.Run(() => MonitorTrackTimePosition(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                _monitorEpisodeListPathTask = Task.Run(() => MonitorEpisodeListPath(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                _monitorBbcTrackInformationTask = Task.Run(() => MonitorBbcTrackInformationTask(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"MopidyProxy could not connect to mopidy websocket on {_mopidyWebSocketEndpoint}", ex);
            }
        }

        public bool IsMopidyCurrentTrackRadioStream()
        {
            if (_mopidyCurrentTrack == null)
                return false;

            var matchingStreamConfiguration = _radioSelectorOptions.RadioStreams.FirstOrDefault(stream => stream.Uri == _mopidyCurrentTrack.Uri);

            return matchingStreamConfiguration == null;
        }

        private async Task MonitorTrackTimePosition(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, cancellationToken);

                    if (_mopidyNowPlayingState.IsPlaying)
                    {
                        var trackTimePosition = await Rpc.GetTimePositionMs();
                        _mopidyNowPlayingState = _mopidyNowPlayingState with { TrackPositionMs = trackTimePosition };
                        RaiseEvent(_mopidyNowPlayingState);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation($"Stopping {nameof(MonitorTrackTimePosition)}.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing time position message from mopidy.");
                }
            }
        }

        private async Task MonitorWebSocket(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _mopidyNowPlayingState = await GetNowPlaying();

                    var cws = new ClientWebSocket();
                    cws.Options.CollectHttpResponseDetails = true;
                    await cws.ConnectAsync(new Uri(_mopidyWebSocketEndpoint), CancellationToken.None);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var messageJson = "";
                        try
                        {
                            var array = new byte[16000];
                            var buffer = new ArraySegment<byte>(array);
                            var received = await cws.ReceiveAsync(buffer, cancellationToken);
                            if (received.EndOfMessage)
                            {
                                messageJson = Encoding.UTF8.GetString(buffer);
                                if (messageJson.Contains('\0'))
                                {
                                    messageJson = messageJson.Substring(0, messageJson.IndexOf('\0'));
                                }

                                var message = JsonSerializer.Deserialize<MopidyBaseMessage>(messageJson, _serialiserOptions);

                                if (message != null)
                                {
                                    if (message.Event == "playback_state_changed")
                                    {
                                        var playbackStateChangedMessage = JsonSerializer.Deserialize<MopidyPlaybackStateChangedMessage>(messageJson, _serialiserOptions);
                                        _mopidyNowPlayingState = _mopidyNowPlayingState with { IsPlaying = playbackStateChangedMessage!.NewState == "playing" };

                                        // xyzzy var isFfmegStream = _mopidyCurrentTrack?.Uri == _ffmpegStreamUri;
                                        var isFfmegStream = _streamService.IsActive(out var streamStationId);
                                        if (!_mopidyNowPlayingState.IsPlaying && isFfmegStream)
                                        {
                                            // BBC stream has become paused, and it is an ffmpeg stream, so stop ffmpeg.
                                            _logger.LogInformation($"Stopping HLS demuxer for station {streamStationId} because received {playbackStateChangedMessage!.NewState} event.");
                                            //await StopFfmpegHlsDemuxer(cancellationToken);
                                            _streamService.Stop();
                                        }

                                        RaiseEvent(_mopidyNowPlayingState);
                                    }
                                    else if (new[] { "track_playback_started", "track_playback_paused", "track_playback_resumed" }.Contains(message.Event))
                                    {
                                        var isPaused = message.Event == "track_playback_paused";

                                        var playbackStartedMessage = JsonSerializer.Deserialize<MopidyTrackPlaybackStartedMessage>(messageJson, _serialiserOptions);
                                        _mopidyCurrentTrack = playbackStartedMessage?.TlTrack.Track;
                                        var nowPlayingDisplayName = GetNowPlayingDisplayName(_mopidyCurrentTrack);

                                        // Common state items
                                        _mopidyNowPlayingState = _mopidyNowPlayingState with { TrackUri = _mopidyCurrentTrack?.Uri ?? "file:///", TrackLengthMs = _mopidyCurrentTrack?.Length ?? -1 };

                                        // xyzzy tidy
                                        // State items which depend on source (BBC radio stream, or not)
                                        //  _mopidyNowPlayingState = IsMopidyCurrentTrackBbcRadioStream(out var bbcRadioStreamMopidyItem)
                                        //      ? await UpdateMopidyNowPlayingStateFromBbc(cancellationToken, bbcRadioStreamMopidyItem, includeTrackDescription: !isPaused)
                                        //      : _mopidyNowPlayingState with { TrackName = nowPlayingDisplayName, TrackDescription = _mopidyCurrentTrack?.Comment ?? "", TrackUri = _mopidyCurrentTrack?.Uri ?? "file:///", TrackLengthMs = _mopidyCurrentTrack?.Length ?? -1 };

                                        // State items which depend on source (BBC radio stream, or not)
                                        if (IsMopidyCurrentTrackBbcRadioStream(out var bbcRadioStreamMopidyItem))
                                        {
                                            _mopidyNowPlayingState = await UpdateMopidyNowPlayingStateFromBbc(cancellationToken, bbcRadioStreamMopidyItem, includeTrackDescription: !isPaused);

                                            // var isFfmegStream = _mopidyCurrentTrack?.Uri == _ffmpegStreamUri;
                                            var isFfmegStream = _streamService.IsActive(out var streamStationId);
                                            if (isPaused && isFfmegStream)
                                            {
                                                // BBC stream has become paused, and it is an ffmpeg stream, so stop ffmpeg.
                                                _logger.LogInformation($"Stopping HLS demuxer for station {streamStationId} because received track_playback_paused event.");
                                                //await StopFfmpegHlsDemuxer(cancellationToken);
                                                _streamService.Stop();
                                            }
                                            // else if (!isFfmegStream)
                                            // {
                                            //     // BBC stream has started/resumed, and is not already an ffmpeg stream, so start ffmpeg.
                                            //     _logger.LogInformation($"Starting HLS demuxer for station {streamStationId} because received {message.Event} event for stream {_mopidyCurrentTrack?.Uri}.");
                                            //     await InjectFfmpegHlsDemuxer(bbcRadioStreamMopidyItem, cancellationToken);
                                            // }
                                        }
                                        else
                                        {
                                            // Not a BBC Radio Stream so end any active ffmpeg stream.
                                            _streamService.Stop();
                                            
                                            // xyzzy tidy
                                            // if (!string.IsNullOrEmpty(_ffmpegStreamUri))
                                            // {
                                            //     // Non-BBC streams do not use the ffmpeg HLS demuxer.
                                            //     _logger.LogInformation($"Stopping HLS demuxer stream {_ffmpegStreamUri} for new stream {_mopidyCurrentTrack?.Uri}.");
                                            //     await StopFfmpegHlsDemuxer(cancellationToken);
                                            // }

                                            _mopidyNowPlayingState = _mopidyNowPlayingState with { TrackName = nowPlayingDisplayName, TrackDescription = _mopidyCurrentTrack?.Comment ?? "", TrackUri = _mopidyCurrentTrack?.Uri ?? "file:///", TrackLengthMs = _mopidyCurrentTrack?.Length ?? -1 };
                                        }

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
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            if (string.IsNullOrEmpty(messageJson))
                            {
                                throw;
                            }
                            else
                            {
                                _logger.LogError(ex, $"Error processing websocket message from mopidy: '{messageJson}'");
                            }

                            await Task.Delay(10000, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation($"Stopping {nameof(MonitorWebSocket)}.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in websocket communication with mopidy");
                    await Task.Delay(10000, cancellationToken);
                }
            }
        }

        // xyzzy move
        private async Task InjectFfmpegHlsDemuxer(MopidyItem episode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(episode.BbcStationId) || string.IsNullOrEmpty(episode.ExternalUri))
            {
                _logger.LogError("Tried to use stream service on a stream with no ExternalUri configured: '{EpisodeUri}'.", episode.Uri);
                return;
            }
            
            _streamService.Start(episode.BbcStationId, episode.ExternalUri);
        }

        // xyzzy move
        private async Task StopFfmpegHlsDemuxer(CancellationToken cancellationToken)
        {
            _streamService.Stop();
        }

        private record MopidyTrack(string Uri, string Name, string Comment, int Length);

        private record MopidyTlTrack(MopidyTrack Track);

        private record MopidyTrackPlaybackStartedMessage(string Event) : MopidyBaseMessage(Event)
        {
            [JsonPropertyName("tl_track")] public MopidyTlTrack TlTrack { get; init; } = default!;
        }

        private record MopidyPlaybackStateChangedMessage(string Event) : MopidyBaseMessage(Event)
        {
            [JsonPropertyName("new_state")] public string NewState { get; init; } = default!;
        }

        private record MopidyStreamTitleChangedMessage(string Event, string Title) : MopidyBaseMessage(Event);

        private record MopidyBaseMessage(string Event);

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
                var currentTrackResponseTask = Rpc.GetCurrentTrack();
                var stateResponseTask = Rpc.GetState();
                var trackPositionTask = Rpc.GetTimePositionMs();
                var nowPlaying = await currentTrackResponseTask;
                var state = await stateResponseTask;
                var trackPosition = await trackPositionTask;

                var isPlaying = state.Result == "playing";
                var nowPlayingName = GetNowPlayingDisplayName(nowPlaying);
                return new MopidyNowPlayingState(isPlaying, nowPlayingName, nowPlaying?.Result?.Comment ?? "", nowPlaying?.Result?.Uri ?? "file:///", trackPosition, nowPlaying?.Result?.Length ?? -1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNowPlaying");
                return MopidyNowPlayingState.Default;
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
            if (mopidyTrack == null)
                return "";

            var matchingStreamConfiguration = _radioSelectorOptions.RadioStreams.FirstOrDefault(stream => stream.Uri == mopidyTrack.Uri);
            if (matchingStreamConfiguration != null)
                return matchingStreamConfiguration.Name.TrimStart('*', ' '); // Favourite stations have * prefix.

            if (!string.IsNullOrWhiteSpace(mopidyTrack.Name))
                return mopidyTrack.Name;

            return mopidyTrack.Uri?.Split('/').Last().Split('.').First().Split('%').First().Replace('_', ' ').Replace('-', ' ')
                   ?? "";
        }

        private async Task MonitorEpisodeListPath(CancellationToken cancellationToken)
        {
            var directoryToMonitor = _configuration["Mopidy:EpisodeList:Path"];
            if (directoryToMonitor == null || !Directory.Exists(directoryToMonitor))
            {
                _logger.LogError($"Cannot monitor directory '{directoryToMonitor}' because it does not exist.");
                return;
            }

            _logger.LogInformation($"Started directory monitor on '{directoryToMonitor}'.");

            // Does the episode list from Mopidy match what was found looking
            // at the directory directly? If not Mopidy should catch up soon so 
            // poll more quickly.
            bool mopidyEpisodeCountOutOfSync = true;
            // Looking at the directory directly, what was found in the previous
            // loop, allows detection of new files.
            HashSet<string> knownFilesInDirectory = new(StringComparer.Ordinal);
            DateTime mopidyLastSyncUtc = DateTime.UtcNow;

            const int longLoopDelayMs = 60000;
            const int shortLoopDelayMs = 10000;
            const int maxDelayBeforeMopidySyncSeconds = 600;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Checking local directory changes is cheap and helps avoid calling Mopidy GetEpisodes.
                    var currentFilesInDirectory = FilesInDirectory(directoryToMonitor).ToList();
                    var directoryContentChanged = DirectoryContentsChangedSincePreviousLoop(currentFilesInDirectory);

                    // GetEpisodes is an expensive call, avoid calling it on every loop.
                    var shouldGetEpisodesFromMopidy = directoryContentChanged
                                                      || mopidyEpisodeCountOutOfSync
                                                      || DateTime.UtcNow.Subtract(mopidyLastSyncUtc).TotalSeconds > maxDelayBeforeMopidySyncSeconds;

                    if (shouldGetEpisodesFromMopidy)
                    {
                        var mopidyEpisodes = await Rpc.GetEpisodes();
                        mopidyLastSyncUtc = DateTime.UtcNow;

                        mopidyEpisodeCountOutOfSync = mopidyEpisodes.Count != currentFilesInDirectory.Count;
                        if (mopidyEpisodeCountOutOfSync)
                            _logger.LogWarning($"Mopidy episode count is out of sync with M4A file count in {nameof(MonitorEpisodeListPath)}. Mopidy {mopidyEpisodes.Count} episodes. Directory {currentFilesInDirectory.Count} files.");

                        _logger.LogInformation("Raising episode list event.");
                        RaiseEpisodeListEvent_WithDebounce(mopidyEpisodes);
                    }

                    await Task.Delay(mopidyEpisodeCountOutOfSync ? shortLoopDelayMs : longLoopDelayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error {nameof(MonitorEpisodeListPath)} in polling loop.");
                    await Task.Delay(longLoopDelayMs, cancellationToken);
                }
            }


            bool DirectoryContentsChangedSincePreviousLoop(IList<string> actualM4AFilesInDirectory)
            {
                var filesInDirectory = new HashSet<string>(actualM4AFilesInDirectory, StringComparer.Ordinal);
                var newFiles = filesInDirectory.Except(knownFilesInDirectory);
                var disappearedFiles = knownFilesInDirectory.Except(filesInDirectory);

                knownFilesInDirectory = filesInDirectory;

                return newFiles.Any() || disappearedFiles.Any();
            }

            IEnumerable<string> FilesInDirectory(string directory)
            {
                var current = Directory
                    .EnumerateFiles(directory, "*", SearchOption.AllDirectories);

                // Mopidy GetEpisodes appears to return all files so I don't need this filter. 
                // .Where(f =>
                //     Path.GetExtension(f).Equals(".m4a", StringComparison.OrdinalIgnoreCase)
                //     && !f.EndsWith(".partial.m4a", StringComparison.OrdinalIgnoreCase)
                //);
                return current;
            }
        }

        private void RaiseEvent(MopidyNowPlayingState mopidyNowPlayingState)
        {
            OnMopidyNotification?.Invoke(null!, new MopidyNotificationEventArgs(mopidyNowPlayingState));
        }

        private Timer? _debounceTimer;
        private readonly Lock _debounceLock = new();
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(5000);

        private void RaiseEpisodeListEvent_WithDebounce(IList<MopidyItem> episodes)
        {
            lock (_debounceLock)
            {
                _debounceTimer?.Dispose();

                _debounceTimer = new Timer(_ =>
                {
                    try
                    {
                        _logger.LogInformation("Debounce timer fired, raising episode list event");
                        OnMopidyEpisodeListNotification?.Invoke(null!, new MopidyEpisodeListNotificationEventArgs(episodes));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during debounced RaiseEpisodeListEvent");
                    }
                }, null, DebounceDelay, Timeout.InfiniteTimeSpan);
            }
        }

        public class StateDto
        {
            public string? Result { get; set; }
        }

        public class TimePositionResponse
        {
            public int Result { get; set; }
        }

        public class NowPlayingResponse
        {
            public required NowPlayingDto Result { get; set; }
        }

        public class NowPlayingDto
        {
            public required string State { get; set; }

            public required string Name { get; set; }

            public required string Uri { get; set; }

            public required string Comment { get; set; }

            public required string Date { get; set; }

            public required int Length { get; set; }
        }

        public class MopidyItems
        {
            public required IList<MopidyItem> Result { get; set; }
        }

        public class MopidyItem
        {
            public required string Name { get; set; }
            public required string Uri { get; set; }
            public string? ExternalUri { get; set; }
            public string? BbcStationId { get; set; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _cancellationTokenSource?.Cancel();
                }

                _disposedValue = true;
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

        public record MopidyNowPlayingState(bool IsPlaying, string TrackName, string TrackDescription, string TrackUri, int TrackPositionMs, int TrackLengthMs)
        {
            public static MopidyNowPlayingState Default => new(false, "", "", "file:///", -1, -1);
        }

        public class MopidyNotificationEventArgs : EventArgs
        {
            private readonly MopidyNowPlayingState _mopidyNowPlayingState;

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
            public IList<MopidyItem> Episodes { get; set; }

            public MopidyEpisodeListNotificationEventArgs(IList<MopidyItem> episodeList)
            {
                Episodes = episodeList;
            }
        }
    }
}