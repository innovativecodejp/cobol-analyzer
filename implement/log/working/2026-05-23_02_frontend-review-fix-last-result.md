# 2026-05-23_02 フロントエンド lastResult リセット修正

## 作業概要

`prompts/2026-05-13_02_frontend-review-fix-last-result.md` に従い、
`analyze()` の API エラー後に `ResizeObserver` が直前の成功結果を再描画する問題を修正した。

---

## 関連コミット

| コミット | 内容 |
|---------|------|
| `bffa318` | `fix frontend stale last result on analyze error` |

---

## 実施内容

### 1. 対象 prompt と仕様を確認

確認した内容:

- `prompts/2026-05-13_02_frontend-review-fix-last-result.md`
- `design/specs/phase3-visualization.md`
- `design/specs/phase4-navigation.md`

仕様との矛盾や未定義事項は見つからなかったため、`implement/docs/` へのフィードバック記録は不要と判断した。

---

### 2. `lastResult` のエラー時リセットを追加

対象ファイル:

- `src/frontend/src/main.ts`

反映内容:

- Analyze ボタンの `catch` ブロック冒頭で `lastResult = null;` を設定
- 既存の `selectionStore.clearAll()` と AST / CFG / DFG のエラー表示を維持
- `tab-project` の表示には変更を加えない

これにより、API エラー後にウィンドウリサイズが発生しても、`ResizeObserver` が旧解析結果を再描画しない状態にした。

---

## 検証

実行コマンド:

```powershell
cd src/frontend
npm test
npm run build
```

結果:

- `npm test`: 11 files / 34 tests PASS
- `npm run build`: `tsc && vite build` 成功

`npm run build` では Vite の chunk size warning が出たが、ビルド自体は成功した。

---

## Git 状態

- commit: `bffa318`
- commit 後の `git status --short`: clean

`git status` などで `C:\Users\msd-d/.config/git/ignore` へのアクセス警告が出たが、
commit 自体は正常に完了した。
