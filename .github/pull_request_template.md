<!--
English first, 日本語はその下。Fill in one language or both.
Delete every section that does not apply to this change. A short, accurate description is worth
more than a completed form.
英語が先、日本語はその下です。どちらか一方でも両方でも構いません。
当てはまらない節は消してください。埋めきった書式より、短くても正確な説明のほうが価値があります。
-->

## Summary / 概要

<!--
What changed, and why the change is necessary. / 何をどう変えたか、なぜその変更が必要か。
-->

## Linked issue / 関連するIssue

<!--
A larger bug fix, a new feature, a change to the agreement with the official LineBreakTest.txt or to the
default behavior of UAX #14, a public API change, a change that touches the zero-allocation contract and
support for a new platform or a new Unicode version need an issue first, so that the intended behavior and
scope are agreed before implementation. Typo fixes, documentation corrections and trivial bug fixes may go
straight to a pull request; write "not required" in that case.
大きめの不具合の修正、新しい機能、公式のLineBreakTest.txtとの一致やUAX #14の既定の挙動を変える変更、
公開APIの変更、無確保の契約に触れる変更、新しいプラットフォームやUnicodeの新しい版への対応は、実装の前に
意図する挙動と範囲を合意するため、先にIssueが必要です。誤字の修正、文書の修正、軽微な不具合の修正は、
そのままプルリクエストで構いません。その場合は「不要」と書いてください。
-->

Closes #

## Kind / 種別

<!-- Keep the ones that apply. / 当てはまるものを残してください。 -->

- Bug fix / 不具合の修正
- New public API / 公開APIの追加
- Change to the line breaking results / 改行の結果の変更
- Behavior change with no API change / API変更を伴わない挙動の変更
- Performance / 性能
- Tests / テスト
- Documentation / 文書
- Build, packaging or CI / ビルド、パッケージ、CI

## Behavior change / 挙動の変更

<!--
What an existing caller would observe differently, including exception types and break positions.
Write "none" if nothing observable changes.
既存の呼び出し側から見て何が変わるか。例外の種別や、改行の位置の変化も含みます。
観測できる変化が無ければ「なし」と書いてください。
-->

## Verification / 検証

<!--
Paste the results you actually ran. Distinguish what you observed, what you derived from reading the
code, and what you assume. Say plainly if you could not run something, and why.
実際に走らせた結果を貼ってください。実際に動かして観察したこと、コードを読んで判断したこと、
推測したことを分けて書いてください。走らせられなかったものは、その理由とともに正直に書いてください。
-->

```console
dotnet build Uax14Net.slnx -c Release
dotnet test Uax14Net.slnx -c Release
```

<!--
Results. / 結果。
-->

Unicode data / Unicode データ: downloaded with `reference/build.sh` / `reference\build.bat`

## Checklist / 確認

- [ ] The pull request is one logical change, with no unrelated refactoring or formatting. / 一つの論理的な変更にまとまっており、無関係な整理や整形を含まない。
- [ ] The pull request targets `develop` for a feature and `main` for anything else. / 機能の追加は `develop` へ、それ以外は `main` へ送った。
- [ ] Every commit builds on its own. / 各コミットが単独でビルドできる。
- [ ] Implementation changes and their verification tests are separate commits. / 実装の変更と検証のテストを別のコミットに分けた。
- [ ] Commit subjects are in Japanese and 50 characters or fewer. A commit body is written only for the lines that classify a change for the release notes, when the reviewer asked for them. If something differs, I said why under the notes for the reviewer. / コミットの件名は日本語で、50文字以内である。コミットの本文は、リリースノートでの変更の分類を指定する行を求められたときだけ書いた。異なる点があれば、その理由を「レビューで見てほしい点」に書いた。
- [ ] The change follows the implementation pattern already established where it was made. / 変更は、その部分で既に確立された実装パターンに従っている。
- [ ] No comments or commented-out code were added, and existing comments were kept. / コメントもコメントアウトしたコードも追加しておらず、既存のコメントを維持した。
- [ ] No build warnings were introduced. / ビルドの警告を増やしていない。
- [ ] No unrelated dependency was updated. / 無関係な依存関係を更新していない。
- [ ] No version bump is included. / バージョンを上げる変更を含めていない。
- [ ] If the change alters what the library guarantees or documents, README.md and README.ja.md were updated. / ライブラリが保証することや文書に書いたことを変えた場合、README.md と README.ja.md を直した。

## If this touches a guarded area / 慎重を要する箇所に触れる場合

<!--
The public API, the line breaking results, the scanner state and anything that allocates, the pair table
and the other fast paths, the UTF-8 decoding and offsets, disposal and failure handling. Delete this
section if the change touches none of them.
公開API、改行の結果、走査器の状態と確保を伴う処理、判定表とそのほかの高速経路、UTF-8のデコードと
オフセット、破棄と失敗処理。いずれにも触れない変更では、この節ごと消してください。
-->

- [ ] The agreement with the official LineBreakTest.txt is not worse: every line still passes, or an issue agreed the change. / 公式のLineBreakTest.txtとの一致を悪くしていない。すべての行が通るか、変更をIssueで合意した。
- [ ] The scan still performs no managed allocation on the UTF-16 path and on the UTF-8 path, and the allocation tests pass. / 走査が、UTF-16の経路でもUTF-8の経路でも、引き続きマネージドメモリを確保せず、確保のテストが通る。
- [ ] A fast path that skips a computation keeps every result identical, and a test compares it with the rules. / 計算を省く高速経路は、すべての結果を同一に保ち、規則と比べるテストがある。
- [ ] If the rules changed, the pair table was regenerated as the contributing guide describes. / 規則を変えた場合、貢献の手引きに書いた手順で判定表を再生成した。
- [ ] For a change to the UTF-8 path, the byte offsets agree with the UTF-16 offsets. / UTF-8の経路を変えた場合、バイトオフセットがUTF-16のオフセットと一致する。

## If this claims a performance improvement / 性能の改善を主張する場合

<!--
Delete this section otherwise. / それ以外の場合は、この節ごと消してください。
-->

- [ ] The baseline and the candidate were compared repeatedly, alternating between them, on the same machine under the same conditions. / 同じ機材、同じ条件で、変更前と変更後を交互に、繰り返し比較した。
- [ ] Enough measurements are reported to distinguish the change from normal run-to-run variation. / 通常の実行ごとのばらつきと区別できるだけの測定値を示した。
- [ ] Correctness tests and performance measurement were run separately. / 正しさのテストと性能の測定を別々に実行した。

<!--
Measurements. / 測定値。
-->

## Notes for the reviewer / レビューで見てほしい点

<!--
Anything you are unsure about, a decision you would like challenged, or something you deliberately
left out and why.
判断に迷った点、異論が欲しい判断、意図して外した部分とその理由。
-->
