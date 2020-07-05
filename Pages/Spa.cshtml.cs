using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using PiperPicker.Models;
using PiperPicker.Pages.Components.Light;
using PiperPicker.Pages.Components.Lighting;
using PiperPicker.Pages.Components.NowPlaying;
using PiperPicker.Pages.Components.SnapClient;

namespace PiperPicker.Pages
{
    [BindProperties]
    public class SpaModel : PageModel
    {
        private IConfiguration _configuration;

        public SpaModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

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
                ViewData = new ViewDataDictionary<RadioModel>(ViewData, new RadioModel(_configuration))
            };
        }

        public IActionResult OnGetEpisodesPartial()
        {
            return new PartialViewResult
            {
                ViewName = "_EpisodesPartial",
                ViewData = new ViewDataDictionary<EpisodesModel>(ViewData, new EpisodesModel(_configuration["Mopidy:EpisodeList:StartWithFilter"]))
            };
        }

        public IActionResult OnGetLightingView()
        {
            return new ViewComponentResult() { ViewComponentName = LightingViewComponent.Name };
        }

        public IActionResult OnGetSettingsView()
        {
            return new ViewComponentResult() { ViewComponentName = SettingsViewComponent.Name };
        }
    }
}