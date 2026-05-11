# 2026-05-12_01 Phase 6 モデル配置フィードバック対応

## 作業概要

implement 側で Phase 6 仕様との乖離が発見されたため、
`implement/docs/feedback-phase6-model-placement.md` をもとに design 側仕様を更新した。

主な論点は、`ProjectAnalyzeResult` を Core 層に置くと
既存の `AnalyzeResult`（Engine 層）参照によって `Core -> Engine` の逆依存が発生し、
`Engine -> Core` と循環する点である。

---

## 影響判定

Phase 6 更新前に Phase 1〜5 への影響を確認した。

| フェーズ | 判定 | 理由 |
|---------|------|------|
| Phase 1 | 修正不要 | パーサー基盤・AST 基本構造への影響なし |
| Phase 2 | 修正必要 | `AnalyzeResult` の配置記述が Core 層になっており、実装および Phase 6 の依存方向と不整合 |
| Phase 3 | 修正不要 | フロントエンド型・可視化仕様への追加影響なし |
| Phase 4 | 修正不要 | ナビゲーション仕様への追加影響なし |
| Phase 5 | 修正不要 | コメント機能・エクスポート入力への追加影響なし |

---

## 実施内容

### specs/phase2-engine.md

- バージョンを `1.5` に更新
- `AnalyzeResult` の配置を `CobolAnalyzer.Engine/AnalyzeResult.cs` と明確化
- `CobolAnalyzer.Core/Models/AnalyzeResult.cs` の構造記述を削除
- 見出しを `AnalyzeResult（Engine層）` に変更

### specs/phase6-export.md

- バージョンを `1.2` に更新
- `ProjectAnalyzeResult` を `CobolAnalyzer.Engine/Project/ProjectAnalyzeResult.cs` に配置
- Core 層は Engine 型に依存しない request/input DTO のみに限定
- `Core -> Engine` 参照を作らない依存方向を明記
- 50件超過は `ProjectController` の validation で `400 Bad Request` とする方針に変更
- `CallGraphBuilder` の `Build_ExceedsMaxNodes_ReturnsError` テスト要件を削除
- `ProjectControllerTests` に 50件超過 validation のテスト要件を追加
- `ParagraphCount` の算出根拠を AST の `NodeType == "Paragraph"` かつ `Category == Unit` と明記
- 完了基準に 51ファイル送信時の API validation を追加

### roadmaps/roadmap.md

- Phase 2 を `v1.5 / 2026-05-12` に更新
- Phase 6 を `v1.2 / 2026-05-12` に更新
- 更新日コメントを Phase 6 実装フィードバック対応に変更

---

## 検証

以下を確認した。

- `git diff --check` が通ること
- `Core/Models/ProjectAnalyzeResult` の旧記述が残っていないこと
- `Build_ExceedsMaxNodes_ReturnsError` の旧テスト要件が残っていないこと
- `AnalyzeResult（Core` の旧見出しが残っていないこと

---

## コミット

仕様更新は以下のコミットとして push 済み。

| コミット | 内容 |
|---------|------|
| `29bf82f` | `docs: align phase6 model placement` |

push 先: `origin/master`

---

## 残事項

`implement/docs/feedback-phase6-model-placement.md` は参照元フィードバックとして未追跡のまま残した。
design 側の仕様更新対象には含めていない。
