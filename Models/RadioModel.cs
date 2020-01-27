using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static PiperPicker.Proxies.MopidyProxy;

namespace PiperPicker.Models
{
    [BindProperties]
    public class RadioModel : PageModel
    {
        HttpClient _client = new HttpClient();

        public RadioModel()
        {
            Stations = new [] {
                new MopidyItem { Name = "Classic FM", Uri = "tunein:station:s8439"},
                new MopidyItem { Name = "BBC Radio 2", Uri = "tunein:station:s24940"},
                new MopidyItem { Name = "BBC Radio 3", Uri = "tunein:station:s24941"},
                new MopidyItem { Name = "BBC Radio 4", Uri = "tunein:station:s25419"},
                new MopidyItem { Name = "BBC Radio 6", Uri = "tunein:station:s44491"},
                new MopidyItem { Name = "BBC Sussex", Uri = "tunein:station:s46590"},
                new MopidyItem { Name = "Eagle Radio", Uri = "tunein:station:s45515"}
            };
        }

        public IEnumerable<MopidyItem> Stations { get; set; }
    }
}