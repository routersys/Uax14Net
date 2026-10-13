using System;
using System.Collections.Generic;
using System.Text;
using Uax14Net;

namespace Uax14Net.Tests;

public class Utf8DecoderTests
{
    private static readonly byte[] Alphabet =
    {
        0x00, 0x41, 0x7F, 0x80, 0x8F, 0x90, 0x9F, 0xA0, 0xBF, 0xC0, 0xC1, 0xC2, 0xDF,
        0xE0, 0xE1, 0xEC, 0xED, 0xEE, 0xEF, 0xF0, 0xF1, 0xF3, 0xF4, 0xF5, 0xFF
    };

    private static List<(int, int)> Oracle(ReadOnlySpan<byte> bytes)
    {
        var list = new List<(int, int)>();
        int index = 0;
        while (index < bytes.Length)
        {
            Rune.DecodeFromUtf8(bytes.Slice(index), out Rune rune, out int consumed);
            list.Add((rune.Value, consumed));
            index += consumed;
        }
        return list;
    }

    private static List<(int, int)> Actual(byte[] bytes)
    {
        var list = new List<(int, int)>();
        var decoder = new Utf8Decoder(bytes);
        int index = 0;
        while (index < bytes.Length)
        {
            int codePoint = decoder.Decode(index, out int length);
            list.Add((codePoint, length));
            index += length;
        }
        return list;
    }

    private static void AssertSame(byte[] bytes)
    {
        List<(int, int)> expected = Oracle(bytes);
        List<(int, int)> actual = Actual(bytes);
        Assert.True(expected.SequenceEqual(actual), Convert.ToHexString(bytes));
    }

    [Fact]
    public void EveryShortSequenceOfBoundaryBytesMatchesRuneDecoding()
    {
        var current = new List<byte[]> { Array.Empty<byte>() };
        for (int length = 1; length <= 4; length++)
        {
            var next = new List<byte[]>();
            foreach (byte[] prefix in current)
            {
                foreach (byte b in Alphabet)
                {
                    byte[] sequence = new byte[length];
                    prefix.CopyTo(sequence, 0);
                    sequence[length - 1] = b;
                    next.Add(sequence);
                    AssertSame(sequence);
                }
            }
            current = next;
        }
    }

    [Fact]
    public void RandomBytesMatchRuneDecoding()
    {
        CorpusRng rng = new CorpusRng(0xB17E5);
        for (int i = 0; i < 200000; i++)
        {
            byte[] bytes = new byte[rng.Next(49)];
            for (int k = 0; k < bytes.Length; k++)
            {
                bytes[k] = rng.Next(3) == 0 ? (byte)rng.Next(256) : Alphabet[rng.Next(Alphabet.Length)];
            }
            AssertSame(bytes);
        }
    }

    [Fact]
    public void EveryScalarValueRoundTrips()
    {
        byte[] buffer = new byte[4];
        for (int value = 0; value <= 0x10FFFF; value++)
        {
            if (value >= 0xD800 && value <= 0xDFFF)
            {
                continue;
            }
            int written = new Rune(value).EncodeToUtf8(buffer);
            var decoder = new Utf8Decoder(buffer.AsSpan(0, written));
            int codePoint = decoder.Decode(0, out int length);
            Assert.True(codePoint == value && length == written, "U+" + value.ToString("X4"));
        }
    }

    [Fact]
    public void TruncatedSequenceIsOneReplacementCharacterInTheBreakOffsets()
    {
        var options = new LineBreakOptions { AmbiguousWidth = AmbiguousWidthMode.Ideographic };
        var positions = new List<int>();
        foreach (LineBreakOpportunity op in LineBreaker.Enumerate(new byte[] { 0xE3, 0x81, 0x41 }.AsSpan(), in options))
        {
            positions.Add(op.Position);
        }
        Assert.Equal(new[] { 2, 3 }, positions);
    }
}
