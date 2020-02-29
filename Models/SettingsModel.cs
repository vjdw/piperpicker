using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

namespace PiperPicker.Models
{
    public class SettingsModel : PageModel
    {
        public IEnumerable<string> LightingHostnames { get; set; }
    }
}