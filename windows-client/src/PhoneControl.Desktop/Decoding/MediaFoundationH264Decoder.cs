using System.Runtime.InteropServices;
using PhoneControl.Screen;

namespace PhoneControl.Desktop.Decoding;

/// <summary>
/// H.264/H.265 decoder using the inbox Windows Media Foundation MFT (DXVA when the OS allows it).
/// Chosen over FFmpeg because it ships with Windows, needs no redistributable, and avoids LGPL shipping.
/// Behind <see cref="IVideoDecoder"/> so unit tests never load mfplat.
/// </summary>
public sealed class MediaFoundationH264Decoder : IVideoDecoder, IDisposable
{
    private readonly object _gate = new();
    private bool _started;
    private IMFTransform? _transform;
    private VideoCodecId _codec;
    private int _width;
    private int _height;
    private bool _providesSamples;

    public static IVideoDecoder? TryCreate()
    {
        try
        {
            var decoder = new MediaFoundationH264Decoder();
            if (!decoder.Startup())
            {
                decoder.Dispose();
                return null;
            }

            return decoder;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    public bool TryDecode(VideoPacket packet, long receivedTimestampMs, out DecodedFrame? frame)
    {
        frame = null;
        if (packet.Codec is not (VideoCodecId.H264 or VideoCodecId.H265))
        {
            return false;
        }

        if (packet.Payload.Length == 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (!Startup())
            {
                return false;
            }

            try
            {
                if (_transform is null || _codec != packet.Codec || _width != packet.Width || _height != packet.Height)
                {
                    if (!CreateTransform(packet))
                    {
                        return false;
                    }
                }

                if (!ProcessNal(packet))
                {
                    return false;
                }

                return TryReadFrame(packet, receivedTimestampMs, out frame);
            }
            catch (COMException)
            {
                ReleaseTransform();
                return false;
            }
            catch (InvalidCastException)
            {
                ReleaseTransform();
                return false;
            }
            catch (SEHException)
            {
                ReleaseTransform();
                return false;
            }
        }
    }

    private bool CreateTransform(VideoPacket packet)
    {
        ReleaseTransform();
        var clsid = packet.Codec == VideoCodecId.H265 ? MfGuids.H265Decoder : MfGuids.H264Decoder;
        var iid = MfGuids.Transform;
        var hr = MfPlat.CoCreateInstance(clsid, IntPtr.Zero, 1, iid, out var unk);
        if (hr < 0 || unk == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            _transform = (IMFTransform)Marshal.GetObjectForIUnknown(unk);
        }
        finally
        {
            Marshal.Release(unk);
        }

        if (_transform is null)
        {
            return false;
        }

        if (MfPlat.MFCreateMediaType(out var input) < 0)
        {
            return false;
        }

        var major = MfGuids.MajorType;
        var video = MfGuids.Video;
        var subtypeKey = MfGuids.Subtype;
        var subtype = packet.Codec == VideoCodecId.H265 ? MfGuids.Hevc : MfGuids.H264;
        var interlaceKey = MfGuids.InterlaceMode;
        var frameKey = MfGuids.FrameSize;
        if (input.SetGUID(ref major, ref video) < 0 ||
            input.SetGUID(ref subtypeKey, ref subtype) < 0 ||
            input.SetUINT32(ref interlaceKey, MfPlat.Progressive) < 0 ||
            input.SetUINT64(ref frameKey, PackSize(packet.Width, packet.Height)) < 0)
        {
            return false;
        }

        if (_transform.SetInputType(0, input, 0) < 0)
        {
            return false;
        }

        IMFMediaType? nv12 = null;
        for (var i = 0; i < 32; i++)
        {
            if (_transform.GetOutputAvailableType(0, i, out var candidate) < 0)
            {
                break;
            }

            if (candidate.GetGUID(ref subtypeKey, out var guid) >= 0 && guid == MfGuids.Nv12)
            {
                nv12 = candidate;
                break;
            }
        }

        if (nv12 is null)
        {
            if (MfPlat.MFCreateMediaType(out var output) < 0)
            {
                return false;
            }

            var nv = MfGuids.Nv12;
            if (output.SetGUID(ref major, ref video) < 0 ||
                output.SetGUID(ref subtypeKey, ref nv) < 0 ||
                output.SetUINT32(ref interlaceKey, MfPlat.Progressive) < 0 ||
                output.SetUINT64(ref frameKey, PackSize(packet.Width, packet.Height)) < 0)
            {
                return false;
            }

            nv12 = output;
        }

        if (_transform.SetOutputType(0, nv12, 0) < 0)
        {
            return false;
        }

        if (_transform.GetOutputStreamInfo(0, out var info) >= 0)
        {
            _providesSamples = (info.Flags & MfPlat.ProvidesSamples) != 0;
        }

        _ = _transform.ProcessMessage(MfPlat.BeginStreaming, IntPtr.Zero);
        _ = _transform.ProcessMessage(MfPlat.StartOfStream, IntPtr.Zero);
        _codec = packet.Codec;
        _width = packet.Width;
        _height = packet.Height;
        return true;
    }

    private bool ProcessNal(VideoPacket packet)
    {
        if (_transform is null)
        {
            return false;
        }

        var payload = packet.Payload.Span;
        if (MfPlat.MFCreateMemoryBuffer(payload.Length, out var buffer) < 0)
        {
            return false;
        }

        if (buffer.Lock(out var ptr, out _, out _) < 0)
        {
            return false;
        }

        Marshal.Copy(payload.ToArray(), 0, ptr, payload.Length);
        _ = buffer.Unlock();
        _ = buffer.SetCurrentLength(payload.Length);
        if (MfPlat.MFCreateSample(out var sample) < 0)
        {
            return false;
        }

        if (sample.AddBuffer(buffer) < 0)
        {
            return false;
        }

        _ = sample.SetSampleTime(packet.CaptureTimestampMs * 10_000);
        _ = sample.SetSampleDuration(10_000 * 40);
        var hr = _transform.ProcessInput(0, sample, 0);
        return hr >= 0;
    }

    private bool TryReadFrame(VideoPacket packet, long receivedTimestampMs, out DecodedFrame? frame)
    {
        frame = null;
        if (_transform is null)
        {
            return false;
        }

        var buffers = new MftOutputDataBuffer[1];
        if (!_providesSamples)
        {
            if (MfPlat.MFCreateSample(out var sample) < 0)
            {
                return false;
            }

            var size = packet.Width * packet.Height * 2;
            if (MfPlat.MFCreateMemoryBuffer(size, out var mediaBuffer) < 0)
            {
                return false;
            }

            _ = sample.AddBuffer(mediaBuffer);
            buffers[0].Sample = Marshal.GetIUnknownForObject(sample);
        }

        var hr = _transform.ProcessOutput(0, 1, buffers, out _);
        if (hr == MfPlat.NeedMoreInput)
        {
            ReleaseBuffer(buffers[0]);
            return false;
        }

        if (hr == MfPlat.StreamChange)
        {
            ReleaseBuffer(buffers[0]);
            _ = CreateTransform(packet);
            return false;
        }

        if (hr < 0 || buffers[0].Sample == IntPtr.Zero)
        {
            ReleaseBuffer(buffers[0]);
            return false;
        }

        try
        {
            var sample = (IMFSample)Marshal.GetObjectForIUnknown(buffers[0].Sample);
            if (sample.ConvertToContiguousBuffer(out var contiguous) < 0)
            {
                return false;
            }

            if (contiguous.Lock(out var ptr, out _, out var length) < 0)
            {
                return false;
            }

            var nv12 = new byte[length];
            Marshal.Copy(ptr, nv12, 0, length);
            _ = contiguous.Unlock();
            var strideKey = MfGuids.DefaultStride;
            var stride = packet.Width;
            if (_transform.GetOutputCurrentType(0, out var type) >= 0 &&
                type.GetUINT32(ref strideKey, out var rawStride) >= 0 &&
                rawStride != 0)
            {
                stride = Math.Abs(rawStride);
            }

            var bgra = YuvConvert.Nv12ToBgra(nv12, packet.Width, packet.Height, stride);
            frame = new DecodedFrame(packet.Width, packet.Height, bgra, packet.CaptureTimestampMs, receivedTimestampMs);
            return true;
        }
        finally
        {
            ReleaseBuffer(buffers[0]);
        }
    }

    private static void ReleaseBuffer(MftOutputDataBuffer buffer)
    {
        if (buffer.Sample != IntPtr.Zero)
        {
            Marshal.Release(buffer.Sample);
        }

        if (buffer.Events != IntPtr.Zero)
        {
            Marshal.Release(buffer.Events);
        }
    }

    private static ulong PackSize(int width, int height) => ((ulong)(uint)width << 32) | (uint)height;

    private bool Startup()
    {
        if (_started)
        {
            return true;
        }

        var hr = MfPlat.MFStartup(MfPlat.Version, 0);
        if (hr < 0)
        {
            return false;
        }

        _started = true;
        return true;
    }

    private void ReleaseTransform()
    {
        if (_transform is not null)
        {
            Marshal.ReleaseComObject(_transform);
            _transform = null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            ReleaseTransform();
            if (_started)
            {
                MfPlat.MFShutdown();
                _started = false;
            }
        }
    }
}
