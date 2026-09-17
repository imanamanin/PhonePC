using NAudio.Wave;
using PhoneControl.Application;

namespace PhoneControl.Desktop.Audio;

public sealed class NAudioPcmPlayer : IAudioPlayer
{
    private readonly object _gate = new();
    private WaveOutEvent? _output;
    private BufferedWaveProvider? _buffer;
    private int _rate;
    private int _channels;
    private int _bits;

    public void PlayPcm(int sampleRate, int channels, int bitsPerSample, ReadOnlyMemory<byte> pcm)
    {
        if (pcm.Length == 0 || sampleRate <= 0 || channels <= 0)
        {
            return;
        }

        var bits = bitsPerSample <= 0 ? 16 : bitsPerSample;
        lock (_gate)
        {
            Ensure(sampleRate, channels, bits);
            if (_buffer is null || _output is null)
            {
                return;
            }
            var data = pcm.ToArray();
            _buffer!.AddSamples(data, 0, data.Length);
            if (_output!.PlaybackState != PlaybackState.Playing)
            {
                try
                {
                    _output.Play();
                }
                catch (Exception)
                {
                    // No output device.
                }
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopUnsafe();
        }
    }

    public void Dispose() => Stop();

    private void Ensure(int sampleRate, int channels, int bits)
    {
        if (_output is not null && _rate == sampleRate && _channels == channels && _bits == bits)
        {
            return;
        }

        StopUnsafe();
        _rate = sampleRate;
        _channels = channels;
        _bits = bits;
        var format = new WaveFormat(sampleRate, bits, channels);
        _buffer = new BufferedWaveProvider(format)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromMilliseconds(750)
        };
        try
        {
            _output = new WaveOutEvent { DesiredLatency = 120 };
            _output.Init(_buffer);
        }
        catch (Exception)
        {
            _output?.Dispose();
            _output = null;
            _buffer = null;
        }
    }

    private void StopUnsafe()
    {
        try
        {
            _output?.Stop();
        }
        catch (Exception)
        {
            // Device already closed.
        }

        _output?.Dispose();
        _output = null;
        _buffer = null;
    }
}
