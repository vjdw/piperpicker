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
using PiperPicker.Proxies;

namespace PiperPicker.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MopidyController : ControllerBase
    {
        [HttpGet("episodes")]
        public async Task<ActionResult<IList<Episode>>> GetEpisodes()
        {
            return new JsonResult(await MopidyProxy.GetEpisodes());
        }

        [HttpPost("playepisode")]
        public async Task<ActionResult> PostPlayEpisode([FromBody]PlayEpisodeDto data)
        {
            await MopidyProxy.ClearQueue();
            await MopidyProxy.PlayEpisode(data.Uri);
            await MopidyProxy.Play();

            return new JsonResult(new {Result = "ok"});
        }

        [HttpPost("playrandomepisode")]
        public async Task<ActionResult> PostPlayRandomEpisode()
        {
            await MopidyProxy.PlayRandomEpisode();

            return new JsonResult(new { Result = "ok" });
        }

        [HttpPost("clear")]
        public async Task<ActionResult> PostClearQueue()
        {
            await MopidyProxy.ClearQueue();

            return new JsonResult(new {Result = "ok"});
        }

        [HttpPost("play")]
        public async Task<ActionResult> PostPlay()
        {
            await MopidyProxy.Play();

            return new JsonResult(new {Result = "ok"});
        }

        [HttpPost("togglepause")]
        public async Task<ActionResult> PostTogglePause()
        {
            var newPlayingState = await MopidyProxy.TogglePause();

            return new JsonResult(new {Result = "ok", State = newPlayingState});
        }

        [JsonObject]
        public class PlayEpisodeDto
        {
            [JsonProperty]
            public string Uri {get; set;}
        }

        public class Episode
        {
            public string Name {get; set;}
            public string Uri {get; set;}
        }
    }
}