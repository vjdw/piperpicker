using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PiperPicker.Proxies;
using SnapProxy = PiperPicker.Proxies.SnapProxy;

namespace PiperPicker.Pages.Components.SnapClient
{
    public class NowPlayingViewComponent : ViewComponent
    {
        public static string Name => nameof(NowPlayingViewComponent).Replace("ViewComponent","");

        public NowPlayingViewComponent() { }
        
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var nowPlaying = await MopidyProxy.GetNowPlaying();
            return View("Default", nowPlaying);
        }
    }
}