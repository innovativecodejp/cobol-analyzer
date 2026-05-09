# 2026-05-09_01 Phase 2 実装記録

## 作業概要

`design/specs/phase2-engine.md` を根拠として、CFG / DFG / MDI 計算エンジンおよび REST API の実装を行った。
実装期間: 2026-05-05 ～ 2026-05-08

---

## 関連コミット

| コミット | 内容 |
|---------|------|
| `fc992f6` | タスク 1: CobolAnalyzer.Engine / Engine.Tests プロジェクト追加 |
| `bbfea2a` | タスク 2: AST ノード拡張・AstBuilder 拡張 |
| `a86cabd` | タスク 3-8: CFG / DFG / MDI エンジン・REST API・テスト |
| `4814100` | Phase 1/2 仕様再レビューによる修正（後述） |

---

## 実施内容

### タスク 1: プロジェクト構造

- `CobolAnalyzer.Engine` プロジェクト（`src/backend/`）を新規作成
- `CobolAnalyzer.Engine.Tests` プロジェクト（`tests/`）を新規作成
- Engine → Core の参照、API → Engine の参照を追加
- Engine.Tests は Parser.Tests/TestData/ を `Content Link` で共有参照

### タスク 2: AST ノード拡張

既存クラスへの追加：

| クラス | 追加プロパティ |
|-------|-------------|
| `StatementNode` | `Operands`, `IoVerb`, `FileName`, `PerformDetails`, `CallTarget`, `TrueStatements`, `FalseStatements` |
| `DataItemNode` | `Value` |

新規クラス（`CobolAnalyzer.Core/Ast/` に配置）：

| クラス | 内容 |
|-------|------|
| `DataReferenceNode` | `ReferenceKind`（Define/Use）、`DataName` |
| `ConditionNode` | `ConditionText`、`References` |
| `PerformDetailsNode` | `PerformKind`（OOL/Inline/Times/Until/Varying）|

AstBuilder 拡張：MOVE / ADD / SUBTRACT / MULTIPLY / DIVIDE / COMPUTE の Operands 収集、IO 文の IoVerb/FileName 設定、PERFORM 種別判定、IF 真偽節のネスト文収集を追加。

### タスク 3: CfgBuilder

- 段落単位の基本ブロック分割（IntraParagraph IF 分割 + InterParagraph GOTO/PERFORM）
- 8 種類のエッジ生成（FallThrough / ConditionalTrue/False / GoTo / PerformCall/Return / PerformThruCall/Return）
- ALTER 文検出（`HasAlter = true`）、再帰 PERFORM 検出（DFS サイクル検出、`HasRecursion = true`）
- STOP RUN / EXIT PROGRAM ブロックを `ExitBlockIds` に登録

### タスク 4: DfgBuilder

- DATA DIVISION 全項目を DfgNode 化（FQDN: `GROUP.CHILD` 形式）
- Redefines / GroupOf / Define / Use エッジ生成
- 影響閉包（ImpactClosure）を BFS で算出
- 戻り値: `DataFlowGraph`（`ImpactClosure` プロパティとして内包）

### タスク 5: MDI 計算エンジン

`CobolAnalyzer.Engine/Metrics/Calculators/` に 6 Calculator + MdiCalculator を実装：

| Calculator | 指標 |
|-----------|------|
| `CyclomaticComplexityCalculator` | CC（パラグラフ別・全体最大値） |
| `GoToDensityCalculator` | GD = GOTO 文数 / 総文数 |
| `AlterRiskCalculator` | AD = ALTER 文数 |
| `NestingDepthCalculator` | ND = IF/EVALUATE/PERFORM の最大ネスト深度 |
| `RedefinesDensityCalculator` | RD = Redefines エッジ数 / DFG 全ノード数 |
| `CrossScopeDependencyCalculator` | CS = 段落境界越え Use エッジ数 |

`MdiCalculator` が 6 指標の生値 + `MdiWeights` から MDI スコアとリスクランクを算出。重み合計チェック（1.0 でなければ `Console.Error.WriteLine` で警告）。

### タスク 6: AnalyzeResult の配置

仕様プロンプトでは `CobolAnalyzer.Core` への配置を示唆していたが、`Core` が `Engine` の型（ControlFlowGraph / DataFlowGraph / MetricsResult）を参照するには `Core → Engine` の参照が必要になり循環依存が発生する。

**判断**: `AnalyzeResult.cs` を `CobolAnalyzer.Engine` 名前空間に配置し、`Core → Engine` 参照を回避した。

### タスク 7: REST API

- `MdiWeights` を `appsettings.json` に追加し DI で注入
- `POST /api/analyze` エンドポイント実装（`AnalyzeController.cs`）
- JSON オプション: camelCase + `JsonStringEnumConverter` + `MaxDepth: 128` + `WhenWritingNull`
- `[JsonDerivedType]` による `AstNode` のポリモーフィックシリアライズ
- CORS: `builder.Environment.IsDevelopment()` 条件で `AllowAnyOrigin()` を登録（本番非適用）

### タスク 8: テスト

xUnit 20 件（CfgBuilder 8 + DfgBuilder 4 + MetricsCalculator 8）実装。`dotnet test` 全件 PASS。

---

## 仕様差分・フィードバック記録

### 1. TrueStatements / FalseStatements の欠落（記録: feedback-2026-05-08-if-statement-branches.md）

仕様 §3.1 StatementNode に IF 真偽節を格納するプロパティ定義がなかった。  
CfgBuilder が ConditionalTrue/False エッジを生成するには真偽節の内容が必要なため、  
`TrueStatements` / `FalseStatements` を StatementNode に追加し、AstBuilder.BuildIf を拡張した。  
→ design/specs/phase2-engine.md §3.1 への反映を推奨。

### 2. SimpleConditionContext.identifier() が存在しない

AstBuilder の `ExtractIdentifiersFromCondition` で `SimpleConditionContext.identifier()` を呼び出したが、当該メソッドは存在しなかった（`relationCondition()` / `classCondition()` / `conditionNameReference()` のみ）。  
**対処**: 条件式テキストを `ConditionNode.ConditionText` に保持する設計に割り切り、識別子抽出は空リストを返すようにした。Phase 4 ナビゲーションで精度が必要になったとき再実装する。

### 3. ParseResult.Ast の型が AstNode? → ProgramNode? に修正

Phase 1 実装では `ParseResult.Ast` が `AstNode?` 型だったが、Engine 側で `ProgramNode` にキャストする必要があるため `ProgramNode?` に変更した。

### 4. DfgBuilder の戻り値形式変更

実装プロンプトでは `(DataFlowGraph Graph, Dictionary<string, List<string>> ImpactClosure)` タプルで返すよう指示していたが、`ImpactClosure` を `DataFlowGraph` のプロパティに内包する方が API レスポンスのシリアライズと一貫性が高いため、`DataFlowGraph` 単体を返す設計に変更した。

### 5. PROGRAM-ID. TEST. がパースできない

テストコード内で `PROGRAM-ID. TEST.` と書いていたが、`TEST` が COBOL85 予約語のためパースエラーとなった。全テストを `PROGRAM-ID. MYPROG.` に変更して解消。

### 6. MetricsResult with 式のコンパイルエラー

`MetricsResult` が record ではなく class のため、`with` 式が使えなかった。新規インスタンスを構築する形に修正。

---

## 仕様再レビュー適用（コミット 4814100）

2026-05-08 に design/specs/ Phase 1–6 の整合性レビューが実施され、Phase 2 に影響する以下の修正を反映した。

| 修正箇所 | 内容 |
|---------|------|
| `AstNode.Id` プロパティ追加 | `"{NodeType}:{StartLine}:{StartColumn}"` 形式 |
| `DataFlowGraph.ImpactClosure` 追加 | `Dictionary<string, List<string>>` プロパティ |
| `CfgBlock.location` 追加 | ブロック内最初の文の位置 |
| `CfgEdge.isRecursive` 追加 | 再帰 PERFORM エッジのフラグ |
| `MetricsResult.ccPerParagraph` 追加 | パラグラフ別 CC 内訳 |
| CORS 条件付き設定 | `IsDevelopment()` ガードを追加 |

---

## テスト結果

```
dotnet test src/backend/CobolAnalyzer.sln
Test summary: total: 40, failed: 0, succeeded: 40, skipped: 0
```

（Parser.Tests 20 件 + Engine.Tests 20 件）

---

## 成果物

| ファイル・ディレクトリ | 状態 |
|---------------------|------|
| `src/backend/CobolAnalyzer.Engine/` | 新規作成 |
| `tests/CobolAnalyzer.Engine.Tests/` | 新規作成 |
| `src/backend/CobolAnalyzer.Core/Ast/` | 拡張（3新規ファイル + 2既存ファイル変更） |
| `src/backend/CobolAnalyzer.Parser/AstBuilder.cs` | 拡張 |
| `src/backend/CobolAnalyzer.API/` | Controllers / Program.cs 変更 |
| `implement/docs/feedback-2026-05-08-if-statement-branches.md` | フィードバック記録 |

---

## Phase 3 への引き渡し

- `POST /api/analyze` が稼働済み（`http://localhost:5000`）
- レスポンス JSON は Phase 3 仕様 `design/specs/phase3-visualization.md` §9 の TypeScript 型定義と整合
- CORS 設定済み（開発環境限定）
- 未実装フィードバック: `StatementNode.TrueStatements/FalseStatements` を `design/specs/phase2-engine.md §3.1` に追記することを推奨
