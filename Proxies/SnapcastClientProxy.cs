using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PiperPicker.Proxies
{
    public class SnapcastClientProxy
    {
        public delegate void SnapcastClientNotificationEventHandler(object source, SnapcastClientNotificationEventArgs e);

        private JsonSerializerOptions _serialiserOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private string SnapServerHost;
        private int SnapServerPort;

        private NetworkStream _stream;
        private readonly object _clientReadLock = new object();
        private readonly object _clientWriteLock = new object();

        private Dictionary<string, string> ClientNameMap;

        public event SnapcastClientNotificationEventHandler OnSnapNotification = default!;
        public IConfiguration Configuration;
        public ILogger<SnapcastClientProxy> Logger;

        private string _clientId;
        private SnapcastClient _clientState;

        public void MonitorClient(string clientId)
        {
            _clientId = clientId;
        }

        public SnapcastClientProxy(IConfiguration configuration, ILogger<SnapcastClientProxy> logger)
        {
            Configuration = configuration;
            Logger = logger;

            _clientId = Guid.Empty.ToString();
            _clientState = SnapcastClient.BuildDefault(_clientId);

            Logger.LogInformation($"{nameof(SnapcastClientProxy)} starting");

            SnapServerHost = Configuration["SnapcastServer:Hostname"]!;
            SnapServerPort = Configuration.GetValue<int>("SnapcastServer:Port");

            ClientNameMap = Configuration
                .GetSection("SnapcastServer:ClientNameMap")
                .GetChildren()
                .ToDictionary(_ => _.Key!, _ => _.Value!);

            var client = new TcpClient();
            try
            {
                client.Connect(SnapServerHost, SnapServerPort);
                _stream = client.GetStream();
                // xyzzy cancel this task when shutting down
                Task.Run(() => MonitorStream());
            }
            catch
            {
                throw new ApplicationException($"SnapProxy could not connect to snapserver on {SnapServerHost}:{SnapServerPort}");
            }
        }

        private record SnapVolumeParams(bool Muted, int Percent);
        private record SnapVolumeMessageParams(string Id, SnapVolumeParams Volume);
        private record SnapVolumeMessage(string Method, SnapVolumeMessageParams Params) : SnapBaseMessage(Method);
        private record SnapBaseMessage(string Method);
        private IEnumerable<SnapBaseMessage> ProcessSnapResponse(string response)
        {
            var responseLines = response.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            IEnumerable<SnapBaseMessage> messages = (IEnumerable<SnapBaseMessage>)(responseLines
                .Select(responseLine =>
                    {
                        var snapMessage = JsonSerializer.Deserialize<SnapBaseMessage>(responseLine, _serialiserOptions);
                        if (snapMessage != null)
                        {
                            return snapMessage.Method switch
                            {
                                "Client.OnVolumeChanged" => JsonSerializer.Deserialize<SnapVolumeMessage>(responseLine, _serialiserOptions),
                                _ => null
                            };
                        }
                        else
                        {
                            return null;
                        }
                    })
                .Where(_ => _ is not null)
                .ToList());

            return messages;
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

                                var messages = ProcessSnapResponse(response);
                                foreach (SnapVolumeMessage message in messages.Where(m => m is SnapVolumeMessage))
                                {
                                    if (message.Params.Id == _clientId)
                                    {
                                        var newClientVolume = new Volume(message.Params.Volume.Muted, message.Params.Volume.Percent);
                                        if (_clientState.Config.Volume != newClientVolume)
                                        {
                                            _clientState = _clientState with { Config = _clientState.Config with { Volume = newClientVolume } };
                                            OnSnapNotification?.Invoke(null!, new SnapcastClientNotificationEventArgs(_clientState));
                                        }
                                    }
                                    Logger.LogInformation("ClientId {ClientId} changed", message.Params.Id);
                                }
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
                    Thread.Sleep(5000);

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

        private record Result(SnapcastClient Client);
        private record GetStatusResponse(string Id, Result Result);

        public bool TryGetSnapClient(string clientMac, out SnapcastClient snapclient)
        {
            var request = BuildSnapRequest("Client.GetStatus", new { id = clientMac });
            string responseJson = SendSnapRequest(request, waitForResponse: true);

            Logger.LogInformation(clientMac + " " + responseJson);

            try
            {
                var statusResponse = JsonSerializer.Deserialize<GetStatusResponse>(responseJson, _serialiserOptions);
                if (statusResponse != null)
                {
                    snapclient = statusResponse.Result.Client;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling snapcast GetStatus response.");
            }

            snapclient = default!;
            return false;
        }

        public void SetMute(string clientMac, bool muted)
        {
            object request = BuildSnapRequest("Client.SetVolume", new { id = clientMac, volume = new { muted = muted } });
            SendSnapRequest(request);
        }

        public void SetVolume(string clientMac, int newVolume)
        {
            lock (_clientReadLock)
            {
                lock (_clientWriteLock)
                {
                    if (TryGetSnapClient(clientMac, out var client))
                    {
                        object request = BuildSnapRequest("Client.SetVolume", new { id = clientMac, volume = new { percent = newVolume } });
                        SendSnapRequest(request);
                    }
                }
            }
        }

        private object BuildSnapRequest(string method, object @params = null!)
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

        public record Volume(bool Muted, int Percent);
        public record Config(string Name, Volume Volume);
        public record SnapcastClient(string Id, Config Config, bool Connected)
        {
            public static SnapcastClient BuildDefault(string Id) => new SnapcastClient(Id, new Config(Id, new Volume(false, 0)), false);
        }

        public class SnapcastClientNotificationEventArgs : EventArgs
        {
            private SnapcastClient ClientState;
            public SnapcastClientNotificationEventArgs(SnapcastClient snapcastClientState)
            {
                ClientState = snapcastClientState;
            }
            public SnapcastClient GetClientState()
            {
                return ClientState;
            }
        }
    }
}