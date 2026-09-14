using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using PhoneControl.Domain;
using PhoneControl.Infrastructure;
using PhoneControl.Screen;

namespace PhoneControl.Tests.Integration;

public sealed class VideoFramingTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Loopback_RoundTripsRawFrame()
    {
        var codec = new RawBgraCodec();
        var original = new DecodedFrame(2, 2, Enumerable.Range(0, 16).Select(i => (byte)i).ToArray(), 123, 0);
        var packet = codec.Encode(original, VideoCodecId.RawBgra, VideoPacketFlags.Keyframe);
        var encoded = VideoPacketCodec.Encode(packet);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            await LengthPrefixedFrame.WriteAsync(stream, encoded, ProtocolPorts.MaxVideoFrameBytes, CancellationToken.None);
        });

        await using var transport = new TcpVideoTransport();
        await transport.ConnectAsync("127.0.0.1", port, CancellationToken.None);
        ReadOnlyMemory<byte>? received = null;
        await foreach (var body in transport.ReadPacketsAsync(CancellationToken.None))
        {
            received = body;
            break;
        }

        await serverTask;
        listener.Stop();
        received.Should().NotBeNull();
        var decodedPacket = VideoPacketCodec.Decode(received!.Value.Span);
        codec.TryDecode(decodedPacket, 200, out var frame).Should().BeTrue();
        frame!.Bgra.Should().Equal(original.Bgra);
        frame.CaptureTimestampMs.Should().Be(123);
        decodedPacket.Keyframe.Should().BeTrue();
        decodedPacket.Codec.Should().Be(VideoCodecId.RawBgra);
    }
}
