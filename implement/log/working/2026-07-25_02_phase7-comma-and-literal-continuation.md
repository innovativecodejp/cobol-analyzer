# Phase 7 前処理（第2弾）：区切りカンマ正規化 & リテラル行継続

- 実行日: 2026-07-25
- 対象仕様: `../../design/specs/phase7-preprocessing.md`（更新済み §3.5 / §3.6 / §7-5 / §8 / §9）
- 実装プロンプト: `../prompts/2026-07-25_02_phase7-comma-and-literal-continuation-impl.md`
- 前段: `2026-07-25_01_phase7-preprocessing-respike.md`（27/31）

## 実装したもの

1. **共有リテラル追跡スキャナ** `CobolPreprocessor.LiteralScanner`（状態機械）
   - 非数字リテラル（`'…'` / `"…"`、二重引用符エスケープ対応）・擬似テキスト（`== … ==`）・行内注記（`*>`）を追跡。
   - 走査開始状態を受け取り、終端状態＋文字ごとの「保護マスク」を返す。§3.1 継続と §3.5 で共有。
2. **§3.1 リテラル行継続の是正**（`JoinContinuation`）
   - col7 `-` 継続で直前行が開いたリテラルの途中なら、継続行 Area B の**再開クォートを除去**して連結。
   - CBSTM03A（HTML を含む `VALUE` リテラルの行継続）が正しく 1 リテラルへ再結合。
3. **§3.5 区切りカンマ正規化**（`NormalizeSeparatorCommas`、パイプライン最終段 §3.6）
   - 「カンマ + 直後空白 / 行末終端」のカンマ 1 文字を空白 1 文字へ**同一長置換**（桁位置保存）。
   - 保護マスク（リテラル/擬似テキスト/注記内部）は対象外。EXEC 縮約の後段のため SQL `SELECT A, B` に触れない。
   - PIC（`ZZ,ZZ9`）・`DECIMAL-POINT IS COMMA`（`1,5`）・添字（`TBL(I,J)`）は「直後が空白でない」ため自動的に非対象。

## テスト（`dotnet build` / `dotnet test`）

- ビルド 0 エラー、全 green: **Parser 33 / Engine 56 / API 3 = 92 pass, 0 fail**。
- 追加テスト（`PreprocessorTests.cs`）:
  - §3.5: CALL/STRING 区切りカンマ正規化＋パース成功、PIC/小数点/リテラル内/擬似テキスト/添字カンマの非破壊、同一長・桁保存。
  - §7-5 golden 非破壊性: `'Segoe UI,sans-serif'` が正規化前後で不変。
  - §3.1 リテラル行継続（CBSTM03A 型）: 再開クォート除去で再結合し、内部カンマ保護、パース成功。
- フィクスチャは実ファイル（`TestData/preprocess/comma-separator.cbl` / `comma-cases.cbl` / `literal-continuation.cbl`）。

## CardDemo 再スパイク（第2弾）

対象: CardDemo `app/cbl`（31本）、コピーブック `app/cpy`。

| バケット | 第1弾 | **第2弾** |
|---|---|---|
| Batch(CB*)  | 10 / 12 | **12 / 12** |
| CICS(CO*)   | 17 / 18 | **18 / 18** |
| Other(CS*)  | 0 / 1 | **1 / 1** |
| **合計**    | 27 / 31 | **31 / 31** |

- **バッチ目標達成**: CB* 12/12（目標 ≥11/12）。名指し必須（CBACT01C–04C / CBTRN01C–03C）すべて pass。
- **CICS 目標達成**: CO* 18/18（目標 ≥17/18）。
- 第1弾で残った 4 本の解消内訳:
  - CBACT04C（`STRING PARM-DATE,`）/ CSUTLDTC（`CALL … USING …,`）→ §3.5 区切りカンマで pass。
  - CBSTM03A（HTML `VALUE` リテラル行継続）→ §3.1 リテラル継続是正で pass。
  - COACTUPC（`COPY … REPLACING`＋区切りカンマ `extraneous input ','`）→ REPLACING 無害化済み＋§3.5 で pass。

> 注: §9 は COACTUPC を「COPY REPLACING の影響で pass せず」既知制約としていたが、実測では区切りカンマ解消により
> 構造解析可能水準で pass した（REPLACING 対象データは無害化のため未定義のまま、意味論解析は非対象）。
> 通過率の観点では全通過だが、REPLACING 非対応という制約自体（展開内容の欠落）は §9 のとおり残る。

## 受け入れ基準（§7）

- [x] `dotnet build` / `dotnet test` グリーン（92 pass）。
- [x] 既存自由形式データ（hello/data-sample/goto-sample）が前処理経由でも `IsSuccess`。
- [x] CardDemo 再スパイク Batch 12/12（≥11/12、名指し必須すべて pass）、CICS 18/18（≥17/18）。
- [x] §7-5 非破壊性 golden test green（リテラル不変）。
- [x] 再現可能な測定ハーネス（`tools/CardDemoSpike`）。

## 申し送り

- 次フェーズ（spec §10）: CardDemo の `implement/samples/` submodule 化（コミットハッシュ固定で本数確定）、samples レジストリ、デモ B/C。
- 本数固定後、閾値（現状 CB* 12・CO* 18・CS* 1）を submodule 版基準に確定。
