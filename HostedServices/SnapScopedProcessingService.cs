using System.Net.Http;
using Microsoft.AspNetCore.SignalR;
using PiperPicker.Hubs;
using PiperPicker.Proxies;

namespace PiperPicker.HostedServices
{
    // SignalR IHubContext must be scoped, so it gets run through
    // the HostedServiceRunner (which creates the scope).
    internal class SnapScopedProcessingService
    {
        private readonly IHubContext<StateHub> _hubContext;

        public SnapScopedProcessingService(IHubContext<StateHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void DoWork()
        {
            var client = new HttpClient();
            SnapProxy.OnSnapNotification +=
                async(object sender, SnapNotificationEventArgs e) =>
                {
                    await _hubContext.Clients.All.SendAsync("SnapNotification", e.GetInfo());
                };
        }
    }
}