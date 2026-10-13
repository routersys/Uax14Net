using System;
using System.Collections.Generic;
using System.Text;
using Uax14Net;

namespace Uax14Net.Tests;

internal sealed class PairTableContexts
{
    private const int Rows = 98;
    private const int Columns = 49;
    private const int DottedCircle = 0x25CC;
    private const int Space = 0x20;

    private readonly List<int>[] _byClass = new List<int>[Columns];
    private readonly int[][] _anyGroup;
    private readonly int[] _anyCodePoints;
    private readonly int[] _populatedClasses;
    private readonly Dictionary<int, LineBreakClass> _forced = new Dictionary<int, LineBreakClass>();
    private readonly HashSet<int> _picked = new HashSet<int>();
    private CorpusRng _rng;

    public PairTableContexts(ulong seed)
    {
        _rng = new CorpusRng(seed);
        for (int i = 0; i < Columns; i++)
        {
            _byClass[i] = new List<int>();
        }

        int[][] pool = RandomCorpus.BuildPool(6);
        _anyGroup = pool[..^1];
        var anyCodePoints = new List<int>();
        foreach (int[] group in _anyGroup)
        {
            foreach (int cp in group)
            {
                if (cp != DottedCircle && cp != Space && !IsSurrogate(cp))
                {
                    anyCodePoints.Add(cp);
                }
                ushort entry = LineBreakData.Lookup(cp);
                LineBreakClass cls = LineBreakResolver.Resolve((LineBreakClass)(byte)entry, (byte)(entry >> 8), LineBreakOptions.Default);
                if (cls == LineBreakClass.CM || cls == LineBreakClass.ZWJ)
                {
                    continue;
                }
                _byClass[(int)cls].Add(cp);
            }
        }
        _byClass[(int)LineBreakClass.AL].Add(DottedCircle);
        _anyCodePoints = anyCodePoints.ToArray();

        var populated = new List<int>();
        for (int i = 0; i < Columns; i++)
        {
            if (_byClass[i].Count > 0)
            {
                populated.Add(i);
            }
        }
        _populatedClasses = populated.ToArray();
    }

    public int ActionAt(string text, int index, bool useTable, in LineBreakOptions baseOptions)
    {
        Dictionary<int, LineBreakClass> forced = _forced;
        LineBreakOptions options = baseOptions with
        {
            ClassOverride = forced.Count == 0 ? null : codePoint => forced.TryGetValue(codePoint, out LineBreakClass cls) ? cls : null
        };
        var scanner = new LineBreakScanner<Utf16Decoder>(new Utf16Decoder(text), options, useTable);
        try
        {
            while (scanner.TryNext(out int position, out BreakAction action))
            {
                if (position == index)
                {
                    return (int)action;
                }
            }
        }
        finally
        {
            scanner.Dispose();
        }
        return -1;
    }

    public bool IsBuildable(int row, int next)
    {
        bool spaceRow = row >= Columns;
        int rowClass = spaceRow ? (int)LineBreakClass.SP : row;
        int beforeSp = spaceRow ? row - Columns : -1;
        if (rowClass == (int)LineBreakClass.SP && !spaceRow)
        {
            return false;
        }
        if (spaceRow && beforeSp == (int)LineBreakClass.SP)
        {
            return false;
        }
        if (_byClass[rowClass].Count == 0 || _byClass[next].Count == 0)
        {
            return false;
        }
        return !spaceRow || beforeSp == (int)LineBreakClass.XX || _byClass[beforeSp].Count > 0;
    }

    public string Build(int row, int next, out int index)
    {
        bool spaceRow = row >= Columns;
        int rowClass = spaceRow ? (int)LineBreakClass.SP : row;
        int beforeSp = spaceRow ? row - Columns : -1;
        bool atStart = spaceRow && beforeSp == (int)LineBreakClass.XX;

        _forced.Clear();
        _picked.Clear();
        var text = new StringBuilder();
        if (!atStart)
        {
            AppendRandom(text, 3);
        }
        if (spaceRow && !atStart)
        {
            AppendCodePoint(text, Pick(beforeSp, false));
        }
        if (spaceRow)
        {
            int spaces = 1 + _rng.Next(3);
            for (int i = 0; i < spaces; i++)
            {
                AppendCodePoint(text, Space);
            }
        }
        else
        {
            AppendCodePoint(text, Pick(rowClass, false));
        }
        index = text.Length;
        AppendCodePoint(text, Pick(next, true));
        AppendRandom(text, 2);
        return text.ToString();
    }

    public static char[] Agree(char[] first, char[] second)
    {
        char[] table = new char[first.Length];
        for (int i = 0; i < table.Length; i++)
        {
            table[i] = first[i] == second[i] ? first[i] : '3';
        }
        return table;
    }

    public static string ToSource(char[] normal, char[] breakAll, char[] keepAll)
    {
        var source = new StringBuilder();
        source.Append("using System;\n\nnamespace Uax14Net;\n\ninternal static class LineBreakPairTable\n{\n");
        source.Append("    public const int Columns = 49;\n    public const int SpaceRowBase = 49;\n");
        source.Append("    public const byte Digit = (byte)'0';\n\n");
        AppendTable(source, "Entries", normal);
        source.Append('\n');
        AppendTable(source, "BreakAllEntries", breakAll);
        source.Append('\n');
        AppendTable(source, "KeepAllEntries", keepAll);
        source.Append("}\n");
        return source.ToString();
    }

    private static void AppendTable(StringBuilder source, string name, char[] table)
    {
        source.Append("    public static ReadOnlySpan<byte> ").Append(name).Append(" =>\n");
        for (int row = 0; row < Rows; row++)
        {
            source.Append("        \"").Append(table, row * Columns, Columns).Append("\"u8");
            source.Append(row < Rows - 1 ? " +\n" : ";\n");
        }
    }

    public char[] DeriveTable(int samples, LineBreakOptions options)
    {
        char[] table = new char[Rows * Columns];
        Array.Fill(table, '3');
        for (int row = 0; row < Rows; row++)
        {
            if (row == (int)LineBreakClass.AK || row == (int)LineBreakClass.AS)
            {
                continue;
            }
            for (int next = 0; next < Columns; next++)
            {
                if (!IsBuildable(row, next))
                {
                    continue;
                }

                int seen = -2;
                bool varies = false;
                for (int sample = 0; sample < samples && !varies; sample++)
                {
                    string text = Build(row, next, out int index);
                    int result = ActionAt(text, index, false, in options);
                    if (result < 0)
                    {
                        continue;
                    }
                    if (seen == -2)
                    {
                        seen = result;
                    }
                    else if (seen != result)
                    {
                        varies = true;
                    }
                }

                if (!varies && seen >= 0)
                {
                    table[row * Columns + next] = (char)('0' + seen);
                }
            }
        }

        return table;
    }

    private static bool IsSurrogate(int codePoint) => codePoint >= 0xD800 && codePoint <= 0xDFFF;

    private static void AppendCodePoint(StringBuilder text, int codePoint)
    {
        if (codePoint >= 0x10000)
        {
            int offset = codePoint - 0x10000;
            text.Append((char)(0xD800 + (offset >> 10)));
            text.Append((char)(0xDC00 + (offset & 0x3FF)));
        }
        else
        {
            text.Append((char)codePoint);
        }
    }

    private int Pick(int cls, bool isNext)
    {
        if (isNext && _rng.Next(8) == 0 && _picked.Add(DottedCircle))
        {
            if (cls != (int)LineBreakClass.AL)
            {
                _forced[DottedCircle] = (LineBreakClass)cls;
            }
            return DottedCircle;
        }

        if (_rng.Next(2) == 0)
        {
            List<int> list = _byClass[cls];
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int natural = list[_rng.Next(list.Count)];
                if (!_forced.ContainsKey(natural) && (isNext || natural != DottedCircle) && !IsSurrogate(natural))
                {
                    _picked.Add(natural);
                    return natural;
                }
            }
        }

        while (true)
        {
            int synthetic = _anyCodePoints[_rng.Next(_anyCodePoints.Length)];
            if (_picked.Add(synthetic))
            {
                _forced[synthetic] = (LineBreakClass)cls;
                return synthetic;
            }
        }
    }

    private int RandomCodePoint()
    {
        if (_rng.Next(2) == 0)
        {
            List<int> list = _byClass[_populatedClasses[_rng.Next(_populatedClasses.Length)]];
            return list[_rng.Next(list.Count)];
        }
        int[] group = _anyGroup[_rng.Next(_anyGroup.Length)];
        return group[_rng.Next(group.Length)];
    }

    private void AppendRandom(StringBuilder text, int maxCount)
    {
        int count = _rng.Next(maxCount + 1);
        for (int i = 0; i < count; i++)
        {
            AppendCodePoint(text, RandomCodePoint());
        }
    }
}
