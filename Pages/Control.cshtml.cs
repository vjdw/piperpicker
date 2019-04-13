using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiperPicker.Pages.Components.SnapClient;
using PiperPicker.Proxies;
using static PiperPicker.Controllers.MopidyController;

namespace PiperPicker.Pages
{
    [BindProperties]
    public class ControlModel : PageModel
    {
        HttpClient _client = new HttpClient();

        public ControlModel() { }

        public IActionResult OnGetSnapClientsView()
        {
            return new ViewComponentResult() { ViewComponentName = SnapClientsViewComponent.Name };
        }

        public IActionResult OnGetNowPlayingView()
        {
            return new ViewComponentResult() { ViewComponentName = NowPlayingViewComponent.Name };
        }

        public IActionResult OnGetPlaybackStateView()
        {
            return new ViewComponentResult() { ViewComponentName = PlaybackStateViewComponent.Name };
        }
    }
}