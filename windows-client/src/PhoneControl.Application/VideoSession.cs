using PhoneControl.Domain;
using PhoneControl.Protocol;
using PhoneControl.Screen;

namespace PhoneControl.Application;

public sealed class VideoSession : IAsyncDisposable
{
    private readonly ConnectionManager _connection;
    private readonly IVideoDecoder _decoder;
    private readonly IClock _clock;
    private readonly IVideoTransportFactory _transportFactory;
    private readonly AdaptiveStreamController _adaptive = new();
    private readonly LatencyMeter _latency = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private long _bytesWindow;
    private DateTimeOffset _bytesWindowStart = DateTimeOffset.MinValue;
    private int _frames;
    private DateTimeOffset _fpsWindow = DateTimeOffset.MinValue;

    public VideoSession(
        ConnectionManager connection,
        IVideoDecoder decoder,
        IClock clock,
        IVideoTransportFactory transportFactory)
    {
        _connection = connection;
        _decoder = decoder;
        _clock = clock;
        _transportFactory = transportFactory;
        _connection.StateChanged += OnStateChanged;
    }

    public event EventHandler<DecodedFrame>? FrameArrived;

    public event EventHandler<StreamHud>? HudChanged;

    public StreamHud Hud { get; private set; } = new(0, 0, 0, 0, 0, 0, "none");

    private void OnStateChanged(object? sender, ConnectionSnapshot snapshot)
    {
        try
        {
            if (snapshot.State is ConnectionState.Connected)
            {
                Start(snapshot);
            }
            else
            {
                Stop();
            }
        }
        catch (Exception)
        {
            Stop();
        }
    }

    private void Start(ConnectionSnapshot snapshot)
    {
        if (_loop is { IsCompleted: false })
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = RunAsync(snapshot, _cts.Token);
    }

    private void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already stopped.
        }
    }

    private async Task RunAsync(ConnectionSnapshot snapshot, CancellationToken cancellationToken)
    {
        var host = HostOf(snapshot.Endpoint);
        if (host is null)
        {
            return;
        }

        await TryStartCaptureAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await PumpAsync(host, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    PublishHud(Hud with { Codec = "offline" });
                    await TryStartCaptureAsync(cancellationToken).ConfigureAwait(false);
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Session ended.
        }
        finally
        {
            await TrySendAsync(MessageTypes.VideoStop, null, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task PumpAsync(string host, CancellationToken cancellationToken)
    {
        await using var transport = _transportFactory.Create();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(5));
        await transport.ConnectAsync(host, ProtocolPorts.Screen, connectCts.Token).ConfigureAwait(false);

        await foreach (var raw in transport.ReadPacketsAsync(cancellationToken).ConfigureAwait(false))
        {
            var received = _clock.UtcNow.ToUnixTimeMilliseconds();
            VideoPacket packet;
            try
            {
                packet = VideoPacketCodec.Decode(raw.Span);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            NoteThroughput(raw.Length, _clock.UtcNow);
            if (!_decoder.TryDecode(packet, received, out var frame) || frame is null)
            {
                PublishHud(Hud with { Codec = packet.Codec.ToString() });
                continue;
            }

            _frames++;
            var latency = _latency.Observe(frame.CaptureTimestampMs, received);
            var fps = Fps(_clock.UtcNow);
            Hud = new StreamHud(
                latency,
                _latency.SmoothedMs,
                fps,
                frame.Width,
                frame.Height,
                _adaptive.Current.BitrateKbps,
                packet.Codec.ToString());
            HudChanged?.Invoke(this, Hud);
            FrameArrived?.Invoke(this, frame);

            var tier = _adaptive.Recommend(BytesPerSecond(_clock.UtcNow), latency, _clock.UtcNow);
            if (tier is not null)
            {
                await TrySendAsync(
                    MessageTypes.VideoAdapt,
                    new VideoAdaptPayload
                    {
                        MaxFps = tier.MaxFps,
                        MaxWidth = tier.MaxWidth,
                        BitrateKbps = tier.BitrateKbps
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task TryStartCaptureAsync(CancellationToken cancellationToken)
    {
        if (!_connection.CanSend)
        {
            return;
        }

        try
        {
            var reply = await _connection.RequestAsync(
                EnvelopeFactory.Create(
                    MessageTypes.VideoStart,
                    _clock.UtcNow.ToUnixTimeMilliseconds(),
                    new VideoStartPayload()),
                TimeSpan.FromSeconds(3),
                cancellationToken).ConfigureAwait(false);
            if (string.Equals(reply?.Error?.Code, "PERMISSION_DENIED", StringComparison.Ordinal))
            {
                PublishHud(Hud with { Codec = "need-capture" });
            }
        }
        catch (Exception)
        {
            // Control channel already closed.
        }
    }

    private async Task TrySendAsync(string type, object? payload, CancellationToken cancellationToken)
    {
        if (!_connection.CanSend)
        {
            return;
        }

        try
        {
            await _connection.SendAsync(
                EnvelopeFactory.Create(type, _clock.UtcNow.ToUnixTimeMilliseconds(), payload),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Control channel already closed.
        }
    }

    private void PublishHud(StreamHud hud)
    {
        Hud = hud;
        HudChanged?.Invoke(this, hud);
    }

    private void NoteThroughput(int bytes, DateTimeOffset now)
    {
        if (_bytesWindowStart == DateTimeOffset.MinValue)
        {
            _bytesWindowStart = now;
        }

        _bytesWindow += bytes;
        if (now - _bytesWindowStart > TimeSpan.FromSeconds(1))
        {
            _bytesWindow = bytes;
            _bytesWindowStart = now;
        }
    }

    private double BytesPerSecond(DateTimeOffset now)
    {
        var elapsed = (now - _bytesWindowStart).TotalSeconds;
        return elapsed <= 0 ? 0 : _bytesWindow / elapsed;
    }

    private int Fps(DateTimeOffset now)
    {
        if (_fpsWindow == DateTimeOffset.MinValue)
        {
            _fpsWindow = now;
        }

        var elapsed = (now - _fpsWindow).TotalSeconds;
        if (elapsed >= 1)
        {
            var fps = (int)Math.Round(_frames / elapsed);
            _frames = 0;
            _fpsWindow = now;
            return fps;
        }

        return Hud.Fps;
    }

    private static string? HostOf(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var parts = endpoint.Split(':');
        return parts[0];
    }

    public async ValueTask DisposeAsync()
    {
        _connection.StateChanged -= OnStateChanged;
        Stop();
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }

        _cts?.Dispose();
    }
}
