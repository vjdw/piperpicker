using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PiperPicker.Proxies
{
    public static class LightingProxy
    {
        public enum Mode
        {
            on, off, schedule
        }

        // TODO: load from config
        private static readonly string BacklightEndpoint = "http://192.168.1.50";
        private static readonly string AmbilightEndpoint = "http://192.168.1.51";

        private static HttpClient _client = new HttpClient();

        public static async Task<string> SetStaticColour(string hostname, int r, int g, int b, int w)
        {
            var response = await _client.PutAsync($"http://{hostname}/state/staticcolour", new StringContent($"{{\"r\":{r}, \"g\":{g}, \"b\":{b}, \"w\":{w}}}"));
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        public static async Task<string> SetMode(string hostname, Mode mode)
        {
            var content = mode switch
            {
                Mode.on => new StringContent("on"),
                Mode.off => new StringContent("off"),
                Mode.schedule => new StringContent("schedule"),
                _ => throw new NotSupportedException()
            };


            var response = await _client.PutAsync($"http://{hostname}/state/mode", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }
    }
}