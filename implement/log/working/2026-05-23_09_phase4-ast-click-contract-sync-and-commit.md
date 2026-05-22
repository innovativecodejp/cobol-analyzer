# 2026-05-23_09 Phase 4 AST クリック契約同期と prompt / commit 反映

## 作業概要

`design/specs/phase4-navigation.md` v1.3 の更新を確認し、
AST の単クリック選択 / ダブルクリック折りたたみ契約を frontend 実装へ同期した。

確認の結果、`AstTree.ts` は `collapsed` フラグ方式自体は維持できていたが、
DOM の `click` / `dblclick` 発火順の都合で、ダブルクリック時にも
単クリック由来の `onNodeClick` が先に走り得る状態だった。

そのため、単クリックを短い遅延で実行し、ダブルクリック時は pending click を
キャンセルする実装へ修正した。あわせて、この修正内容を `prompts/` に出力し、
最後に変更を commit した。

---

## 関連 prompt / 参照

- `design/specs/phase4-navigation.md`
- `design/specs/phase3-visualization.md`
- `prompts/2026-05-23_01_frontend-review-fix-ast-collapse-spec-sync.md`
- `prompts/2026-05-23_02_frontend-review-fix-phase4-ast-click-contract.md`
- `log/working/2026-05-23_08_frontend-review-fix-ast-collapse-spec-sync.md`

仕様確認結果:

- `phase4-navigation.md` v1.3 §7.1 で、AST は「単クリックで N1、ダブルクリックで折りたたみ」の契約へ明文化された
- `phase4-navigation.md` v1.3 §12.8 で、ダブルクリック時に単クリック側の N1 が壊れない接続であることが要求されている
- `phase3-visualization.md` v1.4 §6.1 でも単クリック / ダブルクリックの役割分担が同じ前提になっている
- 追加の仕様矛盾や未定義事項は見つからなかったため、`implement/docs/` への新規フィードバック記録は不要と判断した

---

## 実施内容

### 1. `AstTree` のクリック契約を修正

対象ファイル:

- `src/frontend/src/components/AstTree.ts`

反映内容:

- `NODE_CLICK_DELAY_MS = 250` を追加
- `pendingClickTimer` を追加
- 単クリック時は `scheduleNodeClick(...)` で遅延実行するよう変更
- ダブルクリック時は `clearPendingClick()` を先に呼び、pending な単クリックを破棄してから `collapsed` を反転
- `clear()` 時も timer を破棄し、コンポーネント破棄後の遅延発火を防止

意図:

- DOM では通常 `dblclick` 前に `click` が発火するため、即時 `onNodeClick(...)` 呼び出しでは spec とずれる
- 少し遅延させることで、単クリックとダブルクリックを安全に分離した

---

### 2. `AstTree` テストを追加更新

対象ファイル:

- `src/frontend/src/components/AstTree.test.ts`

反映内容:

- fake timers を導入し、待ち時間依存の検証を安定化
- `astTree_singleClickCallsOnNodeClick`
  - click 直後は未発火であること
  - 250ms 経過後に 1 回だけ `onNodeClick` が呼ばれること
- `astTree_doubleClickDoesNotCallOnNodeClick`
  - click + dblclick 後に `onNodeClick` が呼ばれないこと
  - `collapsed` が切り替わること
- 既存の `astTree_doubleClickTogglesCollapsed` は維持

---

### 3. この修正用 prompt を追加

対象ファイル:

- `prompts/2026-05-23_02_frontend-review-fix-phase4-ast-click-contract.md`

内容:

- Phase 4 v1.3 の AST クリック契約への追従タスク
- 実装対象を `AstTree.ts` / `AstTree.test.ts` に限定
- 単クリック遅延、ダブルクリック時のキャンセル、fake timers による検証方針を明記

---

### 4. 変更を commit

実施内容:

- 今回の差分だけを stage
- commit message: `Sync AST click contract with phase4 spec`

commit:

```text
6f15a29
```

---

## 検証

このシェルでは `node` / `npm` が `PATH` から解決できず、ユーザー領域の Node 配置先にも
直接アクセスできなかったため、Visual Studio 同梱 Node を明示指定して検証した。

実行コマンド:

```powershell
cd src/frontend
$env:PATH='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Microsoft\VisualStudio\NodeJs;' + $env:PATH
& 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Microsoft\VisualStudio\NodeJs\node.exe' `
  'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Microsoft\VisualStudio\NodeJs\node_modules\npm\bin\npm-cli.js' `
  test
& 'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Microsoft\VisualStudio\NodeJs\node.exe' `
  'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Microsoft\VisualStudio\NodeJs\node_modules\npm\bin\npm-cli.js' `
  run build
```

結果:

- `AstTree.test.ts`: 3 tests PASS
- `npm test`: 11 files / 38 tests PASS
- `npm run build`: `tsc && vite build` 成功

備考:

- `npm` 実行時に `C:\Users\msd-d\AppData\Local\npm-cache\_logs` への `EPERM` 警告が出たが、テストとビルド自体は成功
- `vite build` では既存の chunk size warning が出たが、ビルド自体は成功

---

## Git 状態

この作業で扱った主なファイル:

- `src/frontend/src/components/AstTree.ts`
- `src/frontend/src/components/AstTree.test.ts`
- `prompts/2026-05-23_02_frontend-review-fix-phase4-ast-click-contract.md`
- `log/working/2026-05-23_09_phase4-ast-click-contract-sync-and-commit.md`

このログ作成時点では、AST クリック契約修正本体は commit 済み。
本ログファイル自体は未 commit。
