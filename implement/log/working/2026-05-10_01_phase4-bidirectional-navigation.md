# 2026-05-10_01 Phase 4 実装記録

## 作業概要

双方向ナビゲーション（N1〜N4）を実装した。  
AST ツリー・CFG グラフ・DFG グラフと Monaco エディタが相互に連携し、
クリックやカーソル移動でハイライトが伝播する仕組みを構築した。  
実装期間: 2026-05-09 ～ 2026-05-10

---

## 関連コミット

| コミット | 内容 |
|---------|------|
| `0cf2129` | feat(phase4): 双方向ナビゲーション N1–N4 初期実装 |
| `8631933` | fix(phase4/N1): プログラム的ジャンプ後 300ms N2 抑制 |
| `8d96d2f` | fix(phase4/N2): カーソルナビゲーション時の折りたたみ IF ブランチ自動展開 |
| `dd4cdd3` | fix(phase4/n3): IF ブランチ synthetic ブロックへの GoTo エッジ生成・nav ラベル最前面レイヤー化 |

---

## 実施内容

### アーキテクチャ

| コンポーネント | 役割 |
|-------------|------|
| `SelectionStore` | シングルトン EventBus。選択状態（AST/CFG/DFG ノード ID）を管理し、購読者に通知 |
| `LineNodeIndex` | AstNode ツリーを DFS 走査し、行番号 → ノード ID のマップを構築 |
| `MonacoHighlighter` | `deltaDecorations` ラッパー。`highlight()` / `clearAll()` |
| `JumpController` | N1〜N4 の協調制御。SelectionStore・MonacoHighlighter・エディタを橋渡し |

### N1（ダイアグラム → Monaco）

- AST/CFG/DFG のノードクリック → `JumpController.onAstNodeClick/onCfgBlockClick/onDfgNodeClick`
- Monaco を対応行へスクロール・ハイライト
- `SelectionStore` に選択状態を登録 → 各ダイアグラムが `.selected` / `.dimmed` クラスを適用

### N2（Monaco カーソル → ダイアグラム）

- `onDidChangeCursorPosition` を 200ms デバウンスして `JumpController.onCursorMove`
- `LineNodeIndex.lookup()` で行番号 → AST ノード ID を解決
- `SelectionStore.selectAstNode()` → AstTree が `.selected` / `.dimmed` を更新

### N3（GOTO/PERFORM テキスト → ジャンプ先ブロック）

- CFG の各ブロックに `→ GOTO` / `→ PERFORM` ラベルを描画
- クリック → `JumpController.onGotoStatementClick(fromBlockId)`
- `cfg.edges` から GoTo/PerformCall エッジを検索し、ジャンプ先ブロックを Monaco で紫ハイライト

### N4（DFG ノード → 影響閉包ハイライト）

- DFG ノードクリック → `JumpController.onDfgNodeClick(nodeId)`
- `dfg.impactClosure[nodeId]` の ID セットを `SelectionStore.selectDfgNode()` に渡す
- DfgGraph が `.selected` / `.impact` / `.dimmed` クラスを適用

---

## バグと修正

### N1: ハイライトが ~200ms で消える

**原因**: `editor.setPosition()` が `onDidChangeCursorPosition` を発火 → 200ms デバウンス後に
`onCursorMove` が呼ばれ `highlighter.clearAll()` が実行されてしまう。

**修正**: `suppressN2()` で `programmaticMoveUntil = Date.now() + 300` を設定。
`onCursorMove` はこの期間内にリターンする。

---

### N2: IF 内行（例：`GO TO PROCESS-PARA` の行）をクリックしても認識されない

**原因 1**: `AstBuilder.BuildIf` が IF 内ステートメントを `TrueStatements` / `FalseStatements` のみに
格納し、`Children` に追加していなかった。フロントエンドの DFS は `children` しか走査しないため
`LineNodeIndex` に登録されなかった。

**修正 1**: `AstBuilder.BuildIf` で `TrueStatements.Concat(FalseStatements)` を `Children` に追加。

**原因 2**: IF ノードが初期状態で `collapsed: true` のため、内部ノードが AstTree に描画されておらず
N2 で選択しようとすると「対象なし」として全ノードが dimmed になった。

**修正 2**: `AstTree.applySelection()` で対象ノードが未描画の場合に `expandToNode()` で祖先を
展開してから再描画するフローを追加。

---

### N3: `→ GOTO` クリックが無反応

**原因 1**: `CfgBuilder.BuildInterParagraphEdges` はパラグラフの直接子ステートメントしか処理しないため、
IF ブランチ内の GOTO に対応する GoTo エッジが synthetic ブロックから生成されなかった。

**修正 1**: `BuildIntraParagraphEdges` で synthetic ブロック生成直後に
`BuildInterEdgesForSyntheticBlock()` を呼び、GOTO ステートメントから GoTo エッジを生成。

**原因 2**: `→ GOTO` ラベルを `g.node` グループ内の子要素として描画していたため、
隣接ブロックの `rect` 要素に視覚的に重なり、クリックイベントが rect に横取りされた。
（`click` イベントは発火せず、D3 drag の `pointerdown` 捕捉も絡んでいた）

**修正 2**: nav ラベルを `g.node` 内部ではなく独立した最前面 SVG レイヤー（`navLayer`）に描画し、
simulation tick ごとに座標を更新することで常に最前面に表示。

---

## 新規ファイル

| ファイル | 内容 |
|---------|------|
| `src/frontend/src/store/SelectionStore.ts` | 選択状態 EventBus |
| `src/frontend/src/navigation/LineNodeIndex.ts` | 行番号 → AST ノード ID インデックス |
| `src/frontend/src/navigation/MonacoHighlighter.ts` | Monaco デコレーション管理 |
| `src/frontend/src/navigation/JumpController.ts` | N1–N4 協調制御 |

## 主な変更ファイル

| ファイル | 変更内容 |
|---------|---------|
| `src/backend/CobolAnalyzer.Parser/AstBuilder.cs` | BuildIf: IF 内ステートメントを Children に追加 |
| `src/backend/CobolAnalyzer.Engine/Cfg/CfgBuilder.cs` | synthetic ブロックへの GoTo エッジ生成を追加 |
| `src/frontend/src/components/AstTree.ts` | 自動展開ロジック・store 購読 |
| `src/frontend/src/components/CfgGraph.ts` | N1/N3 クリック・nav ラベル最前面レイヤー |
| `src/frontend/src/components/DfgGraph.ts` | N4 クリック・store 購読 |
| `src/frontend/src/components/Editor.ts` | `getEditor()` 追加 |
| `src/frontend/src/main.ts` | JumpController 配線・コンポーネントライフサイクル管理 |
| `src/frontend/src/styles/main.css` | `.selected` / `.dimmed` / `.impact` / `highlight-*` CSS |
| `src/backend/CobolAnalyzer.Api/Properties/launchSettings.json` | ポートを 5157 → 5000 に変更 |

## テスト

- Backend: 21 件パス（Engine 21、Parser 12）
- Frontend: Vitest 21 件パス（アダプタ・MdiPanel）
- ブラウザ手動確認: N1〜N4 すべて動作確認済み
