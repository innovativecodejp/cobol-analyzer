# Phase 4 仕様：双方向ナビゲーション

バージョン: 1.0  
作成日: 2026-05-05  
ステータス: 確定（implement/ への引き渡し可）

前提: `design/specs/phase3-visualization.md` の実装が完了していること。

---

## 1. 目的・スコープ

Phase 3 のダイアグラム表示に、ソースコードとの双方向連動を追加する。
ノードとソース行を相互にナビゲートし、制御フロー遷移・データ影響を手繰れる操作体験を提供する。

### スコープ内
- N1：ダイアグラムノード → Monaco ソース行ジャンプ＋ハイライト
- N2：Monaco カーソル → 対応ダイアグラムノードのハイライト
- N3：GO TO / PERFORM 遷移先ジャンプ（CFG エッジを辿る）
- N4：DFG データ項目クリック → 影響閉包ハイライト
- 選択状態の中央管理（SelectionStore）
- 行番号逆引きインデックス（ソース行 → ASTノード）
- D3.js ノードへのスクロール（`d3.zoom().translateTo`）
- Monaco Decorations によるソース行ハイライト

### スコープ外（Phase 5以降）
- COBOL シンタックスハイライト（Monaco 言語定義）
- プログラム間ナビゲーション（CALL 先プログラムへのジャンプ）
- ナビゲーション履歴（戻る/進む）

---

## 2. Phase 2 / Phase 3 仕様の追補

Phase 4 の実装に必要な変更を上流仕様に反映済み（v1.1）。

| 仕様書 | 変更内容 |
|--------|---------|
| `phase2-engine.md` v1.1 | `DataFlowGraph.ImpactClosure` を API レスポンスに含める |
| `phase3-visualization.md` v1.1 | `DataFlowGraph` TypeScript 型に `impactClosure` フィールドを追加 |

`BasicBlock.Location` は Phase 2 v1.0 時点で定義済み。
値は「ブロック内の最初の `StatementNode.Location`」とする（CfgBuilder 実装時に補足）。

---

## 3. ディレクトリ構造追加分

Phase 3 の構造に以下を追加する。

```
src/frontend/src/
├── store/
│   └── SelectionStore.ts       ← 選択状態の中央管理（EventBus）
├── navigation/
│   ├── LineNodeIndex.ts         ← 行番号逆引きインデックス
│   ├── JumpController.ts        ← N1/N2/N3/N4 のナビゲーションロジック
│   └── MonacoHighlighter.ts     ← Monaco Decorations ラッパー
└── components/                  （既存・一部拡張）
    ├── AstTree.ts               ← N1/N2 ハイライト対応を追加
    ├── CfgGraph.ts              ← N1/N2/N3 ハイライト対応を追加
    └── DfgGraph.ts              ← N1/N4 ハイライト対応を追加
```

---

## 4. SelectionStore（状態管理）

```typescript
// store/SelectionStore.ts

export interface SelectionState {
  selectedAstNodeId: string | null;
  selectedCfgBlockId: string | null;
  selectedDfgNodeId: string | null;
  highlightedLines: { start: number; end: number } | null;
  impactClosureIds: Set<string>;
}

type ChangeHandler = (state: SelectionState) => void;

class SelectionStore {
  private state: SelectionState = {
    selectedAstNodeId: null,
    selectedCfgBlockId: null,
    selectedDfgNodeId: null,
    highlightedLines: null,
    impactClosureIds: new Set(),
  };
  private listeners: ChangeHandler[] = [];

  getState(): Readonly<SelectionState> { return this.state; }

  selectAstNode(id: string | null, lines: { start: number; end: number } | null): void;
  selectCfgBlock(id: string | null, lines: { start: number; end: number } | null): void;
  selectDfgNode(id: string | null, closureIds: string[]): void;
  clearAll(): void;

  on(handler: ChangeHandler): () => void;  // 登録、戻り値は解除関数
  private emit(): void;
}

export const selectionStore = new SelectionStore();
```

各コンポーネント・ナビゲーションモジュールは `selectionStore` のシングルトンを参照する。

---

## 5. 行番号逆引きインデックス（LineNodeIndex）

```typescript
// navigation/LineNodeIndex.ts

export interface LineEntry {
  nodeId: string;       // AstNode を識別する一意ID（"nodeType:startLine:startColumn" 形式）
  nodeType: string;
  category: NodeCategory;
  location: SourceLocation;
  depth: number;        // ASTの深さ（深いほど具体的）
}

export class LineNodeIndex {
  // AstNode ツリーを走査して構築
  static build(root: AstNode): LineNodeIndex;

  // 行番号から最も深い（具体的な）AstNode エントリを返す
  lookup(line: number): LineEntry | null;
}
```

### 構築アルゴリズム

1. AstNode ツリーを DFS で走査する
2. 各ノードについて `startLine` ～ `stopLine` の各行に `LineEntry` を登録する
3. 同一行に複数のエントリがある場合、`depth` が大きいものを採用する（上書き）

---

## 6. MonacoHighlighter

```typescript
// navigation/MonacoHighlighter.ts

export class MonacoHighlighter {
  constructor(private editor: monaco.editor.IStandaloneCodeEditor) {}

  // 対象行範囲に className のデコレーションを設定（前のデコレーションは自動削除）
  highlight(start: number, end: number, className: HighlightClass): void;

  clearAll(): void;
}

export type HighlightClass =
  | 'highlight-node'    // ノードに対応する行（黄背景 #fffde7）
  | 'highlight-jump'    // GOTO/PERFORM 遷移先（紫背景 #f3e5f5）
  | 'highlight-impact'; // 影響閉包（オレンジ背景 #fff3e0）
```

Monaco の `deltaDecorations` を内部で使用し、decoration ID を保持して再呼び出し時に差し替える。

---

## 7. ナビゲーション仕様

### 7.1 N1：ダイアグラムノード → ソース行ジャンプ

**トリガー**: AST / CFG / DFG グラフのノードをクリック

**処理**:
1. クリックされたノードの `location.startLine` / `location.stopLine` を取得
2. `selectionStore.selectAstNode(id, { start, end })` を呼び出す
3. `MonacoHighlighter.highlight(start, end, 'highlight-node')` でソース行をハイライト
4. `editor.revealLineInCenter(start)` でモナコを対象行にスクロール

**D3 上の視覚変化**:
- クリックしたノード: `.selected` クラスを付与（太枠・明色）
- 他のノード: `.dimmed` クラスを付与（opacity 0.3）

### 7.2 N2：Monaco カーソル → ダイアグラムノードハイライト

**トリガー**: Monaco の `onDidChangeCursorPosition` イベント（debounce 200ms）

**処理**:
1. カーソル行番号 `line` を取得
2. `LineNodeIndex.lookup(line)` でエントリを検索
3. エントリが見つかった場合:
   - `selectionStore.selectAstNode(entry.nodeId, entry.location)` を呼び出す
   - AST グラフ上で対応ノードに `.selected` を付与、他を `.dimmed` に
   - 対象ノードが折りたたまれていれば自動展開して表示する
   - `d3.zoom().translateTo()` で対象ノードを SVG 中央に移動
4. エントリが見つからない場合: 選択をクリア（`selectionStore.clearAll()`）

### 7.3 N3：GO TO / PERFORM 遷移先ジャンプ

**トリガー**: CFG グラフ上の BasicBlock 内の Statement テキストをクリック
（StatementType が `"GOTO"` / `"PERFORM_THRU"` / `"PERFORM_LOOP"` のもの）

**処理**:
1. クリックされた StatementNode の StatementType を確認
2. CFG の Edges から `fromBlockId = 現在ブロック.id` かつ `kind ∈ {GoTo, PerformCall, PerformThruCall}` のエッジを取得
3. `toBlockId` で遷移先 BasicBlock を特定
4. 遷移先ブロックを CFG グラフ上でハイライト（`.selected`）
5. `d3.zoom().translateTo()` で遷移先ブロックを SVG 中央に移動
6. 遷移先ブロックの `Location.startLine` で Monaco もジャンプ
   - `MonacoHighlighter.highlight(startLine, stopLine, 'highlight-jump')`
   - `editor.revealLineInCenter(startLine)`

**複数エッジがある場合**（PERFORM THRU など）:
- 全ての該当エッジの遷移先ブロックを `.impact` クラスでハイライトする

### 7.4 N4：DFG データ項目 → 影響閉包ハイライト

**トリガー**: DFG グラフのノードをクリック

**処理**:
1. クリックされた `DfgNode.id`（DataName）を取得
2. `DataFlowGraph.impactClosure[id]` で影響を受けるデータ項目名リストを取得
3. `selectionStore.selectDfgNode(id, closureIds)` を呼び出す
4. DFG グラフ上でのハイライト:
   - クリックしたノード: `.selected`
   - 影響閉包内のノード: `.impact`（オレンジ色）
   - その他: `.dimmed`
5. Monaco ハイライト（省略可）:
   - 影響閉包内のノードに対応するソース行は多数にわたるため Phase 4 ではハイライトしない
   - フォーカスは DFG グラフ上のみとする

---

## 8. D3 ハイライト CSS

```css
/* 選択状態 */
.node.selected rect, .node.selected circle {
  stroke: #f39c12;
  stroke-width: 3px;
  filter: brightness(1.3);
}

/* 影響閉包 */
.node.impact rect, .node.impact circle {
  stroke: #e67e22;
  stroke-width: 2px;
  fill: #fff3e0;
}

/* 非選択（dim） */
.node.dimmed {
  opacity: 0.25;
  transition: opacity 0.2s;
}

/* 通常（トランジション） */
.node {
  transition: opacity 0.2s;
}
```

---

## 9. JumpController

```typescript
// navigation/JumpController.ts

export class JumpController {
  constructor(
    private index: LineNodeIndex,
    private highlighter: MonacoHighlighter,
    private editor: monaco.editor.IStandaloneCodeEditor,
    private cfg: ControlFlowGraph,
    private dfg: DataFlowGraph,
  ) {}

  // N1: ダイアグラムノードクリック
  onDiagramNodeClick(location: SourceLocation): void;

  // N2: Monaco カーソル移動（debounce済みで呼び出すこと）
  onCursorMove(line: number): void;

  // N3: GOTO/PERFORM ステートメントクリック
  onGotoStatementClick(fromBlockId: string, edgeKind: CfgEdgeKind): void;

  // N4: DFG ノードクリック
  onDfgNodeClick(dataName: string): void;
}
```

---

## 10. テスト要件（Vitest）

### LineNodeIndex.test.ts

| テスト名 | 検証内容 |
|----------|---------|
| `build_singleNode_linesMapped` | 1ノードのツリーで startLine〜stopLine が全てインデックスに入る |
| `lookup_line_returnsDeepestNode` | 同じ行に親子ノードがある場合、深い方が返る |
| `lookup_outOfRange_returnsNull` | ノードの範囲外の行番号で null が返る |

### SelectionStore.test.ts

| テスト名 | 検証内容 |
|----------|---------|
| `selectAstNode_firesChangeEvent` | `selectAstNode()` 呼び出しで `on('change')` ハンドラが発火する |
| `clearAll_resetsState` | `clearAll()` で全フィールドが初期値に戻る |
| `selectDfgNode_setsImpactClosureIds` | `selectDfgNode(id, ids)` で `impactClosureIds` が更新される |

### JumpController.test.ts

| テスト名 | 検証内容 |
|----------|---------|
| `onCursorMove_foundNode_storeUpdated` | カーソル行に対応ノードがあれば SelectionStore が更新される |
| `onCursorMove_noNode_storeCleaned` | カーソル行に対応ノードがなければ SelectionStore がクリアされる |
| `onGotoStatementClick_setsTargetBlock` | GoTo エッジの toBlockId が SelectionStore の selectedCfgBlockId に入る |
| `onDfgNodeClick_setsImpactClosure` | クリックした DataName の impactClosure が SelectionStore に反映される |

---

## 11. 完了基準

以下をすべて満たした時点で Phase 4 完了とする。

- [ ] AST グラフのノードをクリックすると Monaco の対応行に黄ハイライトが付く
- [ ] Monaco でカーソルを移動すると対応 AST ノードがハイライトされ、他がdimになる
- [ ] CFG グラフの GOTO ブロックをクリックすると遷移先ブロックがハイライトされ Monaco も紫ハイライトで飛ぶ
- [ ] CFG グラフの PERFORM ブロックをクリックすると呼び出し先ブロックがハイライトされる
- [ ] DFG グラフのデータ項目ノードをクリックすると影響閉包ノードがオレンジハイライトされる
- [ ] data-sample.cbl でWS-NUMERIC をクリックするとWS-CHAR（REDEFINES）も影響閉包としてハイライトされる
- [ ] ノード選択解除（背景クリック）で全ハイライトがリセットされる
- [ ] `npm test` が全テストPASS（LineNodeIndex / SelectionStore / JumpController）

---

## 12. 実装上の注意事項

1. **N2 の debounce**: Monaco `onDidChangeCursorPosition` は毎キーストロークで発火するため、`JumpController.onCursorMove` 呼び出しは 200ms の debounce を挟む。

2. **AST ノードの一意 ID**: `AstNode` は現状 ID プロパティを持たない。Phase 4 実装時に `nodeType + ":" + startLine + ":" + startColumn` 形式の文字列 ID を `LineNodeIndex` 内部で生成して使用する。`AstNode` 本体への ID 追加は implement フィードバックで確認してから判断する。

3. **折りたたまれた AST ノードへのナビゲーション**: N2 でカーソル行に対応するノードが折りたたまれた状態（`collapsed = true`）の場合、祖先ノードまで自動展開してから対象ノードをハイライトする。

4. **CFG force-directed の座標**: `d3.zoom().translateTo(zoom, x, y)` で SVG ビューポートを移動する。ノードの現在座標は D3 シミュレーション終了後に `node.x` / `node.y` から取得する。

5. **N3 の複数エッジ**: PERFORM THRU はケースによって複数エッジが生じる。全ての該当エッジを `.impact` でハイライトし、最初の遷移先のみ `translateTo` でスクロールする。

---

## 13. 参照資料

| 資料 | 参照箇所 | 本仕様との対応 |
|------|---------|--------------|
| `design/specs/phase2-engine.md` v1.1 §5.4 | `DataFlowGraph.ImpactClosure` | §7.4 N4 影響閉包ハイライト |
| `design/specs/phase2-engine.md` §4.2 | `BasicBlock.Location` | §7.3 N3 遷移先ソース行ジャンプ |
| `design/specs/phase2-engine.md` §4.3 | `CfgEdgeKind` 全種別 | §7.3 N3 エッジ種別フィルタリング |
| `design/specs/phase3-visualization.md` v1.1 §9 | `AnalyzeResult` TypeScript 型 | §9 JumpController コンストラクタ引数 |
| `design/specs/phase3-visualization.md` §6.2 | CFG グラフ `.selected` / `.dimmed` | §8 D3 ハイライト CSS |
| `design/brainstorm/phase4-planning.md` | 設計判断メモ | 本仕様全体 |
