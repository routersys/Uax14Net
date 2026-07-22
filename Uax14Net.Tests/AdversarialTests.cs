using System.Collections.Generic;
using Uax14Net;

namespace Uax14Net.Tests;

public class AdversarialTests
{
    private static List<(int Position, LineBreakKind Kind)> Collect(string text)
    {
        var list = new List<(int, LineBreakKind)>();
        foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text))
        {
            list.Add((op.Position, op.Kind));
        }
        return list;
    }

    [Fact]
    public void EntireCodeSpaceResolvesWithoutThrowing()
    {
        for (int cp = 0; cp <= 0x10FFFF; cp++)
        {
            LineBreakClass cls = LineBreaker.GetLineBreakClass(cp);
            Assert.True((byte)cls <= (byte)LineBreakClass.ZWJ);
        }
    }

    [Fact]
    public void CodePointsBeyondRangeDefaultToUnknown()
    {
        Assert.Equal(LineBreakClass.XX, LineBreaker.GetLineBreakClass(0x110000));
        Assert.Equal(LineBreakClass.XX, LineBreaker.GetLineBreakClass(int.MaxValue));
    }

    [Fact]
    public void VerticalTabForcesMandatoryBreak()
    {
        Assert.Equal(LineBreakClass.BK, LineBreaker.GetLineBreakClass(0x000B));
        Assert.Equal(LineBreakClass.BK, LineBreaker.GetLineBreakClass(0x000C));

        string text = "a" + (char)0x000B + "b";
        List<(int Position, LineBreakKind Kind)> ops = Collect(text);
        Assert.Equal(2, ops.Count);
        Assert.Equal((2, LineBreakKind.Mandatory), ops[0]);
        Assert.Equal((3, LineBreakKind.Mandatory), ops[1]);
    }

    [Fact]
    public void NextLineForcesMandatoryBreak()
    {
        Assert.Equal(LineBreakClass.NL, LineBreaker.GetLineBreakClass(0x0085));

        string text = "a" + (char)0x0085 + "b";
        List<(int Position, LineBreakKind Kind)> ops = Collect(text);
        Assert.Equal(2, ops.Count);
        Assert.Equal((2, LineBreakKind.Mandatory), ops[0]);
        Assert.Equal((3, LineBreakKind.Mandatory), ops[1]);
    }

    [Fact]
    public void RegionalIndicatorRunBreaksInPairs()
    {
        List<(int Position, LineBreakKind Kind)> ops =
            Collect("\U0001F1EF\U0001F1F5\U0001F1FA\U0001F1F8");
        Assert.Equal(2, ops.Count);
        Assert.Equal((4, LineBreakKind.Allowed), ops[0]);
        Assert.Equal((8, LineBreakKind.Mandatory), ops[1]);
    }

    [Fact]
    public void EnumerationIsDeterministic()
    {
        string text = "The quick (brown) fox" + (char)0x2014 + "jumps! 12,345.67 一二三 «q» a-b";
        Assert.Equal(Collect(text), Collect(text));
    }

    [Fact]
    public void LoneSurrogatesAreTreatedAsSingleUnits()
    {
        List<(int Position, LineBreakKind Kind)> ops = Collect("a\uD800b");
        Assert.Single(ops);
        Assert.Equal((3, LineBreakKind.Mandatory), ops[0]);
    }
}
