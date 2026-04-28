# 実装用Claude Code ルール

## 仕様の参照先(実装前に必ず読む)
../design/specs/architecture.md
../design/specs/ast-spec.md
../design/specs/mdi-spec.md
../design/specs/ui-spec.md
../design/specs/feature-comment.md

## 実装ルール
- specs/ の内容を実装の basis とする
- brainstorm/ roadmaps/ は参照しない
- 仕様に不明点があれば実装を止めて確認を求める
- テストを必ず書く

## モデル切り替え基準
- Sonnet: 通常実装・テスト・リファクタリング・git操作
- Opus  : アーキテクチャ判断・複雑な設計変更