# 2026-05-23_01 Phase 3 AST 折りたたみフィードバック対応

## 作業概要

implement 側で AST 折りたたみ仕様と現行実装の不整合が報告されたため、
`implement/docs/feedback-2026-05-23-ast-collapse-spec-mismatch.md` をもとに
design 側仕様を更新した。

論点は以下の 2 点である。

- AST 折りたたみ方式が `_children` 退避方式ではなく `collapsed` フラグ方式で実装されている
- 入力仕様が「ノードクリックで折りたたみ」ではなく「単クリックで選択、ダブルクリックで折りたたみ」になっている

---

## 影響判定

Phase 3 更新前に他フェーズへの影響を確認した。

| フェーズ | 判定 | 理由 |
|---------|------|------|
| Phase 1 | 修正不要 | AST 基本構造そのものへの変更はない |
| Phase 2 | 修正不要 | API レスポンスおよびバックエンドモデルへの影響はない |
| Phase 4 | 参照更新が必要 | 振る舞い変更は不要だが、`phase3-visualization.md` の版数更新と「AST 単クリック選択 / ダブルクリック折りたたみ」契約を参照側でも明記する必要がある |
| Phase 5 | 修正不要 | コメント機能への影響はない |
| Phase 6 | 修正不要 | エクスポート仕様への影響はない |

---

## 実施内容

### specs/phase3-visualization.md

- バージョンを `1.4` に更新
- AST 折りたたみ方式を `_children` 退避方式から `collapsed` フラグ方式へ修正
- `AstNodeWithMeta` が `_children` を持たず `children` を常時保持することを明記
- `d3.hierarchy(root, d => (d.collapsed ? null : d.children))` を契約として明記
- AST 操作仕様を「単クリックで `onNodeClick(node)`、ダブルクリックで折りたたみ」に修正
- `AstTree.test.ts` のテスト要件を追加
- 完了基準の AST 折りたたみ操作を「クリック」から「ダブルクリック」に修正
- 参照資料に implement 側フィードバック文書を追加

### roadmaps/roadmap.md

- 更新日コメントを 2026-05-23 に更新
- Phase 3 の仕様バージョンを `v1.4 / 2026-05-23` に更新

### specs/phase4-navigation.md

- バージョンを `1.3` に更新
- `phase3-visualization.md` 参照を `v1.4` に更新
- N1 トリガーを「AST 単クリック / CFG ブロッククリック」と明記
- AST では単クリックを `onNodeClick`、ダブルクリックを `collapsed` 切り替えに使う前提を実装上の注意事項に追加

### roadmaps/roadmap.md（追補）

- Phase 4 の仕様バージョンを `v1.3 / 2026-05-23` に更新

---

## 検証

以下を確認した。

- `phase3-visualization.md` から `_children` 退避方式の記述が除去されていること
- AST 操作仕様が「単クリック」と「ダブルクリック」で分離されていること
- `roadmaps/roadmap.md` の Phase 3 バージョン表記が更新されていること
- `phase4-navigation.md` の上流参照が `phase3-visualization.md v1.4` に追従していること

---

## 残事項

`implement/docs/feedback-2026-05-23-ast-collapse-spec-mismatch.md` は参照元フィードバックとして未追跡のまま残した。
design 側の仕様更新対象には含めていない。
