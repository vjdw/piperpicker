using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PiperPicker.HostedServices;

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

    public class SnapProxy
    {
        private string SnapServerHost;
        private int SnapServerPort;

        private NetworkStream _stream;
        private readonly object _clientReadLock = new object();
        private readonly object _clientWriteLock = new object();

        private Dictionary<string, string> ClientNameMap;

        public event SnapNotificationEventHandler OnSnapNotification;
        public IConfiguration Configuration;
        public ILogger<SnapScopedProcessingService> Logger;

        public SnapProxy(IConfiguration configuration, ILogger<SnapScopedProcessingService> logger)
        {
            Configuration = configuration;
            Logger = logger;
        }

        public void Start()
        {
            Logger.LogInformation($"{nameof(SnapProxy)} starting");

            SnapServerHost = Configuration["SnapcastServer:Hostname"];
            SnapServerPort = Configuration.GetValue<int>("SnapcastServer:Port");

            ClientNameMap = Configuration
                .GetSection("SnapcastServer:ClientNameMap")
                .GetChildren()
                .ToDictionary(_ => _.Key, _ => _.Value);

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

        private void MonitorStream()
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
                        Monitor.TryEnter(_clientReadLock, 1000, ref lockTaken);
                        if (lockTaken)
                        {
                            if (_stream.DataAvailable)
                            {
                                int bytesRead = _stream.Read(bytesToRead, 0, readBufferSize);
                                var response = Encoding.ASCII.GetString(bytesToRead, 0, bytesRead);
                                OnSnapNotification?.Invoke(null, new SnapNotificationEventArgs(response));
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

                    lock (_clientReadLock)
                    {
                        lock (_clientWriteLock)
                        {
                            try { _stream.Dispose(); }
                            catch { }
                            try
                            {
                                var client = new TcpClient();
                                client.Connect(SnapServerHost, SnapServerPort);
                                _stream = client.GetStream();
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, $"Exception when retrying connection to Snapcast at {SnapServerHost}:{SnapServerPort}");
                            }
                        }
                    }
                }
            }
        }

        // public IEnumerable<SnapClient> GetSnapClients()
        // {
        //     var request = BuildSnapRequest("Server.GetStatus");
        //     string responseJson = SendSnapRequest(request, waitForResponse: true);
        //     var response = JObject.Parse(responseJson);

        //     var groups = (JArray)response["result"]["server"]["groups"];
        //     try
        //     {
        //         var snapclients = groups
        //             .SelectMany(group =>
        //                 ((JArray)group["clients"]).Select(client =>
        //                    new SnapClient()
        //                    {
        //                        Host = client["host"].Value<string>("name"),
        //                        Mac = client["host"].Value<string>("mac"),
        //                        Muted = client["config"]["volume"].Value<bool>("muted"),
        //                        Volume = client["config"]["volume"].Value<int>("percent")
        //                    }))
        //             .Where(_ => _.Host != "localhost")
        //             .DistinctBy(_ => _.Mac)
        //             .ToList();

        //         foreach (var snapclient in snapclients)
        //         {
        //             if (ClientNameMap.ContainsKey(snapclient.Host))
        //                 snapclient.DisplayName = ClientNameMap[snapclient.Host];
        //         }

        //         return snapclients.OrderBy(_ => _.DisplayName);
        //     }
        //     catch (Exception ex)
        //     {
        //         Logger.LogError(ex, $"Exception when trying to get list of Snapcast clients from {SnapServerHost}:{SnapServerPort}");
        //         throw;
        //     }
        // }

        // public bool TryGetSnapClient(string clientMac, out SnapClient snapclient)
        // {
        //     var request = BuildSnapRequest("Client.GetStatus", new { id = clientMac });
        //     string responseJson = SendSnapRequest(request, waitForResponse: true);
        //     var response = JObject.Parse(responseJson);

        //     if (response.ContainsKey("result") && ((JObject)response["result"]).ContainsKey("client"))
        //     {
        //         var client = (JObject)response["result"]["client"];
        //         snapclient = new SnapClient()
        //         {
        //             Host = client["host"].Value<string>("name"),
        //             Mac = client["host"].Value<string>("mac"),
        //             Muted = client["config"]["volume"].Value<bool>("muted"),
        //             Volume = client["config"]["volume"].Value<int>("percent")
        //         };
        //         return true;
        //     }

        //     snapclient = null;
        //     return false;
        // }

        public void SetMute(string clientMac, bool muted)
        {
            object request = BuildSnapRequest("Client.SetVolume", new { id = clientMac, volume = new { muted = muted } });
            SendSnapRequest(request);
        }

        public void SetVolume(string clientMac, int percentagePointChange)
        {
            // lock (_clientReadLock)
            // {
            //     lock (_clientWriteLock)
            //     {
            //         if (TryGetSnapClient(clientMac, out var client))
            //         {
            //             var newVolume = Math.Clamp(client.Volume + percentagePointChange, 0, 100);
            //             object request = BuildSnapRequest("Client.SetVolume", new { id = clientMac, volume = new { percent = newVolume } });
            //             SendSnapRequest(request);
            //         }
            //     }
            // }
        }

        private object BuildSnapRequest(string method, object @params = null)
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

        private string SendSnapRequest(dynamic requestObj, bool waitForResponse = false)
        {
            lock (_clientReadLock)
            {
                lock (_clientWriteLock)
                {
                    string requestId = requestObj.id;

                    var request = JsonSerializer.Serialize(requestObj);
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

        public class SnapClient
        {
            public string Host { get; set; }

            private string _displayName = null;
            public string DisplayName
            {
                get
                {
                    return _displayName ?? Host;
                }
                set
                {
                    _displayName = value;
                }
            }

            public string Mac { get; set; }

            public bool Muted { get; set; }

            public int Volume { get; set; }
        }
    }
}