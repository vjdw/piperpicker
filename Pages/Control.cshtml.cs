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
using PiperPicker.Proxies;
using static PiperPicker.Controllers.MopidyController;

namespace PiperPicker.Pages
{
    [BindProperties]
    public class ControlModel : PageModel
    {
        HttpClient _client = new HttpClient();

        public ControlModel() { }

        public async Task<IActionResult> OnGetCurrentTrackAsync()
        {
            return new PartialViewResult()
            {
                ViewName = "_CurrentTrack",
                    ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = await MopidyProxy.GetCurrentTrack() }
            };
        }

        public async Task<IActionResult> OnGetSnapClientsAsync()
        {
            return new PartialViewResult()
            {
                ViewName = "_SnapClients",
                    ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = await SnapProxy.GetSnapClients() }
            };
        }
    }
}