using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PiperPicker.Proxies
{
    public static class SnapProxy
    {
        public static async Task<IEnumerable<SnapClient>> GetSnapClients()
        {
            string response = await SnapRequest("{\"id\":1,\"jsonrpc\":\"2.0\",\"method\":\"Server.GetStatus\"}\r\n");
            var responseJson = JObject.Parse(response);

            var groups = (JArray) responseJson["result"]["server"]["groups"];
            var snapclients = groups.SelectMany(group =>
                ((JArray) group["clients"]).Select(client =>
                    new SnapClient()
                    {
                        Host = client["host"].Value<string>("name"),
                        Muted = client["config"]["volume"].Value<bool>("muted"),
                        Volume = client["config"]["volume"].Value<int>("percent")
                    }));

            return snapclients;
        }

        private static async Task<string> SnapRequest(string request)
        {
            string response = null;
            using(TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("hunchcorn", 1705);
                using(var stream = client.GetStream())
                {
                    byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes(request);
                    stream.Write(bytesToSend, 0, bytesToSend.Length);
                    await stream.FlushAsync();

                    int readBufferSize = 16000;
                    byte[] bytesToRead = new byte[readBufferSize];
                    int bytesRead = await stream.ReadAsync(bytesToRead, 0, readBufferSize);
                    response = Encoding.ASCII.GetString(bytesToRead, 0, bytesRead);
                }
                client.Close();
            }
            return response;
        }

        [JsonObject]
        public class SnapClient
        {
            [JsonProperty]
            public string Host { get; set; }

            [JsonProperty]
            public bool Muted { get; set; }

            [JsonProperty]
            public int Volume { get; set; }
        }
    }
}