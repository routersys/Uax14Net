using System;
using System.Collections.Generic;
using System.Linq;
using Uax14Net;

namespace Uax14Net.Tests;

public class ConformanceTests
{
    [Fact]
    public void MatchesOfficialLineBreakTest()
    {
        int total = 0;
        var failures = new List<string>();

        foreach (LineBreakTestCase c in LineBreakTestData.Parse())
        {
            total++;
            bool[] actual = ComputeBreaks(c.Text, c.BoundaryOffsets);
            for (int i = 0; i < c.Expected.Length; i++)
            {
                if (actual[i] != c.Expected[i])
                {
                    failures.Add($"L{c.LineNumber} boundary {i}: expected {(c.Expected[i] ? '÷' : '×')} got {(actual[i] ? '÷' : '×')} | {c.Raw}");
                    break;
                }
            }
        }

        Assert.True(total > 8000, $"expected the full test file, only parsed {total} cases");
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} of {total} conformance cases failed:\n{string.Join("\n", failures.Take(40))}");
    }

    private static bool[] ComputeBreaks(string text, int[] boundaryOffsets)
    {
        var offsetToIndex = new Dictionary<int, int>(boundaryOffsets.Length);
        for (int i = 0; i < boundaryOffsets.Length; i++)
        {
            offsetToIndex[boundaryOffsets[i]] = i;
        }

        var actual = new bool[boundaryOffsets.Length];
        foreach (LineBreakOpportunity opportunity in LineBreaker.Enumerate(text))
        {
            if (offsetToIndex.TryGetValue(opportunity.Position, out int index))
            {
                actual[index] = true;
            }
        }
        return actual;
    }
}
