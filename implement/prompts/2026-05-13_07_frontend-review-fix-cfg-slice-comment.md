# Frontend レビュー修正 7: CfgGraph targetIds.slice(1) コメント追加

レビュー元: `docs/review-frontend-src-2026-05-13.md`  
優先度: 低  
種別: コード品質  
対象: `src/frontend/src/components/CfgGraph.ts`

---

## 実行前の注意事項

- このプロンプトでは本タスクだけを実装する。
- 既存の単一 Analyze / AST / CFG / DFG / コメント / プロジェクトタブの動作を壊さない。
- `design/specs/` と実装の契約に反する可能性がある変更を見つけた場合は、実装を止めて `implement/docs/` にフィードバックを記録する。
- `design/specs/` は implement 側で変更しない。

---

## 実行プロンプト

nav label click 内の `targetIds.slice(1)` は、最初のターゲットをスキップする理由がコードから読み取りにくい。挙動を変えず、意図を短いコメントで明確にする。

実装方針:

- `targetIds.slice(1)` の直前に、直接ジャンプ先と impact 強調対象の違いを説明するコメントを追加する。
- コメント例:

```typescript
// PERFORM THRU: the direct jump target (index 0) is selected by onStatementClick;
// remaining targets are highlighted as impact blocks.
```

- コメントは実装と矛盾しない内容にする。
- コメント追加以外のロジック変更は行わない。

受け入れ条件:

- `slice(1)` の意図がコード上で分かる。
- CFG nav label click の挙動は変わらない。

検証:

```powershell
cd src/frontend
npm test
npm run build
```
