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
        // TODO: load from config
        private static readonly string SnapServerHost = "hunchcorn";
        private static readonly int SnapServerPort = 1705;

        public static async Task<IEnumerable<SnapClient>> GetSnapClients()
        {
            var request = BuildRpcRequest("Server.GetStatus");
            string responseJson = await SendSnapRequest(request);
            var response = JObject.Parse(responseJson);

            var groups = (JArray) response["result"]["server"]["groups"];
            try
            {
                var snapclients = groups.SelectMany(group =>
                    ((JArray) group["clients"]).Select(client =>
                        new SnapClient()
                        {
                            Host = client["host"].Value<string>("name"),
                                Mac = client["host"].Value<string>("mac"),
                                Muted = client["config"]["volume"].Value<bool>("muted"),
                                Volume = client["config"]["volume"].Value<int>("percent")
                        }));

                return snapclients;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public static async Task SetMute(string clientMac, bool muted)
        {
            string request = BuildRpcRequest("Client.SetVolume", new { id = clientMac, volume = new { muted = muted } });
            string responseJson = await SendSnapRequest(request);
            var response = JObject.Parse(responseJson);
        }

        private static string BuildRpcRequest(string method, object @params = null)
        {
            var requestObj = new
            {
            id = Guid.NewGuid().ToString("N"),
            jsonrpc = "2.0",
            method = method,
            @params = @params ?? new { }
            };
            return JsonConvert.SerializeObject(requestObj);
        }

        private static async Task<string> SendSnapRequest(string request)
        {
            string response = null;
            using(TcpClient client = new TcpClient())
            {
                await client.ConnectAsync(SnapServerHost, SnapServerPort);
                using(var stream = client.GetStream())
                {
                    byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes($"{request}\r\n");
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
            public string Mac { get; set; }

            [JsonProperty]
            public bool Muted { get; set; }

            [JsonProperty]
            public int Volume { get; set; }
        }
    }
}