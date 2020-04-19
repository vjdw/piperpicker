using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using static PiperPicker.Proxies.LightingProxy;

namespace PiperPicker.Models
{
    public class SettingsModel : PageModel
    {
        public IEnumerable<LightSetting> LightSettings { get; set; }
    }

    public class LightSetting
    {
        public string Hostname { get; set; }
        public LightStateDto State { get; set; }
    }
}