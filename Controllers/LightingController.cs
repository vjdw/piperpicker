using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PiperPicker.Proxies;

namespace piperpicker.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LightingController : ControllerBase
    {
        [HttpPost("colour")]
        public async Task<ActionResult> PostColour([FromBody]ColourDto data)
        {
            await LightingProxy.SetStaticColour(data.Hostname, data.Red, data.Green, data.Blue, data.White);
            return new JsonResult(new { Result = "ok" });
        }

        [HttpPost("mode")]
        public async Task<ActionResult> PostMode([FromBody]ModeDto data)
        {
            var mode = (LightingProxy.Mode)Enum.Parse(typeof(LightingProxy.Mode), data.Mode);
            await LightingProxy.SetMode(data.Hostname, mode);
            return new JsonResult(new { Result = "ok" });
        }

        [JsonObject]
        public class LightingDto
        {
            [JsonProperty]
            public string Hostname { get; set; }
        }

        [JsonObject]
        public class ColourDto : LightingDto
        {
            [JsonProperty]
            public int Red { get; set; }
            [JsonProperty]
            public int Green { get; set; }
            [JsonProperty]
            public int Blue { get; set; }
            [JsonProperty]
            public int White { get; set; }
        }

        [JsonObject]
        public class ModeDto : LightingDto
        {
            [JsonProperty]
            public string Mode { get; set; }
        }
    }
}