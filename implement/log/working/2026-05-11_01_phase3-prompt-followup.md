# 2026-05-11_01 Phase 3 プロンプト追補レビュー・修正記録

## 作業概要

`prompts/2026-05-09_01_phase3-visualization-impl.md` の変更内容に対して、
現在の Phase 3 実装が `design/specs/phase3-visualization.md` v1.3 と整合しているかをレビューした。

レビュー後、Phase 4 実装で上書きされた Phase 3 仕様との差分を修正し、
フロントエンド・バックエンド双方のテストと API 疎通を確認した。

関連コミット:

| コミット | 内容 |
|---------|------|
| `1183736` | `fix: align phase3 ast interaction` |

---

## 参照した仕様・プロンプト

| 種別 | ファイル |
|------|----------|
| 仕様 | `../design/specs/phase3-visualization.md` |
| 実装プロンプト | `prompts/2026-05-09_01_phase3-visualization-impl.md` |
| Phase 4 仕様 | `../design/specs/phase4-navigation.md` |

`implement/` のルールに従い、実装判断の根拠は `design/specs/` と実装プロンプトに限定した。
`roadmaps` / `brainstorm` は参照していない。

---

## レビュー結果

### 1. Phase 3 型定義・アダプター

以下は Phase 3 v1.3 / Phase 4 前提と整合していたため、修正不要と判断した。

- `AstNode.id`
- `CfgBlock.statements`
- `CfgBlock.location`
- `CfgEdge.isRecursive`
- `DataFlowGraph.impactClosure`
- `MetricsResult.ccPerParagraph`
- `cfgAdapter` による `D3Node.statements` / `D3Node.location` の保持

### 2. CORS / API ベース URL

以下は仕様と整合していた。

- `CobolAnalyzer.API/Program.cs`
  - Development 環境のみ `AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()`
  - `app.UseCors("DevCors")` も Development 条件下
- `src/frontend/.env.development`
  - `VITE_API_BASE=http://localhost:5000`

### 3. AST ツリーの折りたたみ操作

Phase 3 仕様では「AST ノードをクリックして折りたたみ・展開」と定義されている。
しかし Phase 4 ナビゲーション実装後、`AstTree` は以下の状態になっていた。

- `click`: ソース行ジャンプ用ハンドラのみ
- `dblclick`: 折りたたみ・展開

これは Phase 4 のクリックナビゲーションを追加した際に、Phase 3 完了基準からずれたもの。
Phase 4 のソースジャンプも維持しながら、同一クリックで折りたたみ・展開も行うよう修正した。

### 4. MDI スコア表示サイズ

Phase 3 仕様では MDI スコア表示を `2rem` としている。
実装は `1.6rem` になっていたため、仕様に戻した。

### 5. AST ツリー操作のテスト不足

Phase 3 の Vitest 要件はアダプターと MDI パネル中心で、AstTree コンポーネント操作の回帰テストは存在していなかった。
今回の差分を再発させないため、クリックで以下を検証するテストを追加した。

- クリックハンドラが呼ばれること
- 対象ノードの `collapsed` が切り替わること
- 子ノードが描画から消えること

---

## 修正ファイル

| ファイル | 内容 |
|---------|------|
| `src/frontend/src/components/AstTree.ts` | ノードクリック時に Phase 4 の `onNodeClick` 呼び出し後、Phase 3 の collapse toggle も実行 |
| `src/frontend/src/components/AstTree.test.ts` | AST ノードクリックの collapse / handler 呼び出し回帰テストを追加 |
| `src/frontend/src/styles/main.css` | `.mdi-score` を `font-size: 2rem` に修正 |

---

## 検証結果

### フロントエンド

```text
npm test
Test Files: 8 passed (8)
Tests: 22 passed (22)
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
| `hello.cbl` | AST / CFG / DFG / metrics が返る | OK |
| `goto-sample.cbl` | `GoTo` / `ConditionalTrue` / `ConditionalFalse` / `PerformThruCall` エッジが返る | OK |
| `data-sample.cbl` | `Redefines` / `GroupOf` エッジ、group node が返る | OK |

---

## 完了判断

Phase 3 prompt 変更に対する実装レビュー・修正は完了。

今回の修正は Phase 3 完了基準への追随であり、仕様の矛盾や未定義事項は発見していない。
そのため `implement/docs/` への新規フィードバック記録は不要と判断した。

---

## 残留リスク

- 今回は HTTP/API 疎通と自動テストを中心に確認した。
- ブラウザ上での手動操作確認は、過去の Phase 3 実装ログで実施済みだが、今回修正後の再操作確認は限定的。
- 追加した `AstTree.test.ts` により、少なくともクリックによる collapse と handler 呼び出しは回帰検出できる。
