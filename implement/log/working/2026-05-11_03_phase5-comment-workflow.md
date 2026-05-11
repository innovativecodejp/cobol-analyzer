# 2026-05-11_03 Phase 5 コメント挿入・削除実装記録

## 作業概要

`../design/specs/phase5-comment.md` を根拠として、
COBOL 固定形式コメントへのタグ付きコメント挿入・削除機能を実装した。

Backend ではコメントモデル、挿入・削除ロジック、REST API を追加し、
Frontend ではコメントタブ、挿入 UI、削除 Preview / Remove UI、API 呼び出し層を追加した。

関連コミット:

| コミット | 内容 |
|---------|------|
| `84b015d` | `docs: add phase5 implementation prompt` |
| `00fb379` | `feat: add phase5 comment workflow` |

---

## 参照した仕様・プロンプト

| 種別 | ファイル |
|------|----------|
| 仕様 | `../design/specs/phase5-comment.md` |
| 実装プロンプト | `prompts/2026-05-11_01_phase5-comment-impl.md` |
| 前提仕様 | `../design/specs/phase4-navigation.md` |

`implement/` のルールに従い、実装判断の根拠は `design/specs/` と実装プロンプトに限定した。
`roadmaps` / `brainstorm` は参照していない。

Phase 5 仕様内の SelectionStore 参照については、Phase 4 実装済み状態に合わせて
`selectedAstLineRange?.start` を使用した。

---

## 実施内容

### 1. Backend モデル追加

`CobolAnalyzer.Core/Models/` に Phase 5 用の入出力モデルを追加した。

| ファイル | 内容 |
|---------|------|
| `CommentInsertRequest.cs` | `source` と `InsertionSpec` リスト |
| `CommentInsertResult.cs` | 加工後ソース、挿入件数、警告 |
| `CommentRemoveRequest.cs` | `source` と削除用正規表現 |
| `CommentRemoveResult.cs` | 加工後ソース、削除件数、削除行、正規表現エラー |

### 2. Comment Engine 追加

`CobolAnalyzer.Engine/Comment/` を追加し、以下を実装した。

| ファイル | 内容 |
|---------|------|
| `CommentTag.cs` | `      * [TAG:VALUE] message` 形式の生成・解析 |
| `CommentInserter.cs` | 指定行直前へのコメント挿入、72列超過警告 |
| `CommentRemover.cs` | 固定形式コメント行のみを対象にした Preview / Remove |

`CommentRemover` は不正な正規表現と `RegexMatchTimeoutException` を呼び出し元へ伝播させず、
`patternError` に格納する実装とした。

### 3. Comment API 追加

`CobolAnalyzer.API/Controllers/CommentController.cs` を追加し、以下のエンドポイントを実装した。

| エンドポイント | 内容 |
|---------------|------|
| `POST /api/comment/insert` | タグ付きコメント挿入 |
| `POST /api/comment/preview` | 削除対象コメント行のプレビュー |
| `POST /api/comment/remove` | 削除対象コメント行の削除 |

`Program.cs` には `CommentInserter` / `CommentRemover` の DI 登録を追加した。

入力検証では以下を 400 とした。

- `source` が null / empty / whitespace
- `insertions` が null / empty
- `targetLine <= 0`
- `TAG` / `VALUE` 形式不正
- `pattern` が null / empty / whitespace

正規表現エラーは 500 にせず、200 OK で `patternError` を返す。

### 4. Frontend コメントタブ追加

`src/frontend` に以下を追加・修正した。

| ファイル | 内容 |
|---------|------|
| `src/types/commentTypes.ts` | Phase 5 API 型定義 |
| `src/api/commentApi.ts` | insert / preview / remove API 呼び出し |
| `src/components/CommentPanel.ts` | コメント挿入・削除 UI |
| `index.html` | `コメント` タブとパネルを追加 |
| `src/main.ts` | `CommentPanel` の生成とタブ切替時 render |
| `src/styles/main.css` | コメントパネル用スタイル |

コメント挿入・削除後は Monaco Editor のソースを更新するが、
`POST /api/analyze` は自動実行しない。
再分析はユーザーが Analyze ボタンを押したときだけ実行する。

---

## テスト追加

### Backend

`tests/CobolAnalyzer.Engine.Tests/CommentTests.cs` を追加した。

検証項目:

- `CommentTag` の生成・解析
- コメント行でない行が parse されないこと
- 単一・複数コメント挿入
- `targetLine` が行数超過した場合の末尾追加
- 72列超過時の Warning
- コメント行のみ削除対象になること
- Preview が source を変更しないこと
- 不正な正規表現と timeout が例外伝播しないこと

### Frontend

`src/frontend/src/api/commentApi.test.ts` を追加した。

検証項目:

- `insertComments` が `/api/comment/insert` を呼ぶ
- `previewRemove` が `/api/comment/preview` を呼ぶ
- `removeComments` が `/api/comment/remove` を呼ぶ

---

## 検証結果

### Backend

```text
dotnet test src/backend/CobolAnalyzer.sln
Parser.Tests: 12 passed
Engine.Tests: 39 passed
```

```text
dotnet build src/backend/CobolAnalyzer.sln
Build succeeded
Warnings: 0
Errors: 0
```

### API 疎通

確認時点で `http://localhost:5000/api/comment/*` が期待どおり応答した。

| 確認項目 | 結果 |
|---------|------|
| Insert | `insertedCount = 1`、`[MDI:HIGH]` コメントを含む source を返す |
| Preview | `removedCount = 1`、source は変更なし |
| Remove | `removedCount = 1`、対象コメントが source から消える |
| Invalid Regex | `patternError` が入り、500 にならない |

### Frontend

```text
npm test
Test Files: 9 passed (9)
Tests: 30 passed (30)
```

```text
npm run build
tsc && vite build
PASS
```

Monaco Editor 由来の大きな chunk 警告は出るが、既知の警告でありビルド失敗ではない。

### 差分チェック

```text
git diff --check
PASS
```

### Dev Server

確認時点で以下が応答した。

| URL | 結果 |
|-----|------|
| `http://127.0.0.1:5173` | 200 |
| `http://localhost:5000/api/comment/insert` | 200 |

---

## 完了判断

Phase 5 prompt の実装・自動テスト・API 疎通確認は完了。

仕様の矛盾や未定義事項として `implement/docs/` に新規フィードバックを記録すべき事項は発見していない。

---

## 残留リスク

- フロントエンドの CommentPanel は API 層テストとビルドで確認済みだが、ブラウザ上での手動 UI 操作確認は限定的。
- コメント削除は固定形式 COBOL の 7列目 `*` のみを対象とする。自由形式コメント `*>` は Phase 5 仕様どおりスコープ外。
- コメント挿入後の再分析は自動実行しないため、ユーザーが Analyze ボタンで明示的に再分析する必要がある。
