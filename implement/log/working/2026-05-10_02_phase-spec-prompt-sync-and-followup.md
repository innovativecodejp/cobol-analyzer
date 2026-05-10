# 2026-05-10_02 仕様追随・プロンプト同期・Phase 2 追補対応記録

## 作業概要

Phase 4 実装フィードバックを design 側で反映した後、implement 側の実行プロンプトと実装を再確認した。
Phase 1〜4 の実装プロンプトを更新済み仕様に合わせて修正し、Phase 1 は完了扱いのまま追補メモを記録した。
その後、Phase 2 プロンプト変更に対応して実装をレビューし、必要な修正とテスト追加を行った。

---

## 関連コミット

| コミット | 内容 |
|---------|------|
| `92f8150` | `chore: sync implementation prompts with specs` |
| `27f3df3` | `docs: record phase1 follow-up items` |
| `4cb234f` | `fix: align phase2 implementation with prompt updates` |

---

## 実施内容

### 1. Phase 1〜4 実装プロンプトの同期

更新済みの `design/specs/` を根拠として、以下の実装プロンプトをレビュー・修正した。

| ファイル | 主な修正 |
|---------|---------|
| `prompts/2026-05-03_01_phase1-antlr-parser-impl.md` | `AstNode.Id`、`PERFORM` 単体、`CALL/CallTarget`、追加テストを反映 |
| `prompts/2026-05-05_01_phase2-engine-impl.md` | IF ブランチ `Children`、synthetic ブロック内 `GOTO`、`DataFlowGraph.ImpactClosure`、追加テストを反映 |
| `prompts/2026-05-09_01_phase3-visualization-impl.md` | Phase 2 API 型との鮮度確認項目を追記 |
| `prompts/2026-05-09_02_phase4-navigation-impl.md` | Phase 4 v1.2 の実装形状に合わせ、`SelectionStore` / `LineNodeIndex` / `JumpController` / N3 navLayer / DFG N4 を整理 |

併せて `AGENTS.md` をリポジトリルートに追加し、`launchSettings.json` のポートを `5000` に統一した。

---

### 2. Phase 1 追補メモの記録

Phase 1 は完了扱いとしつつ、プロンプト更新後に見つかった追補候補を `docs/feedback-phase1-followup.md` に記録した。

記録した残件：

- `AstNode.NodeType` と JSON 出力形状の確認
- `PERFORM paragraph` の AstBuilder テスト追加候補
- `CALL "program"` / `CALL identifier` の `CallTarget` テスト追加候補
- `JsonSerializerOptions.MaxDepth` の 256 以上化

このメモは Phase 1 完了を取り消すものではなく、後続作業を止めない追補扱いとした。

---

### 3. Phase 2 実装レビューと修正

Phase 2 プロンプト変更に合わせて、以下を修正した。

| ファイル | 修正内容 |
|---------|---------|
| `src/backend/CobolAnalyzer.Core/Ast/ProgramNode.cs` | `Name` プロパティ追加 |
| `src/backend/CobolAnalyzer.Parser/AstBuilder.cs` | `PROGRAM-ID` から `ProgramNode.Name` を設定 |
| `src/backend/CobolAnalyzer.Engine/Cfg/CfgBuilder.cs` | `ControlFlowGraph.ProgramName` に `ProgramNode.Name` を反映 |
| `src/backend/CobolAnalyzer.Engine/Dfg/DfgBuilder.cs` | `DataFlowGraph.ProgramName` を設定し、`ImpactClosure` の推移閉包計算を修正 |
| `tests/CobolAnalyzer.Engine.Tests/AstBuilderPhase2Tests.cs` | `PERFORM` 単体、IF ブランチ `Children` のテストを追加 |
| `tests/CobolAnalyzer.Engine.Tests/CfgBuilderTests.cs` | IF ブランチ内 GOTO テスト名を仕様に合わせ、ProgramName テスト追加 |
| `tests/CobolAnalyzer.Engine.Tests/DfgBuilderTests.cs` | ImpactClosure の到達検証、REDEFINES 閉包、ProgramName テストを追加 |

`ImpactClosure` は、同一文内の `Use -> Define` 関係と `REDEFINES` 関係をもとに推移閉包を計算する形にした。
これにより、`MOVE WS-A TO WS-B`、`MOVE WS-B TO WS-C` のような連鎖で `WS-A` の影響閉包に `WS-C` が含まれる。

---

## テスト結果

```powershell
dotnet test src/backend/CobolAnalyzer.sln
```

結果：

| テストプロジェクト | 結果 |
|------------------|------|
| `CobolAnalyzer.Parser.Tests` | 12 件 PASS |
| `CobolAnalyzer.Engine.Tests` | 26 件 PASS |

合計 38 件 PASS。失敗なし。

`git diff --check` も PASS。

---

## Git / Push

- `master` の upstream が `origin/main` を向いていたため、push 後の状態表示が紛らわしかった。
- 実際に使用している `origin/master` に upstream を変更した。
- 最終状態は `master...origin/master` で clean。

---

## 残件

- Phase 1 追補メモに記録した項目は未対応のまま保持。
- Phase 4 は完了扱い。既知差分は `docs/feedback-phase4-spec-deviation.md` に記録済み。
