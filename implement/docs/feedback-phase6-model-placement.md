# Phase 6 実装フィードバック：モデル配置と依存関係の未定義事項

作成日: 2026-05-12  
対象仕様: `design/specs/phase6-export.md` v1.1  
対象プロンプト: `implement/prompts/2026-05-12_01_phase6-export-impl.md`

Phase 6 実装開始時の停止条件確認で、現行実装の依存関係と Phase 6 仕様のモデル配置に不整合があるため記録する。

---

## 1. 発見内容

Phase 6 仕様 §5 では、以下のモデルを `CobolAnalyzer.Core/Models/ProjectAnalyzeResult.cs` に追加する例が示されている。

```csharp
public class ProjectAnalyzeResult
{
    public List<AnalyzeResult> Programs { get; init; } = new();
    public ProgramDependencyGraph DependencyGraph { get; init; }
    public MigrationRanking Ranking { get; init; }
    public List<string> Errors { get; init; } = new();
}
```

一方、現行実装では `AnalyzeResult` は `CobolAnalyzer.Engine/AnalyzeResult.cs` に存在する。

現在のプロジェクト参照関係は以下。

| Project | 参照先 |
|---------|--------|
| `CobolAnalyzer.Core` | なし |
| `CobolAnalyzer.Engine` | `CobolAnalyzer.Core` |
| `CobolAnalyzer.API` | `CobolAnalyzer.Parser`, `CobolAnalyzer.Core`, `CobolAnalyzer.Engine` |

この状態で `ProjectAnalyzeResult` を `Core` に置き、`AnalyzeResult` を参照すると、
`Core -> Engine` の参照が必要になり、既存の `Engine -> Core` と循環参照になる。

---

## 2. 判断が必要な点

実装方針として、少なくとも以下のどちらかを design 側で決める必要がある。

### 案 A: `AnalyzeResult` を Core 側へ移動する

`AnalyzeResult` を `CobolAnalyzer.Core` に移動し、Phase 6 の `ProjectAnalyzeResult` も Core に置く。

懸念:

- 既存 API / Engine の namespace 変更が必要になる
- `AnalyzeResult` は `ControlFlowGraph` / `DataFlowGraph` / `MetricsResult` を参照しており、これらも Engine 側にあるため、追加の移動が必要になる可能性がある

### 案 B: `ProjectAnalyzeResult` を Engine 側または API DTO 側へ置く

`ProjectAnalyzeResult` を `CobolAnalyzer.Engine` 側、または `CobolAnalyzer.API` の DTO として定義する。

懸念:

- Phase 6 仕様 §2 / §5 の「`CobolAnalyzer.Core/Models/ProjectAnalyzeResult.cs` に追加」と異なる
- Core/Models に置く request DTO と response DTO の配置方針が分かれる

---

## 3. 関連する追加未定義事項

実装プロンプトでも注意点として挙げているが、以下も Phase 6 実装前に仕様上の扱いを明確にしたい。

### 3-A. `Build_ExceedsMaxNodes_ReturnsError` のエラー表現

Phase 6 仕様 §9 では `CallGraphBuilderTests.Build_ExceedsMaxNodes_ReturnsError` が要求されている。

しかし、仕様 §3.2 の `ProgramDependencyGraph` には `Errors` フィールドがない。
また、仕様 §7.1 では「プログラム数が 50 超」は API の `400 Bad Request` とされている。

そのため、50件超過をどの層で表現するかを確認したい。

- `CallGraphBuilder` が error result を返す
- `ProjectAnalyzer.Errors` に入れる
- `ProjectController` の validation で 400 にして `CallGraphBuilder` では扱わない

### 3-B. `ParagraphCount` の算出根拠

Phase 6 仕様 §4.1 は `MigrationRankingEntry.ParagraphCount` を要求している。

現行 AST で paragraph 相当ノードをどの `NodeType` / `Category` で数えるかを仕様として明記したい。

---

## 4. 実装側の対応

`implement/prompts/2026-05-12_01_phase6-export-impl.md` の停止条件に従い、現時点では Phase 6 実装を停止した。

design 側で上記のモデル配置方針とエラー表現を確定後、implement 側で Phase 6 実装を再開する。
