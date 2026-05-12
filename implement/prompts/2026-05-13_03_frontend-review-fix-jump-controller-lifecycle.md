# Frontend レビュー修正 3: JumpController lifecycle 整理

レビュー元: `docs/review-frontend-src-2026-05-13.md`  
優先度: 中  
種別: Bug / lifecycle  
対象:

```text
src/frontend/src/main.ts
src/frontend/src/navigation/JumpController.ts
src/frontend/src/navigation/JumpController.test.ts
```

---

## 実行前の注意事項

- このプロンプトでは本タスクだけを実装する。
- 既存の単一 Analyze / AST / CFG / DFG / コメント / プロジェクトタブの動作を壊さない。
- `design/specs/` と実装の契約に反する可能性がある変更を見つけた場合は、実装を止めて `implement/docs/` にフィードバックを記録する。
- `design/specs/` は implement 側で変更しない。

---

## 実行プロンプト

`JumpController.dispose()` が存在するがアプリ側から呼ばれておらず、SelectionStore subscription の lifecycle 契約が曖昧になっている点を整理する。

実装方針:

- `JumpController` は Monaco editor に紐づくアプリ単位のシングルインスタンスなので、`renderResult()` のたびに `dispose()` しない。
- `dispose()` を idempotent にする。
  - `this.storeUnsub?.();`
  - `this.storeUnsub = null;`
  - 必要なら `this.highlighter.clearAll();`
- `main.ts` でアプリ終了時に `jumpController.dispose()` が呼ばれるようにする。
  - 例: `window.addEventListener('beforeunload', () => jumpController.dispose());`
- `init()` は解析結果の差し替え責務に限定し、subscription を増やさない。
- `JumpController.test.ts` に `dispose()` が subscription を解除すること、複数回呼んでも壊れないことを確認するテストを追加する。
- `renderResult()` のたびに `JumpController` を再生成する設計へは変更しない。

受け入れ条件:

- `JumpController` の subscription がアプリ lifecycle 上で明示的に解放される。
- `dispose()` を複数回呼んでも例外が出ない。
- `init()` を複数回呼んでも subscription が増えない。
- 既存の N1 / N2 / N3 ナビゲーション挙動が変わらない。

検証:

```powershell
cd src/frontend
npm test
npm run build
```
