// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Net.Http;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.Mvc.RazorPages;
// using Newtonsoft;
// using Newtonsoft.Json;
// using Newtonsoft.Json.Linq;
// using PiperPicker.Controllers;
// using static PiperPicker.Controllers.Mopidy;

// namespace PiperPicker.Pages
// {
//     [BindProperties]
//     public class MopidyProxyModel : PageModel
//     {
//         public async Task<IEnumerable<Episode>> OnGetEpisodes()
//         {
//             return await Mopidy.GetEpisodes();
//         }

//         public async Task<ActionResult> OnPostPlayEpisode([FromBody]PlayEpisodeDto data)
//         {
//             await Mopidy.ClearQueue();
//             await Mopidy.PlayEpisode(data);
//             await Mopidy.Play();

//             return new JsonResult("{}");
//         }

//         public async Task<ActionResult> OnPostClearQueue()
//         {
//             await Mopidy.ClearQueue();
//             return new JsonResult("{}");
//         }

//         public async Task<ActionResult> OnPostPlay()
//         {
//             await Mopidy.Play();

//             return new JsonResult("{}");
//         }
//     }
// }
