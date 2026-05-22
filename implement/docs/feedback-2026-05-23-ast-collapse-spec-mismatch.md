# Phase 3 実装フィードバック: AST 折りたたみ仕様の不整合

作成日: 2026-05-23  
作成者: implement 側 Codex  
対象仕様: `design/specs/phase3-visualization.md`

`prompts/2026-05-13_05_frontend-review-fix-ast-meta-children.md` の実行中に、
AST 折りたたみ仕様と現行実装・レビュー修正方針の不整合を発見したため記録する。  
design 側でこの文書を参照し、仕様を更新すること。

---

## 1. 仕様との不整合

### 1-A. 折りたたみ方式が spec と実装で異なる

**仕様 (`phase3-visualization.md` §6.1, 行 206)**:
```text
- 折りたたみ: ノードクリックで `children` を `_children` に退避して再描画
```

**現行実装**:
- `src/frontend/src/adapters/astAdapter.ts` の `AstNodeWithMeta` は `collapsed` と `children` のみを持つ
- `src/frontend/src/components/AstTree.ts` は
  `d3.hierarchy(root, d => (d.collapsed ? null : d.children))`
  で表示対象を切り替えており、`_children` を保持しない
- `rg "_children" src/frontend/src` の結果、フロント実装配下に `_children` の参照は存在しない

**問題点**:
- レビュー修正プロンプトは「`AstNodeWithMeta._children` は不要なので削除する」前提だが、
  spec は `_children` を使う実装を要求している
- implement 側では「spec を現行実装に合わせるべきか」「現行実装を spec に戻すべきか」を判断できない

**推奨対応**:
- `phase3-visualization.md` §6.1 の折りたたみ仕様を、現行実装に合わせて
  `collapsed` フラグ方式へ更新するか、
  逆に `_children` 方式へ戻すならその設計意図を明文化する

---

### 1-B. 折りたたみ操作の入力仕様も不一致

**仕様 (`phase3-visualization.md` §6.1, 行 206)**:
```text
- 折りたたみ: ノードクリックで `children` を `_children` に退避して再描画
```

**現行実装 / レビュー修正プロンプト**:
- ノードの単クリックは選択 (`onNodeClick`) に使用
- ノードのダブルクリックで `collapsed` をトグルして折りたたみ
- プロンプト受け入れ条件にも
  「AST のクリック選択・ダブルクリック折り畳みが壊れない」とある

**問題点**:
- 単クリックの意味が spec と実装で異なる
- `_children` 削除タスクを完了扱いにする前に、どちらを正式仕様とするか確定が必要

**推奨対応**:
- `phase3-visualization.md` §6.1 に
  「単クリックで選択、ダブルクリックで `collapsed` を切り替える」
  旨を明記する

---

## 2. implement 側の対応

- 本不整合は `design/specs/` と実装契約に関わるため、実装を停止した
- design 側で spec 更新後、その内容を根拠に `implement` 側で作業を再開する

