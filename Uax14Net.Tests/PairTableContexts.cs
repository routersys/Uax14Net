using System;
using System.Collections.Generic;
using System.Text;
using Uax14Net;

namespace Uax14Net.Tests;

internal sealed class PairTableContexts
{
    private const int Rows = 98;
    private const int Columns = 49;

    private static readonly int[] KeyClasses =
    {
        (int)LineBreakClass.NU, (int)LineBreakClass.SY, (int)LineBreakClass.IS, (int)LineBreakClass.CL,
        (int)LineBreakClass.CP, (int)LineBreakClass.OP, (int)LineBreakClass.PO, (int)LineBreakClass.PR,
        (int)LineBreakClass.HY, (int)LineBreakClass.QU, (int)LineBreakClass.SP, (int)LineBreakClass.RI,
        (int)LineBreakClass.AL, (int)LineBreakClass.ID, (int)LineBreakClass.ZW, (int)LineBreakClass.BK,
        (int)LineBreakClass.GL, (int)LineBreakClass.WJ, (int)LineBreakClass.B2, (int)LineBreakClass.HL,
        (int)LineBreakClass.EB, (int)LineBreakClass.EM, (int)LineBreakClass.AK, (int)LineBreakClass.VF
    };

    private readonly List<int>[] _byClass = new List<int>[Columns];
    private readonly int[][] _anyGroup;
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
        foreach (int[] group in _anyGroup)
        {
            foreach (int cp in group)
            {
                ushort entry = LineBreakData.Lookup(cp);
                LineBreakClass cls = LineBreakResolver.Resolve((LineBreakClass)(byte)entry, (byte)(entry >> 8), LineBreakOptions.Default);
                if (cls == LineBreakClass.CM || cls == LineBreakClass.ZWJ)
                {
                    continue;
                }
                _byClass[(int)cls].Add(cp);
            }
        }
        _byClass[(int)LineBreakClass.AL].Add(0x25CC);
    }

    public static int ActionAt(string text, int index, bool useTable)
    {
        var scanner = new LineBreakScanner<Utf16Decoder>(new Utf16Decoder(text), LineBreakOptions.Default, useTable);
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

        var text = new StringBuilder();
        if (!atStart)
        {
            AppendRandom(text, 3);
        }
        if (spaceRow && !atStart)
        {
            AppendCodePoint(text, Pick(beforeSp));
        }
        if (spaceRow)
        {
            int spaces = 1 + _rng.Next(3);
            for (int i = 0; i < spaces; i++)
            {
                AppendCodePoint(text, 0x20);
            }
        }
        else
        {
            AppendCodePoint(text, Pick(rowClass));
        }
        index = text.Length;
        AppendCodePoint(text, Pick(next));
        AppendRandom(text, 2);
        return text.ToString();
    }

    public string DeriveSource(int samples)
    {
        char[] table = new char[Rows * Columns];
        Array.Fill(table, '3');
        for (int row = 0; row < Rows; row++)
        {
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
                    int result = ActionAt(text, index, false);
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

        var source = new StringBuilder();
        source.Append("using System;\n\nnamespace Uax14Net;\n\ninternal static class LineBreakPairTable\n{\n");
        source.Append("    public const int Columns = 49;\n    public const int SpaceRowBase = 49;\n");
        source.Append("    public const byte Digit = (byte)'0';\n\n    public static ReadOnlySpan<byte> Entries =>\n");
        for (int row = 0; row < Rows; row++)
        {
            source.Append("        \"").Append(table, row * Columns, Columns).Append("\"u8");
            source.Append(row < Rows - 1 ? " +\n" : ";\n");
        }
        source.Append("}\n");
        return source.ToString();
    }

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

    private int Pick(int cls)
    {
        List<int> list = _byClass[cls];
        return list[_rng.Next(list.Count)];
    }

    private int RandomCodePoint()
    {
        if (_rng.Next(2) == 0)
        {
            List<int> list = _byClass[KeyClasses[_rng.Next(KeyClasses.Length)]];
            if (list.Count > 0)
            {
                return list[_rng.Next(list.Count)];
            }
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
