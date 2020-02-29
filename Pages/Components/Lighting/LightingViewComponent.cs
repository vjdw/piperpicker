using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PiperPicker.Models;
using PiperPicker.Proxies;
using static PiperPicker.Models.LightingModel;

namespace PiperPicker.Pages.Components.Lighting
{
    public class LightingViewComponent : ViewComponent
    {
        public static string Name => nameof(LightingViewComponent).Replace("ViewComponent","");

        public LightingViewComponent() { }
        
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var lightingModel = new LightingModel
            {
                LightControllers = new [] {
                    new LightControllerModel { Hostname = "192.168.1.52" },
                    new LightControllerModel { Hostname = "ambilight" }
                }
            };

            return View("Default", lightingModel);
        }
    }
}