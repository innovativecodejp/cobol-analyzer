# Phase 7 前処理 実装フィードバック：CardDemo 再スパイクで判明したギャップ

作成日: 2026-07-25
作成者: implement 側 Claude Code
対象仕様: `design/specs/phase7-preprocessing.md`

前処理層（§3）を実装・配線し、CardDemo `app/cbl`（31本）で再スパイクした結果、
仕様の前提と実測の間にギャップを発見した。design 側でこの文書を参照し、spec を更新すること。
implement 側は spec を自分で更新しない。

測定詳細: `implement/log/working/2026-07-25_01_phase7-preprocessing-respike.md`

---

## 実測サマリ

- 前処理配線により **2/31 → 27/31** へ改善。
- Batch(CB*) **10/12**、CICS(CO*) **17/18**、Other(CS*) 0/1。
- 全単体テスト green（Parser 28 / Engine 56 / API 3）。既存自由形式データも前処理経由で `IsSuccess`。

---

## 1. 未定義事項：区切りカンマ（`,`）の扱い

**現象**: バッチ CBACT04C.cbl と CSUTLDTC.cbl が、以下のような**区切りカンマ**で失敗する。

```cobol
       STRING PARM-DATE,                 *> CBACT04C
              WS-TRANID-SUFFIX
         DELIMITED BY SIZE

       CALL "CEEDAYS" USING              *> CSUTLDTC
              WS-DATE-TO-TEST,
              WS-DATE-FORMAT,
```

- エラー: `no viable alternative at input 'WS-DATE-TO-TEST,'`。現行文法（生成済み `Cobol85Parser`）は
  `CALL … USING` / `STRING` の作用対象に続く区切りカンマを受け付けない。
- **実証**: 区切りカンマを空白へ置換すると両ファイルとも **pass**（＝カンマが唯一の詰まり）。

**なぜ spec 更新が要るか**: カンマは COBOL の任意区切り文字（空白と等価）だが、
`PICTURE` 挿入文字（例 `PIC ZZ,ZZ9`）や**リテラル内**（CardDemo 実例 `'… Segoe UI,sans-serif …'`）にも出現する。
無条件置換はこれらを破壊する。安全な正規化には「リテラル外・PIC 句外の区切りカンマのみ空白化」が必要で、
これは §3.1〜§3.4 のどこにも定義がない。

**確認したいこと**:
- (A) §3 に「区切りカンマ正規化（リテラル/PIC を除外）」を追加するか？
      → 追加すれば CBACT04C（§8 が挙げた代表バッチ）が pass、Batch は **11/12** になる。
- (B) それとも §9 の既知制約として据え置くか？

---

## 2. 前提の不一致：CB* の本数（14 vs 12）

- 仕様 §7-3・タスク6 は「バッチ系（CB*、**14本**）の大半（目標 ≥ 12/14）」と記述。
- 取得した CardDemo（`aws-samples/aws-mainframe-modernization-carddemo`, shallow clone）の `app/cbl` は
  CB* が **12本**（`CBACT01C-04C, CBCUS01C, CBEXPORT, CBIMPORT, CBSTM03A/B, CBTRN01C-03C`）。
- 実測 **10/12 pass（83%）**。「大半」は満たすが、絶対数 ≥12 は未達。

**確認したいこと**: 目標記述（本数・閾値）を実コーパス（12本）に合わせて更新するか。
submodule 化する版（コミットハッシュ固定）で本数を確定させるのが望ましい。

---

## 3. 既知制約の確認（spec §9 の範囲内。参考記録）

以下は §9 に既記載の best-effort/非対応で、今回スコープ外として扱った。追加対応の要否のみ確認したい。

- **CBSTM03A.CBL**: HTML を含む `VALUE` リテラルの**行継続**（col7 `-`）。継続時の再開クォート結合が
  未対応で `mismatched input '=' expecting DOT_FS`。→ §9「文字列リテラル継続は best-effort」。
- **COACTUPC.cbl**: `COPY … REPLACING`（CardDemo で本ファイルのみ）。§3.3 で警告＋無害化したが、
  周辺の高複雑度 CICS ロジックと相まって pass せず。→ §9「COPY REPLACING 非対応」。

---

## 実装側の現状（確定済み・変更不要）

- `ParseResult.Warnings`（`List<ParseWarning(int Line, string Kind, string Message)>`）を追加。`IsSuccess` 不変。
- `CobolPreprocessorOptions`（`CopybookPaths` / `CopybookExtensions` / `MaxCopyDepth`）で注入可能。
- 未解決 COPY / REPLACING / 深さ超過 / 循環 / EXEC 縮約を警告として記録（`ParseWarningKind`）。

design 側の判断（特に §1 の A/B）を待って、必要なら実装を再開する。
