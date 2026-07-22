using System.Collections.Generic;
using Uax14Net;

namespace Uax14Net.Tests;

public class TailoringTests
{
    private static List<(int Position, LineBreakKind Kind)> Collect(string text, LineBreakOptions options)
    {
        var list = new List<(int, LineBreakKind)>();
        foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text, options))
        {
            list.Add((op.Position, op.Kind));
        }
        return list;
    }

    [Fact]
    public void StrictnessDefaultKeepsSmallKanaAttached()
    {
        List<(int Position, LineBreakKind Kind)> ops = Collect("あぁ", LineBreakOptions.Default);
        Assert.Single(ops);
        Assert.Equal((2, LineBreakKind.Mandatory), ops[0]);
    }

    [Fact]
    public void StrictnessNormalBreaksBeforeSmallKana()
    {
        var options = new LineBreakOptions { Strictness = LineBreakStrictness.Normal };
        List<(int Position, LineBreakKind Kind)> ops = Collect("あぁ", options);
        Assert.Equal(2, ops.Count);
        Assert.Equal((1, LineBreakKind.Allowed), ops[0]);
    }

    [Fact]
    public void AmbiguousWidthDefaultKeepsAiCharactersTogether()
    {
        List<(int Position, LineBreakKind Kind)> ops = Collect("§§", LineBreakOptions.Default);
        Assert.Single(ops);
        Assert.Equal((2, LineBreakKind.Mandatory), ops[0]);
    }

    [Fact]
    public void AmbiguousWidthIdeographicBreaksBetweenAiCharacters()
    {
        var options = new LineBreakOptions { AmbiguousWidth = AmbiguousWidthMode.Ideographic };
        List<(int Position, LineBreakKind Kind)> ops = Collect("§§", options);
        Assert.Equal(2, ops.Count);
        Assert.Equal((1, LineBreakKind.Allowed), ops[0]);
    }

    [Fact]
    public void BreakAllBreaksBetweenLetters()
    {
        var options = new LineBreakOptions { WordBreak = WordBreakMode.BreakAll };
        List<(int Position, LineBreakKind Kind)> ops = Collect("ab", options);
        Assert.Equal(2, ops.Count);
        Assert.Equal((1, LineBreakKind.Allowed), ops[0]);
        Assert.Equal((2, LineBreakKind.Mandatory), ops[1]);
    }

    [Fact]
    public void BreakAllDoesNotBreakBeforeCombiningMark()
    {
        var options = new LineBreakOptions { WordBreak = WordBreakMode.BreakAll };
        string text = "a" + (char)0x0301 + "b";
        List<(int Position, LineBreakKind Kind)> ops = Collect(text, options);
        Assert.Equal(2, ops.Count);
        Assert.Equal((2, LineBreakKind.Allowed), ops[0]);
        Assert.Equal((3, LineBreakKind.Mandatory), ops[1]);
    }

    [Fact]
    public void KeepAllKeepsIdeographsTogether()
    {
        var options = new LineBreakOptions { WordBreak = WordBreakMode.KeepAll };
        List<(int Position, LineBreakKind Kind)> ops = Collect("一二", options);
        Assert.Single(ops);
        Assert.Equal((2, LineBreakKind.Mandatory), ops[0]);
    }

    [Fact]
    public void KeepAllStillBreaksAtSpaces()
    {
        var options = new LineBreakOptions { WordBreak = WordBreakMode.KeepAll };
        List<(int Position, LineBreakKind Kind)> ops = Collect("一 二", options);
        Assert.Equal(2, ops.Count);
        Assert.Equal((2, LineBreakKind.Allowed), ops[0]);
        Assert.Equal((3, LineBreakKind.Mandatory), ops[1]);
    }

    [Fact]
    public void ClassOverrideCanForceMandatoryBreak()
    {
        var options = new LineBreakOptions
        {
            ClassOverride = cp => cp == 'Z' ? LineBreakClass.BK : null
        };
        List<(int Position, LineBreakKind Kind)> ops = Collect("aZb", options);
        Assert.Contains((2, LineBreakKind.Mandatory), ops);
    }
}
