using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PiperPicker.Models
{
    public class SettingsModel : PageModel
    {
        public IEnumerable<DummySetting> DummySettings { get; set; } = default!;
    }

    public class DummySetting
    {
        public string Xyzzy { get; set; } = default!;
    }
}