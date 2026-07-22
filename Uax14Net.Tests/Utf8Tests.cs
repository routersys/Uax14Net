using System;
using System.Collections.Generic;
using System.Text;
using Uax14Net;

namespace Uax14Net.Tests;

public class Utf8Tests
{
    private static List<(int Position, LineBreakKind Kind)> Collect(byte[] utf8)
    {
        var list = new List<(int, LineBreakKind)>();
        foreach (LineBreakOpportunity op in LineBreaker.Enumerate(utf8.AsSpan()))
        {
            list.Add((op.Position, op.Kind));
        }
        return list;
    }

    [Fact]
    public void AsciiOffsetsMatchCharacterOffsets()
    {
        List<(int Position, LineBreakKind Kind)> ops = Collect(Encoding.UTF8.GetBytes("a b c"));
        Assert.Equal(3, ops.Count);
        Assert.Equal((2, LineBreakKind.Allowed), ops[0]);
        Assert.Equal((4, LineBreakKind.Allowed), ops[1]);
        Assert.Equal((5, LineBreakKind.Mandatory), ops[2]);
    }

    [Fact]
    public void MultibyteOffsetsAreByteOffsets()
    {
        List<(int Position, LineBreakKind Kind)> ops = Collect(Encoding.UTF8.GetBytes("a 一 b"));
        Assert.Equal(3, ops.Count);
        Assert.Equal((2, LineBreakKind.Allowed), ops[0]);
        Assert.Equal((6, LineBreakKind.Allowed), ops[1]);
        Assert.Equal((7, LineBreakKind.Mandatory), ops[2]);
    }

    [Fact]
    public void InvalidBytesBecomeReplacementAndDoNotThrow()
    {
        byte[] bytes = { 0x61, 0xFF, 0x62 };
        List<(int Position, LineBreakKind Kind)> ops = Collect(bytes);
        Assert.Single(ops);
        Assert.Equal((3, LineBreakKind.Mandatory), ops[0]);
    }

    [Fact]
    public void Utf8MatchesUtf16OverConformanceCorpus()
    {
        int checkedCases = 0;
        foreach (LineBreakTestCase c in LineBreakTestData.Parse())
        {
            checkedCases++;

            var utf16Breaks = new List<int>();
            foreach (LineBreakOpportunity op in LineBreaker.Enumerate(c.Text.AsSpan()))
            {
                utf16Breaks.Add(op.Position);
            }

            byte[] utf8 = Encoding.UTF8.GetBytes(c.Text);
            var utf8Breaks = new HashSet<int>();
            foreach (LineBreakOpportunity op in LineBreaker.Enumerate(utf8.AsSpan()))
            {
                utf8Breaks.Add(op.Position);
            }

            Assert.Equal(utf16Breaks.Count, utf8Breaks.Count);
            foreach (int charOffset in utf16Breaks)
            {
                int byteOffset = Encoding.UTF8.GetByteCount(c.Text.AsSpan(0, charOffset));
                Assert.Contains(byteOffset, utf8Breaks);
            }
        }

        Assert.True(checkedCases > 8000);
    }
}
