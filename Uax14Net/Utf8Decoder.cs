using System;
using System.Runtime.CompilerServices;

namespace Uax14Net;

internal readonly ref struct Utf8Decoder : IUtfDecoder
{
    private const int Replacement = 0xFFFD;

    private readonly ReadOnlySpan<byte> _bytes;

    public Utf8Decoder(ReadOnlySpan<byte> bytes) => _bytes = bytes;

    public int Length => _bytes.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Decode(int index, out int length)
    {
        byte b0 = _bytes[index];
        if (b0 < 0x80)
        {
            length = 1;
            return b0;
        }
        if (b0 < 0xC2)
        {
            length = 1;
            return Replacement;
        }
        if (b0 < 0xE0)
        {
            if (index + 1 < _bytes.Length && IsContinuation(_bytes[index + 1]))
            {
                length = 2;
                return ((b0 & 0x1F) << 6) | (_bytes[index + 1] & 0x3F);
            }
            length = 1;
            return Replacement;
        }
        if (b0 < 0xF0)
        {
            if (index + 2 < _bytes.Length && IsContinuation(_bytes[index + 1]) && IsContinuation(_bytes[index + 2]))
            {
                int cp = ((b0 & 0x0F) << 12) | ((_bytes[index + 1] & 0x3F) << 6) | (_bytes[index + 2] & 0x3F);
                if (cp >= 0x800 && (cp < 0xD800 || cp > 0xDFFF))
                {
                    length = 3;
                    return cp;
                }
            }
            length = 1;
            return Replacement;
        }
        if (b0 < 0xF5)
        {
            if (index + 3 < _bytes.Length && IsContinuation(_bytes[index + 1])
                && IsContinuation(_bytes[index + 2]) && IsContinuation(_bytes[index + 3]))
            {
                int cp = ((b0 & 0x07) << 18) | ((_bytes[index + 1] & 0x3F) << 12)
                    | ((_bytes[index + 2] & 0x3F) << 6) | (_bytes[index + 3] & 0x3F);
                if (cp >= 0x10000 && cp <= 0x10FFFF)
                {
                    length = 4;
                    return cp;
                }
            }
            length = 1;
            return Replacement;
        }
        length = 1;
        return Replacement;
    }

    public bool TryGetCharRun(int start, int length, out ReadOnlySpan<char> chars)
    {
        chars = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsContinuation(byte b) => (b & 0xC0) == 0x80;
}
