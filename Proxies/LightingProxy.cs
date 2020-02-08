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
        public class LightStateDto
        {
            public Mode Mode { get; set; }
        }

        public enum Mode
        {
            on, off, random, schedule
        }

        private static HttpClient _client = new HttpClient();

        public static async Task<LightStateDto> GetState(string hostname)
        {
            var response = await _client.GetAsync($"http://{hostname}/state");
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<LightStateDto>(responseContent);
        }

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
                Mode.random => new StringContent("random"),
                Mode.schedule => new StringContent("schedule"),
                _ => throw new NotSupportedException()
            };

            var response = await _client.PutAsync($"http://{hostname}/state/mode", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }
    }
}