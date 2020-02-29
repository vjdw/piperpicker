using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SnapProxy = PiperPicker.Proxies.SnapProxy;

namespace PiperPicker.Pages.Components.SnapClient
{
    public class SnapClientsViewComponent : ViewComponent
    {
        public static string Name => nameof(SnapClientsViewComponent).Replace("ViewComponent","");

        public SnapClientsViewComponent() { }
        
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var snapClients = SnapProxy.GetSnapClients();
            return View("Default", snapClients);
        }
    }
}