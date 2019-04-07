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
using static PiperPicker.Controllers.Mopidy;

namespace PiperPicker.Pages
{
    [BindProperties]
    public class ControlModel : PageModel
    {
        HttpClient _client = new HttpClient();

        public ControlModel()
        {
        }
    }
}
