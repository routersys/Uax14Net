# Uax14Net

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/Uax14Net.svg)](https://github.com/routersys/Uax14Net/releases)

English | [日本語](https://github.com/routersys/Uax14Net/blob/main/README.ja.md)

---

A pure C# implementation of the [Unicode Line Breaking Algorithm](https://www.unicode.org/reports/tr14/) defined by Unicode Standard Annex #14.
It determines where a line of Unicode text may be broken and where a break is mandatory, conforming to Unicode 17.0.0 and UAX #14 Revision 55.
The algorithm runs over UTF-16 or UTF-8 text without a single managed allocation, so the garbage collector never observes the scan, and the library is annotated for Native AOT.
Correctness is not asserted from reading the specification: every boundary is compared against the official LineBreakTest.txt of Unicode 17.0.0.

---

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [Installation](#installation)
4. [Features](#features)
   - [1. Break opportunities and mandatory breaks](#1-break-opportunities-and-mandatory-breaks)
   - [2. Line breaking classes and their resolution](#2-line-breaking-classes-and-their-resolution)
   - [3. Combining marks, joiners and spaces](#3-combining-marks-joiners-and-spaces)
   - [4. Zero allocation and the generated trie](#4-zero-allocation-and-the-generated-trie)
   - [5. Conformance verification](#5-conformance-verification)
   - [6. Performance](#6-performance)
5. [API Reference](#api-reference)
   - [Enumeration](#enumeration)
   - [Opportunities](#opportunities)
   - [Classes](#classes)
   - [Tailoring](#tailoring)
6. [Limitations](#limitations)
7. [Notes](#notes)
8. [Reporting issues](#reporting-issues)
9. [Disclaimer](#disclaimer)
10. [Third-Party Licenses](#third-party-licenses)
11. [License](#license)

---

## Overview

Uax14Net decides line break opportunities for Unicode text. It reports every position at which a line may be broken, and marks the positions where a break is mandated by a hard line break such as a line feed, a carriage return, a form feed or a next line character.

The public surface is modern C#. Text is passed as `ReadOnlySpan<char>` or as UTF-8 in a `ReadOnlySpan<byte>`, opportunities are produced by a `ref struct` enumerator so that a whole document can be scanned without heap traffic, each opportunity is a `readonly struct` carrying an offset and a kind, and the line breaking class of any code point is available through a single static method. Tailoring is carried by a `readonly struct` of options, and the resolution of the complex-context class is a public seam. The stateful scanner is not exposed.

The full set of line breaking classes, together with the auxiliary properties the rules depend on, is compiled into a two-stage lookup trie at build time. A source generator reads the Unicode Character Database files supplied as additional files and emits the trie as static read-only data, so the classification carries no runtime parsing and no static initialisation cost.

The Unicode data is not vendored into this repository. The harness under `reference/` downloads the pinned Unicode 17.0.0 data files, verifies their version header and writes them to `reference/data`. The source generator consumes those files to build the trie, and the test suite loads the official LineBreakTest.txt from the same place and compares it against the scanner.

---

## Requirements

| Item | Requirement |
|---|---|
| OS | Windows, Linux or macOS supported by .NET 10 |
| SDK | .NET SDK 10.0 |
| Language | C# 14 or later (`LangVersion` is set to `latest`) |
| Unsafe code | Not required in the consuming project |
| Reference data | curl, required to download the Unicode data files used by the build and the tests |

---

## Installation

```sh
dotnet add package Uax14Net
```

1. Download the Unicode 17.0.0 data before building from source by running `reference/build.sh`, or `reference/build.bat` on Windows. The source generator needs those files to build the classification trie, and the tests need LineBreakTest.txt.
2. Call `LineBreaker.Enumerate` with the text and iterate the opportunities. The text is passed as `ReadOnlySpan<char>`, so a `string`, an array or a slice are all accepted without copying.
3. Read each opportunity as a UTF-16 offset and a kind. An allowed opportunity is a position where a line may be broken; a mandatory opportunity is a position where a break is required.
4. To produce a Native AOT binary of the sample application, run `publish-aot.bat`.

---

## Features

### 1. Break opportunities and mandatory breaks

The scanner produces one result per break opportunity. A position is reported when the algorithm allows a break there or requires one. Positions where a break is prohibited are not reported, so a consumer that wraps text iterates only the candidates it can act upon.

Mandatory breaks arise from the hard line breaks of rules LB4 and LB5: the line feed, the carriage return, the next line character and the mandatory break class that covers the vertical tab and the form feed. A carriage return followed by a line feed is treated as a single break rather than two. The end of the text is reported once as a mandatory boundary, in keeping with rule LB3.

### 2. Line breaking classes and their resolution

Every code point is assigned one of the forty-nine line breaking classes of Unicode 17.0.0. The classes that the algorithm resolves before applying the rules, following rule LB1, are resolved by default: ambiguous, surrogate and unknown become alphabetic, conditional Japanese starter becomes non-starter, and the complex-context class becomes a combining mark when its general category is a non-spacing or spacing mark and alphabetic otherwise.

The rules that depend on more than the class itself are supported in full. The East Asian width distinguishes the treatment of quotation marks in rule LB19a and of parentheses in rule LB30. The general category separates initial from final punctuation for the quotation rules LB15a, LB15b and LB19. The Brahmic classes drive the orthographic syllable rule LB28a, including the dotted circle. Regional indicators are paired by rule LB30a, emoji bases and modifiers are joined by rule LB30b, and the numeric rules LB23 through LB25 keep numbers and their prefixes, postfixes and separators together.

The classes resolved by rule LB1 are tailorable through `LineBreakOptions`. The strictness selects whether the conditional Japanese starter keeps small kana attached or allows a break before them, the ambiguous-width policy resolves ambiguous characters as alphabetic or ideographic, `WordBreakMode` offers break-all and keep-all, and a class-override delegate reassigns the line breaking class of any code point. The resolution of the complex-context class is a public seam: an `IComplexContextResolver` receives each maximal run of complex-context characters and reports the breaks within it, which lets a dictionary segmenter for Thai, Lao, Khmer or Burmese be plugged in. The default resolution, used when no resolver is supplied, assigns the combining mark or alphabetic class by general category and inserts no break inside such a run.

### 3. Combining marks, joiners and spaces

A combining character sequence is treated as its base character, as required by rule LB9. A combining mark or a zero width joiner that follows a base is folded into that base and produces no break before itself. A combining mark that has no base, because it opens the text or follows a space or a hard line break, is treated as alphabetic by rule LB10.

The zero width joiner prevents a break after itself under rule LB8a, which keeps an emoji zero-width-joiner sequence together. The word joiner prevents a break on either side under rule LB11, and the non-breaking classes are handled by rules LB12 and LB12a. A zero width space produces a break opportunity after itself and after any spaces that follow it, under rule LB8.

### 4. Zero allocation and the generated trie

The scanner is a `ref struct`. Its entire state lives on the stack, the classification data is static read-only memory, and no array or object is allocated while a text is scanned, whether the input is UTF-16 or UTF-8. The classification is a two-stage trie whose blocks are deduplicated and emitted as a `ReadOnlySpan<byte>`, which the runtime serves directly from the assembly without a heap copy. When a complex-context resolver is supplied, each run is handed to it through a pooled buffer; the default path allocates nothing.

The absence of managed allocation is measured, not asserted. `GC.GetAllocatedBytesForCurrentThread` reports a delta of zero bytes across the enumeration of a multilingual sample on both the UTF-16 and the UTF-8 path, and across the classification of every code point in the Unicode range.

### 5. Conformance verification

The scanner is compared against the official LineBreakTest.txt of Unicode 17.0.0. Each test line lists a sequence of code points with a break or a no-break marker at every boundary, including the boundaries before the first and after the last code point. The suite reproduces every marker.

| Item | Agreement |
|---|---|
| LineBreakTest.txt, all 19338 lines | Exact |
| Break and no-break at every boundary, including start and end of text | Exact |
| Boundaries reported as UTF-16 offsets across the supplementary planes | Exact |

The test project contains 69 tests and all of them pass. The conformance test alone drives all 19338 cases of the official file, and a second test replays the whole corpus through the UTF-8 path and checks that its byte offsets agree with the UTF-16 offsets. The remaining tests cover the empty string, single characters, hard and mandatory breaks, the carriage-return line-feed pair, supplementary plane offsets, emoji zero-width-joiner sequences, regional indicator pairing, lone surrogates, the whole code space, the tailoring options, the complex-context resolver seam, and the absence of managed allocation on both the UTF-16 and the UTF-8 path. Three further groups of tests guard the optimisations. One fixes the output over a pseudo-random corpus of 150000 sequences under fourteen option sets, by a hash recorded before the pair table existed. One compares the pair table with the rules, over 400000 random sequences and over every table entry in random contexts. One compares the scan of runs of characters with scanning one character at a time, over 400000 random sequences on the UTF-16 path and as many on the UTF-8 path.

### 6. Performance

The algorithm is a sequential scan: each break decision depends on the classes and the accumulated state to its left, so the scan cannot be vectorised across characters. The throughput therefore comes from the compact trie, from a pair table, from aggressive inlining and from the absence of allocation, rather than from single-instruction-multiple-data parallelism.

The pair table holds the decision for a pair of classes, indexed by the class before a position and the class after it. After spaces it is indexed by the class before the spaces and the class after them. A decision that depends on more than those classes, for example on the East Asian width, on a look-ahead or on the state accumulated to the left, is made by the rules instead, and with `WordBreak` set to `BreakAll` or `KeepAll` the rules decide every position. A pair is stored in the table only if its result never varied when the rules evaluated it in 6000 random contexts.

Where the table decides, the scanner reads a run of characters at a time. It advances in local variables while each character belongs to a class that needs nothing beyond the number state, and it returns at a break or at the first character that needs the general path. Such a character is a quotation mark, a regional indicator, a character of a Brahmic script, a combining mark, or the second character of a pair that the table leaves to the rules. The classes AI, CJ, SG and XX, and SA when no resolver is set, are read as the class that rule LB1 assigns them.

The first table is a fixed record taken on a workstation. It is not regenerated.

13th Gen Intel Core i7-1360P under Windows 11, sample application published with Native AOT for an `x86-64-v3` baseline, best of 480 runs, which are 30 runs in each of 16 processes alternating between the builds, over a multilingual corpus of 630784 code points containing Latin, CJK, Thai, Hebrew, digits, punctuation, quotation marks, emoji and regional indicators.

| Build | Native AOT throughput | Native AOT time per code point | JIT throughput | JIT time per code point |
|---|---:|---:|---:|---:|
| Before the optimisations | 59.8 MB/s | 32.74 ns | 54.1 MB/s | 36.14 ns |
| After the optimisations | 244.4 MB/s | 8.01 ns | 233.5 MB/s | 8.38 ns |

The sample application sets `TieredCompilation` to false in its project file, so its just-in-time figures are the same on the first pass as after warming up. They are the best of 30 runs in a process, after one untimed pass over the corpus. Every optimisation below leaves the output identical, which the tests above check. The effect of each is its speed-up over the stage before it, measured with the Native AOT build of the sample, alternating between all six builds.

| Optimisation | Effect |
|---|---|
| Decision for a pair of classes read from a table | 1.54x |
| State of the scan updated with fewer branches for classes that keep no state | 1.07x |
| Runs of characters scanned in local variables | 1.64x |
| Number and punctuation classes included in those runs | 1.41x |
| Line break classes and classes read as another class included in those runs | 1.11x |

The two routines that contain the scanning loops carry `MethodImplOptions.AggressiveOptimization`, so that under the just-in-time compiler they are optimized at the first call instead of starting in the unoptimized tier. Native AOT compiles ahead of time and is unaffected. With tiered compilation at its default, the first pass over the corpus in a fresh process took 24.68 ms before the optimisations, 10.91 ms after them without the mark and 7.44 ms with the mark. A marked routine does not benefit from dynamic profile-guided optimization, which the documentation of `MethodImplOptions` states, and once a process has warmed up the best of 30 runs took 5.04 ms with the mark against 4.70 ms without it.

The second section is regenerated by the benchmark workflow on a GitHub Actions runner. The hardware differs from the workstation, which makes it an independent check that the figures do not depend on one particular machine.

<!-- BENCHMARK:CI:BEGIN -->

Measured by CI on a GitHub Actions `windows-latest` runner with AMD64 Family 25 Model 17 Stepping 1, AuthenticAMD. Figures are the best of 50 runs over a 630784 code point multilingual corpus, taken in one process after one untimed pass. Both builds run back to back in the same job. Recorded on 2026-10-13 from commit `f31d682`.

| Metric | Native AOT | JIT |
|---|---:|---:|
| Throughput | 235.1 MB/s | 243.0 MB/s |
| Time per code point | 8.32 ns | 8.05 ns |

<!-- BENCHMARK:CI:END -->

The Native AOT figures come from the sample application published with Native AOT, and the JIT figures from the same application run through the just-in-time compiler.

---

## API Reference

```csharp
using Uax14Net;

foreach (LineBreakOpportunity op in LineBreaker.Enumerate("The quick brown fox"))
{
    if (op.IsMandatory)
    {
        Console.WriteLine($"mandatory break at {op.Position}");
    }
    else
    {
        Console.WriteLine($"allowed break at {op.Position}");
    }
}
```

### Enumeration

| Member | Description |
|---|---|
| `LineBreaker.Enumerate(ReadOnlySpan<char>)` | Returns a `ref struct` enumerator over the break opportunities of UTF-16 text. |
| `LineBreaker.Enumerate(ReadOnlySpan<char>, in LineBreakOptions)` | The same, with tailoring options. |
| `LineBreaker.Enumerate(ReadOnlySpan<byte>)` | Enumerates UTF-8 text and reports byte offsets. |
| `LineBreaker.Enumerate(ReadOnlySpan<byte>, in LineBreakOptions)` | The same, with tailoring options. |
| `LineBreakEnumerator`, `Utf8LineBreakEnumerator` | The `ref struct` enumerators, with `GetEnumerator`, `MoveNext`, `Current` and `Dispose`. |

The enumerator yields one opportunity per position at which a break is allowed or required. Positions where a break is prohibited are skipped. The final opportunity is at the offset equal to the length of the text and is always mandatory. The UTF-16 enumerator reports UTF-16 offsets; the UTF-8 enumerator reports byte offsets. Invalid UTF-8 is decoded as the replacement character.

### Opportunities

| Member | Description |
|---|---|
| `LineBreakOpportunity.Position` | The UTF-16 offset at which the line may be broken. A break is placed before the code unit at this offset. |
| `LineBreakOpportunity.Kind` | `LineBreakKind.Allowed` or `LineBreakKind.Mandatory`. |
| `LineBreakOpportunity.IsMandatory` | True when the break is required by a hard line break or the end of the text. |

`LineBreakKind` is `Allowed` or `Mandatory`. An allowed break is a candidate for wrapping; a mandatory break must be honoured regardless of the available width.

### Classes

| Member | Description |
|---|---|
| `LineBreaker.GetLineBreakClass(int)` | Returns the `Line_Break` property value of a Unicode scalar value. |
| `LineBreakClass` | The forty-nine line breaking classes of Unicode 17.0.0. |

`GetLineBreakClass` returns the property value as recorded in the Unicode Character Database, before the resolution of rule LB1. A code point outside the Unicode range resolves to the unknown class.

### Tailoring

`LineBreakOptions` is a `readonly struct` with `init` accessors; `LineBreakOptions.Default` is the plain UAX #14 default that the conformance suite verifies.

| Member | Default | Description |
|---|---|---|
| `Strictness` | `Strict` | `Strict` keeps small kana attached as non-starters; `Normal` resolves the conditional Japanese starter to ideographic and allows a break before small kana. |
| `WordBreak` | `Normal` | `BreakAll` allows a break at every position the non-tailorable rules permit; `KeepAll` suppresses the break between two ideographic characters. |
| `AmbiguousWidth` | `Alphabetic` | Resolves the ambiguous class to alphabetic or to ideographic. |
| `ClassOverride` | `null` | A delegate `int -> LineBreakClass?` that reassigns the line breaking class of a code point before rule LB1. |
| `ComplexContextResolver` | `null` | An `IComplexContextResolver` that segments runs of complex-context characters. |

`IComplexContextResolver.Resolve(ReadOnlySpan<char> run, Span<bool> breakBefore)` is called once per maximal run of complex-context characters. Setting `breakBefore[i]` allows a break before `run[i]`. The engine ignores a break the non-tailorable rules forbid, so a resolver cannot produce a non-conforming result. A resolver applies to UTF-16 input; on UTF-8 input the default resolution is used.

---

## Limitations

- Measuring the width of text, choosing the break that fits a given line width, rendering, hyphenation and language specific dictionary segmentation are outside the scope of this library. It reports break opportunities; the choice of where to break for a particular width belongs to the layout engine.
- No dictionary for the complex-context scripts is bundled. Without a resolver, Thai, Lao, Khmer, Burmese and similar scripts break only at spaces, which is the UAX #14 default. A dictionary segmenter can be supplied through `IComplexContextResolver`; the library provides the seam, not the dictionary, and the seam applies to UTF-16 input.
- Text is processed as a whole span. Incremental line breaking across separately supplied chunks is not provided, because the lookahead of several rules crosses the chunk boundary.
- The class assignments and the rules track Unicode 17.0.0 and UAX #14 Revision 55. A different Unicode version requires regenerating the trie from the corresponding data files.
- Building from source requires the Unicode data files. Without `reference/data` the source generator cannot build the trie and the conformance test cannot execute.
- The sample application under `Uax14Net.Examples` writes to the console and therefore allocates managed memory. The zero-allocation guarantee applies to the library.

---

## Notes

- Input encoding: UTF-16 input reports UTF-16 code unit offsets, and UTF-8 input reports byte offsets. A surrogate pair is a single scalar value at the offset of its high surrogate, a lone surrogate is treated as a single unit of the surrogate class, and an invalid UTF-8 sequence is decoded as one replacement character.
- Determinism: the scan produces identical output across repeated runs and holds no state between calls. Because the enumerator is a `ref struct` over a caller-owned span, separate enumerations are independent and no instance is shared.
- Resolution seam: rule LB1 resolution is separate from the rule engine, so the tailoring options and the complex-context resolver change class assignments and interior breaks without touching the non-tailorable rules.
- Pair table: `Uax14Net/LineBreakPairTable.cs` is derived from the rules, and the tests compare it with them. The [contributing guide](https://github.com/routersys/Uax14Net/blob/main/CONTRIBUTING.md) describes how to regenerate it after a change to the rules.
- Native AOT: the library sets `IsAotCompatible`, which enables the trim, single-file and AOT analyzers. `publish-aot.bat` publishes the sample application for `win-x64` and requires the MSVC toolset for the native linker.
- Regenerating reference data: `reference/build.sh` and `reference/build.bat` download the Unicode 17.0.0 data files, verify their version header, and write them to `reference/data`. That directory is excluded from version control.

---

## Reporting issues

Report problems on GitHub [Issues](https://github.com/routersys/Uax14Net/issues). The library does not send any report automatically.

Please include the following in the report.

| Item | Content |
|---|---|
| Environment | The version of Uax14Net, the version of the .NET SDK or runtime, the operating system and the processor architecture |
| Input | The text as code points, and whether it was passed as UTF-16 or as UTF-8 |
| Steps | The calls in the order they were made, including every option that differs from its default |
| Result | The expected and the actual break offsets |
| Exception | The type, the message and the stack trace, unmodified |

Report a vulnerability privately as the [security policy](https://github.com/routersys/Uax14Net/blob/main/SECURITY.md) describes, not in an issue.

To contribute a change, read the [contributing guide](https://github.com/routersys/Uax14Net/blob/main/CONTRIBUTING.md).

---

## Disclaimer

This library is published under the MIT License.

This software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement.

The author accepts no liability for any damage arising from the use of or the inability to use this library. Use it at your own risk.

---

## Third-Party Licenses

Uax14Net is built from the Unicode Character Database. The full license text is stored in the repository under [`.github/LICENSE/UnicodeDataFiles.txt`](https://github.com/routersys/Uax14Net/blob/main/.github/LICENSE/UnicodeDataFiles.txt).

The Unicode data files are distributed under the Unicode License V3, which permits use, modification and redistribution provided that the copyright and permission notice accompany the data. That file carries the text unmodified. No Unicode data file is vendored into this repository, and the reference harness downloads the data on demand.

| Software | Purpose | License | Copyright |
|---|---|---|---|
| [Unicode Character Database](https://www.unicode.org/ucd/) | Source of the line breaking classes and the auxiliary properties, and of the LineBreakTest.txt used for verification | Unicode License V3 | Copyright © 1991-2026 Unicode, Inc. |

---

## License

[MIT License](https://github.com/routersys/Uax14Net/blob/main/LICENSE.txt)
