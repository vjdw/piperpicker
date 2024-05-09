using System.Net.Sockets;
using System.Text;
using System.Text.Json;

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
        private static int _lastKnownClientVolume = 0;
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
                Task.Run(() => MonitorStream());
            }
            catch
            {
                throw new ApplicationException($"SnapProxy could not connect to snapserver on {SnapServerHost}:{SnapServerPort}");
            }
        }

        // DTOs for notifications, changes coming from other snap clients.
        private record SnapVolume(bool Muted, int Percent);
        private record SnapVolumeNotificationParams(string Id, SnapVolume Volume);
        private record SnapVolumeNotification(string Method, SnapVolumeNotificationParams Params) : SnapBaseNotification(Method);
        private record SnapBaseNotification(string Method);

        // DTOs for responses from snap requests this client has made
        private record SnapGetStatusResultClientConfig(SnapVolume Volume);
        private record SnapGetStatusResultClient(string Id, SnapGetStatusResultClientConfig Config);
        private record SnapGetStatusResult(SnapGetStatusResultClient Client);
        private record SnapGetStatusResponse(string Id, SnapGetStatusResult Result) : SnapBaseResponse(Id);
        private record SnapBaseResponse(string Id);

        private IEnumerable<SnapVolumeNotificationParams> ProcessSnapResponse(string response)
        {
            var responseLines = response.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            IEnumerable<SnapVolumeNotificationParams> messages = (IEnumerable<SnapVolumeNotificationParams>)(responseLines
                .Select(responseLine =>
                    {
                        if (responseLine.Contains("Client.GetStatus-"))
                        {
                            var snapMessage = JsonSerializer.Deserialize<SnapGetStatusResponse>(responseLine, _serialiserOptions);
                            if (snapMessage == null)
                            {
                                Logger.LogWarning($"The following GetStatus response could not be deserialized: '{responseLine}'");
                                return null;
                            }
                            var client = snapMessage.Result.Client;
                            return new SnapVolumeNotificationParams(client.Id, client.Config.Volume);
                        }
                        else
                        {
                            var snapMessage = JsonSerializer.Deserialize<SnapBaseNotification>(responseLine, _serialiserOptions);
                            if (snapMessage == null)
                            {
                                Logger.LogWarning($"The following notification could not be deserialized: '{responseLine}'");
                                return null;
                            }
                            return snapMessage.Method switch
                            {
                                "Client.OnVolumeChanged" => JsonSerializer.Deserialize<SnapVolumeNotification>(responseLine, _serialiserOptions)?.Params,
                                _ => null
                            };
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
                                foreach (SnapVolumeNotificationParams message in messages.Where(m => m is SnapVolumeNotificationParams))
                                {
                                    if (message.Id == _clientId)
                                    {
                                        var newClientVolume = new Volume(message.Volume.Muted, message.Volume.Percent);
                                        _lastKnownClientVolume = message.Volume.Percent;

                                        _clientState = _clientState with { Config = _clientState.Config with { Volume = newClientVolume } };
                                        OnSnapNotification?.Invoke(null!, new SnapcastClientNotificationEventArgs(_clientState));
                                    }
                                    Logger.LogInformation("ClientId {ClientId} changed", message.Id);
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

        public void RequestStatus(string clientMac)
        {
            var request = BuildSnapRequest("Client.GetStatus", new { id = clientMac });
            SendSnapRequest(request);

            Logger.LogInformation($"Called GetStatus using clientMac '{clientMac}'");
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

                        _lastKnownClientVolume = newVolume;
                    }
                }
            }
        }

        private object BuildSnapRequest(string method, object @params = null!)
        {
            dynamic requestObj = new
            {
                id = $"{method}-{Guid.NewGuid():N}",
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

        private static Dictionary<string, Volume> _clientLastKnownVolumes = new();

        public record Volume(bool Muted, int Percent);
        public record Config(string Name, Volume Volume);
        public record SnapcastClient(string Id, Config Config, bool Connected)
        {

            public static SnapcastClient BuildDefault(string Id)
            {
                return new SnapcastClient(Id, new Config(Id, new Volume(false, _lastKnownClientVolume)), false);
            }
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