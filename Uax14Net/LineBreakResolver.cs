using System.Runtime.CompilerServices;

namespace Uax14Net;

internal static class LineBreakResolver
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LineBreakClass Resolve(LineBreakClass raw, byte flags, in LineBreakOptions options)
        => raw switch
        {
            LineBreakClass.AI => options.AmbiguousWidth == AmbiguousWidthMode.Ideographic ? LineBreakClass.ID : LineBreakClass.AL,
            LineBreakClass.SG or LineBreakClass.XX => LineBreakClass.AL,
            LineBreakClass.CJ => options.Strictness == LineBreakStrictness.Normal ? LineBreakClass.ID : LineBreakClass.NS,
            LineBreakClass.SA => ResolveComplexContext(flags),
            _ => raw
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static LineBreakClass ResolveComplexContext(byte flags)
        => (flags & LineBreakData.FlagSaCombiningBase) != 0 ? LineBreakClass.CM : LineBreakClass.AL;
}
