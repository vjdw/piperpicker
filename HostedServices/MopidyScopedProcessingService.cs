using System.Net.Http;
using Microsoft.AspNetCore.SignalR;
using PiperPicker.Hubs;
using PiperPicker.Proxies;

namespace PiperPicker.HostedServices
{
    // SignalR IHubContext must be scoped, so it gets run through
    // the HostedServiceRunner (which creates the scope).
    internal class MopidyScopedProcessingService
    {
        private readonly IHubContext<StateHub> _hubContext;

        public MopidyScopedProcessingService(IHubContext<StateHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void DoWork()
        {
            var client = new HttpClient();
            MopidyProxy.OnMopidyNotification +=
                async(object sender, MopidyNotificationEventArgs e) =>
                {
                    await _hubContext.Clients.All.SendAsync("MopidyNotification", e.GetInfo());
                };
        }
    }
}