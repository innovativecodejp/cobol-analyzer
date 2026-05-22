# 2026-05-12_01 Phase 6 フィードバック・プロンプト同期記録

## 作業概要

Phase 6 実装着手前に、`design/specs/phase6-export.md` と現行実装の依存関係を照合したところ、
`ProjectAnalyzeResult` の配置方針に未定義事項があることを確認した。

implement 側ルールに従い、`implement/docs/feedback-phase6-model-placement.md` に記録して実装を停止した。
その後、design 側の更新内容を前提に実装プロンプトを同期し、型不整合 1 件を修正した。

---

## 関連コミット

| コミット | 内容 |
|---------|------|
| `fbf33be` | `docs: add phase6 implementation prompt` |
| `01ce196` | `docs: record phase6 model placement feedback` |
| `daa74e6` | `docs: align phase prompts with updated specs` |
| `c14e7ca` | `docs: update phase6 implementation prompt` |
| `e482243` | `fix: align analyze result ast type` |

---

## 実施内容

### 1. Phase 6 実装 prompt を追加し、着手条件を整理

`prompts/2026-05-12_01_phase6-export-impl.md` を追加し、Phase 6 の実装対象、
依存関係、API/Frontend の完了条件を implement 側作業用 prompt として整理した。

その後のフィードバックと design 側更新を受けて、同ファイルは後続コミットで同期更新した。

---

### 2. Phase 6 の未定義事項をフィードバックとして記録

`implement/docs/feedback-phase6-model-placement.md` に以下を記録した。

- `AnalyzeResult` が `CobolAnalyzer.Engine` にある状態で、`ProjectAnalyzeResult` を `CobolAnalyzer.Core` に置くと `Core -> Engine` 参照が必要になり循環参照になる
- `ProjectAnalyzeResult` を Engine 側または API DTO 側へ置くか、design 側で方針確定が必要
- `sources.Count > 50` をどの層でエラーにするか未定義
- `ParagraphCount` をどの AST 条件で数えるか明文化が必要

implement 側では `design/specs/` を変更せず、記録のみ行った。

---

### 3. Phase 2 / Phase 6 実装プロンプトを仕様更新に同期

`prompts/2026-05-05_01_phase2-engine-impl.md` では、`AnalyzeResult` の配置を
`CobolAnalyzer.Engine/AnalyzeResult.cs` に固定し、`CobolAnalyzer.Core` から
`CobolAnalyzer.Engine` への参照を追加しない方針を明記した。

`prompts/2026-05-12_01_phase6-export-impl.md` では、以下を明文化した。

- `ProjectAnalyzeResult` は Engine 側に置く
- `Core -> Engine` 参照を作らない
- 50 件超過は `ProjectController` validation で 400 とする
- `ProjectControllerTests` 用の API テストプロジェクト追加手順
- 完了確認に 51 ファイル validation の確認項目を追加

---

### 4. `AnalyzeResult.Ast` の型を実装実態に合わせて修正

`src/backend/CobolAnalyzer.Engine/AnalyzeResult.cs` の `Ast` プロパティを以下に修正した。

```csharp
public ProgramNode? Ast { get; init; }
```

これにより、実際に返しているルート AST 型と API/Engine 間の契約を一致させた。

---

## 検証・備考

このセッション単体のテスト実行記録は残していない。
後続の Phase 6 実装完了時の一括検証は `log/working/2026-05-13_01_phase6-export-implementation.md` に記録されている。
