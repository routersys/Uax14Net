using System;
using Uax14Net;

namespace Uax14Net.Tests;

public class AllocationTests
{
    private const string Sample =
        "The quick (brown) fox—jumps! 12,345.67 一丁あぁ "
        + "\U0001F1EF\U0001F1F5 co‑operate กข «quote» a-b 100km "
        + "\U0001F469‍\U0001F680 person(s) ক্ক";

    [Fact]
    public void EnumerationDoesNotAllocate()
    {
        _ = Consume(Sample);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++)
        {
            _ = Consume(Sample);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void ClassLookupDoesNotAllocate()
    {
        _ = LineBreaker.GetLineBreakClass('A');

        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int cp = 0; cp <= 0x2FFFF; cp++)
        {
            sum += (int)LineBreaker.GetLineBreakClass(cp);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.True(sum > 0);
        Assert.Equal(0, after - before);
    }

    [Fact]
    public void Utf8EnumerationDoesNotAllocate()
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(Sample);
        _ = Consume(utf8);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++)
        {
            _ = Consume(utf8);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    private static int Consume(string text)
    {
        int sum = 0;
        foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text))
        {
            sum += op.Position + (int)op.Kind;
        }
        return sum;
    }

    private static int Consume(byte[] utf8)
    {
        int sum = 0;
        foreach (LineBreakOpportunity op in LineBreaker.Enumerate(utf8.AsSpan()))
        {
            sum += op.Position + (int)op.Kind;
        }
        return sum;
    }
}
