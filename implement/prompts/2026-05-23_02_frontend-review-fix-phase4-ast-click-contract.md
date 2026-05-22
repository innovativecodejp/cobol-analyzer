# Frontend spec sync 2: Phase 4 AST 単クリック/ダブルクリック契約の同期

仕様: `../design/specs/phase4-navigation.md`（v1.3, 更新日 2026-05-23）  
関連:

```text
../design/specs/phase3-visualization.md
prompts/2026-05-23_01_frontend-review-fix-ast-collapse-spec-sync.md
```

対象:

```text
src/frontend/src/components/AstTree.ts
src/frontend/src/components/AstTree.test.ts
```

---

## 実行前の注意事項

- この prompt は、Phase 4 v1.3 で明確化された AST 単クリック選択 / ダブルクリック折りたたみ契約への追従だけを扱う。
- 既存の AST / CFG / DFG / コメント / プロジェクトタブの動作を壊さない。
- `design/specs/` と実装の契約に反する追加差分を見つけた場合は、実装を止めて `implement/docs/` にフィードバックを記録する。
- `design/specs/` は implement 側で変更しない。

---

## 実行プロンプト

`design/specs/phase4-navigation.md` v1.3 の §7.1 と §12.8 に合わせて、AST ノードのイベント接続を修正する。

今回の正式仕様は以下である。

- AST ノードの単クリックは N1 として `onNodeClick(...)` を呼ぶ
- AST ノードのダブルクリックは `collapsed` をトグルして再描画する
- ダブルクリック時に単クリック由来の N1 が発火してはならない
- Phase 3 v1.4 の `collapsed` フラグ方式を維持する

背景:

- DOM の通常挙動では `dblclick` 前に `click` が発火するため、`click` で即座に `onNodeClick` を呼ぶ実装は spec とずれる
- 現行の `AstTree` が `.on('click', ...)` で直接 `onNodeClick` を呼んでいる場合、ダブルクリック時にも N1 が走る

実装方針:

- まず `phase4-navigation.md` の §7.1 / §12.8 と `phase3-visualization.md` の §6.1 を読み、クリック契約を確認する。
- `AstTree` に pending click timer を追加し、単クリックは短い遅延後に `onNodeClick` を実行する。
- ダブルクリック時は pending な単クリックを必ずキャンセルしてから `collapsed` を反転し、再描画する。
- `clear()` 時にも pending timer を破棄し、再解析やコンポーネント破棄後に遅延発火しないようにする。
- 既存の `expandToNode()`、SelectionStore 購読、`collapsed` ベースの描画方式は維持する。不要な大きいリファクタはしない。
- テストを更新または追加し、以下を明示的に検証する。
  - 単クリックでは `onNodeClick` が 1 回だけ呼ばれる
  - ダブルクリックでは `onNodeClick` が呼ばれず、`collapsed` が切り替わる
  - 既存の折りたたみ表示更新が壊れていない
- timer を使うテストでは fake timers を使い、待ち時間依存の flaky な検証にしない。

受け入れ条件:

- AST 単クリックで `onNodeClick(nodeId, location)` が呼ばれる。
- AST ダブルクリックで `onNodeClick` は呼ばれず、`collapsed` がトグルされる。
- ダブルクリック後に子ノード表示が更新される。
- `AstTree.clear()` 後に pending click が遅延発火しない。
- TypeScript / Vitest が通る。

検証:

```powershell
cd src/frontend
npm test
npm run build
```
