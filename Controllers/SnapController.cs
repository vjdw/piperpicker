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
    public class SnapController : ControllerBase
    {
        [HttpPost("snapclientmute")]
        public async Task<ActionResult> PostSnapClientMute([FromBody]PostSnapClientMuteDto dto)
        {
            await SnapProxy.SetMute(dto.ClientMac, dto.Muted);

            return new JsonResult(new {Result = "ok"});
        }

        public class PostSnapClientMuteDto
        {
            public string ClientMac { get; set; }
            public bool Muted { get; set; }
        }
    }
}