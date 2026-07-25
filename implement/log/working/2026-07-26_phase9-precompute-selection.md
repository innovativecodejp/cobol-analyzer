# Phase 9 事前計算：デモ対象集合の選定スナップショット

- sample: `carddemo` (pin `59cc6c2fd7ebd7ef7925cad552a01a4b8b6e4d5e`)
- 解析対象ファイル数: 31
- ランキング本数: 31
- MDI 重み: D:\dev\projects\cobol-analyzer\implement\src\backend\CobolAnalyzer.API\appsettings.json
- N（MDI 上位）: 8
- 選定本数（和集合）: 9

## MDI 上位 N

| rank | program | MDI | risk | strategy | fanIn | fanOut |
|---|---|---|---|---|---|---|
| 1 | COACTUPC | 24.3 | Low | BigBang | 0 | 1 |
| 2 | CBSTM03B | 19.5 | Low | BigBang | 1 | 0 |
| 3 | COCRDUPC | 15.3 | Low | BigBang | 0 | 0 |
| 4 | COTRN02C | 12.4 | Low | BigBang | 0 | 1 |
| 5 | COCRDLIC | 11.2 | Low | BigBang | 0 | 0 |
| 6 | COUSR00C | 9.7 | Low | BigBang | 0 | 0 |
| 7 | COTRN00C | 9.7 | Low | BigBang | 0 | 0 |
| 8 | COCRDSLC | 9.6 | Low | BigBang | 0 | 0 |

## バケット代表（CB*/CO*/CS*）

| bucket | program | 上位Nに追加 |
|---|---|---|
| CB* | CBSTM03B | 既に上位N |
| CO* | COACTUPC | 既に上位N |
| CS* | CSUTLDTC | 追加 |

## 選定集合（和集合・ランク昇順）

| rank | program | MDI | strategy |
|---|---|---|---|
| 1 | COACTUPC | 24.3 | BigBang |
| 2 | CBSTM03B | 19.5 | BigBang |
| 3 | COCRDUPC | 15.3 | BigBang |
| 4 | COTRN02C | 12.4 | BigBang |
| 5 | COCRDLIC | 11.2 | BigBang |
| 6 | COUSR00C | 9.7 | BigBang |
| 7 | COTRN00C | 9.7 | BigBang |
| 8 | COCRDSLC | 9.6 | BigBang |
| 30 | CSUTLDTC | 4.9 | Incremental |

## 全ランキング

| rank | program | MDI | risk | strategy | fanIn | fanOut |
|---|---|---|---|---|---|---|
| 1 | COACTUPC | 24.3 | Low | BigBang | 0 | 1 |
| 2 | CBSTM03B | 19.5 | Low | BigBang | 1 | 0 |
| 3 | COCRDUPC | 15.3 | Low | BigBang | 0 | 0 |
| 4 | COTRN02C | 12.4 | Low | BigBang | 0 | 1 |
| 5 | COCRDLIC | 11.2 | Low | BigBang | 0 | 0 |
| 6 | COUSR00C | 9.7 | Low | BigBang | 0 | 0 |
| 7 | COTRN00C | 9.7 | Low | BigBang | 0 | 0 |
| 8 | COCRDSLC | 9.6 | Low | BigBang | 0 | 0 |
| 9 | COACTVWC | 9.5 | Low | BigBang | 0 | 0 |
| 10 | COUSR02C | 9.4 | Low | BigBang | 0 | 0 |
| 11 | COUSR03C | 9.4 | Low | BigBang | 0 | 0 |
| 12 | COTRN01C | 9.3 | Low | BigBang | 0 | 0 |
| 13 | COBIL00C | 9.2 | Low | BigBang | 0 | 0 |
| 14 | CBACT01C | 8.8 | Low | BigBang | 0 | 2 |
| 15 | CORPT00C | 8.6 | Low | BigBang | 0 | 0 |
| 16 | CBACT03C | 8.4 | Low | BigBang | 0 | 1 |
| 17 | CBTRN03C | 8.3 | Low | BigBang | 0 | 1 |
| 18 | CBACT02C | 8.2 | Low | BigBang | 0 | 1 |
| 19 | CBCUS01C | 7.9 | Low | BigBang | 0 | 1 |
| 20 | CBACT04C | 7.7 | Low | BigBang | 0 | 1 |
| 21 | COADM01C | 7.7 | Low | BigBang | 0 | 0 |
| 22 | COUSR01C | 7.6 | Low | BigBang | 0 | 0 |
| 23 | COMEN01C | 7.5 | Low | BigBang | 0 | 0 |
| 24 | CBTRN02C | 7.3 | Low | BigBang | 0 | 1 |
| 25 | CBSTM03A | 7.2 | Low | BigBang | 0 | 2 |
| 26 | CBTRN01C | 7.1 | Low | BigBang | 0 | 1 |
| 27 | CBEXPORT | 7.0 | Low | BigBang | 0 | 1 |
| 28 | CBIMPORT | 6.0 | Low | BigBang | 0 | 1 |
| 29 | COSGN00C | 5.7 | Low | BigBang | 0 | 0 |
| 30 | CSUTLDTC | 4.9 | Low | Incremental | 2 | 1 |
| 31 | COBSWAIT | 0.5 | Low | BigBang | 0 | 0 |

## 注記（silent 変更禁止・既定値と実測差）

- **DFG 重複 FILLER 修正**: 本フェーズで `DfgBuilder.ComputeImpactClosure` を重複 Id 耐性に修正した結果、
  解析可能プログラムが 10 → 31 本に増加（`implement/docs/feedback-phase9-dfg-filler-duplicate-key.md`）。
- **MDI 分布**: Low=31（最大 MDI 24.3）。固定コーパス＋固定重みでは
  High/Critical に達するプログラムは無く、戦略は BigBang / Incremental の範囲。重みは改変しない（仕様 §8）。
- **project.json**: `programs` は空配列（size 削減）。demo C の ProjectPanel は dependencyGraph/ranking のみ参照し、
  個別 AnalyzeResult は `programs/{NAME}.json` で配布。schema は ProjectAnalyzeResult のまま。
- **AST 図**: 構造概観（Structure/Unit カテゴリ）。Element レベル（Statement/DataItem）は可読性のため省略。
- **N / 図形式 / Pages ソース**: 既定（N=8 / SVG / docs/）。
