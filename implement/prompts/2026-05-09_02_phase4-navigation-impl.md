# Phase 4 実装プロンプト：双方向ナビゲーション

仕様: `../design/specs/phase4-navigation.md`（実装前に全文を読むこと）

---

## 現状確認（実装済み範囲）

Phase 3 実装済みファイル（変更可・拡張可）：

```
src/frontend/
├── index.html
├── package.json / vite.config.ts / tsconfig.json
└── src/
    ├── main.ts
    ├── vite-env.d.ts
    ├── types/analyzeResult.ts
    ├── api/analyzeApi.ts
    ├── adapters/cfgAdapter.ts / dfgAdapter.ts / astAdapter.ts
    ├── adapters/*.test.ts（9テスト）
    ├── components/Editor.ts / AstTree.ts / CfgGraph.ts / DfgGraph.ts / MdiPanel.ts
    ├── components/MdiPanel.test.ts（2テスト）
    └── styles/main.css
```

Phase 3 時点で Phase 4 用に保持済みのデータ：
- `cfgAdapter.ts`: `D3Node.statements`（`CfgStatement[]`）/ `D3Node.location` を保持済み
- `analyzeResult.ts`: `DataFlowGraph.impactClosure: Record<string, string[]>` 定義済み
- `AstNode.id` は `"{NodeType}:{StartLine}:{StartColumn}"` 形式（Phase 1 §6.3）

---

## 実装前の注意事項

### 1. コンポーネントインスタンスの寿命管理

現在の `main.ts` は解析のたびに `new AstTree()` 等を生成するが、
Phase 4 では SelectionStore への購読（`selectionStore.on(...)`）を登録するため、
古いインスタンスの購読を解除しないとメモリリークになる。

`main.ts` でコンポーネント参照を変数に保持し、再解析前に `clear()` を呼び出すこと：

```typescript
let currentAstTree: AstTree | null = null;
// ...
function renderResult(result: AnalyzeResult): void {
  currentAstTree?.clear();  // ← 購読解除を含む
  currentAstTree = new AstTree(astContainer);
  currentAstTree.render(toD3Hierarchy(result.ast));
  // ...
}
```

### 2. D3 ハイライトはクラス付け替えで行う（再レンダリング禁止）

SelectionStore の変化ハンドラ内では `render()` を呼ばず、D3 の `.classed()` でクラスのみ変更する。

```typescript
selectionStore.on(state => {
  this.g.selectAll<SVGGElement, ...>('g.node')
    .classed('selected', d => d.id === state.selectedCfgBlockId)
    .classed('dimmed', d => state.selectedCfgBlockId !== null && d.id !== state.selectedCfgBlockId);
});
```

### 3. Editor.ts に `getEditor()` メソッドを追加

`MonacoHighlighter` と `main.ts` の N2 実装が `IStandaloneCodeEditor` を必要とする。
`Editor.ts` に以下を追加：

```typescript
getEditor(): monaco.editor.IStandaloneCodeEditor {
  return this.inner;
}
```

### 4. N2 の debounce は main.ts で実装

仕様 §12-1 の通り、`onDidChangeCursorPosition` のコールバックは 200ms debounce を挟む。
debounce は標準ライブラリなし・手実装（`let timer: ReturnType<typeof setTimeout>`）で良い。

### 5. AstNode の id フィールド

`AstNode` 型（`types/analyzeResult.ts`）に `id: string` フィールドが定義されていることを確認し、
なければ追加する（バックエンドは Phase 1 §6.3 で出力済み）。

---

## タスク一覧

以下を順に実施する。

---

### タスク 1: CSS 追加（styles/main.css）

仕様 §8 の D3 ハイライト CSS を `main.css` に追記する。
Monaco Decorations 用のクラスも追加する：

```css
/* D3 ハイライト（§8） */
.node.selected rect, .node.selected circle { ... }
.node.impact rect, .node.impact circle { ... }
.node.dimmed { ... }
.node { transition: opacity 0.2s; }

/* Monaco Decorations */
.highlight-node { background-color: #fffde7; }
.highlight-jump { background-color: #f3e5f5; }
.highlight-impact { background-color: #fff3e0; }
```

Monaco Decorations の CSS はグローバルスタイルとして `main.css` に定義する。
`deltaDecorations` の `className` オプションで指定する形式に合わせること。

---

### タスク 2: SelectionStore の実装

仕様 §4 の通り `src/store/SelectionStore.ts` を新規作成する。

実装ポイント：
- `on(handler)` の戻り値は購読解除関数（`() => void`）
- `selectAstNode` / `selectCfgBlock` / `selectDfgNode` / `clearAll` は各自で `emit()` を呼ぶ
- `selectDfgNode(id, closureIds)` で `impactClosureIds` は `new Set(closureIds)` にする

テスト `src/store/SelectionStore.test.ts` を作成し、仕様 §10 の 3 件を実装する。

---

### タスク 3: LineNodeIndex の実装

仕様 §5 の通り `src/navigation/LineNodeIndex.ts` を新規作成する。

実装ポイント：
- `LineEntry.nodeId` は `AstNode.id` をそのまま使用する
- 構築アルゴリズム（§5 下部）：DFS 走査 → 各行に登録 → 同一行は `depth` 大きい方で上書き
- `Map<number, LineEntry>` で行番号をキーに保持する

テスト `src/navigation/LineNodeIndex.test.ts` を作成し、仕様 §10 の 3 件を実装する。

---

### タスク 4: MonacoHighlighter の実装

仕様 §6 の通り `src/navigation/MonacoHighlighter.ts` を新規作成する。

実装ポイント：
- `IStandaloneCodeEditor` を受け取りコンストラクタで保持
- `deltaDecorations(oldIds, newDecorations)` で前回のデコレーションを自動削除
- `highlight()` 呼び出しのたびに decoration ID 配列を更新する
- `clearAll()` は `deltaDecorations(this.decorationIds, [])` で全削除

Monaco の `IModelDecorationOptions` 例：
```typescript
{
  isWholeLine: true,
  className: 'highlight-node',  // CSS クラス名
}
```

---

### タスク 5: JumpController の実装

仕様 §9 の通り `src/navigation/JumpController.ts` を新規作成する。

**N1: `onDiagramNodeClick(location: SourceLocation)`**
1. `selectionStore.selectAstNode(nodeId, { start: location.startLine, end: location.stopLine })`
2. `MonacoHighlighter.highlight(start, end, 'highlight-node')`
3. `editor.revealLineInCenter(start)`

**N2: `onCursorMove(line: number)`**
1. `LineNodeIndex.lookup(line)` でエントリ検索
2. 見つかれば `selectionStore.selectAstNode(entry.nodeId, { start, end })`
3. 見つからなければ `selectionStore.clearAll()` + `MonacoHighlighter.clearAll()`

**N3: `onGotoStatementClick(fromBlockId: string, edgeKind: CfgEdgeKind)`**
1. CFG の `edges` から `fromBlockId` かつ `kind ∈ {GoTo, PerformCall, PerformThruCall}` のエッジを取得
2. 最初の `toBlockId` で `selectionStore.selectCfgBlock(toBlockId, location)`
3. 全該当エッジの toBlockId を `impactClosureIds` として `selectDfgNode` でなく Store に追記（`.impact` 適用）
4. `MonacoHighlighter.highlight(startLine, stopLine, 'highlight-jump')`
5. `editor.revealLineInCenter(startLine)`

**N4: `onDfgNodeClick(dataName: string)`**
1. `DataFlowGraph.impactClosure[dataName]` で影響閉包リストを取得
2. `selectionStore.selectDfgNode(dataName, closureIds)`

テスト `src/navigation/JumpController.test.ts` を作成し、仕様 §10 の 4 件を実装する。
テストでは `LineNodeIndex` / `MonacoHighlighter` / `editor` をモックオブジェクトで代替する。

---

### タスク 6: AstTree.ts の拡張（N1 / N2）

**N1（ノードクリック → ソース行ジャンプ）**:
- ノードのクリックハンドラを折りたたみトグルから「N1 + 折りたたみトグル」へ変更
- N1 は `JumpController.onDiagramNodeClick(d.data.location)` を呼ぶ
- 折りたたみは選択と独立して動作させる（ダブルクリックを折りたたみ、シングルクリックを N1 にするなど）
- または単クリックで N1 のみ実行し、折りたたみは別途ダブルクリックに割り当てる（仕様に未定義のため実装者判断でよい）

**N2（Store → AST ハイライト）**:
- `render()` 後に `selectionStore.on(...)` を購読
- ハンドラ内で `g.selectAll('g.node').classed('selected', ...).classed('dimmed', ...)`
- 対象ノードが折りたたまれている場合（`collapsed = true`）: 祖先を辿って展開し `render(root)` を再呼び出し
- 購読解除関数を `clear()` 内で呼ぶ

---

### タスク 7: CfgGraph.ts の拡張（N1 / N2 / N3）

**N1（ブロッククリック → ソース行ジャンプ）**:
- ノードのクリックで `JumpController.onDiagramNodeClick(d.location)` を呼ぶ

**N2（Store → CFG ハイライト）**:
- `selectionStore.on(...)` で `selectedCfgBlockId` に基づきノードの `.selected` / `.dimmed` を制御

**N3（Statement テキストクリック → 遷移先ジャンプ）**:
- 各 CFG ノード内に `d.statements` をテキストとして追加描画する
  - 対象: `statementType ∈ ['GoTo', 'PerformThruCall', 'PerformCall']`
  - ノード矩形（120×40px）を高さ可変に拡張、またはノード下にテキストリストを描画する
- クリックで `JumpController.onGotoStatementClick(d.id, statement.statementType)` を呼ぶ

**背景クリックでクリア**:
- SVG 背景（`rect.bg` や `svg` 自体）クリックで `selectionStore.clearAll()` + `MonacoHighlighter.clearAll()`

---

### タスク 8: DfgGraph.ts の拡張（N1 / N4）

**N1（ノードクリック）**:
- ノードクリックで `JumpController.onDiagramNodeClick(d.location)` を呼ぶ
- DFG ノードは `DataItem` のため `location` は `DfgNode` に `location` フィールドがあれば使用
  - なければ N1 は DFG ノードでは省略し、N4 のみ実装する（仕様 §7.4 では DFG は N4 主体）

**N4（ノードクリック → 影響閉包）**:
- ノードクリックで `JumpController.onDfgNodeClick(d.name)`
- Store の変化で `.selected` / `.impact` / `.dimmed` を付け替え

---

### タスク 9: main.ts の修正

1. コンポーネント参照変数の追加（`let currentAstTree`, `let currentCfgGraph`, `let currentDfgGraph`）
2. `renderResult()` 冒頭で旧インスタンスの `clear()` を呼ぶ
3. 解析成功後に `JumpController` を初期化し、各コンポーネントに渡す
4. N2 の debounce 実装：

```typescript
let cursorDebounce: ReturnType<typeof setTimeout> | null = null;
editor.getEditor().onDidChangeCursorPosition(e => {
  if (cursorDebounce) clearTimeout(cursorDebounce);
  cursorDebounce = setTimeout(() => {
    jumpController.onCursorMove(e.position.lineNumber);
  }, 200);
});
```

5. 再解析時（Analyze ボタン）に前の `JumpController` を破棄し新規生成する

---

### タスク 10: ビルド検証とテスト実行

```powershell
cd src/frontend
npm test
npm run build
```

- テスト: Phase 3 の 11 件 + Phase 4 の 10 件 = 合計 21 件 PASS を確認
- ビルド: エラーなし（チャンクサイズ警告は許容）

---

### タスク 11: ブラウザ動作確認

バックエンドと Vite Dev Server を起動し、以下を順に確認する。

#### hello.cbl — N1 / N2 基本動作

1. hello.cbl を Analyze する
2. **N1**: AST タブで `Division` ノードをクリック → Monaco の対応行（例: 行1）に黄ハイライト / スクロール
3. **N1**: CFG タブで `MAIN-PARA` ブロックをクリック → Monaco の対応行に黄ハイライト
4. **N2**: Monaco のカーソルを `DISPLAY WS-MESSAGE.`（行9）に移動 → AST タブで対応 Statement ノードがハイライト・他が dim
5. 背景クリックでハイライトがリセットされる

#### goto-sample.cbl — N3 動作確認

6. goto-sample.cbl を Analyze する
7. CFG タブで `MAIN-PARA` ブロック内の `GoTo` Statement テキストをクリック
8. `END-PARA` ブロックがハイライト（`.selected`）され、Monaco も `END-PARA` の行に紫ハイライトで移動する

#### data-sample.cbl — N4 動作確認

9. data-sample.cbl を Analyze する
10. DFG タブで `WS-NUMERIC` ノードをクリック
11. `WS-CHAR`（REDEFINES）がオレンジ（`.impact`）でハイライトされる
12. `WS-NUMERIC` 自身は `.selected`（太枠）で表示される
13. その他ノードが `.dimmed`（薄表示）になる

---

### タスク 12: 問題の修正

動作確認で発見した問題を修正する。修正後は `npm run build` と `npm test` を実行し、
全テスト PASS を確認する。

仕様との矛盾・未定義事項を発見した場合は `implement/docs/` にフィードバックを記録し、
実装を止めてユーザーに確認すること。

---

## 完了確認

仕様 §11 の 8 項目すべてにチェックが入ったことを確認する：

```
- [ ] AST グラフのノードをクリックすると Monaco の対応行に黄ハイライトが付く
- [ ] Monaco でカーソルを移動すると対応 AST ノードがハイライトされ、他が dim になる
- [ ] CFG グラフの GoTo ブロックをクリックすると遷移先ブロックがハイライトされ Monaco も紫で飛ぶ
- [ ] CFG グラフの PERFORM ブロックをクリックすると呼び出し先ブロックがハイライトされる
- [ ] DFG グラフのデータ項目ノードをクリックすると影響閉包ノードがオレンジハイライトされる
- [ ] data-sample.cbl で WS-NUMERIC をクリックすると WS-CHAR（REDEFINES）も影響閉包としてハイライトされる
- [ ] ノード選択解除（背景クリック）で全ハイライトがリセットされる
- [ ] npm test が全テスト PASS（LineNodeIndex / SelectionStore / JumpController を含む 21 件以上）
```

---

## 仕様との矛盾発見時の対処

実装中に仕様の矛盾・未定義事項を発見した場合：

1. `implement/docs/` にフィードバック内容を記録する
2. 実装を停止してユーザーに報告する
3. ユーザーの指示を待つ（`design/specs/` を自分で変更しない）
