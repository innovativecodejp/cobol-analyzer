# Frontend レビュー修正 2: analyze エラー時の lastResult リセット

レビュー元: `docs/review-frontend-src-2026-05-13.md`  
優先度: 高  
種別: Bug  
対象: `src/frontend/src/main.ts`

---

## 実行前の注意事項

- このプロンプトでは本タスクだけを実装する。
- 既存の単一 Analyze / AST / CFG / DFG / コメント / プロジェクトタブの動作を壊さない。
- `design/specs/` と実装の契約に反する可能性がある変更を見つけた場合は、実装を止めて `implement/docs/` にフィードバックを記録する。
- `design/specs/` は implement 側で変更しない。

---

## 実行プロンプト

`analyze()` 失敗後に `ResizeObserver` が旧 `lastResult` を使って古い解析結果を再描画する問題を修正する。

実装方針:

- Analyze ボタンの `catch` ブロック先頭で `lastResult = null;` を設定する。
- `selectionStore.clearAll()` と既存のエラー表示は維持する。
- このタスクではエラー表示重複の大きなリファクタは行わない。重複整理は別タスクで扱う。
- `tab-project` はエラー表示で消さない。

受け入れ条件:

- API エラー後にリサイズしても、直前の成功結果が AST / CFG / DFG に再描画されない。
- 正常解析後のリサイズ再描画は従来どおり動く。

検証:

```powershell
cd src/frontend
npm test
npm run build
```
