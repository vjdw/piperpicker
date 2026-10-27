using System.Diagnostics;
using System.Threading.Channels;

public sealed class FfmpegStreamService : IDisposable
{
    private readonly Lock _lock = new();
    private Process? _process;
    private string? _stationId;
    private StreamHub? _hub;

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
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                foreach (var a in new[]
                         {
                             "-nostdin", "-loglevel", "warning",
                             "-reconnect", "1", "-reconnect_streamed", "1", "-reconnect_delay_max", "5",
                             "-i", sourceUri,
                             "-vn", "-c:a", "copy",
                             "-f", "adts", "pipe:1"
                         })
                    psi.ArgumentList.Add(a);

                var process = Process.Start(psi)!;
                var hub = new StreamHub(256 * 1024);

                // Drain stderr, might want to enable logging. 
                process.ErrorDataReceived += (_, e) =>
                {
                    /* logger.LogWarning("{Line}", e.Data); */
                };
                process.BeginErrorReadLine();

                // Single reader of stdout
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await hub.PumpAsync(process.StandardOutput.BaseStream, CancellationToken.None);
                    }
                    finally
                    {
                        hub.Complete();
                    }
                });

                _process = process;
                _hub = hub;
                _stationId = stationId;
            }
        }
    }

    public (byte[][] backlog, ChannelReader<byte[]> live, Action unsubscribe)? TrySubscribe(string stationId)
    {
        lock (_lock)
        {
            if (_process is null || _process.HasExited || _stationId != stationId || _hub is null)
                return null;
            return _hub.Subscribe();
        }
    }

    public bool IsActive(out string? stationId)
    {
        lock (_lock) return IsActiveLocked(out stationId);
    }

    private bool IsActiveLocked(out string? stationId)
    {
        stationId = _stationId;
        return _process is { HasExited: false };
    }

    private void StopLocked()
    {
        if (_process is { HasExited: false })
            _process.Kill(entireProcessTree: true);
        _process?.Dispose();
        _process = null;
        _hub = null;
        _stationId = null;
    }

    public void Stop()
    {
        lock (_lock) StopLocked();
    }

    public void Dispose()
    {
        lock (_lock) StopLocked();
    }

    /// <summary>
    /// Fans out a single ffmpeg stdout stream to any number of HTTP clients.
    /// <para>
    /// One task (<see cref="PumpAsync"/>) is the only reader of the ffmpeg pipe, so client
    /// connects, disconnects and cancellations can never interfere with it or swallow data.
    /// Each chunk read is appended to a bounded in-memory backlog and pushed to every
    /// subscriber's own channel.
    /// </para>
    /// <para>
    /// <see cref="Subscribe"/> atomically snapshots the backlog and registers the subscriber,
    /// so a new client gets the recent audio immediately (starting its player buffer full)
    /// and then continues live, with no gap or duplicate between the two. This absorbs
    /// segment-boundary jitter from the HLS source at the cost of a few seconds of delay.
    /// </para>
    /// <para>
    /// Slow clients never block the pump or other clients: per-client channels are bounded
    /// with <c>DropOldest</c>, so a stalled client loses audio rather than back-pressuring
    /// ffmpeg. The backlog is sized in bytes, not time (256 KB is about 21 s at 96 kbps).
    /// The backlog starts empty and only fills as ffmpeg runs, and the first chunk of a
    /// snapshot may begin mid ADTS frame, which decoders should resync past.
    /// </para>
    /// <para>
    /// Thread safety: all shared state is guarded by a single lock, and nothing awaits while
    /// holding it. Call <see cref="Complete"/> when the pump ends to close every subscriber.
    /// </para>
    /// </summary>
    public sealed class StreamHub
    {
        private readonly object _lock = new();
        private readonly LinkedList<byte[]> _backlog = new();
        private long _backlogBytes;
        private readonly long _maxBacklogBytes; // e.g. 10s * 16KB/s (128kbps) ≈ 160KB; use 256KB
        private readonly List<Channel<byte[]>> _subs = new();

        public StreamHub(long maxBacklogBytes = 256 * 1024) => _maxBacklogBytes = maxBacklogBytes;

        // Called from the single task that reads ffmpeg stdout
        public async Task PumpAsync(Stream ffmpegStdout, CancellationToken ct)
        {
            var buf = new byte[16 * 1024];
            int n;
            while ((n = await ffmpegStdout.ReadAsync(buf, ct)) > 0)
            {
                var chunk = buf.AsSpan(0, n).ToArray();
                lock (_lock)
                {
                    _backlog.AddLast(chunk);
                    _backlogBytes += n;
                    while (_backlogBytes > _maxBacklogBytes && _backlog.First != null)
                    {
                        _backlogBytes -= _backlog.First.Value.Length;
                        _backlog.RemoveFirst();
                    }

                    foreach (var s in _subs)
                        s.Writer.TryWrite(chunk); // never blocks on slow clients
                }
            }
        }

        public (byte[][] backlog, ChannelReader<byte[]> live, Action unsubscribe) Subscribe()
        {
            // Bounded + DropOldest so one stalled client can't grow memory unbounded
            var ch = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(512)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });
            lock (_lock)
            {
                var snapshot = _backlog.ToArray(); // snapshot and subscribe atomically: no gap, no duplicate
                _subs.Add(ch);
                return (snapshot, ch.Reader, () =>
                {
                    lock (_lock) _subs.Remove(ch);
                    ch.Writer.TryComplete();
                });
            }
        }

        public void Complete()
        {
            lock (_lock)
                foreach (var s in _subs)
                    s.Writer.TryComplete();
        }
    }
}