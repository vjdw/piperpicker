using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PiperPicker.Proxies
{
    public static class MopidyProxy
    {
        private static HttpClient _client = new HttpClient();

        public static async Task<StateDto> GetState()
        {
            var responseContent = await MopidyPost("core.playback.get_state");
            return JsonConvert.DeserializeObject<StateDto>(responseContent);
        }

        public static async Task<CurrentTrackDto> GetCurrentTrack()
        {
            var responseContent = await MopidyPost("core.playback.get_current_track");
            var mopidyResponse = JObject.Parse(responseContent);
            return mopidyResponse["result"].ToObject<CurrentTrackDto>();
        }

        public static async Task<IList<Episode>> GetEpisodes()
        {
            var responseContent = await MopidyPost("core.library.browse", "file:///media/data/get-iplayer-downloads");
            var episodeList = JsonConvert.DeserializeObject<EpisodeList>(responseContent);
            return episodeList.Result;
        }

        public static async Task PlayEpisode(string episodeUri)
        {
            await ClearQueue();
            await MopidyPost("core.tracklist.add", episodeUri);
            await Play();
        }

        public static async Task ClearQueue()
        {
            await MopidyPost("core.tracklist.clear");
        }

        public static async Task Play()
        {
            await MopidyPost("core.playback.play");
        }

        public static async Task TogglePause()
        {
            var state = await GetState();
            if (state.Result == "playing")
            {
                await MopidyPost("core.playback.pause");
            }
            else
            {
                await MopidyPost("core.playback.play");
            }
        }

        private static async Task<string> MopidyPost(string method, string targetUri = null)
        {
            var content = new StringContent($"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"{method}\" {(string.IsNullOrEmpty(targetUri) ? "" : $", \"params\":{{\"uri\":\"{targetUri}\"}}")} }}");
            var response = await _client.PostAsync("http://hunchcorn:6680/mopidy/rpc", content);
            return await response.Content.ReadAsStringAsync();
        }

        [JsonObject]
        public class PlayEpisodeDto
        {
            [JsonProperty]
            public string Uri {get; set;}
        }

        [JsonObject]
        public class StateDto
        {
            [JsonProperty]
            public string Result {get; set;}
        }

        [JsonObject]
        public class MopidyResponse
        {
            [JsonProperty]
            public dynamic Result {get; set;}
        }

        [JsonObject]
        public class CurrentTrackDto
        {
            [JsonProperty]
            public string Name {get; set;}
            [JsonProperty]
            public string Uri {get; set;}
            [JsonProperty]
            public string Comment {get; set;}
            [JsonProperty]
            public string Date {get; set;}
        }

        public class EpisodeList
        {
            public IList<Episode> Result {get; set;}
        }
        public class Episode
        {
            public string Name {get; set;}
            public string Uri {get; set;}
        }
    }
}