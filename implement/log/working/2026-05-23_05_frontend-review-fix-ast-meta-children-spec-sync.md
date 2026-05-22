# 2026-05-23_05 AST 折りたたみ仕様不整合と残り prompt の扱い

## 作業概要

`prompts/2026-05-13_05_frontend-review-fix-ast-meta-children.md` の実行可否を確認したところ、
`design/specs/phase3-visualization.md` と現行実装・レビュー修正方針の間に不整合が見つかった。

implement 側ルールに従い、`implement/docs/` へフィードバックを記録して実装を停止した。
あわせて、残りの low-priority prompt `06` と `07` がこの不整合と独立して進められるかを整理した。

---

## 関連 prompt / 参照

- `prompts/2026-05-13_05_frontend-review-fix-ast-meta-children.md`
- `prompts/2026-05-13_06_frontend-review-fix-main-error-display.md`
- `prompts/2026-05-13_07_frontend-review-fix-cfg-slice-comment.md`
- `design/specs/phase3-visualization.md`
- `docs/review-frontend-src-2026-05-13.md`
- `docs/feedback-2026-05-23-ast-collapse-spec-mismatch.md`

---

## 実施内容

### 1. prompt 05 の spec 不整合を確認

確認結果:

- `src/frontend/src/adapters/astAdapter.ts` の `AstNodeWithMeta` は `collapsed` と `children` のみを持つ
- `src/frontend/src/components/AstTree.ts` は `d3.hierarchy(root, d => (d.collapsed ? null : d.children))` で折りたたみを実装している
- `rg "_children" src/frontend/src` の結果、`_children` 参照はフロント実装配下に存在しない

一方、`design/specs/phase3-visualization.md` §6.1 には以下が残っていた。

- 「ノードクリックで `children` を `_children` に退避して再描画」

このため、レビュー修正 prompt の「`_children` は不要なので削除する」という前提と、
spec の要求が一致していない状態だった。

さらに、入力操作の仕様も不一致だった。

- spec: ノードクリックで折りたたみ
- 現行実装: 単クリックで選択、ダブルクリックで折りたたみ

---

### 2. implement/docs へフィードバックを記録して実装停止

`docs/feedback-2026-05-23-ast-collapse-spec-mismatch.md` を追加し、以下を記録した。

- `_children` 方式と `collapsed` フラグ方式のどちらを正式仕様とするか未確定
- AST ノードの単クリック / ダブルクリックの役割が spec と実装で異なる
- design 側で `phase3-visualization.md` の更新が必要

implement 側では `design/specs/` を変更せず、記録のみ行った。

---

### 3. 残り prompt 06 / 07 の扱いを整理

結論:

- `06` は `src/frontend/src/main.ts` の DOM 書き込み重複整理であり、`05` の AST 折りたたみ仕様不整合とは独立
- `07` は `src/frontend/src/components/CfgGraph.ts` の `targetIds.slice(1)` に説明コメントを足すだけであり、`05` と独立

追加確認:

- `main.ts` には `lastResult = null;` がすでに入っているため、`06` ではその行を維持したまま helper 化すればよい
- `07` はロジック変更不要で、コメント追加のみで対応可能

推奨順:

1. design 側で `phase3-visualization.md` を更新
2. implement 側で `05` を再実行
3. 続けて `06` を実行
4. 最後に `07` を実行

---

## 状態

- `05` は spec 更新待ちで停止
- `06` と `07` は現時点の確認では追加の design フィードバックなしで実行可能
- この時点では実装コードの変更・テスト実行は行っていない

