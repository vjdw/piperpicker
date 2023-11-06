//using System.Net.Http;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using PiperPicker.Proxies;

//namespace PiperPicker.HostedServices
//{
//    // SignalR IHubContext must be scoped, so it gets run through
//    // the HostedServiceRunner (which creates the scope).
//    public class MopidyScopedProcessingService
//    {
//        private readonly IConfiguration _configuration;
//        private readonly ILogger<MopidyScopedProcessingService> _logger;

//        public MopidyScopedProcessingService(IConfiguration configuration, ILogger<MopidyScopedProcessingService> logger)
//        {
//            _configuration = configuration;
//            _logger = logger;
//        }

//        public void DoWork()
//        {
//            var client = new HttpClient();
//            // MopidyProxy.Logger = _logger;
//            // MopidyProxy.Configuration = _configuration;
//            // MopidyProxy.OnMopidyNotification +=
//            //     async(object sender, MopidyNotificationEventArgs e) =>
//            //     {
//            //        // await _hubContext.Clients.All.SendAsync("MopidyNotification", e.GetInfo());
//            //        // await System.Threading.Tasks.Task.Delay(5000);
//            //       //  await _hubContext.Clients.All.SendAsync("MopidyNotification", e.GetInfo());
//            //     };
//            // MopidyProxy.Start();
//        }
//    }
//}