using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using piperpicker.Database;
using PiperPicker.Models;

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

            var model = new SettingsModel
            {
                LightingHostnames = dbLights.Select(_ => _.Hostname).ToEnumerable()
            };

            return View("Default", model);
        }

        public IActionResult OnPostLightController()
        {
            return new ViewComponentResult() { ViewComponentType = typeof(SettingsViewComponent) };
        }
    }
}
