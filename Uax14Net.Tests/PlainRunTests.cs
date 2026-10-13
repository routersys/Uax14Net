using System;
using System.Collections.Generic;
using System.Text;
using Uax14Net;

namespace Uax14Net.Tests;

public class PlainRunTests
{
    private const int CaseCount = 400000;
    private const int MaxLength = 40;

    private static void Stepwise<TDecoder>(TDecoder decoder, in LineBreakOptions options, List<long> result)
        where TDecoder : struct, IUtfDecoder, allows ref struct
    {
        result.Clear();
        var scanner = new LineBreakScanner<TDecoder>(decoder, options);
        try
        {
            while (scanner.TryNext(out int position, out BreakAction action))
            {
                if (action != BreakAction.Prohibited)
                {
                    result.Add(((long)position << 2) | (long)action);
                }
            }
        }
        finally
        {
            scanner.Dispose();
        }
    }

    private static void Skipping<TDecoder>(TDecoder decoder, in LineBreakOptions options, List<long> result)
        where TDecoder : struct, IUtfDecoder, allows ref struct
    {
        result.Clear();
        var scanner = new LineBreakScanner<TDecoder>(decoder, options);
        try
        {
            while (scanner.TryNextBreak(out int position, out BreakAction action))
            {
                result.Add(((long)position << 2) | (long)action);
            }
        }
        finally
        {
            scanner.Dispose();
        }
    }

    [Fact]
    public void ScanningPlainRunsMatchesSteppingOverUtf16()
    {
        int[][] pool = RandomCorpus.BuildPool(6);
        LineBreakOptions[] options = RandomCorpus.BuildOptions();
        CorpusRng rng = new CorpusRng(0x91A1);
        char[] buffer = new char[MaxLength * 2];
        var stepped = new List<long>();
        var skipped = new List<long>();
        for (int i = 0; i < CaseCount; i++)
        {
            int length = RandomCorpus.Generate(ref rng, pool, MaxLength, buffer, true);
            LineBreakOptions option = options[rng.Next(options.Length)];
            Stepwise(new Utf16Decoder(buffer.AsSpan(0, length)), in option, stepped);
            Skipping(new Utf16Decoder(buffer.AsSpan(0, length)), in option, skipped);
            Assert.True(stepped.SequenceEqual(skipped), "case " + i + " differs for " + Convert.ToHexString(Encoding.Unicode.GetBytes(buffer, 0, length)));
        }
    }

    [Fact]
    public void ScanningPlainRunsMatchesSteppingOverUtf8()
    {
        int[][] pool = RandomCorpus.BuildPool(6);
        LineBreakOptions[] options = RandomCorpus.BuildOptions();
        CorpusRng rng = new CorpusRng(0x91A2);
        char[] buffer = new char[MaxLength * 2];
        byte[] bytes = new byte[MaxLength * 8];
        var stepped = new List<long>();
        var skipped = new List<long>();
        for (int i = 0; i < CaseCount; i++)
        {
            int length = RandomCorpus.Generate(ref rng, pool, MaxLength, buffer, false);
            int byteLength = Encoding.UTF8.GetBytes(buffer, 0, length, bytes, 0);
            LineBreakOptions option = options[rng.Next(options.Length)];
            Stepwise(new Utf8Decoder(bytes.AsSpan(0, byteLength)), in option, stepped);
            Skipping(new Utf8Decoder(bytes.AsSpan(0, byteLength)), in option, skipped);
            Assert.True(stepped.SequenceEqual(skipped), "case " + i + " differs for " + Convert.ToHexString(bytes.AsSpan(0, byteLength)));
        }
    }
}
