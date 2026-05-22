# 2026-05-23_01 Parser テストデータ修正・コミット・push

## 作業概要

Parser テスト用 COBOL サンプル 2 ファイルの差分を確認し、関連テストを実行したうえで
コミットと `origin/master` への push を行った。

---

## 関連コミット

| コミット | 内容 |
|---------|------|
| `a2044a2` | `testdata: fix COBOL sample inputs` |

---

## 実施内容

### 1. テストデータ差分を確認

対象ファイル:

- `tests/CobolAnalyzer.Parser.Tests/TestData/hello.cbl`
- `tests/CobolAnalyzer.Parser.Tests/TestData/data-sample.cbl`

反映内容:

- `hello.cbl` の `WS-MESSAGE` 定義を `PIC X(22)` に更新
- `data-sample.cbl` の末尾改行を追加

---

### 2. Parser テストを実行

実行コマンド:

```powershell
dotnet test tests/CobolAnalyzer.Parser.Tests/CobolAnalyzer.Parser.Tests.csproj
```

結果:

- `CobolAnalyzer.Parser.Tests` 12 件 PASS
- 失敗 0

生成コード由来の `CLSCompliant` 警告は出たが、テスト結果には影響しなかった。

---

### 3. Git 操作

以下を実施した。

- 未コミット差分確認
- 2 ファイルを stage
- `testdata: fix COBOL sample inputs` で commit
- `origin/master` へ push

push 時点では `master` は `origin/master` に対して既存 2 コミット先行しており、
今回の commit を含めて合計 3 コミットが反映された。

---

## Git / Push 結果

- push 先: `origin/master`
- 反映結果: `e567b71..a2044a2`
- push 後の `git status --short --branch`: `master...origin/master`

作業ツリーは clean を確認した。

---

## 注意事項

`git status` などで `C:\Users\msd-d/.config/git/ignore` へのアクセス警告が出たが、
commit / push 自体は正常に完了した。
