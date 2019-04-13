using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PiperPicker.Proxies;
using SnapProxy = PiperPicker.Proxies.SnapProxy;

namespace PiperPicker.Pages.Components.SnapClient
{
    public class PlaybackStateViewComponent : ViewComponent
    {
        public static string Name => nameof(PlaybackStateViewComponent).Replace("ViewComponent","");

        public PlaybackStateViewComponent() { }
        
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var nowPlaying = await MopidyProxy.GetState();
            return View("Default", nowPlaying.Result);
        }
    }
}