using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PiperPicker.Models;
using PiperPicker.Proxies;
using static PiperPicker.Models.LightingModel;
using static PiperPicker.Proxies.LightingProxy;

namespace PiperPicker.Pages.Components.Light
{
    public class LightViewComponent : ViewComponent
    {
        public static string Name => nameof(LightViewComponent).Replace("ViewComponent", "");

        public LightViewComponent() { }

        public async Task<IViewComponentResult> InvokeAsync(string hostname)
        {
            var model = new LightModel
            {
                Hostname = hostname,
            };

            try
            {
                var state = await LightingProxy.GetState(hostname);
                model.Mode = state.Mode.ToString();
                model.StaticColourHexCode = state.StaticColourHexCode;
            }
            catch (Exception e)
            {
                model.Error = e.Message;
            }

            return View("Default", model);
        }
    }
}
