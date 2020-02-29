using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.AspNetCore.Mvc;
using piperpicker.Database;
using PiperPicker.Models;
using static PiperPicker.Models.LightingModel;

namespace PiperPicker.Pages.Components.Lighting
{
    public class LightingViewComponent : ViewComponent
    {
        private ILiteDatabase _db;

        public static string Name => nameof(LightingViewComponent).Replace("ViewComponent","");

        public LightingViewComponent(ILiteDatabase db)
        {
            _db = db;
        }
        
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var lightingModel = new LightingModel
            {
                LightControllers = _db.GetCollection<LightControllerEntity>().Query().Select(_ => new LightControllerModel { Hostname = _.Hostname } ).ToList()
            };

            return View("Default", lightingModel);
        }
    }
}