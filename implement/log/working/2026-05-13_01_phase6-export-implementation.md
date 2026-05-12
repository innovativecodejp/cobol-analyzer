# 2026-05-13_01 Phase 6 分析機能・エクスポート実装記録

## 作業概要

`prompts/2026-05-12_01_phase6-export-impl.md` に従い、`design/specs/phase6-export.md` v1.2 を根拠として Phase 6 を実装した。

複数 COBOL ファイル横断のプロジェクト解析、プログラム間 CALL 依存グラフ、移行優先度ランキング、Markdown 注釈レポート、Markdown 移行設計書、frontend のプロジェクトタブを追加した。

実装中に Core と Parser/Engine の依存方向を確認し、`CobolAnalyzer.Engine` から `CobolAnalyzer.Parser` を直接参照しないよう、Engine 側に `IProjectSourceParser` を置き、API 側の `CobolSourceParser` で `CobolParserFacade` に接続する構成にした。

---

## 関連コミット

| コミット | 内容 |
|---------|------|
| `8febd32` | `feat: implement phase6 export workflow` |

---

## 実施内容

### 1. Backend モデル追加

Core には Engine 型に依存しない request/input DTO のみを追加した。

| ファイル | 内容 |
|---------|------|
| `src/backend/CobolAnalyzer.Core/Models/CobolSource.cs` | COBOL ソース入力 |
| `src/backend/CobolAnalyzer.Core/Models/ProjectAnalyzeRequest.cs` | 複数ファイル解析 request |
| `src/backend/CobolAnalyzer.Core/Models/ExportReportRequest.cs` | 注釈レポート request |
| `src/backend/CobolAnalyzer.Core/Models/ExportDesignRequest.cs` | 移行設計書 request |

Engine 側には Phase 6 の分析結果モデルを追加した。

| ファイル | 内容 |
|---------|------|
| `src/backend/CobolAnalyzer.Engine/Project/ProjectAnalyzeResult.cs` | プロジェクト解析結果 |
| `src/backend/CobolAnalyzer.Engine/Project/ProgramDependencyGraph.cs` | 依存ノード、依存エッジ、循環・動的 CALL フラグ |
| `src/backend/CobolAnalyzer.Engine/Project/MigrationRanking.cs` | ランキング、移行戦略、ランキング生成 |

---

### 2. ProjectAnalyzer / CallGraphBuilder 実装

`ProjectAnalyzer` で複数 COBOL ソースを順次解析し、既存の CFG / DFG / Metrics / MDI 算出を再利用した。

主な仕様対応：

- 個別 parse error は `programs[].errors` に格納し、他ファイルの解析は継続
- `sources.Count > 50` は Controller 側 validation とし、Engine 側では扱わない
- `LineCount` は物理行数として算出
- `ParagraphCount` は `ParagraphNode` かつ `Category == Unit` の AST ノード数として算出
- 外部プログラムは依存グラフには出すがランキング対象外

`CallGraphBuilder` は `StatementNode.CallTarget` のみを CALL 先として扱う。

主な仕様対応：

- `CallTarget != null` は静的 CALL として edge 作成
- `CallTarget == null` は動的 CALL として `HasDynamicCall = true`、edge は作成しない
- 同一 caller / callee は 1 edge に集約し、`CallSites` に位置を蓄積
- 外部 CALL 先は `IsExternal = true`
- `FanIn` / `FanOut` を集計
- CALL グラフの cycle を検出し `HasCycle = true`

---

### 3. Markdown Export 実装

以下を追加した。

| ファイル | 内容 |
|---------|------|
| `src/backend/CobolAnalyzer.Engine/Export/AnnotationReportGenerator.cs` | 単一プログラムの注釈レポート生成 |
| `src/backend/CobolAnalyzer.Engine/Export/MigrationDesignGenerator.cs` | 複数プログラムの移行設計書生成 |
| `src/backend/CobolAnalyzer.Engine/Export/MarkdownEscaper.cs` | Markdown table cell 用 escape |

注釈レポートでは、再解析、MDI 算出、タグ付きコメント抽出、リスクパターン、移行戦略提案を出力する。

移行設計書では、ランキング、依存関係概要、依存関係一覧、各プログラム分析サマリー、タグ付きコメントの一部を出力する。

Markdown テーブルに入る値は `|` と改行を escape / 正規化するようにした。

---

### 4. API 追加

以下の Controller を追加した。

| Endpoint | 内容 |
|----------|------|
| `POST /api/project/analyze` | 複数ファイル一括解析 |
| `POST /api/export/annotation-report` | 注釈レポート Markdown 生成 |
| `POST /api/export/migration-design` | 移行設計書 Markdown 生成 |

Validation：

- `sources` null / empty は 400
- `sources.Count > 50` は 400
- `fileName` blank は 400
- `source` blank は 400
- annotation report の `source` blank は 400

Export API は JSON ではなく以下で返す。

```text
text/markdown; charset=utf-8
```

---

### 5. Backend テスト追加

以下を追加した。

| ファイル | 主な検証 |
|---------|---------|
| `tests/CobolAnalyzer.Engine.Tests/CallGraphBuilderTests.cs` | 静的 CALL、動的 CALL、外部ノード、cycle、FanIn/FanOut |
| `tests/CobolAnalyzer.Engine.Tests/MigrationRankingTests.cs` | MDI 順、戦略判定、ParagraphCount、同点ソート |
| `tests/CobolAnalyzer.Engine.Tests/ExportGeneratorTests.cs` | レポート内容、タグコメント、Markdown escape、設計書セクション |
| `tests/CobolAnalyzer.API.Tests/ProjectControllerTests.cs` | validation で Engine を呼ばないこと、有効 request で呼ぶこと |

`tests/CobolAnalyzer.API.Tests` を新規 xUnit プロジェクトとして追加し、`src/backend/CobolAnalyzer.sln` に登録した。

---

### 6. Frontend 実装

`プロジェクト` タブを追加し、既存 AST / CFG / DFG / コメントタブを維持した。

追加した主なファイル：

| ファイル | 内容 |
|---------|------|
| `src/frontend/src/types/projectTypes.ts` | Phase 6 API 型 |
| `src/frontend/src/api/projectApi.ts` | `POST /api/project/analyze` |
| `src/frontend/src/api/exportApi.ts` | Markdown download API |
| `src/frontend/src/components/FileDropZone.ts` | 複数ファイル選択・drop・一覧・削除 |
| `src/frontend/src/components/DependencyGraph.ts` | D3 force layout の依存グラフ |
| `src/frontend/src/components/RankingTable.ts` | 移行優先度ランキング表 |
| `src/frontend/src/components/ProjectPanel.ts` | Project タブ統合 |

UI では以下を実装した。

- `.cbl` / `.cob` / `.cpy` の複数ファイル選択・drop
- 50 件超過時の UI 側制限表示
- `Analyze Project`
- 依存グラフ / ランキングのサブタブ
- 外部ノードをグレー表示
- リスク別ノード色
- 注釈レポート Markdown download
- 移行設計書 Markdown download

---

## 検証結果

### Backend unit tests

```powershell
dotnet test src/backend/CobolAnalyzer.sln
```

結果：

| テストプロジェクト | 結果 |
|------------------|------|
| `CobolAnalyzer.Parser.Tests` | 12 件 PASS |
| `CobolAnalyzer.Engine.Tests` | 56 件 PASS |
| `CobolAnalyzer.API.Tests` | 3 件 PASS |

合計 71 件 PASS。失敗なし。

---

### Frontend tests / build

この環境では `npm` が PATH に見えなかったため、Visual Studio 同梱の `node.exe` から直接実行した。

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Microsoft\VisualStudio\NodeJs\node.exe" node_modules/vitest/vitest.mjs run
```

結果：

| 項目 | 結果 |
|------|------|
| Vitest | 34 件 PASS |

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Microsoft\VisualStudio\NodeJs\node.exe" node_modules/typescript/bin/tsc
```

結果：PASS。

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Microsoft\VisualStudio\NodeJs\node.exe" node_modules/vite/bin/vite.js build
```

結果：PASS。

Monaco 由来の chunk size warning は出たが、build は成功。

---

### API 動作確認

既存または起動した `CobolAnalyzer.API` を `http://localhost:5000` で確認した。

| 確認項目 | 結果 |
|---------|------|
| 2ファイル解析で `PROG-A -> PROG-B` edge | OK |
| 51ファイル送信で 400 | OK |
| 外部 CALL 先を `IsExternal = true` | OK |
| 動的 CALL で `HasDynamicCall = true` | OK |
| annotation report が `text/markdown; charset=utf-8` | OK |
| annotation report に ProgramName / tag | OK |
| migration design が `text/markdown; charset=utf-8` | OK |
| migration design にランキング / 依存関係セクション | OK |

Frontend は `http://localhost:5173` が 200 応答することを確認した。

---

## Git / Push

以下の commit を作成し、`origin/master` に push 済み。

```text
8febd32 feat: implement phase6 export workflow
```

commit 時、通常権限では `.git/index.lock` 作成が拒否された。
これは Git 管理の破損ではなく、`implement/` が writable root で、実際の `.git/` が親ディレクトリにあるためのサンドボックス権限問題。
権限付きで `git add` / `git commit` / `git push` を実行し、正常完了した。

`git status` は clean。

---

## 注意事項

- `git status` などで `C:\Users\msd-d/.config/git/ignore` の権限警告が出るが、commit / push には影響しなかった。
- 作業後、backend `localhost:5000` と frontend `localhost:5173` は起動状態だった。
- `design/specs/` は implement 側では変更していない。

---

## 残件

- frontend の実ブラウザ操作確認は `http://localhost:5173` で実施可能。
- `npm` が PATH に無い環境では、通常の `npm test` / `npm run build` ではなく Node 実行ファイルを直接指定する必要がある。
