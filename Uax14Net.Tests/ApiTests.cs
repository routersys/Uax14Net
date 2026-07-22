using System.Collections.Generic;
using Uax14Net;

namespace Uax14Net.Tests;

public class ApiTests
{
    private static List<LineBreakOpportunity> Collect(string text)
    {
        var list = new List<LineBreakOpportunity>();
        foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text))
        {
            list.Add(op);
        }
        return list;
    }

    [Fact]
    public void EmptyStringHasNoOpportunities()
    {
        Assert.Empty(Collect(string.Empty));
    }

    [Fact]
    public void SingleCharacterBreaksOnlyAtEnd()
    {
        List<LineBreakOpportunity> ops = Collect("a");
        Assert.Single(ops);
        Assert.Equal(1, ops[0].Position);
        Assert.Equal(LineBreakKind.Mandatory, ops[0].Kind);
    }

    [Fact]
    public void AlphabeticRunDoesNotBreakInside()
    {
        List<LineBreakOpportunity> ops = Collect("ab");
        Assert.Single(ops);
        Assert.Equal(2, ops[0].Position);
    }

    [Fact]
    public void SpaceProducesAllowedBreak()
    {
        List<LineBreakOpportunity> ops = Collect("a b");
        Assert.Equal(2, ops.Count);
        Assert.Equal(2, ops[0].Position);
        Assert.Equal(LineBreakKind.Allowed, ops[0].Kind);
        Assert.Equal(3, ops[1].Position);
        Assert.Equal(LineBreakKind.Mandatory, ops[1].Kind);
    }

    [Fact]
    public void NewlineIsMandatoryBreak()
    {
        List<LineBreakOpportunity> ops = Collect("a\nb");
        Assert.Equal(2, ops.Count);
        Assert.Equal(2, ops[0].Position);
        Assert.Equal(LineBreakKind.Mandatory, ops[0].Kind);
    }

    [Fact]
    public void CarriageReturnLineFeedIsOneMandatoryBreak()
    {
        List<LineBreakOpportunity> ops = Collect("a\r\nb");
        Assert.Equal(2, ops.Count);
        Assert.Equal(3, ops[0].Position);
        Assert.Equal(LineBreakKind.Mandatory, ops[0].Kind);
        Assert.Equal(4, ops[1].Position);
    }

    [Fact]
    public void SupplementaryPlaneOffsetsAreUtf16()
    {
        List<LineBreakOpportunity> ops = Collect("A\U0001F469B");
        Assert.Equal(3, ops.Count);
        Assert.Equal(1, ops[0].Position);
        Assert.Equal(3, ops[1].Position);
        Assert.Equal(4, ops[2].Position);
    }

    [Fact]
    public void EmojiZwjSequenceStaysTogether()
    {
        List<LineBreakOpportunity> ops = Collect("\U0001F469‍\U0001F680");
        Assert.Single(ops);
        Assert.Equal(5, ops[0].Position);
        Assert.Equal(LineBreakKind.Mandatory, ops[0].Kind);
    }
}
