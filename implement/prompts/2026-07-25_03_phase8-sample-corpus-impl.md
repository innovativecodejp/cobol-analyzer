# Phase 8 実装プロンプト：サンプルコーパス統合（submodule ＋ samples レジストリ）

仕様: `../design/specs/phase8-sample-corpus.md`（実装前に必ず全文を読むこと）
前段: `../design/specs/phase7-preprocessing.md`（前処理で CardDemo 31/31）

---

## 前提確認

1. 上記仕様の §3（submodule）§4（レジストリ）§5（ローダ）§6（Spike 再ターゲット）§7（受け入れ基準）を把握する。
2. `implement/samples/` の有無、`tools/CardDemoSpike/Program.cs`、`CobolPreprocessorOptions`（CopybookPaths）を確認する。
3. 不明点・仕様矛盾は実装を止めてユーザーに確認する（`design/specs/` を自分で変更しない）。

---

## 実装タスク一覧（順に実施）

### タスク 1：CardDemo submodule 追加（仕様 §3）

- `implement/samples/carddemo/` に submodule を追加：
  ```
  git submodule add https://github.com/aws-samples/aws-mainframe-modernization-carddemo.git implement/samples/carddemo
  ```
- **HTTPS URL** を使う（公開ユーザーが `--recursive` clone 可能に）。SSH/個人アカウント参照は使わない。
- **コミットを固定**（追加時点の HEAD を pin）。`.gitmodules` に記載されることを確認。
- 取得後、実測を記録：`app/cbl` の `*.cbl`/`*.CBL` 本数と内訳（CB*/CO*/CS*）。前回は CB* 12 / CO* 18 / CS* 1 = 31。「CB* 14本」との差異の出所（別リビジョン等）を一度確認し、ログに残す。

### タスク 2：samples レジストリ（仕様 §4）

- `implement/samples/registry.json` を作成（仕様 §4 のスキーマ）。`carddemo` エントリを記載：
  - `pinnedCommit` = タスク1で固定したハッシュ、`root` = `carddemo`、`cobolDir` = `app/cbl`、`cobolGlobs` = `["*.cbl","*.CBL"]`、`copybookDirs` = `["app/cpy"]`、`license` = `Apache-2.0`、`sourceUrl`。

### タスク 3：ローダ `SampleRegistry`（仕様 §5）

- `registry.json` を読み、`name` → 解決済み絶対パス（cobolDir / cobolGlobs / copybookDirs）を返すクラスを実装（`CobolAnalyzer.Core` 推奨）。
- ベースディレクトリ（`implement/samples/`）は注入可能に。未登録名・パス不存在は明示的に扱う。
- `copybookDirs` は `CobolPreprocessorOptions.CopybookPaths` にそのまま渡せる形にする。

### タスク 4：ローダの単体テスト（仕様 §8）

- 登録名の解決 / 未登録名 / パス不存在 / copybookDirs 受け渡し。
- registry.json は実ファイルを読む（ハードコード禁止）。

### タスク 5：CardDemoSpike の再ターゲット（仕様 §6）

- 引数省略時は `SampleRegistry` の `carddemo` を対象にする（明示ディレクトリ引数は後方互換で残す）。
- 固定版 submodule に対して測定し、pass/fail 一覧・バケット・本数を出力。
- 結果を `implement/log/working/2026-07-25_04_phase8-corpus-fixed-respike.md` に記録（本数・閾値を確定）。

### タスク 6：完了確認（仕様 §7）

```
git submodule update --init --recursive
dotnet build
dotnet test
dotnet run --project tools/CardDemoSpike        # 引数省略＝レジストリ carddemo
```
- submodule 込みで取得でき、`SampleRegistry` が carddemo を実在パスへ解決。
- CardDemoSpike が固定版で **31/31**（CB* 12 / CO* 18 / CS* 1）を再現（差異は原因記録）。
- 既存テスト green（92 pass を割らない）。
- Apache-2.0 帰属（submodule の LICENSE/NOTICE）がツリーに存在。

---

## 仕様との矛盾発見時の対処

1. `implement/docs/` にフィードバックを記録する。
2. 実装を停止してユーザーに報告する。
3. `design/specs/` を自分で変更しない。

---

## 完了後の申し送り

- 確定した本数・閾値を要約し、次（デモ B: ギャラリー → C: 静的インタラクティブ）の spec 化に渡す。
- `design/roadmaps/roadmap.md` に Phase 7 / Phase 8 の行を追加するのは design 側の作業（ユーザー確認時）。
