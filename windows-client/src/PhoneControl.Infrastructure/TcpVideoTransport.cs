using System.Net.Sockets;
using System.Runtime.CompilerServices;
using PhoneControl.Domain;

namespace PhoneControl.Infrastructure;

public sealed class TcpVideoTransportFactory : IVideoTransportFactory
{
    public IVideoTransport Create() => new TcpVideoTransport();
}

/// <summary>Channel C: length-prefixed video packets on port 17891. Never toggles USB.</summary>
public sealed class TcpVideoTransport : IVideoTransport
{
    private TcpClient? _client;
    private NetworkStream? _stream;

    public bool IsConnected => _client?.Connected == true;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        await DisposeAsync().ConfigureAwait(false);
        _client = new TcpClient();
        await _client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        _stream = _client.GetStream();
    }

    public async Task SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Video transport is not connected.");
        }

        await LengthPrefixedFrame.WriteAsync(_stream, packet, ProtocolPorts.MaxVideoFrameBytes, cancellationToken)
            .ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadPacketsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Video transport is not connected.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var body = await LengthPrefixedFrame.ReadAsync(_stream, ProtocolPorts.MaxVideoFrameBytes, cancellationToken)
                .ConfigureAwait(false);
            yield return body;
        }
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        return ValueTask.CompletedTask;
    }
}
