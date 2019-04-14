using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiperPicker.Hubs;
using PiperPicker.Proxies;

namespace PiperPicker.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SnapController : ControllerBase
    {
        private static IHubContext<StateHub> _stateHubContext;

        public SnapController(IHubContext<StateHub> hubContext)
        {
            _stateHubContext = hubContext;
        }

        [HttpPost("snapnotification")]
        public async Task<ActionResult> PostSnapNotification()
        {
            await _stateHubContext.Clients.All.SendAsync("SnapNotification", "xyzzy-movetoseparatecontroller?");

            return new JsonResult(new { Result = "ok" });
        }

        [HttpPost("snapclientmute")]
        public async Task<ActionResult> PostSnapClientMute([FromBody] PostSnapClientMuteDto dto)
        {
            SnapProxy.SetMute(dto.ClientMac, dto.Muted);

            return new JsonResult(new { Result = "ok" });
        }

        public class PostSnapClientMuteDto
        {
            public string ClientMac { get; set; }
            public bool Muted { get; set; }
        }
    }
}