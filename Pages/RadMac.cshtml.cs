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
using PiperPicker.Controllers;
using PiperPicker.Proxies;
using static PiperPicker.Proxies.MopidyProxy;

namespace PiperPicker.Pages
{
    [BindProperties]
    public class RadMacModel : PageModel
    {
        HttpClient _client = new HttpClient();

        public RadMacModel()
        {
            Task.Run(async () => {
                Episodes = (await MopidyProxy.GetEpisodes())
                    .Where(_ => _.Name.StartsWith("RadMac"))
                    .Select(_ => {
                        var parts = _.Name.Split('_', 3);
                        _.Name = $"{parts[1]} {parts[2].Replace(".m4a", "").Replace('_',' ')}";
                        return _;
                    })
                    .OrderByDescending(_ => _.Name);
            }).Wait();
        }

        public IEnumerable<MopidyItem> Episodes { get; set; }
    }
}
