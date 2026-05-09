# Phase 2 実装プロンプト：AST設計・指標計算エンジン

仕様: `../design/specs/phase2-engine.md`（実装前に必ず全文を読むこと）

---

## 前提確認

実装を開始する前に以下を確認する：

1. `../design/specs/phase2-engine.md` を読み、内容を把握する
2. Phase 1 の実装済み構造を確認する：
   - AST ノードは `CobolAnalyzer.Core/Ast/` にある（仕様の記述と異なる点に注意）
   - `CobolAnalyzer.Parser/AstBuilder.cs` が Phase 1 の AstBuilder
3. 不明点があれば実装を止めてユーザーに確認する

### Phase 1 との差分に関する注意

仕様 §2 では新規 AST ノード 3 件を `CobolAnalyzer.Parser/Ast/` に配置すると記載しているが、
Phase 1 の実装では全 AST ノードが `CobolAnalyzer.Core/Ast/` にある。
**新規 AST ノードも `CobolAnalyzer.Core/Ast/` に配置すること**（一貫性を優先）。

---

## 実装タスク一覧

以下の順序で実装する。各タスク完了後に次に進む。

---

### タスク 1：プロジェクト構造の追加

#### 1-1. CobolAnalyzer.Engine プロジェクトの作成

```
src/backend/CobolAnalyzer.Engine/
├── CobolAnalyzer.Engine.csproj
├── Cfg/
├── Dfg/
└── Metrics/
    └── Calculators/
```

```bash
dotnet new classlib -n CobolAnalyzer.Engine -o src/backend/CobolAnalyzer.Engine --framework net8.0
dotnet sln src/backend/CobolAnalyzer.sln add src/backend/CobolAnalyzer.Engine/CobolAnalyzer.Engine.csproj
```

プロジェクト参照：
- `CobolAnalyzer.Engine` → `CobolAnalyzer.Core`
- `CobolAnalyzer.API` → `CobolAnalyzer.Engine`（既存の API プロジェクトに参照追加）

#### 1-2. CobolAnalyzer.Engine.Tests プロジェクトの作成

```
tests/CobolAnalyzer.Engine.Tests/
├── CobolAnalyzer.Engine.Tests.csproj
├── CfgBuilderTests.cs
├── DfgBuilderTests.cs
└── MetricsCalculatorTests.cs
```

```bash
dotnet new xunit -n CobolAnalyzer.Engine.Tests -o tests/CobolAnalyzer.Engine.Tests --framework net8.0
dotnet sln src/backend/CobolAnalyzer.sln add tests/CobolAnalyzer.Engine.Tests/CobolAnalyzer.Engine.Tests.csproj
```

プロジェクト参照：
- `CobolAnalyzer.Engine.Tests` → `CobolAnalyzer.Engine`
- `CobolAnalyzer.Engine.Tests` → `CobolAnalyzer.Parser`（AstBuilder でテスト用 AST を生成するため）

テストデータは `tests/CobolAnalyzer.Parser.Tests/TestData/` を共有参照する。
`CobolAnalyzer.Engine.Tests.csproj` に以下を追加：

```xml
<ItemGroup>
  <Content Include="..\CobolAnalyzer.Parser.Tests\TestData\**" Link="TestData\%(RecursiveDir)%(Filename)%(Extension)">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

---

### タスク 2：AST ノード拡張

#### 2-1. 既存 StatementNode の拡張

`CobolAnalyzer.Core/Ast/StatementNode.cs` に以下のプロパティを追加：

```csharp
public List<DataReferenceNode> Operands { get; init; } = new();
public string? IoVerb { get; init; }
public string? FileName { get; init; }
public string? CallTarget { get; init; }
public PerformDetailsNode? PerformDetails { get; init; }
```

（`PerformFrom` / `PerformThru` / `CallTarget` は Phase 1 で既に実装済みなら重複追加しないこと）

#### 2-2. 既存 DataItemNode の拡張

`CobolAnalyzer.Core/Ast/DataItemNode.cs` に `Value` プロパティを追加：

```csharp
public string? Value { get; init; }   // VALUE句
```

#### 2-3. 新規ノード：DataReferenceNode

`CobolAnalyzer.Core/Ast/DataReferenceNode.cs` を新規作成：

```csharp
public enum ReferenceKind { Define, Use }

public class DataReferenceNode : AstNode
{
    public string DataName { get; init; } = "";
    public ReferenceKind Kind { get; init; }
    // NodeType = "DataReference", Category = Element
}
```

#### 2-4. 新規ノード：ConditionNode

`CobolAnalyzer.Core/Ast/ConditionNode.cs` を新規作成：

```csharp
public class ConditionNode : AstNode
{
    public string ConditionText { get; init; } = "";
    public List<DataReferenceNode> References { get; init; } = new();
    // NodeType = "Condition", Category = Element
}
```

#### 2-5. 新規ノード：PerformDetailsNode

`CobolAnalyzer.Core/Ast/PerformDetailsNode.cs` を新規作成：

```csharp
public enum PerformKind { OOL, Inline, Times, Until, Varying }

public class PerformDetailsNode : AstNode
{
    public PerformKind Kind { get; init; }
    public string? TimesExpression { get; init; }
    public ConditionNode? UntilCondition { get; init; }
    // NodeType = "PerformDetails", Category = Element
}
```

#### 2-6. AstBuilder の拡張

`CobolAnalyzer.Parser/AstBuilder.cs` を以下の点で拡張する：

- `MOVE A TO B` → `StatementNode.Operands` に `DataReferenceNode(A, Use)` と `DataReferenceNode(B, Define)` を追加
- `READ/WRITE/OPEN/CLOSE` → `StatementNode.IoVerb` と `StatementNode.FileName` を設定
- `PERFORM paragraph` → `StatementType = "PERFORM"`、`PerformFrom` に呼び出し先、`PerformThru = null`
- `PERFORM UNTIL/VARYING` → `StatementNode.PerformDetails`（PerformKind.Until / Varying）を設定
- `IF / EVALUATE / PERFORM UNTIL` の条件式 → `ConditionNode` を生成
- IF / EVALUATE のブランチ専用リストを持つ場合、その中の `StatementNode` は同じインスタンスを `AstNode.Children` にも追加する
- `DataItemNode` の VALUE 句 → `DataItemNode.Value` を設定

DataReferenceNode の収集対象文（最低限）：MOVE, ADD, SUBTRACT, MULTIPLY, DIVIDE, COMPUTE, IF（条件内）, EVALUATE（WHEN条件内）

---

### タスク 3：CFG（制御フローグラフ）実装

#### 3-1. モデルクラス

`CobolAnalyzer.Engine/Cfg/` に以下を作成：

**BasicBlock.cs**（仕様 §4.2 の通り）
- `Id`（`"{ParagraphName}:{index}"` 形式）
- `ParagraphName?`
- `Statements`（`List<StatementNode>`）
- `Location?`

**CfgEdge.cs**（仕様 §4.3 の通り）
- `FromBlockId`, `ToBlockId`
- `CfgEdgeKind`（enum: 仕様 §4.3 の8種類）
- `IsRecursive`（`bool`, デフォルト false）

**ControlFlowGraph.cs**（仕様 §4.4 の通り）
- `ProgramName`, `Blocks`, `Edges`, `EntryBlockId`, `ExitBlockIds`
- `HasAlter`, `HasRecursion`

#### 3-2. CfgBuilder.cs

`CobolAnalyzer.Engine/Cfg/CfgBuilder.cs` を実装：

```csharp
public class CfgBuilder
{
    public ControlFlowGraph Build(ProgramNode ast) { ... }
}
```

構築ルール（仕様 §4.5 準拠）：

1. **基本ブロック分割**：PROCEDURE DIVISION 先頭、パラグラフ/セクション先頭、GO TO の遷移先パラグラフ先頭、IF/EVALUATE の分岐先（ConditionalTrue/False/WHEN）、PERFORM（OOL）の復帰先となる次の文
2. **エッジ生成**：
   - 連続文 → FallThrough
   - IF → ConditionalTrue（真節先頭）+ ConditionalFalse（偽節先頭または次ブロック）
   - GO TO → GoTo（遷移先パラグラフ先頭ブロック）
   - PERFORM（OOL）→ PerformCall（対象パラグラフ先頭）+ PerformReturn（対象パラグラフ末→呼出元次ブロック）
   - PERFORM THRU → PerformThruCall / PerformThruReturn
   - IF / EVALUATE の synthetic ブロック内にある GO TO → その synthetic ブロックから遷移先パラグラフ先頭への GoTo
3. **ALTER 文**：`HasAlter = true` を設定、遷移先エッジは生成しない
4. **再帰 PERFORM 検出**：PERFORM コールグラフでサイクル検出 → `HasRecursion = true`、該当エッジに `IsRecursive = true`
5. **STOP RUN / EXIT PROGRAM**：そのブロックの Id を `ExitBlockIds` に追加

---

### タスク 4：DFG（データフローグラフ）実装

#### 4-1. モデルクラス

`CobolAnalyzer.Engine/Dfg/` に以下を作成：

**DfgNode.cs**（仕様 §5.2 の通り）
- `Id`（FQDN: `"GROUP.CHILD"` 形式）、`Name`, `LevelNumber`, `Picture?`, `IsGroup`

**DfgEdge.cs**（仕様 §5.3 の通り）
- `FromId`, `ToId`
- `DfgEdgeKind`（enum: Define / Use / Redefines / GroupOf）
- `StatementRef?`

**DataFlowGraph.cs**（仕様 §5.4 の通り）
- `ProgramName`, `Nodes`, `Edges`, `ImpactClosure`

#### 4-2. DfgBuilder.cs

`CobolAnalyzer.Engine/Dfg/DfgBuilder.cs` を実装：

```csharp
public class DfgBuilder
{
    public DataFlowGraph Build(ProgramNode ast) { ... }
}
```

構築ルール：

1. **ノード生成**：DATA DIVISION 内の全 `DataItemNode` を `DfgNode` に変換。Id は FQDN（祖先グループ名を `.` で連結）
2. **Redefines エッジ**：`DataItemNode.RedefinesTarget != null` → `DfgEdge(Kind=Redefines, FromId=当該項目, ToId=RedefinesTarget)`
3. **GroupOf エッジ**：集団項目の子項目 → `DfgEdge(Kind=GroupOf, FromId=子, ToId=親)`
4. **Define/Use エッジ**：`StatementNode.Operands` 内の `DataReferenceNode` を走査。`Kind=Define` → `DfgEdge(Kind=Define, ToId=DataName)`、`Kind=Use` → `DfgEdge(Kind=Use, FromId=DataName)`。`StatementRef` には `"Line:{node.Location?.StartLine}"` 形式で設定
5. **影響閉包**：変数 X の影響閉包 = X の Define エッジから到達可能な Use エッジの集合（BFS/DFS で計算）を `DataFlowGraph.ImpactClosure` に格納

---

### タスク 5：MDI指標計算エンジン実装

#### 5-1. MdiWeights.cs

`CobolAnalyzer.Engine/Metrics/MdiWeights.cs` を仕様 §6.3 の通り実装（デフォルト値を設定）。

#### 5-2. MetricsResult.cs

`CobolAnalyzer.Engine/Metrics/MetricsResult.cs` を仕様 §6.2 の通り実装。
仕様 §12.3 に従い `Dictionary<string, int> CcPerParagraph` プロパティも追加すること：

```csharp
public Dictionary<string, int> CcPerParagraph { get; init; } = new();
```

#### 5-3. MdiScore.cs

`CobolAnalyzer.Engine/Metrics/MdiScore.cs` を仕様 §6.3 の通り実装：

```csharp
public enum MdiRisk { Low, Medium, High, Critical }

public class MdiScore
{
    public double Score { get; init; }
    public MdiRisk Risk { get; init; }
    public Dictionary<string, double> WeightedContributions { get; init; } = new();
}
```

#### 5-4. 各指標 Calculator の実装

`CobolAnalyzer.Engine/Metrics/Calculators/` に以下を作成：

**CyclomaticComplexityCalculator.cs**
- 入力: `ControlFlowGraph`
- パラグラフ単位で CC = 分岐エッジ数（ConditionalTrue）+ 1
- 戻り値: `Dictionary<string, int>`（パラグラフ名→CC）。全体CC は最大値

**GoToDensityCalculator.cs**
- 入力: `ProgramNode`（AST）
- GO TO 文数 / PROCEDURE DIVISION 総文数
- 総文数が 0 の場合は 0.0 を返す

**AlterRiskCalculator.cs**
- 入力: `ProgramNode`（AST）
- ALTER 文数を返す

**NestingDepthCalculator.cs**
- 入力: `ProgramNode`（AST）
- IF / EVALUATE / PERFORM のネスト最大深度（再帰的に計算）

**RedefinesDensityCalculator.cs**
- 入力: `DataFlowGraph`
- Redefines エッジ数 / DFG 全ノード数
- ノード数が 0 の場合は 0.0 を返す

**CrossScopeDependencyCalculator.cs**
- 入力: `DataFlowGraph` + `ControlFlowGraph`
- パラグラフ境界を越える DFG Use エッジ数
  （Use エッジの `StatementRef` の行番号が、Define エッジの StatementRef とは異なるパラグラフに属する場合をカウント）

**MdiCalculator.cs**
- 入力: `MetricsResult`（CC/GD/AD/ND/RD/CS の生値）+ `MdiWeights`
- 仕様 §6.4 の算出式で `MdiScore` を生成
- リスクランク判定: 仕様 §6.5 の通り
- `WeightedContributions` に各指標の寄与値（`w_X × n(x, sat) × 100`）を格納
- 仕様 §12.5：重みの合計が 1.0 でない場合、`Console.Error.WriteLine` で警告（例外は不可）

---

### タスク 6：AnalyzeResult モデル

`CobolAnalyzer.Core/Models/AnalyzeResult.cs` を仕様 §7 の通り作成：

```csharp
public class AnalyzeResult
{
    public ProgramNode? Ast { get; init; }
    public ControlFlowGraph? Cfg { get; init; }
    public DataFlowGraph? Dfg { get; init; }
    public MetricsResult? Metrics { get; init; }
    public List<ParseError> Errors { get; init; } = new();
    public bool IsSuccess => Errors.Count == 0;
}
```

`ControlFlowGraph` / `DataFlowGraph` / `MetricsResult` への参照が必要なため、
`CobolAnalyzer.Core.csproj` に `CobolAnalyzer.Engine` への参照を追加するか、
またはこれらの型を `CobolAnalyzer.Core` 側に移すことなく `CobolAnalyzer.API` 側で解決すること。

> **推奨**: `AnalyzeResult.cs` は `CobolAnalyzer.API` 側（または新規の `CobolAnalyzer.Engine` 内）に配置し、
> Core が Engine に依存しない構造を維持する。
> 配置先を変更した場合は `using` / `namespace` を調整すること。

---

### タスク 7：REST API 拡張

#### 7-1. appsettings.json に MdiWeights セクションを追加

`CobolAnalyzer.API/appsettings.json` に仕様 §9 のスキーマを追加：

```json
"MdiWeights": {
  "CyclomaticComplexity": 0.25,
  "GoToDensity": 0.20,
  "AlterRisk": 0.20,
  "NestingDepth": 0.15,
  "RedefinesDensity": 0.10,
  "CrossScopeDependency": 0.10,
  "CcSaturation": 50.0,
  "GdSaturation": 0.3,
  "AdSaturation": 1.0,
  "NdSaturation": 8.0,
  "RdSaturation": 0.3,
  "CsSaturation": 50.0
}
```

#### 7-2. Program.cs に DI 登録を追加

```csharp
builder.Services.Configure<MdiWeights>(builder.Configuration.GetSection("MdiWeights"));
builder.Services.AddSingleton<CfgBuilder>();
builder.Services.AddSingleton<DfgBuilder>();
builder.Services.AddSingleton<MdiCalculator>();
```

#### 7-3. AnalyzeController.cs の実装

`CobolAnalyzer.API/Controllers/AnalyzeController.cs` を新規作成：

- `POST /api/analyze`
- リクエスト: `{ "source": string }`
- `source` が null/空 → 400 Bad Request
- 処理フロー：`CobolParserFacade.Parse()` → （エラーあれば）200 OK でエラー返却 → `CfgBuilder.Build()` → `DfgBuilder.Build()` → 各 Calculator → `MdiCalculator.Calculate()` → `AnalyzeResult` を 200 OK で返却
- サーバー内部例外 → 500 Internal Server Error

CFG/DFG の JSON シリアライズについて（仕様 §12.1）：
現時点では `Blocks` / `Edges` のリスト形式でそのまま返す。
Phase 3 で D3.js 用の `{ nodes, links }` 形式への変換が必要になる場合は、その時点で AnalyzeController に変換ロジックを追加する。

---

### タスク 8：テストの実装

テストデータには `tests/CobolAnalyzer.Parser.Tests/TestData/` を共有参照（タスク 1 の設定による）。

#### AstBuilderPhase2Tests.cs（仕様 §10 の2テスト）

| テスト名 | 使用するテストデータ / インラインソース |
|----------|----------------------------------------|
| `Build_PerformSingle_StatementTypeIsPerform` | インライン（`PERFORM paragraph` 単体） |
| `Build_IfBranchStatements_AlsoInChildren` | インライン（IF の True/False ブランチに文を含む） |

#### CfgBuilderTests.cs（仕様 §10 の9テスト）

各テストで `CobolParserFacade.Parse()` + `AstBuilder` でAST生成後、`CfgBuilder.Build()` を実行。

| テスト名 | 使用するテストデータ / インラインソース |
|----------|----------------------------------------|
| `Build_SimpleSequence_FallThroughEdges` | インライン（連続文のみ） |
| `Build_IfStatement_TrueFalseEdges` | インライン（IF文1つ） |
| `Build_GoTo_GoToEdge` | `goto-sample.cbl` |
| `Build_PerformOOL_CallAndReturnEdges` | `goto-sample.cbl` または インライン |
| `Build_PerformThru_ThruEdges` | `goto-sample.cbl` |
| `Build_IfBranchGoTo_GoToEdgeFromSyntheticBlock` | インライン（IF ブランチ内に GO TO） |
| `Build_AlterStatement_HasAlterTrue` | インライン（ALTER文含む） |
| `Build_RecursivePerform_HasRecursionTrue` | インライン（相互再帰PERFORM） |
| `Build_EntryAndExit_Correct` | `hello.cbl` |

#### DfgBuilderTests.cs（仕様 §10 の4テスト）

| テスト名 | 使用するテストデータ |
|----------|----------------------|
| `Build_MoveStatement_DefineAndUseEdges` | `hello.cbl` または インライン |
| `Build_Redefines_RedefinesEdge` | `data-sample.cbl` |
| `Build_GroupItem_GroupOfEdges` | `data-sample.cbl` |
| `Build_ImpactClosure_CorrectReach` | インライン（A→B→C の連鎖） |

#### MetricsCalculatorTests.cs（仕様 §10 の8テスト）

| テスト名 | 内容 |
|----------|------|
| `Cc_LinearProgram_IsOne` | 分岐なしの CFG で CC = 1 |
| `Cc_OneIf_IsTwo` | IF 1つの CFG で CC = 2 |
| `Gd_NoGoTo_IsZero` | GO TO なし AST で GD = 0.0 |
| `Ad_HasAlter_CountIsOne` | ALTER 1件の AST で AlterCount = 1 |
| `Nd_NestedIf_CorrectDepth` | IF(IF) の AST で MaxNestingDepth = 2 |
| `Mdi_AllZeroMetrics_ScoreIsZero` | 全指標 0 → MDI = 0.0, Risk = Low |
| `Mdi_AllSaturated_ScoreIs100` | 全指標が飽和点 → MDI = 100.0, Risk = Critical |
| `Mdi_WeightsFromConfig_Applied` | カスタム重みを渡すと計算結果に反映される |

---

## 完了確認

仕様 §11 の完了基準をすべて確認する：

```
dotnet build src/backend/CobolAnalyzer.sln
dotnet test src/backend/CobolAnalyzer.sln
dotnet run --project src/backend/CobolAnalyzer.API
```

- `POST /api/analyze` に `goto-sample.cbl` の内容を送信 → CFG の `edges` に `"kind": "GoTo"` が含まれる
- `POST /api/analyze` に `data-sample.cbl` の内容を送信 → DFG の `edges` に `"kind": "Redefines"` が含まれる
- `POST /api/analyze` に `data-sample.cbl` の内容を送信 → DFG の `impactClosure` が返る
- `POST /api/analyze` に `goto-sample.cbl` の内容を送信 → `metrics.mdi.score` が数値で返る
- `appsettings.json` の `MdiWeights.CyclomaticComplexity` を変更して再起動 → スコアが変わる
- Swagger UI（`/swagger`）で `/api/analyze` エンドポイントが確認できる

---

## 仕様との矛盾発見時の対処

実装中に仕様の矛盾・未定義事項を発見した場合：

1. `implement/docs/` にフィードバック内容を記録する
2. 実装を停止してユーザーに報告する
3. ユーザーの指示を待つ（`design/specs/` を自分で変更しない）
