# 2026-05-08_01 Phase 1〜6 仕様書 整合性レビュー

## 作業概要

Phase 1〜6 の全仕様書を横断的に精査し、矛盾点と追加推奨事項を洗い出した。

---

## 実施内容

`design/specs/phase1-antlr-parser.md`〜`phase6-export.md` を全文精読し、
以下の観点でチェックを実施した。

- 同一クラス・インターフェースのC#定義とTypeScript定義の一致
- §本文の記述とコードブロックの一致
- 完了基準と実装注意事項の一致
- 上流フェーズの定義を下流フェーズが正しく参照しているか
- フロントエンドが必要とするAPIフィールドが実際にAPIに含まれているか

詳細は `design/brainstorm/spec-consistency-review.md` に記録した。

---

## 発見事項サマリー

### 矛盾点（8件）

| # | 影響フェーズ | 内容 | 優先度 |
|---|------------|------|--------|
| C1 | Phase 2/3 | `AnalyzeResult.Ast` がC#で非nullable・TSでnullable | 高 |
| C2 | Phase 2/3 | `CfgEdge.IsRecursive` が §4.5 で言及されているがクラス定義にない | 高 |
| C3 | Phase 2/3 | `MetricsResult.CcPerParagraph` が §12.3 で必要とされるがクラス定義にない | 中 |
| C4 | Phase 1/2 | `DataItemNode.IsGroup` の実装式が Phase 1 と Phase 2 で異なる | 高 |
| C5 | Phase 3/4 | TypeScript `CfgBlock` に `location` プロパティがなく Phase 4 N3 が実装不可 | 高 |
| C6 | Phase 5 | 完了基準 §10.9「再分析が走る」vs 注意事項 §11.5「手動再分析」の矛盾 | 中 |
| C7 | Phase 2 | API レスポンス例に `impactClosure` フィールドが未掲載 | 低 |
| C8 | Phase 3 | CORS のコード例（§7）と設計方針（§12.5）の不一致 | 低 |

### 追加推奨（6件）

| # | 影響フェーズ | 内容 | 優先度 |
|---|------------|------|--------|
| A1 | Phase 1 | `StatementType = "CALL"` の明示定義が欠落（Phase 6 が前提とする） | 高 |
| A2 | Phase 2/6 | CALL 文の呼び出し先リテラル取得方法が未定義（Operands 型との不整合） | 高 |
| A3 | Phase 2/3/4 | AstNode の ID フィールド戦略を仕様に明記 | 中 |
| A4 | Phase 3 | TypeScript `CfgBlock.statements` を `unknown[]` から具体型に変更（N3必要） | 高 |
| A5 | Phase 6 | ランキング同点ルールの根拠を §4.4 に注記 | 低 |
| A6 | Phase 5 | フロントエンドの SelectionStore 参照記述を正確化 | 低 |

---

## 実施した更新

| フェーズ | 旧Ver | 新Ver | 適用修正 |
|---------|-------|-------|---------|
| Phase 1 | 1.1 | 1.2 | A3(AstNode.Id追加), C4(IsGroup統一), A1(CALL StatementType追加) |
| Phase 2 | 1.1 | 1.2 | A2(CallTarget追加), C2(CfgEdge.IsRecursive追加), C3(CcPerParagraph追加), C1(Ast nullable化), C7(APIレスポンス例修正), §12.3注記更新 |
| Phase 3 | 1.1 | 1.2 | A3(AstNode.id追加), C5+A4(CfgBlock型修正・CfgStatement定義), C2(isRecursive追加), C3(ccPerParagraph追加), C8(CORS条件付き設定) |
| Phase 4 | 1.0 | 変更なし | §12.2注意事項のみ更新（バージョンアップ対象外）|
| Phase 5 | 1.0 | 1.1 | C6(完了基準#9修正), A6(SelectionStore参照修正) |
| Phase 6 | 1.0 | 変更なし | A5は影響軽微のため据え置き |

Phase 4 の §12.2 注意事項は内容を修正したが仕様上の変更ではないためバージョンは据え置き。
A5（Phase 6 §4.4 注記）は低優先度のため今回対応を見送り。

---

## 成果物

| ファイル | 状態 |
|---------|------|
| `design/brainstorm/spec-consistency-review.md` | 作成済み（gitignore・詳細版） |
| `design/log/working/2026-05-08_01_consistency-review.md` | 本ファイル |
