# cobol-analyzer ロードマップ

作成日: 2026-05-05  
更新日: 2026-07-26（Phase 7 前処理・Phase 8 サンプルコーパス・Phase 9 デモの起票／確定を反映）

---

## フェーズ一覧

| フェーズ | タイトル | 状態 | 仕様書 |
|---------|---------|------|--------|
| Phase 1 | 環境構築・ANTLRパーサー | ✅ 仕様確定 | `specs/phase1-antlr-parser.md` |
| Phase 2 | AST設計・指標計算エンジン | ✅ 仕様確定 | `specs/phase2-engine.md` |
| Phase 3 | ダイアグラム可視化 | ✅ 仕様確定 | `specs/phase3-visualization.md` |
| Phase 4 | 双方向ナビゲーション | ✅ 仕様確定 | `specs/phase4-navigation.md` |
| Phase 5 | コメント挿入・削除 | ✅ 仕様確定 | `specs/phase5-comment.md` |
| Phase 6 | 分析機能・エクスポート | ✅ 仕様確定 | `specs/phase6-export.md` |
| Phase 7 | 実コード対応・前処理層 | ✅ 仕様確定 | `specs/phase7-preprocessing.md` |
| Phase 8 | サンプルコーパス統合 | ✅ 仕様確定 | `specs/phase8-sample-corpus.md` |
| Phase 9 | デモ B（ギャラリー）／ C（静的インタラクティブ） | ✅ 仕様確定 | `specs/phase9-demo-gallery-interactive.md` |

---

## Phase 1：環境構築・ANTLRパーサー

**目的**: COBOLソースをパースしてASTをJSONで返すAPIの基盤を作る

### 主な成果物
- .NET 8 / ASP.NET Core ソリューション構成
- ANTLR4 + grammars-v4 Cobol.g4 によるパーサー
- ASTノード階層（ProgramNode / DivisionNode / SectionNode / ParagraphNode / StatementNode / DataItemNode）
- `POST /api/parse` エンドポイント

### 完了基準（抜粋）
- `dotnet build` / `dotnet test` が通る
- hello.cbl のパースで `isSuccess: true` が返る
- Swagger UI でエンドポイントが確認できる

### 依存
- なし（起点フェーズ）

---

## Phase 2：AST設計・指標計算エンジン

**目的**: CFG・DFGの構築と MDI（移行困難度指数）算出エンジンを実装する

### 主な成果物
- ASTノード拡張（ConditionNode / PerformDetailsNode / DataReferenceNode）
- `CobolAnalyzer.Engine` プロジェクト
  - CFG（基本ブロック分割・8種エッジ・ALTER/再帰検出）
  - DFG（Define/Use/Redefines/GroupOf・影響閉包）
  - MDI計算エンジン（CC / GD / AD / ND / RD / CS の6指標、加重スコア式）
- `POST /api/analyze` エンドポイント（AST + CFG + DFG + Metrics を返す）
- `appsettings.json` による MDI重み・飽和点の外部設定化

### 完了基準（抜粋）
- goto-sample.cbl で CFG に GoTo エッジが含まれる
- data-sample.cbl で DFG に Redefines エッジが含まれる
- MDI スコアが返り、appsettings 変更でスコアが変わる

### 依存
- Phase 1 完了

---

## Phase 3：ダイアグラム可視化

**目的**: Phase 2 の CFG / DFG / AST を D3.js でブラウザ上に可視化する

### 主な成果物
- TypeScript + D3.js フロントエンド（Vite/Vitest）
- ASTツリー表示（折りたたみ可能なツリーダイアグラム）
- CFGグラフ表示（ノード＝基本ブロック、エッジ＝制御遷移、エッジ種別カラーリング）
- DFGグラフ表示（ノード＝データ項目、エッジ＝依存種別カラーリング）
- MDIスコア・リスクランクのサマリーパネル
- Monaco Editor によるCOBOLソース入力・表示

### 設計上の注意
- Phase 2 の `POST /api/analyze` レスポンス形式（CFG/DFG の JSON 構造）を確定してから設計する
- D3.js の `{ nodes, links }` 形式と API レスポンスのマッピングを仕様に明記する

### 依存
- Phase 2 完了（API レスポンス形式の確定）

---

## Phase 4：双方向ナビゲーション

**目的**: ダイアグラムのノードとCOBOLソースコードを相互に追跡できるようにする

### 主な成果物
- ノードクリック → ソースの該当行ハイライト
- ソース行クリック → 対応ノードのハイライト
- GO TO / PERFORM の遷移先ジャンプ
- データ項目クリック → 影響閉包のハイライト表示

### 依存
- Phase 3 完了

---

## Phase 5：コメント挿入・削除

**目的**: COBOLソースへのタグ付きコメント挿入と正規表現による一括削除を提供する

### 主な成果物
- コメント挿入 API（`POST /api/comment/insert`）
  - 指定パラグラフ・行番号へのコメント追加
  - タグ形式（例: `* [MDI:HIGH] ...`）
- コメント削除 API（`POST /api/comment/remove`）
  - 正規表現マッチによるコメント行の一括削除
- フロントエンド：コメント挿入パネル

### 依存
- Phase 3 完了（ソース表示基盤）

---

## Phase 6：分析機能・エクスポート

**目的**: 移行優先度ランキング・注釈レポート・移行設計書の自動生成を提供する

### 主な成果物
- プログラム間依存グラフ（複数 COBOL ファイルの CALL 関係）
- 移行優先度ランキング（MDI スコア順）
- 注釈レポート出力（Markdown）
- 移行設計書自動生成（Markdown テンプレート）
- プロジェクト分析 API（`POST /api/project/analyze`）
- エクスポート API（`POST /api/export/annotation-report`, `POST /api/export/migration-design`）

### 依存
- Phase 2 完了（MDI スコア）
- Phase 5 完了（コメント情報）

---

## Phase 7：実コード対応・前処理層

**目的**: 実運用の COBOL（AWS CardDemo）をパースできるよう、文法生成前に前処理層を挟む

### 主な成果物
- COBOL 前処理層（COPY 展開・EXEC 縮約・区切りカンマ正規化・リテラル行継続）
- `CobolPreprocessorOptions`（`CopybookPaths` / `CopybookExtensions` / `MaxCopyDepth`）
- `ParseResult.Warnings`（`IsSuccess` は不変で警告を記録）
- §3.5 区切りカンマ正規化（リテラル／PIC 句を除外）＋リテラル行継続

### 完了基準（抜粋）
- CardDemo `app/cbl` が前処理経由で **31/31 パース成功**
- コピーブック検索パス（`CopybookPaths`）が注入可能

### 依存
- Phase 1 完了（パーサー基盤）

---

## Phase 8：サンプルコーパス統合

**目的**: 実コードを再現可能なサンプルコーパスとして固定・名前解決可能にする

### 主な成果物
- CardDemo を submodule でコミット固定（HTTPS・pin `59cc6c2`、Apache-2.0 帰属保持）
- `implement/samples/registry.json`（サンプル定義）
- ローダ `SampleRegistry`（name → 解決済み絶対パス、`copybookDirs` を前処理へ受け渡し）
- `CardDemoSpike` をレジストリ既定へ再ターゲット

### 完了基準（抜粋）
- 固定版で **31/31（CB\* 12 / CO\* 18 / CS\* 1）** を再現（「CB\* 14本」は誤りと確定）
- 既存テスト green（100 pass）、Apache-2.0 帰属がツリーに存在

### 依存
- Phase 7 完了（前処理で実コードが通る）

---

## Phase 9：デモ B（ギャラリー）／ C（静的インタラクティブ）

**目的**: 固定コーパスの解析結果を事前計算し、バックエンド不要で GitHub Pages に静的ホストする公開デモを作る

### 主な成果物
- 事前計算パイプライン（レジストリ `carddemo` を解析 → JSON / Markdown / 図）
- デモ B（ギャラリー）: Phase 6 エクスポート＋AST/CFG/DFG 図の静的書き出し、全31本ランキング表
- デモ C（静的インタラクティブ）: フロント `src/api/*.ts` 境界を静的 JSON 読み込みへ差し替え（`VITE_STATIC_DATA`）
- GitHub Pages 出力（`docs/`）・`base` パス・Apache-2.0/pin 帰属表示

### 設計上の注意
- デモ対象は **MDI 上位 N（既定8）＋バケット代表**を決定論的に選定。全31本はランキング表で網羅
- 静的モードは `API_BASE` へ非通信。ライブ再解析（任意ソース）は非スコープ

### 依存
- Phase 8 完了（コーパス固定・名前解決）
- Phase 6 完了（エクスポート）／ Phase 3・4 完了（可視化・ナビ）

---

## 設計フェーズ進捗

| 仕様書 | バージョン | 更新日 | 状態 |
|--------|-----------|--------|------|
| `specs/phase1-antlr-parser.md` | 1.4 | 2026-05-10 | ✅ 確定 |
| `specs/phase2-engine.md` | 1.5 | 2026-05-12 | ✅ 確定 |
| `specs/phase3-visualization.md` | 1.4 | 2026-05-23 | ✅ 確定 |
| `specs/phase4-navigation.md` | 1.3 | 2026-05-23 | ✅ 確定 |
| `specs/phase5-comment.md` | 1.2 | 2026-05-08 | ✅ 確定 |
| `specs/phase6-export.md` | 1.2 | 2026-05-12 | ✅ 確定 |
| `specs/phase7-preprocessing.md` | — | 2026-07-25 | ✅ 確定 |
| `specs/phase8-sample-corpus.md` | — | 2026-07-25 | ✅ 確定 |
| `specs/phase9-demo-gallery-interactive.md` | — | 2026-07-26 | ✅ 確定 |
