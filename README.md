# cobol-analyzer

COBOLコード解析・AST可視化・移行困難度指数(MDI)算出ツール

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

46年の開発経験、レガシーシステム改修の実務経験、
および1970年代からの設計思想の理解を背景に開発。

## 技術スタック

| 領域 | 技術 |
|------|------|
| パーサー | ANTLR4 + COBOLグラマー(grammars-v4) |
| バックエンド | C# / ASP.NET Core |
| フロントエンド | TypeScript + D3.js + Monaco Editor |
| テスト | xUnit(backend) / Vitest(frontend) |

## ディレクトリ構成
cobol-analyzer/
├── design/          設計・検討・仕様策定
│   ├── brainstorm/  検討ログ
│   ├── roadmaps/    ロードマップ
│   ├── specs/       確定仕様(implementへの引き渡し物)
│   └── research/    調査資料・COBOLサンプル
│
└── implement/       実装
├── src/
│   ├── backend/   ASP.NET Core API
│   └── frontend/  TypeScript + D3.js + Monaco
├── tests/
└── docs/

## 開発状況

| フェーズ | 内容 | 状態 |
|---------|------|------|
| Phase 1 | 環境構築・ANTLRパーサー | 🔲 未着手 |
| Phase 2 | AST設計・指標計算エンジン | 🔲 未着手 |
| Phase 3 | ダイアグラム可視化 | 🔲 未着手 |
| Phase 4 | 双方向ナビゲーション | 🔲 未着手 |
| Phase 5 | コメント挿入・削除 | 🔲 未着手 |
| Phase 6 | 分析機能・エクスポート | 🔲 未着手 |

## 関連プロジェクト

- [portfolio](../portfolio/) ポートフォリオサイト原稿管理

## License

MIT 