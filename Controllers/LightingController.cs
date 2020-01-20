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
            await LightingProxy.SetColour(data.red, data.green, data.blue, data.white);
            return new JsonResult(new { Result = "ok" });
        }

        [JsonObject]
        public class ColourDto
        {
            [JsonProperty]
            public int red { get; set; }
            [JsonProperty]
            public int green { get; set; }
            [JsonProperty]
            public int blue { get; set; }
            [JsonProperty]
            public int white { get; set; }
        }
    }
}