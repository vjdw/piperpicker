using System.Text.Json;

namespace PiperPicker.Proxies;

public partial class MopidyProxy
{
    /// <summary>
    /// Inner class wrapping Mopidy HTTP API calls.
    /// </summary>
    public class MopidyCaller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly HttpClient _client;
        private readonly string _mopidyEndpoint;
        private readonly Random _random = new ();

        public MopidyCaller(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
            _client = httpClientFactory.CreateClient();
            _mopidyEndpoint = _configuration["Mopidy:Endpoint"] ?? throw new ApplicationException("Mopidy:Endpoint not configured");
        }

        public async Task PlayEpisode(MopidyItem episode)
        {
            await ClearQueue();
            await MopidyPost("core.tracklist.add", new string[] { episode.Uri });
            await Play();
        }

        public async Task PlayRandomEpisode()
        {
            var episodes = await GetEpisodes();
            var randomEpisode = episodes.ElementAt(_random.Next(0, episodes.Count()));
            await PlayEpisode(randomEpisode);
        }
        
        public async Task ClearQueue()
        {
            await MopidyPost("core.tracklist.clear");
        }
        
        public async Task Play()
        {
            await MopidyPost("core.playback.play");
        }

        public async Task Pause()
        {
            await MopidyPost("core.playback.pause");
        }

        public async Task<string> TogglePause()
        {
            var state = await GetState();
            if (state.Result == "playing")
            {
                await Pause();
                return "paused";
            }
            else
            {
                await Play();
                return "playing";
            }
        }

        public async Task SeekRelative(int seekMs)
        {
            var responseContent = await MopidyPost("core.playback.get_time_position");
            var timePositionResponse = JsonSerializer.Deserialize<TimePositionResponse>(responseContent, _serialiserOptions);
            if (timePositionResponse != null)
            {
                var newPositionMs = timePositionResponse.Result + seekMs;
                await MopidyPost("core.playback.seek", seekPositionMs: newPositionMs);
            }
        }

        public async Task<int> GetTimePositionMs()
        {
            try
            {
                var timePositionResponseContent = await MopidyPost("core.playback.get_time_position");
                var timePositionResponse = JsonSerializer.Deserialize<TimePositionResponse>(timePositionResponseContent, _serialiserOptions);
                return timePositionResponse?.Result ?? -1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTimePositionMs");
                return -1;
            }
        }

        public async Task<StateDto> GetState()
        {
            var responseContent = await MopidyPost("core.playback.get_state");
            return JsonSerializer.Deserialize<StateDto>(responseContent, _serialiserOptions) ?? new StateDto();
        }

        public async Task<NowPlayingResponse?> GetCurrentTrack()
        {
            var currentTrackResponse = await MopidyPost("core.playback.get_current_track");
            var nowPlaying = JsonSerializer.Deserialize<NowPlayingResponse>(currentTrackResponse, _serialiserOptions);
            return nowPlaying;
        }

        public async Task<IList<MopidyItem>> GetEpisodes()
        {
            var pathInMopidyFormat = $"file:///{_configuration["Mopidy:EpisodeList:Path"]}";
            await MopidyPost("core.library.refresh", pathInMopidyFormat);
            var responseContent = await MopidyPost("core.library.browse", pathInMopidyFormat);
            var mopidyItems = JsonSerializer.Deserialize<MopidyItems>(responseContent, _serialiserOptions);

            if (mopidyItems == null)
            {
                _logger.LogError($"Deserialising response from Mopidy core.library.browse gave null result.");
                return new List<MopidyItem>();
            }

            return mopidyItems.Result;
        }

        private async Task<string> MopidyPost(string method, string[] targetUris)
        {
            var requestContent = $"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"{method}\", \"params\":{{\"uris\":{JsonSerializer.Serialize(targetUris, _serialiserOptions)}}} }}";
            var content = new StringContent(requestContent);
            content.Headers.Clear();
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await _client.PostAsync($"{_mopidyEndpoint}", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        private async Task<string> MopidyPost(string method)
        {
            var requestContent = $"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"{method}\" }}";
            return await MopidyPostActual(requestContent);
        }

        private async Task<string> MopidyPost(string method, string targetUri)
        {
            var requestContent = $"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"{method}\", {$"\"params\":{{\"uri\":\"{targetUri}\"}}"} }}";
            return await MopidyPostActual(requestContent);
        }

        private async Task<string> MopidyPost(string method, int seekPositionMs)
        {
            var requestContent = $"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"{method}\", {$"\"params\":{{\"time_position\":{seekPositionMs}}}"} }}";
            return await MopidyPostActual(requestContent);
        }

        private async Task<string> MopidyPostActual(string requestContent)
        {
            var content = new StringContent(requestContent);
            content.Headers.Clear();
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            var response = await _client.PostAsync($"{_mopidyEndpoint}", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        private readonly JsonSerializerOptions _serialiserOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }
}