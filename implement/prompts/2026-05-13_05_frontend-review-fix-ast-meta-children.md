# Frontend レビュー修正 5: AstNodeWithMeta._children 削除

レビュー元: `docs/review-frontend-src-2026-05-13.md`  
優先度: 低  
種別: 設計の不整合  
対象:

```text
src/frontend/src/adapters/astAdapter.ts
src/frontend/src/components/AstTree.ts
src/frontend/src/**/*.test.ts
```

---

## 実行前の注意事項

- このプロンプトでは本タスクだけを実装する。
- 既存の単一 Analyze / AST / CFG / DFG / コメント / プロジェクトタブの動作を壊さない。
- `design/specs/` と実装の契約に反する可能性がある変更を見つけた場合は、実装を止めて `implement/docs/` にフィードバックを記録する。
- `design/specs/` は implement 側で変更しない。

---

## 実行プロンプト

`AstNodeWithMeta._children` は型に存在するが、現行の折り畳み実装では `collapsed` と `children` のみを使っており、読み書きされていない。不要フィールドを削除して型を整理する。

実装方針:

- `rg "_children" src/frontend/src` で参照がないことを確認する。
- `AstNodeWithMeta` から `_children?: AstNodeWithMeta[];` を削除する。
- `toD3Hierarchy()` の戻り値や `AstTree` の折り畳み挙動は変えない。
- テスト fixture に `_children` があれば削除する。

受け入れ条件:

- `_children` の参照が `src/frontend/src` からなくなる。
- AST のクリック選択・ダブルクリック折り畳みが壊れない。

検証:

```powershell
cd src/frontend
npm test
npm run build
```
