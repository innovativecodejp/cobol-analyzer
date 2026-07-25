# 2026-07-26_02 Phase 9 仕様策定：デモ B（ギャラリー）／ C（静的インタラクティブ）

## 作業概要

Phase 8（サンプルコーパス統合）が implement 側で完了（`3d50d25`、submodule pin `59cc6c2` / `SampleRegistry` / **31/31 確定**・100 pass）したのを受け、Phase 8 §10 の接続先である**公開デモ**の仕様（Phase 9）を起票した。

固定コーパス（CardDemo）を**事前計算**し、**バックエンド不要で GitHub Pages に静的ホスト**できる形で 2 系統のデモを定義：

- **デモ B（ギャラリー）**: Phase 6 エクスポート（注釈レポート／移行設計書）＋ AST/CFG/DFG 図を静的書き出しした閲覧専用コレクション。
- **デモ C（静的インタラクティブ）**: 既存フロントの `src/api/*.ts` 境界だけを静的 JSON 読み込みへ差し替え、backend なしでインタラクティブに動かす。

## Phase 8 完了確認（implement 実測）

| 項目 | 確定値 |
|------|--------|
| submodule pin | `59cc6c2fd7ebd7ef7925cad552a01a4b8b6e4d5e`（HTTPS・`.gitmodules` 記載）|
| 本数 | CB\* 12 / CO\* 18 / CS\* 1 = **31/31 pass** |
| 「CB\* 14本」 | **誤り**と確定（pin の `app/cbl` に CB\* は12本のみ）。以後の閾値は本 pin 基準 |
| テスト | Core 8 / Parser 33 / Engine 56 / API 3 = **100 pass** |
| 残フィードバック | なし（Phase 7 のカンマ／本数ギャップは `fb04c5a` で解消済み）|

## 事前確認したグラウンドトゥルース（設計根拠）

- **precompute の継ぎ目**: フロント4モジュール（`analyzeApi` / `projectApi` / `exportApi` / `commentApi`）はいずれも `fetch(${API_BASE}/api/...)` の薄いラッパ → 静的ローダ差し替えが明確。
- **選定を駆動する型**: `ProjectAnalyzeResult.ranking.entries`（MDI 順・`MigrationStrategy` 付き）→ デモ B/C の「MDI 上位 N＋バケット代表」を決定論的に導出可能。
- **成果物の供給元**: Phase 6（依存グラフ・ランキング・注釈レポート／移行設計書 Markdown）＋ Phase 3（AST/CFG/DFG 図）。

## ユーザー決定事項（起票前に確認）

| 論点 | 決定 |
|------|------|
| ホスティング | **GitHub Pages**（`docs/` 配信）。B・C を同一サイトに配置 |
| ギャラリー範囲 | **MDI 上位 N（既定8）＋バケット代表（CB\*/CO\*/CS\* 各1）**。全31本はランキング表で網羅、フル成果物は対象集合のみ |
| spec 構成 | **1本の spec（Phase 9）に B/C 両方**を章分けで収録 |

## 成果物

| ファイル | 状態 |
|---------|------|
| `design/specs/phase9-demo-gallery-interactive.md` | 作成・コミット対象 |
| `design/roadmaps/roadmap.md` | Phase 7〜9 行を反映・更新 |
| `design/log/working/2026-07-26_02_phase9-demo-spec.md` | 本ログ・コミット対象 |

## 申し送り（implement 側）

- 本仕様（§4 precompute / §5 デモ B / §6 デモ C / §7 Pages / §8 受け入れ基準）に沿って実装する。
- **決定論**: pin `59cc6c2` ＋固定 MDI 重みで、デモ対象集合（§3）が一意に定まること。選定順・本数を実装ログに記録（silent 打ち切り禁止）。
- **静的モード**: `VITE_STATIC_DATA` で分岐、`API_BASE` へ一切通信しない。コメント挿入／削除は無効化、エクスポートは事前計算 Markdown のダウンロードへ差し替え（§6-3）。
- **base パス**: GitHub Pages プロジェクトサイト（`/<repo>/`）前提で Vite `base` と B の相対リンクを解決。
- 既定値（N=8 / 図は SVG / `docs/`）は出力量次第で調整可、理由をログに残す。
- 実装プロンプト（`implement/prompts/…`）は implement 配下のため本 design コミットには含めない。
