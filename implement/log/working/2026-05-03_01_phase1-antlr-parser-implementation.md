# Phase 1: ANTLR-based COBOL Parser Implementation

**日付**: 2026-05-03  
**担当**: iCode33667 + Claude Sonnet 4.6

---

## 作業概要

Phase 1（環境構築・ANTLRパーサー）の実装を完了した。

---

## 実施内容

### 1. 仕様準備

- `implement/docs/feedback-phase1-specs-missing.md` を作成し、`design/specs/` が未作成であることを記録
- design 側で `design/specs/phase1-antlr-parser.md` を作成、Phase 1 の実装仕様・受け入れ基準が確定

### 2. プロジェクト構成

`src/backend/CobolAnalyzer.sln` にて以下3プロジェクトを構成:

| プロジェクト | 役割 |
|---|---|
| `CobolAnalyzer.Core` | AST ノード定義・ParseResult モデル |
| `CobolAnalyzer.Parser` | ANTLR4 パーサー・AstBuilder |
| `CobolAnalyzer.API` | ASP.NET Core REST API |

### 3. ANTLR4 グラマー

- `grammars-v4` リポジトリから `Cobol85.g4` / `Cobol85Preprocessor.g4` を取得
- ANTLR4 ツール（`antlr-4.13.1-complete.jar`）で C# コードを生成
- 生成済みコードを `CobolAnalyzer.Parser/Generated/` に配置

### 4. AST ノード設計

`CobolAnalyzer.Core/Ast/` に以下を実装:

- `AstNode` — 基底クラス（NodeType, Category, Location, Children）
- `ProgramNode` — プログラム全体のルート
- `DivisionNode` — IDENTIFICATION / ENVIRONMENT / DATA / PROCEDURE DIVISION
- `SectionNode` — WORKING-STORAGE SECTION 等
- `ParagraphNode` — 段落
- `StatementNode` — 各 COBOL 文（StatementType, PerformFrom/Thru）
- `DataItemNode` — データ項目（LevelNumber, Picture, RedefinesTarget, IsGroup）
- `NodeCategory` — Structure / Unit / Element

### 5. パーサー実装

- `CobolParserFacade` — ANTLR4 パーサーのラッパー、`Parse(string source)` で `ParseResult` を返す
- `AstBuilder` — ANTLR4 リスナーパターンで ParseTree → 型付き AST に変換

### 6. REST API

- `POST /api/parse` — COBOL ソースを受け取り AST JSON を返す
- Swagger UI を `/swagger` で提供
- 起動ポート: `http://localhost:5157`

### 7. テスト

`tests/CobolAnalyzer.Parser.Tests/` にて xUnit テスト 12 件を実装・全件パス:

| テストクラス | テスト数 |
|---|---|
| `ParserTests` | 4 |
| `AstBuilderTests` | 8 |

テストデータ: `hello.cbl` / `goto-sample.cbl` / `data-sample.cbl` / `syntax-error.cbl`

---

## 完了確認

| 確認項目 | 結果 |
|---|---|
| `dotnet build` | 0 エラー |
| `dotnet test` | 12/12 パス |
| `POST /api/parse` | 正常レスポンス（AST JSON） |

---

## コミット

| ハッシュ | 内容 |
|---|---|
| `6cee131` | feat(phase1): implement ANTLR-based COBOL parser with AST and REST API |
| `b56795d` | docs: add Phase 1 impl prompt and feedback doc; update CLAUDE.md policy |

---

## 次フェーズへの申し送り

- Phase 2（MDI算出）の仕様は `design/specs/mdi-spec.md` を参照して実装開始
- Phase 3（UI）は `design/specs/ui-spec.md` を参照
- `docs/feedback-phase1-specs-missing.md` は解決済み（仕様確定・実装完了）
