import editorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker';
import { analyze } from './api/analyzeApi';
import { toD3Hierarchy } from './adapters/astAdapter';
import { toCfgData } from './adapters/cfgAdapter';
import { toDfgData } from './adapters/dfgAdapter';
import { Editor } from './components/Editor';
import { AstTree } from './components/AstTree';
import { CfgGraph } from './components/CfgGraph';
import { DfgGraph } from './components/DfgGraph';
import { MdiPanel } from './components/MdiPanel';
import { JumpController } from './navigation/JumpController';
import { MonacoHighlighter } from './navigation/MonacoHighlighter';
import { selectionStore } from './store/SelectionStore';
import type { AnalyzeResult } from './types/analyzeResult';
import './styles/main.css';

window.MonacoEnvironment = {
  getWorker(_workerId: string, _label: string): Worker {
    return new editorWorker();
  },
};

const editorContainer = document.getElementById('editor-container')!;
const editor = new Editor(editorContainer);

const mdiContainer = document.getElementById('mdi-panel')!;
const mdiPanel = new MdiPanel(mdiContainer);

const jumpController = new JumpController(
  editor.getEditor(),
  new MonacoHighlighter(editor.getEditor()),
);

// N2: Monaco cursor move → AST node highlight (200ms debounce, wired once)
let cursorDebounce: ReturnType<typeof setTimeout> | null = null;
editor.getEditor().onDidChangeCursorPosition(e => {
  if (cursorDebounce !== null) clearTimeout(cursorDebounce);
  cursorDebounce = setTimeout(() => {
    jumpController.onCursorMove(e.position.lineNumber);
  }, 200);
});

document.querySelectorAll('.tab-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    const tab = btn.getAttribute('data-tab');
    if (!tab) return;
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById(`tab-${tab}`)?.classList.add('active');
  });
});

let currentAstTree: AstTree | null = null;
let currentCfgGraph: CfgGraph | null = null;
let currentDfgGraph: DfgGraph | null = null;

function showErrors(result: AnalyzeResult): void {
  const html = `<div class="error-list">${result.errors
    .map(e => `<p class="error-item">Line ${e.line}:${e.column} — ${e.message}</p>`)
    .join('')}</div>`;
  document.getElementById('tab-ast')!.innerHTML = html;
  document.getElementById('tab-cfg')!.innerHTML = html;
  document.getElementById('tab-dfg')!.innerHTML = html;
}

function renderResult(result: AnalyzeResult): void {
  // Tear down existing component instances (unsubscribes from SelectionStore)
  currentAstTree?.clear();
  currentCfgGraph?.clear();
  currentDfgGraph?.clear();
  currentAstTree = null;
  currentCfgGraph = null;
  currentDfgGraph = null;

  if (result.errors.length > 0) {
    selectionStore.clearAll();
    showErrors(result);
    return;
  }

  const astContainer = document.getElementById('tab-ast')!;
  astContainer.innerHTML = '';
  if (result.ast) {
    currentAstTree = new AstTree(astContainer);
    currentAstTree.setOnNodeClick((nodeId, location) => jumpController.onAstNodeClick(nodeId, location));
    currentAstTree.render(toD3Hierarchy(result.ast));
  }

  const cfgContainer = document.getElementById('tab-cfg')!;
  cfgContainer.innerHTML = '';
  if (result.cfg) {
    currentCfgGraph = new CfgGraph(cfgContainer);
    currentCfgGraph.setOnNodeClick((blockId, location) => jumpController.onCfgBlockClick(blockId, location));
    currentCfgGraph.setOnStatementClick((blockId) => jumpController.onGotoStatementClick(blockId));
    currentCfgGraph.setOnBackgroundClick(() => selectionStore.clearAll());
    currentCfgGraph.render(toCfgData(result.cfg));
  }

  const dfgContainer = document.getElementById('tab-dfg')!;
  dfgContainer.innerHTML = '';
  if (result.dfg) {
    currentDfgGraph = new DfgGraph(dfgContainer);
    currentDfgGraph.setOnNodeClick(nodeId => jumpController.onDfgNodeClick(nodeId));
    currentDfgGraph.setOnBackgroundClick(() => selectionStore.clearAll());
    currentDfgGraph.render(toDfgData(result.dfg));
  }

  if (result.metrics) {
    mdiPanel.render(result.metrics);
  }

  if (result.ast && result.cfg && result.dfg) {
    jumpController.init(result.ast, result.cfg, result.dfg);
  }
}

let lastResult: AnalyzeResult | null = null;

const ro = new ResizeObserver(() => {
  if (lastResult) renderResult(lastResult);
});
ro.observe(document.getElementById('diagram-area')!);

const analyzeBtn = document.getElementById('analyze-btn') as HTMLButtonElement;
analyzeBtn.addEventListener('click', async () => {
  analyzeBtn.disabled = true;
  try {
    const source = editor.getValue();
    const result = await analyze(source);
    lastResult = result;
    renderResult(result);
  } catch (err) {
    selectionStore.clearAll();
    const msg = err instanceof Error ? err.message : String(err);
    const html = `<div class="error-list"><p class="error-item">${msg}</p></div>`;
    document.getElementById('tab-ast')!.innerHTML = html;
    document.getElementById('tab-cfg')!.innerHTML = html;
    document.getElementById('tab-dfg')!.innerHTML = html;
  } finally {
    analyzeBtn.disabled = false;
  }
});
