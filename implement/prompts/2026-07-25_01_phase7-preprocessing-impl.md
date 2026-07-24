# Phase 7（前処理）実装プロンプト：実コード対応の前処理層

仕様: `../design/specs/phase7-preprocessing.md`（実装前に必ず全文を読むこと）
参考: `../design/log/working/2026-07-24_01_carddemo-parse-spike.md`（詰まりの実測）

---

## 前提確認

1. 上記仕様を読み、§3（前処理要件）・§4（配線点）・§7（受け入れ基準）を把握する。
2. `implement/src/backend/CobolAnalyzer.Parser/` の現状（`CobolParserFacade.cs`, `Generated/`）と、既存テスト（`tests/CobolAnalyzer.Parser.Tests/`）を確認する。
3. 不明点・仕様矛盾があれば実装を止めてユーザーに確認する（`design/specs/` を自分で変更しない）。

---

## 実装タスク一覧（順に実施。各完了後に次へ）

### タスク 1：前処理器 `CobolPreprocessor` の実装（仕様 §3）

`CobolAnalyzer.Parser/CobolPreprocessor.cs` を新規作成。入力＝生ソース、出力＝正規化済みソース（＋警告）。

- **§3.1 固定形式正規化**: 行ごとに col1–6 除外、col7 判定（`*`/`/`→コメント除去、`-`→継続連結、空白→通常）、col8–72 採用、col73 以降無視。行長 ≤7・空行を安全に処理。
- **§3.2 旧式 ID 段落除去**: `AUTHOR.` `INSTALLATION.` `DATE-WRITTEN.` `DATE-COMPILED.` `SECURITY.` の段落見出しと、その自由記述行を次の段落/DIVISION/SECTION まで除去。
- **§3.3 COPY 展開**: `COPY <member>.`（大小文字非依存）を検出し、検索パスから `<member>.cpy`/`.CPY` を解決してテキスト置換（置換内容も §3.1 正規化）。一段必須、入れ子は深さ上限＋循環検出で best-effort。未解決は警告＋当該行を無害化。`COPY ... REPLACING` は非対応＝警告＋無害化。
- **§3.4 EXEC ブロック**: `EXEC CICS ... END-EXEC` / `EXEC SQL ... END-EXEC`（行跨り・`.`有無）を検出し、no-op 文（例 `CONTINUE`）へ縮約。縮約した事実を警告/メタに記録。

> 実装の指針: §3.1→§3.4 を段階的に適用するパイプラインとし、各段を単体テスト可能な純粋関数に分ける。行番号対応は可能な範囲で保持（エラー位置報告のため。困難なら警告に原本行の目安を残す）。

### タスク 2：警告モデル（仕様 §6）

- 非致命的事象（未解決 COPY / REPLACING / exec 縮約）を表す **警告**を導入する。
- 既存 API 契約を壊さないこと。推奨：`ParseResult` に `List<ParseWarning> Warnings { get; init; } = new();` を追加（`IsSuccess` は従来どおり `Errors.Count == 0`）。`ParseWarning(int Line, string Kind, string Message)` 相当。
- 破壊的変更になる設計を採る場合は実装を止めてユーザーに確認する。

### タスク 3：Facade への配線（仕様 §4）

- `CobolParserFacade.Parse(string source)` の先頭で `CobolPreprocessor` を適用し、正規化済みソースを lexer へ渡す。
- コピーブック検索パス・拡張子候補を Facade もしくは前処理器に**注入可能**にする（コンストラクタ引数 or オプション）。既定（未指定）でも動作し、その場合 COPY は未解決警告扱い。
- `CobolSourceParser`（DI 登録）とプロジェクト解析経路が、配線後も正しく動くことを確認。

### タスク 4：単体テスト（仕様 §8）

`tests/CobolAnalyzer.Parser.Tests/` に前処理器テストを追加：
- 固定形式（連番あり/なし・col7 コメント・継続・73–80 切り捨て）
- 旧式 ID 段落除去
- COPY 展開（解決/未解決/一段/入れ子上限/循環）
- EXEC 縮約
- テストデータは実ファイル読み込み（ハードコード禁止）。

### タスク 5：CardDemo 測定の正式化 & 再スパイク（仕様 §7-3,4）

- 測定対象を取得：AWS CardDemo（`aws-samples/aws-mainframe-modernization-carddemo`, Apache-2.0）の `app/cbl`（31本）と `app/cpy`（コピーブック）。※本フェーズは測定用に取得すればよい（`implement/samples/` への submodule 正式化は次フェーズ）。
- 再現可能な測定（テスト or `implement/tools/` の小スクリプト）を用意し、`app/cbl` 全 `*.cbl` を `CobolParserFacade.Parse`（コピーブック検索パス = CardDemo `app/cpy`）に通し、**pass/fail 一覧＋エラーバケット**を出力する。
- 結果を `implement/log/working/` に記録する（バッチ CB* の通過数、CICS CO* の改善）。

### タスク 6：完了確認（仕様 §7）

```
dotnet build
dotnet test
```
- 既存テスト（parser/AST）が**グリーン**。
- 既存自由形式データ（`hello.cbl`/`data-sample.cbl`/`goto-sample.cbl`）が前処理経由でも `IsSuccess = true`。
- CardDemo 再スパイク：**バッチ CB* の大半（目標 ≥ 12/14）が pass**。CICS 系は改善を記録。
- 目標未達・想定外の詰まりが出たら、原因（バケット）を記録して停止しユーザーに報告。

---

## 仕様との矛盾発見時の対処

1. `implement/docs/` にフィードバックを記録する。
2. 実装を停止してユーザーに報告する。
3. `design/specs/` を自分で変更しない（ユーザーの指示を待つ）。

---

## 完了後の申し送り

- 再スパイクの pass 率（バッチ/CICS）を要約し、次フェーズ（デモ B/C・CardDemo submodule 化・samples レジストリ）の spec 化に渡す。
