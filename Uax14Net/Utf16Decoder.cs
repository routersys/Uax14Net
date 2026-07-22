using System;
using System.Runtime.CompilerServices;

namespace Uax14Net;

internal readonly ref struct Utf16Decoder : IUtfDecoder
{
    private readonly ReadOnlySpan<char> _text;

    public Utf16Decoder(ReadOnlySpan<char> text) => _text = text;

    public int Length => _text.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Decode(int index, out int length)
    {
        char c = _text[index];
        if (char.IsHighSurrogate(c) && index + 1 < _text.Length && char.IsLowSurrogate(_text[index + 1]))
        {
            length = 2;
            return char.ConvertToUtf32(c, _text[index + 1]);
        }
        length = 1;
        return c;
    }

    public bool TryGetCharRun(int start, int length, out ReadOnlySpan<char> chars)
    {
        chars = _text.Slice(start, length);
        return true;
    }
}
