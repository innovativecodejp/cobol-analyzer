# Phase 8 仕様：サンプルコーパス統合（submodule ＋ samples レジストリ）

- 起票日: 2026-07-25
- 前提: `phase7-preprocessing.md`（§10 の接続先）。前処理で CardDemo `app/cbl` が **31/31 パース成功**（`../log/working/2026-07-25_02_phase7-comma-and-literal-continuation.md`）。

## §1 目的・背景

Phase 7 で実コード（AWS CardDemo）が通ることを確認したが、CardDemo 本体はリポジトリ外（測定時に手動取得）で、本数・閾値が版に依存して揺れる。本フェーズで：

1. CardDemo を **submodule でコミット固定**し、本数・閾値を確定・再現可能にする。
2. サンプルを**名前で参照**できる **samples レジストリ**を導入し、解析・測定・（次フェーズの）デモが同じ定義を共有できるようにする。

これは Phase 7 §10 の第1増分であり、デモ（B: ギャラリー / C: 静的インタラクティブ）の土台。

## §2 スコープ / 非スコープ

**スコープ**
- CardDemo を `implement/samples/carddemo/` に submodule 化（コミット固定）。
- `implement/samples/registry.json`（サンプル定義）とローダ（`SampleRegistry`）。
- `tools/CardDemoSpike` をレジストリ既定へ再ターゲットし、固定版で本数・閾値を確定。

**非スコープ（次フェーズ）**
- デモ B（ギャラリー書き出し）/ デモ C（静的インタラクティブ）。
- CardDemo 以外のコーパス（NIST85 等）の追加（レジストリは拡張可能に設計するが、本フェーズは carddemo のみ）。

## §3 CardDemo submodule

- 追加先: `implement/samples/carddemo/`
- 取得元: `https://github.com/aws-samples/aws-mainframe-modernization-carddemo`（**HTTPS URL**。公開ユーザーが `git clone --recursive` で取得できること。SSH/個人アカウント参照は使わない）
- **コミットを固定（pin）**する。`.gitmodules` に記載。
- ライセンス: **Apache-2.0**。submodule なので upstream の `LICENSE`/`NOTICE` がそのまま含まれる（帰属保持）。本リポジトリ側で改変しない。
- 固定後、実測を記録：`app/cbl` の本数と内訳（前回実測 **CB* 12 / CO* 18 / CS* 1 = 31**）。当初仕様の「CB* 14本」との差異の出所（別リビジョン等）を submodule 固定時に一度確認する。

## §4 samples レジストリ

`implement/samples/registry.json` にサンプルを宣言的に定義する。将来のコーパス追加に耐える最小スキーマ：

```json
{
  "samples": [
    {
      "name": "carddemo",
      "description": "AWS CardDemo — realistic COBOL mainframe credit card app",
      "license": "Apache-2.0",
      "sourceUrl": "https://github.com/aws-samples/aws-mainframe-modernization-carddemo",
      "pinnedCommit": "<submodule で固定したハッシュ>",
      "root": "carddemo",
      "cobolDir": "app/cbl",
      "cobolGlobs": ["*.cbl", "*.CBL"],
      "copybookDirs": ["app/cpy"]
    }
  ]
}
```

- パスは `implement/samples/` からの相対（`root` = submodule ディレクトリ名）。
- `copybookDirs` は `CobolPreprocessorOptions.CopybookPaths` にそのまま渡せる形にする。

## §5 ローダ（`SampleRegistry`）

- `registry.json` を読み、`name` → 解決済み絶対パス（cobolDir / cobolGlobs / copybookDirs）を返す小さなクラス。
- 実装位置は `CobolAnalyzer.Core`（依存の少ない層）か新規小プロジェクト。API/エンジン/ツールから再利用できること。
- サンプル未登録・パス不存在は明示的な例外 or 結果型で扱う（呼び出し側が判別できる）。
- レジストリのベースディレクトリ（`implement/samples/`）は注入可能にする（テスト容易性）。

## §6 CardDemoSpike の再ターゲット

- 既定で **レジストリの `carddemo`** を対象にする（引数省略時）。従来どおり明示ディレクトリ引数も残してよい（後方互換）。
- 固定版 submodule に対して測定し、**pass/fail 一覧・バケット・本数**を出力。結果を `implement/log/working/` に記録。
- ここで確定した本数（CB*/CO*/CS*）を、以後の閾値の基準とする。

## §7 受け入れ基準

1. `git clone --recursive`（HTTPS）で submodule 込みに取得できる（外部ユーザー相当。ローカル環境の SSL 制約がある場合は代替手段でコミット存在を確認）。
2. `SampleRegistry` で `carddemo` を解決し、cobolDir / copybookDirs が実在パスとして返る。
3. `tools/CardDemoSpike`（レジストリ既定）が固定版で **31/31**（CB* 12 / CO* 18 / CS* 1）を再現。差異があれば原因を記録。
4. 既存 `dotnet build` / `dotnet test` がグリーン（92 pass を割らない）。
5. Apache-2.0 帰属が submodule 経由で保持されている（upstream LICENSE/NOTICE がツリーに存在）。

## §8 テスト

- `SampleRegistry` 単体テスト：登録名の解決、未登録名、パス不存在、copybookDirs の受け渡し。
- レジストリの JSON パースを実ファイルで検証（ハードコード禁止、既存方針踏襲）。
- （測定はツール実行で担保。ユニットに CardDemo 全走査は含めなくてよい。）

## §9 既知の制約・運用

- submodule はネットワーク取得が前提（オフライン CI では skip 可能に）。
- サンプルサイズ増加に注意（本フェーズは carddemo のみ）。
- CardDemo は改変しない（upstream 追従は pin 更新で明示的に行う）。

## §10 次フェーズへの接続

- 本フェーズ完了（コーパス固定＋名前解決）→ **デモ B（ギャラリー）**：レジストリの carddemo を解析し、既存 Phase 6 エクスポート（注釈レポート／移行設計書）＋図を書き出して静的に見せる。→ **デモ C（静的インタラクティブ）**：解析結果 JSON を事前計算し、フロント `src/api/*.ts` の境界を静的読み込みへ差し替え。
- デモ B/C の spec は本フェーズ完了後に起票する（B/C 固有の設計判断＝出力構成・ホスティング等を別途確定）。
