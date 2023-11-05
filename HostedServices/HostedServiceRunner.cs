using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PiperPicker.HostedServices
{
    internal class HostedServiceRunner : IHostedService
    {
        public HostedServiceRunner(IServiceProvider services)
        {
            Services = services;
        }

        public IServiceProvider Services { get; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            using(var scope = Services.CreateScope())
            {
                var snapService = scope.ServiceProvider.GetRequiredService<SnapScopedProcessingService>();
                snapService.DoWork();

                var mopidyService = scope.ServiceProvider.GetRequiredService<MopidyScopedProcessingService>();
                mopidyService.DoWork();
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}