# cobol-analyzer プロジェクト共通ルール

## プロジェクト概要
COBOLコード解析・AST可視化・移行困難度指数(MDI)算出ツール
ポートフォリオ公開目的、GitHubで公開予定

## ディレクトリの役割
- design/   : 設計・検討専用。実装コードを置かない
- implement/: 実装専用。design/specs/ を参照して実装する

## 基本方針
- design/ での検討内容は specs/ に集約してから implement/ に引き渡す
- implement/ は specs/ のみを根拠として実装する。brainstorm・roadmaps は参照しない
- 仕様に不明点があれば実装を止めて確認する

## 実装フィードバックの扱い
実装中に仕様の矛盾・未定義事項・前提の崩れを発見した場合は、以下の手順で対処する。

1. フィードバック内容を `implement/docs/` に記録して実装を止め、ユーザーに確認する
2. ユーザーが design サブプロジェクトの Codex を起動する
3. design 側で `implement/docs/` を参照し、`design/specs/` を更新する
4. implement 側で更新後の `design/specs/` を参照して実装を再開する

implement は design/specs/ を更新しない。フィードバックの記録（implement/docs/）と仕様の更新（design/specs/）は必ず分離する。

## ポートフォリオとの連携
成果・知見は ~/dev/projects/portfolio/ に反映する
