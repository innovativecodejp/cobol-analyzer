# Frontend spec sync 1: AST 折りたたみ契約を Phase 3 v1.4 に同期

仕様: `../design/specs/phase3-visualization.md`（v1.4, 更新日 2026-05-23）  
関連:

```text
prompts/2026-05-13_05_frontend-review-fix-ast-meta-children.md
docs/feedback-2026-05-23-ast-collapse-spec-mismatch.md
```

対象:

```text
src/frontend/src/adapters/astAdapter.ts
src/frontend/src/components/AstTree.ts
src/frontend/src/adapters/astAdapter.test.ts
src/frontend/src/components/AstTree.test.ts
```

---

## 実行前の注意事項

- この prompt は、`design/specs/phase3-visualization.md` v1.4 で解消された AST 折りたたみ仕様不整合への追従に限定する。
- `prompts/2026-05-13_05_frontend-review-fix-ast-meta-children.md` は更新前 spec 前提のため、そのまま再利用しない。
- 既存の AST / CFG / DFG / コメント / プロジェクトタブの動作を壊さない。
- `design/specs/` と実装の契約に反する可能性がある追加差分を見つけた場合は、実装を止めて `implement/docs/` にフィードバックを記録する。
- `design/specs/` は implement 側で変更しない。

---

## 実行プロンプト

`design/specs/phase3-visualization.md` v1.4 に合わせて、AST 折りたたみ方式と入力操作契約を実装・テストへ同期する。

今回の正式仕様は以下である。

- `AstNodeWithMeta` は `_children` を持たず、完全な `children` ツリーを保持する
- 折りたたみ状態は `collapsed` フラグで表現する
- `toD3Hierarchy()` では `Element` カテゴリのみ初期状態で `collapsed = true`
- `d3.hierarchy(root, d => (d.collapsed ? null : d.children))` で表示対象を切り替える
- AST ノードの単クリックは `onNodeClick(...)` を呼ぶ
- AST ノードのダブルクリックで `collapsed` をトグルして再描画する

実装方針:

- まず `phase3-visualization.md` の §5.3 / §6.1 / §10 / §11 / §12.6 を読み、契約を再確認する。
- `rg "_children" src/frontend/src` を実行し、`_children` 参照が残っていれば削除する。
- `AstNodeWithMeta` は `collapsed` と `children` を使う現在の構造に統一し、`children` 配列の退避・復元ロジックを入れない。
- `AstTree` の描画は `collapsed` フラグだけで制御し、単クリックで折りたたまない。
- ダブルクリック時は `collapsed` を反転し、子ノード表示が更新されることを維持する。
- テストは更新後 spec の観点へ揃える。
  - `astAdapter_elementNodesInitiallyCollapsed`
  - `astTree_singleClickCallsOnNodeClick`
  - `astTree_doubleClickTogglesCollapsed`
- 現行コードがすでに仕様を満たしている場合は、不要なリファクタは行わず、テスト・fixture・命名のズレだけを最小修正する。

受け入れ条件:

- `src/frontend/src` 配下に `_children` 参照がない。
- `AstNodeWithMeta.children` は完全木を保持し、描画時のみ `collapsed` で子ノード列挙を止める。
- AST 単クリックで `onNodeClick` が呼ばれる。
- AST ダブルクリックで `collapsed` が切り替わり、再描画後の表示ノード数が変化する。
- 更新後 spec とテスト名・fixture の前提が一致する。

検証:

```powershell
rg "_children" src/frontend/src
cd src/frontend
npm test
npm run build
```
