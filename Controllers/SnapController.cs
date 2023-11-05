using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PiperPicker.Proxies;

namespace PiperPicker.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SnapController : ControllerBase
    {
        public SnapController()
        {
            //_stateHubContext = hubContext;
        }

        [HttpPost("snapclientmute")]
        public async Task<ActionResult> PostSnapClientMute([FromBody] PostSnapClientMuteDto dto)
        {
            SnapProxy.SetMute(dto.ClientMac, dto.Muted);

            return new JsonResult(new { Result = "ok" });
        }

        [HttpPost("snapclientglobalvolume")]
        public async Task<ActionResult> PostSnapClientGlobalVolume([FromBody] PostSnapClientGlobalVolumeDto dto)
        {
            // var snapClients = SnapProxy.GetSnapClients();
            // foreach (var client in snapClients)
            // {
            //     SnapProxy.SetVolume(client.Mac, dto.PercentagePointChange);
            // }

            return new JsonResult(new { Result = "ok" });
        }

        public class PostSnapClientMuteDto
        {
            public string ClientMac { get; set; }
            public bool Muted { get; set; }
        }

        public class PostSnapClientGlobalVolumeDto
        {
            public int PercentagePointChange { get; set; }
        }
    }
}