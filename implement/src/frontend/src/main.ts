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

function showErrors(result: AnalyzeResult): void {
  const html = `<div class="error-list">${result.errors
    .map(e => `<p class="error-item">Line ${e.line}:${e.column} — ${e.message}</p>`)
    .join('')}</div>`;
  document.getElementById('tab-ast')!.innerHTML = html;
  document.getElementById('tab-cfg')!.innerHTML = html;
  document.getElementById('tab-dfg')!.innerHTML = html;
}

function renderResult(result: AnalyzeResult): void {
  if (result.errors.length > 0) {
    showErrors(result);
    return;
  }

  const astContainer = document.getElementById('tab-ast')!;
  astContainer.innerHTML = '';
  if (result.ast) {
    const tree = new AstTree(astContainer);
    tree.render(toD3Hierarchy(result.ast));
  }

  const cfgContainer = document.getElementById('tab-cfg')!;
  cfgContainer.innerHTML = '';
  if (result.cfg) {
    const graph = new CfgGraph(cfgContainer);
    graph.render(toCfgData(result.cfg));
  }

  const dfgContainer = document.getElementById('tab-dfg')!;
  dfgContainer.innerHTML = '';
  if (result.dfg) {
    const graph = new DfgGraph(dfgContainer);
    graph.render(toDfgData(result.dfg));
  }

  if (result.metrics) {
    mdiPanel.render(result.metrics);
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
    const msg = err instanceof Error ? err.message : String(err);
    const html = `<div class="error-list"><p class="error-item">${msg}</p></div>`;
    document.getElementById('tab-ast')!.innerHTML = html;
    document.getElementById('tab-cfg')!.innerHTML = html;
    document.getElementById('tab-dfg')!.innerHTML = html;
  } finally {
    analyzeBtn.disabled = false;
  }
});
