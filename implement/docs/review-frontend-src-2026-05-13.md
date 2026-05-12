# フロントエンド実装コードレビュー

日付: 2026-05-13  
対象: `implement/src/frontend/src/`

---

## Bug（動作に影響するもの）

### 1. CfgGraph: D3 シミュレーションが `clear()` で停止されない

**ファイル**: `components/CfgGraph.ts:155–158`  
**影響**: 連続解析時の軽微なCPUリーク

`render()` 内で生成した `simulation` はローカル変数のため `clear()` から参照できない。再解析のたびに旧シミュレーションがクールダウンまで動き続ける。

**修正**:

```ts
// クラスメンバに昇格
private simulation: d3.Simulation<SimNode, SimLink> | null = null;

// render() 先頭
this.simulation?.stop();
this.simulation = d3.forceSimulation<SimNode>(nodes)...

// clear() 内
this.simulation?.stop();
this.simulation = null;
```

---

### 2. `lastResult` がエラー時にリセットされない

**ファイル**: `main.ts:151–158`  
**影響**: エラー後にリサイズすると旧解析結果が再描画される

`catch` ブロックで `lastResult = null` が抜けているため、`ResizeObserver` が旧データで `renderResult()` を呼んでしまう。

**修正**:

```ts
} catch (err) {
  lastResult = null; // 追加
  selectionStore.clearAll();
  ...
}
```

---

### 3. `JumpController.dispose()` が呼ばれない

**ファイル**: `main.ts:31–34`、`navigation/JumpController.ts:92–94`  
**影響**: SelectionStore subscription のリーク（現在は実害なし）

`JumpController` は `dispose()` を持つが `main.ts` から呼ばれない。シングルインスタンスなので現状は問題ないが、`renderResult()` 内の他コンポーネントと同様に管理すべき。

**修正**: `renderResult()` 冒頭で `jumpController.dispose()` を呼び、再初期化前にクリーンアップする。ただし `jumpController` はグローバルな Monaco インスタンスに紐づくため、dispose/re-init ではなく `init()` のリセット責務を整理する方が現実的。

---

## 設計の不整合

### 4. `CfgGraph.setOnStatementClick` の第2引数が使われていない

**ファイル**: `components/CfgGraph.ts:75`、`main.ts:113`  
**影響**: public API の型と実装の契約が食い違っている

コールバック型は `(blockId: string, statementType: string) => void` だが、`main.ts` は `blockId` のみ利用し `JumpController.onGotoStatementClick` も `statementType` を受け取らない。

**修正案 A**: 不要なら型から削除してシグネチャを `(blockId: string) => void` に揃える。  
**修正案 B**: 将来 `statementType` を使う意図があるなら `JumpController` 側を先に対応させる。

---

### 5. `AstNodeWithMeta._children` が dead code

**ファイル**: `adapters/astAdapter.ts:6`  
**影響**: 型に存在するが書き込み・読み取りともにない

折り畳みは `collapsed` フラグと `d3.hierarchy(root, d => d.collapsed ? null : d.children)` で実現されており `_children` は不要。型から削除してよい。

---

## コード品質

### 6. エラー表示の重複

**ファイル**: `main.ts:77–83`、`main.ts:154–157`

`showErrors()` と `catch` ブロックがほぼ同じHTML書き込みを繰り返している。

**修正**: `catch` 側から `showErrors()` を活用するか、`showErrorMessage(msg: string)` を切り出して両者から呼ぶ。

```ts
function showErrorMessage(msg: string): void {
  const html = `<div class="error-list"><p class="error-item">${msg}</p></div>`;
  document.getElementById('tab-ast')!.innerHTML = html;
  document.getElementById('tab-cfg')!.innerHTML = html;
  document.getElementById('tab-dfg')!.innerHTML = html;
}
```

---

### 7. `navLabels` click: `targetIds.slice(1)` に説明がない

**ファイル**: `components/CfgGraph.ts:249`

なぜ最初のターゲットをスキップするのか意図が不明。PERFORM THRU の複数ターゲット処理であれば一言コメントを入れる。

```ts
// PERFORM THRU: 直接ジャンプ先(index 0)はonStatementClickで処理済み、残りをimpactとして強調
this.impactBlockIds = new Set(targetIds.slice(1));
```

---

## 良い点（維持すること）

- **レイヤー分離が明確**: `types → api → adapters → store → navigation → components` の依存方向が一方向で一貫している
- **SelectionStore の相互排他設計**: `selectAstNode`/`selectCfgBlock`/`selectDfgNode` がそれぞれ他の選択をリセットし、不整合状態が起きない
- **suppressN2 の競合対策**: N1/N3 後の 300ms 抑制でカーソルイベントが選択をクリアする race condition を防いでいる
- **expandToNode の自動展開**: 折り畳み中の祖先が N2 経由で選択されたとき自動展開して再 render するロジックが丁寧
- **テスト**: adapters/store/navigation の各層に vitest テストがあり、核心ロジックがカバーされている

---

## 優先度まとめ

| 優先度 | # | 種別 | 概要 | 影響 |
|--------|---|------|------|------|
| 高 | 1 | Bug | CfgGraph: simulation 未停止 | 連続解析時のCPUリーク |
| 高 | 2 | Bug | lastResult 未クリア → リサイズで旧データ表示 | UX不整合 |
| 中 | 3 | Bug | JumpController.dispose() 未呼出 | リーク（現状実害なし） |
| 中 | 4 | 設計 | setOnStatementClick 第2引数が dead param | API契約の不整合 |
| 低 | 5 | 設計 | _children が dead code | 型の不要フィールド |
| 低 | 6 | 品質 | エラー表示の重複 | 保守性 |
| 低 | 7 | 品質 | slice(1) の意図不明 | 可読性 |
