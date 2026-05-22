# 2026-05-23_06 main.ts エラー表示重複整理

## 作業概要

`prompts/2026-05-13_06_frontend-review-fix-main-error-display.md` に従い、
`src/frontend/src/main.ts` の AST / CFG / DFG 向けエラー表示書き込みを共通化した。

parse error 表示と API error 表示の文面は変えず、
重複していた DOM 書き込み処理だけを helper に集約した。

---

## 関連 prompt / 参照

- `prompts/2026-05-13_06_frontend-review-fix-main-error-display.md`
- `docs/review-frontend-src-2026-05-13.md`
- `design/specs/phase3-visualization.md`

仕様確認結果:

- `design/specs/phase3-visualization.md` §12.3 では、`errors[]` がある場合はダイアグラムエリアにエラー一覧を表示し、AST / CFG / DFG を描画しない契約になっている
- 今回の変更は表示先と表示内容を維持したまま、書き込み処理を共通化するだけで仕様逸脱はなかった
- `tab-project` を変更する要件は存在せず、今回も対象外とした

---

## 実施内容

### 1. エラー表示書き込みを helper 化

対象ファイル:

- `src/frontend/src/main.ts`

反映内容:

- `setDiagramErrorHtml(html: string): void` を追加
- AST / CFG / DFG の 3 タブへの `innerHTML` 書き込みをこの helper に集約

---

### 2. parse error / API error の入口を整理

反映内容:

- `showErrors(result)` は既存の parse error HTML を生成した後、`setDiagramErrorHtml()` を呼ぶ形に変更
- `showErrorMessage(msg: string)` を追加し、Analyze ボタンの `catch` ブロックから利用する形に変更
- 既存の `lastResult = null;` はそのまま維持
- `selectionStore.clearAll()` の呼び出し位置・挙動は変更しない

維持した内容:

- parse error の `Line {line}:{column} — {message}` 表示形式
- API error の単一メッセージ表示形式
- `tab-project` と `tab-comment` の内容非変更

---

## 検証

このシェルでは `npm` が `PATH` から解決できないため、
ユーザー環境に配置済みの npm を明示して実行した。

実行コマンド:

```powershell
cd src/frontend
& 'C:\Users\msd-d\AppData\Local\Programs\nodejs\npm.cmd' test
& 'C:\Users\msd-d\AppData\Local\Programs\nodejs\npm.cmd' run build
```

結果:

- `npm test`: 11 files / 37 tests PASS
- `npm run build`: `tsc && vite build` 成功

補足:

- `vite build` では既存の chunk size warning が出たが、ビルド自体は成功した

---

## Git 状態

関連 commit:

```text
d908716 frontend: deduplicate diagram error rendering
```

commit 対象:

- `src/frontend/src/main.ts`

この commit には `design/` 配下の既存変更は含めていない。
