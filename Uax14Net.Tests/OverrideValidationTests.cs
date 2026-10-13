using System;
using System.Collections.Generic;
using Uax14Net;

namespace Uax14Net.Tests;

public class OverrideValidationTests
{
    private static LineBreakOptions Override(byte value)
        => new LineBreakOptions { ClassOverride = cp => cp == 'b' ? (LineBreakClass)value : null };

    [Theory]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(64)]
    [InlineData(105)]
    [InlineData(128)]
    [InlineData(255)]
    public void UndefinedClassThrowsInvalidOperationException(byte value)
    {
        LineBreakOptions options = Override(value);
        foreach (string text in new[] { "abc", "a b c", "b", "あbあ" })
        {
            InvalidOperationException utf16 = Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text, in options))
                {
                }
            });
            Assert.Contains("U+0062", utf16.Message);
            Assert.Contains(value.ToString(), utf16.Message);

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (LineBreakOpportunity op in LineBreaker.Enumerate(bytes.AsSpan(), in options))
                {
                }
            });
        }
    }

    [Fact]
    public void EveryDefinedClassIsAccepted()
    {
        for (byte value = 0; value <= (byte)LineBreakClass.ZWJ; value++)
        {
            LineBreakOptions options = Override(value);
            const string text = "a b cあbあ";
            var positions = new List<int>();
            foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text, in options))
            {
                positions.Add(op.Position);
            }
            Assert.NotEmpty(positions);
            Assert.Equal(text.Length, positions[^1]);
        }
    }
}
