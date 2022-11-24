using System;
using System.Linq;
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

            nowPlaying.Name = !string.IsNullOrWhiteSpace(nowPlaying.Name)
                ? nowPlaying.Name
                : !string.IsNullOrWhiteSpace(nowPlaying.Uri)
                    ? NowPlayingUriToName(nowPlaying.Uri)
                    : "Empty Play Queue";

            return View("Default", nowPlaying);
        }

        private string NowPlayingUriToName(string uri)
        {
            // URI is expected to be like: "http://a.files.bbci.co.uk/media/live/manifesto/audio/simulcast/hls/uk/sbr_med/ak/bbc_radio_one_relax.m3u8"
            try
            {
                var partsOfPlaylistName = uri.Split('/').Last().Split('.').First().Split('_', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                return string.Join(' ', partsOfPlaylistName.Select(part => { return part == "bbc" ? "BBC" : part.First().ToString().ToUpperInvariant() + part[1..]; }));
            }
            catch
            {
                return uri;
            }
        }
    }
}