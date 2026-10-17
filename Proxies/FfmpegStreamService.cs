using System.Diagnostics;

namespace PiperPicker.Proxies;

public sealed class FfmpegStreamService : IDisposable
{
    private readonly Lock _lock = new();
    private Process? _process;
    private string? _stationId;
    private CancellationTokenSource? _client;

    public void Start(string stationId, string sourceUri)
    {
        lock (_lock)
        {
            if (_process is null || _process.HasExited || _stationId != stationId)
            {
                StopLocked();

                var psi = new ProcessStartInfo("ffmpeg")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                };
                foreach (var a in new[]
                         {
                             "-nostdin", "-loglevel", "warning",
                             "-i", sourceUri,
                             "-vn", "-c:a", "copy",
                             "-f", "adts", "pipe:1"
                         })
                    psi.ArgumentList.Add(a);

                _process = Process.Start(psi)!;
                _stationId = stationId;
            }
        }
    }

    public void Stop()
    {
      // xyzzy keep:  lock (_lock) StopLocked();
    }

    private void StopLocked()
    {
        _client?.Cancel();
        _client = null;

        if (_process is { HasExited: false })
            _process.Kill(entireProcessTree: true);
        _process?.Dispose();
        _process = null;
        _stationId = null;
    }

    public bool IsActive(out string? stationId)
    {
        lock (_lock) return IsActiveLocked(out stationId);
    }

    private bool IsActiveLocked(out string? stationId)
    {
        stationId = _stationId;
        return _process != null;
    }
    
    /// Returns the ffmpeg stdout stream, or null if that station isn't running.
    /// Any previously attached client is cancelled.
    public Stream? TryAttach(string stationId, CancellationToken requestAborted,  out CancellationToken token)
    {
        lock (_lock)
        {
            token = default;
            if (_process is null || _process.HasExited || _stationId != stationId)
                return null;

            _client?.Cancel();
            _client = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            token = _client.Token;
            return _process.StandardOutput.BaseStream;
        }
    }

    public void Dispose() => Stop();
}