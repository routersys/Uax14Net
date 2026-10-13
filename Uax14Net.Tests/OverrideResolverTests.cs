using System;
using System.Collections.Generic;
using Uax14Net;

namespace Uax14Net.Tests;

public class OverrideResolverTests
{
    private sealed class Recorder : IComplexContextResolver
    {
        private readonly bool _breakEverywhere;

        public Recorder(bool breakEverywhere) => _breakEverywhere = breakEverywhere;

        public List<string> Runs { get; } = new List<string>();

        public void Resolve(ReadOnlySpan<char> run, Span<bool> breakBefore)
        {
            Runs.Add(run.ToString());
            if (_breakEverywhere)
            {
                breakBefore.Fill(true);
            }
        }
    }

    private static List<int> Positions(string text, LineBreakOptions options)
    {
        var positions = new List<int>();
        foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text, in options))
        {
            positions.Add(op.Position);
        }
        return positions;
    }

    [Fact]
    public void LettersMovedOutOfSaAreNotPassedToTheResolver()
    {
        const string thai = "กขฃ";
        Func<int, LineBreakClass?> toId = cp => cp >= 0x0E01 && cp <= 0x0E03 ? LineBreakClass.ID : null;
        var recorder = new Recorder(false);

        List<int> withResolver = Positions(thai, new LineBreakOptions { ClassOverride = toId, ComplexContextResolver = recorder });

        Assert.Empty(recorder.Runs);
        Assert.Equal(new[] { 1, 2, 3 }, withResolver);
        Assert.Equal(withResolver, Positions(thai, new LineBreakOptions { ClassOverride = toId }));
    }

    [Fact]
    public void LettersMovedIntoSaArePassedToTheResolver()
    {
        Func<int, LineBreakClass?> toSa = cp => cp == 'a' || cp == 'b' ? LineBreakClass.SA : null;
        var breaking = new Recorder(true);
        var quiet = new Recorder(false);

        List<int> breaks = Positions("abab", new LineBreakOptions { ClassOverride = toSa, ComplexContextResolver = breaking });
        List<int> noBreaks = Positions("abab", new LineBreakOptions { ClassOverride = toSa, ComplexContextResolver = quiet });

        Assert.Equal(new[] { "abab" }, breaking.Runs);
        Assert.Equal(new[] { "abab" }, quiet.Runs);
        Assert.Equal(new[] { 1, 2, 3, 4 }, breaks);
        Assert.Equal(new[] { 4 }, noBreaks);
    }

    [Fact]
    public void SaLettersThatAreNotOverriddenKeepTheirRuns()
    {
        Func<int, LineBreakClass?> toId = cp => cp == 'x' ? LineBreakClass.ID : null;
        var recorder = new Recorder(false);

        Positions("xกขxฃ", new LineBreakOptions { ClassOverride = toId, ComplexContextResolver = recorder });

        Assert.Equal(new[] { "กข", "ฃ" }, recorder.Runs);
    }
}
