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

    private static readonly int[][] Pool = BuildPool();

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

    private struct Rng
    {
        private ulong _state;

        public Rng(ulong seed) => _state = seed;

        public int Next(int bound)
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)((z >> 33) % (ulong)bound);
        }
    }

    private sealed class OddBreaks : IComplexContextResolver
    {
        public void Resolve(ReadOnlySpan<char> run, Span<bool> breakBefore)
        {
            for (int i = 1; i < breakBefore.Length; i += 2)
            {
                breakBefore[i] = true;
            }
        }
    }

    private static LineBreakClass? OverrideClass(int codePoint)
    {
        if ((codePoint & 7) == 5)
        {
            return LineBreakClass.CM;
        }
        if ((codePoint & 15) == 9)
        {
            return LineBreakClass.ID;
        }
        return null;
    }

    private static int[][] BuildPool()
    {
        var groups = new SortedDictionary<int, List<int>>();
        for (int cp = 0; cp <= 0x10FFFF; cp++)
        {
            int key = LineBreakData.Lookup(cp);
            if (!groups.TryGetValue(key, out List<int>? members))
            {
                members = new List<int>();
                groups[key] = members;
            }
            members.Add(cp);
        }

        var pool = new List<int[]>();
        foreach (List<int> members in groups.Values)
        {
            var picked = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int candidate = members[(int)((long)i * (members.Count - 1) / 3)];
                if (!picked.Contains(candidate))
                {
                    picked.Add(candidate);
                }
            }
            pool.Add(picked.ToArray());
        }
        pool.Add(new[] { 0x25CC });
        return pool.ToArray();
    }

    private static LineBreakOptions[] BuildOptions()
    {
        var options = new List<LineBreakOptions>();
        foreach (LineBreakStrictness strictness in Enum.GetValues<LineBreakStrictness>())
        {
            foreach (WordBreakMode wordBreak in Enum.GetValues<WordBreakMode>())
            {
                foreach (AmbiguousWidthMode ambiguous in Enum.GetValues<AmbiguousWidthMode>())
                {
                    options.Add(new LineBreakOptions
                    {
                        Strictness = strictness,
                        WordBreak = wordBreak,
                        AmbiguousWidth = ambiguous
                    });
                }
            }
        }
        options.Add(new LineBreakOptions { ComplexContextResolver = new OddBreaks() });
        options.Add(new LineBreakOptions { ClassOverride = OverrideClass });
        return options.ToArray();
    }

    private static int Generate(ref Rng rng, Span<char> buffer, bool allowLoneSurrogates)
    {
        int length = 1 + rng.Next(MaxLength);
        int written = 0;
        for (int i = 0; i < length; i++)
        {
            int[] group = Pool[rng.Next(Pool.Length)];
            int cp = group[rng.Next(group.Length)];
            if (!allowLoneSurrogates && cp >= 0xD800 && cp <= 0xDFFF)
            {
                cp = 'x';
            }
            if (cp >= 0x10000)
            {
                int offset = cp - 0x10000;
                buffer[written++] = (char)(0xD800 + (offset >> 10));
                buffer[written++] = (char)(0xDC00 + (offset & 0x3FF));
            }
            else
            {
                buffer[written++] = (char)cp;
            }
        }
        return written;
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        hash ^= value;
        return hash * 0x100000001B3UL;
    }

    private static ulong HashOptions(in LineBreakOptions options)
    {
        var rng = new Rng(Seed);
        Span<char> buffer = stackalloc char[MaxLength * 2];
        ulong hash = 0xCBF29CE484222325UL;
        for (int i = 0; i < CaseCount; i++)
        {
            int length = Generate(ref rng, buffer, true);
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
        LineBreakOptions[] options = BuildOptions();
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
        LineBreakOptions[] options = BuildOptions();
        int[] optionIndexes = { 0, 5, 6, 11 };
        foreach (int optionIndex in optionIndexes)
        {
            LineBreakOptions option = options[optionIndex];
            var rng = new Rng(Seed);
            char[] chars = new char[MaxLength * 2];
            byte[] bytes = new byte[MaxLength * 8];
            int[] byteOffsets = new int[MaxLength * 2 + 1];
            for (int i = 0; i < CaseCount / 5; i++)
            {
                int length = Generate(ref rng, chars, false);
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
