using System.Net.Sockets;
using System.Runtime.CompilerServices;
using PhoneControl.Domain;

namespace PhoneControl.Infrastructure;

/// <summary>
/// Length-prefixed TCP transport. Must target the tethering IP, never toggle USB mode.
/// </summary>
public sealed class TcpControlTransport : IControlTransport
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

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Transport is not connected.");
        }

        await LengthPrefixedFrame.WriteAsync(_stream, frame, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Transport is not connected.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var body = await LengthPrefixedFrame.ReadAsync(_stream, cancellationToken).ConfigureAwait(false);
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
