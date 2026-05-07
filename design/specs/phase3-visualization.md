# Phase 3 仕様：ダイアグラム可視化

バージョン: 1.1  
作成日: 2026-05-05  
更新日: 2026-05-05（Phase 4 仕様策定に伴い DataFlowGraph 型へ ImpactClosure を追補）  
ステータス: 確定（implement/ への引き渡し可）

前提: `design/specs/phase2-engine.md` の実装が完了し、`POST /api/analyze` が稼働していること。

---

## 1. 目的・スコープ

TypeScript + D3.js によるシングルページアプリケーションを構築し、
Phase 2 の `POST /api/analyze` レスポンス（AST / CFG / DFG / Metrics）をブラウザ上に可視化する。

### スコープ内
- フロントエンド開発環境構築（Vite / TypeScript / D3.js / Monaco Editor / Vitest）
- 画面レイアウト（Monaco Editor + ダイアグラムタブ + MDIパネル）
- AST ツリー表示（折りたたみ可能）
- CFG グラフ表示（force-directed、エッジ種別カラーリング）
- DFG グラフ表示（force-directed、エッジ種別カラーリング）
- MDI スコア・リスクランク・指標内訳パネル
- API アダプター（レスポンス JSON → D3.js データ形式変換）
- CORS 設定（バックエンド側）

### スコープ外（Phase 4以降）
- ノード↔ソース双方向ナビゲーション
- COBOL シンタックスハイライト（Monaco）
- dagre レイアウト（有向グラフの整列）
- プログラム間依存グラフ

---

## 2. フロントエンド技術スタック

| 技術 | バージョン | 用途 |
|------|-----------|------|
| TypeScript | 5.x | 言語 |
| Vite | 5.x | ビルドツール・Dev Server |
| D3.js | 7.x | グラフ・ツリー描画（SVG） |
| Monaco Editor | 0.47.x | COBOLソース入力（プレーンテキスト） |
| Vitest | 1.x | ユニットテスト |

---

## 3. ディレクトリ構造

```
implement/
└── src/
    └── frontend/
        ├── package.json
        ├── vite.config.ts
        ├── tsconfig.json
        ├── index.html
        └── src/
            ├── main.ts
            ├── types/
            │   └── analyzeResult.ts       ← API レスポンスの TypeScript 型定義
            ├── api/
            │   └── analyzeApi.ts          ← POST /api/analyze 呼び出し
            ├── adapters/
            │   ├── astAdapter.ts          ← AstNode → d3.hierarchy 変換
            │   ├── cfgAdapter.ts          ← ControlFlowGraph → D3 nodes/links 変換
            │   └── dfgAdapter.ts          ← DataFlowGraph → D3 nodes/links 変換
            ├── components/
            │   ├── Editor.ts              ← Monaco Editor ラッパー
            │   ├── AstTree.ts             ← D3.js tree レイアウト
            │   ├── CfgGraph.ts            ← D3.js force-directed（CFG）
            │   ├── DfgGraph.ts            ← D3.js force-directed（DFG）
            │   └── MdiPanel.ts            ← MDI スコア・指標内訳
            └── styles/
                └── main.css
```

---

## 4. 画面レイアウト

```
┌──────────────────────────────────────────────────────────────┐
│  COBOL Analyzer                                              │ ← Header
├─────────────────────┬────────────────────────────────────────┤
│                     │  [ AST ]  [ CFG ]  [ DFG ]            │ ← タブ
│  Monaco Editor      ├────────────────────────────────────────┤
│  （COBOLソース）     │                                        │
│                     │       ダイアグラム表示エリア            │
│                     │       （D3.js SVG）                    │
│  [ Analyze ]        │                                        │
├─────────────────────┴────────────────────────────────────────┤
│  MDI パネル: Score: 18.5  Risk: Low  [ CC ] [ GD ] ...      │ ← 下部固定
└──────────────────────────────────────────────────────────────┘
```

- 左ペイン（幅40%）: Monaco Editor + Analyze ボタン
- 右ペイン（幅60%）: タブ + SVG ダイアグラム
- 下部バー（固定高さ）: MDI パネル
- SVG エリアはウィンドウリサイズに追従する（`ResizeObserver`）

---

## 5. API アダプター

Phase 2 API の JSON 形式を D3.js が消費できる形式に変換する責務を担う。
変換ロジックはコンポーネントに混在させず、`adapters/` に分離する。

### 5.1 CFG アダプター（cfgAdapter.ts）

**入力**: Phase 2 `ControlFlowGraph` JSON

```typescript
// 入力型（Phase 2 API レスポンスの部分）
interface CfgBlock {
  id: string;
  paragraphName: string | null;
  statements: unknown[];
}
interface CfgEdge {
  fromBlockId: string;
  toBlockId: string;
  kind: CfgEdgeKind;
}
type CfgEdgeKind =
  | 'FallThrough' | 'ConditionalTrue' | 'ConditionalFalse'
  | 'GoTo' | 'PerformCall' | 'PerformReturn'
  | 'PerformThruCall' | 'PerformThruReturn';
```

**出力**: D3 force-directed 形式

```typescript
interface D3Node {
  id: string;
  label: string;           // paragraphName ?? id
  statementCount: number;
  isEntry: boolean;
  isExit: boolean;
}
interface D3Link {
  source: string;
  target: string;
  kind: CfgEdgeKind;
}
interface D3CfgData { nodes: D3Node[]; links: D3Link[]; }
```

### 5.2 DFG アダプター（dfgAdapter.ts）

**入力**: Phase 2 `DataFlowGraph` JSON → **出力**: D3 force-directed 形式

```typescript
interface D3DfgNode {
  id: string;
  name: string;
  levelNumber: number;
  isGroup: boolean;
  hasRedefines: boolean;   // Redefines エッジの起点なら true
}
interface D3DfgLink {
  source: string;
  target: string;
  kind: DfgEdgeKind;       // 'Define' | 'Use' | 'Redefines' | 'GroupOf'
}
interface D3DfgData { nodes: D3DfgNode[]; links: D3DfgLink[]; }
```

### 5.3 AST アダプター（astAdapter.ts）

`d3.hierarchy()` は `children` プロパティを持つオブジェクトをそのまま受け取れる。
Phase 2 AST の `children[]` 構造はそのまま渡せるため、変換は最小限とする。

```typescript
// d3.hierarchy() に渡す前に各ノードへ視覚プロパティを付与する
function toD3Hierarchy(astNode: AstNode): AstNodeWithMeta {
  return {
    ...astNode,
    collapsed: astNode.category === 'Element',  // Elementは初期折りたたみ
    children: astNode.children.map(toD3Hierarchy),
  };
}
```

---

## 6. ダイアグラム仕様

### 6.1 AST ツリー（AstTree.ts）

- レイアウト: `d3.tree()`（左→右の水平ツリー）
- ノード形状: 円（半径 6px）
- ノード色:

| NodeCategory | 色 |
|-------------|-----|
| Structure | `#1a4fa8`（紺） |
| Unit | `#2e86c1`（水色） |
| Element | `#808080`（グレー） |

- ラベル: `nodeType`（ノード右隣）
- 折りたたみ: ノードクリックで `children` を `_children` に退避して再描画
- ズーム: `d3.zoom()` によるパン・ズーム

### 6.2 CFG グラフ（CfgGraph.ts）

- レイアウト: `d3.forceSimulation`
  - `forceLink`（リンク距離 80px）
  - `forceManyBody`（反発力 -200）
  - `forceCenter`
- ノード形状: 角丸矩形（幅 120px、高さ 40px）
- ノード色:

| 条件 | 色 |
|------|-----|
| エントリ（`isEntry=true`） | `#27ae60`（緑） |
| エグジット（`isExit=true`） | `#e67e22`（オレンジ） |
| ALTER警告ブロック | 赤枠（`stroke: #e74c3c, stroke-width: 2`） |
| 通常 | `#2e86c1`（水色） |

- ラベル: `label`（paragraphName または id）+ 文数（右下小文字）
- エッジ色:

| CfgEdgeKind | 色 | 線種 |
|-------------|-----|------|
| FallThrough | `#808080` | 実線 |
| ConditionalTrue | `#27ae60` | 実線 |
| ConditionalFalse | `#e74c3c` | 実線 |
| GoTo | `#8e44ad` | 破線 |
| PerformCall | `#2980b9` | 実線 |
| PerformReturn | `#2980b9` | 点線 |
| PerformThruCall | `#2980b9` | 破線 |
| PerformThruReturn | `#2980b9` | 点線 |

- エッジラベル: エッジ中点に種別名（小文字）を表示
- ズーム: `d3.zoom()` によるパン・ズーム
- 最大ノード数: 200ブロックを超える場合、SVG上に警告メッセージを表示し描画をスキップする

### 6.3 DFG グラフ（DfgGraph.ts）

- レイアウト: `d3.forceSimulation`（CFG と同設定）
- ノード形状:

| 条件 | 形状 |
|------|------|
| `isGroup=true` | 円（半径 14px） |
| `isGroup=false` | 円（半径 8px） |
| `hasRedefines=true` | 赤枠（`stroke: #e74c3c`） |

- ノード色: `#2e86c1`（水色）統一
- ラベル: `name`
- エッジ色:

| DfgEdgeKind | 色 | 線種 |
|-------------|-----|------|
| Define | `#e74c3c`（赤） | 実線・矢印 |
| Use | `#2980b9`（青） | 実線・矢印 |
| Redefines | `#e67e22`（オレンジ） | 破線 |
| GroupOf | `#808080`（グレー） | 実線（矢印なし） |

- ズーム: `d3.zoom()` によるパン・ズーム

### 6.4 MDI パネル（MdiPanel.ts）

- スコア数値: 大きめフォント（2rem）、小数第1位まで
- リスクバッジ:

| MdiRisk | 背景色 |
|---------|-------|
| Low | `#27ae60` |
| Medium | `#f39c12` |
| High | `#e67e22` |
| Critical | `#e74c3c` |

- 指標内訳: 横方向バーチャート（各指標の加重後スコアを積み上げ）
  - 表示順: CC / GD / AD / ND / RD / CS
  - ラベル: 指標ID + 実測値（例: `CC: 3`）

---

## 7. CORS 設定（バックエンド）

`CobolAnalyzer.API/Program.cs` に以下を追加する：

```csharp
// 開発環境のみ全オリジン許可
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// ...

app.UseCors("DevCors");
```

本番環境では許可オリジンを明示的に指定すること（Phase 3 スコープ外）。

---

## 8. API 呼び出し（analyzeApi.ts）

```typescript
const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

export async function analyze(source: string): Promise<AnalyzeResult> {
  const res = await fetch(`${API_BASE}/api/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ source }),
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json();
}
```

`VITE_API_BASE` は `.env.development` で設定する：

```
VITE_API_BASE=http://localhost:5000
```

---

## 9. TypeScript 型定義（types/analyzeResult.ts）

Phase 2 API レスポンスに対応する型を手動定義する。

```typescript
export type NodeCategory = 'Structure' | 'Unit' | 'Element';

export interface AstNode {
  nodeType: string;
  category: NodeCategory;
  location: SourceLocation;
  children: AstNode[];
}

export interface SourceLocation {
  startLine: number; startColumn: number;
  stopLine: number; stopColumn: number;
}

export type CfgEdgeKind =
  | 'FallThrough' | 'ConditionalTrue' | 'ConditionalFalse'
  | 'GoTo' | 'PerformCall' | 'PerformReturn'
  | 'PerformThruCall' | 'PerformThruReturn';

export type DfgEdgeKind = 'Define' | 'Use' | 'Redefines' | 'GroupOf';

export type MdiRisk = 'Low' | 'Medium' | 'High' | 'Critical';

export interface ControlFlowGraph {
  programName: string;
  blocks: CfgBlock[];
  edges: CfgEdge[];
  entryBlockId: string;
  exitBlockIds: string[];
  hasAlter: boolean;
  hasRecursion: boolean;
}

export interface CfgBlock {
  id: string;
  paragraphName: string | null;
  statements: unknown[];
}

export interface CfgEdge {
  fromBlockId: string;
  toBlockId: string;
  kind: CfgEdgeKind;
}

export interface DataFlowGraph {
  programName: string;
  nodes: DfgNode[];
  edges: DfgEdge[];
  // Phase 4 双方向ナビゲーションで使用（キー: DataName、値: 影響を受ける DataName[]）
  impactClosure: Record<string, string[]>;
}

export interface DfgNode {
  id: string; name: string;
  levelNumber: number; picture: string | null;
  isGroup: boolean;
}

export interface DfgEdge {
  fromId: string; toId: string;
  kind: DfgEdgeKind;
}

export interface MetricsResult {
  programName: string;
  cyclomaticComplexity: number;
  goToDensity: number;
  alterCount: number;
  maxNestingDepth: number;
  redefinesDensity: number;
  crossScopeDependencies: number;
  mdi: MdiScore;
}

export interface MdiScore {
  score: number;
  risk: MdiRisk;
  weightedContributions: Record<string, number>;
}

export interface ParseError {
  line: number; column: number; message: string;
}

export interface AnalyzeResult {
  ast: AstNode | null;
  cfg: ControlFlowGraph | null;
  dfg: DataFlowGraph | null;
  metrics: MetricsResult | null;
  errors: ParseError[];
}
```

---

## 10. テスト要件（Vitest）

### adapters/*.test.ts

| テスト名 | 検証内容 |
|----------|---------|
| `cfgAdapter_mapsBlocksToNodes` | `blocks[]` が `nodes[]` に正しく変換される |
| `cfgAdapter_mapsEdgesToLinks` | `fromBlockId/toBlockId` が `source/target` にマップされる |
| `cfgAdapter_entryNodeFlagged` | `entryBlockId` に対応するノードの `isEntry = true` |
| `cfgAdapter_exitNodesFlagged` | `exitBlockIds` に対応するノードの `isExit = true` |
| `dfgAdapter_mapsNodesToD3Nodes` | `DfgNode[]` が `D3DfgNode[]` に正しく変換される |
| `dfgAdapter_redefinesEdgeSetsFlag` | Redefines エッジの起点ノードで `hasRedefines = true` |
| `astAdapter_elementNodesInitiallyCollapsed` | Element カテゴリノードが `collapsed = true` で初期化される |

### MdiPanel.test.ts

| テスト名 | 検証内容 |
|----------|---------|
| `mdiPanel_lowRisk_greenBadge` | Risk = Low でバッジ色が `#27ae60` |
| `mdiPanel_criticalRisk_redBadge` | Risk = Critical でバッジ色が `#e74c3c` |

---

## 11. 完了基準

以下をすべて満たした時点で Phase 3 完了とする。

- [ ] `npm run dev` で Vite Dev Server が起動する
- [ ] `npm run build` がエラーなし
- [ ] `npm test` が全テストPASS
- [ ] Monaco Editor に hello.cbl を貼り付けて Analyze ボタンを押すと AST ツリーが表示される
- [ ] CFG タブに切り替えると CFG グラフが表示される
- [ ] DFG タブに切り替えると DFG グラフが表示される
- [ ] MDI パネルにスコア・リスクランク・指標バーが表示される
- [ ] goto-sample.cbl の CFG で GoTo エッジが紫破線で表示される
- [ ] data-sample.cbl の DFG で Redefines エッジがオレンジ破線で表示される
- [ ] AST ノードをクリックして折りたたみ・展開が動作する
- [ ] ダイアグラムエリアでパン・ズームが動作する

---

## 12. 実装上の注意事項

1. **Monaco Editor の COBOL**: Phase 3 ではプレーンテキストとして扱う。`monaco.editor.create()` の `language` オプションは `'plaintext'` を指定する。シンタックスハイライトは Phase 4 以降で検討。

2. **SVG の最大ノード数制限**: CFG ブロック数が 200 を超える場合、SVG 上に「ノード数が多すぎるため表示を省略しています（{n} ブロック）」と表示し、描画をスキップする。

3. **エラーレスポンスの表示**: `errors[]` が空でない場合（構文エラー）、ダイアグラムエリアにエラーメッセージ一覧を表示し、CFG/DFG/AST の描画は行わない。

4. **型定義の鮮度管理**: `types/analyzeResult.ts` は Phase 2 実装後に API の実レスポンスと照合し、齟齬があれば更新すること（`implement/docs/` にフィードバックを記録してから `design/specs/` を修正する）。

5. **CORS は開発環境限定**: `AllowAnyOrigin()` は開発環境のみの設定とし、`appsettings.Development.json` の `"AllowDevCors": true` フラグで切り替える設計にすること。

---

## 13. 参照資料

| 資料 | 参照箇所 | 本仕様との対応 |
|------|---------|--------------|
| `design/specs/phase2-engine.md` §4 | CFG モデル（BasicBlock / CfgEdge / CfgEdgeKind） | §5.1 CFG アダプター入力型 |
| `design/specs/phase2-engine.md` §5 | DFG モデル（DfgNode / DfgEdge / DfgEdgeKind） | §5.2 DFG アダプター入力型 |
| `design/specs/phase2-engine.md` §6 | MDI 指標・MdiScore・MdiRisk | §6.4 MDI パネル |
| `design/specs/phase2-engine.md` §8 | `POST /api/analyze` レスポンス例 | §9 TypeScript 型定義 |
| `design/brainstorm/phase3-planning.md` | 設計判断メモ | 本仕様全体 |
