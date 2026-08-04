# cobol-analyzer

COBOLコード解析・AST/CFG/DFG可視化・移行困難度指数(MDI)算出ツール

## 公開デモ(バックエンド不要)

実在するCOBOL基幹システム(**AWS CardDemo**・全31プログラム)を解析した結果を、そのままブラウザで確認できます。

**入口 → https://innovativecodejp.github.io/cobol-analyzer/**

| デモ | 内容 |
|------|------|
| [**ギャラリー**](https://innovativecodejp.github.io/cobol-analyzer/gallery/) | 全31本の移行優先度ランキング、プログラム間依存グラフ、対象9本の注釈レポートとAST/CFG/DFG図、プロジェクト移行設計書 |
| [**インタラクティブ解析**](https://innovativecodejp.github.io/cobol-analyzer/app/) | 事前計算結果をブラウザで操作。AST折りたたみ / CFG・DFGナビ / ノード↔ソース双方向ハイライト / GO TO・PERFORMジャンプ / データ影響閉包 / MDIパネル |

サンプルコーパスは [AWS CardDemo](https://github.com/aws-samples/aws-mainframe-modernization-carddemo)(Apache-2.0)を
コミット `59cc6c2` で固定参照したもの(改変なし)。解析結果は事前計算した静的成果物(JSON / Markdown / SVG)で、
サーバを必要としません。

## 概要

COBOL → C# 移行プロジェクトにおける**移行前の設計判断支援**を目的としたツール。
LLMによるコード変換ではなく、移行の難しさを可視化・定量化し、
アーキテクト視点での設計判断を支援する。

### 主な機能

- ASTダイアグラム可視化(D3.js)
- 双方向ナビゲーション(ノード↔コード、GOTO/PERFORM飛び先追跡)
- データ項目トレース・変更影響分析
- 複雑度指数(MDI: Migration Difficulty Index)算出
- プログラム間依存グラフ
- コメント挿入(タグ付き)・正規表現削除
- 移行優先度ランキング
- 注釈レポート・移行設計書自動生成(Markdown)

## 背景・開発動機

COBOL移行案件において、既存の自動変換ツールは
「コードを別言語に書き換える」ことに注力しているが、
**移行前に何が難しいかを把握する**ツールは少ない。

本ツールは移行設計者が「どこから手をつけるか」
「どこにリスクがあるか」を判断するための
**移行前工程の設計支援**に特化する。

45年以上の開発経験、レガシーシステム改修の実務経験、
および1970年代からの設計思想の理解を背景に開発。

## 技術スタック

| 領域 | 技術 |
|------|------|
| パーサー | ANTLR4 + COBOLグラマー(grammars-v4) |
| バックエンド | C# / ASP.NET Core |
| フロントエンド | TypeScript + D3.js + Monaco Editor |
| テスト | xUnit(backend) / Vitest(frontend) |

## ディレクトリ構成

```
cobol-analyzer/
├── design/              設計・検討・仕様策定
│   ├── specs/           確定仕様(implementへの引き渡し物)
│   ├── roadmaps/        ロードマップ
│   ├── brainstorm/      検討ログ
│   ├── research/        調査資料・COBOLサンプル
│   └── log/             設計作業ログ
│
├── implement/           実装
│   ├── src/backend/     ASP.NET Core API
│   ├── src/frontend/    TypeScript + D3.js + Monaco
│   ├── tools/           事前計算ツール(DemoPrecompute)・パース検証(CardDemoSpike)
│   ├── samples/         サンプルコーパス定義(registry.json)+ CardDemo(submodule)
│   ├── tests/
│   └── log/             実装作業ログ
│
└── docs/                公開デモの静的成果物(GitHub Pages 配信元)
```

## 開発状況

Phase 1〜9 を実装。合成サンプルでの機能実装(Phase 1〜6)を経て、
実在コードでの動作(Phase 7〜8)と公開デモ(Phase 9)まで到達している。

| フェーズ | 内容 | 状態 |
|---------|------|------|
| Phase 1 | 環境構築・ANTLRパーサー | ✅ 実装済み |
| Phase 2 | AST設計・指標計算エンジン | ✅ 実装済み |
| Phase 3 | ダイアグラム可視化 | ✅ 実装済み |
| Phase 4 | 双方向ナビゲーション | ✅ 実装済み |
| Phase 5 | コメント挿入・削除 | ✅ 実装済み |
| Phase 6 | 分析機能・エクスポート | ✅ 実装済み |
| Phase 7 | 実コード対応・前処理層(COPY展開 / EXEC縮約 / 区切りカンマ / リテラル行継続) | ✅ 実装済み |
| Phase 8 | サンプルコーパス統合(CardDemoをsubmoduleで固定・名前解決) | ✅ 実装済み |
| Phase 9 | 静的デモ(事前計算パイプライン + ギャラリー / インタラクティブ) | ✅ 実装済み |

### 実コードでの結果

- CardDemo `app/cbl` の **31本すべてがパース成功**(CB\* 12 / CO\* 18 / CS\* 1)。
- 同 **31本すべてが解析(AST / CFG / DFG / MDI)まで完走**。
- デモ対象の9本は決定論的に選定(MDI上位8本 + バケット代表 `CS*` 1本)。固定コーパス・固定重みに対して一意に定まる。
- テスト: backend 107 / frontend 49。

## 既知の制約

- **MDI の分布**: 上記コーパス(CardDemo)と既定の重み設定では、31本すべてが `Low`(最大 24.3)に収まる。
  デモを見栄えさせるための重み調整は行っていない。
- **静的デモの制約**: 事前計算済みの対象のみを扱う。任意のCOBOLをアップロードしての再解析、
  コメント挿入・削除(ソース書き換えを伴う機能)はバックエンドが必要なため静的デモでは提供しない。
- 動的CALL解析、`COPY REPLACING` は未対応。

## 関連プロジェクト

- [基幹構造設計室 / Core Systems Architecture](https://innovative-code.jp) — COBOL→C#移行の実務・研究(サイト)

## License

MIT

サンプルコーパスとして参照している [AWS CardDemo](https://github.com/aws-samples/aws-mainframe-modernization-carddemo) は
Apache-2.0 であり、本リポジトリのライセンスとは別に、その条件に従います(submoduleとして未改変で固定参照)。
