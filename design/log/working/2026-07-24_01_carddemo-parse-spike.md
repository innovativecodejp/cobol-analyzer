# CardDemo パース耐性スパイク & Phase 7 キックオフ

- 実行日: 2026-07-24
- 目的: 実用規模の実 COBOL に対する現行パーサの耐性を実測し、「実コードで動くデモ」（Phase 7）の前提と作業量を確定する。

## 対象

- コーパス: **AWS CardDemo**（`aws-samples/aws-mainframe-modernization-carddemo`、Apache-2.0）。クレカ基幹の実メインフレームアプリ。
- 規模: COBOL 44本＋コピーブック 62本。CICS 17/44、COPY 31/44、EXEC SQL 0、`AUTHOR.` 24/44、`DATE-WRITTEN.` 5。固定形式・連番なし（col1–6空白、col7に `*` コメント指標）。
- 測定対象: `app/cbl` の 31 プログラム。
- 手法: 使い捨てハーネスで各 `*.cbl` を `CobolParserFacade.Parse(source)` に投入し、`ParseResult.Errors` を集計。前処理は挟まない（現行 facade と同条件）。

## 測定結果

| 条件 | 通過 | エラーの主因 |
|---|---|---|
| 生ソースそのまま | **0 / 31** | L1 `mismatched input '**' expecting {ID, IDENTIFICATION}`（固定形式コメント行 col7 `*` で即死、26本） |
| 簡易な桁正規化（col7コメント除去＋8–72桁抽出のみ） | **2 / 31** | エラーが L3–6 へ前進。`mismatched input 'AWS'/'CARDDEMO'/'April' expecting <EOF>`（旧式 IDENTIFICATION 段落）、`token recognition error at '%'` 等 |

- 生: 全滅。ただし死因は文法ではなく**固定形式のコメント行**。
- 簡易正規化: 2本（COBSWAIT / CSUTLDTC）が完全通過し、残りの死因が **`AUTHOR.` / `DATE-WRITTEN.` の旧式 ID 段落**、COPY、EXEC CICS へ移動。

## 診断（詰まりの正体・重要度順）

1. **固定形式の前処理が無い**（コメント col7、連番 col1–6、73–80 無視、継続行）。現行 facade は生ソースを直接 `Cobol85Lexer`→`Cobol85Parser.startRule()` に渡している（`CobolParserFacade.cs`）。→ 最大の壁。
2. **旧式 IDENTIFICATION 段落**（`AUTHOR.` / `DATE-WRITTEN.` / 他 `INSTALLATION.`/`DATE-COMPILED.`/`SECURITY.`）を現行文法経路が受けない。24/44 が該当。
3. **COPY(31/44) / EXEC CICS(17/44)** の展開・処理が無い（完全解析には要対応）。
4. **生成済み `Cobol85Preprocessor`（`startRule()` あり）がリポジトリに存在するが未配線**（`Generated/` 以外に利用箇所なし）。ProLeap 系の前処理器は固定形式・コメント・COPY・EXEC ブロックをまとめて捌くのが定石。

## 結論

- ボトルネックは **「コア文法が実 COBOL に耐えない」ではなく「入力パイプライン（前処理）が繋がっていない」**。前者は底なしだが、後者は**境界の明確な有界作業**。→ 実コードデモは実現可能。
- 通過率の「天井」は**前処理を配線した後**に再測定して初めて出る（本スパイクは 生＋簡易正規化まで）。

## Phase 7 スコープ（デモ / サンプルコーパス）

実用規模コードで動く2段デモ:

- **B: 事前レンダリング型ギャラリー** — CardDemo を解析し、既存エクスポート（Phase 6: 注釈レポート / 移行設計書）＋図を書き出して静的に見せる。
- **C: 静的インタラクティブ型** — 解析結果を JSON で事前計算し、フロント（`src/api/*.ts` の 1 本の境界を「静的 JSON 読み込み」へ差し替え）をサーバ無しで動かす。読み取り系ビュー（AST/CFG/DFG/依存/MDI/ナビ）は無改造で動く見込み。書き換え系（コメント挿入/エクスポート）は静的では非表示 or 事前生成。

## クリティカルパス（依存順）

1. **前処理の配線**（＝共通土台・最優先）: 既存 `Cobol85Preprocessor` の活用 or 固定形式正規化層の追加＋**旧式 ID 段落の許容**＋**EXEC CICS のブロック扱い**。→ **再スパイクで通過率の天井を測定**（バッチ 14本 CB* がまず通る見込み。CICS 17本は exec 対応後）。
2. **サンプルコーパス統合**: CardDemo を `implement/samples/carddemo/` に **submodule**（Apache-2.0、ヘッダ/NOTICE 保持）＋ `samples/registry.json`（名前→パス→説明）で「サンプル名指定」で解析可能に。
3. **B（ギャラリー）**: バッチ解析結果＋既存エクスポートで静的ケーススタディを出力。
4. **C（静的インタラクティブ）**: `api/*.ts` に静的データモードを追加し、Vite 静的ビルド→ホスト。

## 次アクション

- まず **(1) 前処理配線 → 再スパイク**。ここで Phase 7 spec（`design/specs/phase7-…`）の前提が確定する。spec 化はこの再測定の後。
- デモは **バッチ先行**（CICS 待たずに実規模デモを出せる）。

## 再現メモ

- ハーネス: `CobolParserFacade.Parse` を対象ディレクトリの全 `*.cbl` に適用し、`IsSuccess` と先頭エラーを集計するだけの小さな console。前処理有無をフラグ切替。
- CardDemo は shallow clone で取得（`app/cbl`）。正式には Phase 7 で submodule 化。
