namespace PhoneControl.Screen;

public static class YuvConvert
{
    public static byte[] Nv12ToBgra(ReadOnlySpan<byte> nv12, int width, int height, int stride = 0)
    {
        if (stride <= 0)
        {
            stride = width;
        }

        var bgra = new byte[width * height * 4];
        var ySize = stride * height;
        if (nv12.Length < ySize + stride * height / 2)
        {
            throw new ArgumentException("NV12 buffer is too small.", nameof(nv12));
        }

        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var y = nv12[row * stride + col];
                var uvIndex = ySize + (row / 2) * stride + (col & ~1);
                var u = nv12[uvIndex];
                var v = nv12[uvIndex + 1];
                var c = y - 16;
                var d = u - 128;
                var e = v - 128;
                var r = Clamp((298 * c + 409 * e + 128) >> 8);
                var g = Clamp((298 * c - 100 * d - 208 * e + 128) >> 8);
                var b = Clamp((298 * c + 516 * d + 128) >> 8);
                var i = (row * width + col) * 4;
                bgra[i] = (byte)b;
                bgra[i + 1] = (byte)g;
                bgra[i + 2] = (byte)r;
                bgra[i + 3] = 255;
            }
        }

        return bgra;
    }

    private static int Clamp(int value) => Math.Clamp(value, 0, 255);
}
