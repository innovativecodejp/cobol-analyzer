# フィードバック：StatementNode への IF 真偽節情報の追加

日付: 2026-05-08  
発見タスク: タスク 3 (CfgBuilder 実装)

## 問題

仕様 §3.1 StatementNode に TrueStatements / FalseStatements プロパティの定義がない。
一方、仕様 §4.5 CfgBuilder は「IF → ConditionalTrue + ConditionalFalse エッジを生成する」と規定している。

現在の AstBuilder は IF 文を StatementNode (StatementType="IF") として生成するが、
真節・偽節内のネスト文は Children に追加していない。そのため CfgBuilder は
ConditionalTrue/False エッジの接続先ブロックの内容を把握できない。

## 対処（実装側で決定）

StatementNode に以下のプロパティを追加する（spec §3.1 の暗黙要件として扱う）：

```csharp
public List<StatementNode> TrueStatements { get; init; } = new();
public List<StatementNode> FalseStatements { get; init; } = new();
```

AstBuilder.BuildIf を拡張して ifThen().statement() / ifElse().statement() から生成する。

## design/specs への反映要否

仕様 §3.1 StatementNode に上記2プロパティを追記することを推奨。
JSON レスポンスにも trueStatements / falseStatements フィールドが現れる。
