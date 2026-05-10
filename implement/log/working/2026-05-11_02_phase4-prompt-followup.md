# 2026-05-11_02 Phase 4 プロンプト追補レビュー・修正記録

## 作業概要

`prompts/2026-05-09_02_phase4-navigation-impl.md` の変更内容に対して、
現在の Phase 4 双方向ナビゲーション実装が `design/specs/phase4-navigation.md` v1.2 と整合しているかをレビューした。

レビュー後、N1 / N2 / N3 / N4 の仕様差分とテスト不足を修正し、
フロントエンド・バックエンド双方の自動テスト、ビルド、API 疎通を確認した。

関連コミット:

| コミット | 内容 |
|---------|------|
| `43fca18` | `fix: align phase4 navigation behavior` |

---

## 参照した仕様・プロンプト

| 種別 | ファイル |
|------|----------|
| 仕様 | `../design/specs/phase4-navigation.md` |
| 実装プロンプト | `prompts/2026-05-09_02_phase4-navigation-impl.md` |
| 前提仕様 | `../design/specs/phase3-visualization.md` |

`implement/` のルールに従い、実装判断の根拠は `design/specs/` と実装プロンプトに限定した。
`roadmaps` / `brainstorm` は参照していない。

---

## レビュー結果

### 1. SelectionStore

実装済みの `SelectionStore` は Phase 4 仕様の状態項目と概ね一致していた。

- `selectedAstNodeId`
- `selectedAstLineRange`
- `selectedCfgBlockId`
- `selectedDfgNodeId`
- `impactClosureIds`

ただし、テスト名と検証粒度が仕様 §10 の 4 件と一致していなかったため、
テストを仕様項目に合わせて整理した。

### 2. LineNodeIndex

`LineNodeIndex` は仕様どおり `AstNode.id` を `LineEntry.nodeId` として保持し、
同一開始行では深いノードを優先していたため、修正不要と判断した。

### 3. JumpController

実装済みの N1 / N2 / N3 / N4 の基本動作は入っていたが、
仕様 §10 のテスト要件に対して不足があった。

不足していた観点:

- `init()` による LineNodeIndex 再構築
- `onCursorMove()` で該当ノードなしの場合の `clearAll`
- N1/N3 後 300ms の N2 抑制

これらを `JumpController.test.ts` に追加した。

### 4. AstTree

Phase 4 prompt では、AST 操作は以下の割り当てになっている。

- 単クリック: N1（AST ノード → Monaco 行ジャンプ）
- ダブルクリック: 折りたたみ / 展開

直前の Phase 3 追補対応では単クリックで N1 と折りたたみを同時に実行していたため、
Phase 4 prompt に合わせて分離した。

また、N2 で選択された AST ノードを D3 上で中央へ移動する処理を追加した。

### 5. CfgGraph

N3 の複数遷移先表示について、仕様では以下を求めている。

- 最初の遷移先: `.selected`
- 複数遷移先の残り: `.impact`

既存実装では `selectionStore.selectedCfgBlockId` による単一選択のみで、
複数遷移先の `.impact` 表示が不足していた。

`CfgGraph` 内に `impactBlockIds` を持たせ、Statement ラベルクリック時に対象エッジの遷移先を抽出し、
最初以外を `.impact` として表示できるようにした。

あわせて、選択された CFG ブロックを D3 上で中央へ移動する処理を追加した。

### 6. main.ts

解析エラー時または API 呼び出し失敗時に、前回の D3 / Monaco 選択状態が残る可能性があった。

`renderResult()` のエラー分岐、および Analyze 失敗時の `catch` で `selectionStore.clearAll()` を呼び、
表示状態をリセットするようにした。

---

## 修正ファイル

| ファイル | 内容 |
|---------|------|
| `src/frontend/src/components/AstTree.ts` | 単クリック N1 / ダブルクリック collapse に分離、選択ノード中央移動を追加 |
| `src/frontend/src/components/AstTree.test.ts` | 単クリックとダブルクリックの責務分離をテスト |
| `src/frontend/src/components/CfgGraph.ts` | N3 複数遷移先 `.impact` 表示、選択ブロック中央移動を追加 |
| `src/frontend/src/main.ts` | 解析エラー時・API 失敗時に SelectionStore をクリア |
| `src/frontend/src/navigation/JumpController.test.ts` | `init` 再構築、該当なし clear、N2 抑制のテストを追加 |
| `src/frontend/src/store/SelectionStore.test.ts` | Phase 4 仕様 §10 のテスト粒度に整理 |

---

## 検証結果

### フロントエンド

```text
npm test
Test Files: 8 passed (8)
Tests: 27 passed (27)
```

```text
npm run build
tsc && vite build
PASS
```

Monaco Editor 由来の大きな chunk 警告は出るが、既知の警告でありビルド失敗ではない。

### バックエンド

```text
dotnet test src/backend/CobolAnalyzer.sln
Parser.Tests: 12 passed
Engine.Tests: 26 passed
```

ANTLR 生成コード由来の `CS3021` warning は既存の警告。

### 差分チェック

```text
git diff --check
PASS
```

### Dev Server / API 疎通

確認時点で以下が 200 応答。

| URL | 結果 |
|-----|------|
| `http://localhost:5173` | 200 |
| `http://localhost:5000/swagger` | 200 |

### `/api/analyze` サンプル確認

| サンプル | 確認内容 | 結果 |
|---------|----------|------|
| `goto-sample.cbl` | `GoTo` 4件、`PerformThruCall` 1件、CFG block 7件 | OK |
| `data-sample.cbl` | `Redefines` 1件、`impactClosure` に `WS-BUFFER.WS-NUMERIC -> WS-BUFFER.WS-CHAR` | OK |

---

## 完了判断

Phase 4 prompt 変更に対する実装レビュー・修正は完了。

今回の修正は Phase 4 完了基準への追随であり、仕様の矛盾や未定義事項は発見していない。
そのため `implement/docs/` への新規フィードバック記録は不要と判断した。

---

## 残留リスク

- 自動テスト、ビルド、API 疎通は確認済み。
- ブラウザ上での実クリック操作確認は未実施。
- D3 force layout の座標は simulation の進行状況に依存するため、中央移動はノード座標が確定しているタイミングで最も安定する。
- `data-sample.cbl` の DFG ノード ID は階層付きの `WS-BUFFER.WS-NUMERIC` / `WS-BUFFER.WS-CHAR` 形式であり、UI 上の表示名 `WS-NUMERIC` / `WS-CHAR` とは異なる。
