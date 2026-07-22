using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Uax14Net.Tests;

internal readonly struct LineBreakTestCase
{
    public LineBreakTestCase(int lineNumber, string raw, int[] codePoints, string text, int[] boundaryOffsets, bool[] expected)
    {
        LineNumber = lineNumber;
        Raw = raw;
        CodePoints = codePoints;
        Text = text;
        BoundaryOffsets = boundaryOffsets;
        Expected = expected;
    }

    public int LineNumber { get; }

    public string Raw { get; }

    public int[] CodePoints { get; }

    public string Text { get; }

    public int[] BoundaryOffsets { get; }

    public bool[] Expected { get; }
}

internal static class LineBreakTestData
{
    public static IEnumerable<LineBreakTestCase> Parse()
    {
        string[] lines = File.ReadAllLines(ReferenceData.Path("LineBreakTest.txt"));
        for (int ln = 0; ln < lines.Length; ln++)
        {
            string rawLine = lines[ln];
            int hash = rawLine.IndexOf('#');
            string body = (hash >= 0 ? rawLine.Substring(0, hash) : rawLine).Trim();
            if (body.Length == 0)
            {
                continue;
            }

            string[] tokens = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var codePoints = new List<int>();
            var expected = new List<bool>();
            bool valid = true;
            for (int i = 0; i < tokens.Length; i++)
            {
                if ((i & 1) == 0)
                {
                    if (tokens[i] == "÷")
                    {
                        expected.Add(true);
                    }
                    else if (tokens[i] == "×")
                    {
                        expected.Add(false);
                    }
                    else
                    {
                        valid = false;
                        break;
                    }
                }
                else
                {
                    codePoints.Add(int.Parse(tokens[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                }
            }

            if (!valid || codePoints.Count == 0)
            {
                continue;
            }

            var sb = new StringBuilder();
            var offsets = new int[codePoints.Count + 1];
            for (int i = 0; i < codePoints.Count; i++)
            {
                offsets[i] = sb.Length;
                sb.Append(char.ConvertFromUtf32(codePoints[i]));
            }
            offsets[codePoints.Count] = sb.Length;

            yield return new LineBreakTestCase(
                ln + 1,
                body,
                codePoints.ToArray(),
                sb.ToString(),
                offsets,
                expected.ToArray());
        }
    }
}
