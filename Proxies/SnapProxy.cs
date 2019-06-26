using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiperPicker.Hubs;

namespace PiperPicker.Proxies
{
    public delegate void SnapNotificationEventHandler(object source, SnapNotificationEventArgs e);

    public class SnapNotificationEventArgs : EventArgs
    {
        private string EventInfo;
        public SnapNotificationEventArgs(string notification)
        {
            EventInfo = notification;
        }
        public string GetInfo()
        {
            return EventInfo;
        }
    }

    public static class SnapProxy
    {
        // TODO: load from config
        private static readonly string SnapServerHost = "hunchcorn";
        private static readonly int SnapServerPort = 1705;

        private static NetworkStream _stream;
        private static readonly object _clientReadLock = new object();
        private static readonly object _clientWriteLock = new object();

        public static event SnapNotificationEventHandler OnSnapNotification;

        static SnapProxy()
        {
            var client = new TcpClient();
            try
            {
                client.Connect(SnapServerHost, SnapServerPort);
                _stream = client.GetStream();
                Task.Run(() => MonitorStream());
            }
            catch
            {
                throw new ApplicationException($"SnapProxy could not connect to snapserver on {SnapServerHost}:{SnapServerPort}");
            }
        }

        private static void MonitorStream()
        {
            var rnd = new Random();
            int readBufferSize = 16000;
            byte[] bytesToRead = new byte[readBufferSize];

            while (true)
            {
                try
                {
                    Thread.Sleep(rnd.Next(900, 1100));
                    bool lockTaken = false;
                    try
                    {
                        System.Threading.Monitor.TryEnter(_clientReadLock, 1000, ref lockTaken);
                        if (lockTaken)
                        {
                            if (_stream.DataAvailable)
                            {
                                int bytesRead = _stream.Read(bytesToRead, 0, readBufferSize);
                                var response = Encoding.ASCII.GetString(bytesToRead, 0, bytesRead);
                                if (OnSnapNotification != null)
                                    OnSnapNotification(null, new SnapNotificationEventArgs(response));
                            }
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                            Monitor.Exit(_clientReadLock);
                    }
                }
                catch
                {
                    Thread.Sleep(60000);

                    lock(_clientReadLock)
                    {
                        lock(_clientWriteLock)
                        {
                            try { _stream.Dispose(); }
                            catch { }
                            try
                            {
                                var client = new TcpClient();
                                client.Connect(SnapServerHost, SnapServerPort);
                                _stream = client.GetStream();
                            }
                            catch
                            {
                                // TODO: xyzzy / fix logging
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<SnapClient> GetSnapClients()
        {
            var request = BuildSnapRequest("Server.GetStatus");
            string responseJson = SendSnapRequest(request, waitForResponse : true);
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

                return snapclients.OrderBy(_ => _.Host);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public static bool TryGetSnapClient(string clientMac, out SnapClient snapclient)
        {
            var request = BuildSnapRequest("Client.GetStatus", new { id = clientMac });
            string responseJson = SendSnapRequest(request, waitForResponse : true);
            var response = JObject.Parse(responseJson);

            if (response.ContainsKey("result") && ((JObject) response["result"]).ContainsKey("client"))
            {
                var client = (JObject) response["result"]["client"];
                snapclient = new SnapClient()
                {
                    Host = client["host"].Value<string>("name"),
                    Mac = client["host"].Value<string>("mac"),
                    Muted = client["config"]["volume"].Value<bool>("muted"),
                    Volume = client["config"]["volume"].Value<int>("percent")
                };
                return true;
            }

            snapclient = null;
            return false;
        }

        public static void SetMute(string clientMac, bool muted)
        {
            object request = BuildSnapRequest("Client.SetVolume", new { id = clientMac, volume = new { muted = muted } });
            SendSnapRequest(request);
        }

        public static void SetVolume(string clientMac, int percentagePointChange)
        {
            lock(_clientReadLock)
            {
                lock(_clientWriteLock)
                {
                    if (TryGetSnapClient(clientMac, out var client))
                    {
                        var newVolume = Math.Clamp(client.Volume + percentagePointChange, 0, 100);
                        object request = BuildSnapRequest("Client.SetVolume", new { id = clientMac, volume = new { percent = newVolume } });
                        SendSnapRequest(request);
                    }
                }
            }
        }

        private static object BuildSnapRequest(string method, object @params = null)
        {
            dynamic requestObj = new
            {
            id = Guid.NewGuid().ToString("N"),
            jsonrpc = "2.0",
            method = method,
            @params = @params ?? new { }
            };
            return requestObj;
        }

        private static string SendSnapRequest(dynamic requestObj, bool waitForResponse = false)
        {
            lock(_clientReadLock)
            {
                lock(_clientWriteLock)
                {
                    string requestId = requestObj.id;

                    var request = JsonConvert.SerializeObject(requestObj);
                    byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes($"{request}\r\n");
                    _stream.Write(bytesToSend, 0, bytesToSend.Length);
                    _stream.Flush();

                    if (waitForResponse)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            Thread.Sleep(50);
                            int readBufferSize = 16000;
                            byte[] bytesToRead = new byte[readBufferSize];
                            if (_stream.DataAvailable)
                            {
                                int bytesRead = _stream.Read(bytesToRead, 0, readBufferSize);
                                var responses = Encoding.ASCII.GetString(bytesToRead, 0, bytesRead);
                                foreach (var response in responses.Split("\r\n"))
                                {
                                    if (response.Contains(requestId))
                                        return response;
                                }
                            }
                        }
                    }
                    return "{}";
                }
            }
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