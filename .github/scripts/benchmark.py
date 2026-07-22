#!/usr/bin/env python3
import argparse
import datetime
import os
import platform
import subprocess
import sys


def measure(aot, runs):
    aot = os.path.abspath(aot)
    result = subprocess.run(
        [aot, "bench", str(runs)],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    line = None
    for candidate in result.stdout.splitlines():
        if candidate.startswith("BENCH "):
            line = candidate
            break
    if line is None:
        sys.stderr.write(result.stdout)
        raise SystemExit("benchmark output did not contain a BENCH line")
    fields = {}
    for token in line.split()[1:]:
        key, _, value = token.partition("=")
        fields[key] = value
    return fields


def cpu_name():
    system = platform.system()
    if system == "Windows":
        try:
            out = subprocess.run(
                ["wmic", "cpu", "get", "name"],
                capture_output=True,
                text=True,
                check=True,
            ).stdout.splitlines()
            for row in out[1:]:
                row = row.strip()
                if row:
                    return row
        except Exception:
            pass
    if system == "Linux":
        try:
            for row in open("/proc/cpuinfo", encoding="utf-8"):
                if row.startswith("model name"):
                    return row.split(":", 1)[1].strip()
        except Exception:
            pass
    return platform.processor() or "unknown processor"


def block_en(fields, runs, date, commit, cpu):
    mb = float(fields["throughput_mb_per_s"])
    ns = float(fields["ns_per_codepoint"])
    cps = int(fields["codepoints"])
    return (
        f"Measured by CI on a GitHub Actions `windows-latest` runner with {cpu}. "
        f"Figures are the best of {runs} runs over a {cps} code point multilingual corpus, "
        f"published with Native AOT. Recorded on {date} from commit `{commit[:7]}`.\n\n"
        "| Metric | Native AOT |\n"
        "|---|---:|\n"
        f"| Throughput | {mb:.1f} MB/s |\n"
        f"| Time per code point | {ns:.2f} ns |\n"
    )


def block_ja(fields, runs, date, commit, cpu):
    mb = float(fields["throughput_mb_per_s"])
    ns = float(fields["ns_per_codepoint"])
    cps = int(fields["codepoints"])
    return (
        f"GitHub Actions の `windows-latest` ランナー ({cpu}) で CI が計測。"
        f"NativeAOT で発行し、{cps} コードポイントの多言語コーパスに対する {runs} 回中の最良値。"
        f"{date} に コミット `{commit[:7]}` で記録。\n\n"
        "| 指標 | NativeAOT |\n"
        "|---|---:|\n"
        f"| スループット | {mb:.1f} MB/s |\n"
        f"| コードポイント毎の時間 | {ns:.2f} ns |\n"
    )


def replace_region(path, body):
    text = open(path, encoding="utf-8").read()
    begin = "<!-- BENCHMARK:CI:BEGIN -->"
    end = "<!-- BENCHMARK:CI:END -->"
    i = text.index(begin) + len(begin)
    j = text.index(end)
    updated = text[:i] + "\n\n" + body + "\n" + text[j:]
    open(path, "w", encoding="utf-8", newline="\n").write(updated)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--aot", required=True)
    parser.add_argument("--runs", default="20")
    parser.add_argument("--readme", required=True)
    parser.add_argument("--readme-ja", required=True)
    parser.add_argument("--commit", required=True)
    args = parser.parse_args()

    fields = measure(args.aot, int(args.runs))
    date = datetime.date.today().isoformat()
    cpu = cpu_name()

    replace_region(args.readme, block_en(fields, int(args.runs), date, args.commit, cpu))
    replace_region(args.readme_ja, block_ja(fields, int(args.runs), date, args.commit, cpu))
    print("updated benchmark sections")


if __name__ == "__main__":
    main()
