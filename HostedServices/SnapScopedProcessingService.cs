using System.Net.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PiperPicker.Hubs;
using PiperPicker.Proxies;

namespace PiperPicker.HostedServices
{
    // SignalR IHubContext must be scoped, so it gets run through
    // the HostedServiceRunner (which creates the scope).
    public class SnapScopedProcessingService
    {
        private readonly IHubContext<StateHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SnapScopedProcessingService> _logger;

        public SnapScopedProcessingService(IHubContext<StateHub> hubContext, IConfiguration configuration, ILogger<SnapScopedProcessingService> logger)
        {
            _hubContext = hubContext;
            _configuration = configuration;
            _logger = logger;
        }

        public void DoWork()
        {
            var client = new HttpClient();
            SnapProxy.Logger = _logger;
            SnapProxy.Configuration = _configuration;
            SnapProxy.OnSnapNotification +=
                async(object sender, SnapNotificationEventArgs e) =>
                {
                    await _hubContext.Clients.All.SendAsync("SnapNotification", e.GetInfo());
                };
            SnapProxy.Start();
        }
    }
}