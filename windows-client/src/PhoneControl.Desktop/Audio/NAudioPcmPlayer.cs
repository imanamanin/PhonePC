using NAudio.Wave;
using PhoneControl.Application;

namespace PhoneControl.Desktop.Audio;

public sealed class NAudioPcmPlayer : IAudioPlayer
{
    private readonly object _gate = new();
    private readonly System.Windows.Threading.Dispatcher? _dispatcher =
        System.Windows.Application.Current?.Dispatcher;
    private WaveOutEvent? _output;
    private BufferedWaveProvider? _buffer;
    private int _rate;
    private int _channels;
    private int _bits;

    public void PlayPcm(int sampleRate, int channels, int bitsPerSample, ReadOnlyMemory<byte> pcm)
    {
        if (pcm.Length < 4 || sampleRate < 8000 || channels <= 0)
        {
            return;
        }

        var copy = pcm.ToArray();
        var bits = bitsPerSample <= 0 ? 16 : bitsPerSample;
        if (_dispatcher is not null && !_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => PlayOnUi(sampleRate, channels, bits, copy));
            return;
        }

        PlayOnUi(sampleRate, channels, bits, copy);
    }

    public void Stop()
    {
        if (_dispatcher is not null && !_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(Stop);
            return;
        }

        lock (_gate)
        {
            StopUnsafe();
        }
    }

    public void Dispose() => Stop();

    private void PlayOnUi(int sampleRate, int channels, int bits, byte[] pcm)
    {
        lock (_gate)
        {
            Ensure(sampleRate, channels, bits);
            if (_buffer is null || _output is null)
            {
                return;
            }

            var data = Align(pcm, _buffer.WaveFormat.BlockAlign);
            if (data.Length == 0)
            {
                return;
            }

            try
            {
                _buffer.AddSamples(data, 0, data.Length);
                if (_output.PlaybackState != PlaybackState.Playing)
                {
                    _output.Play();
                }
            }
            catch (Exception)
            {
                StopUnsafe();
            }
        }
    }

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
            BufferDuration = TimeSpan.FromMilliseconds(1000)
        };
        try
        {
            _output = new WaveOutEvent { DesiredLatency = 100, NumberOfBuffers = 3 };
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

        try
        {
            _output?.Dispose();
        }
        catch (Exception)
        {
            // Already disposed.
        }

        _output = null;
        _buffer = null;
    }

    private static byte[] Align(byte[] pcm, int blockAlign)
    {
        if (blockAlign <= 0)
        {
            return pcm;
        }

        var n = pcm.Length - (pcm.Length % blockAlign);
        if (n <= 0)
        {
            return Array.Empty<byte>();
        }

        if (n == pcm.Length)
        {
            return pcm;
        }

        var cut = new byte[n];
        Buffer.BlockCopy(pcm, 0, cut, 0, n);
        return cut;
    }
}
