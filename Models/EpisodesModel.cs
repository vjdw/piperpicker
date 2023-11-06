using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PiperPicker.Proxies;
using static PiperPicker.Proxies.MopidyProxy;

namespace PiperPicker.Models
{
    [BindProperties]
    public class EpisodesModel : PageModel
    {
        HttpClient _client = new HttpClient();

        public EpisodesModel(string episodeNameFilter)
        {
            // Task.Run(async () => {
            //     Episodes = (await MopidyProxy.GetEpisodes())
            //         .Where(_ => _.Name.StartsWith(episodeNameFilter))
            //         .Select(_ => {
            //             var parts = _.Name.Split('_', 3);
            //             _.Name = $"{parts[1]} {parts[2].Replace(".mp3", "").Replace(".aac", "").Replace(".m4a", "").Replace('_',' ')}";
            //             return _;
            //         })
            //         .OrderByDescending(_ => _.Name);
            // }).Wait();
        }

        public IEnumerable<MopidyItem> Episodes { get; set; }
    }
}
