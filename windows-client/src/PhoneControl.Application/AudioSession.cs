using PhoneControl.Domain;
using PhoneControl.Screen;

namespace PhoneControl.Application;

public interface IAudioPlayer : IDisposable
{
    void PlayPcm(int sampleRate, int channels, int bitsPerSample, ReadOnlyMemory<byte> pcm);
    void Stop();
}

public sealed class NullAudioPlayer : IAudioPlayer
{
    public void PlayPcm(int sampleRate, int channels, int bitsPerSample, ReadOnlyMemory<byte> pcm)
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}

public sealed class AudioSession : IAsyncDisposable
{
    private readonly ConnectionManager _connection;
    private readonly IVideoTransportFactory _transportFactory;
    private readonly IAudioPlayer _player;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public AudioSession(
        ConnectionManager connection,
        IVideoTransportFactory transportFactory,
        IAudioPlayer player)
    {
        _connection = connection;
        _transportFactory = transportFactory;
        _player = player;
        _connection.StateChanged += OnStateChanged;
    }

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

        _player.Stop();
    }

    private async Task RunAsync(ConnectionSnapshot snapshot, CancellationToken cancellationToken)
    {
        var host = HostOf(snapshot.Endpoint);
        if (host is null)
        {
            return;
        }

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
                    await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Session ended.
        }
        finally
        {
            _player.Stop();
        }
    }

    private async Task PumpAsync(string host, CancellationToken cancellationToken)
    {
        await using var transport = _transportFactory.Create();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(5));
        await transport.ConnectAsync(host, ProtocolPorts.Audio, connectCts.Token).ConfigureAwait(false);

        await foreach (var raw in transport.ReadPacketsAsync(cancellationToken).ConfigureAwait(false))
        {
            AudioPacket packet;
            try
            {
                packet = AudioPacketCodec.Decode(raw.Span);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (packet.SampleRate < 8000 || packet.Channels <= 0 || packet.Pcm.Length < 4)
            {
                continue;
            }

            _player.PlayPcm(packet.SampleRate, packet.Channels, packet.BitsPerSample, packet.Pcm);
        }
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
        _player.Dispose();
    }
}
