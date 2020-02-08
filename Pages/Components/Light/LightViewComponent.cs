using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PiperPicker.Models;
using PiperPicker.Proxies;
using static PiperPicker.Models.LightingModel;

namespace PiperPicker.Pages.Components.Light
{
    public class LightViewComponent : ViewComponent
    {
        public static string Name => nameof(LightViewComponent).Replace("ViewComponent", "");

        public LightViewComponent() { }

        public async Task<IViewComponentResult> InvokeAsync(string hostname)
        {
            var state = await LightingProxy.GetState(hostname);

            var model = new LightModel
            {
                Hostname = hostname,
                Mode = state.Mode.ToString()
            };

            return View("Default", model);
        }
    }
}
