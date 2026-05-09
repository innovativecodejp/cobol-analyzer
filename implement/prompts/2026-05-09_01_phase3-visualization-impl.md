# Phase 3 実装プロンプト：ダイアグラム可視化

仕様: `../design/specs/phase3-visualization.md`（実装前に必ず全文を読むこと）

---

## 現状確認（実装済み範囲）

以下はすでに実装・コミット済みである（コミット `13993ea`）。

```
src/frontend/
├── package.json / vite.config.ts / tsconfig.json / index.html / .env.development
└── src/
    ├── vite-env.d.ts
    ├── main.ts
    ├── types/analyzeResult.ts
    ├── api/analyzeApi.ts
    ├── adapters/cfgAdapter.ts / dfgAdapter.ts / astAdapter.ts
    ├── adapters/*.test.ts（9テスト）
    ├── components/Editor.ts / AstTree.ts / CfgGraph.ts / DfgGraph.ts / MdiPanel.ts
    ├── components/MdiPanel.test.ts（2テスト）
    └── styles/main.css
```

`npm run build` および `npm test`（11件）はすでに PASS 済み。

**未完了**: 仕様 §11 完了基準のうち、ブラウザ動作確認（下記タスク参照）が未実施。

---

## 実装前の注意事項

### tsconfig の差分

`tsconfig.json` に `"skipLibCheck": true` を追加済み。
これは `node_modules/vite/dist/node/index.d.ts` および Monaco の型宣言との競合を回避するためであり、
Vite + Monaco 構成では標準的な対処。削除しないこと。

### Monaco Worker 設定

`main.ts` の先頭で `window.MonacoEnvironment` を設定し、
`monaco-editor/esm/vs/editor/editor.worker?worker` を返すようにしている。
`vite-env.d.ts` に `Window.MonacoEnvironment` の独自宣言はない（Monaco 側の型を使用）。

---

## タスク一覧

以下を順に実施する。

---

### タスク 1: バックエンド起動確認

```powershell
cd src/backend
dotnet run --project CobolAnalyzer.API
```

- `https://localhost:5000` または `http://localhost:5000` でリッスンしていることを確認する
- Swagger UI（`http://localhost:5000/swagger`）で `/api/analyze` エンドポイントが表示されることを確認する
- 起動しない場合は `dotnet build src/backend/CobolAnalyzer.sln` でビルドエラーを確認する

---

### タスク 2: フロントエンド Dev Server 起動確認

別ターミナルで：

```powershell
cd src/frontend
npm run dev
```

- `http://localhost:5173`（または Vite が表示するポート）にブラウザでアクセスできることを確認する
- Monaco Editor の入力エリアとタブ（AST / CFG / DFG）が表示されることを確認する
- MDI フッターバーが画面下部に固定表示されていることを確認する

---

### タスク 3: hello.cbl の動作確認

Monaco Editor に以下を貼り付けて「Analyze」ボタンを押す：

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

確認項目：

- [ ] AST タブに `d3.tree()` 水平ツリーが表示される
- [ ] ノードをクリックして折りたたみ・展開が動作する
- [ ] ダイアグラムエリアでマウスホイール（ズーム）とドラッグ（パン）が動作する
- [ ] CFG タブに切り替えると force-directed グラフが表示される
  - エントリブロック（MAIN-PARA）が緑色であること
  - STOP RUN を含むブロックがオレンジ色（エグジット）であること
- [ ] DFG タブに切り替えると WS-MESSAGE のノードが表示される
- [ ] MDI フッターにスコア数値・リスクバッジ・指標バーが表示される

---

### タスク 4: goto-sample.cbl の動作確認

Monaco Editor に以下を貼り付けて「Analyze」ボタンを押す：

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

確認項目：

- [ ] CFG タブで GoTo エッジが **紫色破線**（`#8e44ad` / `stroke-dasharray: 6,3`）で表示される
- [ ] ConditionalTrue エッジが緑色実線、ConditionalFalse エッジが赤色実線で表示される
- [ ] PerformThruCall / PerformThruReturn エッジが青色（`#2980b9`）で表示される
- [ ] エッジラベル（`goto` / `conditionaltrue` 等）がエッジ中点付近に表示される

---

### タスク 5: data-sample.cbl の動作確認

Monaco Editor に以下を貼り付けて「Analyze」ボタンを押す：

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

確認項目：

- [ ] DFG タブで Redefines エッジが **オレンジ色破線**（`#e67e22` / `stroke-dasharray: 6,3`）で表示される
- [ ] Redefines エッジの起点ノード（WS-CHAR）に赤枠（`stroke: #e74c3c`）が付いていること
- [ ] GroupOf エッジがグレー実線で表示される
- [ ] 集団項目ノード（WS-BUFFER 等）が大きい円（r=14）で表示される

---

### タスク 6: エラー表示の確認

Monaco Editor に構文エラーを含む COBOL を入力して「Analyze」ボタンを押す：

```
IDENTIFICATION DIVISION
PROGRAM-ID HELLO.
```

確認項目：

- [ ] 全タブパネルにエラーメッセージ一覧（`Line X:Y — ...`）が表示される
- [ ] CFG / DFG / AST のグラフは描画されない

---

### タスク 7: 問題の修正

動作確認で発見した問題を修正する。修正後は再度 `npm run build` および `npm test` を実行し、
テストが引き続き全件 PASS であることを確認する。

仕様との矛盾・未定義事項を発見した場合は `implement/docs/` にフィードバックを記録し、
実装を止めてユーザーに確認すること。

---

## 完了確認

仕様 §11 の全項目にチェックが入ったことを確認する：

```
- [ ] npm run dev で Vite Dev Server が起動する
- [ ] npm run build がエラーなし
- [ ] npm test が全テスト PASS
- [ ] Monaco Editor に hello.cbl を貼り付けて Analyze ボタンを押すと AST ツリーが表示される
- [ ] CFG タブに切り替えると CFG グラフが表示される
- [ ] DFG タブに切り替えると DFG グラフが表示される
- [ ] MDI パネルにスコア・リスクランク・指標バーが表示される
- [ ] goto-sample.cbl の CFG で GoTo エッジが紫破線で表示される
- [ ] data-sample.cbl の DFG で Redefines エッジがオレンジ破線で表示される
- [ ] AST ノードをクリックして折りたたみ・展開が動作する
- [ ] ダイアグラムエリアでパン・ズームが動作する
```

---

## 仕様との矛盾発見時の対処

実装中に仕様の矛盾・未定義事項を発見した場合：

1. `implement/docs/` にフィードバック内容を記録する
2. 実装を停止してユーザーに報告する
3. ユーザーの指示を待つ（`design/specs/` を自分で変更しない）
