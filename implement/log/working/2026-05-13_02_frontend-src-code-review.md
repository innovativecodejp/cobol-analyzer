# 2026-05-13_02 フロントエンド実装コードレビュー

## 作業概要

`implement/src/frontend/src/` 全体のコードレビューを実施した。
結果は `implement/docs/review-frontend-src-2026-05-13.md` に記録した。

---

## レビュー対象

| 区分 | ファイル数 |
|------|-----------|
| types/ | 3 |
| api/ | 5 |
| adapters/ | 3 |
| store/ | 1 |
| navigation/ | 3 |
| components/ | 10 |
| main.ts | 1 |

---

## 発見事項サマリー

| 優先度 | 種別 | 概要 |
|--------|------|------|
| 高 | Bug | CfgGraph: D3 simulation が clear() で停止されない |
| 高 | Bug | lastResult がエラー時にリセットされず、リサイズで旧データ再表示 |
| 中 | Bug | JumpController.dispose() が main.ts から呼ばれない |
| 中 | 設計 | CfgGraph.setOnStatementClick の第2引数が未使用 |
| 低 | 設計 | AstNodeWithMeta._children が dead code |
| 低 | 品質 | main.ts のエラー表示コード重複 |
| 低 | 品質 | CfgGraph navLabels の slice(1) に説明コメントなし |

specs への影響なし。DESIGN プロジェクトへの差し戻し不要。

---

## 関連コミット

| コミット | 内容 |
|---------|------|
| `2179ec0` | `docs: record frontend src code review results` |

---

## 次のアクション

優先度「高」の 2件（simulation 未停止、lastResult 未クリア）を implement 内で修正する。
