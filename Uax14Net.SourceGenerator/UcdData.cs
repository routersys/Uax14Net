using System;
using System.Collections.Generic;
using System.Globalization;

namespace Uax14Net.SourceGenerator;

internal static class UcdData
{
    public const int MaxCodePoint = 0x10FFFF;
    public const int CodePointCount = 0x110000;

    public const byte FlagEastAsian = 1;
    public const byte FlagInitialPunctuation = 2;
    public const byte FlagFinalPunctuation = 4;
    public const byte FlagPictographicUnassigned = 8;
    public const byte FlagSaCombiningBase = 16;

    public static readonly string[] Canonical =
    {
        "AI", "AK", "AL", "AP", "AS", "B2", "BA", "BB", "BK", "CB", "CJ", "CL", "CM",
        "CP", "CR", "EB", "EM", "EX", "GL", "H2", "H3", "HH", "HL", "HY", "ID", "IN",
        "IS", "JL", "JT", "JV", "LF", "NL", "NS", "NU", "OP", "PO", "PR", "QU", "RI",
        "SA", "SG", "SP", "SY", "VF", "VI", "WJ", "XX", "ZW", "ZWJ"
    };

    private static readonly Dictionary<string, int> Index = BuildIndex();

    private static readonly Dictionary<string, string> LineBreakAlias = new()
    {
        ["Unknown"] = "XX",
        ["Prefix_Numeric"] = "PR",
        ["Ideographic"] = "ID"
    };

    private static Dictionary<string, int> BuildIndex()
    {
        var map = new Dictionary<string, int>(Canonical.Length);
        for (int i = 0; i < Canonical.Length; i++)
        {
            map[Canonical[i]] = i;
        }
        return map;
    }

    public static ushort[] BuildValues(
        string derivedLineBreak,
        string eastAsianWidth,
        string emojiData,
        string derivedGeneralCategory)
    {
        var lineBreak = BuildLineBreak(derivedLineBreak);
        var eastAsian = BuildEastAsian(eastAsianWidth);
        var generalCategory = BuildGeneralCategory(derivedGeneralCategory);
        var pictographic = BuildPictographic(emojiData);

        int qu = Index["QU"];
        int sa = Index["SA"];
        var values = new ushort[CodePointCount];
        for (int cp = 0; cp < CodePointCount; cp++)
        {
            int cls = lineBreak[cp];
            byte flags = 0;
            if (eastAsian[cp])
            {
                flags |= FlagEastAsian;
            }
            if (cls == qu)
            {
                byte gc = generalCategory[cp];
                if (gc == GcInitialPunctuation)
                {
                    flags |= FlagInitialPunctuation;
                }
                else if (gc == GcFinalPunctuation)
                {
                    flags |= FlagFinalPunctuation;
                }
            }
            if (pictographic[cp] && generalCategory[cp] == GcUnassigned)
            {
                flags |= FlagPictographicUnassigned;
            }
            if (cls == sa)
            {
                byte gc = generalCategory[cp];
                if (gc == GcMarkNonspacing || gc == GcMarkSpacing)
                {
                    flags |= FlagSaCombiningBase;
                }
            }
            values[cp] = (ushort)(cls | (flags << 8));
        }
        return values;
    }

    private static byte[] BuildLineBreak(string text)
    {
        int xx = Index["XX"];
        var result = new byte[CodePointCount];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)xx;
        }

        foreach (var line in EnumerateLines(text))
        {
            int at = line.IndexOf("@missing:", StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }
            string body = line.Substring(at + "@missing:".Length);
            int semi = body.IndexOf(';');
            if (semi < 0)
            {
                continue;
            }
            if (!TryParseRange(body.Substring(0, semi), out int start, out int end))
            {
                continue;
            }
            if (start == 0 && end >= MaxCodePoint)
            {
                continue;
            }
            string value = body.Substring(semi + 1).Trim();
            string cls = LineBreakAlias.TryGetValue(value, out string? mapped) ? mapped : value;
            if (!Index.TryGetValue(cls, out int ci))
            {
                throw new FormatException($"unknown Line_Break @missing value '{value}'");
            }
            Fill(result, start, end, (byte)ci);
        }

        foreach (var (start, end, value) in EnumerateAssignments(text))
        {
            if (!Index.TryGetValue(value, out int ci))
            {
                throw new FormatException($"unknown Line_Break class '{value}'");
            }
            Fill(result, start, end, (byte)ci);
        }
        return result;
    }

    private static bool[] BuildEastAsian(string text)
    {
        var result = new bool[CodePointCount];
        foreach (var (start, end, value) in EnumerateAssignments(text))
        {
            bool asian = value == "F" || value == "W" || value == "H";
            if (!asian)
            {
                continue;
            }
            for (int cp = start; cp <= end; cp++)
            {
                result[cp] = true;
            }
        }
        return result;
    }

    private const byte GcUnassigned = 0;
    private const byte GcMarkNonspacing = 1;
    private const byte GcMarkSpacing = 2;
    private const byte GcInitialPunctuation = 3;
    private const byte GcFinalPunctuation = 4;
    private const byte GcOtherAssigned = 5;

    private static byte[] BuildGeneralCategory(string text)
    {
        var result = new byte[CodePointCount];
        foreach (var (start, end, value) in EnumerateAssignments(text))
        {
            byte code = value switch
            {
                "Mn" => GcMarkNonspacing,
                "Mc" => GcMarkSpacing,
                "Pi" => GcInitialPunctuation,
                "Pf" => GcFinalPunctuation,
                "Cn" => GcUnassigned,
                _ => GcOtherAssigned
            };
            if (code == GcUnassigned)
            {
                continue;
            }
            for (int cp = start; cp <= end; cp++)
            {
                result[cp] = code;
            }
        }
        return result;
    }

    private static bool[] BuildPictographic(string text)
    {
        var result = new bool[CodePointCount];
        foreach (var (start, end, value) in EnumerateAssignments(text))
        {
            if (value != "Extended_Pictographic")
            {
                continue;
            }
            for (int cp = start; cp <= end; cp++)
            {
                result[cp] = true;
            }
        }
        return result;
    }

    private static IEnumerable<(int start, int end, string value)> EnumerateAssignments(string text)
    {
        foreach (var line in EnumerateLines(text))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            int hash = line.IndexOf('#');
            string content = hash >= 0 ? line.Substring(0, hash) : line;
            int semi = content.IndexOf(';');
            if (semi < 0)
            {
                continue;
            }
            if (!TryParseRange(content.Substring(0, semi), out int start, out int end))
            {
                continue;
            }
            string value = content.Substring(semi + 1).Trim();
            if (value.Length == 0)
            {
                continue;
            }
            yield return (start, end, value);
        }
    }

    private static void Fill(byte[] array, int start, int end, byte value)
    {
        if (start < 0)
        {
            start = 0;
        }
        if (end > MaxCodePoint)
        {
            end = MaxCodePoint;
        }
        for (int cp = start; cp <= end; cp++)
        {
            array[cp] = value;
        }
    }

    private static bool TryParseRange(string token, out int start, out int end)
    {
        start = 0;
        end = 0;
        token = token.Trim();
        int dots = token.IndexOf("..", StringComparison.Ordinal);
        if (dots >= 0)
        {
            return TryParseHex(token.Substring(0, dots), out start)
                && TryParseHex(token.Substring(dots + 2), out end);
        }
        if (TryParseHex(token, out start))
        {
            end = start;
            return true;
        }
        return false;
    }

    private static bool TryParseHex(string token, out int value)
        => int.TryParse(token.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

    private static IEnumerable<string> EnumerateLines(string text)
    {
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n' || c == '\r')
            {
                if (i > start)
                {
                    yield return text.Substring(start, i - start);
                }
                else
                {
                    yield return string.Empty;
                }
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                start = i + 1;
            }
        }
        if (start < text.Length)
        {
            yield return text.Substring(start);
        }
    }
}
