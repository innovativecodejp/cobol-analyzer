# Phase 5 仕様：コメント挿入・削除

バージョン: 1.2  
作成日: 2026-05-05  
更新日: 2026-05-08（整合性レビューによる修正: 完了基準#9 再分析記述修正・SelectionStore参照修正・targetLine 範囲外扱い統一）  
ステータス: 確定（implement/ への引き渡し可）

前提: `design/specs/phase3-visualization.md` の実装が完了し、Monaco Editor が稼働していること。  
（Phase 4 完了は必須でないが、SelectionStore を利用するため Phase 4 完了後が望ましい）

---

## 1. 目的・スコープ

COBOLソースコードに移行作業用のタグ付きコメントを挿入・一括削除する機能を提供する。
分析結果（MDIスコア・リスクパターン）を直接ソースに注釈として埋め込み、
移行設計書の一次情報として活用する。

### スコープ内
- タグ形式の定義（`* [TAG:VALUE] message`）
- コメント挿入 API（`POST /api/comment/insert`）
- コメント削除プレビュー API（`POST /api/comment/preview`）
- コメント削除 API（`POST /api/comment/remove`）
- `CobolAnalyzer.Engine/Comment/` の追加
- フロントエンド：コメントパネル（挿入・削除 UI）
- Phase 4 の SelectionStore との連携（選択ノードの行番号を自動設定）

### スコープ外（Phase 6以降）
- ファイルシステムへの直接書き込み
- コメント付きソースのエクスポート（Markdown / ダウンロード）
- COBOL 自由形式（Free Format）のコメント対応（`*>` 形式）

---

## 2. COBOL コメント形式

COBOL固定形式（Fixed Format）のコメント行仕様：

```
列番号: 1234567890123456789...
        ^^^^^^ * テキスト...
        |      |
        |      7列目: * でコメント行
        1〜6列: シーケンス番号（本仕様では半角スペース6文字）
```

タグ付きコメントの形式：

```
      * [TAG:VALUE] メッセージテキスト
```

### タグ形式の制約

| 要素 | 制約 |
|------|------|
| `TAG` | 英大文字・数字・ハイフンのみ（例: `MDI`, `REVIEW`, `MY-TAG`） |
| `VALUE` | 英数字・ハイフン・アンダースコア・ドット（`:`は不可） |
| `メッセージ` | 任意テキスト（UTF-8）。8〜72列に収まらない場合は警告を返す |
| コメント行全体 | 72列以内を推奨（超過時は警告、切り捨ては行わない） |

### 定義済みタグ

| タグ | VALUE 例 | 用途 |
|------|---------|------|
| `MDI` | `LOW` / `MEDIUM` / `HIGH` / `CRITICAL` | MDI リスクランクの注釈 |
| `REVIEW` | `2026-05-05` | 確認要求日の記録 |
| `TODO` | `REFACTOR` / `SPLIT` / `REMOVE` | 移行時のアクション |
| `NOTE` | 任意 | 自由メモ |

カスタムタグも使用可能（TAG 制約に従えば任意の文字列）。

---

## 3. プロジェクト構造追加分

```
implement/
├── src/
│   └── backend/
│       ├── CobolAnalyzer.Engine/
│       │   └── Comment/                      ← 新規
│       │       ├── CommentInserter.cs
│       │       ├── CommentRemover.cs
│       │       └── CommentTag.cs
│       ├── CobolAnalyzer.Core/
│       │   └── Models/
│       │       ├── CommentInsertRequest.cs   ← 追加
│       │       ├── CommentInsertResult.cs    ← 追加
│       │       ├── CommentRemoveRequest.cs   ← 追加
│       │       └── CommentRemoveResult.cs    ← 追加
│       └── CobolAnalyzer.API/
│           └── Controllers/
│               └── CommentController.cs      ← 追加
└── tests/
    └── CobolAnalyzer.Engine.Tests/
        └── CommentTests.cs                   ← 追加（既存テストプロジェクトに追加）

src/frontend/src/
└── components/
    └── CommentPanel.ts                       ← 追加
```

---

## 4. タグモデル（CommentTag）

```csharp
// CommentTag.cs
public record CommentTag(string Tag, string Value, string Message)
{
    // タグ形式文字列を生成: "* [TAG:VALUE] message"
    public string ToCobolCommentLine()
        => $"      * [{Tag}:{Value}] {Message}";

    // 文字列からパース（マッチしない場合は null）
    public static CommentTag? TryParse(string line);

    // 正規表現: ^\s{6}\*\s\[([A-Z0-9\-]+):([^\]]+)\]\s?(.*)$
    private static readonly Regex Pattern = new(
        @"^\s{6}\*\s\[([A-Z0-9\-]+):([^\:\]]+)\]\s?(.*)$",
        RegexOptions.Compiled);
}
```

---

## 5. CommentInserter

```csharp
// CommentInserter.cs
public class CommentInserter
{
    // 挿入指示リストに従いソーステキストを変換して返す
    public CommentInsertResult Insert(string source, IReadOnlyList<InsertionSpec> insertions);
}

public record InsertionSpec(
    int TargetLine,          // 挿入先行番号（1始まり）。この行の直前に挿入する
    string Tag,
    string Value,
    string Message
);
```

### 挿入アルゴリズム

1. `source` を行分割（`\n` / `\r\n` 対応）してリストを作成する
2. `insertions` を `TargetLine` の**降順**でソートする（上の挿入が行番号をずらさないため）
3. 各 `InsertionSpec` について：
   a. `TargetLine - 1` のインデックスの直前に `ToCobolCommentLine()` を挿入する
   b. `TargetLine` が行数より大きい場合は末尾に追加する
4. 行リストを結合してソーステキストを再構築して返す

### 挿入結果（CommentInsertResult）

```csharp
public class CommentInsertResult
{
    public string Source { get; init; }           // 加工後のソーステキスト
    public int InsertedCount { get; init; }
    public List<CommentWarning> Warnings { get; init; } = new();
}

public record CommentWarning(int Line, string Message);
// 警告例: "コメント行が72列を超えています（{n}列）"
```

---

## 6. CommentRemover

```csharp
// CommentRemover.cs
public class CommentRemover
{
    // パターンにマッチするコメント行を削除して返す
    public CommentRemoveResult Remove(string source, string pattern);

    // 削除対象行をプレビュー（実際には削除しない）
    public CommentRemoveResult Preview(string source, string pattern);
}
```

### 削除条件

1. 7列目が `*` のコメント行であること（非コメント行は絶対に削除しない）
2. コメントテキスト部分（8列目以降）が `pattern` の正規表現にマッチすること

### 削除アルゴリズム

1. `source` を行分割する
2. 各行について：
   a. コメント行判定: `line.Length >= 7 && line[6] == '*'`
   b. マッチ判定: `Regex.IsMatch(line[7..], pattern, options, timeout: 1秒)`
   c. a かつ b の行を削除対象にする
3. `Preview` は削除対象行を `RemovedLines` に格納して `Source` は変更しない
4. `Remove` は削除対象行を除いたソーステキストを返す

### 削除結果（CommentRemoveResult）

```csharp
public class CommentRemoveResult
{
    public string Source { get; init; }            // 加工後のソーステキスト（Preview時は元のまま）
    public int RemovedCount { get; init; }
    public List<RemovedLine> RemovedLines { get; init; } = new();
    public string? PatternError { get; init; }     // 正規表現エラーメッセージ（null = 正常）
}

public record RemovedLine(int LineNumber, string Content);
```

---

## 7. REST API

### 7.1 コメント挿入

```
POST /api/comment/insert
Content-Type: application/json
```

**リクエスト Body**

```json
{
  "source": "<COBOLソースコード>",
  "insertions": [
    { "targetLine": 10, "tag": "MDI", "value": "HIGH", "message": "GO TO が多用されています" },
    { "targetLine": 25, "tag": "REVIEW", "value": "2026-05-05", "message": "境界確認が必要" }
  ]
}
```

**レスポンス（成功時）**

```json
{
  "source": "<コメント挿入後のCOBOLソースコード>",
  "insertedCount": 2,
  "warnings": [
    { "line": 10, "message": "コメント行が72列を超えています（78列）" }
  ]
}
```

**HTTPステータス**

| 状況 | ステータス |
|------|-----------|
| 正常 | 200 OK |
| source / insertions が空 | 400 Bad Request |
| targetLine が範囲外（0以下） | 400 Bad Request |
| targetLine が行数超過 | 200 OK（末尾に追加） |
| TAG / VALUE 形式不正 | 400 Bad Request |

### 7.2 コメント削除プレビュー

```
POST /api/comment/preview
Content-Type: application/json
```

**リクエスト Body**

```json
{
  "source": "<COBOLソースコード>",
  "pattern": "\\[MDI:.*?\\]"
}
```

**レスポンス**

```json
{
  "source": "<元のソースコード（変更なし）>",
  "removedCount": 3,
  "removedLines": [
    { "lineNumber": 10, "content": "      * [MDI:HIGH] GO TO が多用されています" },
    { "lineNumber": 25, "content": "      * [MDI:MEDIUM] ネスト深度が高い" }
  ],
  "patternError": null
}
```

### 7.3 コメント削除実行

```
POST /api/comment/remove
Content-Type: application/json
```

**リクエスト Body**（同上）

**レスポンス**

```json
{
  "source": "<コメント削除後のCOBOLソースコード>",
  "removedCount": 3,
  "removedLines": [...],
  "patternError": null
}
```

**正規表現エラー時**

```json
{
  "source": "<元のソースコード>",
  "removedCount": 0,
  "removedLines": [],
  "patternError": "無効な正規表現: ..."
}
```

**HTTPステータス**（insert と同様）

| 状況 | ステータス |
|------|-----------|
| 正常（patternError = null） | 200 OK |
| source / pattern が空 | 400 Bad Request |
| 正規表現エラー（patternError あり） | 200 OK（エラー内容をレスポンスに含める） |

---

## 8. フロントエンド：CommentPanel

### 8.1 UI 構成

Phase 3 の画面に「コメント」タブを追加する（ダイアグラムタブと並列）。

```
[ AST ] [ CFG ] [ DFG ] [ コメント ]
                         ↑ Phase 5 で追加
```

コメントタブは「挿入」「削除」の2つのサブパネルを持つ。

#### 挿入サブパネル

```
挿入先行番号: [___10___]  ← Phase 4 SelectionStore から自動設定
タグ種別:     [MDI ▼]     ← MDI / REVIEW / TODO / NOTE / カスタム
値:           [HIGH    ]
メッセージ:   [__________________]
              [ Insert ]
```

- 「Phase 4 SelectionStore から自動設定」: 選択ノードがある場合は `selectionStore.getState().highlightedLines?.start` を初期値にセット
- SelectionStore が未使用環境（Phase 4 未実装）でも手動入力で動作すること

#### 削除サブパネル

```
正規表現パターン: [\[MDI:.*?\]        ]
                  [ Preview ] [ Remove ]

プレビュー結果:
  行 10: "      * [MDI:HIGH] GO TO が多用..."
  行 25: "      * [MDI:MEDIUM] ネスト深度..."
  合計 2 件が削除されます
```

### 8.2 CommentPanel.ts の型定義

```typescript
// types/commentTypes.ts（追加）

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

### 8.3 API 呼び出し（commentApi.ts 追加）

```typescript
// api/commentApi.ts

export async function insertComments(req: CommentInsertRequest): Promise<CommentInsertResult>;
export async function previewRemove(req: CommentRemoveRequest): Promise<CommentRemoveResult>;
export async function removeComments(req: CommentRemoveRequest): Promise<CommentRemoveResult>;
```

挿入・削除後は Monaco Editor のソース（`editor.setValue(result.source)`）を更新する。
更新後は分析結果を要更新状態として表示し、`POST /api/analyze` の再実行はユーザーが Analyze ボタンを押したときだけ行う。

---

## 9. テスト要件（xUnit / Vitest）

### CommentTests.cs（xUnit）

#### CommentTag 系

| テスト名 | 検証内容 |
|----------|---------|
| `CommentTag_ToCobolCommentLine_CorrectFormat` | `[MDI:HIGH]` タグが `      * [MDI:HIGH] message` になる |
| `CommentTag_TryParse_ValidLine_ReturnsTag` | 正しい形式の行から CommentTag がパースできる |
| `CommentTag_TryParse_NonCommentLine_ReturnsNull` | コメント行でない行は null を返す |
| `CommentTag_TryParse_NoTagFormat_ReturnsNull` | タグ形式でないコメント行は null を返す |

#### CommentInserter 系

| テスト名 | 検証内容 |
|----------|---------|
| `Insert_SingleInsertion_LineInsertedBefore` | targetLine=5 の挿入でコメントが5行目の直前に入る |
| `Insert_MultipleInsertions_DescendingOrder` | 複数挿入が降順処理され行番号がずれない |
| `Insert_TargetLineExceedsLength_AppendsToEnd` | targetLine が行数超過の場合、末尾に追加される |
| `Insert_LongMessage_WarningReturned` | 72列超のコメント行で Warning が返る |

#### CommentRemover 系

| テスト名 | 検証内容 |
|----------|---------|
| `Remove_PatternMatchesCommentLine_Removed` | パターンにマッチするコメント行が削除される |
| `Remove_PatternMatchesCodeLine_NotRemoved` | 非コメント行はパターンマッチしても削除されない |
| `Preview_DoesNotModifySource` | Preview では Source が変更されない |
| `Remove_InvalidPattern_PatternErrorSet` | 不正な正規表現で PatternError に内容が入る |
| `Remove_PatternTimeout_HandledGracefully` | 正規表現マッチが1秒でタイムアウトしても例外が伝播しない |

### adapters/commentApi.test.ts（Vitest）

| テスト名 | 検証内容 |
|----------|---------|
| `insertComments_callsCorrectEndpoint` | `POST /api/comment/insert` が呼ばれる |
| `previewRemove_callsPreviewEndpoint` | `POST /api/comment/preview` が呼ばれる |

---

## 10. 完了基準

以下をすべて満たした時点で Phase 5 完了とする。

- [ ] `dotnet test` が全テストPASS（CommentTests を含む）
- [ ] `POST /api/comment/insert` で hello.cbl に `[MDI:HIGH]` コメントが挿入された新ソースが返る
- [ ] `POST /api/comment/preview` で挿入済みソースの削除対象行一覧が返る
- [ ] `POST /api/comment/remove` で `[MDI:.*?]` パターンにマッチするコメント行が削除された新ソースが返る
- [ ] `POST /api/comment/remove` に不正な正規表現を渡すと `patternError` に内容が入る（500 にならない）
- [ ] フロントエンドの挿入パネルで行番号・タグ・値・メッセージを入力し Insert を押すと Monaco Editor のソースが更新される
- [ ] Phase 4 で AST ノードを選択した状態でコメントタブを開くと行番号が自動設定される
- [ ] 削除パネルで Preview を押すと削除対象行一覧が表示される
- [ ] 削除パネルで Remove を押すと Monaco Editor のソースが更新される（再分析は Analyze ボタンで手動実行）
- [ ] `npm test` が全テストPASS（commentApi を含む）

---

## 11. 実装上の注意事項

1. **挿入順序**: `CommentInserter` は `insertions` を `TargetLine` 降順でソートしてから処理する。昇順で処理すると上の行への挿入によって下の行番号がずれる。

2. **正規表現タイムアウト**: `CommentRemover` は `Regex.IsMatch` に `matchTimeout: TimeSpan.FromSeconds(1)` を指定する。`RegexMatchTimeoutException` をキャッチして `PatternError` に格納し、例外を呼び出し元に伝播させない。

3. **非コメント行の保護**: 削除条件を「コメント行（7列目 `*`）かつパターンマッチ」の AND にする。この判定を CommentRemover の先頭に置き、条件を満たさない行は絶対に変更しない。

4. **Monaco Editor の更新**: 挿入・削除後は `editor.setValue(result.source)` でソースを全置換する。この操作でカーソル位置がリセットされるため、操作前のカーソル行を保存して `editor.revealLineInCenter()` で元の位置に戻す。

5. **再分析のトリガー**: 挿入・削除後の `POST /api/analyze` 再実行はユーザーが明示的に Analyze ボタンを押すことで行う。自動再分析はしない（コメント行はパース対象外のため影響は少ないが、ユーザーの意図しないタイミングでの通信を避ける）。

---

## 12. 参照資料

| 資料 | 参照箇所 | 本仕様との対応 |
|------|---------|--------------|
| `design/specs/phase2-engine.md` §6.5 | MDI リスクランク（Low/Medium/High/Critical） | §2 定義済みタグ MDI の VALUE |
| `design/specs/phase3-visualization.md` §4 | 画面レイアウト（タブ構成） | §8.1 コメントタブ追加 |
| `design/specs/phase4-navigation.md` §4 | SelectionStore の `selectedAstNodeId` / `highlightedLines` | §8.1 行番号自動設定 |
| `design/brainstorm/phase5-planning.md` | 設計判断メモ | 本仕様全体 |
