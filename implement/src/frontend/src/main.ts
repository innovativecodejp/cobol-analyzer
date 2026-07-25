import editorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker';
import { analyze, loadProgramByName } from './api/analyzeApi';
import { downloadAnnotationReport } from './api/exportApi';
import { STATIC_MODE, loadManifest, loadProgramSource } from './api/staticData';
import { toD3Hierarchy } from './adapters/astAdapter';
import { toCfgData } from './adapters/cfgAdapter';
import { toDfgData } from './adapters/dfgAdapter';
import { Editor } from './components/Editor';
import { AstTree } from './components/AstTree';
import { CfgGraph } from './components/CfgGraph';
import { CommentPanel } from './components/CommentPanel';
import { DfgGraph } from './components/DfgGraph';
import { MdiPanel } from './components/MdiPanel';
import { ProjectPanel } from './components/ProjectPanel';
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
window.addEventListener('beforeunload', () => jumpController.dispose());

const commentPanel = new CommentPanel(
  document.getElementById('tab-comment')!,
  () => editor.getValue(),
  source => editor.setValue(source),
  editor.getEditor(),
);
commentPanel.render();

const projectPanel = new ProjectPanel(document.getElementById('tab-project')!);
projectPanel.render();

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
    if (tab === 'comment') {
      commentPanel.render();
    } else if (tab === 'project') {
      projectPanel.render();
    }
  });
});

let currentAstTree: AstTree | null = null;
let currentCfgGraph: CfgGraph | null = null;
let currentDfgGraph: DfgGraph | null = null;

function setDiagramErrorHtml(html: string): void {
  document.getElementById('tab-ast')!.innerHTML = html;
  document.getElementById('tab-cfg')!.innerHTML = html;
  document.getElementById('tab-dfg')!.innerHTML = html;
}

function showErrors(result: AnalyzeResult): void {
  const html = `<div class="error-list">${result.errors
    .map(e => `<p class="error-item">Line ${e.line}:${e.column} — ${e.message}</p>`)
    .join('')}</div>`;
  setDiagramErrorHtml(html);
}

function showErrorMessage(msg: string): void {
  setDiagramErrorHtml(`<div class="error-list"><p class="error-item">${msg}</p></div>`);
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
    lastResult = null;
    selectionStore.clearAll();
    const msg = err instanceof Error ? err.message : String(err);
    showErrorMessage(msg);
  } finally {
    analyzeBtn.disabled = false;
  }
});

// 静的データモード（デモ C）: バックエンド非依存。プログラムピッカーで事前計算結果を読む。
// 任意ソース解析（Analyze）・コメント・プロジェクトはバックエンド前提のため非表示（§6-3）。
if (STATIC_MODE) {
  void initStaticMode();
}

async function initStaticMode(): Promise<void> {
  analyzeBtn.style.display = 'none';
  editor.getEditor().updateOptions({ readOnly: true });
  document
    .querySelectorAll('.tab-btn[data-tab="comment"], .tab-btn[data-tab="project"]')
    .forEach(btn => ((btn as HTMLElement).style.display = 'none'));

  let manifest;
  try {
    manifest = await loadManifest();
  } catch (err) {
    showErrorMessage(err instanceof Error ? err.message : String(err));
    return;
  }

  const bar = document.createElement('div');
  bar.id = 'program-picker-bar';

  const label = document.createElement('label');
  label.htmlFor = 'program-picker';
  label.textContent = 'プログラム: ';

  const picker = document.createElement('select');
  picker.id = 'program-picker';
  for (const p of manifest.programs) {
    const option = document.createElement('option');
    option.value = p.programName;
    option.textContent = `${p.programName}  (MDI ${p.mdi.toFixed(1)} · ${p.strategy})`;
    picker.appendChild(option);
  }

  const dlButton = document.createElement('button');
  dlButton.id = 'static-report-dl';
  dlButton.type = 'button';
  dlButton.textContent = '注釈レポートDL';
  dlButton.addEventListener('click', () => {
    void downloadAnnotationReport({ fileName: picker.value, source: '' });
  });

  bar.append(label, picker, dlButton);
  document.getElementById('left-pane')!.insertBefore(bar, editorContainer);

  const attribution = document.createElement('div');
  attribution.id = 'static-attribution';
  const link = document.createElement('a');
  link.href = manifest.corpus.sourceUrl;
  link.target = '_blank';
  link.rel = 'noopener';
  link.textContent = manifest.corpus.name;
  attribution.append(
    document.createTextNode('静的デモ（バックエンド不要）｜ 出典 '),
    link,
    document.createTextNode(
      ` (${manifest.corpus.license}, pin ${manifest.corpus.pinnedCommit.slice(0, 12)})`,
    ),
  );
  document.getElementById('mdi-bar')!.appendChild(attribution);

  async function loadProgram(name: string): Promise<void> {
    try {
      const [source, result] = await Promise.all([loadProgramSource(name), loadProgramByName(name)]);
      editor.setValue(source);
      lastResult = result;
      renderResult(result);
    } catch (err) {
      lastResult = null;
      selectionStore.clearAll();
      showErrorMessage(err instanceof Error ? err.message : String(err));
    }
  }

  picker.addEventListener('change', () => void loadProgram(picker.value));
  if (manifest.programs.length > 0) await loadProgram(manifest.programs[0].programName);
}
