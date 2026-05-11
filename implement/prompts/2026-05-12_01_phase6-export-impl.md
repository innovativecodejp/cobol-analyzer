# Phase 6 実装プロンプト：分析機能・エクスポート

仕様: `../design/specs/phase6-export.md`（実装前に必ず全文を読むこと）

---

## 現状確認（実装済み範囲）

Phase 1〜5 は実装・レビュー・push 済み。

主な前提:

- Backend
  - `POST /api/analyze` 実装済み
  - `POST /api/comment/insert` / `preview` / `remove` 実装済み
  - `CobolAnalyzer.Core` / `CobolAnalyzer.Parser` / `CobolAnalyzer.Engine` / `CobolAnalyzer.API` 構成済み
  - `Program.cs` で `JsonStringEnumConverter` 登録済み
  - `Program.cs` で Development CORS 設定済み
- Engine
  - `AnalyzeResult` は現行実装では `CobolAnalyzer.Engine/AnalyzeResult.cs` に存在する
  - `MdiScore` / `MdiRisk` は `CobolAnalyzer.Engine/Metrics/MdiScore.cs` に存在する
  - `ProgramNode.Name` 実装済み
  - `StatementNode.CallTarget` 実装済み
  - 静的 CALL は `StatementType = "CALL"` かつ `CallTarget != null`
  - 動的 CALL は `StatementType = "CALL"` かつ `CallTarget == null`
- Frontend
  - Vite + TypeScript + Monaco Editor + D3.js 実装済み
  - `src/frontend/src/types/analyzeResult.ts` に `AnalyzeResult` / `MdiScore` / `MdiRisk` 型定義あり
  - `src/frontend/src/api/analyzeApi.ts` / `commentApi.ts` は同じ `API_BASE` パターンを使用
  - `main.ts` は AST / CFG / DFG / コメントタブを扱う

直近の確認済み状態:

- `dotnet test src/backend/CobolAnalyzer.sln`: Parser 12 / Engine 39 PASS
- `npm test`: 30 tests PASS
- `npm run build`: PASS

---

## 実装前の注意事項

### 1. 参照元は specs のみ

`implement/` のルールに従い、実装判断の根拠は `design/specs/` のみとする。
`roadmaps` / `brainstorm` は参照しない。

Phase 6 仕様 §12 は `design/docs/` や `design/brainstorm/` を参照資料として列挙しているが、
implement 側では開かない。
必要な戦略判定ロジックや説明文は `phase6-export.md` 本文に記載済みの内容を使う。

仕様の矛盾・未定義事項を見つけた場合は、実装修正を進めず、
`implement/docs/` にフィードバックを記録してユーザーに確認する。

### 2. Core と Engine の依存方向に注意

Phase 6 仕様 §5 は `ProjectAnalyzeResult` を `CobolAnalyzer.Core/Models/` に追加し、
その中で `List<AnalyzeResult>` を持つ例を示している。

ただし現行実装では `AnalyzeResult` は `CobolAnalyzer.Engine` にある。
`Core` から `Engine` を参照すると循環参照になる可能性が高い。

実装開始時に `.csproj` の参照関係を必ず確認する。

- 循環参照なしに仕様どおり実装できる場合はそのまま進める
- 循環参照になる場合は、以下のどちらが妥当か判断できないため実装を止める
  - `AnalyzeResult` を `Core` 側へ移動する
  - `ProjectAnalyzeResult` を `Engine` 側または API DTO 側へ置く

この場合は `implement/docs/feedback-phase6-*.md` に記録して、ユーザーに確認する。

### 3. CALL 解析は `CallTarget` を使う

`CallGraphBuilder` は `StatementNode.Operands` ではなく `StatementNode.CallTarget` を参照する。

- `CallTarget != null`: 静的 CALL としてエッジを作る
- `CallTarget == null`: 動的 CALL として `HasDynamicCall = true`、エッジは作らない

比較用のプログラム名は大文字正規化する。

### 4. Markdown 出力は text/markdown

Export API は JSON ではなく Markdown テキストを返す。

```text
Content-Type: text/markdown; charset=utf-8
```

プログラム名・ファイル名・コメント本文・エラー文を Markdown テーブルへ入れるときは、
`|` などのテーブル破壊文字をエスケープする。

### 5. Phase 6 UI は既存タブを壊さない

Phase 6 では `プロジェクト` タブを追加する。

既存の AST / CFG / DFG / コメントタブ、および単一ソースの Analyze 操作は維持する。
`renderResult()` のエラー表示や再描画で `tab-project` の中身を消さない。

---

## タスク一覧

以下を順に実施する。

---

### タスク 1: 現行依存関係とモデル配置を確認

以下を確認する。

- `CobolAnalyzer.Core.csproj`
- `CobolAnalyzer.Engine.csproj`
- `CobolAnalyzer.API.csproj`
- `CobolAnalyzer.Engine/AnalyzeResult.cs`
- `CobolAnalyzer.Engine/Metrics/MdiScore.cs`

確認結果に基づき、Phase 6 の公開モデル配置を決める。

期待:

- API レスポンス JSON は Phase 6 仕様 §7.1 の形になる
- `MigrationStrategy` は `JsonStringEnumConverter` により文字列で返る
- 既存 `POST /api/analyze` のレスポンス形状は変えない

循環参照が避けられない場合は実装を止め、`implement/docs/` に記録する。

---

### タスク 2: Project / Export 用モデル追加

Phase 6 仕様 §3〜§6 のモデルを追加する。

追加候補:

```text
src/backend/CobolAnalyzer.Core/Models/ProjectAnalyzeRequest.cs
src/backend/CobolAnalyzer.Core/Models/ProjectAnalyzeResult.cs
src/backend/CobolAnalyzer.Core/Models/ExportReportRequest.cs
src/backend/CobolAnalyzer.Core/Models/ExportDesignRequest.cs
src/backend/CobolAnalyzer.Engine/Project/ProgramDependencyGraph.cs
src/backend/CobolAnalyzer.Engine/Project/MigrationRanking.cs
```

最低限定義する型:

```csharp
public class ProjectAnalyzeRequest
{
    public List<CobolSource> Sources { get; init; } = new();
}

public record CobolSource(string FileName, string Source);

public class ProjectAnalyzeResult
{
    public List<AnalyzeResult> Programs { get; init; } = new();
    public ProgramDependencyGraph DependencyGraph { get; init; } = new();
    public MigrationRanking Ranking { get; init; } = new();
    public List<string> Errors { get; init; } = new();
}

public class ExportReportRequest
{
    public string FileName { get; init; } = "program.cbl";
    public string? Source { get; init; }
}

public class ExportDesignRequest
{
    public List<CobolSource> Sources { get; init; } = new();
}
```

依存関係モデル:

```csharp
public class ProgramDependencyGraph
{
    public List<DependencyNode> Nodes { get; init; } = new();
    public List<DependencyEdge> Edges { get; init; } = new();
    public bool HasCycle { get; init; }
    public bool HasDynamicCall { get; init; }
}

public class DependencyNode
{
    public string ProgramName { get; init; } = "";
    public string? FileName { get; init; }
    public MdiScore? Mdi { get; init; }
    public bool IsExternal { get; init; }
    public int FanIn { get; init; }
    public int FanOut { get; init; }
}

public class DependencyEdge
{
    public string CallerProgram { get; init; } = "";
    public string CalleeProgram { get; init; } = "";
    public List<SourceLocation> CallSites { get; init; } = new();
}
```

ランキングモデル:

```csharp
public enum MigrationStrategy
{
    BigBang,
    Incremental,
    StranglerFig,
    NeedsStudy
}

public class MigrationRankingEntry
{
    public int Rank { get; init; }
    public string ProgramName { get; init; } = "";
    public string FileName { get; init; } = "";
    public MdiScore Mdi { get; init; } = new();
    public int LineCount { get; init; }
    public int ParagraphCount { get; init; }
    public int FanIn { get; init; }
    public int FanOut { get; init; }
    public MigrationStrategy Strategy { get; init; }
}

public class MigrationRanking
{
    public List<MigrationRankingEntry> Entries { get; init; } = new();
}
```

注意:

- nullable warning を増やさないよう、文字列は `""` 初期化または nullable にする
- `SourceLocation` は既存 `CobolAnalyzer.Core.Models.SourceLocation` を使う
- `MdiScore` の namespace は現行実装に合わせる

---

### タスク 3: ProjectAnalyzer 実装

`src/backend/CobolAnalyzer.Engine/Project/ProjectAnalyzer.cs` を追加する。

責務:

- 複数 COBOL ソースを解析する
- 各ソースについて既存 `AnalyzeController` と同等の Parse / CFG / DFG / Metrics / MDI を実行する
- `AnalyzeResult` 一覧を作る
- 依存グラフを作る
- 移行ランキングを作る
- プロジェクトレベルのエラーを `Errors` に格納する

推奨 DI:

```csharp
public ProjectAnalyzer(
    CobolParserFacade parser,
    CfgBuilder cfgBuilder,
    DfgBuilder dfgBuilder,
    MdiCalculator mdiCalculator,
    CallGraphBuilder callGraphBuilder,
    MigrationRankingBuilder rankingBuilder)
```

実装方針:

- `POST /api/analyze` の既存挙動を壊さない
- 単一プログラム解析ロジックを共有化する場合も、差分は最小限にする
- 解析失敗したソースは `AnalyzeResult.Errors` に入れ、他ソースの解析は継続する
- `sources.Count > 50` は API Controller で 400 にする
- `ProjectAnalyzer` 内でも 50 超過を検出し、`Errors` に入れる

LineCount:

- `source` を `\n` / `\r\n` 対応で行数計算する

ParagraphCount:

- AST の paragraph 相当ノードを既存 NodeType / Category に合わせて数える
- 現行 AST 構造から判断できない場合は、実装を止めず、最小妥当な方法を選びテストで固定する

---

### タスク 4: CallGraphBuilder 実装

`src/backend/CobolAnalyzer.Engine/Project/CallGraphBuilder.cs` を追加する。

要件:

- 各 `ProgramNode` の `StatementNode` を再帰的に走査する
- `StatementType == "CALL"` の文を対象にする
- `CallTarget != null` の場合、`caller -> callee` のエッジを作る
- 同一 caller / callee の CALL が複数ある場合は 1 エッジに集約し、`CallSites` に全位置を入れる
- `CallTarget == null` の場合、`HasDynamicCall = true` にする
- ソース未提供の callee は外部ノードとして追加する
- `FanIn` / `FanOut` を集計する
- 循環依存があれば `HasCycle = true`
- プログラム名は大文字正規化する

固定:

- `StatementNode.CallTarget` を使う
- 非 CALL 文や `Operands` だけにある値から CALL エッジを作らない
- 動的 CALL はエッジを作らない

50 件超過:

- Phase 6 仕様では `CallGraphBuilderTests.Build_ExceedsMaxNodes_ReturnsError` が要求されている
- 一方で `ProgramDependencyGraph` には `Errors` がない
- 実装前にエラー表現を確認する
- 既存構造で自然に表現できない場合は、`ProjectAnalyzer.Errors` / API 400 をエラー面として扱う
- 仕様どおりのテスト名と整合しないと判断した場合は `implement/docs/` に記録して止める

---

### タスク 5: MigrationRanking 実装

`src/backend/CobolAnalyzer.Engine/Project/MigrationRanking.cs` または専用 builder に、
ランキング生成ロジックを実装する。

推奨クラス:

```csharp
public class MigrationRankingBuilder
{
    public MigrationRanking Build(
        IReadOnlyList<AnalyzeResult> programs,
        ProgramDependencyGraph dependencyGraph,
        IReadOnlyDictionary<string, string> fileNames,
        IReadOnlyDictionary<string, int> lineCounts,
        IReadOnlyDictionary<string, int> paragraphCounts);
}
```

戦略判定:

| 優先順位 | 条件 | 判定戦略 |
|---------|------|---------|
| 1 | MDI >= 75 | `NeedsStudy` |
| 2 | MDI >= 50 または FanIn + FanOut >= 6 | `StranglerFig` |
| 3 | FanIn + FanOut >= 3 または MDI >= 25 | `Incremental` |
| 4 | それ以外 | `BigBang` |

ランキング順:

1. MDI スコア降順
2. FanIn 降順
3. ProgramName 昇順

注意:

- 外部ノードはランキング対象外
- Parse エラーで `Metrics == null` のプログラムもランキング対象外
- `Rank` は 1 始まりで連番

---

### タスク 6: Markdown Export Generator 実装

以下を追加する。

```text
src/backend/CobolAnalyzer.Engine/Export/AnnotationReportGenerator.cs
src/backend/CobolAnalyzer.Engine/Export/MigrationDesignGenerator.cs
```

#### AnnotationReportGenerator

入力:

- `fileName`
- タグ付きコメントを含む COBOL source

処理:

- サーバー側で再解析する
- MDI を算出する
- `CommentTag.TryParse(line)` でタグ付きコメントを抽出する
- Markdown レポートを生成する

出力に含めるもの:

- タイトル: `# COBOL 移行分析レポート：{ProgramName}`
- 生成日: `YYYY-MM-DD`
- ファイル名
- MDI サマリー
- 指標内訳
- 高リスクパターン
- タグ付きコメント一覧
- 移行戦略提案

Parse エラー時:

- HTTP 200 で Markdown を返す
- レポート内にエラー内容を記載する
- 500 にしない

タグ付きコメント:

- 行番号、タグ、値、メッセージを表に出力する
- タグなしの場合は `なし` を表示する

#### MigrationDesignGenerator

入力:

- 複数 COBOL source

処理:

- `ProjectAnalyzer` で全体分析する
- Markdown 設計書を生成する

出力に含めるもの:

- タイトル: `# COBOL 移行設計書`
- 生成日
- 対象プログラム数
- 移行優先度ランキング
- プログラム間依存関係
- 依存関係一覧
- 各プログラム分析サマリー
- タグ付きコメントがある場合は各プログラム最大 3 件を表示する

Markdown helper:

- テーブルセル用 escape 関数を用意する
- `|` は `\|` にする
- 改行は空白に正規化する
- null は空文字または `なし` として扱う

戦略説明文:

| Strategy | 説明文 |
|---------|--------|
| `BigBang` | MDI スコアが低く、プログラム間依存も少ないため、ビッグバン移行が実現可能です。一括置換によるリスクは低いと判断されます。 |
| `Incremental` | 中程度の複雑性または依存関係を持つため、段階的な移行を推奨します。機能単位での順次移行を計画してください。 |
| `StranglerFig` | 高い複雑性または多くのプログラム間依存が存在します。Strangler Fig パターンによる段階的置換が適切です。継ぎ目となる境界を特定してから着手してください。 |
| `NeedsStudy` | MDI スコアが Critical レベルです。構造的複雑性が非常に高く、移行前に詳細な調査が必要です。専門家によるレビューを推奨します。 |

---

### タスク 7: ProjectController / ExportController 追加

以下を追加する。

```text
src/backend/CobolAnalyzer.API/Controllers/ProjectController.cs
src/backend/CobolAnalyzer.API/Controllers/ExportController.cs
```

`Program.cs` に DI 登録する。

```csharp
builder.Services.AddSingleton<CallGraphBuilder>();
builder.Services.AddSingleton<MigrationRankingBuilder>();
builder.Services.AddSingleton<ProjectAnalyzer>();
builder.Services.AddSingleton<AnnotationReportGenerator>();
builder.Services.AddSingleton<MigrationDesignGenerator>();
```

#### ProjectController

```text
POST /api/project/analyze
```

Validation:

- `sources` が null / empty -> 400
- `sources.Count > 50` -> 400
- 各 `fileName` が null / empty / whitespace -> 400
- 各 `source` が null / empty / whitespace -> 400

Response:

- 正常: 200 OK `ProjectAnalyzeResult`
- 個別 parse error は 200 OK の `programs[].errors` に入る
- プロジェクトレベルの recoverable error は `errors` に入る

#### ExportController

```text
POST /api/export/annotation-report
POST /api/export/migration-design
```

Validation:

- annotation-report: `source` が null / empty / whitespace -> 400
- migration-design: `sources` が null / empty -> 400
- migration-design: `sources.Count > 50` -> 400

Markdown response:

```csharp
return Content(markdown, "text/markdown; charset=utf-8");
```

Parse エラー:

- annotation-report は 200 OK
- Markdown 内にエラー内容を含める

---

### タスク 8: Backend xUnit テスト追加

以下を追加する。

```text
tests/CobolAnalyzer.Engine.Tests/CallGraphBuilderTests.cs
tests/CobolAnalyzer.Engine.Tests/MigrationRankingTests.cs
tests/CobolAnalyzer.Engine.Tests/ExportGeneratorTests.cs
```

#### CallGraphBuilderTests

仕様 §9 の以下を実装する。

- `Build_StaticCall_EdgeCreated`
- `Build_DynamicCall_HasDynamicCallTrue`
- `Build_ExternalProgram_IsExternalTrue`
- `Build_CircularCall_HasCycleTrue`
- `Build_FanInFanOut_Correct`
- `Build_ExceedsMaxNodes_ReturnsError`

重視する観点:

- `CALL "PROG-B"` が `CallTarget = "PROG-B"` としてエッジ化される
- 動的 CALL は `HasDynamicCall = true`
- 外部プログラムは `IsExternal = true`
- A -> B -> A で `HasCycle = true`
- FanIn / FanOut が正しい

#### MigrationRankingTests

- `Rank_ByMdiDescending`
- `Strategy_Critical_NeedsStudy`
- `Strategy_HighFanInOut_StranglerFig`
- `Strategy_Low_BigBang`

追加推奨:

- 同点時 FanIn 降順
- FanIn 同点時 ProgramName 昇順

#### ExportGeneratorTests

- `AnnotationReport_ContainsProgramName`
- `AnnotationReport_ContainsTagComments`
- `AnnotationReport_NoTagComments_ShowsNone`
- `MigrationDesign_ContainsRankingTable`
- `MigrationDesign_ContainsDependencySection`

追加推奨:

- Markdown テーブルセルの `|` が escape される
- Parse エラー時にも Markdown が返る

---

### タスク 9: Frontend 型定義追加

`src/frontend/src/types/projectTypes.ts` を追加する。

既存 `src/frontend/src/types/analyzeResult.ts` の型を import して使う。

```typescript
import type { AnalyzeResult, MdiScore, SourceLocation } from './analyzeResult';

export interface CobolSource {
  fileName: string;
  source: string;
}

export interface DependencyNode {
  programName: string;
  fileName: string | null;
  mdi: MdiScore | null;
  isExternal: boolean;
  fanIn: number;
  fanOut: number;
}

export interface DependencyEdge {
  callerProgram: string;
  calleeProgram: string;
  callSites: SourceLocation[];
}

export interface ProgramDependencyGraph {
  nodes: DependencyNode[];
  edges: DependencyEdge[];
  hasCycle: boolean;
  hasDynamicCall: boolean;
}

export type MigrationStrategy =
  | 'BigBang'
  | 'Incremental'
  | 'StranglerFig'
  | 'NeedsStudy';

export interface MigrationRankingEntry {
  rank: number;
  programName: string;
  fileName: string;
  mdi: MdiScore;
  lineCount: number;
  paragraphCount: number;
  fanIn: number;
  fanOut: number;
  strategy: MigrationStrategy;
}

export interface ProjectAnalyzeResult {
  programs: AnalyzeResult[];
  dependencyGraph: ProgramDependencyGraph;
  ranking: { entries: MigrationRankingEntry[] };
  errors: string[];
}

export interface ExportReportRequest {
  fileName: string;
  source: string;
}

export interface ExportDesignRequest {
  sources: CobolSource[];
}
```

---

### タスク 10: projectApi / exportApi 実装と Vitest

以下を追加する。

```text
src/frontend/src/api/projectApi.ts
src/frontend/src/api/exportApi.ts
src/frontend/src/api/exportApi.test.ts
```

`projectApi.ts`:

```typescript
export async function analyzeProject(sources: CobolSource[]): Promise<ProjectAnalyzeResult>;
```

`exportApi.ts`:

```typescript
export async function downloadAnnotationReport(req: ExportReportRequest): Promise<void>;
export async function downloadMigrationDesign(req: ExportDesignRequest): Promise<void>;
```

実装方針:

- `API_BASE` は `analyzeApi.ts` / `commentApi.ts` と同じ
- HTTP エラー時は `throw new Error(\`API error: ${res.status}\`)`
- Markdown レスポンスは `res.text()` で受ける
- Blob を作成し `<a download>` で保存する
- テストしやすいよう `downloadAsFile(content, fileName, mimeType)` を export してもよい

Vitest:

- `downloadAnnotationReport_callsCorrectEndpoint`
- `downloadMigrationDesign_callsCorrectEndpoint`

追加推奨:

- `analyzeProject_callsCorrectEndpoint`
- `downloadAsFile_createsBlobAndAnchor`

---

### タスク 11: Frontend コンポーネント追加

以下を追加する。

```text
src/frontend/src/components/FileDropZone.ts
src/frontend/src/components/DependencyGraph.ts
src/frontend/src/components/RankingTable.ts
```

#### FileDropZone

責務:

- `.cbl` / `.cob` / `.cpy` を含むテキストファイルを選択・ドロップで受け取る
- ファイル名と内容を `CobolSource[]` として保持する
- 追加・削除ができる
- `Analyze Project` ボタンを押せる

最低要件:

- input type=file multiple
- drag/drop
- ファイル一覧表示
- 50 件超過時は UI 上で警告または API エラー表示

#### DependencyGraph

責務:

- `ProgramDependencyGraph` を D3 force layout で表示
- ノードは円
- 矢印付きエッジ
- エッジラベルは CALL 件数
- `d3.zoom()` 対応

色:

| 条件 | 色 |
|------|----|
| 内部 + Critical | `#e74c3c` |
| 内部 + High | `#e67e22` |
| 内部 + Medium | `#f39c12` |
| 内部 + Low | `#27ae60` |
| 外部 | `#808080` |

表示:

- 内部ノード: `PROGRAM-NAME` + MDI スコア
- 外部ノード: `PROGRAM-NAME ?`
- `hasCycle = true` の場合は警告表示
- 循環エッジの厳密特定が過大になる場合は、まずグラフ全体の警告表示を優先する

#### RankingTable

責務:

- `MigrationRankingEntry[]` を `<table>` で表示
- 列: 順位 / プログラム / MDI / リスク / ファンイン / ファンアウト / 推奨戦略
- リスク列は Phase 3 MDI パネルと同じ色を使う
- 推奨戦略の tooltip/title に説明文を入れる

---

### タスク 12: ProjectPanel 相当を統合

仕様では `FileDropZone` / `DependencyGraph` / `RankingTable` を個別追加している。
既存 `CommentPanel` と同様に、統合用コンポーネントを追加してもよい。

推奨:

```text
src/frontend/src/components/ProjectPanel.ts
```

責務:

- ファイル入力
- Analyze Project 実行
- 依存グラフ / ランキングのサブタブ切替
- 注釈レポート DL
- 移行設計書 DL
- エラー表示

注釈レポート DL:

- 単一プログラム用なので、選択中ファイルまたは最初のファイルを対象にする
- UI 上で対象ファイルが分かるようにする

移行設計書 DL:

- 現在の `CobolSource[]` 全体を対象にする

自動 Analyze:

- ファイル読み込み時に自動で project analyze しない
- `Analyze Project` ボタンで明示的に実行する

---

### タスク 13: index.html / main.ts / CSS 統合

#### index.html

タブに `プロジェクト` を追加する。

```html
<button class="tab-btn" data-tab="project">プロジェクト</button>
```

パネルを追加する。

```html
<div id="tab-project" class="tab-panel"></div>
```

#### main.ts

- `ProjectPanel` または各 Phase 6 コンポーネントを import
- アプリ起動時に一度だけ生成する
- `tab-project` を既存 `renderResult()` で消さない
- 既存 AST / CFG / DFG / コメントタブの動作を変えない

#### main.css

Phase 6 UI 用の最小スタイルを追加する。

方針:

- 既存 UI と同じ静かな業務ツール風
- ファイル一覧、グラフ、ランキングテーブル、ダウンロードボタンが読みやすいこと
- カード乱用を避け、タブ内のパネルとして整理する

---

### タスク 14: API 動作確認

バックエンドを起動する。

```powershell
cd src/backend
dotnet run --project CobolAnalyzer.API --launch-profile http
```

#### Project Analyze

2ファイルを送信する。

- `PROG-A` が `CALL "PROG-B"` を含む
- `PROG-B` も sources に含める

期待:

- HTTP 200
- `dependencyGraph.edges` が 1 件以上
- `PROG-A -> PROG-B` が含まれる
- `ranking.entries` が MDI 降順

#### External Program

`PROG-A` が `CALL "EXTERNAL-PROG"` を含むが、sources に `EXTERNAL-PROG` を含めない。

期待:

- 外部ノードが追加される
- `isExternal = true`

#### Dynamic CALL

`CALL WS-PROGRAM-NAME` を含むソースを送信する。

期待:

- `dependencyGraph.hasDynamicCall = true`
- 動的 CALL のエッジは作らない

#### Annotation Report

タグ付きコメント入りソースを送信する。

期待:

- HTTP 200
- Content-Type が `text/markdown`
- Markdown に ProgramName / MDI / 戦略提案 / タグコメント表が含まれる

#### Migration Design

複数ソースを送信する。

期待:

- HTTP 200
- Content-Type が `text/markdown`
- Markdown にランキング表と依存関係セクションが含まれる

---

### タスク 15: Frontend 動作確認

Vite Dev Server を起動する。

```powershell
cd src/frontend
npm run dev
```

確認項目:

- `http://localhost:5173` が開ける
- タブに `プロジェクト` が表示される
- 複数 COBOL ファイルを選択またはドロップできる
- `Analyze Project` で依存グラフが表示される
- ランキングテーブルが MDI 順で表示される
- 外部ノードがグレーで表示される
- `注釈レポートDL` で Markdown ファイルがダウンロードされる
- `移行設計書DL` で Markdown ファイルがダウンロードされる
- 既存の単一 Analyze / AST / CFG / DFG / コメントタブが壊れていない

---

### タスク 16: 最終検証

以下を実行し、全件 PASS を確認する。

```powershell
dotnet test src/backend/CobolAnalyzer.sln
```

```powershell
cd src/frontend
npm test
npm run build
```

期待:

- Backend: 既存 Parser / Engine tests + Phase 6 tests が PASS
- Frontend: 既存 tests + exportApi / projectApi tests が PASS
- `npm run build` はエラーなし
  - Monaco 由来の chunk size warning は許容

---

## 完了確認

仕様 §10 の完了基準をすべて確認する。

```text
- [ ] dotnet test が全テストPASS（CallGraphBuilder / MigrationRanking / ExportGenerator を含む）
- [ ] POST /api/project/analyze に2ファイル（一方が他方をCALLする）を送信して依存グラフのエッジが1件返る
- [ ] POST /api/project/analyze で MDI スコア降順のランキングが返る
- [ ] POST /api/export/annotation-report でMarkdownが返る（ProgramName・MDIスコア・戦略提案を含む）
- [ ] POST /api/export/annotation-report にタグコメント付きソースを渡してテーブルにタグが出力される
- [ ] POST /api/export/migration-design で複数プログラムの設計書Markdownが返る
- [ ] フロントエンドでファイルをドロップして「Analyze Project」を押すと依存グラフが表示される
- [ ] ランキングテーブルに MDI順の一覧が表示される
- [ ] 「注釈レポートDL」ボタンでMarkdownファイルがダウンロードされる
- [ ] 「移行設計書DL」ボタンでMarkdownファイルがダウンロードされる
- [ ] npm test が全テストPASS
```

---

## 仕様との矛盾発見時の対処

実装中に仕様の矛盾・未定義事項を発見した場合:

1. `implement/docs/` にフィードバック内容を記録する
2. 実装を停止してユーザーに報告する
3. ユーザーの指示を待つ
4. `design/specs/` は implement 側で変更しない

特に以下は実装開始時に注意する。

- `ProjectAnalyzeResult` を `Core/Models` に置くと `AnalyzeResult` 参照で循環参照にならないか
- `Build_ExceedsMaxNodes_ReturnsError` のエラー表現が `ProgramDependencyGraph` に存在しない点をどう扱うか
- `ParagraphCount` の算出元が現行 AST で明確か
- 循環依存エッジの赤破線強調をどの粒度まで実装するか
