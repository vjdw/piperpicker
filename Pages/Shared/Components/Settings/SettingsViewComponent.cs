using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using piperpicker.Database;
using PiperPicker.Models;
using PiperPicker.Proxies;

namespace PiperPicker.Pages.Components.Light
{
    public class SettingsViewComponent : ViewComponent
    {
        private ILiteDatabase _db;

        public static string Name => nameof(SettingsViewComponent).Replace("ViewComponent", "");

        public SettingsViewComponent(ILiteDatabase db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync(string hostname)
        {
            var dbLights = _db.GetCollection<LightControllerEntity>().Query();

            var lightSettings = await Task.WhenAll(dbLights
                    .ToEnumerable()
                    .Select(async _ =>
                    {
                        var lightSetting = new LightSetting
                        {
                            Hostname = _.Hostname,
                        };

                        try
                        {
                            lightSetting.State = await LightingProxy.GetState(_.Hostname);
                        }
                        catch
                        {
                            lightSetting.State = new LightingProxy.LightStateDto();
                        }

                        return lightSetting;
                    }));

            var model = new SettingsModel
            {
                LightSettings = lightSettings
            };

            return View("Default", model);
        }

        public IActionResult OnPostLightController()
        {
            return new ViewComponentResult() { ViewComponentType = typeof(SettingsViewComponent) };
        }
    }
}
