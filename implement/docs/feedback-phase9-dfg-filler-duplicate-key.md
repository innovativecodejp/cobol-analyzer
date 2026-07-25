# フィードバック（Phase 9 実装中の前提崩れ）：DFG 解析が重複 FILLER でクラッシュ

- 記録日: 2026-07-26
- 検出フェーズ: Phase 9（デモ B/C 事前計算パイプライン実装中、`tools/DemoPrecompute`）
- 種別: エンジンの既存バグ／仕様前提の崩れ（`phase9-demo-gallery-interactive.md` §3 の前提）

## 事象

固定コーパス `carddemo`（pin `59cc6c2`）31 本を `ProjectAnalyzer.Analyze` に通したところ、
**31 本中 21 本がエンジン解析で失敗**し、`ProjectAnalyzeResult.Errors` に次の例外が記録された。

```
An item with the same key has already been added. Key: WS-ERROR-RECORD.FILLER
An item with the same key has already been added. Key: STATEMENT-LINES.ST-LINE0.FILLER
... （いずれも末尾 .FILLER）
```

結果、`ranking.entries` は **10 本のみ**。しかも 10 本すべて MDI が Low（最大 19.5 = BigBang）で、
バケット内訳は CB* 9 本 / CO* 1 本（COBSWAIT）/ **CS* 0 本**。

## 根本原因

`CobolAnalyzer.Engine/Dfg/DfgBuilder.cs` の `ComputeImpactClosure`（127 行目付近）：

```csharp
var dependencyGraph = nodes.ToDictionary(
    n => n.Id, ...);   // ← n.Id が重複すると ArgumentException
```

`DfgNode.Id` は `"{親修飾名}.{項目名}"`（`CollectDataNodes`、41 行目）。COBOL では
**同一グループ配下に複数の `FILLER`** を書けるため、`親.FILLER` という同一 Id が複数生成され、
`ToDictionary` が重複キーで例外を投げる。`FILLER` の多重定義は実コードで極めて一般的。

- これは **Phase 2/6 のエンジン既存バグ**であり、`/api/analyze`（ライブ）でも同じソースで失敗する。
- Phase 8 の「31/31」は**パーサ成功**（`CobolParserFacade.Parse.IsSuccess`）の測定であり、
  エンジン解析（AST→CFG/DFG/Metrics）の成功を保証していなかった。ここに齟齬がある。

## 仕様前提への影響（`phase9-demo-gallery-interactive.md` §3）

- §3 は `ranking.entries` が全 31 本を含む前提で「MDI 上位 N＋バケット代表」を選定する。
  実際は 10 本しか載らず、**「CS* は1本のみのため必ず含む」が満たせない**（CS* が 0 本）。
- また MDI が全て Low のため、移行戦略（StranglerFig/NeedsStudy）を見せるデモにならない。
  複雑度の高い CICS 系（CO*）大型プログラムは、まさに失敗している 21 本に含まれる。

## 対応方針（本実装での判断）

最小・安全なエンジン修正で `ComputeImpactClosure` を**重複 Id 耐性**にする（重複キーを黙って
1 本に畳む）。`FILLER` は文中で参照されないため影響閉包の意味論は変わらず、既存 100 テストに
影響しない。これにより 21 本が解析可能になり、§3 の選定が意味を持つ。

- 本修正は **implement 側のバグ修正**（`design/specs/` の変更ではない）。
- design 側で §3 の文言（「31 本前提」「CS* 必ず含む」）を見直す場合は、本ドキュメントを参照。
- 修正後の実測（本数・バケット・選定集合）は `log/working/2026-07-26_phase9-precompute-selection.md` に記録する。

## ユーザー確認事項

1. 上記エンジン修正（DFG 重複 FILLER 耐性）を Phase 9 の一部として実施してよいか。
2. §3 の「CS* 必ず含む」は、修正後に CS* が解析可能になる前提で維持でよいか
   （もし修正後も CS* が解析不能なら、その旨をログに残して選定から外す）。
