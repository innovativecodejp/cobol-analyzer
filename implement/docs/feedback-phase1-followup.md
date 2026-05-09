# Phase 1 追補対応メモ

作成日: 2026-05-10

## 位置づけ

Phase 1 は完了扱いとする。
以下は Phase 1 実装プロンプト更新後のレビューで見つかった、仕様追補・テスト補強候補である。

## 残件

- `AstNode` の `NodeType` プロパティと JSON 出力形状を確認する。
- `PERFORM paragraph` の AstBuilder テストを追加する。
- `CALL "program"` / `CALL identifier` の `CallTarget` テストを追加する。
- `JsonSerializerOptions.MaxDepth` を 256 以上にする。

## 扱い

- Phase 1 完了を取り消すものではない。
- 後続 Phase の作業は止めない。
- `design/specs/` の変更が必要な場合は design 側で判断する。
