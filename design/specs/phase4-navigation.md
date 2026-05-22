# Phase 4 仕様：双方向ナビゲーション

バージョン: 1.3
作成日: 2026-05-05  
更新日: 2026-05-23（Phase 3 AST 折りたたみ仕様 v1.4 への参照更新。AST 単クリック選択 / ダブルクリック折りたたみ契約との整合を明確化）
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

Phase 4 の実装が依存する上流仕様・実装前提は以下とする。

| 仕様書 | 変更内容 |
|--------|---------|
| `phase2-engine.md` v1.4 | `DataFlowGraph.ImpactClosure` を API レスポンスに含める |
| `phase3-visualization.md` v1.4 | `DataFlowGraph` TypeScript 型に `impactClosure` フィールドを追加 |
| `phase3-visualization.md` v1.4 | `D3Node` に `statements` / `location` を保持し、BasicBlock 内の遷移文クリックで使用可能にする |
| `phase2-engine.md` v1.4 | IF ブランチ内ステートメントを AST `Children` にも含めること（LineNodeIndex が `children` を DFS するため） |
| `phase2-engine.md` v1.4 | IF ブランチ synthetic ブロック内の GOTO からも `GoTo` エッジを生成すること（N3 が CFG エッジを辿るため） |

`BasicBlock.Location` は Phase 2 で定義済み。
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
  selectedAstLineRange: { start: number; end: number } | null;
  selectedCfgBlockId: string | null;
  selectedDfgNodeId: string | null;
  impactClosureIds: Set<string>;
}

type Handler = (state: SelectionState) => void;

class SelectionStore {
  private state: SelectionState = {
    selectedAstNodeId: null,
    selectedAstLineRange: null,
    selectedCfgBlockId: null,
    selectedDfgNodeId: null,
    impactClosureIds: new Set(),
  };
  private handlers = new Set<Handler>();

  getState(): SelectionState { return this.state; }

  selectAstNode(id: string, lineRange: { start: number; end: number }): void;
  selectCfgBlock(id: string): void;
  selectDfgNode(id: string, closureIds: string[]): void;
  clearAll(): void;

  on(handler: Handler): () => void;  // 登録、戻り値は解除関数
  private emit(): void;
}

export const selectionStore = new SelectionStore();
```

各コンポーネント・ナビゲーションモジュールは `selectionStore` のシングルトンを参照する。
Monaco の実際の行ハイライトは `MonacoHighlighter` が管理し、`SelectionStore` は D3 側の選択状態と AST 選択時の行範囲だけを保持する。

---

## 5. 行番号逆引きインデックス（LineNodeIndex）

```typescript
// navigation/LineNodeIndex.ts

export interface LineEntry {
  nodeId: string;       // AstNode を識別する一意ID（"nodeType:startLine:startColumn" 形式）
  startLine: number;
  stopLine: number;
  depth: number;        // ASTの深さ（深いほど具体的）
}

export class LineNodeIndex {
  // AstNode ツリーを走査して構築
  constructor(root: AstNode);

  // 行番号から最も深い（具体的な）AstNode エントリを返す
  lookup(line: number): LineEntry | undefined;
}
```

### 構築アルゴリズム

1. AstNode ツリーを DFS で走査する
2. 各ノードについて `location.startLine` をキーに `LineEntry` を登録する
3. `LineEntry` には `startLine` / `stopLine` を保持し、選択時のハイライト範囲に使う
4. 同一開始行に複数のエントリがある場合、`depth` が大きいものを採用する（上書き）

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

**トリガー**: AST ノードの単クリック、または CFG グラフのブロッククリック（DFG ノードは N4 で扱う）

**処理**:
1. クリックされたノードの `location.startLine` / `location.stopLine` を取得
2. AST ノードの場合は `selectionStore.selectAstNode(id, { start, end })`、CFG ブロックの場合は `selectionStore.selectCfgBlock(id)` を呼び出す
3. `MonacoHighlighter.highlight(start, end, 'highlight-node')` でソース行をハイライト
4. `editor.revealLineInCenter(start)` で Monaco を対象行にスクロールし、`editor.setPosition()` でカーソルを移動する
5. カーソル移動によって N2 が即時発火しないよう、300ms 間 N2 を抑制する

**D3 上の視覚変化**:
- クリックしたノード: `.selected` クラスを付与（太枠・明色）
- 他のノード: `.dimmed` クラスを付与（opacity 0.3）

### 7.2 N2：Monaco カーソル → ダイアグラムノードハイライト

**トリガー**: Monaco の `onDidChangeCursorPosition` イベント（debounce 200ms）

**処理**:
1. N1/N3 によるプログラム的なカーソル移動から 300ms 以内の場合は処理をスキップする
2. カーソル行番号 `line` を取得
3. `LineNodeIndex.lookup(line)` でエントリを検索
4. エントリが見つかった場合:
   - `selectionStore.selectAstNode(entry.nodeId, { start: entry.startLine, end: entry.stopLine })` を呼び出す
   - AST グラフ上で対応ノードに `.selected` を付与、他を `.dimmed` に
   - 対象ノードが折りたたまれていれば自動展開して表示する
   - `d3.zoom().translateTo()` で対象ノードを SVG 中央に移動
   - Monaco の行ハイライトは付与しない。N1/N3 のハイライトが残っている場合は `MonacoHighlighter.clearAll()` で解除する
5. エントリが見つからない場合: 選択をクリア（`selectionStore.clearAll()`）

### 7.3 N3：GO TO / PERFORM 遷移先ジャンプ

**トリガー**: CFG グラフ上の BasicBlock 内に Phase 4 で描画する Statement テキストをクリック
（StatementType が `"GOTO"` / `"PERFORM"` / `"PERFORM_THRU"` / `"PERFORM_LOOP"` のもの）

**処理**:
1. クリックされた StatementNode の StatementType を確認
2. Phase 3 `cfgAdapter` が保持する `D3Node.statements` から対象 Statement の `location` を取得する
3. CFG の Edges から `fromBlockId = 現在ブロック.id` かつ `kind ∈ {GoTo, PerformCall, PerformThruCall}` のエッジを取得
4. `toBlockId` で遷移先 BasicBlock を特定
5. 遷移先ブロックを CFG グラフ上でハイライト（`.selected`）
6. `d3.zoom().translateTo()` で遷移先ブロックを SVG 中央に移動
7. 遷移先ブロックの `Location.startLine` で Monaco もジャンプ
   - `MonacoHighlighter.highlight(startLine, stopLine, 'highlight-jump')`
   - `editor.revealLineInCenter(startLine)`

**複数エッジがある場合**（PERFORM THRU など）:
- 全ての該当エッジの遷移先ブロックを `.impact` クラスでハイライトする

**描画要件**:
- GOTO / PERFORM のクリック可能ラベルは `g.node` 内部ではなく、独立した最前面 SVG レイヤー（例: `navLayer`）に描画する
- `navLayer` のラベル座標は D3 simulation の tick ごとに更新する
- ラベルを最前面に分離し、ノード矩形や隣接ブロックにクリックイベントを横取りされないようにする

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
  private index: LineNodeIndex | null = null;
  private cfg: ControlFlowGraph | null = null;
  private dfg: DataFlowGraph | null = null;
  private programmaticMoveUntil = 0;

  constructor(
    private readonly editor: monaco.editor.IStandaloneCodeEditor,
    private readonly highlighter: MonacoHighlighter,
  ) {}

  // Analyze 実行ごとに最新データへ差し替える
  init(ast: AstNode, cfg: ControlFlowGraph, dfg: DataFlowGraph): void;

  // N1: AST ノードクリック
  onAstNodeClick(nodeId: string, location: SourceLocation): void;

  // N1: CFG ブロッククリック
  onCfgBlockClick(blockId: string, location: SourceLocation | null): void;

  // N2: Monaco カーソル移動（debounce済みで呼び出すこと）
  onCursorMove(line: number): void;

  // N3: GOTO/PERFORM ステートメントクリック
  onGotoStatementClick(fromBlockId: string): void;

  // N4: DFG ノードクリック
  onDfgNodeClick(nodeId: string): void;

  dispose(): void;
}
```

`JumpController` は Monaco Editor と `MonacoHighlighter` に対して一度だけ生成する。
Analyze ボタン押下で AST / CFG / DFG が更新されるたびに `init(ast, cfg, dfg)` を呼び、`LineNodeIndex` とグラフデータを差し替える。
この方式により、Analyze のたびに Monaco のカーソルイベントリスナーを再登録しない。

---

## 10. テスト要件（Vitest）

### LineNodeIndex.test.ts

| テスト名 | 検証内容 |
|----------|---------|
| `build_singleNode_startLineMapped` | 1ノードのツリーで startLine がインデックスに入る |
| `lookup_line_returnsDeepestNode` | 同じ開始行に親子ノードがある場合、深い方が返る |
| `lookup_outOfRange_returnsUndefined` | ノードの開始行に該当しない行番号で undefined が返る |

### SelectionStore.test.ts

| テスト名 | 検証内容 |
|----------|---------|
| `selectAstNode_firesChangeEvent` | `selectAstNode()` 呼び出しで `on('change')` ハンドラが発火する |
| `selectAstNode_setsLineRange` | `selectAstNode(id, range)` で `selectedAstLineRange` が更新される |
| `clearAll_resetsState` | `clearAll()` で全フィールドが初期値に戻る |
| `selectDfgNode_setsImpactClosureIds` | `selectDfgNode(id, ids)` で `impactClosureIds` が更新される |

### JumpController.test.ts

| テスト名 | 検証内容 |
|----------|---------|
| `init_rebuildsLineNodeIndex` | `init(ast, cfg, dfg)` で最新 AST から LineNodeIndex が再構築される |
| `onCursorMove_foundNode_storeUpdated` | カーソル行に対応ノードがあれば SelectionStore が更新される |
| `onCursorMove_noNode_storeCleared` | カーソル行に対応ノードがなければ SelectionStore がクリアされる |
| `onCursorMove_suppressedAfterProgrammaticMove` | N1/N3 後 300ms 間は N2 処理がスキップされる |
| `onGotoStatementClick_setsTargetBlock` | GoTo エッジの toBlockId が SelectionStore の selectedCfgBlockId に入る |
| `onDfgNodeClick_setsImpactClosure` | クリックした DataName の impactClosure が SelectionStore に反映される |

---

## 11. 完了基準

以下をすべて満たした時点で Phase 4 完了とする。

- [ ] AST グラフのノードをクリックすると Monaco の対応行に黄ハイライトが付く
- [ ] Monaco でカーソルを移動すると対応 AST ノードがハイライトされ、他がdimになる
- [ ] CFG グラフの GOTO ラベルをクリックすると遷移先ブロックがハイライトされ Monaco も紫ハイライトで飛ぶ
- [ ] CFG グラフの PERFORM ラベルをクリックすると呼び出し先ブロックがハイライトされる
- [ ] DFG グラフのデータ項目ノードをクリックすると影響閉包ノードがオレンジハイライトされる
- [ ] data-sample.cbl でWS-NUMERIC をクリックするとWS-CHAR（REDEFINES）も影響閉包としてハイライトされる
- [ ] ノード選択解除（背景クリック）で全ハイライトがリセットされる
- [ ] `npm test` が全テストPASS（LineNodeIndex / SelectionStore / JumpController）

---

## 12. 実装上の注意事項

1. **N2 の debounce**: Monaco `onDidChangeCursorPosition` は毎キーストロークで発火するため、`JumpController.onCursorMove` 呼び出しは 200ms の debounce を挟む。

2. **N1/N3 後の N2 抑制**: N1/N3 で `editor.setPosition()` を呼ぶと Monaco の `onDidChangeCursorPosition` が発火する。N1/N3 の Monaco ハイライトが N2 で即時解除されないよう、`JumpController` はプログラム的なカーソル移動後 300ms 間 N2 をスキップする。

3. **AST ノードの一意 ID**: `AstNode.Id` は Phase 1 §6.3 で `"{NodeType}:{StartLine}:{StartColumn}"` 形式として定義済み。`LineNodeIndex` は `AstNode.Id` をそのまま `LineEntry.nodeId` として使用する。

4. **折りたたまれた AST ノードへのナビゲーション**: N2 でカーソル行に対応するノードが折りたたまれた状態（`collapsed = true`）の場合、祖先ノードまで自動展開してから対象ノードをハイライトする。

5. **CFG force-directed の座標**: `d3.zoom().translateTo(zoom, x, y)` で SVG ビューポートを移動する。ノードの現在座標は D3 シミュレーション終了後に `node.x` / `node.y` から取得する。

6. **N3 の複数エッジ**: PERFORM THRU はケースによって複数エッジが生じる。全ての該当エッジを `.impact` でハイライトし、最初の遷移先のみ `translateTo` でスクロールする。

7. **N3 ラベルの SVG レイヤー**: GOTO / PERFORM ラベルは `g.node` 内部ではなく、独立した最前面レイヤー（例: `navLayer`）に描画する。ノード矩形や隣接ブロックがクリックイベントを横取りしないようにするため、simulation tick ごとに `navLayer` のラベル座標を更新する。

8. **AST クリック操作の共存**: Phase 3 §6.1 のとおり、AST ツリーでは単クリックを `onNodeClick`（N1）に使用し、ダブルクリックを `collapsed` 切り替えに使用する。Phase 4 のナビゲーション実装はダブルクリックで折りたたみが壊れず、単クリック時だけ N1 が発火する前提でイベントを接続する。

---

## 13. 参照資料

| 資料 | 参照箇所 | 本仕様との対応 |
|------|---------|--------------|
| `design/specs/phase2-engine.md` v1.4 §5.4 | `DataFlowGraph.ImpactClosure` | §7.4 N4 影響閉包ハイライト |
| `design/specs/phase2-engine.md` §4.2 | `BasicBlock.Location` | §7.3 N3 遷移先ソース行ジャンプ |
| `design/specs/phase2-engine.md` §4.3 | `CfgEdgeKind` 全種別 | §7.3 N3 エッジ種別フィルタリング |
| `design/specs/phase3-visualization.md` v1.4 §9 | `AnalyzeResult` TypeScript 型 | §9 JumpController コンストラクタ引数 |
| `design/specs/phase3-visualization.md` v1.4 §5.1 | `D3Node.statements` / `D3Node.location` | §7.3 N3 Statement クリック |
| `design/specs/phase3-visualization.md` §6.2 | CFG グラフ `.selected` / `.dimmed` | §8 D3 ハイライト CSS |
| `design/brainstorm/phase4-planning.md` | 設計判断メモ | 本仕様全体 |
| `implement/docs/feedback-phase4-spec-deviation.md` | 実装フィードバック | §4 / §5 / §7.3 / §9 / §12 の更新根拠 |
