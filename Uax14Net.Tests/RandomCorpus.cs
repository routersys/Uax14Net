using System;
using System.Collections.Generic;
using Uax14Net;

namespace Uax14Net.Tests;

internal struct CorpusRng
{
    private ulong _state;

    public CorpusRng(ulong seed) => _state = seed;

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

internal sealed class OddBreaks : IComplexContextResolver
{
    public void Resolve(ReadOnlySpan<char> run, Span<bool> breakBefore)
    {
        for (int i = 1; i < breakBefore.Length; i += 2)
        {
            breakBefore[i] = true;
        }
    }
}

internal static class RandomCorpus
{
    public static LineBreakClass? OverrideClass(int codePoint)
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

    public static LineBreakOptions[] BuildWideOverrideOptions()
    {
        LineBreakStrictness[] strictness = Enum.GetValues<LineBreakStrictness>();
        WordBreakMode[] wordBreak = Enum.GetValues<WordBreakMode>();
        AmbiguousWidthMode[] ambiguous = Enum.GetValues<AmbiguousWidthMode>();
        int classCount = Enum.GetValues<LineBreakClass>().Length;
        var options = new List<LineBreakOptions>();
        for (int seed = 1; seed <= 24; seed++)
        {
            uint salt = (uint)seed * 2654435761u;
            uint divisor = (uint)(2 + seed % 5);
            Func<int, LineBreakClass?> map = codePoint =>
            {
                uint h = ((uint)codePoint ^ salt) * 2246822519u;
                h ^= h >> 15;
                h *= 3266489917u;
                h ^= h >> 13;
                return h % divisor == 0 ? (LineBreakClass)(int)((h >> 8) % (uint)classCount) : null;
            };
            options.Add(new LineBreakOptions
            {
                ClassOverride = map,
                Strictness = strictness[seed % strictness.Length],
                WordBreak = wordBreak[seed % wordBreak.Length],
                AmbiguousWidth = ambiguous[seed % ambiguous.Length],
                ComplexContextResolver = seed % 3 == 0 ? new OddBreaks() : null
            });
        }
        return options.ToArray();
    }

    public static int[][] BuildPool(int picksPerGroup)
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
            for (int i = 0; i < picksPerGroup; i++)
            {
                int candidate = members[(int)((long)i * (members.Count - 1) / (picksPerGroup - 1))];
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

    public static LineBreakOptions[] BuildOptions()
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

    public static int Append(Span<char> buffer, int written, int codePoint, bool allowLoneSurrogates)
    {
        if (!allowLoneSurrogates && codePoint >= 0xD800 && codePoint <= 0xDFFF)
        {
            codePoint = 'x';
        }
        if (codePoint >= 0x10000)
        {
            int offset = codePoint - 0x10000;
            buffer[written++] = (char)(0xD800 + (offset >> 10));
            buffer[written++] = (char)(0xDC00 + (offset & 0x3FF));
        }
        else
        {
            buffer[written++] = (char)codePoint;
        }
        return written;
    }

    public static int Generate(ref CorpusRng rng, int[][] pool, int maxLength, Span<char> buffer, bool allowLoneSurrogates)
    {
        int length = 1 + rng.Next(maxLength);
        int written = 0;
        for (int i = 0; i < length; i++)
        {
            int[] group = pool[rng.Next(pool.Length)];
            int cp = group[rng.Next(group.Length)];
            written = Append(buffer, written, cp, allowLoneSurrogates);
        }
        return written;
    }
}
