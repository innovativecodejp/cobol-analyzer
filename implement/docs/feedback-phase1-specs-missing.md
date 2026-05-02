# フィードバック: Phase 1 実装開始不可 — design/specs/ が未作成

**日時**: 2026-05-03  
**状態**: 実装停止・確認待ち

## 問題

Phase 1（環境構築・ANTLRパーサー）の実装を開始しようとしたが、
`design/specs/` に仕様ファイルが存在しないため実装できない。

`implement/CLAUDE.md` に従い、以下のファイルを参照する必要があるが、すべて未作成：

- `design/specs/architecture.md` — 全体アーキテクチャ設計
- `design/specs/ast-spec.md` — AST設計仕様
- `design/specs/mdi-spec.md` — MDI（移行困難度指数）定義
- `design/specs/ui-spec.md` — UI仕様
- `design/specs/feature-comment.md` — コメント機能仕様

## Phase 1 に必要な仕様の観点

design 側で仕様を確定する際に、以下の観点が必要：

### architecture.md に必要な内容
- バックエンド構成（ASP.NET Core のプロジェクト構造、API設計方針）
- フロントエンド構成（TypeScript + D3.js + Monaco Editor のプロジェクト構造）
- ANTLRグラマーの配置と管理方針（`grammars-v4` リポジトリの利用方法）
- COBOL方言の対応範囲（どの方言を対象とするか）

### ast-spec.md に必要な内容（Phase 1 に直接関係）
- ANTLRパーサーが生成するパースツリーからどのようなASTノードを作るか
- ノードの種類と構造（`design/docs/CobolStructureAnalysis.md` の理論との対応）
- C# での AST モデルクラス設計の方針

### Phase 1 スコープの明確化
- Phase 1 で実装する範囲（どこまで作ればPhase 1完了か）
- 受け入れ基準（テスト方針、サンプルCOBOLコードのパース成功基準）

## 参考情報

`design/docs/CobolStructureAnalysis.md` に包括的な理論フレームワーク（AST・IR・CFG・DFG）が存在する。
この内容を基に `design/specs/` に実装可能な仕様を落とし込むことが必要。

## 依頼内容

design サブプロジェクトの Claude Code を起動して、上記を参照のうえ
最低限 `design/specs/architecture.md` と `design/specs/ast-spec.md` を作成してください。
Phase 1 の受け入れ基準も合わせて明記いただけると実装をスムーズに進められます。
