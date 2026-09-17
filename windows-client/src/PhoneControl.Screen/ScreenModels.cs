using System.Buffers.Binary;
using PhoneControl.Domain;

namespace PhoneControl.Screen;

public enum VideoCodecId : byte
{
    H264 = 1,
    H265 = 2,
    Jpeg = 3,
    RawBgra = 4
}

[Flags]
public enum VideoPacketFlags : byte
{
    None = 0,
    Keyframe = 1,
    Config = 2
}

public sealed record VideoPacket(
    VideoCodecId Codec,
    int Width,
    int Height,
    long CaptureTimestampMs,
    VideoPacketFlags Flags,
    ReadOnlyMemory<byte> Payload)
{
    public bool Keyframe => Flags.HasFlag(VideoPacketFlags.Keyframe);
}

public static class VideoPacketCodec
{
    public const int HeaderSize = 14;

    public static byte[] Encode(VideoPacket packet)
    {
        var payload = packet.Payload.Span;
        var buffer = new byte[HeaderSize + payload.Length];
        buffer[0] = (byte)packet.Flags;
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(1, 8), packet.CaptureTimestampMs);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(9, 2), (ushort)packet.Width);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(11, 2), (ushort)packet.Height);
        buffer[13] = (byte)packet.Codec;
        payload.CopyTo(buffer.AsSpan(HeaderSize));
        return buffer;
    }

    public static VideoPacket Decode(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HeaderSize)
        {
            throw new InvalidOperationException("Video packet header is truncated.");
        }

        var flags = (VideoPacketFlags)buffer[0];
        var timestamp = BinaryPrimitives.ReadInt64BigEndian(buffer.Slice(1, 8));
        var width = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(9, 2));
        var height = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(11, 2));
        var codec = (VideoCodecId)buffer[13];
        var payload = buffer[HeaderSize..].ToArray();
        return new VideoPacket(codec, width, height, timestamp, flags, payload);
    }
}

public sealed record DecodedFrame(
    int Width,
    int Height,
    byte[] Bgra,
    long CaptureTimestampMs,
    long ReceivedTimestampMs);

public interface IVideoDecoder
{
    bool TryDecode(VideoPacket packet, long receivedTimestampMs, out DecodedFrame? frame);
}

public interface IVideoEncoder
{
    VideoPacket Encode(DecodedFrame frame, VideoCodecId codec, VideoPacketFlags flags);
}

public sealed class RawBgraCodec : IVideoDecoder, IVideoEncoder
{
    public bool TryDecode(VideoPacket packet, long receivedTimestampMs, out DecodedFrame? frame)
    {
        frame = null;
        if (packet.Codec != VideoCodecId.RawBgra)
        {
            return false;
        }

        var expected = packet.Width * packet.Height * 4;
        if (packet.Payload.Length != expected)
        {
            return false;
        }

        frame = new DecodedFrame(
            packet.Width,
            packet.Height,
            packet.Payload.ToArray(),
            packet.CaptureTimestampMs,
            receivedTimestampMs);
        return true;
    }

    public VideoPacket Encode(DecodedFrame frame, VideoCodecId codec, VideoPacketFlags flags)
    {
        if (codec != VideoCodecId.RawBgra)
        {
            throw new ArgumentOutOfRangeException(nameof(codec));
        }

        return new VideoPacket(
            VideoCodecId.RawBgra,
            frame.Width,
            frame.Height,
            frame.CaptureTimestampMs,
            flags | VideoPacketFlags.Keyframe,
            frame.Bgra);
    }
}

public sealed record StreamHud(
    int LatencyMs,
    int SmoothedLatencyMs,
    int Fps,
    int Width,
    int Height,
    int BitrateKbps,
    string Codec);

public sealed class LatencyMeter
{
    private double _ema;

    public int LastMs { get; private set; }
    public int SmoothedMs => (int)Math.Round(_ema);

    public int Observe(long captureTimestampMs, long receivedTimestampMs, int oneWayShiftMs = 0)
    {
        var raw = (int)Math.Clamp(receivedTimestampMs - captureTimestampMs - oneWayShiftMs, 0, 60_000);
        LastMs = raw;
        _ema = _ema <= 0 ? raw : (_ema * 0.8) + (raw * 0.2);
        return raw;
    }
}

public sealed record VideoQualityTier(int MaxWidth, int MaxFps, int BitrateKbps);

public sealed class AdaptiveStreamController
{
    public static readonly VideoQualityTier High = new(1280, 30, 4000);
    public static readonly VideoQualityTier Medium = new(720, 24, 2000);
    public static readonly VideoQualityTier Low = new(540, 15, 1000);
    public static readonly VideoQualityTier Minimal = new(360, 10, 600);

    private readonly TimeSpan _minInterval;
    private DateTimeOffset _lastEmit = DateTimeOffset.MinValue;
    private VideoQualityTier _current = High;

    public AdaptiveStreamController(TimeSpan? minInterval = null)
    {
        _minInterval = minInterval ?? TimeSpan.FromSeconds(2);
    }

    public VideoQualityTier Current => _current;

    public VideoQualityTier? Recommend(double bytesPerSecond, int latencyMs, DateTimeOffset now)
    {
        var bitrateKbps = bytesPerSecond * 8.0 / 1000.0;
        VideoQualityTier next;
        if (latencyMs > 250 || bitrateKbps < 700)
        {
            next = Minimal;
        }
        else if (latencyMs > 160 || bitrateKbps < 1500)
        {
            next = Low;
        }
        else if (latencyMs > 90 || bitrateKbps < 3000)
        {
            next = Medium;
        }
        else
        {
            next = High;
        }

        if (next == _current || now - _lastEmit < _minInterval)
        {
            _current = next;
            return null;
        }

        _current = next;
        _lastEmit = now;
        return next;
    }
}

public sealed class StreamDebugStats
{
    public int Fps { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int LatencyMs { get; init; }
}

public sealed record AudioPacket(
    int SampleRate,
    int Channels,
    int BitsPerSample,
    long CaptureTimestampMs,
    ReadOnlyMemory<byte> Pcm)
{
    public const int Pcm16 = 1;
}

public static class AudioPacketCodec
{
    public const int HeaderSize = 16;

    public static byte[] Encode(AudioPacket packet)
    {
        var pcm = packet.Pcm.Span;
        var buffer = new byte[HeaderSize + pcm.Length];
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(1, 8), packet.CaptureTimestampMs);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(9, 4), packet.SampleRate);
        buffer[13] = (byte)packet.Channels;
        buffer[14] = (byte)packet.BitsPerSample;
        buffer[15] = AudioPacket.Pcm16;
        pcm.CopyTo(buffer.AsSpan(HeaderSize));
        return buffer;
    }

    public static AudioPacket Decode(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HeaderSize)
        {
            throw new InvalidOperationException("Audio packet header is truncated.");
        }

        var timestamp = BinaryPrimitives.ReadInt64BigEndian(buffer.Slice(1, 8));
        var sampleRate = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(9, 4));
        var channels = buffer[13];
        var bits = buffer[14];
        var pcm = buffer[HeaderSize..].ToArray();
        return new AudioPacket(sampleRate, channels, bits, timestamp, pcm);
    }
}
