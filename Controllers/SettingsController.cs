using System;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using piperpicker.Database;
using PiperPicker.Pages.Components.Light;

namespace PiperPicker.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private ILiteDatabase _db;

        public SettingsController(ILiteDatabase db)
        {
            _db = db;
        }

        [HttpPost("lightcontroller-add")]
        public async Task<ActionResult> PostLightControllerAdd([FromForm]LightControllerDto lightControllerDto)
        {
            var dbLights = _db.GetCollection<LightControllerEntity>();
            dbLights.Insert(new LightControllerEntity { Hostname = lightControllerDto.Hostname });

            return new ViewComponentResult() { ViewComponentName = SettingsViewComponent.Name };
        }

        [HttpPost("lightcontroller-remove")]
        public async Task<ActionResult> PostLightControllerRemove([FromForm]LightControllerDto lightControllerDto)
        {
            var dbLights = _db.GetCollection<LightControllerEntity>();
            dbLights.DeleteMany(_ => _.Hostname == lightControllerDto.Hostname);

            return new ViewComponentResult() { ViewComponentName = SettingsViewComponent.Name };
        }

        [JsonObject]
        public class LightControllerDto
        {
            [JsonProperty]
            public string Hostname { get; set; }
        }
    }
}