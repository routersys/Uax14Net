namespace Uax14Net;

public readonly struct LineBreakOpportunity
{
    public LineBreakOpportunity(int position, LineBreakKind kind)
    {
        Position = position;
        Kind = kind;
    }

    public int Position { get; }

    public LineBreakKind Kind { get; }

    public bool IsMandatory => Kind == LineBreakKind.Mandatory;
}
