namespace PhoneControl.Screen;

/// <summary>
/// MediaCodec may emit Annex-B start codes or AVCC length-prefixed NALs.
/// Media Foundation's H.264 MFT wants Annex-B.
/// </summary>
public static class H264AnnexB
{
    public static byte[] Normalize(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        if (HasStartCode(payload))
        {
            return payload.ToArray();
        }

        if (LooksLikeAvcc(payload))
        {
            return AvccToAnnexB(payload);
        }

        var prefixed = new byte[payload.Length + 4];
        prefixed[3] = 1;
        payload.CopyTo(prefixed.AsSpan(4));
        return prefixed;
    }

    public static byte[] Concat(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var a = Normalize(first);
        var b = Normalize(second);
        var output = new byte[a.Length + b.Length];
        a.CopyTo(output, 0);
        b.CopyTo(output, a.Length);
        return output;
    }

    public static bool HasStartCode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length >= 4 && payload[0] == 0 && payload[1] == 0 && payload[2] == 0 && payload[3] == 1)
        {
            return true;
        }

        return payload.Length >= 3 && payload[0] == 0 && payload[1] == 0 && payload[2] == 1;
    }

    public static bool ContainsSps(ReadOnlySpan<byte> annexB)
    {
        return ContainsNalType(annexB, 7);
    }

    public static bool ContainsIdr(ReadOnlySpan<byte> annexB)
    {
        return ContainsNalType(annexB, 5);
    }

    private static bool ContainsNalType(ReadOnlySpan<byte> annexB, byte nalType)
    {
        var offset = 0;
        while (offset < annexB.Length)
        {
            var start = IndexOfStartCode(annexB, offset);
            if (start < 0)
            {
                return false;
            }

            var nal = start + StartCodeLength(annexB.Slice(start));
            if (nal >= annexB.Length)
            {
                return false;
            }

            if ((annexB[nal] & 0x1F) == nalType)
            {
                return true;
            }

            offset = nal + 1;
        }

        return false;
    }

    private static bool LooksLikeAvcc(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 5)
        {
            return false;
        }

        var length = (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3];
        return length > 0 && length <= payload.Length - 4;
    }

    private static byte[] AvccToAnnexB(ReadOnlySpan<byte> payload)
    {
        var output = new List<byte>(payload.Length + 8);
        var offset = 0;
        while (offset + 4 <= payload.Length)
        {
            var length = (payload[offset] << 24) | (payload[offset + 1] << 16) | (payload[offset + 2] << 8) | payload[offset + 3];
            offset += 4;
            if (length <= 0 || offset + length > payload.Length)
            {
                break;
            }

            output.Add(0);
            output.Add(0);
            output.Add(0);
            output.Add(1);
            for (var i = 0; i < length; i++)
            {
                output.Add(payload[offset + i]);
            }

            offset += length;
        }

        return output.Count == 0 ? NormalizeRaw(payload) : output.ToArray();
    }

    private static byte[] NormalizeRaw(ReadOnlySpan<byte> payload)
    {
        var prefixed = new byte[payload.Length + 4];
        prefixed[3] = 1;
        payload.CopyTo(prefixed.AsSpan(4));
        return prefixed;
    }

    private static int IndexOfStartCode(ReadOnlySpan<byte> buffer, int start)
    {
        for (var i = start; i + 2 < buffer.Length; i++)
        {
            if (buffer[i] != 0 || buffer[i + 1] != 0)
            {
                continue;
            }

            if (buffer[i + 2] == 1)
            {
                return i;
            }

            if (i + 3 < buffer.Length && buffer[i + 2] == 0 && buffer[i + 3] == 1)
            {
                return i;
            }
        }

        return -1;
    }

    private static int StartCodeLength(ReadOnlySpan<byte> at)
    {
        if (at.Length >= 4 && at[0] == 0 && at[1] == 0 && at[2] == 0 && at[3] == 1)
        {
            return 4;
        }

        return 3;
    }
}
