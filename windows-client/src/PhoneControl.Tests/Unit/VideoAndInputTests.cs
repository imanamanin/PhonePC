using FluentAssertions;
using PhoneControl.Application;
using PhoneControl.Domain;
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
    public void H264AnnexB_ConvertsAvccToStartCodes()
    {
        var avcc = new byte[] { 0, 0, 0, 1, 0x67, 0x42 };
        // length-prefixed 1-byte NAL 0x67 would be 00 00 00 01 67 — already annex-b.
        H264AnnexB.HasStartCode(avcc).Should().BeTrue();
        var lengthPrefixed = new byte[] { 0, 0, 0, 2, 0x67, 0x42 };
        var annex = H264AnnexB.Normalize(lengthPrefixed);
        annex.Should().Equal(0, 0, 0, 1, 0x67, 0x42);
        H264AnnexB.ContainsSps(new byte[] { 0, 0, 0, 1, 0x67, 0x42 }).Should().BeTrue();
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
    public void AudioPacket_RoundTrip()
    {
        var pcm = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var packet = new AudioPacket(48000, 2, 16, 99, pcm);
        var decoded = AudioPacketCodec.Decode(AudioPacketCodec.Encode(packet));
        decoded.SampleRate.Should().Be(48000);
        decoded.Channels.Should().Be(2);
        decoded.BitsPerSample.Should().Be(16);
        decoded.CaptureTimestampMs.Should().Be(99);
        decoded.Pcm.ToArray().Should().Equal(pcm);
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
    public async Task Pointer_ClickBecomesTap()
    {
        var sent = new List<MessageEnvelope>();
        var controller = new PointerInputController(new RecordingInputClient(sent), new StubClock())
        {
            Screen = new PhoneScreen(1080, 2400, 0)
        };
        var surface = new WindowRect(108, 240);
        await controller.HandleAsync(
            NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventLeftDown),
            surface, 54, 120, CancellationToken.None);
        await controller.HandleAsync(
            NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventLeftUp),
            surface, 54, 120, CancellationToken.None);
        sent.Should().ContainSingle();
        var payload = EnvelopeFactory.ReadPayload<InputTouchPayload>(sent[0]);
        payload!.Action.Should().Be("tap");
        payload.DurationMs.Should().Be(60);
    }

    [Fact]
    public async Task Pointer_RightClickTurnsPage()
    {
        var sent = new List<MessageEnvelope>();
        var controller = new PointerInputController(new RecordingInputClient(sent), new StubClock())
        {
            Screen = new PhoneScreen(1080, 2400, 0),
            RightClickDelay = TimeSpan.Zero
        };
        await controller.HandleAsync(
            NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventRightUp),
            new WindowRect(108, 240), 10, 10, CancellationToken.None);
        sent.Should().ContainSingle();
        var payload = EnvelopeFactory.ReadPayload<InputTouchPayload>(sent[0]);
        payload!.Action.Should().Be("swipe");
        payload.X2.Should().BeLessThan(payload.X);
    }

    [Fact]
    public async Task Pointer_DoubleRightClickTurnsOtherWay()
    {
        var sent = new List<MessageEnvelope>();
        var controller = new PointerInputController(new RecordingInputClient(sent), new StubClock())
        {
            Screen = new PhoneScreen(1080, 2400, 0),
            RightClickDelay = TimeSpan.FromMilliseconds(250)
        };
        var first = controller.HandleAsync(
            NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventRightUp),
            new WindowRect(108, 240), 10, 10, CancellationToken.None);
        await controller.HandleAsync(
            NativeSendInput.FromMouse(0, 0, NativeSendInput.MouseEventRightUp),
            new WindowRect(108, 240), 10, 10, CancellationToken.None);
        await first;
        sent.Should().ContainSingle();
        var payload = EnvelopeFactory.ReadPayload<InputTouchPayload>(sent[0]);
        payload!.Action.Should().Be("swipe");
        payload.X2.Should().BeGreaterThan(payload.X);
    }

    [Fact]
    public void TextPayload_KeepsPersianLetters()
    {
        var ev = new Win32InputEvent(Win32InputKind.TextInput, 0, 0, 0, 1, "سلام");
        var envelope = Win32InputMap.ToEnvelope(ev, null, 1);
        MessageSerializer.SerializeToString(envelope).Should().Contain("سلام");
        EnvelopeFactory.ReadPayload<InputKeyPayload>(envelope)!.Text.Should().Be("سلام");
    }

    [Fact]
    public void Backspace_IsNotAndroidBack()
    {
        Win32InputMap.MapKey(Win32InputMap.VkBack).Should().Be("backspace");
        Win32InputMap.MapKey(Win32InputMap.VkEscape).Should().Be("back");
    }

    [Fact]
    public void Win32Map_TextInput_SendsTypePayload()
    {
        var ev = new Win32InputEvent(Win32InputKind.TextInput, 0, 0, 0, 1, "a");
        var envelope = Win32InputMap.ToEnvelope(ev, null, 1);
        var payload = EnvelopeFactory.ReadPayload<InputKeyPayload>(envelope);
        payload!.Action.Should().Be("type");
        payload.Text.Should().Be("a");
    }

    [Fact]
    public void Win32Map_Pinch_UsesInputPinch()
    {
        var ev = new Win32InputEvent(Win32InputKind.Pinch, 0, 0, 40, 1.25);
        var envelope = Win32InputMap.ToEnvelope(ev, new PhonePoint(10, 20), 1);
        envelope.Type.Should().Be(MessageTypes.InputPinch);
        EnvelopeFactory.ReadPayload<InputPinchPayload>(envelope)!.Scale.Should().Be(1.25);
    }

    private sealed class RecordingInputClient : IRemoteInputClient
    {
        private readonly List<MessageEnvelope> _sent;

        public RecordingInputClient(List<MessageEnvelope> sent) => _sent = sent;

        public Task SendAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
        {
            _sent.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class StubClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddSeconds(1);
    }
}
