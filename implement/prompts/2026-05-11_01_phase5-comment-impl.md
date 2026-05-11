# Phase 5 実装プロンプト：コメント挿入・削除

仕様: `../design/specs/phase5-comment.md`（実装前に必ず全文を読むこと）

---

## 現状確認（実装済み範囲）

Phase 1〜4 は実装・レビュー済み。

主な前提:

- Backend
  - `POST /api/analyze` 実装済み
  - `CobolAnalyzer.Engine` / `CobolAnalyzer.Core` / `CobolAnalyzer.API` 構成済み
  - `Program.cs` で DI 登録・Swagger・Development CORS 設定済み
- Frontend
  - Vite + TypeScript + Monaco Editor + D3.js 実装済み
  - `Editor` は `getValue()` / `setValue()` / `getEditor()` を持つ
  - `SelectionStore` 実装済み
    - `selectedAstLineRange`
    - `selectedAstNodeId`
    - `selectedCfgBlockId`
    - `selectedDfgNodeId`
    - `impactClosureIds`
  - `main.ts` は AST / CFG / DFG / MDI を描画し、Analyze は明示操作で実行される

直近の確認済み状態:

- `npm test`: 27 tests PASS
- `npm run build`: PASS
- `dotnet test src/backend/CobolAnalyzer.sln`: Parser 12 / Engine 26 PASS

---

## 実装前の注意事項

### 1. 参照元は specs のみ

`implement/` のルールに従い、実装判断の根拠は `design/specs/` のみとする。
`roadmaps` / `brainstorm` は参照しない。

仕様の矛盾・未定義事項を見つけた場合は、実装修正を進めず、
`implement/docs/` にフィードバックを記録してユーザーに確認する。

### 2. SelectionStore の行番号参照

Phase 5 仕様 §8.1 には `selectionStore.getState().highlightedLines?.start` という表記があるが、
Phase 4 仕様・実装の SelectionStore は `highlightedLines` ではなく `selectedAstLineRange` を持つ。

実装時は Phase 4 の確定仕様・実装に合わせて、コメント挿入先の自動設定には以下を使う。

```typescript
selectionStore.getState().selectedAstLineRange?.start
```

この扱いに疑義がある場合は、`implement/docs/` に仕様フィードバックを記録して実装を止める。

### 3. commentApi のテスト配置

Phase 5 仕様 §8.3 は `api/commentApi.ts` の追加を指定している。
一方、仕様 §9 の表には `adapters/commentApi.test.ts` とある。

責務上は API 呼び出し層なので、テストは `src/frontend/src/api/commentApi.test.ts` に置く。
この点を仕様差分として扱う必要があると判断した場合は、`implement/docs/` に記録して実装を止める。

### 4. コメント操作後に自動再分析しない

Insert / Remove 後は Monaco Editor のソースを `editor.setValue(result.source)` で更新する。
ただし `POST /api/analyze` は自動実行しない。
ユーザーが Analyze ボタンを押したときだけ再分析する。

---

## タスク一覧

以下を順に実施する。

---

### タスク 1: Core モデル追加

`src/backend/CobolAnalyzer.Core/Models/` に Phase 5 用モデルを追加する。

追加ファイル:

```text
CommentInsertRequest.cs
CommentInsertResult.cs
CommentRemoveRequest.cs
CommentRemoveResult.cs
```

最低限、以下を定義する。

```csharp
namespace CobolAnalyzer.Core.Models;

public class CommentInsertRequest
{
    public string? Source { get; init; }
    public List<InsertionSpec> Insertions { get; init; } = new();
}

public record InsertionSpec(
    int TargetLine,
    string Tag,
    string Value,
    string Message
);

public class CommentInsertResult
{
    public string Source { get; init; } = "";
    public int InsertedCount { get; init; }
    public List<CommentWarning> Warnings { get; init; } = new();
}

public record CommentWarning(int Line, string Message);

public class CommentRemoveRequest
{
    public string? Source { get; init; }
    public string? Pattern { get; init; }
}

public class CommentRemoveResult
{
    public string Source { get; init; } = "";
    public int RemovedCount { get; init; }
    public List<RemovedLine> RemovedLines { get; init; } = new();
    public string? PatternError { get; init; }
}

public record RemovedLine(int LineNumber, string Content);
```

`InsertionSpec` は API request と Engine の `CommentInserter` が共有して使う。

---

### タスク 2: CommentTag 実装

`src/backend/CobolAnalyzer.Engine/Comment/CommentTag.cs` を追加する。

要件:

- `public record CommentTag(string Tag, string Value, string Message)`
- `ToCobolCommentLine()` は固定形式コメントとして以下を返す

```text
      * [TAG:VALUE] message
```

- `TryParse(string line)` はタグ形式コメント行をパースし、合わなければ `null`
- 正規表現は仕様 §4 の制約に合わせる

```csharp
@"^\s{6}\*\s\[([A-Z0-9\-]+):([^\:\]]+)\]\s?(.*)$"
```

注意:

- API validation では TAG / VALUE 制約を別途厳密にチェックする
- `VALUE` に `:` は許可しない
- カスタムタグは TAG 制約に合えば許可する

---

### タスク 3: CommentInserter 実装

`src/backend/CobolAnalyzer.Engine/Comment/CommentInserter.cs` を追加する。

要件:

- `Insert(string source, IReadOnlyList<InsertionSpec> insertions)` を実装
- `source` は `\n` / `\r\n` の両方に対応して行分割する
- `insertions` は `TargetLine` 降順で処理する
- `TargetLine` は 1 始まり
- `TargetLine` の行の直前にコメントを挿入する
- `TargetLine > 行数` の場合は末尾に追加する
- `TargetLine <= 0` は Controller 側で 400 にする
- コメント行が 72 列を超える場合は Warning を返し、切り捨てはしない

結果:

- `Source`: 加工後ソース
- `InsertedCount`: 挿入件数
- `Warnings`: `CommentWarning(Line, Message)`

警告メッセージ例:

```text
コメント行が72列を超えています（78列）
```

---

### タスク 4: CommentRemover 実装

`src/backend/CobolAnalyzer.Engine/Comment/CommentRemover.cs` を追加する。

要件:

- `Remove(string source, string pattern)`
- `Preview(string source, string pattern)`
- 削除対象は以下の AND 条件
  - `line.Length >= 7 && line[6] == '*'`
  - `Regex.IsMatch(line[7..], pattern, options, timeout: 1秒)`
- 非コメント行は絶対に削除しない
- `Preview` は `Source` を変更しない
- `Remove` は削除対象行を除いた `Source` を返す
- `RemovedLines` には 1 始まりの行番号と元行内容を入れる
- 不正な正規表現は例外を伝播させず `PatternError` に入れる
- `RegexMatchTimeoutException` も例外を伝播させず `PatternError` に入れる

正規表現タイムアウト:

```csharp
Regex.IsMatch(input, pattern, RegexOptions.None, TimeSpan.FromSeconds(1))
```

---

### タスク 5: CommentController 追加

`src/backend/CobolAnalyzer.API/Controllers/CommentController.cs` を追加する。

ルート:

```text
POST /api/comment/insert
POST /api/comment/preview
POST /api/comment/remove
```

既存 `AnalyzeController` と同じ Controller スタイルに合わせる。

DI:

- `CommentInserter`
- `CommentRemover`

`Program.cs` に登録する。

```csharp
builder.Services.AddSingleton<CommentInserter>();
builder.Services.AddSingleton<CommentRemover>();
```

Validation:

#### Insert

- `source` が null / empty / whitespace → 400
- `insertions` が null / empty → 400
- `targetLine <= 0` → 400
- `targetLine > 行数` → 200 OK、末尾追加
- TAG 形式不正 → 400
  - `^[A-Z0-9-]+$`
- VALUE 形式不正 → 400
  - `^[A-Za-z0-9_.-]+$`
  - `:` は不可

#### Preview / Remove

- `source` が null / empty / whitespace → 400
- `pattern` が null / empty / whitespace → 400
- 正規表現エラーは 200 OK
  - `patternError` に内容を入れる
  - 500 にしない

---

### タスク 6: Backend xUnit テスト追加

`tests/CobolAnalyzer.Engine.Tests/CommentTests.cs` を追加する。

仕様 §9 のテストをすべて実装する。

CommentTag:

- `CommentTag_ToCobolCommentLine_CorrectFormat`
- `CommentTag_TryParse_ValidLine_ReturnsTag`
- `CommentTag_TryParse_NonCommentLine_ReturnsNull`
- `CommentTag_TryParse_NoTagFormat_ReturnsNull`

CommentInserter:

- `Insert_SingleInsertion_LineInsertedBefore`
- `Insert_MultipleInsertions_DescendingOrder`
- `Insert_TargetLineExceedsLength_AppendsToEnd`
- `Insert_LongMessage_WarningReturned`

CommentRemover:

- `Remove_PatternMatchesCommentLine_Removed`
- `Remove_PatternMatchesCodeLine_NotRemoved`
- `Preview_DoesNotModifySource`
- `Remove_InvalidPattern_PatternErrorSet`
- `Remove_PatternTimeout_HandledGracefully`

テストでは以下を重視する。

- 固定形式コメントの 7 列目 `*`
- 非コメント行の保護
- Preview が source を変更しないこと
- Regex 例外が呼び出し元へ伝播しないこと

---

### タスク 7: Frontend 型定義追加

`src/frontend/src/types/commentTypes.ts` を追加する。

仕様 §8.2 の型を実装する。

```typescript
export interface InsertionSpec {
  targetLine: number;
  tag: string;
  value: string;
  message: string;
}

export interface CommentInsertRequest {
  source: string;
  insertions: InsertionSpec[];
}

export interface CommentInsertResult {
  source: string;
  insertedCount: number;
  warnings: Array<{ line: number; message: string }>;
}

export interface CommentRemoveRequest {
  source: string;
  pattern: string;
}

export interface CommentRemoveResult {
  source: string;
  removedCount: number;
  removedLines: Array<{ lineNumber: number; content: string }>;
  patternError: string | null;
}
```

---

### タスク 8: commentApi 実装と Vitest

`src/frontend/src/api/commentApi.ts` を追加する。

既存 `analyzeApi.ts` と同じ `API_BASE` パターンを使う。

実装関数:

- `insertComments(req: CommentInsertRequest): Promise<CommentInsertResult>`
- `previewRemove(req: CommentRemoveRequest): Promise<CommentRemoveResult>`
- `removeComments(req: CommentRemoveRequest): Promise<CommentRemoveResult>`

HTTP エラー時は `throw new Error(\`API error: ${res.status}\`)` の形に合わせる。

テスト:

`src/frontend/src/api/commentApi.test.ts` を追加する。

最低限:

- `insertComments_callsCorrectEndpoint`
- `previewRemove_callsPreviewEndpoint`
- 可能なら `removeComments_callsRemoveEndpoint` も追加する

`globalThis.fetch` を mock し、URL / method / body を検証する。

---

### タスク 9: CommentPanel 実装

`src/frontend/src/components/CommentPanel.ts` を追加する。

責務:

- コメント挿入 UI
- コメント削除 Preview / Remove UI
- API 呼び出し
- Monaco Editor ソース更新
- Phase 4 SelectionStore から選択行の自動設定

推奨コンストラクタ:

```typescript
export class CommentPanel {
  constructor(
    private readonly container: HTMLElement,
    private readonly getSource: () => string,
    private readonly setSource: (source: string) => void,
  ) {}

  render(): void;
}
```

`setSource` は `editor.setValue(result.source)` を呼ぶ。

挿入パネル:

- targetLine input
- tag select
  - `MDI`
  - `REVIEW`
  - `TODO`
  - `NOTE`
  - `CUSTOM`
- custom tag input（CUSTOM の場合だけ使用）
- value input
- message input
- Insert button
- warnings / status 表示

行番号初期値:

```typescript
const selectedLine = selectionStore.getState().selectedAstLineRange?.start;
```

SelectionStore が未選択なら空欄または `1` を初期値にする。
コメントタブ表示時・render 時に現在の選択を反映する。

削除パネル:

- regex pattern input
- Preview button
- Remove button
- preview result list
- patternError 表示
- removedCount 表示

挿入・削除後:

- `setSource(result.source)`
- 「ソースを更新しました。再分析は Analyze を押してください。」相当の状態表示を出す
- 自動 Analyze は実行しない
- 操作前の Monaco カーソル行を保存できる場合は、更新後に `editor.getEditor().revealLineInCenter()` で戻す
  - `CommentPanel` が `IStandaloneCodeEditor` を直接受け取る設計にしてもよい

---

### タスク 10: index.html / main.ts / CSS 統合

#### index.html

タブに `コメント` を追加する。

```html
<button class="tab-btn" data-tab="comment">コメント</button>
```

パネルを追加する。

```html
<div id="tab-comment" class="tab-panel"></div>
```

#### main.ts

- `CommentPanel` を import
- アプリ起動時に一度だけ生成する
- `editor.getValue()` / `editor.setValue()` を渡す
- コメントタブクリック時にも、選択行が最新になるよう `commentPanel.render()` する
- Analyze の再実行はしない

注意:

- `renderResult()` で `tab-comment` の中身を消さない
- AST / CFG / DFG のエラー表示時もコメントタブは操作可能なままにする

#### main.css

CommentPanel 用の最小スタイルを追加する。

方針:

- 既存 UI と同じ静かな業務ツール風
- 入力・ボタン・結果一覧が読みやすいこと
- コメントタブはカード乱用せず、パネル内で section を分ける

---

### タスク 11: API 動作確認

バックエンドを起動する。

```powershell
cd src/backend
dotnet run --project CobolAnalyzer.API --launch-profile http
```

以下を確認する。

#### Insert

`hello.cbl` に `[MDI:HIGH]` コメントを挿入する。

期待:

- HTTP 200
- `insertedCount = 1`
- `source` に `      * [MDI:HIGH] ...` が含まれる

#### Preview

挿入済みソースに対して以下を実行。

```json
{ "pattern": "\\[MDI:.*?\\]" }
```

期待:

- HTTP 200
- `removedCount >= 1`
- `source` は変更されない
- `removedLines` に MDI コメントが含まれる

#### Remove

同じ pattern で削除する。

期待:

- HTTP 200
- `removedCount >= 1`
- `source` から MDI コメントが消える

#### Invalid Regex

不正 pattern を送る。

期待:

- HTTP 200
- `patternError` に内容が入る
- 500 にならない

---

### タスク 12: Frontend 動作確認

Vite Dev Server を起動する。

```powershell
cd src/frontend
npm run dev
```

確認項目:

- `http://localhost:5173` が開ける
- タブに `コメント` が表示される
- 挿入パネルで targetLine / tag / value / message を入力し Insert すると Monaco Editor のソースが更新される
- Phase 4 で AST ノード選択後、コメントタブを開くと targetLine が選択行で初期化される
- Preview で削除対象行が一覧表示される
- Remove で Monaco Editor のソースが更新される
- Insert / Remove 後に自動 Analyze されない
- Analyze ボタンを押すと更新後ソースで再分析される

---

### タスク 13: 最終検証

以下を実行し、全件 PASS を確認する。

```powershell
dotnet test src/backend/CobolAnalyzer.sln
```

```powershell
cd src/frontend
npm test
npm run build
```

期待:

- Backend: 既存 Parser / Engine tests + `CommentTests` が PASS
- Frontend: 既存 tests + `commentApi` tests が PASS
- `npm run build` はエラーなし
  - Monaco 由来の chunk size warning は許容

---

## 完了確認

仕様 §10 の完了基準をすべて確認する。

```text
- [ ] dotnet test が全テストPASS（CommentTests を含む）
- [ ] POST /api/comment/insert で hello.cbl に [MDI:HIGH] コメントが挿入された新ソースが返る
- [ ] POST /api/comment/preview で挿入済みソースの削除対象行一覧が返る
- [ ] POST /api/comment/remove で [MDI:.*?] パターンにマッチするコメント行が削除された新ソースが返る
- [ ] POST /api/comment/remove に不正な正規表現を渡すと patternError に内容が入る（500 にならない）
- [ ] フロントエンドの挿入パネルで行番号・タグ・値・メッセージを入力し Insert を押すと Monaco Editor のソースが更新される
- [ ] Phase 4 で AST ノードを選択した状態でコメントタブを開くと行番号が自動設定される
- [ ] 削除パネルで Preview を押すと削除対象行一覧が表示される
- [ ] 削除パネルで Remove を押すと Monaco Editor のソースが更新される（再分析は Analyze ボタンで手動実行）
- [ ] npm test が全テストPASS（commentApi を含む）
```

---

## 仕様との矛盾発見時の対処

実装中に仕様の矛盾・未定義事項を発見した場合:

1. `implement/docs/` にフィードバック内容を記録する
2. 実装を停止してユーザーに報告する
3. ユーザーの指示を待つ
4. `design/specs/` は implement 側で変更しない
