using System;
using System.Collections.Generic;
using System.IO;
using Uax14Net;

namespace Uax14Net.Tests;

public class PairTableTests
{
    private const int CaseCount = 400000;
    private const int MaxLength = 30;
    private const int ContextsPerEntry = 200;
    private const int DerivationSamples = 6000;
    private const string OutputVariable = "UAX14NET_PAIR_TABLE_OUTPUT";

    private static void Trace(string text, in LineBreakOptions options, bool useTable, List<long> trace)
    {
        trace.Clear();
        var scanner = new LineBreakScanner<Utf16Decoder>(new Utf16Decoder(text), options, useTable);
        try
        {
            while (scanner.TryNext(out int position, out BreakAction action))
            {
                trace.Add(((long)position << 2) | (long)action);
            }
        }
        finally
        {
            scanner.Dispose();
        }
    }

    [Fact]
    public void TablePathMatchesRulePathOverRandomText()
    {
        int[][] pool = RandomCorpus.BuildPool(6);
        LineBreakOptions[] options = RandomCorpus.BuildOptions();
        CorpusRng rng = new CorpusRng(0x7AB1E);
        char[] buffer = new char[MaxLength * 2];
        var viaTable = new List<long>();
        var viaRules = new List<long>();
        for (int i = 0; i < CaseCount; i++)
        {
            int length = RandomCorpus.Generate(ref rng, pool, MaxLength, buffer, true);
            string text = new string(buffer, 0, length);
            LineBreakOptions option = options[rng.Next(options.Length)];
            Trace(text, in option, true, viaTable);
            Trace(text, in option, false, viaRules);
            Assert.True(viaTable.SequenceEqual(viaRules), "case " + i + " differs for " + Convert.ToHexString(System.Text.Encoding.Unicode.GetBytes(text)));
        }
    }

    [Fact]
    public void EveryTableEntryMatchesRulePathInRandomContexts()
        => CheckEntries(LineBreakPairTable.Entries, new LineBreakOptions());

    [Fact]
    public void EveryBreakAllEntryMatchesRulePathInRandomContexts()
        => CheckEntries(LineBreakPairTable.BreakAllEntries, new LineBreakOptions { WordBreak = WordBreakMode.BreakAll });

    [Fact]
    public void EveryKeepAllEntryMatchesRulePathInRandomContexts()
        => CheckEntries(LineBreakPairTable.KeepAllEntries, new LineBreakOptions { WordBreak = WordBreakMode.KeepAll });

    private static void CheckEntries(ReadOnlySpan<byte> entries, LineBreakOptions options)
    {
        var contexts = new PairTableContexts(0xBEEF);
        int checkedEntries = 0;
        for (int row = 0; row < entries.Length / LineBreakPairTable.Columns; row++)
        {
            for (int next = 0; next < LineBreakPairTable.Columns; next++)
            {
                int entry = entries[row * LineBreakPairTable.Columns + next] - LineBreakPairTable.Digit;
                if (entry >= 3)
                {
                    continue;
                }
                Assert.True(contexts.IsBuildable(row, next), "row " + row + " next " + next + " cannot be built");
                for (int i = 0; i < ContextsPerEntry; i++)
                {
                    string text = contexts.Build(row, next, out int index);
                    int expected = contexts.ActionAt(text, index, false, in options);
                    Assert.True(entry == expected, "row " + row + " next " + next + " table " + entry + " rules " + expected + " at " + index + " in " + Convert.ToHexString(System.Text.Encoding.Unicode.GetBytes(text)));
                }
                checkedEntries++;
            }
        }
        Assert.True(checkedEntries > 3000);
    }

    [Fact]
    public void WritesDerivedTableWhenRequested()
    {
        string? path = Environment.GetEnvironmentVariable(OutputVariable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }
        char[] normal = Derive(new LineBreakOptions());
        char[] breakAll = Derive(new LineBreakOptions { WordBreak = WordBreakMode.BreakAll });
        char[] keepAll = Derive(new LineBreakOptions { WordBreak = WordBreakMode.KeepAll });
        File.WriteAllText(path, PairTableContexts.ToSource(normal, breakAll, keepAll));
    }

    private static char[] Derive(LineBreakOptions options)
    {
        char[] first = new PairTableContexts(0xA11CE).DeriveTable(DerivationSamples, options);
        char[] second = new PairTableContexts(0xB0B5).DeriveTable(DerivationSamples, options);
        return PairTableContexts.Agree(first, second);
    }
}
