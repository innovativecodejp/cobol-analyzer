# log/

設計フェーズの各種ログを管理するディレクトリです。

## サブディレクトリ

| ディレクトリ | 用途 |
|---|---|
| working/ | 日々の作業ログ |

---

## working/ — 作業ログ規約

### ファイル名形式

```
<working_date>_<sequence_no>_<title>.md
```

| フィールド | 説明 |
|---|---|
| `working_date` | 実行日の日付（`yyyy-mm-dd` 形式） |
| `sequence_no` | 同一日付内の連番（01オリジン、2桁ゼロ埋め） |
| `title` | ログ内容を代表するタイトル（英小文字・ハイフン区切り推奨） |

### 例

```
2026-05-01_01_cobol-structure-analysis.md
2026-05-01_02_mdi-definition-review.md
2026-05-02_01_ast-design-brainstorm.md
```

### 連番のルール

- 同一日付のログが複数ある場合は `sequence_no` をインクリメントする
- 異なる日付に戻ることはなく、日付が変わったら `01` からリセットする
