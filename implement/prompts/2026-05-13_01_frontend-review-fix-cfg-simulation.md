# Frontend レビュー修正 1: CfgGraph simulation 停止

レビュー元: `docs/review-frontend-src-2026-05-13.md`  
優先度: 高  
種別: Bug  
対象: `src/frontend/src/components/CfgGraph.ts`

---

## 実行前の注意事項

- このプロンプトでは本タスクだけを実装する。
- 既存の単一 Analyze / AST / CFG / DFG / コメント / プロジェクトタブの動作を壊さない。
- `design/specs/` と実装の契約に反する可能性がある変更を見つけた場合は、実装を止めて `implement/docs/` にフィードバックを記録する。
- `design/specs/` は implement 側で変更しない。

---

## 実行プロンプト

`CfgGraph.render()` で生成している D3 force simulation が `clear()` から停止できず、連続解析時に旧 simulation がクールダウンまで動き続ける問題を修正する。

実装方針:

- `CfgGraph` クラスに `private simulation: d3.Simulation<SimNode, SimLink> | null = null;` を追加する。
- `render()` の先頭で既存 simulation を `stop()` してから `null` に戻す。
- `render()` 内のローカル変数 `simulation` を `this.simulation` に置き換える。
- drag handler や tick handler は `this.simulation` またはローカル参照を安全に使う。
- `clear()` でも `this.simulation?.stop(); this.simulation = null;` を行う。
- `MAX_BLOCKS` 超過で早期 return する場合も、旧 simulation が残らないようにする。
- 表示・選択・ズームの既存挙動は変えない。

受け入れ条件:

- `clear()` 後に旧 simulation が動き続けない。
- 再解析・リサイズ再描画で CFG 表示が壊れない。
- TypeScript build が通る。

検証:

```powershell
cd src/frontend
npm test
npm run build
```
