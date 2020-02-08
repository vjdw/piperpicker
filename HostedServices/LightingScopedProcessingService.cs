using System.Net.Http;
using Microsoft.AspNetCore.SignalR;
using PiperPicker.Hubs;
using PiperPicker.Proxies;

namespace PiperPicker.HostedServices
{
    // SignalR IHubContext must be scoped, so it gets run through
    // the HostedServiceRunner (which creates the scope).
    internal class LightingScopedProcessingService
    {
        private readonly IHubContext<StateHub> _hubContext;

        public LightingScopedProcessingService(IHubContext<StateHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void DoWork()
        {
            var client = new HttpClient();
            LightingProxy.OnLightingNotification +=
                async(object sender, LightingNotificationEventArgs e) =>
                {
                    await _hubContext.Clients.All.SendAsync("LightingNotification", e.GetInfo());
                };
        }
    }
}