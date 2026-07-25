# Phase 9 実装ログ：デモ B（ギャラリー）／ C（静的インタラクティブ）

- 実装日: 2026-07-26
- 仕様: `design/specs/phase9-demo-gallery-interactive.md`
- プロンプト: `implement/prompts/2026-07-26_01_phase9-demo-gallery-interactive-impl.md`

## 成果サマリ

固定コーパス `carddemo`（pin `59cc6c2`）を事前計算し、バックエンド不要の静的サイトを `docs/` に生成した。

- **事前計算ツール** `tools/DemoPrecompute`：プロジェクト解析（全31本）→ 対象集合選定（§3）→ 対象の
  AnalyzeResult / 注釈レポート / 移行設計書 / 図（SVG）を `docs/data/` に書き出し、デモ B ギャラリー HTML を
  `docs/gallery/` に生成。
- **デモ C**：フロント `src/api/*.ts` の 4 境界を `VITE_STATIC_DATA=1` で静的ローダへ切替え、`docs/app/` に静的ビルド。
- **ランディング** `docs/index.html`、`docs/.nojekyll`。

## 実装中に発見した前提崩れ（重要）

`design/specs` が前提とする「CardDemo 31 本が解析可能」が崩れていた。パーサ（Phase 8 の 31/31）は通るが、
**エンジンの DFG 解析が重複 FILLER でクラッシュ**し、31 本中 21 本が解析失敗 → ranking 10 本のみ・CS* 0 本で §3 が成立しなかった。

- 根本原因: `DfgBuilder.ComputeImpactClosure` の `nodes.ToDictionary(n => n.Id, …)`。同一グループ配下の複数 `FILLER` で
  修飾 Id（`親.FILLER`）が重複し `ArgumentException`。ライブ `/api/analyze` でも同じ入力で失敗する既存バグ。
- フィードバック記録: `implement/docs/feedback-phase9-dfg-filler-duplicate-key.md`
- ユーザー確認: 「エンジンを修正して継続」を選択。
- 対応: `ComputeImpactClosure` を重複 Id 耐性に修正（重複キーは 1 エントリに畳む。FILLER は文中参照されず影響閉包の
  意味論不変）。回帰テスト `DfgBuilderTests.Build_DuplicateFillerInGroup_DoesNotThrow` を追加。
- 結果: 解析可能プログラムが 10 → **31** に増加。§3 の選定が成立（CS* = CSUTLDTC を含む）。

## デモ対象集合（§3・決定論・pin `59cc6c2` ＋ appsettings.json 固定重み）

- N（MDI 上位）= 8、選定本数（和集合）= **9**。
- 選定キー（ランク昇順）: `COACTUPC, CBSTM03B, COCRDUPC, COTRN02C, COCRDLIC, COUSR00C, COTRN00C, COCRDSLC, CSUTLDTC`。
  - 上位 8 は CB*/CO*。バケット代表として CS*（CSUTLDTC, rank 30）を追加、計 9。
- 詳細スナップショット: `implement/log/working/2026-07-26_phase9-precompute-selection.md`。
- 2 回実行して選定キー・順序が一致（決定論を確認）。

## 既定値・非 silent な判断

- N=8 / 図形式 SVG / Pages ソース `docs/`（いずれも仕様 §11 既定）。
- **MDI 分布**: 固定コーパス＋固定重みでは全 31 本が Low（最大 MDI 24.3）。戦略は BigBang / Incremental の範囲で、
  High/Critical は出現しない（重みは改変しない＝仕様 §8）。
- **project.json**: `programs` は空配列（size 削減、5.2MB→20KB）。demo C の ProjectPanel は dependencyGraph/ranking のみ
  参照し、個別 AnalyzeResult は `data/programs/{NAME}.json` で配布。schema は ProjectAnalyzeResult のまま。
- **AST 図**: 構造概観（Structure/Unit カテゴリ）。Element レベル（Statement/DataItem）は可読性のため省略。
- **デモ C のスコープ**: §6-2 のインタラクティブ機能（AST 折りたたみ・CFG/DFG ナビ・双方向ハイライト・GO TO/PERFORM・
  影響閉包・MDI）は単一プログラム系のため、プログラムピッカーで対象集合を切替える単一プログラム探索を実装。
  コメント挿入/削除・プロジェクトタブ（依存グラフ）はバックエンド前提／ドロップ入力前提のため静的モードで非表示
  （依存グラフ・全ランキングはデモ B ギャラリーで提供）。エクスポートは事前計算 Markdown のダウンロードへ差替え（§6-3）。

## 追加・変更ファイル

- `tools/DemoPrecompute/**`（新規ツール）、`tests/DemoPrecompute.Tests/**`（選定ロジックの単体・6 件）。
- `src/backend/CobolAnalyzer.Engine/Dfg/DfgBuilder.cs`（重複 Id 耐性）、`tests/.../DfgBuilderTests.cs`（回帰 +1）。
- `src/backend/CobolAnalyzer.sln`（DemoPrecompute / DemoPrecompute.Tests を追加）。
- フロント: `src/api/staticData.ts`（新規）、`analyzeApi/projectApi/exportApi/commentApi.ts`（静的分岐）、
  `main.ts`（静的ピッカー）、`vite.config.ts`（base 注入）、`vite-env.d.ts`、`styles/main.css`、
  `src/api/staticData.test.ts` / `staticMode.branch.test.ts`（新規テスト）。
- `docs/**`（生成物：index.html / .nojekyll / gallery / app / data）。

## 再生成手順（precompute → build → docs 反映）

```
# 0. submodule（コーパス）取得
git submodule update --init --recursive

# 1. ビルド & テスト（backend 107 / frontend 49）
cd implement/src/backend && dotnet build CobolAnalyzer.sln && dotnet test CobolAnalyzer.sln
cd ../frontend && npm test

# 2. 事前計算（docs/data + docs/gallery + 選定ログを生成。既定 out=<repo>/docs, N=8）
cd ../../  # implement/
dotnet run --project tools/DemoPrecompute

# 3. デモ C 静的ビルド（docs/app へ。base/data base は Pages サブパス）
#    ※ Git Bash は POSIX パス変換で base を壊すため PowerShell で実行する
cd src/frontend
$env:VITE_STATIC_DATA='1'; $env:VITE_BASE='/cobol-analyzer/app/'; $env:VITE_DATA_BASE='/cobol-analyzer/data/'
npm run build -- --outDir ../../../docs/app --emptyOutDir

# 4. docs/ をコミット（GitHub Pages ソース = main の docs/）
```

pin 更新時は上記 2〜3 を再実行して成果物を更新する（コーパスは Phase 8 の pin 更新方針に従う）。

## 受け入れ基準の対応（§8）

1. 事前計算の再現性: pin+固定重みで決定論。選定キー/順/本数をログ記録。✓
2. デモ B: 索引（全31本ランキング＋依存グラフ図）＋対象9本ページ（注釈レポート＋AST/CFG/DFG 図）を静的表示。✓
3. デモ C: `VITE_STATIC_DATA` で静的ビルドが通り、事前計算 JSON を読んでインタラクティブに動作。
   静的バンドルに `localhost:5000` / `/api/*` 文字列は残らず（dead-code 除去）、API_BASE へ通信しない。✓
4. ホスティング: `docs/`（index/gallery/app/data）を base サブパスで解決。リンク解決 87/87。✓
5. 帰属: Apache-2.0 ＋ pin をランディング・ギャラリー・デモ C フッタに明示。✓
6. 既存資産非破壊: backend 100→**107** pass（DFG 回帰 +1、選定 +6）、frontend 38→**49** pass。
   ライブ API 経路（`API_BASE` 実 fetch）はライブモードで維持（既存 api テスト green）。✓
