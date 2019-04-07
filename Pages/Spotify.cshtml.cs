using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PiperPicker.Pages
{
    public class SpotifyModel : PageModel
    {
        public string Message { get; set; }

        public void OnGet()
        {
            Message = "spotify page.";
        }
    }
}
