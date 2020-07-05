using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using static PiperPicker.Proxies.MopidyProxy;

namespace PiperPicker.Models
{
    [BindProperties]
    public class RadioModel : PageModel
    {
        public RadioModel(IConfiguration configuration)
        {
            Stations = configuration
                .GetSection("Mopidy:RadioStreams")
                .GetChildren()
                .Select(_ => new MopidyItem { Name = _.Key, Uri = _.Value })
                .OrderBy(_ => _.Name)
                .ToArray();
        }

        public IEnumerable<MopidyItem> Stations { get; set; }
    }
}
