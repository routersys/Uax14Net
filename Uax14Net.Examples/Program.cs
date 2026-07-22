using System;
using System.Diagnostics;
using System.Text;
using Uax14Net;

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (Exception)
{
}

if (args.Length > 0 && string.Equals(args[0], "bench", StringComparison.OrdinalIgnoreCase))
{
    int runs = args.Length > 1 && int.TryParse(args[1], out int parsed) ? parsed : 20;
    RunBenchmark(runs);
    return 0;
}

if (args.Length > 0 && string.Equals(args[0], "breaks", StringComparison.OrdinalIgnoreCase))
{
    string input = args.Length > 1 ? args[1] : Console.In.ReadToEnd();
    PrintBreaks(input);
    return 0;
}

Demonstrate();
return 0;

static void Demonstrate()
{
    const string sample = "The quick (brown) fox can’t jump 12,345.67 metres—right? 日本語の文章も一行に収める。";
    Console.WriteLine("sample:");
    Console.WriteLine(sample);
    Console.WriteLine();
    Console.WriteLine("wrapped at width 30:");
    foreach (string line in Wrap(sample, 30))
    {
        Console.WriteLine("| " + line);
    }
    Console.WriteLine();
    Console.WriteLine("break opportunities:");
    foreach (LineBreakOpportunity op in LineBreaker.Enumerate(sample))
    {
        Console.WriteLine($"  {op.Position,4}  {(op.IsMandatory ? "mandatory" : "allowed")}");
    }
}

static void PrintBreaks(string text)
{
    foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text))
    {
        Console.WriteLine($"{op.Position}\t{(op.IsMandatory ? "mandatory" : "allowed")}");
    }
}

static System.Collections.Generic.List<string> Wrap(string text, int width)
{
    var lines = new System.Collections.Generic.List<string>();
    int lineStart = 0;
    int lastBreak = 0;
    foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text))
    {
        if (op.IsMandatory)
        {
            lines.Add(text.Substring(lineStart, op.Position - lineStart).TrimEnd());
            lineStart = op.Position;
            lastBreak = op.Position;
            continue;
        }
        if (op.Position - lineStart > width && lastBreak > lineStart)
        {
            lines.Add(text.Substring(lineStart, lastBreak - lineStart).TrimEnd());
            lineStart = lastBreak;
        }
        lastBreak = op.Position;
    }
    if (lineStart < text.Length)
    {
        lines.Add(text.Substring(lineStart).TrimEnd());
    }
    return lines;
}

static void RunBenchmark(int runs)
{
    string unit = "The quick (brown) fox can’t jump 12,345.67 metres—right? "
        + "日本語の文章、句読点や「引用符」も含む。ราคา ๑๒๓ บาท. שלום עולם. "
        + "person(s) e.g. https://example.com/a/b 100km \U0001F469‍\U0001F680 \U0001F1EF\U0001F1F5\n";
    var builder = new StringBuilder(unit.Length * 4096);
    for (int i = 0; i < 4096; i++)
    {
        builder.Append(unit);
    }
    string corpus = builder.ToString();

    int codePoints = 0;
    for (int i = 0; i < corpus.Length; i++)
    {
        if (!char.IsLowSurrogate(corpus[i]))
        {
            codePoints++;
        }
    }

    long breaks = Count(corpus);

    double bestMs = double.MaxValue;
    for (int run = 0; run < runs; run++)
    {
        var sw = Stopwatch.StartNew();
        long sink = Count(corpus);
        sw.Stop();
        GC.KeepAlive(sink);
        double ms = sw.Elapsed.TotalMilliseconds;
        if (ms < bestMs)
        {
            bestMs = ms;
        }
    }

    double seconds = bestMs / 1000.0;
    double megabytesPerSecond = corpus.Length * 2 / 1024.0 / 1024.0 / seconds;
    double nanosPerCodePoint = bestMs * 1_000_000.0 / codePoints;

    Console.WriteLine(
        $"BENCH chars={corpus.Length} codepoints={codePoints} breaks={breaks} "
        + $"best_ms={bestMs:F3} throughput_mb_per_s={megabytesPerSecond:F1} ns_per_codepoint={nanosPerCodePoint:F2}");
}

static long Count(string text)
{
    long n = 0;
    foreach (LineBreakOpportunity op in LineBreaker.Enumerate(text))
    {
        n += op.Position;
    }
    return n;
}
