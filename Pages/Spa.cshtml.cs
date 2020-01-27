using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PiperPicker.Pages.Components.NowPlaying;
using PiperPicker.Pages.Components.SnapClient;

namespace PiperPicker.Pages
{
    [BindProperties]
    public class SpaModel : PageModel
    {
        public IActionResult OnGetSnapClientsView()
        {
            return new ViewComponentResult() { ViewComponentName = SnapClientsViewComponent.Name };
        }

        public IActionResult OnGetNowPlayingView()
        {
            return new ViewComponentResult() { ViewComponentName = NowPlayingViewComponent.Name };
        }

        public IActionResult OnGetSnapCastPartial()
        {
            return new PartialViewResult
            {
                ViewName = "_SnapCastPartial",
                ViewData = this.ViewData
            };
        }

        public IActionResult OnGetRadioPartial()
        {
            return new PartialViewResult
            {
                ViewName = "_RadioPartial",
                ViewData = new ViewDataDictionary<RadioModel>(ViewData, new RadioModel())
            };
        }

        public IActionResult OnGetRadMacPartial()
        {
            return new PartialViewResult
            {
                ViewName = "_RadMacPartial",
                ViewData = new ViewDataDictionary<RadMacModel>(ViewData, new RadMacModel())
            };
        }

        public IActionResult OnGetLightingPartial()
        {
            return new PartialViewResult
            {
                ViewName = "_LightingPartial"
            };
        }
    }
}