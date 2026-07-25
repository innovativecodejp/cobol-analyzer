# Phase 9 実装プロンプト：デモ B（ギャラリー）／ C（静的インタラクティブ）

仕様: `../design/specs/phase9-demo-gallery-interactive.md`（実装前に必ず全文を読むこと）
前段:
- `../design/specs/phase8-sample-corpus.md`（§10 接続先。`SampleRegistry` / carddemo pin `59cc6c2fd7ebd7ef7925cad552a01a4b8b6e4d5e` / `app/cbl` = 31/31）
- `../design/specs/phase6-export.md`（依存グラフ・移行優先度ランキング・Markdown エクスポート）
- `../design/specs/phase3-visualization.md` / `phase4-navigation.md`（AST/CFG/DFG・双方向ナビ）

---

## 前提確認（実装を始める前に）

1. 仕様 §3（対象選定）§4（precompute）§5（デモ B）§6（デモ C）§7（Pages）§8（受け入れ基準）§9（テスト）を把握する。
2. 既存資産の現状を確認する：
   - `src/backend/CobolAnalyzer.Core/Samples/SampleRegistry.cs`（carddemo 解決）
   - `src/backend/CobolAnalyzer.Engine/Project/ProjectAnalyzer.cs`（依存グラフ＋ranking）
   - `tools/CardDemoSpike/Program.cs`（レジストリ既定・全走査の実績）
   - Export 経路：`Controllers/ExportController.cs`（`api/export/annotation-report` / `migration-design`）
   - フロント API 境界：`src/frontend/src/api/{analyzeApi,projectApi,exportApi,commentApi}.ts`（いずれも `fetch(${API_BASE}/api/...)` の薄いラッパ）
   - 型スキーマ：`src/frontend/src/types/{analyzeResult,projectTypes,commentTypes}.ts`（precompute JSON はこれと**同一スキーマ**）
   - `src/frontend/vite.config.ts`（現状 `base` 未設定・test のみ）
3. 現状のグリーンを記録する（`dotnet build` / `dotnet test` / フロント `npm test`）。受け入れ基準 §8-6 の非破壊判定の基準線にする。
4. 不明点・仕様矛盾は実装を止め、`implement/docs/` に記録してユーザーに確認する（`design/specs/` を自分で変更しない）。

---

## 実装タスク一覧（順に実施）

### タスク 1：事前計算パイプライン（precompute）— 仕様 §4

- `tools/` に新規ツール（例 `tools/DemoPrecompute`）を追加、または `CardDemoSpike` を拡張する。入力は `SampleRegistry` の `carddemo`（cobolDir=`app/cbl`、copybookDirs=`app/cpy` を `CobolPreprocessorOptions.CopybookPaths` に渡す）。
- **プロジェクト解析（全31本）**: `ProjectAnalyzer` 相当で `ProgramDependencyGraph` ＋ `ranking`（strategy 付き）を算出。
- **対象集合の決定（§3・決定論）**:
  1. `ranking.entries` の MDI 上位 **N=8**、
  2. バケット代表 各1本（`CB*`/`CO*`/`CS*`、上位 N になければ追加。`CS*` は1本なので必ず含む）、
  3. 1・2 の和集合（重複排除、上限目安 ≤11 本）。
  - **選定順・本数・キー**を precompute ログに記録（silent な打ち切り禁止。N 等を変えるなら理由をログに残す）。
- **プログラム解析（対象集合のみ）**: 各 `AnalyzeResult`（AST / CFG / DFG / Metrics）。
- **Markdown エクスポート**: 対象集合の注釈レポート（`api/export/annotation-report` 相当）＋プロジェクト移行設計書（`api/export/migration-design` 相当）を、既存 Export ロジックを直接呼んで生成する（HTTP を経由しない）。
- **出力**: JSON は既存型と同一スキーマで書き出す（フロント無改造で読める）。配置は §7（`docs/data/` 等）に合わせる。図の書き出しはタスク3で扱う。

### タスク 2：precompute の決定論テスト — 仕様 §9

- 固定コーパス（pin `59cc6c2`）＋固定重み（`appsettings.json`）に対し、**対象集合キー・ランキング順・本数のスナップショット**を検証する。
- CardDemo 全走査は**ツール実行**で担保し、ユニットテストに全解析を含めない（Phase 8 方針踏襲）。
- 未存在キー・不整合は明示的に失敗させる。

### タスク 3：図の静的書き出し（AST / CFG / DFG）— 仕様 §5

- Phase 3 の描画を静的画像（**SVG 推奨**）として対象集合ぶん書き出す。既存フロント描画を流用するか、precompute から SVG 生成する。手段は実装裁量だが再現可能にする。
- 依存グラフ図（索引用）も1枚書き出す。
- 図形式を SVG 以外にする場合は理由を実装ログに残す（silent 変更禁止）。

### タスク 4：デモ B ギャラリー（静的 HTML）— 仕様 §5・§7

- 閲覧専用の静的コレクションを `docs/gallery/` に生成する。
  - **索引ページ**: 全31本の移行優先度ランキング表（rank/program/MDI/strategy/fanIn/fanOut）、依存グラフ図、コーパス出所（sourceUrl / pin / **Apache-2.0 帰属**）。
  - **各対象プログラム**: 注釈レポート（Markdown→HTML）＋ AST/CFG/DFG 図 ＋ MDI サマリー・リスクランク。
  - **プロジェクト移行設計書**（Markdown→HTML）を1本掲載。
- Markdown→HTML の生成手段は実装裁量（既存 Markdown を素直に HTML 化）。相対リンクは `/<repo>/` サブパス配信前提で解決する。

### タスク 5：デモ C 静的インタラクティブ — 仕様 §6

- **静的ローダへの差し替え**: `analyzeApi.ts` / `projectApi.ts` / `exportApi.ts` / `commentApi.ts` を、環境フラグ（例 `VITE_STATIC_DATA=1`）で分岐させ、precompute JSON をプログラム名キーで読む静的ローダに切り替える。**静的モードでは `API_BASE` へ一切 fetch しない**。UI/描画コンポーネントは無改造。
- **維持する対話機能**: AST 折りたたみ、CFG/DFG ナビ、ノード↔ソース双方向ハイライト（Phase 4）、GO TO / PERFORM ジャンプ、データ項目影響閉包、MDI パネル。プログラム選択は**デモ対象集合**（§3）から選ぶピッカー。
- **静的モードで無効化 / 差し替え（§6-3）**:
  - コメント挿入／削除（`commentApi`）：非表示 or 無効化（明示）。任意ソース再解析は不可。
  - エクスポート（`exportApi`）：`downloadAsFile` を precompute 済み Markdown（静的資産）を読んで保存する形に差し替え。任意ソース再生成は不可。
- **ビルド**: 静的データモードで `vite build`。GitHub Pages プロジェクトサイト（`/<repo>/` 配下）に合わせ Vite `base` を設定し、出力を `docs/app/` へ。

### タスク 6：静的ローダのフロントテスト — 仕様 §9

- プログラム名で JSON 解決、未存在キーで明示エラー、**静的モードで fetch を呼ばない（`API_BASE` 非アクセス）** を検証する。
- 既存 `*.test.ts`（`projectApi.test.ts` / `exportApi.test.ts` / `commentApi.test.ts`）のライブ経路テストは壊さない（静的モードは**追加**）。

### タスク 7：ホスティング配置 — 仕様 §7

- `docs/` を Pages ソースとして構成する：
  - `docs/index.html`（ランディング：B ギャラリー・C アプリへの導線）
  - `docs/gallery/`（デモ B）／`docs/app/`（デモ C ビルド出力）／`docs/data/`（precompute JSON ＋ エクスポート Markdown）
- **帰属・ライセンス**: CardDemo の Apache-2.0 帰属（NOTICE 相当）と pin コミットをサイト上に明示（B 索引・C フッタ等）。
- `docs/` 配下の成果物をコミットしてクリーンチェックアウトで配信可能にする。

### タスク 8：完了確認 — 仕様 §8

```
git submodule update --init --recursive
dotnet build
dotnet test                                     # 既存テスト green（100 pass を割らない）
dotnet run --project tools/DemoPrecompute       # precompute 決定論・対象集合ログ出力
cd src/frontend && npm test                     # 静的ローダ含めグリーン
VITE_STATIC_DATA=1 npm run build                # base サブパスで静的ビルド
```
- precompute が決定論的に走り、対象集合が §3 規則どおり一意（順・本数をログ記録）。
- デモ B：索引（全31本ランキング表＋依存グラフ図）と対象 N 本ページ（注釈レポート＋AST/CFG/DFG 図）が静的表示。
- デモ C：静的ビルドが通り、`API_BASE` 非通信でインタラクティブ機能が動く。ピッカーが対象集合を列挙。
- Pages 配置（`docs/`）が `base` サブパスで解決。クリーンチェックアウトで両デモが開ける。
- Apache-2.0 帰属＋pin がサイト上に表示。既存 `dotnet build`/`dotnet test`/フロントテストがグリーン（ライブ API 経路を壊さない）。
- 再生成手順（precompute → build → `docs/` 反映）を実装ログに残す。

---

## 既定値（§11・silent 変更禁止）

- 対象本数 **N = 8**、図形式 **SVG 推奨**、Pages ソース **`docs/`**。
- 出力量・描画都合で変える場合は理由を実装ログ（`implement/log/working/`）に残す。

---

## 仕様との矛盾発見時の対処

1. `implement/docs/` にフィードバックを記録する。
2. 実装を停止してユーザーに報告する。
3. `design/specs/` を自分で変更しない（フィードバック記録と仕様更新は分離）。

---

## 完了後の申し送り

- 確定した対象集合（キー・順・本数）と Pages 配置を要約する。
- `design/roadmaps/roadmap.md` の Phase 7〜9 確定行の反映は design 側の作業（ユーザー確認時）。
- 成果・知見は `~/dev/projects/portfolio/` に反映（公開デモ URL・スクリーンショット等）。
