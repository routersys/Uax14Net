# Contributing to Uax14Net

English | [日本語](CONTRIBUTING.ja.md)

Thank you for your interest in contributing to Uax14Net.

Uax14Net is a pure C# implementation of the Unicode Line Breaking Algorithm (UAX #14). It is verified against the official LineBreakTest.txt of Unicode 17.0.0, and its scan performs no managed allocation. Please read the [README](README.md) first: it states what the library guarantees and, just as importantly, what it does not.

---

## Table of Contents

1. [How to Contribute](#how-to-contribute)
2. [Getting Started](#getting-started)
3. [Issues](#issues)
4. [Pull Requests](#pull-requests)
5. [Verification](#verification)
6. [Code Conventions](#code-conventions)
7. [Commit Conventions](#commit-conventions)
8. [Releasing](#releasing)
9. [Code of Conduct](#code-of-conduct)

---

## How to Contribute

Typo fixes, documentation corrections and trivial bug fixes can go straight to a pull request.

Open an issue first for any of the following, so that the intended behavior and scope are agreed before implementation. If you are unsure which side your change falls on, open an issue.

- A larger bug fix
- A new feature
- A change to the agreement with the official LineBreakTest.txt, or to the default behavior of UAX #14
- A new public API, or a change to an existing one
- A change that touches the zero-allocation contract
- Support for a new platform, target framework or Unicode version

Report a vulnerability privately as the [security policy](SECURITY.md) describes, never in an issue.

Issues and pull requests are welcome in English or Japanese.

---

## Getting Started

### Environment

| Item | Requirement |
|---|---|
| SDK | .NET SDK 10.0 |
| Runtime | .NET 10.0, the only target framework of the library and of the tests |
| Unicode data | curl, to download the data files that the build and the tests read |
| Native AOT | The MSVC C toolset, only to publish the sample application with `publish-aot.bat` |

### Source

Fork the repository and clone your fork. Branch from `develop` for a feature, and from `main` for anything else.

### Build and test

```console
reference/build.sh
dotnet build Uax14Net.slnx -c Release
dotnet test Uax14Net.slnx -c Release
```

On Windows, run `reference\build.bat` instead of `reference/build.sh`.

The source generator reads the Unicode data files under `reference/data`, and the conformance test reads LineBreakTest.txt from the same place. That directory is not committed. The two scripts download the pinned Unicode 17.0.0 files and verify the version header of each one. Without the files, the source generator cannot build the trie and the conformance test cannot run.

The build treats warnings as errors and enforces code style. Do not work around either; fix the underlying issue.

---

## Issues

Open an issue from a template. Templates are available in English and Japanese for a bug report, a proposal, a question and a documentation problem.

For a bug report, include:

- the expected and actual behavior;
- the smallest code that reproduces it, with every option that differs from its default;
- the text as a list of code points such as U+0061 U+0020 U+4E00, and whether it was passed as UTF-16 or as UTF-8;
- the versions of Uax14Net and of the .NET runtime, the operating system and the processor architecture;
- for a wrong break, the offsets that the enumerator returned and the offsets you expected, and whether the official LineBreakTest.txt, the text of UAX #14 or another implementation agrees with you;
- any exception, with its type, message and stack trace unmodified.

Attachments to an issue are visible to everyone. Attach a text only if you have the right to redistribute it. A short sequence of code points that reproduces the problem is best.

---

## Pull Requests

A pull request that adds a feature targets `develop`. A bug fix and any other change targets `main`.

Keep each pull request to one logical change. Avoid unrelated refactoring, formatting changes and dependency updates; update a dependency only for a concrete reason, such as a required feature, a bug fix, a security fix or a compatibility requirement.

Follow the implementation pattern already established in the code you are changing. If existing implementations disagree, verify the intended behavior against the README and the tests instead of copying either one mechanically.

Take particular care when changing:

- the public API;
- the break results, and the agreement with the official LineBreakTest.txt;
- the scanner state, and anything that allocates;
- the pair table and any other fast path that skips a rule;
- the UTF-8 decoding and the byte offsets;
- the source generator and the trie that it emits;
- the disposal of the pooled buffer that the complex-context resolver uses.

If a change alters what the library guarantees or documents, update [README.md](README.md) and [README.ja.md](README.ja.md) in the same pull request.

Do not include a version bump. The developer updates the version and cuts the release.

The developer reviews the pull request and merges it with a merge commit, which keeps your commits in the history. The [commit conventions](#commit-conventions) apply to every commit in the pull request.

Contributions are accepted under the repository's MIT [LICENSE](LICENSE.txt). By opening a pull request you agree that your contribution is licensed under those terms. If a change brings in third-party code or data, say so in the pull request and give its license.

---

## Verification

Report what you actually ran, in the pull request. Include the result of the build and of the tests. A skipped test is not a passing test.

Distinguish what you observed, what you derived from reading the code, and what you assume. A defect found by reading code is a real finding; say that it has not been reproduced.

### Conformance

Do not make the agreement with the official LineBreakTest.txt worse. All 19338 lines pass, and a change that makes any of them fail needs an issue first.

### Fast paths

A fast path that skips a rule must keep every result identical. The pair table is derived from the rules and not written by hand. After a change to the rules, regenerate it as the README describes under Notes. The tests compare every table entry and the whole scan with the rules, and a recorded hash fixes the output over a pseudo-random corpus under fourteen option sets. If a change alters results on purpose, update the recorded hashes and say in the pull request why the results change.

### Allocation

The tests measure `GC.GetAllocatedBytesForCurrentThread` across the enumeration of a sample on both the UTF-16 and the UTF-8 path. Add the same measurement for a new path.

### Performance

Do not claim an improvement from a single measurement.

- Compare the baseline and the candidate repeatedly, alternating between them, on the same machine under the same conditions.
- Report enough measurements to distinguish the change from the run-to-run variation.
- Run the correctness tests and the performance measurement separately.
- Measure under the just-in-time compiler and under Native AOT, because a change can help one and hurt the other. A difference of a few percent under the just-in-time compiler has reversed between two measurement runs in this repository, so use Native AOT for a small difference.

`dotnet run --project Uax14Net.Examples -c Release -- bench` reports the throughput on your own hardware, and `publish-aot.bat` publishes the sample application for Native AOT.

---

## Code Conventions

Follow the implementation patterns and the style already used in the code you are changing, and the repository's [`.editorconfig`](.editorconfig).

Do not add comments or commented-out code. Keep the comments and documentation comments that already exist in the code you modify.

Fix the build warnings that your change introduces.

---

## Commit Conventions

- Keep each commit to the smallest practical logical unit, and make sure every commit builds on its own.
- Keep implementation changes and the tests that verify them in separate commits.
- Commit subjects in this repository are written in Japanese, are 50 characters or fewer, and end in a noun phrase, such as 〜の追加 or 〜の修正. If you cannot write the subject in Japanese, say so in the pull request.
- Do not write a message body. The exception is the lines that classify a change for the release notes. If a commit needs one, the reviewer tells you how to write it.

---

## Releasing

The developer cuts the releases. The version follows semantic versioning and is decided from the difference in the public API, not from the size of the change: removing or narrowing anything a consumer could rely on is a major release, adding public API is a minor release, and anything else is a patch release.

---

## Code of Conduct

All contributors are expected to follow the repository's [Code of Conduct](CODE_OF_CONDUCT.md).
