using System;

namespace Uax14Net;

public static class LineBreaker
{
    public static LineBreakEnumerator Enumerate(ReadOnlySpan<char> text)
        => new(text, LineBreakOptions.Default);

    public static LineBreakEnumerator Enumerate(ReadOnlySpan<char> text, in LineBreakOptions options)
        => new(text, options);

    public static Utf8LineBreakEnumerator Enumerate(ReadOnlySpan<byte> utf8Text)
        => new(utf8Text, LineBreakOptions.Default);

    public static Utf8LineBreakEnumerator Enumerate(ReadOnlySpan<byte> utf8Text, in LineBreakOptions options)
        => new(utf8Text, options);

    public static LineBreakClass GetLineBreakClass(int codePoint) => LineBreakData.GetClass(codePoint);
}

internal static class LineBreakCore
{
    public static bool TryAdvance<TDecoder>(ref LineBreakScanner<TDecoder> scanner, out LineBreakOpportunity current)
        where TDecoder : struct, IUtfDecoder, allows ref struct
    {
        while (scanner.TryNext(out int position, out BreakAction action))
        {
            if (action != BreakAction.Prohibited)
            {
                current = new LineBreakOpportunity(
                    position,
                    action == BreakAction.Mandatory ? LineBreakKind.Mandatory : LineBreakKind.Allowed);
                return true;
            }
        }
        current = default;
        return false;
    }
}

public ref struct LineBreakEnumerator
{
    private LineBreakScanner<Utf16Decoder> _scanner;
    private LineBreakOpportunity _current;

    internal LineBreakEnumerator(ReadOnlySpan<char> text, LineBreakOptions options)
    {
        _scanner = new LineBreakScanner<Utf16Decoder>(new Utf16Decoder(text), options);
        _current = default;
    }

    public readonly LineBreakOpportunity Current => _current;

    public readonly LineBreakEnumerator GetEnumerator() => this;

    public void Dispose() => _scanner.Dispose();

    public bool MoveNext() => LineBreakCore.TryAdvance(ref _scanner, out _current);
}

public ref struct Utf8LineBreakEnumerator
{
    private LineBreakScanner<Utf8Decoder> _scanner;
    private LineBreakOpportunity _current;

    internal Utf8LineBreakEnumerator(ReadOnlySpan<byte> utf8Text, LineBreakOptions options)
    {
        _scanner = new LineBreakScanner<Utf8Decoder>(new Utf8Decoder(utf8Text), options);
        _current = default;
    }

    public readonly LineBreakOpportunity Current => _current;

    public readonly Utf8LineBreakEnumerator GetEnumerator() => this;

    public void Dispose() => _scanner.Dispose();

    public bool MoveNext() => LineBreakCore.TryAdvance(ref _scanner, out _current);
}
