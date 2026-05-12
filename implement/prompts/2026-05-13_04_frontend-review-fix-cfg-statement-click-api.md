# Frontend レビュー修正 4: CfgGraph.setOnStatementClick 契約整理

レビュー元: `docs/review-frontend-src-2026-05-13.md`  
優先度: 中  
種別: 設計の不整合  
対象:

```text
src/frontend/src/components/CfgGraph.ts
src/frontend/src/main.ts
```

---

## 実行前の注意事項

- このプロンプトでは本タスクだけを実装する。
- 既存の単一 Analyze / AST / CFG / DFG / コメント / プロジェクトタブの動作を壊さない。
- `design/specs/` と実装の契約に反する可能性がある変更を見つけた場合は、実装を止めて `implement/docs/` にフィードバックを記録する。
- `design/specs/` は implement 側で変更しない。

---

## 実行プロンプト

`CfgGraph.setOnStatementClick` の callback 型が `(blockId: string, statementType: string) => void` になっている一方、呼び出し側は `blockId` しか使っておらず、`JumpController.onGotoStatementClick` も `statementType` を受け取らない。public API と実装の契約を揃える。

実装方針:

- まず `rg "setOnStatementClick|onStatementClick|onGotoStatementClick" src/frontend/src` で利用箇所を確認する。
- `statementType` を使う仕様・実装がない場合は、`CfgGraph` の callback 型を `(blockId: string) => void` に変更する。
- `setOnStatementClick(handler: (blockId: string) => void): void` に変更する。
- nav label click 内では `this.onStatementClick?.(d.block.id);` に変更する。
- `main.ts` の呼び出しは `currentCfgGraph.setOnStatementClick(blockId => jumpController.onGotoStatementClick(blockId));` のように明示する。
- `statementType` はラベル表示や nav label data では引き続き使う。削除しない。
- もし specs が `statementType` を callback 契約として要求している場合は実装を止め、`implement/docs/` に仕様フィードバックを記録する。

受け入れ条件:

- 未使用の public callback 引数がなくなる。
- N3 の GOTO / PERFORM / PERFORM THRU ジャンプ動作は維持される。
- TypeScript build が通る。

検証:

```powershell
cd src/frontend
npm test
npm run build
```
