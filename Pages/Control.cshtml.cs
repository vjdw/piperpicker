using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

        public ControlModel()
        {
            Task.Run(async () => {
                Status = await MopidyProxy.GetCurrentTrack();
            }).Wait();
        }

        public MopidyProxy.CurrentTrackDto Status {get; set;}
    }
}
