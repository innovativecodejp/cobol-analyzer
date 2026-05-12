# Frontend レビュー修正 6: main.ts エラー表示重複整理

レビュー元: `docs/review-frontend-src-2026-05-13.md`  
優先度: 低  
種別: コード品質  
対象: `src/frontend/src/main.ts`

---

## 実行前の注意事項

- このプロンプトでは本タスクだけを実装する。
- 既存の単一 Analyze / AST / CFG / DFG / コメント / プロジェクトタブの動作を壊さない。
- `design/specs/` と実装の契約に反する可能性がある変更を見つけた場合は、実装を止めて `implement/docs/` にフィードバックを記録する。
- `design/specs/` は implement 側で変更しない。

---

## 実行プロンプト

`showErrors()` と Analyze ボタンの `catch` ブロックで AST / CFG / DFG タブへ同じような HTML を書き込む処理が重複している。挙動を変えずに共通化する。

実装方針:

- AST / CFG / DFG の 3 タブへ同じ HTML を書く helper を追加する。
  - 例: `function setDiagramErrorHtml(html: string): void`
- `showErrors(result: AnalyzeResult)` は既存の parse error 表示 HTML を作り、helper を呼ぶ。
- `catch` ブロック用に `showErrorMessage(msg: string): void` を追加するか、同等の小さな helper にまとめる。
- タスク 2 の `lastResult = null;` が既に入っている場合は残す。未実装の場合はこのタスクでは追加しない。
- `tab-project` の内容は変更しない。
- このタスクでは HTML escaping などの仕様外変更を広げない。必要性を見つけた場合は別フィードバックとして扱う。

受け入れ条件:

- parse error と API error の表示内容が従来と同等である。
- AST / CFG / DFG 以外のタブを消さない。
- 重複した DOM 書き込み処理が 1 箇所にまとまっている。

検証:

```powershell
cd src/frontend
npm test
npm run build
```
