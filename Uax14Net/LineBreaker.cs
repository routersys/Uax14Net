using System;

namespace Uax14Net;

public static class LineBreaker
{
    public static LineBreakEnumerator Enumerate(ReadOnlySpan<char> text) => new(text);

    public static LineBreakClass GetLineBreakClass(int codePoint) => LineBreakData.GetClass(codePoint);
}

public ref struct LineBreakEnumerator
{
    private LineBreakScanner _scanner;
    private LineBreakOpportunity _current;

    internal LineBreakEnumerator(ReadOnlySpan<char> text)
    {
        _scanner = new LineBreakScanner(text);
        _current = default;
    }

    public readonly LineBreakOpportunity Current => _current;

    public readonly LineBreakEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        while (_scanner.TryNext(out int position, out BreakAction action))
        {
            if (action != BreakAction.Prohibited)
            {
                _current = new LineBreakOpportunity(
                    position,
                    action == BreakAction.Mandatory ? LineBreakKind.Mandatory : LineBreakKind.Allowed);
                return true;
            }
        }
        return false;
    }
}
