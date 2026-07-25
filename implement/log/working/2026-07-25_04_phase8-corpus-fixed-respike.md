# Phase 8：サンプルコーパス統合（submodule 固定版での再スパイク）

- 実行日: 2026-07-25
- 対象仕様: `../../design/specs/phase8-sample-corpus.md`
- 実装プロンプト: `../prompts/2026-07-25_03_phase8-sample-corpus-impl.md`
- 前段: `2026-07-25_02_phase7-comma-and-literal-continuation.md`（前処理で 31/31）

## 実装したもの

1. **CardDemo submodule 固定**（§3）
   - 追加先: `implement/samples/carddemo/`、取得元: HTTPS（`https://github.com/aws-samples/aws-mainframe-modernization-carddemo.git`）。
   - **pin コミット: `59cc6c2fd7ebd7ef7925cad552a01a4b8b6e4d5e`**。`.gitmodules`（リポジトリルート）に HTTPS URL を記載。
   - Apache-2.0: submodule に upstream の `LICENSE` / `NOTICE` がそのまま含まれる（帰属保持・本リポジトリ側で改変なし）。
   - ローカルの SSL 証明書制約は取得時のみ回避（`.gitmodules` には焼き込まない。§7-1 想定どおり）。
2. **samples レジストリ**（§4）: `implement/samples/registry.json`（`carddemo` エントリ、pin コミット・cobolDir・cobolGlobs・copybookDirs）。
3. **ローダ `SampleRegistry`**（§5、`CobolAnalyzer.Core/Samples/`）
   - `registry.json` を読み、`name` → 解決済み絶対パス（cobolDir / cobolGlobs / copybookDirs）を返す。
   - ベースディレクトリ注入可能、上位探索（`LocateBaseDirectory` / `LoadDefault`）。未登録名・パス不存在を明示的に扱う。
   - `copybookDirs` は `CobolPreprocessorOptions.CopybookPaths` へそのまま渡せる形。
4. **CardDemoSpike 再ターゲット**（§6）: 引数省略時はレジストリの `carddemo` を対象（`--sample <name>` も可、明示ディレクトリ引数は後方互換で保持）。

## 本数の確定（submodule 固定版・実測）

| バケット | 本数 | pass |
|---|---|---|
| Batch(CB*)  | 12 | **12** |
| CICS(CO*)   | 18 | **18** |
| Other(CS*)  | 1  | **1** |
| **合計**    | **31** | **31 / 31** |

- 固定版 `app/cbl` は **CB* 12 / CO* 18 / CS* 1 = 31**。前フェーズの手動取得実測と一致。
- **当初仕様の「CB* 14本」は誤り**（pin `59cc6c2` の `app/cbl` には CB* が 12 本しか存在しない）。`app/cbl` 以外や別リビジョンを数えた可能性。以後の閾値は本 pin を基準に確定する。
- 以後の受け入れ閾値（確定）: Batch CB* = 12/12、CICS CO* = 18/18、合計 31/31。

## テスト（`dotnet build` / `dotnet test`）

- ビルド 0 エラー、全 green: **Core 8 / Parser 33 / Engine 56 / API 3 = 100 pass, 0 fail**。
- `SampleRegistry` 単体テスト（`CobolAnalyzer.Core.Tests`）: 登録名解決 / 大小無視 / cobolGlobs 列挙 / 未登録名 / パス不存在 / registry.json 欠如 / copybookDirs 受け渡し。実 `registry.json`（carddemo）解決も検証（§7-2）。
- レジストリ JSON は実ファイル読み込み（`TestData/samples-base/registry.json` と実 `implement/samples/registry.json`）。

## 受け入れ基準（§7）

- [x] submodule（HTTPS・pin）を追加、`.gitmodules` に記載。`git submodule update --init --recursive` で取得可能。
- [x] `SampleRegistry` が `carddemo` を実在パス（cobolDir / copybookDirs）へ解決。
- [x] `CardDemoSpike`（レジストリ既定・引数省略）が固定版で **31/31**（CB* 12 / CO* 18 / CS* 1）を再現。
- [x] 既存テスト green（92 → 100 pass、割らない）。
- [x] Apache-2.0 帰属（submodule の `LICENSE` / `NOTICE`）がツリーに存在。

## 申し送り（次フェーズ・spec §10）

- コーパス固定＋名前解決が完了。次は **デモ B（ギャラリー）**：レジストリの carddemo を解析し、Phase 6 エクスポート（注釈レポート／移行設計書）＋図を静的書き出し → **デモ C（静的インタラクティブ）**。
- `design/roadmaps/roadmap.md` への Phase 7 / 8 追記は design 側作業（ユーザー確認時）。
