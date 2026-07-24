# Phase 7 前処理層 実装 & CardDemo 再スパイク

- 実行日: 2026-07-25
- 対象仕様: `../../design/specs/phase7-preprocessing.md`
- 前提スパイク: `../../design/log/working/2026-07-24_01_carddemo-parse-spike.md`

## 実装したもの

1. **`CobolPreprocessor`**（`src/backend/CobolAnalyzer.Parser/CobolPreprocessor.cs`）— 仕様 §3 の前処理パイプライン。
   - §3.1 固定形式正規化（連番欄除外・col7 指標判定・col8–72 採用・col73–80 無視・継続行連結・コメント除去）
   - §3.2 旧式 IDENTIFICATION 段落除去（AUTHOR./INSTALLATION./DATE-WRITTEN./DATE-COMPILED./SECURITY. と自由記述行）
   - §3.3 COPY テキスト展開（検索パス解決・一段必須・入れ子は深さ上限＋循環検出・未解決/REPLACING は警告＋無害化）
   - §3.4 EXEC CICS / EXEC SQL ブロックの `CONTINUE` 縮約
   - 各段は独立した純粋関数。行番号対応は best-effort で保持（除去行は空スロット化）。
2. **警告モデル**（仕様 §6）— `ParseResult.Warnings`（`List<ParseWarning>`）を追加。`IsSuccess` は従来どおり `Errors.Count == 0` で不変。既存 API 契約は非破壊。
3. **Facade 配線**（仕様 §4）— `CobolParserFacade.Parse` 先頭で前処理を適用。`CobolPreprocessorOptions`（コピーブック検索パス・拡張子候補・深さ上限）を注入可能。既定コンストラクタは従来どおり動作（COPY は未解決警告扱い）。
4. **単体テスト** — `tests/CobolAnalyzer.Parser.Tests/PreprocessorTests.cs`（16 本）。フィクスチャは実ファイル（`TestData/preprocess/`）。
5. **測定ハーネス** — `tools/CardDemoSpike/`（`dotnet run --project tools/CardDemoSpike -- <cblDir> <cpyDir> [out.md]`）。CardDemo 本体はリポジトリ非同梱（測定時に取得。submodule 化は次フェーズ）。

## テスト結果（`dotnet build` / `dotnet test`）

- ビルド成功、0 エラー。
- 全テスト green: **Parser 28 / Engine 56 / API 3 = 87 pass, 0 fail**。
- 既存自由形式データ（`hello.cbl` / `data-sample.cbl` / `goto-sample.cbl`）は前処理経由でも `IsSuccess = true`（§7-2 充足）。

## CardDemo 再スパイク結果（前処理配線後）

対象: AWS CardDemo `app/cbl`（31本）、コピーブック検索パス `app/cpy`。

| バケット | pass / total | 前処理前スパイク（生/簡易） |
|---|---|---|
| Batch(CB*)  | **10 / 12** | — |
| CICS(CO*)   | **17 / 18** | — |
| Other(CS*)  | 0 / 1 | — |
| **合計**    | **27 / 31** | 0/31（生）→ 2/31（簡易正規化） |

→ 前処理配線により **2/31 → 27/31** へ改善。CICS(CO*) は §3.4 の EXEC 縮約で 17/18 が構造解析可能に（EXEC ブロックは 5–18 件/本を縮約、COPY は未解決 3 件/本を警告処理）。

### 失敗 4 本の原因バケット

| ファイル | 分類 | 原因 | 位置づけ |
|---|---|---|---|
| CBACT04C.cbl | Batch | `STRING PARM-DATE, …` の**区切りカンマ**を文法が受けない | 文法限界。§3 スコープ外（下記） |
| CSUTLDTC.cbl | Other(CS*) | `CALL … USING WS-DATE-TO-TEST, …` の**区切りカンマ** | 同上 |
| CBSTM03A.CBL | Batch | HTML を含む **VALUE リテラルの行継続**（col7 `-`）。継続時の再開クォート結合が未対応 | 仕様 §9「文字列リテラル継続は best-effort」の既知制約 |
| COACTUPC.cbl | CICS | `COPY … REPLACING`（本ファイルのみ）＋高複雑度 | 仕様 §9「COPY REPLACING 非対応」の既知制約 |

- カンマ区切りの実証: 該当 2 本はカンマを空白へ置換すると **pass**（`CALL/STRING` の区切りカンマが唯一の詰まり）。ただしカンマは `PICTURE` 挿入文字やリテラル内（例 `Segoe UI,sans-serif`）にも現れるため、無条件置換は不可。安全な正規化はリテラル/PIC を避ける必要があり、**設計判断が要る**（`implement/docs/` にフィードバック記録）。

## 受け入れ基準（§7）評価

- [x] `dotnet build` / 既存 `dotnet test` グリーン。
- [x] 自由形式テストデータが前処理経由でも `IsSuccess = true`。
- [△] バッチ CB* の大半が pass → **10/12（83%）**。「大半」は満たすが、仕様が想定した絶対数（≥12、前提 14本）には未達。CB* の実本数は **12**（仕様の 14 と不一致）。
- [x] CICS 改善を測定・記録 → **17/18**。
- [x] 再現可能な測定ハーネスを `tools/` に用意。

## 申し送り（次フェーズ / design への確認事項）

`implement/docs/feedback-2026-07-25-carddemo-respike-gaps.md` に記録。要点:
1. **区切りカンマの扱い**: 前処理で区切りカンマ（`, ` / 行末 `,`）を空白正規化するか、既知制約とするか。リテラル・PIC を避ける実装が必要。→ 対応すれば CBACT04C（仕様が挙げた代表バッチ）が pass、batch 11/12。
2. **CB* 本数の不一致**: 仕様 §7 は「CB* 14本」だが取得した CardDemo は **12本**。目標記述（≥12/14）の見直し要。
3. **リテラル行継続**（CBSTM03A）: §9 best-effort の範囲。厳密対応は将来フェーズ。
