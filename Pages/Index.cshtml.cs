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

namespace piperpicker.Pages
{
    [BindProperties]
    public class IndexModel : PageModel
    {
        HttpClient _client = new HttpClient();

        public IndexModel()
        {
            Episodes = GetEpisodes().GetAwaiter().GetResult();
        }

        public IEnumerable<Episode> Episodes { get; set; }

        public async Task<IEnumerable<Episode>> GetEpisodes()
        {
            var content = new StringContent($"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"core.library.browse\", \"params\":{{\"uri\":\"file:///media/data/get-iplayer-downloads\"}} }}");
            
            var response = await _client.PostAsync("http://hunchcorn:6680/mopidy/rpc", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            //var responseObject = JObject.Parse(responseContent);
            var episodeList = JsonConvert.DeserializeObject<EpisodeList>(responseContent);
            return episodeList.Result;
        }

        public async Task<ActionResult> OnPostPlayEpisode([FromBody]PlayEpisodeDto data)
        {
            await ClearQueue();

            // core.playback.play
            var content = new StringContent($"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"core.tracklist.add\", \"params\":{{\"uri\":\"{data.Uri}\"}} }}");
            var response = await _client.PostAsync("http://hunchcorn:6680/mopidy/rpc", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            await Play();

            return new JsonResult("{}");
        }

        private async Task ClearQueue()
        {
            // core.tracklist.clear
            var content = new StringContent($"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"core.tracklist.clear\" }}");
            
            var response = await _client.PostAsync("http://hunchcorn:6680/mopidy/rpc", content);
            var responseContent = await response.Content.ReadAsStringAsync();
        }

        private async Task Play()
        {
            // core.tracklist.clear
            var content = new StringContent($"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"core.playback.play\" }}");
            var response = await _client.PostAsync("http://hunchcorn:6680/mopidy/rpc", content);
            var responseContent = await response.Content.ReadAsStringAsync();
        }

        public void OnGet()
        {

        }
    }

    [JsonObject]
    public class PlayEpisodeDto
    {
        [JsonProperty]
        public string Uri {get; set;}
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
