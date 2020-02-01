
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace PiperPicker.Models
{
    public class LightingModel : PageModel
    {
        public IEnumerable<LightControllerModel> LightControllers { get; set; }

        public class LightControllerModel
        {
            public string Hostname { get; set; }
        }
    }
}