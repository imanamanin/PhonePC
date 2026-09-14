using System.IO;
using System.Windows.Media.Imaging;
using PhoneControl.Screen;

namespace PhoneControl.Desktop.Decoding;

public sealed class JpegWicDecoder : IVideoDecoder
{
    public bool TryDecode(VideoPacket packet, long receivedTimestampMs, out DecodedFrame? frame)
    {
        frame = null;
        if (packet.Codec != VideoCodecId.Jpeg)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(packet.Payload.ToArray(), writable: false);
            var decoder = new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var source = decoder.Frames[0];
            var converted = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            var pixels = new byte[width * height * 4];
            converted.CopyPixels(pixels, width * 4, 0);
            frame = new DecodedFrame(width, height, pixels, packet.CaptureTimestampMs, receivedTimestampMs);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>
/// Tries H.264 (Media Foundation), then JPEG (WIC), then raw BGRA (tests/loopback).
/// </summary>
public sealed class CompositeVideoDecoder : IVideoDecoder
{
    private readonly IVideoDecoder[] _decoders;

    public CompositeVideoDecoder(params IVideoDecoder[] decoders)
    {
        _decoders = decoders;
    }

    public bool TryDecode(VideoPacket packet, long receivedTimestampMs, out DecodedFrame? frame)
    {
        foreach (var decoder in _decoders)
        {
            if (decoder.TryDecode(packet, receivedTimestampMs, out frame) && frame is not null)
            {
                return true;
            }
        }

        frame = null;
        return false;
    }
}
