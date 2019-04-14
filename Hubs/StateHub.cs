using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PiperPicker.Proxies;

namespace PiperPicker.Hubs
{
    public class StateHub : Hub
    {
        public async Task SendSnapNotification(string message)
        {
            await Clients.All.SendAsync("SnapNotification", message);
        }
    }
}