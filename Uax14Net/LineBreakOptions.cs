using System;

namespace Uax14Net;

public readonly struct LineBreakOptions
{
    public LineBreakStrictness Strictness { get; init; }

    public WordBreakMode WordBreak { get; init; }

    public AmbiguousWidthMode AmbiguousWidth { get; init; }

    public Func<int, LineBreakClass?>? ClassOverride { get; init; }

    public IComplexContextResolver? ComplexContextResolver { get; init; }

    public static LineBreakOptions Default => default;
}
