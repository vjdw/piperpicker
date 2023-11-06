using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PiperPicker.Proxies
{
    public delegate void MopidyNotificationEventHandler(object source, MopidyNotificationEventArgs e);

    public class MopidyNotificationEventArgs : EventArgs
    {
        private string EventInfo;
        public MopidyNotificationEventArgs(string notification)
        {
            EventInfo = notification;
        }
        public string GetInfo()
        {
            return EventInfo;
        }
    }

    public class MopidyProxy
    {
        private string MopidyEndpoint;
        private HttpClient _client = default!;
        private Random _random = new Random();

        public event MopidyNotificationEventHandler OnMopidyNotification;
        public IConfiguration Configuration;
        public ILogger<MopidyProxy> Logger;

        public MopidyProxy(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MopidyProxy> logger)
        {
            _client = httpClientFactory.CreateClient();
            Configuration = configuration;
            Logger = logger;
        }

        public void Start()
        {
            
            Logger.LogInformation($"{nameof(MopidyProxy)} starting");
            MopidyEndpoint = Configuration["Mopidy:Endpoint"];
        }

        public async Task<StateDto> GetState()
        {
            var responseContent = await MopidyPost("core.playback.get_state");
            return JsonSerializer.Deserialize<StateDto>(responseContent);
        }

        // public async Task<NowPlayingDto> GetNowPlaying()
        // {
        //     var currentTrackResponseTask = MopidyPost("core.playback.get_current_track");
        //     var stateResponseTask = MopidyPost("core.playback.get_state");

        //     var currentTrackResponse = await currentTrackResponseTask;
        //     var mopidyResponse = JObject.Parse(currentTrackResponse);
        //     var nowPlaying = mopidyResponse["result"].ToObject<NowPlayingDto>()
        //         ?? new NowPlayingDto();
        //     nowPlaying.State = JsonConvert.DeserializeObject<StateDto>(await stateResponseTask).Result;

        //     return nowPlaying;
        // }

        public async Task<IList<MopidyItem>> GetEpisodes()
        {
            var responseContent = await MopidyPost("core.library.browse", Configuration["Mopidy:EpisodeList:Path"]);
            var mopidyItems = JsonSerializer.Deserialize<MopidyItems>(responseContent);
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

            RaiseEvent("play");
        }

        public async Task<string> TogglePause()
        {
            var state = await GetState();
            if (state.Result == "playing")
            {
                await MopidyPost("core.playback.pause");
                RaiseEvent("pause");
                return "paused";
            }
            else
            {
                await MopidyPost("core.playback.play");
                RaiseEvent("play");
                return "playing";
            }
        }

        private async Task<string> MopidyPost(string method, string[] targetUris)
        {
            var requestContent = $"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"{method}\", \"params\":{{\"uris\":{JsonSerializer.Serialize(targetUris)}}} }}";
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

        private void RaiseEvent(string info)
        {
            OnMopidyNotification?.Invoke(null, new MopidyNotificationEventArgs("playepisode"));
        }

        public class PlayMopidyItemDto
        {
            public string Uri { get; set; }
        }

        public class StateDto
        {
            public string Result { get; set; }
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
    }
}