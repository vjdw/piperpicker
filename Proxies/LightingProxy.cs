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
        // TODO: load from config
        private static readonly string LightingEndpoint = "http://192.168.1.50";

        private static HttpClient _client = new HttpClient();

        public static async Task SetColour(int red, int green, int blue, int white)
        {
            await LightPost(new ColourDto { R = red, G = green, B = blue, W = white } );
        }

        private static async Task<string> LightPost(ColourDto colour)
        {
            var content =  new FormUrlEncodedContent(new[] {
                new KeyValuePair<string,string>("R", $"{colour.R}"),
                new KeyValuePair<string, string>("G", $"{colour.G}"),
                new KeyValuePair<string, string>("B", $"{colour.B}"),
                new KeyValuePair<string, string>("W", $"{colour.W}")
            });
            var response = await _client.PostAsync($"{LightingEndpoint}/test", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }

        [JsonObject]
        public class ColourDto
        {
            [JsonProperty]
            public int R { get; set; }
            [JsonProperty]
            public int G { get; set; }
            [JsonProperty]
            public int B { get; set; }
            [JsonProperty]
            public int W { get; set; }
        }
    }
}