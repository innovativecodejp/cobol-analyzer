# Phase 1 実装プロンプト：環境構築・ANTLRパーサー

仕様: `../design/specs/phase1-antlr-parser.md`（実装前に必ず全文を読むこと）

---

## 前提確認

実装を開始する前に以下を確認する：

1. `../design/specs/phase1-antlr-parser.md` を読み、内容を把握する
2. `implement/src/backend/` および `implement/tests/` の現状を確認する
3. 不明点があれば実装を止めてユーザーに確認する

---

## 実装タスク一覧

以下の順序で実装する。各タスク完了後に次に進む。

### タスク 1：ソリューション・プロジェクト構造の作成

仕様 §3 のディレクトリ構造に従い、以下を作成する：

```
implement/src/backend/
├── CobolAnalyzer.sln
├── CobolAnalyzer.Parser/
│   └── CobolAnalyzer.Parser.csproj
├── CobolAnalyzer.Core/
│   └── CobolAnalyzer.Core.csproj
└── CobolAnalyzer.API/
    └── CobolAnalyzer.API.csproj
implement/tests/
└── CobolAnalyzer.Parser.Tests/
    └── CobolAnalyzer.Parser.Tests.csproj
```

- `dotnet new sln`, `dotnet new classlib`, `dotnet new webapi`, `dotnet new xunit` を使用
- 各プロジェクトを sln に追加（`dotnet sln add`）
- プロジェクト間参照を設定：
  - Parser → Core
  - API → Parser, Core
  - Tests → Parser, Core
- NuGetパッケージを仕様 §4 の通り追加

### タスク 2：ANTLR4文法ファイルの取得とコード生成

仕様 §5 に従う。

1. grammars-v4 リポジトリ（`https://github.com/antlr/grammars-v4`）の `cobol/` ディレクトリから以下を取得：
   - `Cobol.g4`
   - `CobolPreprocessor.g4`
2. `CobolAnalyzer.Parser/Grammar/` に配置
3. ファイル先頭に取得コミットハッシュをコメントで記録
4. ANTLR4ツールでC#コードを生成：
   ```bash
   java -jar antlr-4.13.x-complete.jar -Dlanguage=CSharp -package CobolAnalyzer.Parser.Generated -o ../Generated CobolPreprocessor.g4 Cobol.g4
   ```
5. `Generated/` をgit管理対象に含める（`.gitignore` で除外しないこと）

> Javaが利用できない場合はユーザーに報告して停止する。

### タスク 3：ASTノード定義

仕様 §6 の全クラスを実装する。

`CobolAnalyzer.Parser/Ast/` に以下を作成：

**NodeCategory.cs**
```csharp
public enum NodeCategory { Structure, Unit, Element }
```

**AstNode.cs**（仕様 §6.3 の通り）
- `Id`, `NodeType`, `Category`, `Location`, `Children` プロパティ
- `Id` は AstBuilder で `"{NodeType}:{StartLine}:{StartColumn}"` 形式に設定する
- `SourceLocation` record

**ProgramNode.cs** — Category: Structure
**DivisionNode.cs** — Category: Structure、`Name` プロパティ（DIVISION名）
**SectionNode.cs** — Category: Unit、`Name` プロパティ
**ParagraphNode.cs** — Category: Unit、`Name` プロパティ
**StatementNode.cs** — Category: Element、`StatementType` プロパティ
  - PERFORM / PERFORM THRU 用に `PerformFrom` / `PerformThru` プロパティを追加（nullable string）
  - ファイルI/O用に `IoVerb` / `FileName` プロパティを追加（nullable string）
  - CALL 用に `CallTarget` プロパティを追加（nullable string。静的CALLは大文字正規化済み、動的CALLは null）
**DataItemNode.cs** — Category: Element（仕様 §6.5 の通り）
  - `LevelNumber`, `Name`, `Picture?`, `RedefinesTarget?`, `IsGroup`

### タスク 4：ParseResult モデル定義

`CobolAnalyzer.Core/Models/ParseResult.cs` に仕様 §7 の通り実装：
- `ParseResult` クラス（`Ast`, `Errors`, `IsSuccess`）
- `ParseError` record（`Line`, `Column`, `Message`）

### タスク 5：CobolParserFacade の実装

`CobolAnalyzer.Parser/CobolParserFacade.cs` に実装：

- `Parse(string source)` → `ParseResult`
- ANTLR4のカスタムエラーリスナーを実装し、`ConsoleErrorListener` を無効化（仕様 §11-2）
- エラーは例外ではなく `ParseError` として収集する
- `source` が null/空の場合は `ParseResult` に `ParseError` を1件追加して返す（例外throw禁止）
- case-insensitive対応：g4の設定を確認し、`source.ToUpper()` は行わない（仕様 §11-4）

### タスク 6：AstBuilder の実装

`CobolAnalyzer.Parser/AstBuilder.cs` に実装：

ANTLR4の ParseTree を走査し、以下のマッピングを行う：

| ANTLR4 ルール | ASTノード | 備考 |
|---|---|---|
| compilationUnit / startRule | ProgramNode | ルートノード |
| identificationDivision / environmentDivision / dataDivision / procedureDivision | DivisionNode | Name に DIVISION種別 |
| workingStorageSection / fileSection 等 | SectionNode | |
| paragraph / section（PROCEDURE内） | ParagraphNode | |
| statement 各種 | StatementNode | StatementType は仕様 §6.5 IR分類表の粒度で記録 |
| dataDescriptionEntry | DataItemNode | |

**CFG保存要件**（仕様 §6.5）を必ず実装：
- GO TO → `StatementType = "GOTO"`
- ALTER → `StatementType = "ALTER"`
- PERFORM paragraph → `StatementType = "PERFORM"`、`PerformFrom` にパラグラフ名、`PerformThru = null`
- PERFORM ... THRU → `StatementType = "PERFORM_THRU"`、`PerformFrom`/`PerformThru` にパラグラフ名
- PERFORM UNTIL/VARYING → `StatementType = "PERFORM_LOOP"`、条件式テキストを保持
- CALL "program-name" → `StatementType = "CALL"`、`CallTarget` に大文字正規化済みプログラム名
- CALL identifier → `StatementType = "CALL"`、`CallTarget = null`

**DFG保存要件**（仕様 §6.5）を必ず実装：
- DataItemNode の `LevelNumber`, `Picture`, `RedefinesTarget` を正しく設定
- READ/WRITE 文の `IoVerb`, `FileName` を StatementNode に設定

`SourceLocation` は ANTLR4の `IToken.Line` / `Column` / `Stop` から取得する。

### タスク 7：REST API の実装

仕様 §8 の通り実装。

**Program.cs**：
- Swagger/OpenAPI を有効化
- `System.Text.Json` の `MaxDepth` を 256 以上に設定（仕様 §11-3）
- 循環参照ハンドリングは不要（構造上発生しない）だが `MaxDepth` は明示的に設定する

**ParseController.cs**：
- `POST /api/parse` エンドポイント
- リクエスト: `{ "source": string }`
- `source` が null/空 → 400 Bad Request
- `CobolParserFacade.Parse()` を呼び出し
- 成功/エラーともに 200 OK で `ParseResult` を JSON 返却
- サーバー内部例外 → 500 Internal Server Error

### タスク 8：テストデータの作成

`tests/CobolAnalyzer.Parser.Tests/TestData/` に以下を作成：

- `hello.cbl` — 仕様 §9 記載のサンプルをそのままコピー
- `syntax-error.cbl` — 構文エラーを含むサンプル（ピリオド抜けなど）
- `goto-sample.cbl` — 仕様 §9 記載のサンプルをそのままコピー
- `data-sample.cbl` — 仕様 §9 記載のサンプルをそのままコピー

### タスク 9：テストの実装

**ParserTests.cs** に仕様 §9 の4テストを実装：
- `Parse_HelloWorld_ReturnsAstWithDivisions`
- `Parse_EmptySource_ReturnsError`
- `Parse_SyntaxError_ReturnsErrors`
- `Parse_SourceLocation_IsCorrect`

**AstBuilderTests.cs** に仕様 §9 の9テストを実装：
- `Build_ProcedureDivision_ContainsParagraphs`
- `Build_WorkingStorage_ContainsSection`
- `Build_GoTo_StatementTypeIsGoto`
- `Build_Perform_StatementTypeIsPerform`
- `Build_PerformThru_PreservesFromAndThru`
- `Build_DataItem_PreservesLevelAndPicture`
- `Build_Redefines_PreservesTargetName`
- `Build_GroupItem_IsGroupTrue`
- `Build_NodeCategory_MatchesExpected`

テストデータは `.cbl` ファイルを `File.ReadAllText()` で読み込む。ハードコードしない。

---

## 完了確認

仕様 §10 の完了基準をすべて確認する：

```
dotnet build
dotnet test
dotnet run --project src/backend/CobolAnalyzer.API
```

- `POST /api/parse` に `hello.cbl` の内容を送信 → `isSuccess: true`
- `POST /api/parse` に `syntax-error.cbl` の内容を送信 → `errors` に1件以上
- Swagger UI（`/swagger`）でエンドポイントが確認できる

---

## 仕様との矛盾発見時の対処

実装中に仕様の矛盾・未定義事項を発見した場合：

1. `implement/docs/` にフィードバック内容を記録する
2. 実装を停止してユーザーに報告する
3. ユーザーの指示を待つ（`design/specs/` を自分で変更しない）
