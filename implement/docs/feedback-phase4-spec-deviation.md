# Phase 4 実装フィードバック：仕様との乖離・未定義事項

作成日: 2026-05-10  
作成者: implement 側 Claude Code  
対象仕様: `design/specs/phase4-navigation.md` v1.1、`design/specs/phase2-engine.md` v1.3

Phase 4 双方向ナビゲーション実装中に発見した仕様との乖離および未定義事項を記録する。  
design 側でこの文書を参照し、各 spec を更新すること。

---

## 1. 仕様との乖離（実装が spec と異なる箇所）

### 1-A. `SelectionState` のフィールド名差異

**仕様（phase4-navigation.md §4）**:
```typescript
export interface SelectionState {
  selectedAstNodeId: string | null;
  selectedCfgBlockId: string | null;
  selectedDfgNodeId: string | null;
  highlightedLines: { start: number; end: number } | null;  // ← spec のフィールド名
  impactClosureIds: Set<string>;
}
```

**実装**:
- `highlightedLines` は使用せず、`selectedAstLineRange: { start: number; end: number } | null` として実装した
- ハイライト対象行は `MonacoHighlighter` が直接管理するため、SelectionStore に持たせる必要がなかった

**推奨対応**: spec の `highlightedLines` を `selectedAstLineRange` に改名するか、削除する。

---

### 1-B. `JumpController` のコンストラクタシグネチャ差異

**仕様（phase4-navigation.md §9）**:
```typescript
export class JumpController {
  constructor(
    private index: LineNodeIndex,
    private highlighter: MonacoHighlighter,
    private editor: monaco.editor.IStandaloneCodeEditor,
    private cfg: ControlFlowGraph,
    private dfg: DataFlowGraph,
  ) {}
}
```

**実装**:
```typescript
export class JumpController {
  constructor(
    private readonly editor: monaco.editor.IStandaloneCodeEditor,
    private readonly highlighter: MonacoHighlighter,
  ) {}

  init(ast: AstNode, cfg: ControlFlowGraph, dfg: DataFlowGraph): void { ... }
}
```

`JumpController` は main.ts 起動時に一度だけ生成し、Analyze ボタン押下ごとに `init()` で
データを差し替えるパターンを採用した。  
仕様通りにコンストラクタで全引数を受け取ると、Analyze のたびにインスタンスを再生成する必要があり、
Monaco エディタへのカーソルイベントリスナーも再登録が必要になるため採用しなかった。

**推奨対応**: spec の JumpController コンストラクタを `(editor, highlighter)` + `init(ast, cfg, dfg)` パターンに修正する。

---

### 1-C. `LineNodeIndex.LineEntry` のフィールド差異

**仕様（phase4-navigation.md §5）**:
```typescript
export interface LineEntry {
  nodeId: string;
  nodeType: string;      // ← spec にあるが実装では省略
  category: NodeCategory; // ← spec にあるが実装では省略
  location: SourceLocation; // ← spec にあるが実装では省略
  depth: number;
}
```

**実装**:
```typescript
export interface LineEntry {
  nodeId: string;
  startLine: number;
  stopLine: number;
  depth: number;
}
```

N2 の実装上、`nodeType`・`category`・`location` は不要だった（`nodeId` と行範囲のみ使用）。

**推奨対応**: spec の `LineEntry` を実装に合わせて簡略化する。

---

## 2. 仕様の未定義事項（spec に記載がなく実装で判断した箇所）

### 2-A. N1 → N2 の干渉抑制パターン

**状況**: N1（ノードクリック）で `editor.setPosition()` を呼ぶと Monaco の
`onDidChangeCursorPosition` が即座に発火し、200ms デバウンス後に N2 が実行されて
Monaco ハイライトが消去されてしまう。

**実装での対処**: `JumpController` に `programmaticMoveUntil` タイムスタンプを持たせ、
N1/N3 実行時に 300ms 間 N2 を抑制する。

**推奨対応**: phase4-navigation.md §12 に「N1/N3 実行後 300ms 間は N2 をスキップする」旨を追記する。

---

### 2-B. `AstBuilder.BuildIf` の `Children` への追加

**状況**: `AstBuilder.BuildIf` が IF 内ステートメントを `TrueStatements`/`FalseStatements` のみに
格納し `Children` に追加していなかった。  
フロントエンドの `LineNodeIndex` は `children` のみを DFS 走査するため、
IF ブランチ内の行が N2 ナビゲーションで検出できなかった。

**実装での対処**: `AstBuilder.BuildIf` で `TrueStatements.Concat(FalseStatements)` の各ノードを
`node.Children` に追加するよう修正した。

**推奨対応**: phase2-engine.md §4.1（StatementNode / IF ノードの定義）に
「TrueStatements・FalseStatements の要素は Children にも追加すること」を明記する。

---

### 2-C. `CfgBuilder` の IF ブランチ synthetic ブロックへの GoTo エッジ生成

**状況**: `CfgBuilder.BuildInterParagraphEdges` はパラグラフの直接子ステートメントしか処理しない。
IF ブランチ内の GOTO（例: `IF flag GO TO PARA-X END-IF`）は synthetic ブロックに格納されるが、
その synthetic ブロックからの GoTo エッジが生成されなかった。  
結果として N3 でクリックしても対応エッジが見つからず動作しなかった。

**実装での対処**: `BuildIntraParagraphEdges` で synthetic ブロック生成直後に
`BuildInterEdgesForSyntheticBlock()` を呼び、GOTO ステートメントから GoTo エッジを生成した。

**推奨対応**: phase2-engine.md §4.2（CfgBuilder の IF 処理）に
「IF ブランチの synthetic ブロック内に GOTO がある場合、そのブロックから対象パラグラフへの
GoTo エッジも生成すること」を明記する。

---

### 2-D. N3 nav ラベルの SVG レンダリング方式

**状況**: GOTO/PERFORM ラベルを `g.node` グループの子要素として描画すると、
force-directed レイアウトでノードが密集した際に隣接ブロックの `rect` 要素に重なり、
クリックイベントが rect に横取りされてラベルのクリックが動作しなかった。

**実装での対処**: nav ラベルを `g.node` 内部ではなく独立した最前面 SVG レイヤー（`navLayer`）に
描画し、simulation tick ごとに座標を更新することで常に最前面に表示。

**推奨対応**: phase4-navigation.md §7.3 に
「GOTO/PERFORM ラベルは g.node 内部ではなく独立した最前面レイヤーに描画すること」を追記する。

---

## 3. 参考：影響を受ける仕様書

| 仕様書 | 対象セクション | フィードバック番号 |
|--------|--------------|-----------------|
| `phase4-navigation.md` | §4 SelectionState | 1-A |
| `phase4-navigation.md` | §9 JumpController | 1-B |
| `phase4-navigation.md` | §5 LineNodeIndex | 1-C |
| `phase4-navigation.md` | §12 実装上の注意事項 | 2-A, 2-D |
| `phase2-engine.md` | §4.1 StatementNode/IF | 2-B |
| `phase2-engine.md` | §4.2 CfgBuilder IF 処理 | 2-C |
