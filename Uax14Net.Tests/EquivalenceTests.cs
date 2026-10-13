using System;
using System.Collections.Generic;
using System.Text;
using Uax14Net;

namespace Uax14Net.Tests;

public class EquivalenceTests
{
    private const int CaseCount = 150000;
    private const int MaxLength = 24;
    private const ulong Seed = 0x5EED14UL;

    private static readonly int[][] Pool = RandomCorpus.BuildPool(4);

    private static readonly ulong[] Expected =
    {
        5713657711091297824UL,
        4224038782985681684UL,
        12601296830528486608UL,
        12601296830528486608UL,
        422362283889476672UL,
        12803946840607693626UL,
        7478282132456751072UL,
        4915080586267127436UL,
        12601296830528486608UL,
        12601296830528486608UL,
        6549243534294971014UL,
        3399396709213728492UL,
        1970949159857028784UL,
        16278030567349502879UL
    };

    private static ulong Mix(ulong hash, ulong value)
    {
        hash ^= value;
        return hash * 0x100000001B3UL;
    }

    private static ulong HashOptions(in LineBreakOptions options)
    {
        CorpusRng rng = new CorpusRng(Seed);
        Span<char> buffer = stackalloc char[MaxLength * 2];
        ulong hash = 0xCBF29CE484222325UL;
        for (int i = 0; i < CaseCount; i++)
        {
            int length = RandomCorpus.Generate(ref rng, Pool, MaxLength, buffer, true);
            foreach (LineBreakOpportunity op in LineBreaker.Enumerate(buffer.Slice(0, length), in options))
            {
                hash = Mix(hash, ((ulong)op.Position << 1) | (op.IsMandatory ? 1UL : 0UL));
            }
            hash = Mix(hash, 0xFFFFFFFFUL);
        }
        return hash;
    }

    [Fact]
    public void Utf16OutputMatchesRecordedHashes()
    {
        LineBreakOptions[] options = RandomCorpus.BuildOptions();
        ulong[] actual = new ulong[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            actual[i] = HashOptions(in options[i]);
        }
        Assert.Equal(Expected, actual);
    }

    [Fact]
    public void Utf8OffsetsMatchUtf16OverRandomText()
    {
        LineBreakOptions[] options = RandomCorpus.BuildOptions();
        int[] optionIndexes = { 0, 5, 6, 11 };
        foreach (int optionIndex in optionIndexes)
        {
            LineBreakOptions option = options[optionIndex];
            CorpusRng rng = new CorpusRng(Seed);
            char[] chars = new char[MaxLength * 2];
            byte[] bytes = new byte[MaxLength * 8];
            int[] byteOffsets = new int[MaxLength * 2 + 1];
            for (int i = 0; i < CaseCount / 5; i++)
            {
                int length = RandomCorpus.Generate(ref rng, Pool, MaxLength, chars, false);
                int byteLength = Encoding.UTF8.GetBytes(chars, 0, length, bytes, 0);

                int bytePosition = 0;
                for (int c = 0; c < length; c++)
                {
                    byteOffsets[c] = bytePosition;
                    if (char.IsHighSurrogate(chars[c]))
                    {
                        byteOffsets[c + 1] = bytePosition;
                        c++;
                        bytePosition += 4;
                    }
                    else
                    {
                        bytePosition += Encoding.UTF8.GetByteCount(chars, c, 1);
                    }
                }
                byteOffsets[length] = bytePosition;
                Assert.Equal(byteLength, bytePosition);

                var expected = new List<(int, LineBreakKind)>();
                foreach (LineBreakOpportunity op in LineBreaker.Enumerate(chars.AsSpan(0, length), in option))
                {
                    expected.Add((byteOffsets[op.Position], op.Kind));
                }
                var actual = new List<(int, LineBreakKind)>();
                foreach (LineBreakOpportunity op in LineBreaker.Enumerate(bytes.AsSpan(0, byteLength), in option))
                {
                    actual.Add((op.Position, op.Kind));
                }
                Assert.Equal(expected, actual);
            }
        }
    }
}
