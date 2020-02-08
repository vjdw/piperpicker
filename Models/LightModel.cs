
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace PiperPicker.Models
{
    public class LightModel : PageModel
    {
        public string Hostname { get; set; }
        public string Mode { get; set; }
    }
}