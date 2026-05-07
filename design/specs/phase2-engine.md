# Phase 2 仕様：AST設計・指標計算エンジン

バージョン: 1.2  
作成日: 2026-05-05  
更新日: 2026-05-08（整合性レビューによる修正: AnalyzeResult.Ast nullable化・CfgEdge.IsRecursive追加・MetricsResult.CcPerParagraph追加・StatementNode.CallTarget追加・APIレスポンス例修正）  
ステータス: 確定（implement/ への引き渡し可）

前提: `design/specs/phase1-antlr-parser.md` の実装が完了していること。

---

## 1. 目的・スコープ

Phase 1 のパーサー基盤上に制御フローグラフ（CFG）・データフローグラフ（DFG）の構築と
移行困難度指数（MDI）算出エンジンを実装する。

### スコープ内
- ASTノード拡張（Phase 1 の不足情報を補完）
- CFG構築（基本ブロック分割・制御遷移エッジ生成）
- DFG構築（Define-Use連鎖・REDEFINES・グループ項目関係）
- MDI指標計算エンジン（6指標 + 複合スコア）
- REST API拡張（`POST /api/analyze`）
- xUnit単体テスト

### スコープ外（Phase 3以降）
- CFG / DFG の可視化（D3.js）
- 双方向ナビゲーション（ノード↔コード）
- プログラム間依存グラフ
- COPYBOOK展開
- コメント挿入・削除

---

## 2. プロジェクト構造追加分

Phase 1 の構造に以下を追加する。

```
implement/
├── src/
│   └── backend/
│       ├── CobolAnalyzer.sln                      ← Engine プロジェクト追加
│       ├── CobolAnalyzer.Parser/                  （Phase 1、ASTノード拡張のみ変更）
│       │   └── Ast/
│       │       ├── ConditionNode.cs               ← 追加
│       │       ├── PerformDetailsNode.cs          ← 追加
│       │       └── DataReferenceNode.cs           ← 追加
│       ├── CobolAnalyzer.Engine/                  ← 新規プロジェクト
│       │   ├── CobolAnalyzer.Engine.csproj
│       │   ├── Cfg/
│       │   │   ├── BasicBlock.cs
│       │   │   ├── CfgEdge.cs
│       │   │   ├── ControlFlowGraph.cs
│       │   │   └── CfgBuilder.cs
│       │   ├── Dfg/
│       │   │   ├── DfgNode.cs
│       │   │   ├── DfgEdge.cs
│       │   │   ├── DataFlowGraph.cs
│       │   │   └── DfgBuilder.cs
│       │   └── Metrics/
│       │       ├── MetricsResult.cs
│       │       ├── MdiScore.cs
│       │       ├── MdiWeights.cs
│       │       └── Calculators/
│       │           ├── CyclomaticComplexityCalculator.cs
│       │           ├── GoToDensityCalculator.cs
│       │           ├── AlterRiskCalculator.cs
│       │           ├── NestingDepthCalculator.cs
│       │           ├── RedefinesDensityCalculator.cs
│       │           ├── CrossScopeDependencyCalculator.cs
│       │           └── MdiCalculator.cs
│       ├── CobolAnalyzer.Core/
│       │   └── Models/
│       │       └── AnalyzeResult.cs               ← 追加
│       └── CobolAnalyzer.API/
│           └── Controllers/
│               └── AnalyzeController.cs           ← 追加
└── tests/
    └── CobolAnalyzer.Engine.Tests/
        ├── CobolAnalyzer.Engine.Tests.csproj
        ├── CfgBuilderTests.cs
        ├── DfgBuilderTests.cs
        ├── MetricsCalculatorTests.cs
        └── TestData/                              （Phase 1 TestData を共有参照）
```

---

## 3. ASTノード拡張

Phase 1 で保留した情報を補完する。既存ノードの `StatementNode` と `DataItemNode` は
プロパティを追加拡張する。新規ノードを3つ追加する。

### 3.1 StatementNode 拡張

```csharp
public class StatementNode : AstNode
{
    public string StatementType { get; init; }
    // 追加
    public List<DataReferenceNode> Operands { get; init; } = new();  // 参照データ項目リスト
    public string? IoVerb { get; init; }      // READ / WRITE / OPEN / CLOSE（I/O文のみ）
    public string? FileName { get; init; }    // I/O文の対象ファイル名
    // PERFORM用（StatementType が PERFORM_THRU / PERFORM_LOOP のとき有効）
    public string? PerformFrom { get; init; }
    public string? PerformThru { get; init; }
    public PerformDetailsNode? PerformDetails { get; init; }
    // CALL用（StatementType が "CALL" のとき有効）
    public string? CallTarget { get; init; }  // 静的CALLの呼び出し先名（大文字正規化済み）。動的CALLは null
}
```

### 3.2 DataItemNode 拡張

```csharp
public class DataItemNode : AstNode
{
    public int LevelNumber { get; init; }
    public string Name { get; init; }
    public string? Picture { get; init; }
    public string? RedefinesTarget { get; init; }
    public string? Value { get; init; }       // VALUE句（追加）
    public bool IsGroup => Picture == null && Children.OfType<DataItemNode>().Any();
}
```

### 3.3 新規ノード：ConditionNode（Element）

IF / EVALUATE / PERFORM UNTIL の条件式を保持する。

```csharp
public class ConditionNode : AstNode
{
    // Category = Element
    public string ConditionText { get; init; }           // 条件式のソーステキスト（原文保持）
    public List<DataReferenceNode> References { get; init; } = new();  // 参照データ項目
}
```

### 3.4 新規ノード：PerformDetailsNode（Element）

```csharp
public class PerformDetailsNode : AstNode
{
    // Category = Element
    public PerformKind Kind { get; init; }    // OOL / Inline / Times / Until / Varying
    public string? TimesExpression { get; init; }
    public ConditionNode? UntilCondition { get; init; }
}

public enum PerformKind { OOL, Inline, Times, Until, Varying }
```

### 3.5 新規ノード：DataReferenceNode（Element）

文中でデータ項目を参照する箇所を表す。DFG構築の起点となる。

```csharp
public class DataReferenceNode : AstNode
{
    // Category = Element
    public string DataName { get; init; }         // 参照されるデータ項目名
    public ReferenceKind Kind { get; init; }      // Define / Use
}

public enum ReferenceKind { Define, Use }
```

---

## 4. CFG（制御フローグラフ）

### 4.1 形式モデル

`design/docs/CobolStructureAnalysis.md` §2.2 の形式定義：

> G_CFG = (V, E, s, t)  
> V : 基本ブロック集合  
> E : 制御遷移エッジ集合  
> s : エントリブロック（PROCEDURE DIVISION 先頭）  
> t : 出口ブロック（STOP RUN / EXIT PROGRAM）

### 4.2 BasicBlock

```csharp
public class BasicBlock
{
    public string Id { get; init; }                    // "{ParagraphName}:{index}" 形式
    public string? ParagraphName { get; init; }        // 所属パラグラフ名（null = 合成ブロック）
    public List<StatementNode> Statements { get; init; } = new();
    public SourceLocation? Location { get; init; }
}
```

### 4.3 CfgEdge

```csharp
public class CfgEdge
{
    public string FromBlockId { get; init; }
    public string ToBlockId { get; init; }
    public CfgEdgeKind Kind { get; init; }
    public bool IsRecursive { get; init; }   // 相互再帰PERFORMのサイクルを構成するエッジの場合 true
}

public enum CfgEdgeKind
{
    FallThrough,
    ConditionalTrue,
    ConditionalFalse,
    GoTo,
    PerformCall,
    PerformReturn,
    PerformThruCall,
    PerformThruReturn
}
```

### 4.4 ControlFlowGraph

```csharp
public class ControlFlowGraph
{
    public string ProgramName { get; init; }
    public List<BasicBlock> Blocks { get; init; } = new();
    public List<CfgEdge> Edges { get; init; } = new();
    public string EntryBlockId { get; init; }
    public List<string> ExitBlockIds { get; init; } = new();
    public bool HasAlter { get; init; }       // ALTER文が存在する（動的GOTO）
    public bool HasRecursion { get; init; }   // 相互再帰PERFORMが存在する
}
```

### 4.5 CfgBuilder の構築ルール

#### 基本ブロック分割（リーダ判定）

以下の文はブロック先頭（リーダ）となる：

1. PROCEDURE DIVISION の最初の文
2. パラグラフ / セクションの先頭文
3. GO TO の遷移先パラグラフ先頭
4. IF / EVALUATE の分岐先（真・偽・WHEN）
5. PERFORM（OOL）の復帰先となる次の文

#### ALTER 文の扱い

ALTER 文は静的解析では遷移先不定。CfgBuilder は：
- `ControlFlowGraph.HasAlter = true` を設定
- ALTER 文を含む基本ブロックに警告フラグを付ける
- 遷移先エッジは生成しない（動的解析は Phase 2 スコープ外）

#### 再帰 PERFORM の検出

CfgBuilder は PERFORM コールグラフでサイクルを検出した場合：
- `ControlFlowGraph.HasRecursion = true` を設定
- サイクルを構成するエッジに `IsRecursive = true` フラグを付ける

---

## 5. DFG（データフローグラフ）

### 5.1 形式モデル

`design/docs/CobolStructureAnalysis.md` §2.2 の形式定義：

> G_DFG = (V, E, τ)  
> V : データ要素集合  
> E : データ依存エッジ集合  
> τ : エッジ型割当関数

### 5.2 DfgNode

```csharp
public class DfgNode
{
    public string Id { get; init; }           // DataItemNode.Name（FQDN: "GROUP.FIELD" 形式）
    public string Name { get; init; }
    public int LevelNumber { get; init; }
    public string? Picture { get; init; }
    public bool IsGroup { get; init; }
}
```

### 5.3 DfgEdge

```csharp
public class DfgEdge
{
    public string FromId { get; init; }
    public string ToId { get; init; }
    public DfgEdgeKind Kind { get; init; }
    public string? StatementRef { get; init; }  // 依存を生じさせた文の SourceLocation 文字列
}

public enum DfgEdgeKind
{
    Define,       // 文が ToId データ項目を定義する
    Use,          // 文が FromId データ項目を参照する
    Redefines,    // FromId が ToId を REDEFINES する
    GroupOf       // FromId は ToId の集団項目（親）
}
```

### 5.4 DataFlowGraph

```csharp
public class DataFlowGraph
{
    public string ProgramName { get; init; }
    public List<DfgNode> Nodes { get; init; } = new();
    public List<DfgEdge> Edges { get; init; } = new();
    // Phase 4 双方向ナビゲーションで影響閉包ハイライトに使用
    public Dictionary<string, List<string>> ImpactClosure { get; init; } = new();
}
```

### 5.5 影響閉包（ImpactClosure）

変数 X の影響閉包 = X の Define エッジから到達可能な Use エッジの集合。
`DfgBuilder` は `ImpactClosure`（キー: DataName、値: 影響を受ける DataName リスト）を
計算し、`DataFlowGraph.ImpactClosure` に格納して返す。
フロントエンドはこの値を使って DFG ノードクリック時の影響閉包ハイライトを実現する。

---

## 6. MDI指標計算エンジン

### 6.1 指標一覧

`design/docs/CobolStructureAnalysis.md` §4.3「定量的結果」に対応する6指標。

| 指標ID | クラス | 入力 | 説明 |
|--------|--------|------|------|
| CC | `CyclomaticComplexityCalculator` | CFG | パラグラフごとの CC = 分岐エッジ数 + 1。プログラム全体は最大値 |
| GD | `GoToDensityCalculator` | AST | GO TO 文数 / PROCEDURE DIVISION 総文数 |
| AD | `AlterRiskCalculator` | AST | ALTER 文数（1件以上で高リスク） |
| ND | `NestingDepthCalculator` | AST | IF / EVALUATE / PERFORM のネスト最大深度 |
| RD | `RedefinesDensityCalculator` | DFG | Redefines エッジ数 / DFG 全ノード数 |
| CS | `CrossScopeDependencyCalculator` | DFG + CFG | パラグラフ境界を越える DFG Use エッジ数 |

### 6.2 MetricsResult

```csharp
public class MetricsResult
{
    public string ProgramName { get; init; }
    public int CyclomaticComplexity { get; init; }                    // CC: パラグラフ最大値
    public Dictionary<string, int> CcPerParagraph { get; init; } = new();  // CC: パラグラフ別内訳
    public double GoToDensity { get; init; }                          // GD: 0.0–1.0
    public int AlterCount { get; init; }                              // AD: ALTER文数
    public int MaxNestingDepth { get; init; }                         // ND
    public double RedefinesDensity { get; init; }                     // RD: 0.0–1.0
    public int CrossScopeDependencies { get; init; }                  // CS
    public MdiScore Mdi { get; init; }
}
```

### 6.3 MdiScore と重み設定

```csharp
public class MdiScore
{
    public double Score { get; init; }        // 0.0–100.0
    public MdiRisk Risk { get; init; }        // Low / Medium / High / Critical
    public Dictionary<string, double> WeightedContributions { get; init; } = new();
}

public enum MdiRisk { Low, Medium, High, Critical }
```

```csharp
// MdiWeights.cs（appsettings.json から DI で注入）
public class MdiWeights
{
    public double CyclomaticComplexity { get; set; } = 0.25;
    public double GoToDensity { get; set; }          = 0.20;
    public double AlterRisk { get; set; }            = 0.20;
    public double NestingDepth { get; set; }         = 0.15;
    public double RedefinesDensity { get; set; }     = 0.10;
    public double CrossScopeDependency { get; set; } = 0.10;

    // 正規化の飽和点（この値で n(x) = 1.0）
    public double CcSaturation { get; set; }   = 50.0;
    public double GdSaturation { get; set; }   = 0.3;
    public double AdSaturation { get; set; }   = 1.0;
    public double NdSaturation { get; set; }   = 8.0;
    public double RdSaturation { get; set; }   = 0.3;
    public double CsSaturation { get; set; }   = 50.0;
}
```

### 6.4 MDIスコア算出式

```
n(x, sat) = min(x / sat, 1.0)

MDI = 100 × (
    w_CC × n(CC, 50)  +
    w_GD × n(GD, 0.3) +
    w_AD × n(AD, 1)   +
    w_ND × n(ND, 8)   +
    w_RD × n(RD, 0.3) +
    w_CS × n(CS, 50)
)
```

### 6.5 リスクランク判定

| MDI スコア | MdiRisk |
|------------|---------|
| 0 ≤ MDI < 25 | Low |
| 25 ≤ MDI < 50 | Medium |
| 50 ≤ MDI < 75 | High |
| 75 ≤ MDI ≤ 100 | Critical |

---

## 7. AnalyzeResult（Core層）

```csharp
// Models/AnalyzeResult.cs
public class AnalyzeResult
{
    public ProgramNode? Ast { get; init; }          // 構文エラー時は null
    public ControlFlowGraph? Cfg { get; init; }     // 構文エラー時は null
    public DataFlowGraph? Dfg { get; init; }        // 構文エラー時は null
    public MetricsResult? Metrics { get; init; }    // 構文エラー時は null
    public List<ParseError> Errors { get; init; } = new();
    public bool IsSuccess => Errors.Count == 0;
}
```

---

## 8. REST API 拡張

### 新エンドポイント

```
POST /api/analyze
Content-Type: application/json
```

### リクエスト Body

```json
{
  "source": "<COBOLソースコード文字列>"
}
```

### レスポンス（成功時）

```json
{
  "ast": { "nodeType": "Program", ... },
  "cfg": {
    "programName": "HELLO",
    "blocks": [ { "id": "MAIN-PARA:0", "paragraphName": "MAIN-PARA", "statements": [...] } ],
    "edges": [ { "fromBlockId": "...", "toBlockId": "...", "kind": "FallThrough" } ],
    "entryBlockId": "MAIN-PARA:0",
    "exitBlockIds": ["END-PARA:1"],
    "hasAlter": false,
    "hasRecursion": false
  },
  "dfg": {
    "programName": "HELLO",
    "nodes": [ { "id": "WS-MESSAGE", "name": "WS-MESSAGE", "levelNumber": 1, "picture": "X(20)" } ],
    "edges": [ { "fromId": "WS-MESSAGE", "toId": "WS-MESSAGE", "kind": "Define" } ],
    "impactClosure": { "WS-MESSAGE": [] }
  },
  "metrics": {
    "programName": "HELLO",
    "cyclomaticComplexity": 3,
    "goToDensity": 0.05,
    "alterCount": 0,
    "maxNestingDepth": 2,
    "redefinesDensity": 0.0,
    "crossScopeDependencies": 1,
    "mdi": { "score": 18.5, "risk": "Low", "weightedContributions": { "CC": 3.75, ... } }
  },
  "errors": []
}
```

### 既存エンドポイント

`POST /api/parse`（Phase 1）はそのまま維持する。

### HTTPステータス

| 状況 | ステータス |
|------|-----------|
| 正常 | 200 OK |
| 構文エラーあり | 200 OK（errors に詳細、ast / cfg / dfg / metrics は null） |
| source が空/null | 400 Bad Request |
| サーバー内部エラー | 500 Internal Server Error |

---

## 9. appsettings.json 設定スキーマ追加

```json
{
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
}
```

`MdiWeights` は `IOptions<MdiWeights>` で DI コンテナに登録する。

---

## 10. テスト要件

### CfgBuilderTests.cs

| テスト名 | 検証内容 |
|----------|---------|
| `Build_SimpleSequence_FallThroughEdges` | 連続文が FallThrough エッジで接続される |
| `Build_IfStatement_TrueFalseEdges` | IF文が ConditionalTrue / ConditionalFalse エッジを生成する |
| `Build_GoTo_GoToEdge` | GO TO文が GoTo エッジを生成する |
| `Build_PerformOOL_CallAndReturnEdges` | PERFORM（OOL）が PerformCall / PerformReturn エッジを生成する |
| `Build_PerformThru_ThruEdges` | PERFORM THRU が PerformThruCall / PerformThruReturn エッジを生成する |
| `Build_AlterStatement_HasAlterTrue` | ALTER文を含むプログラムで HasAlter = true |
| `Build_RecursivePerform_HasRecursionTrue` | 相互再帰PERFORMで HasRecursion = true |
| `Build_EntryAndExit_Correct` | EntryBlockId が PROCEDURE DIVISION 先頭、ExitBlockIds に STOP RUN ブロックが含まれる |

### DfgBuilderTests.cs

| テスト名 | 検証内容 |
|----------|---------|
| `Build_MoveStatement_DefineAndUseEdges` | MOVE A TO B で A に Use、B に Define エッジが生成される |
| `Build_Redefines_RedefinesEdge` | REDEFINES 句で Redefines エッジが生成される |
| `Build_GroupItem_GroupOfEdges` | 集団項目の親子関係で GroupOf エッジが生成される |
| `Build_ImpactClosure_CorrectReach` | A → B → C の Define-Use 連鎖で A の影響閉包に C が含まれる |

### MetricsCalculatorTests.cs

| テスト名 | 検証内容 |
|----------|---------|
| `Cc_LinearProgram_IsOne` | 分岐なしプログラムの CC = 1 |
| `Cc_OneIf_IsTwo` | IF 1つのプログラムの CC = 2 |
| `Gd_NoGoTo_IsZero` | GO TO なしで GD = 0.0 |
| `Ad_HasAlter_CountIsOne` | ALTER 1件で AlterCount = 1 |
| `Nd_NestedIf_CorrectDepth` | IF(IF) のネストで MaxNestingDepth = 2 |
| `Mdi_AllZeroMetrics_ScoreIsZero` | 全指標 0 で MDI = 0.0、Risk = Low |
| `Mdi_AllSaturated_ScoreIs100` | 全指標が飽和点で MDI = 100.0、Risk = Critical |
| `Mdi_WeightsFromConfig_Applied` | appsettings の重みが適用される |

---

## 11. 完了基準

以下をすべて満たした時点で Phase 2 完了とする。

- [ ] `dotnet build` がエラーなし
- [ ] `dotnet test` が全テストPASS（Engine.Tests を含む）
- [ ] `POST /api/analyze` に goto-sample.cbl を送信して CFG に GoTo エッジが含まれる
- [ ] `POST /api/analyze` に data-sample.cbl を送信して DFG に Redefines エッジが含まれる
- [ ] `POST /api/analyze` に goto-sample.cbl を送信して MDI スコアが返る
- [ ] appsettings.json の MdiWeights を変更するとスコアが変わる
- [ ] Swagger UI でエンドポイントが確認できる

---

## 12. 実装上の注意事項

1. **CFG の JSON シリアライズ**: `ControlFlowGraph` は `Blocks` と `Edges` のリスト形式で返す。Phase 3 の D3.js が `{ nodes: [...], links: [...] }` 形式を期待するため、API Controller 層で変換するか `AnalyzeController` でシリアライズ形式を調整すること。Phase 3 と事前に調整する。

2. **DFG ノード ID の衝突**: COBOL では同名のデータ項目が異なる集団項目下に存在できる（QUALIFIED NAME）。`DfgNode.Id` は `PARENT.CHILD` の FQDN 形式にすること。

3. **CC の粒度**: CC はパラグラフ単位で計算し、プログラム全体の CC は最大値を `CyclomaticComplexity` に格納する。各パラグラフの CC は `CcPerParagraph`（パラグラフ名 → CC値）に格納する。

4. **ALTER 文**: ALTER は動的制御フロー変更であり、静的解析の限界を示す代表的高リスクパターン（研究資料 §5.2.1）。MDI への寄与は `AD_saturation = 1`（1件で飽和）とし、ALTER が 1 件でも存在するプログラムは AD 指標が最大になる設計とする。

5. **MdiWeights の合計検証**: 重みの合計が 1.0 でない場合、起動時に警告ログを出力すること（エラーで止めない）。

---

## 13. 参照資料

| 資料 | 参照箇所 | 本仕様との対応 |
|------|---------|--------------|
| `design/docs/CobolStructureAnalysis.md` §2.2 | G_CFG = (V, E, s, t) の形式定義 | §4.1 CFG形式モデル |
| `design/docs/CobolStructureAnalysis.md` §2.2 | G_DFG = (V, E, τ) の形式定義 | §5.1 DFG形式モデル |
| `design/docs/CobolStructureAnalysis.md` §3.1 | IR単位定義（制御/データ/境界/副作用） | §3 ASTノード拡張の設計根拠 |
| `design/docs/CobolStructureAnalysis.md` §3.2 | CFG：基本ブロック・支配関係・GO TO / ALTER / ループ | §4.5 CfgBuilder 構築ルール |
| `design/docs/CobolStructureAnalysis.md` §3.3 | DFG：Define-Use・影響閉包・REDEFINES | §5.5 影響閉包 |
| `design/docs/CobolStructureAnalysis.md` §4.3 | 定量的結果：CC・ネスト深度・依存密度 | §6.1 指標一覧 |
| `design/docs/CobolStructureAnalysis.md` §5.2.1 | リスクパターン：ALTER・非構造化制御フロー | §6.4 MDI式・§11.4 ALTER文注意事項 |
| `design/specs/phase1-antlr-parser.md` | AstNode / DataItemNode / StatementNode の定義 | §3 ASTノード拡張の前提 |
| `design/brainstorm/phase2-planning.md` | 設計判断メモ | 本仕様全体 |
