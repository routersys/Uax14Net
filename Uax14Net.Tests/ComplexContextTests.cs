using System;
using System.Collections.Generic;
using Uax14Net;

namespace Uax14Net.Tests;

public class ComplexContextTests
{
    private sealed class BreakEverywhere : IComplexContextResolver
    {
        public void Resolve(ReadOnlySpan<char> run, Span<bool> breakBefore)
        {
            for (int i = 1; i < breakBefore.Length; i++)
            {
                breakBefore[i] = true;
            }
        }
    }

    private sealed class BreakBeforeIndex : IComplexContextResolver
    {
        private readonly int _index;

        public BreakBeforeIndex(int index) => _index = index;

        public void Resolve(ReadOnlySpan<char> run, Span<bool> breakBefore)
        {
            if (_index < breakBefore.Length)
            {
                breakBefore[_index] = true;
            }
        }
    }

    private sealed class CaptureRun : IComplexContextResolver
    {
        public string? Captured { get; private set; }

        public int Length { get; private set; }

        public void Resolve(ReadOnlySpan<char> run, Span<bool> breakBefore)
        {
            Captured = run.ToString();
            Length = breakBefore.Length;
        }
    }

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
    public void DefaultKeepsComplexContextRunUnbroken()
    {
        List<(int Position, LineBreakKind Kind)> ops = Collect("กขค", LineBreakOptions.Default);
        Assert.Single(ops);
        Assert.Equal((3, LineBreakKind.Mandatory), ops[0]);
    }

    [Fact]
    public void ResolverBreaksAtEveryInteriorPosition()
    {
        var options = new LineBreakOptions { ComplexContextResolver = new BreakEverywhere() };
        List<(int Position, LineBreakKind Kind)> ops = Collect("กขค", options);
        Assert.Equal(3, ops.Count);
        Assert.Equal((1, LineBreakKind.Allowed), ops[0]);
        Assert.Equal((2, LineBreakKind.Allowed), ops[1]);
        Assert.Equal((3, LineBreakKind.Mandatory), ops[2]);
    }

    [Fact]
    public void ResolverBreaksAtSpecificPosition()
    {
        var options = new LineBreakOptions { ComplexContextResolver = new BreakBeforeIndex(2) };
        List<(int Position, LineBreakKind Kind)> ops = Collect("กขค", options);
        Assert.Equal(2, ops.Count);
        Assert.Equal((2, LineBreakKind.Allowed), ops[0]);
        Assert.Equal((3, LineBreakKind.Mandatory), ops[1]);
    }

    [Fact]
    public void ResolverReceivesTheRunSpan()
    {
        var capture = new CaptureRun();
        var options = new LineBreakOptions { ComplexContextResolver = capture };
        _ = Collect("aกขb", options);
        Assert.Equal("กข", capture.Captured);
        Assert.Equal(2, capture.Length);
    }

    [Fact]
    public void ResolverAppliesWithinSurroundingText()
    {
        var options = new LineBreakOptions { ComplexContextResolver = new BreakEverywhere() };
        List<(int Position, LineBreakKind Kind)> ops = Collect("aกขb", options);
        Assert.Equal(2, ops.Count);
        Assert.Equal((2, LineBreakKind.Allowed), ops[0]);
        Assert.Equal((4, LineBreakKind.Mandatory), ops[1]);
    }
}
