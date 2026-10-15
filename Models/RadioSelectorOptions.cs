using static PiperPicker.Proxies.MopidyProxy;

namespace PiperPicker.Models
{
    public class RadioSelectorOptions
    {
        public const string SectionName = "Mopidy";

        public List<MopidyItem> RadioStreams { get; } = new();
    }
}