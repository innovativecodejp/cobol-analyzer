# Phase 6 仕様：分析機能・エクスポート

バージョン: 1.2
作成日: 2026-05-05  
更新日: 2026-05-12（実装フィードバック反映: ProjectAnalyzeResult の Engine 配置・50件超過 validation・ParagraphCount 算出根拠を明確化）
ステータス: 確定（implement/ への引き渡し可）

前提:
- `design/specs/phase2-engine.md` の実装が完了し、MDI算出が稼働していること
- `design/specs/phase5-comment.md` の実装が完了し、タグ付きコメントが使用可能なこと

---

## 1. 目的・スコープ

複数COBOLファイルを横断した依存グラフ・移行優先度ランキングを提供し、
分析結果をMarkdown形式の注釈レポート・移行設計書としてエクスポートする。
本フェーズでPhase 1〜5 の全機能が統合される。

### スコープ内
- プログラム間依存グラフ（静的CALL解析）
- 移行優先度ランキング（MDIスコア順・移行戦略提案付き）
- 注釈レポート生成（単一プログラム、Markdown）
- 移行設計書生成（複数プログラム横断、Markdown）
- REST API 3エンドポイント
- フロントエンド：マルチファイル入力・依存グラフ・ランキングテーブル・ダウンロードボタン

### スコープ外
- ファイルシステムへの直接保存
- PDF出力
- 動的CALL（変数による呼び出し先）の解析
- プログラム数50超の大規模プロジェクト（API validation で 400 Bad Request を返す）

---

## 2. プロジェクト構造追加分

```
implement/
├── src/
│   └── backend/
│       ├── CobolAnalyzer.Engine/
│       │   ├── Project/                          ← 新規
│       │   │   ├── ProjectAnalyzer.cs
│       │   │   ├── ProjectAnalyzeResult.cs
│       │   │   ├── CallGraphBuilder.cs
│       │   │   ├── ProgramDependencyGraph.cs
│       │   │   └── MigrationRanking.cs
│       │   └── Export/                           ← 新規
│       │       ├── AnnotationReportGenerator.cs
│       │       └── MigrationDesignGenerator.cs
│       ├── CobolAnalyzer.Core/
│       │   └── Models/
│       │       ├── ProjectAnalyzeRequest.cs      ← 追加
│       │       ├── CobolSource.cs                ← 追加
│       │       ├── ExportReportRequest.cs        ← 追加
│       │       └── ExportDesignRequest.cs        ← 追加
│       └── CobolAnalyzer.API/
│           └── Controllers/
│               ├── ProjectController.cs          ← 追加
│               └── ExportController.cs           ← 追加
└── tests/
    ├── CobolAnalyzer.Engine.Tests/
        ├── CallGraphBuilderTests.cs              ← 追加
        ├── MigrationRankingTests.cs              ← 追加
        └── ExportGeneratorTests.cs               ← 追加
    └── CobolAnalyzer.API.Tests/
        └── ProjectControllerTests.cs             ← 追加

src/frontend/src/
├── components/
│   ├── FileDropZone.ts                           ← 追加
│   ├── DependencyGraph.ts                        ← 追加
│   └── RankingTable.ts                           ← 追加
├── api/
│   ├── projectApi.ts                             ← 追加
│   └── exportApi.ts                              ← 追加
└── types/
    └── projectTypes.ts                           ← 追加
```

モデル配置方針:
- `CobolAnalyzer.Core` は Engine 型に依存しない request/input DTO のみを置く。
- `ProjectAnalyzeResult` は `AnalyzeResult` / `ProgramDependencyGraph` / `MigrationRanking` を含むため、`CobolAnalyzer.Engine/Project` に置く。
- `CobolAnalyzer.API` は Core の request DTO を受け取り、Engine の result model を JSON 応答として返す。
- `Core -> Engine` 参照は作らない。`Engine -> Core` と `API -> Core/Engine` の依存方向を維持する。

---

## 3. プログラム間依存グラフ

### 3.1 形式モデル

```
G_CALL = (V, E)
V : DependencyNode 集合（プログラム単位）
E : DependencyEdge 集合（静的CALL関係）
```

### 3.2 データモデル

```csharp
// ProgramDependencyGraph.cs
public class ProgramDependencyGraph
{
    public List<DependencyNode> Nodes { get; init; } = new();
    public List<DependencyEdge> Edges { get; init; } = new();
    public bool HasCycle { get; init; }        // 循環CALL が存在する
    public bool HasDynamicCall { get; init; }  // 動的CALL（変数）が存在する
}

public class DependencyNode
{
    public string ProgramName { get; init; }
    public string? FileName { get; init; }     // null = 外部プログラム（ソース未提供）
    public MdiScore? Mdi { get; init; }        // null = 外部プログラム
    public bool IsExternal { get; init; }      // true = ソース未提供
    public int FanIn { get; init; }            // 被依存数（何プログラムからCALLされているか）
    public int FanOut { get; init; }           // 依存数（何プログラムをCALLしているか）
}

public class DependencyEdge
{
    public string CallerProgram { get; init; }
    public string CalleeProgram { get; init; }
    public List<SourceLocation> CallSites { get; init; } = new();  // CALL文の位置一覧
}
```

### 3.3 CallGraphBuilder の構築ルール

1. 各プログラムの PROCEDURE DIVISION を走査し、`StatementType = "CALL"` の StatementNode を検出する
2. `StatementNode.CallTarget` が非 null の場合、その値を呼び出し先プログラム名とする（Phase 2 §3.1。大文字正規化済み）
3. `StatementNode.CallTarget` が null の場合（変数CALL）は `HasDynamicCall = true` を設定しエッジを生成しない
4. 検出した (caller, callee) ペアで `DependencyEdge` を生成し、CALL 文の `StatementNode.Location` を `CallSites` に追加する
5. 全ノードの `FanIn` / `FanOut` を集計する
6. DFS/BFS でサイクル検出を行い、存在する場合は `HasCycle = true` を設定する
7. 入力プログラム数の 50 件上限は `ProjectController` で検証する。`CallGraphBuilder` は検証済み入力を前提とし、ノード数超過エラーは返さない

---

## 4. 移行優先度ランキング

### 4.1 MigrationRankingEntry

```csharp
public class MigrationRankingEntry
{
    public int Rank { get; init; }
    public string ProgramName { get; init; }
    public string FileName { get; init; }
    public MdiScore Mdi { get; init; }
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

`LineCount` は `CobolSource.Source` を改行で分割した物理行数とする。
`ParagraphCount` は解析済み `AnalyzeResult.Ast` 配下の AST ノードのうち、`NodeType == "Paragraph"` かつ `Category == Unit` のノード数とする。
ソース未提供の外部プログラムノードはランキング対象外とし、`ParagraphCount` を算出しない。

### 4.2 移行戦略（MigrationStrategy）

`design/docs/CobolStructureAnalysis.md` §5.2.2「戦略選択」に基づく。

```csharp
public enum MigrationStrategy
{
    BigBang,      // ビッグバン移行
    Incremental,  // 段階的移行
    StranglerFig, // Strangler Fig パターン
    NeedsStudy    // 詳細調査が必要
}
```

### 4.3 戦略判定ロジック

| 優先順位 | 条件 | 判定戦略 |
|---------|------|---------|
| 1 | MDI ≥ 75（Critical） | `NeedsStudy` |
| 2 | MDI ≥ 50（High以上）または FanIn+FanOut ≥ 6 | `StranglerFig` |
| 3 | FanIn+FanOut ≥ 3 または MDI ≥ 25（Medium以上） | `Incremental` |
| 4 | それ以外 | `BigBang` |

### 4.4 ランキング順序

1. MDI スコア 降順
2. 同点の場合は FanIn 降順
3. さらに同点の場合はプログラム名 昇順

---

## 5. ProjectAnalyzer

```csharp
// CobolAnalyzer.Engine/Project/ProjectAnalyzer.cs
public class ProjectAnalyzer
{
    // 複数COBOLソースを受け取り、全プログラムの分析結果と依存グラフ・ランキングを返す
    public ProjectAnalyzeResult Analyze(IReadOnlyList<CobolSource> sources);
}

// CobolAnalyzer.Core/Models/ProjectAnalyzeRequest.cs
public class ProjectAnalyzeRequest
{
    public List<CobolSource> Sources { get; init; } = new();
}

// CobolAnalyzer.Core/Models/CobolSource.cs
public record CobolSource(string FileName, string Source);

// CobolAnalyzer.Engine/Project/ProjectAnalyzeResult.cs
public class ProjectAnalyzeResult
{
    public List<AnalyzeResult> Programs { get; init; } = new();    // 各プログラムの分析結果
    public ProgramDependencyGraph DependencyGraph { get; init; }
    public MigrationRanking Ranking { get; init; }
    public List<string> Errors { get; init; } = new();             // プロジェクトレベルのエラー
}
```

`ProjectAnalyzeResult` は Engine 層の分析結果モデルとする。
`AnalyzeResult` は Phase 2 で `CobolAnalyzer.Engine/AnalyzeResult.cs` に配置され、CFG / DFG / Metrics 型を参照するため、Core 層へ移動しない。
Core 層に置くのは `ProjectAnalyzeRequest` / `CobolSource` / `ExportReportRequest` / `ExportDesignRequest` のように Engine 型を参照しない DTO のみとする。

入力 validation:
- `sources` が空、または 50 件を超える場合、`ProjectController` が `400 Bad Request` を返し、`ProjectAnalyzer` / `CallGraphBuilder` は呼び出さない。
- `ProjectAnalyzeResult.Errors` は、validation 通過後にプロジェクト分析中に発生したプロジェクトレベルのエラーを格納する。

---

## 6. Markdown エクスポート

### 6.1 注釈レポート（AnnotationReportGenerator）

**入力**: COBOLソースコード（タグ付きコメント挿入済み）  
**処理**: サーバーが再解析（Parse + Analyze + CommentTag 抽出）  
**出力**: Markdown テキスト

#### レポート構成

```markdown
# COBOL 移行分析レポート：{ProgramName}

生成日: {YYYY-MM-DD}
ファイル名: {fileName}

---

## MDI サマリー

| 指標 | スコア | リスクランク |
|------|--------|------------|
| 総合MDI | {score:.1f} | {risk} |

## 指標内訳

| 指標ID | 指標名 | 実測値 | 寄与スコア |
|--------|--------|--------|-----------|
| CC | サイクロマティック複雑度 | {cc} | {contribution:.2f} |
| GD | GO TO 密度 | {gd:.3f} | {contribution:.2f} |
| AD | ALTER 文数 | {ad} | {contribution:.2f} |
| ND | ネスト深度 | {nd} | {contribution:.2f} |
| RD | REDEFINES 密度 | {rd:.3f} | {contribution:.2f} |
| CS | スコープ横断依存数 | {cs} | {contribution:.2f} |

## 高リスクパターン

{リスクパターンの箇条書き（検出されたもののみ）}
- GO TO 文が {n} 件存在します（非構造化制御フロー）
- ALTER 文が {n} 件存在します（動的制御フロー変更・高リスク）
- REDEFINES が {n} 件存在します
- ネスト深度が {nd} 階層あります

## タグ付きコメント一覧

| 行番号 | タグ | 値 | メッセージ |
|--------|-----|---|-----------|
{タグコメントの行}

（タグ付きコメントがない場合は「なし」と表示）

## 移行戦略提案

**判定**: {Strategy の日本語名}

{戦略の説明文}
```

#### 移行戦略の説明文テンプレート

| Strategy | 説明文 |
|---------|--------|
| `BigBang` | MDI スコアが低く、プログラム間依存も少ないため、ビッグバン移行が実現可能です。一括置換によるリスクは低いと判断されます。 |
| `Incremental` | 中程度の複雑性または依存関係を持つため、段階的な移行を推奨します。機能単位での順次移行を計画してください。 |
| `StranglerFig` | 高い複雑性または多くのプログラム間依存が存在します。Strangler Fig パターンによる段階的置換が適切です。継ぎ目となる境界を特定してから着手してください。 |
| `NeedsStudy` | MDI スコアが Critical レベルです。構造的複雑性が非常に高く、移行前に詳細な調査が必要です。専門家によるレビューを推奨します。 |

### 6.2 移行設計書（MigrationDesignGenerator）

**入力**: 複数COBOLソースコード（タグ付きコメント挿入済み）  
**処理**: ProjectAnalyzer で全プログラムを分析  
**出力**: Markdown テキスト

#### 設計書構成

```markdown
# COBOL 移行設計書

生成日: {YYYY-MM-DD}
対象プログラム数: {n}

---

## 移行優先度ランキング

| 順位 | プログラム名 | ファイル名 | MDI | リスク | ファンイン | ファンアウト | 推奨戦略 |
|------|------------|-----------|-----|--------|----------|------------|--------|
| 1 | ... | ... | ... | ... | ... | ... | ... |

## プログラム間依存関係

{依存グラフの概要説明}
- 総プログラム数: {n}
- CALL エッジ数: {m}
- 循環依存: {あり/なし}
- 動的CALL（解析不能）: {あり/なし}

### 依存関係一覧

| 呼び出し元 | 呼び出し先 | CALL箇所数 |
|-----------|-----------|-----------|

## 各プログラム分析サマリー

### {ProgramName}
- **ファイル**: {fileName}
- **MDI**: {score:.1f}（{risk}）
- **推奨戦略**: {strategy}
- **行数**: {lineCount} / **パラグラフ数**: {paragraphCount}
- **主要指標**: CC={cc}, GD={gd:.3f}, AD={ad}, ND={nd}

{タグ付きコメントがある場合は最初の3件を表示}

---
```

### 6.3 ExportRequest モデル

```csharp
// CobolAnalyzer.Core/Models/ExportReportRequest.cs（単一プログラム注釈レポート）
public class ExportReportRequest
{
    public string FileName { get; init; } = "program.cbl";
    public string Source { get; init; }   // タグコメント挿入済みソース
}

// CobolAnalyzer.Core/Models/ExportDesignRequest.cs（移行設計書）
public class ExportDesignRequest
{
    public List<CobolSource> Sources { get; init; } = new();
}
```

---

## 7. REST API

### 7.1 複数ファイル一括解析

```
POST /api/project/analyze
Content-Type: application/json
```

**リクエスト Body**

```json
{
  "sources": [
    { "fileName": "PROG-A.cbl", "source": "..." },
    { "fileName": "PROG-B.cbl", "source": "..." }
  ]
}
```

**レスポンス**

```json
{
  "programs": [
    { "ast": {...}, "cfg": {...}, "dfg": {...}, "metrics": {...}, "errors": [] }
  ],
  "dependencyGraph": {
    "nodes": [
      { "programName": "PROG-A", "fileName": "PROG-A.cbl", "mdi": {...}, "isExternal": false, "fanIn": 0, "fanOut": 1 },
      { "programName": "PROG-B", "fileName": "PROG-B.cbl", "mdi": {...}, "isExternal": false, "fanIn": 1, "fanOut": 0 }
    ],
    "edges": [
      { "callerProgram": "PROG-A", "calleeProgram": "PROG-B", "callSites": [...] }
    ],
    "hasCycle": false,
    "hasDynamicCall": false
  },
  "ranking": {
    "entries": [
      { "rank": 1, "programName": "PROG-A", "fileName": "PROG-A.cbl", "mdi": {...},
        "lineCount": 200, "paragraphCount": 8, "fanIn": 0, "fanOut": 1, "strategy": "BigBang" }
    ]
  },
  "errors": []
}
```

**HTTPステータス**

| 状況 | ステータス |
|------|-----------|
| 正常 | 200 OK |
| sources が空 | 400 Bad Request（`ProjectController` validation。Engine は呼び出さない） |
| プログラム数が 50 超 | 400 Bad Request（`ProjectController` validation。Engine は呼び出さない） |

### 7.2 注釈レポート生成

```
POST /api/export/annotation-report
Content-Type: application/json
```

**リクエスト Body**

```json
{ "fileName": "PROG-A.cbl", "source": "<タグコメント付きCOBOLソース>" }
```

**レスポンス**

```
Content-Type: text/markdown; charset=utf-8

# COBOL 移行分析レポート：PROG-A
...（Markdownテキスト）
```

**HTTPステータス**

| 状況 | ステータス |
|------|-----------|
| 正常 | 200 OK |
| source が空 | 400 Bad Request |
| パースエラーあり | 200 OK（レポートにエラー内容を記載） |

### 7.3 移行設計書生成

```
POST /api/export/migration-design
Content-Type: application/json
```

**リクエスト Body**

```json
{
  "sources": [
    { "fileName": "PROG-A.cbl", "source": "..." },
    { "fileName": "PROG-B.cbl", "source": "..." }
  ]
}
```

**レスポンス**

```
Content-Type: text/markdown; charset=utf-8

# COBOL 移行設計書
...（Markdownテキスト）
```

---

## 8. フロントエンド追加

### 8.1 マルチファイル入力（FileDropZone.ts）

Phase 3 の画面に「プロジェクト」タブを追加する。

```
[ AST ] [ CFG ] [ DFG ] [ コメント ] [ プロジェクト ]
                                      ↑ Phase 6 で追加
```

プロジェクトタブの構成：

```
┌──────────────────────────────────────────────────────────┐
│  ファイルをドロップ または [ファイル選択]                 │  ← FileDropZone
│  PROG-A.cbl ✓   PROG-B.cbl ✓   [+ 追加]               │
│  [ Analyze Project ]                                      │
├──────────────────────────────────────────────────────────┤
│  [ 依存グラフ ] [ ランキング ]  タブ切り替え             │
├──────────────────────────────────────────────────────────┤
│  D3.js 依存グラフ または ランキングテーブル               │
├──────────────────────────────────────────────────────────┤
│  [ 注釈レポートDL ] [ 移行設計書DL ]                     │
└──────────────────────────────────────────────────────────┘
```

### 8.2 依存グラフ（DependencyGraph.ts）

- レイアウト: `d3.forceSimulation`
- ノード形状: 円
- ノード色:

| 条件 | 色 |
|------|---|
| 内部（解析済み）+ Critical | `#e74c3c`（赤） |
| 内部（解析済み）+ High | `#e67e22`（オレンジ） |
| 内部（解析済み）+ Medium | `#f39c12`（黄） |
| 内部（解析済み）+ Low | `#27ae60`（緑） |
| 外部（未解析） | `#808080`（グレー） |

- ノードラベル: プログラム名 + MDIスコア（外部は `?`）
- エッジ: 矢印付き実線、エッジラベルに CALL件数
- ズーム: `d3.zoom()`

### 8.3 ランキングテーブル（RankingTable.ts）

| 順位 | プログラム | MDI | リスク | ファンイン | ファンアウト | 推奨戦略 |
|------|-----------|-----|--------|----------|------------|--------|

- テーブルは `<table>` として DOM に生成
- リスク列はバッジスタイル（Phase 3 MDIパネルと同じ色）
- 推奨戦略のツールチップに説明文を表示

### 8.4 ダウンロード処理

```typescript
// api/exportApi.ts
export async function downloadAnnotationReport(req: ExportReportRequest): Promise<void>;
export async function downloadMigrationDesign(req: ExportDesignRequest): Promise<void>;

// Blob を生成して <a download> で保存
function downloadAsFile(content: string, fileName: string, mimeType: string): void {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = fileName; a.click();
  URL.revokeObjectURL(url);
}
```

### 8.5 TypeScript 型定義（projectTypes.ts）

```typescript
export interface CobolSource { fileName: string; source: string; }

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

export type MigrationStrategy = 'BigBang' | 'Incremental' | 'StranglerFig' | 'NeedsStudy';

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
```

---

## 9. テスト要件（xUnit / Vitest）

### CallGraphBuilderTests.cs

| テスト名 | 検証内容 |
|----------|---------|
| `Build_StaticCall_EdgeCreated` | `CALL "PROG-B"` で PROG-A → PROG-B のエッジが生成される |
| `Build_DynamicCall_HasDynamicCallTrue` | `CALL identifier` で `HasDynamicCall = true` |
| `Build_ExternalProgram_IsExternalTrue` | ソース未提供の被呼び出しプログラムが `IsExternal = true` |
| `Build_CircularCall_HasCycleTrue` | A→B→A の循環で `HasCycle = true` |
| `Build_FanInFanOut_Correct` | FanIn / FanOut が正しく集計される |

### ProjectControllerTests.cs

| テスト名 | 検証内容 |
|----------|---------|
| `Analyze_EmptySources_ReturnsBadRequest` | `sources` が空の場合、400 Bad Request を返し Engine を呼び出さない |
| `Analyze_ExceedsMaxSources_ReturnsBadRequest` | 51 ファイルの場合、400 Bad Request を返し Engine を呼び出さない |
| `Analyze_ValidSources_CallsProjectAnalyzer` | 1〜50 ファイルの場合、`ProjectAnalyzer` を呼び出して結果を返す |

### MigrationRankingTests.cs

| テスト名 | 検証内容 |
|----------|---------|
| `Rank_ByMdiDescending` | MDIスコア降順でランキングされる |
| `Strategy_Critical_NeedsStudy` | MDI ≥ 75 で `NeedsStudy` |
| `Strategy_HighFanInOut_StranglerFig` | FanIn+FanOut ≥ 6 で `StranglerFig` |
| `Strategy_Low_BigBang` | MDI < 25 かつ FanIn+FanOut < 3 で `BigBang` |
| `Rank_ParagraphCount_CountsParagraphNodes` | `NodeType == "Paragraph"` かつ `Category == Unit` の AST ノード数を `ParagraphCount` に反映する |

### ExportGeneratorTests.cs

| テスト名 | 検証内容 |
|----------|---------|
| `AnnotationReport_ContainsProgramName` | レポートにプログラム名が含まれる |
| `AnnotationReport_ContainsTagComments` | タグコメントが表に出力される |
| `AnnotationReport_NoTagComments_ShowsNone` | タグなしの場合「なし」が出力される |
| `MigrationDesign_ContainsRankingTable` | 設計書に順位テーブルが含まれる |
| `MigrationDesign_ContainsDependencySection` | 設計書に依存関係セクションが含まれる |

### exportApi.test.ts（Vitest）

| テスト名 | 検証内容 |
|----------|---------|
| `downloadAnnotationReport_callsCorrectEndpoint` | `POST /api/export/annotation-report` が呼ばれる |
| `downloadMigrationDesign_callsCorrectEndpoint` | `POST /api/export/migration-design` が呼ばれる |

---

## 10. 完了基準

以下をすべて満たした時点で Phase 6 完了とする。

- [ ] `dotnet test` が全テストPASS（CallGraphBuilder / MigrationRanking / ExportGenerator を含む）
- [ ] `POST /api/project/analyze` に2ファイル（一方が他方をCALLする）を送信して依存グラフのエッジが1件返る
- [ ] `POST /api/project/analyze` に51ファイルを送信すると `400 Bad Request` が返り、Engine が呼び出されない
- [ ] `POST /api/project/analyze` で MDI スコア降順のランキングが返る
- [ ] `POST /api/export/annotation-report` でMarkdownが返る（ProgramName・MDIスコア・戦略提案を含む）
- [ ] `POST /api/export/annotation-report` にタグコメント付きソースを渡してテーブルにタグが出力される
- [ ] `POST /api/export/migration-design` で複数プログラムの設計書Markdownが返る
- [ ] フロントエンドでファイルをドロップして「Analyze Project」を押すと依存グラフが表示される
- [ ] ランキングテーブルに MDI順の一覧が表示される
- [ ] 「注釈レポートDL」ボタンでMarkdownファイルがダウンロードされる
- [ ] 「移行設計書DL」ボタンでMarkdownファイルがダウンロードされる
- [ ] `npm test` が全テストPASS

---

## 11. 実装上の注意事項

1. **CALL 情報の前提**: Phase 1/2 の仕様では `CALL` 文を `StatementType = "CALL"` の `StatementNode` として扱い、静的 CALL の呼び出し先は `StatementNode.CallTarget` に保持する。`CallGraphBuilder` は `Operands` ではなく `CallTarget` を参照する。

2. **プログラム名の正規化**: COBOLのPROGRAM-IDは通常大文字。`CALL "prog-b"` と `CALL "PROG-B"` を同一プログラムとして扱うため、比較は大文字正規化した名前で行う。

3. **Markdown のエスケープ**: プログラム名・コメントテキストに Markdown の特殊文字（`|`, `*`, `_` 等）が含まれる可能性がある。テーブルセルに挿入する前にエスケープ処理を行う。

4. **enum の JSON 表現**: `MigrationStrategy` は Phase 1 §8 の `JsonStringEnumConverter` 設定により `BigBang` / `Incremental` / `StranglerFig` / `NeedsStudy` の文字列で返す。TypeScript 型定義は数値 enum を受け取らない。

5. **モデル配置**: `ProjectAnalyzeResult` は `AnalyzeResult` / `ProgramDependencyGraph` / `MigrationRanking` に依存するため Engine 層に配置する。Core 層は Engine 型を参照しない request/input DTO のみを保持し、`Core -> Engine` 参照を作らない。

6. **50件上限の責務**: Phase 6 の対象プログラム数は最大 50 とし、`ProjectController` が `sources.Count > 50` を `400 Bad Request` として扱う。`CallGraphBuilder` は validation 済み入力を前提とし、上限超過エラーを返す責務を持たない。

7. **大量データのダウンロード**: ファイルが大きい場合、`Blob` の生成はメモリを消費する。Phase 6 の対象プログラム数を最大 50 に制限することで問題を回避する。

8. **循環依存の表示**: `HasCycle = true` の場合、依存グラフ上でサイクルを構成するエッジを赤破線で強調表示し、ユーザーに警告を示す。

---

## 12. 参照資料

| 資料 | 参照箇所 | 本仕様との対応 |
|------|---------|--------------|
| `design/docs/CobolStructureAnalysis.md` §5.2.2 | 移行戦略選択（ビッグバン・Strangler Fig・増分） | §4.3 戦略判定ロジック・§6.1 戦略説明文 |
| `design/docs/CobolStructureAnalysis.md` §5.2.1 | リスクパターン（制御・データ・統合リスク） | §6.1 注釈レポートの「高リスクパターン」 |
| `design/specs/phase2-engine.md` v1.5 §6・§7 | MDI指標・MdiScore・MdiRisk・AnalyzeResult の Engine 配置 | §4.1 DependencyNode.Mdi・§5 ProjectAnalyzeResult・§6.1 レポートの指標内訳 |
| `design/specs/phase3-visualization.md` §6.4 | MDIパネルのリスクバッジ色 | §8.3 ランキングテーブルのリスク列 |
| `design/specs/phase5-comment.md` §4 | CommentTag パース | §6.1 注釈レポートのタグコメント一覧 |
| `design/brainstorm/phase6-planning.md` | 設計判断メモ | 本仕様全体 |
| `implement/docs/feedback-phase6-model-placement.md` | 実装側フィードバック | §2 モデル配置方針・§5 ProjectAnalyzer・§7.1 validation・§9 テスト要件 |
