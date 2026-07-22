# Uax14Net

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](#)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](#)
[![Release](https://img.shields.io/github/v/release/routersys/Uax14Net.svg)](https://github.com/routersys/Uax14Net/releases)

[English](https://github.com/routersys/Uax14Net/blob/main/README.md) | 日本語

---

Unicode Standard Annex #14 が定める [Unicode 改行アルゴリズム](https://www.unicode.org/reports/tr14/) を、ネイティブ依存のない純粋な C# で実装したライブラリです。
入力された Unicode テキストについて、行を折り返せる位置と改行が必須となる位置を判定します。準拠対象は Unicode 17.0.0 と UAX #14 Revision 55 です。
アルゴリズムは UTF-16 または UTF-8 のテキスト上で動作し、マネージド割り当てを一切行いません。ガベージコレクターは走査を観測せず、ライブラリ全体が Native AOT に対応します。
正しさは仕様の読解では主張しません。すべての境界を公式の LineBreakTest.txt (Unicode 17.0.0) と照合します。

---

## 目次

1. [概要](#概要)
2. [要件](#要件)
3. [導入](#導入)
4. [機能](#機能)
   - [1. 改行機会と必須改行](#1-改行機会と必須改行)
   - [2. 改行クラスと解決](#2-改行クラスと解決)
   - [3. 結合文字・接合子・空白](#3-結合文字接合子空白)
   - [4. ゼロアロケーションと生成トライ](#4-ゼロアロケーションと生成トライ)
   - [5. 適合検証](#5-適合検証)
   - [6. 性能](#6-性能)
5. [API リファレンス](#api-リファレンス)
   - [列挙](#列挙)
   - [改行機会](#改行機会)
   - [クラス](#クラス)
   - [Tailoring](#tailoring)
6. [制限事項](#制限事項)
7. [注記](#注記)
8. [免責事項](#免責事項)
9. [サードパーティライセンス](#サードパーティライセンス)
10. [ライセンス](#ライセンス)

---

## 概要

Uax14Net は Unicode テキストの改行機会を判定します。行を折り返せる位置をすべて報告し、改行・復帰・改ページ・ネクストラインといった強制改行が改行を要求する位置を必須として印します。

公開面はモダンな C# です。テキストは `ReadOnlySpan<char>` として、あるいは UTF-8 の `ReadOnlySpan<byte>` として渡します。改行機会は `ref struct` の列挙子が生成するため、文書全体をヒープ負荷なしで走査できます。各改行機会はオフセットと種別を保持する `readonly struct` です。任意のコードポイントの改行クラスは静的メソッド一つで取得できます。tailoring は `readonly struct` のオプションが担い、複合文脈クラスの解決は公開された接合点です。状態を持つ走査器は公開しません。

改行クラスの全体と、規則が依存する補助プロパティは、ビルド時に二段のルックアップトライへ格納します。ソースジェネレーターが追加ファイルとして与えられた Unicode Character Database のファイルを読み、トライを静的な読み取り専用データとして出力します。分類は実行時の解析を伴わず、静的初期化のコストもありません。

Unicode データはリポジトリへ同梱しません。`reference/` 配下のハーネスが固定した Unicode 17.0.0 のデータファイルをダウンロードし、バージョンヘッダーを検証して `reference/data` へ書き出します。ソースジェネレーターはそのファイルからトライを構築し、テストは同じ場所から公式の LineBreakTest.txt を読み込んで走査器と照合します。

---

## 要件

| 項目 | 要件 |
|---|---|
| OS | .NET 10 が対応する Windows・Linux・macOS |
| SDK | .NET SDK 10.0 |
| 言語 | C# 14 以降 (`LangVersion` は `latest`) |
| unsafe コード | 利用側プロジェクトでは不要 |
| 参照データ | ビルドとテストが用いる Unicode データファイルの取得に Git または curl が必要 |

---

## 導入

```sh
dotnet add package Uax14Net
```

1. ソースからビルドする前に `reference/build.sh`、Windows では `reference/build.bat` を実行して Unicode 17.0.0 のデータを取得します。ソースジェネレーターは分類トライの構築に、テストは LineBreakTest.txt にそのファイルを必要とします。
2. `LineBreaker.Enumerate` にテキストを渡し、改行機会を列挙します。テキストは `ReadOnlySpan<char>` として渡すため、`string`・配列・スライスをコピーなしで受け取ります。
3. 各改行機会を UTF-16 オフセットと種別として読みます。allowed の機会は行を折り返せる位置、mandatory の機会は改行が必須の位置です。
4. サンプルアプリケーションの Native AOT バイナリを生成するには `publish-aot.bat` を実行します。

---

## 機能

### 1. 改行機会と必須改行

走査器は改行機会ごとに結果を一つ生成します。アルゴリズムがその位置で改行を許すか要求するとき、その位置を報告します。改行が禁止される位置は報告しないため、テキストを折り返す利用側は処理できる候補だけを列挙します。

必須改行は規則 LB4 と LB5 の強制改行から生じます。改行・復帰・ネクストライン、そして垂直タブと改ページを含む強制改行クラスがこれにあたります。復帰に改行が続く場合は、二つでなく一つの改行として扱います。テキスト終端は規則 LB3 に従い、必須の境界として一度だけ報告します。

### 2. 改行クラスと解決

すべてのコードポイントに Unicode 17.0.0 の 49 個の改行クラスのいずれかを割り当てます。規則 LB1 に従って規則適用の前に解決するクラスは、既定で次のように解決します。曖昧・サロゲート・不明は英字へ、条件付き日本語開始文字は非開始文字へ、複合文脈クラスは一般カテゴリが非結合印または結合印のとき結合印へ、それ以外では英字へ解決します。

クラスだけでは決まらない規則も完全に実装します。East Asian Width は規則 LB19a の引用符と規則 LB30 の括弧の扱いを分けます。一般カテゴリは引用符の規則 LB15a・LB15b・LB19 で始め括弧と終わり括弧を区別します。Brahmic 系のクラスは点線円を含む正書法音節の規則 LB28a を駆動します。Regional Indicator は規則 LB30a で対にし、Emoji Base と Emoji Modifier は規則 LB30b で結び、数値の規則 LB23 から LB25 は数と前置・後置・区切りをまとめます。

規則 LB1 が解決するクラスは `LineBreakOptions` で tailoring できます。厳密度は条件付き日本語開始文字が小書き仮名を密着させるか、その前で改行を許すかを選びます。曖昧幅の方針は曖昧クラスを英字と表意のどちらへ解決するかを決めます。`WordBreakMode` は break-all と keep-all を提供します。クラス上書きの委譲は任意のコードポイントの改行クラスを再割り当てします。複合文脈クラスの解決は公開された接合点で、`IComplexContextResolver` が複合文脈文字の各極大区間を受け取り、その内部の改行を報告します。これによりタイ・ラオ・クメール・ビルマの辞書分割器を差し込めます。Resolver を与えない既定解決は、一般カテゴリにより結合印か英字を割り当て、そうした区間の内部には改行を挿入しません。

### 3. 結合文字・接合子・空白

結合文字列は規則 LB9 に従い、その基底文字として扱います。基底に続く結合印または Zero Width Joiner は基底へ畳み込み、その前では改行しません。基底を持たない結合印は、テキスト先頭・空白・強制改行の後に現れるため、規則 LB10 により英字として扱います。

Zero Width Joiner は規則 LB8a によりその後の改行を禁止し、Emoji の Zero Width Joiner 列をまとめます。Word Joiner は規則 LB11 により両側の改行を禁止し、非改行クラスは規則 LB12 と LB12a が扱います。Zero Width Space は規則 LB8 により、その後と、続く空白の後に改行機会を生じます。

### 4. ゼロアロケーションと生成トライ

走査器は `ref struct` です。状態はすべてスタック上にあり、分類データは静的な読み取り専用メモリで、入力が UTF-16 でも UTF-8 でも、テキストの走査中に配列やオブジェクトを割り当てません。分類はブロックを重複排除した二段トライで、`ReadOnlySpan<byte>` として出力します。実行時はこれをアセンブリからヒープコピーなしで直接参照します。複合文脈 Resolver を与えた場合は、各区間をプール済みバッファ経由で渡します。既定経路は何も割り当てません。

マネージド割り当てが無いことは主張ではなく計測です。`GC.GetAllocatedBytesForCurrentThread` は、多言語サンプルを UTF-16 と UTF-8 の両経路で列挙する間と、Unicode 範囲の全コードポイントの分類にわたり、増分ゼロバイトを報告します。

### 5. 適合検証

走査器を公式の LineBreakTest.txt (Unicode 17.0.0) と照合します。各テスト行はコードポイント列を、先頭の前と末尾の後を含むすべての境界の改行・非改行の印とともに並べます。テストはすべての印を再現します。

| 項目 | 一致 |
|---|---|
| LineBreakTest.txt の全 19338 行 | 完全一致 |
| 先頭と末尾を含む全境界の改行・非改行 | 完全一致 |
| 追加面を跨ぐ UTF-16 オフセットとして報告する境界 | 完全一致 |

テストプロジェクトは 62 個のテストを含み、すべて合格します。適合テストだけで公式ファイルの全 19338 ケースを検査し、別のテストがコーパス全体を UTF-8 経路で再生して、そのバイトオフセットが UTF-16 オフセットと一致することを確認します。残るテストは、空文字列・単一文字・強制改行と必須改行・復帰改行の対・追加面のオフセット・Emoji の Zero Width Joiner 列・Regional Indicator の対・孤立サロゲート・全コード空間・tailoring オプション・複合文脈 Resolver の接合点・UTF-16 と UTF-8 両経路でのマネージド割り当ての不在を網羅します。

### 6. 性能

アルゴリズムは逐次走査です。各改行判定はその左のクラスと蓄積した状態に依存するため、走査を文字方向へベクトル化できません。したがってスループットは、コンパクトなトライ・積極的なインライン化・割り当ての不在から得られ、SIMD 並列からは得られません。

一つ目の表はワークステーションで取得した固定記録です。再生成しません。

Windows 11 上の 13th Gen Intel Core i7-1360P で、サンプルアプリケーションを `x86-64-v3` ベースラインの Native AOT で発行し、Latin・CJK・タイ・ヘブライ・数字・約物・引用符・Emoji・Regional Indicator を含む 630784 コードポイントの多言語コーパスに対する 30 回中の最良値です。

| 指標 | Native AOT |
|---|---:|
| スループット | 70.1 MB/s |
| コードポイント毎の時間 | 27.92 ns |

二つ目の節は性能計測ワークフローが GitHub Actions ランナー上で再生成します。ハードウェアはワークステーションと異なるため、値が特定の一台に依存しないことの独立した確認になります。

<!-- BENCHMARK:CI:BEGIN -->

性能計測ワークフローはまだこのリポジトリの計測値を公開していません。

<!-- BENCHMARK:CI:END -->

JIT コンパイラで得た値は意図的に載せません。掲載する値は、ライブラリがサンプルとして提供する Native AOT ビルドのものです。

---

## API リファレンス

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

### 列挙

| メンバー | 説明 |
|---|---|
| `LineBreaker.Enumerate(ReadOnlySpan<char>)` | UTF-16 テキストの改行機会を走査する `ref struct` 列挙子を返します。 |
| `LineBreaker.Enumerate(ReadOnlySpan<char>, in LineBreakOptions)` | tailoring オプション付きで同上。 |
| `LineBreaker.Enumerate(ReadOnlySpan<byte>)` | UTF-8 テキストを走査し、バイトオフセットを報告します。 |
| `LineBreaker.Enumerate(ReadOnlySpan<byte>, in LineBreakOptions)` | tailoring オプション付きで同上。 |
| `LineBreakEnumerator`、`Utf8LineBreakEnumerator` | `ref struct` の列挙子。`GetEnumerator`・`MoveNext`・`Current`・`Dispose` を持ちます。 |

列挙子は改行を許すか要求する位置ごとに機会を一つ返します。改行が禁止される位置は飛ばします。最後の機会はテキスト長に等しいオフセットにあり、常に必須です。UTF-16 の列挙子は UTF-16 オフセットを、UTF-8 の列挙子はバイトオフセットを報告します。不正な UTF-8 は置換文字として復号します。

### 改行機会

| メンバー | 説明 |
|---|---|
| `LineBreakOpportunity.Position` | 行を折り返せる UTF-16 オフセットです。改行はこのオフセットのコード単位の前に置きます。 |
| `LineBreakOpportunity.Kind` | `LineBreakKind.Allowed` または `LineBreakKind.Mandatory` です。 |
| `LineBreakOpportunity.IsMandatory` | 強制改行またはテキスト終端により改行が必須のとき真です。 |

`LineBreakKind` は `Allowed` または `Mandatory` です。allowed の改行は折り返しの候補で、mandatory の改行は幅にかかわらず必ず守ります。

### クラス

| メンバー | 説明 |
|---|---|
| `LineBreaker.GetLineBreakClass(int)` | Unicode スカラー値の `Line_Break` プロパティ値を返します。 |
| `LineBreakClass` | Unicode 17.0.0 の 49 個の改行クラスです。 |

`GetLineBreakClass` は Unicode Character Database に記録されたプロパティ値を、規則 LB1 の解決前の状態で返します。Unicode 範囲外のコードポイントは不明クラスへ解決します。

### Tailoring

`LineBreakOptions` は `init` アクセサーを持つ `readonly struct` です。`LineBreakOptions.Default` は適合テストが検証する素の UAX #14 既定です。

| メンバー | 既定 | 説明 |
|---|---|---|
| `Strictness` | `Strict` | `Strict` は小書き仮名を非開始文字として密着させ、`Normal` は条件付き日本語開始文字を表意へ解決し小書き仮名の前で改行を許します。 |
| `WordBreak` | `Normal` | `BreakAll` は非tailorable規則が許す位置すべてで改行を許し、`KeepAll` は表意文字同士の改行を抑止します。 |
| `AmbiguousWidth` | `Alphabetic` | 曖昧クラスを英字と表意のどちらへ解決するかを選びます。 |
| `ClassOverride` | `null` | 規則 LB1 の前にコードポイントの改行クラスを再割り当てする委譲 `int -> LineBreakClass?`。 |
| `ComplexContextResolver` | `null` | 複合文脈文字の区間を分割する `IComplexContextResolver`。 |

`IComplexContextResolver.Resolve(ReadOnlySpan<char> run, Span<bool> breakBefore)` は複合文脈文字の極大区間ごとに一度呼ばれます。`breakBefore[i]` を立てると `run[i]` の前での改行を許します。非tailorable規則が禁じる改行はエンジンが無視するため、Resolver は非適合な結果を生めません。Resolver は UTF-16 入力に適用され、UTF-8 入力では既定解決を用います。

---

## 制限事項

- 文字幅の測定、指定した行幅に収まる改行の選択、描画、ハイフネーション、言語固有の辞書分割は本ライブラリの範囲外です。本ライブラリは改行機会を報告します。特定の幅に対してどこで折り返すかの選択はレイアウトエンジンの役割です。
- 複合文脈スクリプトの辞書は同梱しません。Resolver が無ければ、タイ・ラオ・クメール・ビルマなどは空白でのみ改行します。これは UAX #14 の既定です。辞書分割器は `IComplexContextResolver` で与えられます。本ライブラリは接合点を提供し、辞書は提供しません。接合点は UTF-16 入力に適用されます。
- テキストは span 全体として処理します。別々に与えたチャンクを跨ぐ逐次的な改行判定は提供しません。複数の規則の先読みがチャンク境界を越えるためです。
- クラス割り当てと規則は Unicode 17.0.0 と UAX #14 Revision 55 に追随します。別の Unicode バージョンでは、対応するデータファイルからトライを再生成する必要があります。
- ソースからのビルドには Unicode データファイルが必要です。`reference/data` が無ければ、ソースジェネレーターはトライを構築できず、適合テストは実行できません。
- `Uax14Net.Examples` のサンプルアプリケーションはコンソールへ書き込むため、マネージドメモリを割り当てます。ゼロアロケーションの保証はライブラリに適用します。

---

## 注記

- 入力エンコーディング: UTF-16 入力は UTF-16 コード単位のオフセットを、UTF-8 入力はバイトオフセットを報告します。サロゲート対は高位サロゲートのオフセットにある単一のスカラー値で、孤立サロゲートはサロゲートクラスの単一の単位として扱い、不正な UTF-8 列は一つの置換文字として復号します。
- 決定性: 走査は繰り返し実行しても同一の出力を生成し、呼び出し間で状態を保持しません。列挙子は呼び出し側が所有するスパン上の `ref struct` のため、別々の列挙は独立し、インスタンスを共有しません。
- 解決の接合点: 規則 LB1 の解決は規則エンジンから分離しており、tailoring オプションと複合文脈 Resolver は非tailorable規則へ触れずにクラス割り当てと区間内部の改行を変えます。
- Native AOT: ライブラリは `IsAotCompatible` を設定し、trim・単一ファイル・AOT の各アナライザーを有効にします。`publish-aot.bat` はサンプルアプリケーションを `win-x64` 向けに発行し、ネイティブリンカーに MSVC ツールセットを必要とします。
- 参照データの再生成: `reference/build.sh` と `reference/build.bat` は Unicode 17.0.0 のデータファイルをダウンロードし、バージョンヘッダーを検証して `reference/data` へ書き出します。このディレクトリはバージョン管理から除外します。

---

## 免責事項

本ライブラリは MIT License で公開します。

本ソフトウェアは現状のまま提供し、商品性・特定目的への適合性・非侵害の保証を含め、明示または黙示を問わずいかなる保証も行いません。

本ライブラリの使用または使用不能から生じるいかなる損害についても、作者は責任を負いません。利用は自己責任で行ってください。

---

## サードパーティライセンス

Uax14Net は Unicode Character Database から構築します。ライセンス全文はリポジトリの [`.github/LICENSE/UnicodeDataFiles.txt`](https://github.com/routersys/Uax14Net/blob/main/.github/LICENSE/UnicodeDataFiles.txt) に格納します。

Unicode データファイルは Unicode License V3 で配布します。著作権表示と許諾表示がデータに伴う限り、使用・改変・再配布を許します。当該ファイルはその本文を無変更で保持します。Unicode データファイルはリポジトリへ同梱せず、参照ハーネスが必要に応じてデータをダウンロードします。

| ソフトウェア | 目的 | ライセンス | 著作権 |
|---|---|---|---|
| [Unicode Character Database](https://www.unicode.org/ucd/) | 改行クラスと補助プロパティ、および検証に用いる LineBreakTest.txt の出所 | Unicode License V3 | Copyright © 1991-2026 Unicode, Inc. |

---

## ライセンス

[MIT License](https://github.com/routersys/Uax14Net/blob/main/LICENSE.txt)
