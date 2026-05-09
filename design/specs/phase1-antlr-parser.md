# Phase 1 仕様：環境構築・ANTLRパーサー

バージョン: 1.4  
作成日: 2026-05-03  
更新日: 2026-05-10（整合性レビュー反映済み。Phase 4 実装フィードバック対応: StatementType "PERFORM" 追加）  
ステータス: 確定（implement/ への引き渡し可）

---

## 1. 目的・スコープ

COBOLソースコードをANTLR4でパースし、ASTをJSONとして返すREST APIを構築する。
本フェーズは後続フェーズ（MDI算出、CFG/DFG構築、可視化）のパーサー基盤となる。

### スコープ内
- 開発環境セットアップ
- ソリューション・プロジェクト構造の初期構築
- ANTLR4 + grammars-v4 Cobol.g4 によるCOBOLパーサー実装
- ParseTree → ASTノード変換（基本構造）
- REST API（`POST /api/parse`）
- xUnit単体テスト

### スコープ外（Phase 2以降）
- MDI（移行困難度指数）算出
- CFG / DFG 構築
- フロントエンドUI
- COPYBOOK展開（プリプロセッサ統合）
- COBOL方言対応（Fujitsu/NEC系）

---

## 2. 前提ツール

| ツール | バージョン | 用途 |
|--------|-----------|------|
| .NET SDK | 8.0 (LTS) | バックエンド開発・ビルド |
| Java | 11以上 | ANTLR4ツール（コード生成時のみ） |
| ANTLR4ツール | 4.13.x | g4ファイルからC#コード生成 |
| Git | 任意 | ソース管理 |

Node.js / npmはPhase 3まで不要。

---

## 3. ディレクトリ構造

```
implement/
├── src/
│   └── backend/
│       ├── CobolAnalyzer.sln
│       ├── CobolAnalyzer.Parser/
│       │   ├── CobolAnalyzer.Parser.csproj
│       │   ├── Grammar/
│       │   │   ├── Cobol.g4                   ← grammars-v4からコピー
│       │   │   └── CobolPreprocessor.g4       ← grammars-v4からコピー
│       │   ├── Generated/                     ← ANTLR4生成C#コード（git管理対象）
│       │   ├── Ast/
│       │   │   ├── AstNode.cs
│       │   │   ├── NodeCategory.cs
│       │   │   ├── ProgramNode.cs
│       │   │   ├── DivisionNode.cs
│       │   │   ├── SectionNode.cs
│       │   │   ├── ParagraphNode.cs
│       │   │   ├── StatementNode.cs
│       │   │   └── DataItemNode.cs
│       │   ├── CobolParserFacade.cs
│       │   └── AstBuilder.cs
│       ├── CobolAnalyzer.Core/
│       │   ├── CobolAnalyzer.Core.csproj
│       │   └── Models/
│       │       └── ParseResult.cs
│       └── CobolAnalyzer.API/
│           ├── CobolAnalyzer.API.csproj
│           ├── Program.cs
│           └── Controllers/
│               └── ParseController.cs
└── tests/
    └── CobolAnalyzer.Parser.Tests/
        ├── CobolAnalyzer.Parser.Tests.csproj
        ├── ParserTests.cs
        ├── AstBuilderTests.cs
        └── TestData/
            ├── hello.cbl       ← 最小限の正常なCOBOLサンプル
            └── syntax-error.cbl ← 構文エラーを含むサンプル
```

---

## 4. NuGetパッケージ

### CobolAnalyzer.Parser
```xml
<PackageReference Include="Antlr4.Runtime.Standard" Version="4.13.*" />
```

### CobolAnalyzer.API
```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />
```

### CobolAnalyzer.Parser.Tests
```xml
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
```

---

## 5. ANTLR4文法ファイル取得

grammars-v4リポジトリの以下ファイルを `Grammar/` にコピーして使用する。

- リポジトリ: `https://github.com/antlr/grammars-v4`
- パス: `cobol/`
- 対象ファイル:
  - `Cobol.g4`
  - `CobolPreprocessor.g4`
- 取得コミット: ファイルヘッダに記録すること（例: `// grammars-v4 commit: abc1234`）

### コード生成コマンド
```bash
# Grammar/ ディレクトリで実行
java -jar antlr-4.13.x-complete.jar -Dlanguage=CSharp -package CobolAnalyzer.Parser.Generated -o ../Generated CobolPreprocessor.g4 Cobol.g4
```

Generated/ フォルダはgit管理対象とする（CI環境でのJava依存を排除するため）。

---

## 6. ASTノード定義

### 6.1 形式モデル（理論的根拠）

`design/docs/CobolStructureAnalysis.md` の形式定義に基づく：

> T_AST = (V, E, λ)  
> V : 構文ノード集合  
> E : 包含関係（親子エッジ）  
> λ : ノード型割当関数 V → NodeType

実装上の対応：

| 形式モデル | C# 実装 |
|-----------|---------|
| V の各要素 | `AstNode` インスタンス |
| E（包含関係） | `AstNode.Children` リスト |
| λ（ノード型割当） | `AstNode.NodeType` プロパティ |

### 6.2 ノード粒度分類（3レベル）

研究資料の「3レベルノード分類」に対応する `NodeCategory` を持たせる。
Phase 2以降のCFG/IR構築時に分類フィルタリングに使用する。

```csharp
public enum NodeCategory
{
    Structure,    // レベル1：プログラム骨格（Program, Division）
    Unit,         // レベル2：作用単位境界（Section, Paragraph, DataGroup）
    Element       // レベル3：最小構文要素（Statement, DataItem）
}
```

### 6.3 基底クラス

```csharp
// AstNode.cs
public abstract class AstNode
{
    public string Id { get; init; }              // "{NodeType}:{StartLine}:{StartColumn}" 形式（AstBuilder が設定）
    public string NodeType { get; init; }
    public NodeCategory Category { get; init; }
    public SourceLocation Location { get; init; }
    public List<AstNode> Children { get; init; } = new();
}

public record SourceLocation(int StartLine, int StartColumn, int StopLine, int StopColumn);
```

### 6.4 ノード種別

| クラス | Category | 対応するCOBOL構造 | 備考 |
|--------|---------|-----------------|------|
| `ProgramNode` | Structure | プログラム全体 | ルートノード |
| `DivisionNode` | Structure | IDENTIFICATION / ENVIRONMENT / DATA / PROCEDURE DIVISION | `Name`プロパティにDIVISION名 |
| `SectionNode` | Unit | WORKING-STORAGE SECTION, FILE SECTION 等 | DATA DIVISION内 |
| `ParagraphNode` | Unit | PROCEDUREのパラグラフ・セクション | `Name`プロパティにパラグラフ名 |
| `StatementNode` | Element | 各ステートメント（MOVE, PERFORM, IF等） | `StatementType`プロパティにキーワード |

### 6.5 Phase 2向け保存要件（情報損失禁止構文）

以下のCOBOL固有構文は、Phase 2（CFG/DFG構築）・Phase 6（CALL依存解析）・将来のIR変換拡張で必須となる。
AstBuilder でこれらの情報を落としてはならない。

#### CFG構築（Phase 2）に必要な構文

研究資料 §3.2「移行指向CFG設計」より：

| 構文 | StatementType 値 | 備考 |
|------|-----------------|------|
| `GO TO paragraph-name` | `"GOTO"` | 非構造化制御フロー。制御エッジ解析の起点 |
| `ALTER paragraph TO PROCEED TO paragraph` | `"ALTER"` | 動的GOTO変更。高リスクパターン |
| `PERFORM paragraph` | `"PERFORM"` | OOL単体実行。`StatementNode.PerformFrom` に呼び出し先パラグラフ名を保持し、`PerformThru = null` とする |
| `PERFORM paragraph THRU paragraph` | `"PERFORM_THRU"` | 範囲実行。境界情報（From/Thru）を保持 |
| `PERFORM UNTIL / VARYING` | `"PERFORM_LOOP"` | ループ構造。条件式テキストを保持 |
| `CALL "program-name"` | `"CALL"` | 静的CALL。`StatementNode.CallTarget` に大文字正規化済みのプログラム名を格納する。Phase 6 依存グラフ構築で必要 |
| `CALL identifier` | `"CALL"` | 動的CALL。`StatementNode.CallTarget = null` とし、静的CALLと区別する |

PERFORM / PERFORM THRU の `From` / `Thru` パラグラフ名は `StatementNode` の追加プロパティとして保持すること。

#### DFG構築（Phase 2）に必要な構文

研究資料 §3.3「移行焦点DFGモデル」より：

| 構文 | 対応ノード | 備考 |
|------|-----------|------|
| `01 GROUP-NAME.` / `05 FIELD PIC ...` | `DataItemNode`（Element） | レベル番号・親子関係を保持 |
| `05 FIELD-B REDEFINES FIELD-A PIC ...` | `DataItemNode` | `RedefinesTarget` プロパティに対象フィールド名 |
| `READ file INTO var` / `WRITE rec FROM var` | `StatementNode` | I/O文。`IoVerb` と `FileName` を保持 |

DataItemNode は DATA DIVISION 専用ノードとして追加する：

```csharp
// DataItemNode.cs（Element カテゴリ）
public class DataItemNode : AstNode
{
    public int LevelNumber { get; init; }           // 01, 05, 77 等
    public string Name { get; init; }
    public string? Picture { get; init; }           // PIC句
    public string? RedefinesTarget { get; init; }   // REDEFINES対象名（null = 非REDEFINES）
    public bool IsGroup => Picture == null && Children.OfType<DataItemNode>().Any();
}
```

#### 作用分類（将来のIR変換拡張）に必要な分類

研究資料 §3.1「IR単位定義」より、StatementNode は以下のいずれかに分類できる情報を持つ：

| IR作用分類 | 代表的なCOBOL文 |
|-----------|----------------|
| 制御作用（Control） | IF / EVALUATE / PERFORM / GO TO |
| データ作用（Data） | MOVE / COMPUTE / ADD / SUBTRACT |
| 境界作用（Boundary） | CALL / EXIT PROGRAM / STOP RUN |
| 副作用（SideEffect） | DISPLAY / READ / WRITE / OPEN / CLOSE |

Phase 1では分類の算出は不要だが、StatementType 値をこれらに対応付けられる粒度で記録すること。

---

## 7. CobolParserFacade

```csharp
// CobolParserFacade.cs
public class CobolParserFacade
{
    // COBOLソーステキストを受け取り、ParseResultを返す
    public ParseResult Parse(string source);
}
```

### ParseResult（Core層で定義）

```csharp
// Models/ParseResult.cs
public class ParseResult
{
    public ProgramNode? Ast { get; init; }
    public List<ParseError> Errors { get; init; } = new();
    public bool IsSuccess => Errors.Count == 0;
}

public record ParseError(int Line, int Column, string Message);
```

---

## 8. REST API

### エンドポイント

```
POST /api/parse
Content-Type: application/json
```

### JSON シリアライズ設定

API レスポンスは TypeScript 型定義と一致させるため、`System.Text.Json` の出力を camelCase にし、
すべての enum を文字列でシリアライズする。

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.MaxDepth = 128;
});
```

MVC Controller を使う場合は `AddJsonOptions` で同等の設定を行う。
この方針は Phase 2 以降で追加される `CfgEdgeKind` / `DfgEdgeKind` / `MdiRisk` / `MigrationStrategy` にも適用する。

### リクエストBody

```json
{
  "source": "<COBOLソースコード文字列>"
}
```

### レスポンス（成功時）

```json
{
  "ast": {
    "id": "Program:1:0",
    "nodeType": "Program",
    "category": "Structure",
    "location": { "startLine": 1, "startColumn": 0, "stopLine": 100, "stopColumn": 0 },
    "children": [...]
  },
  "errors": []
}
```

### レスポンス（構文エラーあり）

```json
{
  "ast": null,
  "errors": [
    { "line": 5, "column": 10, "message": "missing '.' at 'MOVE'" }
  ]
}
```

### HTTPステータス

| 状況 | ステータス |
|------|-----------|
| 正常パース | 200 OK |
| 構文エラーあり | 200 OK（errorsに詳細） |
| sourceが空/null | 400 Bad Request |
| サーバー内部エラー | 500 Internal Server Error |

---

## 9. テスト要件

### ParserTests.cs

| テスト名 | 検証内容 |
|----------|---------|
| `Parse_HelloWorld_ReturnsAstWithDivisions` | 最小COBOLファイルが正常にパースされ、4つのDIVISIONノードを含むASTが返る |
| `Parse_EmptySource_ReturnsError` | 空文字列を渡すとエラーが返る（例外ではなくParseError） |
| `Parse_SyntaxError_ReturnsErrors` | 構文エラーのあるCOBOLを渡すとerrorsリストに1件以上入る |
| `Parse_SourceLocation_IsCorrect` | ASTノードのStartLine/StopLineが正しい行番号を持つ |

### AstBuilderTests.cs

| テスト名 | 検証内容 |
|----------|---------|
| `Build_ProcedureDivision_ContainsParagraphs` | PROCEDURE DIVISIONのパラグラフが正しくParagraphNodeに変換される |
| `Build_WorkingStorage_ContainsSection` | WORKING-STORAGE SECTIONが正しくSectionNodeに変換される |
| `Build_GoTo_StatementTypeIsGoto` | GO TO文のStatementTypeが `"GOTO"` であること |
| `Build_Perform_StatementTypeIsPerform` | `PERFORM paragraph` のStatementTypeが `"PERFORM"` であり、PerformFrom が保持されること |
| `Build_PerformThru_PreservesFromAndThru` | PERFORM ... THRU ... のFrom/Thruパラグラフ名が保持されること |
| `Build_DataItem_PreservesLevelAndPicture` | レベル番号・名前・PIC句が正しくDataItemNodeに格納される |
| `Build_Redefines_PreservesTargetName` | REDEFINES句の対象フィールド名がRedefinesTargetに格納される |
| `Build_GroupItem_IsGroupTrue` | PIC句なしの集団項目でIsGroup=trueになること |
| `Build_NodeCategory_MatchesExpected` | Program=Structure、Paragraph=Unit、Statement=Element の分類が正しいこと |

### TestData ファイル一覧

| ファイル | 内容 |
|--------|------|
| `hello.cbl` | 最小限の正常COBOLサンプル（4 DIVISION、1パラグラフ） |
| `syntax-error.cbl` | 構文エラーを含むサンプル |
| `goto-sample.cbl` | GO TO / ALTER / PERFORM THRU を含むサンプル（CFG保存要件テスト用） |
| `data-sample.cbl` | REDEFINES・グループ項目・ファイルI/Oを含むサンプル（DFG保存要件テスト用） |

### TestData/hello.cbl（最小サンプル）

```cobol
       IDENTIFICATION DIVISION.
       PROGRAM-ID. HELLO.
       ENVIRONMENT DIVISION.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-MESSAGE PIC X(20) VALUE 'HELLO WORLD'.
       PROCEDURE DIVISION.
       MAIN-PARA.
           DISPLAY WS-MESSAGE.
           STOP RUN.
```

### TestData/goto-sample.cbl（CFG保存要件確認用）

```cobol
       IDENTIFICATION DIVISION.
       PROGRAM-ID. GOTO-SAMPLE.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-FLAG PIC 9 VALUE 0.
       PROCEDURE DIVISION.
       MAIN-PARA.
           IF WS-FLAG = 1
               GO TO PROCESS-PARA
           END-IF.
           GO TO END-PARA.
       PROCESS-PARA.
           PERFORM CALC-PARA THRU CALC-END-PARA.
           GO TO END-PARA.
       CALC-PARA.
           MOVE 1 TO WS-FLAG.
       CALC-END-PARA.
           MOVE 0 TO WS-FLAG.
       END-PARA.
           STOP RUN.
```

### TestData/data-sample.cbl（DFG保存要件確認用）

```cobol
       IDENTIFICATION DIVISION.
       PROGRAM-ID. DATA-SAMPLE.
       ENVIRONMENT DIVISION.
       INPUT-OUTPUT SECTION.
       FILE-CONTROL.
           SELECT INFILE ASSIGN TO 'INPUT.DAT'.
       DATA DIVISION.
       FILE SECTION.
       FD  INFILE.
       01  IN-RECORD.
           05 IN-KEY   PIC X(10).
           05 IN-DATA  PIC X(80).
       WORKING-STORAGE SECTION.
       01 WS-BUFFER.
           05 WS-NUMERIC PIC 9(10).
           05 WS-CHAR    REDEFINES WS-NUMERIC PIC X(10).
       PROCEDURE DIVISION.
       MAIN-PARA.
           OPEN INPUT INFILE.
           READ INFILE INTO WS-BUFFER.
           DISPLAY WS-CHAR.
           CLOSE INFILE.
           STOP RUN.
```

---

## 10. 完了基準

以下をすべて満たした時点でPhase 1完了とする。

- [ ] `dotnet build` がエラーなし
- [ ] `dotnet test` が全テストPASS
- [ ] `dotnet run --project src/backend/CobolAnalyzer.API` でAPIが起動する
- [ ] `POST /api/parse` に hello.cbl の内容を送信して `isSuccess: true` のレスポンスが返る
- [ ] `POST /api/parse` に syntax-error.cbl の内容を送信して `errors` に1件以上入る
- [ ] Swagger UI（`/swagger`）でエンドポイントが確認できる

---

## 11. 実装上の注意事項

1. **Generated/ のgit管理**: コード生成にJavaが必要なため、生成済みC#コードをリポジトリに含める。再生成が必要な場合は `Grammar/` に記録したコミットハッシュのg4ファイルを使うこと。

2. **ANTLR4エラーリスナー**: デフォルトのConsoleErrorListenerを無効化し、カスタムエラーリスナーでParseErrorを収集する実装にすること。例外をthrowする実装は避ける。

3. **AstNode JSONシリアライズ**: `Children`の再帰構造を持つため、System.Text.JsonのReferenceHandling設定に注意すること。循環参照は構造上発生しないが、深いネストに対応できるよう`MaxDepth`を適切に設定する（デフォルト64は不足する可能性がある）。

4. **大文字正規化**: COBOLはcase-insensitiveだが、grammars-v4のCobol.g4は大文字入力を前提とする。パース前に`source.ToUpper()`は行わず、g4のcase-insensitive設定（`options { caseInsensitive=true; }`）を確認して対応すること。

---

## 12. 参照資料

| 資料 | 参照箇所 | 本仕様との対応 |
|------|---------|--------------|
| `design/docs/CobolStructureAnalysis.md` §2.2 | 形式定義 T_AST = (V, E, λ) | §6.1 形式モデル |
| `design/docs/CobolStructureAnalysis.md` §4.1 | AST理論 Phase 1-7「3レベルノード分類」 | §6.2 NodeCategory |
| `design/docs/CobolStructureAnalysis.md` §3.1 | IR単位：制御/データ/境界/副作用 | §6.5 IR作用分類表 |
| `design/docs/CobolStructureAnalysis.md` §3.2 | CFG：GO TO / ALTER / PERFORM THRU | §6.5 CFG構築保存要件 |
| `design/docs/CobolStructureAnalysis.md` §3.3 | DFG：REDEFINES / グループ項目 / ファイルI/O | §6.5 DFG構築保存要件 |
| `design/brainstorm/phase1-planning.md` | 設計判断メモ | 本仕様全体 |
