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
                new MopidyItem { Name = "BBC Radio 2", Uri = "http://bbcmedia.ic.llnwd.net/stream/bbcmedia_radio2_mf_p"},
                new MopidyItem { Name = "BBC Radio 3", Uri = "http://bbcmedia.ic.llnwd.net/stream/bbcmedia_radio3_mf_p"},
                new MopidyItem { Name = "BBC Radio 4", Uri = "http://bbcmedia.ic.llnwd.net/stream/bbcmedia_radio4fm_mf_p"},
                new MopidyItem { Name = "BBC Radio 6", Uri = "http://bbcmedia.ic.llnwd.net/stream/bbcmedia_6music_mf_p"}
            };
        }

        public IEnumerable<MopidyItem> Stations { get; set; }
    }
}
