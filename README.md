# Uax14Net

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/Uax14Net.svg)](https://github.com/routersys/Uax14Net/releases)

English | [日本語](https://github.com/routersys/Uax14Net/blob/main/README.ja.md)

---

A pure C# implementation of the [Unicode Line Breaking Algorithm](https://www.unicode.org/reports/tr14/) defined by Unicode Standard Annex #14.
It determines where a line of Unicode text may be broken and where a break is mandatory, conforming to Unicode 17.0.0 and UAX #14 Revision 55.
The algorithm runs over a `ReadOnlySpan<char>` without a single managed allocation, so the garbage collector never observes the scan, and the library is annotated for Native AOT.
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
6. [Limitations](#limitations)
7. [Notes](#notes)
8. [Disclaimer](#disclaimer)
9. [Third-Party Licenses](#third-party-licenses)
10. [License](#license)

---

## Overview

Uax14Net decides line break opportunities for Unicode text. It reports every position at which a line may be broken, and marks the positions where a break is mandated by a hard line break such as a line feed, a carriage return, a form feed or a next line character.

The public surface is modern C#. Text is passed as `ReadOnlySpan<char>`, opportunities are produced by a `ref struct` enumerator so that a whole document can be scanned without heap traffic, each opportunity is a `readonly struct` carrying a UTF-16 offset and a kind, and the line breaking class of any code point is available through a single static method. The stateful scanner is not exposed.

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
| Reference data | Git or curl, required to download the Unicode data files used by the build and the tests |

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

The default resolution of the complex-context class is a distinct step, so that a language specific analyser can later replace it without touching the non-tailorable rules. The first release exposes only the default resolution.

### 3. Combining marks, joiners and spaces

A combining character sequence is treated as its base character, as required by rule LB9. A combining mark or a zero width joiner that follows a base is folded into that base and produces no break before itself. A combining mark that has no base, because it opens the text or follows a space or a hard line break, is treated as alphabetic by rule LB10.

The zero width joiner prevents a break after itself under rule LB8a, which keeps an emoji zero-width-joiner sequence together. The word joiner prevents a break on either side under rule LB11, and the non-breaking classes are handled by rules LB12 and LB12a. A zero width space produces a break opportunity after itself and after any spaces that follow it, under rule LB8.

### 4. Zero allocation and the generated trie

The scanner is a `ref struct`. Its entire state lives on the stack, the classification data is static read-only memory, and no array or object is allocated while a text is scanned. The classification is a two-stage trie whose blocks are deduplicated and emitted as a `ReadOnlySpan<byte>`, which the runtime serves directly from the assembly without a heap copy.

The absence of managed allocation is measured, not asserted. `GC.GetAllocatedBytesForCurrentThread` reports a delta of zero bytes across the enumeration of a multilingual sample and across the classification of every code point in the Unicode range.

### 5. Conformance verification

The scanner is compared against the official LineBreakTest.txt of Unicode 17.0.0. Each test line lists a sequence of code points with a break or a no-break marker at every boundary, including the boundaries before the first and after the last code point. The suite reproduces every marker.

| Item | Agreement |
|---|---|
| LineBreakTest.txt, all 19338 lines | Exact |
| Break and no-break at every boundary, including start and end of text | Exact |
| Boundaries reported as UTF-16 offsets across the supplementary planes | Exact |

The test project contains 43 tests and all of them pass. The conformance test alone drives all 19338 cases of the official file. The remaining tests cover the empty string, single characters, hard and mandatory breaks, the carriage-return line-feed pair, supplementary plane offsets, emoji zero-width-joiner sequences, regional indicator pairing, lone surrogates, the whole code space, and the absence of managed allocation.

### 6. Performance

The algorithm is a sequential scan: each break decision depends on the classes and the accumulated state to its left, so the scan cannot be vectorised across characters. The throughput therefore comes from the compact trie, from aggressive inlining and from the absence of allocation, rather than from single-instruction-multiple-data parallelism.

The first table is a fixed record taken on a workstation. It is not regenerated.

13th Gen Intel Core i7-1360P under Windows 11, sample application published with Native AOT for an `x86-64-v3` baseline, best of 30 runs, over a multilingual corpus of 630784 code points containing Latin, CJK, Thai, Hebrew, digits, punctuation, quotation marks, emoji and regional indicators.

| Metric | Native AOT |
|---|---:|
| Throughput | 70.1 MB/s |
| Time per code point | 27.92 ns |

The second section is regenerated by the benchmark workflow on a GitHub Actions runner. The hardware differs from the workstation, which makes it an independent check that the figures do not depend on one particular machine.

<!-- BENCHMARK:CI:BEGIN -->

Measured by CI on a GitHub Actions `windows-latest` runner with AMD64 Family 25 Model 1 Stepping 1, AuthenticAMD. Figures are the best of 20 runs over a 630784 code point multilingual corpus, published with Native AOT. Recorded on 2026-07-22 from commit `862baed`.

| Metric | Native AOT |
|---|---:|
| Throughput | 47.5 MB/s |
| Time per code point | 41.18 ns |

<!-- BENCHMARK:CI:END -->

Figures obtained through the just-in-time compiler are deliberately absent. The reported figures are those of the Native AOT build that the library ships as a sample.

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
| `LineBreaker.Enumerate(ReadOnlySpan<char>)` | Returns a `ref struct` enumerator over the break opportunities of the text. |
| `LineBreakEnumerator.GetEnumerator()` | Returns the enumerator itself, so it can be used directly in a `foreach`. |
| `LineBreakEnumerator.MoveNext()` | Advances to the next opportunity and reports whether one is available. |
| `LineBreakEnumerator.Current` | The opportunity at the current position. |

The enumerator yields one opportunity per position at which a break is allowed or required. Positions where a break is prohibited are skipped. The final opportunity is at the offset equal to the length of the text and is always mandatory.

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

---

## Limitations

- Measuring the width of text, choosing the break that fits a given line width, rendering, hyphenation and language specific dictionary segmentation are outside the scope of this library. It reports break opportunities; the choice of where to break for a particular width belongs to the layout engine.
- The complex-context class is resolved by the default rule of LB1, which assigns the combining mark or the alphabetic class by general category and inserts no break inside a run of such characters other than at spaces. A dictionary based segmentation of Thai, Lao, Khmer, Burmese or similar scripts is not performed. The resolution is a distinct internal step so that such an analyser can be added later.
- The class assignments and the rules track Unicode 17.0.0 and UAX #14 Revision 55. A different Unicode version requires regenerating the trie from the corresponding data files.
- Building from source requires the Unicode data files. Without `reference/data` the source generator cannot build the trie and the conformance test cannot execute.
- The sample application under `Uax14Net.Examples` writes to the console and therefore allocates managed memory. The zero-allocation guarantee applies to the library.

---

## Notes

- Input encoding: the scanner operates on UTF-16 and reports offsets as UTF-16 code unit indices. A surrogate pair is a single scalar value at the offset of its high surrogate, and a lone surrogate is treated as a single unit of the surrogate class.
- Determinism: the scan produces identical output across repeated runs and holds no state between calls. Because the enumerator is a `ref struct` over a caller-owned span, separate enumerations are independent and no instance is shared.
- Resolution seam: rule LB1 resolution is a single method separate from the rule engine, which keeps the non-tailorable rules intact while allowing the complex-context resolution to be replaced in a future release.
- Native AOT: the library sets `IsAotCompatible`, which enables the trim, single-file and AOT analyzers. `publish-aot.bat` publishes the sample application for `win-x64` and requires the MSVC toolset for the native linker.
- Regenerating reference data: `reference/build.sh` and `reference/build.bat` download the Unicode 17.0.0 data files, verify their version header, and write them to `reference/data`. That directory is excluded from version control.

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
