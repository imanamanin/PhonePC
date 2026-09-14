using FluentAssertions;
using PhoneControl.Input;
using PhoneControl.Protocol;
using PhoneControl.Screen;

namespace PhoneControl.Tests.Unit;

public sealed class VideoAndInputTests
{
    [Fact]
    public void VideoPacket_RoundTrip()
    {
        var packet = new VideoPacket(VideoCodecId.H264, 720, 1280, 99, VideoPacketFlags.Keyframe | VideoPacketFlags.Config, new byte[] { 0, 0, 0, 1, 103 });
        var decoded = VideoPacketCodec.Decode(VideoPacketCodec.Encode(packet));
        decoded.Width.Should().Be(720);
        decoded.Height.Should().Be(1280);
        decoded.CaptureTimestampMs.Should().Be(99);
        decoded.Keyframe.Should().BeTrue();
        decoded.Payload.ToArray().Should().Equal(0, 0, 0, 1, 103);
    }

    [Fact]
    public void RawCodec_Loopback()
    {
        var codec = new RawBgraCodec();
        var frame = new DecodedFrame(1, 1, new byte[] { 9, 8, 7, 255 }, 1, 2);
        var packet = codec.Encode(frame, VideoCodecId.RawBgra, VideoPacketFlags.None);
        codec.TryDecode(packet, 5, out var decoded).Should().BeTrue();
        decoded!.Bgra.Should().Equal(9, 8, 7, 255);
    }

    [Fact]
    public void AdaptiveController_DropsTier_OnHighLatency()
    {
        var controller = new AdaptiveStreamController(TimeSpan.Zero);
        var tier = controller.Recommend(bytesPerSecond: 5_000_000, latencyMs: 400, DateTimeOffset.UtcNow);
        tier.Should().NotBeNull();
        tier!.MaxWidth.Should().Be(AdaptiveStreamController.Minimal.MaxWidth);
    }

    [Fact]
    public void AdaptiveController_ThrottlesEmits()
    {
        var controller = new AdaptiveStreamController(TimeSpan.FromSeconds(10));
        var now = DateTimeOffset.UtcNow;
        controller.Recommend(100, 400, now).Should().NotBeNull();
        controller.Recommend(100, 400, now).Should().BeNull();
    }

    [Fact]
    public void LatencyMeter_IsNonNegative()
    {
        var meter = new LatencyMeter();
        meter.Observe(1000, 1042).Should().Be(42);
        meter.SmoothedMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Win32Map_MouseDown_BecomesTouchDown()
    {
        var ev = NativeSendInput.FromMouse(10, 20, NativeSendInput.MouseEventLeftDown);
        var envelope = Win32InputMap.ToEnvelope(ev, new PhonePoint(11, 22), 1);
        envelope.Type.Should().Be(MessageTypes.InputTouch);
        EnvelopeFactory.ReadPayload<InputTouchPayload>(envelope)!.Action.Should().Be("down");
    }

    [Fact]
    public void Win32Map_Escape_BecomesBack()
    {
        var ev = new Win32InputEvent(Win32InputKind.KeyDown, 0, 0, Win32InputMap.VkEscape, 1);
        var envelope = Win32InputMap.ToEnvelope(ev, null, 1);
        envelope.Type.Should().Be(MessageTypes.InputKey);
        EnvelopeFactory.ReadPayload<InputKeyPayload>(envelope)!.Key.Should().Be("back");
    }

    [Fact]
    public void CommandCatalog_KnowsVideoAndPinch()
    {
        CommandCatalog.CanExecute(MessageTypes.VideoStart).Should().BeTrue();
        CommandCatalog.CanExecute(MessageTypes.VideoStop).Should().BeTrue();
        CommandCatalog.CanExecute(MessageTypes.VideoAdapt).Should().BeTrue();
        CommandCatalog.CanExecute(MessageTypes.VideoFrame).Should().BeTrue();
        CommandCatalog.CanExecute(MessageTypes.InputPinch).Should().BeTrue();
    }

    [Fact]
    public void Nv12_ConvertsKnownSize()
    {
        var width = 2;
        var height = 2;
        var nv12 = new byte[width * height + width * height / 2];
        Array.Fill(nv12, (byte)128);
        nv12[0] = 16;
        var bgra = YuvConvert.Nv12ToBgra(nv12, width, height);
        bgra.Should().HaveCount(16);
        bgra[3].Should().Be(255);
        bgra = YuvConvert.Nv12ToBgra(nv12, width, height, stride: 2);
        bgra.Should().HaveCount(16);
    }

    [Fact]
    public void TruncatedVideoPacket_Throws()
    {
        var act = () => VideoPacketCodec.Decode(new byte[] { 1, 2, 3 });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Win32Map_Wheel_BecomesScroll()
    {
        var ev = NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventWheel, 120);
        var envelope = Win32InputMap.ToEnvelope(ev, new PhonePoint(11, 22), 1);
        var payload = EnvelopeFactory.ReadPayload<InputTouchPayload>(envelope);
        payload!.Action.Should().Be("scroll");
        payload.Delta.Should().Be(120);
    }

    [Fact]
    public void Win32Map_Pinch_UsesInputPinch()
    {
        var ev = new Win32InputEvent(Win32InputKind.Pinch, 0, 0, 40, 1.25);
        var envelope = Win32InputMap.ToEnvelope(ev, new PhonePoint(10, 20), 1);
        envelope.Type.Should().Be(MessageTypes.InputPinch);
        EnvelopeFactory.ReadPayload<InputPinchPayload>(envelope)!.Scale.Should().Be(1.25);
    }
}
