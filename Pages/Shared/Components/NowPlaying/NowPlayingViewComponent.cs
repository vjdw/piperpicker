using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PiperPicker.Proxies;

namespace PiperPicker.Pages.Components.NowPlaying
{
    public class NowPlayingViewComponent : ViewComponent
    {
        public static string Name => nameof(NowPlayingViewComponent).Replace("ViewComponent","");

        public NowPlayingViewComponent() { }
        
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var nowPlaying = await MopidyProxy.GetNowPlaying();

            nowPlaying.State = string.IsNullOrWhiteSpace(nowPlaying.State)
                ? "Idle"
                : $"{nowPlaying.State.Substring(0, 1).ToUpperInvariant()}{nowPlaying.State.Substring(1)}";

            nowPlaying.Name = string.IsNullOrWhiteSpace(nowPlaying.Name)
                ? "Empty Play Queue"
                : nowPlaying.Name;

            return View("Default", nowPlaying);
        }
    }
}