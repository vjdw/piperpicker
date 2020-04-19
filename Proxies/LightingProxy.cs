using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PiperPicker.Proxies
{
    public delegate void LightingNotificationEventHandler(object source, LightingNotificationEventArgs e);

    public class LightingNotificationEventArgs : EventArgs
    {
        private string EventInfo;
        public LightingNotificationEventArgs(string notification)
        {
            EventInfo = notification;
        }
        public string GetInfo()
        {
            return EventInfo;
        }
    }

    public static class LightingProxy
    {
        public static event LightingNotificationEventHandler OnLightingNotification;

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

            OnLightingNotification?.Invoke(null, new LightingNotificationEventArgs(hostname));

            return responseContent;
        }

        public static async Task<string> AddScheduleItem(string hostname, int hour, int minute, int r, int g, int b, int w = 0)
        {
            var colourJson = $"{{\"r\":{r}, \"g\":{g}, \"b\":{b}, \"w\":{w}}}";

            var response = await _client.PutAsync($"http://{hostname}/state/schedule/{hour}/{minute}", new StringContent(colourJson, Encoding.UTF8, "application/json"));
            var responseContent = await response.Content.ReadAsStringAsync();

            OnLightingNotification?.Invoke(null, new LightingNotificationEventArgs(hostname));

            return responseContent;
        }

        public static async Task<string> DeleteScheduleItem(string hostname, int hour, int minute)
        {
            var response = await _client.DeleteAsync($"http://{hostname}/state/schedule/{hour}/{minute}");
            var responseContent = await response.Content.ReadAsStringAsync();

            OnLightingNotification?.Invoke(null, new LightingNotificationEventArgs(hostname));

            return responseContent;
        }

        public class LightStateDto
        {
            public Mode Mode { get; set; }
            public string StaticColour { get; set; }

            public string StaticColourHexCode
            {
                get
                {
                    var parts = StaticColour.TrimStart('(').TrimEnd(')').Split(',');
                    var r = int.Parse(parts[0]);
                    var g = int.Parse(parts[1]);
                    var b = int.Parse(parts[2]);
                    return $"#{r:X2}{g:X2}{b:X2}";
                }
            }

            public IList<ScheduleItem> Schedule { get; set; }
        }

        public class ScheduleItem
        {
            public int r { get; set; }
            public int g { get; set; }
            public int b { get; set; }
            public int hour { get; set; }
            public int minute { get; set; }
            public int ticks { get; set; }
        }

        public enum Mode
        {
            on, off, random, schedule
        }
    }
}