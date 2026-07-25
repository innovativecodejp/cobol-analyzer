# 2026-07-26_01 Phase 8 仕様策定：サンプルコーパス統合

## 作業概要

Phase 7（前処理層）の §3.5 区切りカンマ／リテラル行継続の実装（`fb04c5a`）により
CardDemo `app/cbl` が **31/31 パース成功**に到達した。これを受けて Phase 7 §10 の第1増分として、
実コード（AWS CardDemo）を**再現可能なサンプルコーパス**としてリポジトリに固定・名前解決可能にする
Phase 8 仕様を起票した。

本フェーズはデモ B（ギャラリー）／ C（静的インタラクティブ）の土台であり、
本数・閾値が版に依存して揺れる問題（Phase 7 で顕在化した「CB* 14 vs 12」）を submodule 固定で解消する。

---

## 実施内容

### specs/phase8-sample-corpus.md（新規・コミット対象）

| セクション | 内容 |
|-----------|------|
| §1 目的・背景 | CardDemo を submodule でコミット固定し、本数・閾値を確定・再現可能化。samples レジストリで名前参照 |
| §2 スコープ | submodule 化・registry.json・SampleRegistry・CardDemoSpike 再ターゲット（デモ B/C・他コーパスは非スコープ）|
| §3 CardDemo submodule | `implement/samples/carddemo/` に HTTPS URL でコミット pin。Apache-2.0 の LICENSE/NOTICE を submodule 経由で保持・非改変 |
| §4 samples レジストリ | `registry.json` の最小スキーマ（name/sourceUrl/pinnedCommit/cobolDir/cobolGlobs/copybookDirs）。copybookDirs は `CobolPreprocessorOptions.CopybookPaths` へ直接渡せる形 |
| §5 ローダ | `SampleRegistry`（name→解決済み絶対パス）。配置は `CobolAnalyzer.Core` 想定・ベースディレクトリ注入可能 |
| §6 CardDemoSpike 再ターゲット | 既定でレジストリ `carddemo` を対象（明示ディレクトリ引数は後方互換で残す）。固定版で本数確定 |
| §7 受け入れ基準 | `clone --recursive`（HTTPS）取得・レジストリ解決・**31/31 再現（CB* 12 / CO* 18 / CS* 1）**・既存テスト green（92 pass 維持）・Apache-2.0 帰属保持 |
| §8 テスト | SampleRegistry 単体（解決/未登録/パス不存在/copybookDirs 受け渡し）・JSON 実ファイル検証 |
| §9 既知の制約・運用 | submodule はネットワーク前提（オフライン CI は skip 可）・CardDemo 非改変・pin 更新で upstream 追従 |
| §10 次フェーズ接続 | 完了後にデモ B（ギャラリー）→ C（静的インタラクティブ）の spec を起票 |

---

## Phase 7 からの接続

| Phase 7 の帰結 | Phase 8 での確定 |
|---------------|-----------------|
| §3.5 採用で CardDemo 31/31（前処理配線時点 27/31 から改善）| §7-3 で 31/31 を固定版の再現基準に |
| 「CB* 14 vs 12」本数不一致（§7 で比率＋名指しへ変更）| §3 で submodule 固定時に 14 の出所を確認し、CB* 12 / CO* 18 / CS* 1 を確定 |
| コピーブック検索パス注入（`CobolPreprocessorOptions.CopybookPaths`）| §4 registry の `copybookDirs` をそのまま渡せる形で定義 |

---

## 成果物

| ファイル | 状態 |
|---------|------|
| `design/specs/phase8-sample-corpus.md` | 作成・コミット対象 |
| `design/log/working/2026-07-26_01_phase8-sample-corpus-spec.md` | 本ログ・コミット対象 |

---

## 申し送り（implement 側）

- 本仕様に沿って implement 側で submodule 追加・`SampleRegistry`・`registry.json`・CardDemoSpike 再ターゲットを実装する。
- submodule 固定時に `app/cbl` の本数（CB*/CO*/CS*）を実測し、「CB* 14本」の出所を一度確認して記録する（§3）。
- 実装中に仕様の矛盾・未定義事項があれば `implement/docs/` に記録し、design 側で spec を更新する（CLAUDE.md の手順）。
- `implement/prompts/2026-07-25_03_phase8-sample-corpus-impl.md` は implement 配下のため本 design コミットには含めない。
