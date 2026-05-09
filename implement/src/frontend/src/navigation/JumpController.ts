import type * as monaco from 'monaco-editor';
import type { AstNode, ControlFlowGraph, DataFlowGraph, SourceLocation } from '../types/analyzeResult';
import { selectionStore } from '../store/SelectionStore';
import { LineNodeIndex } from './LineNodeIndex';
import type { MonacoHighlighter } from './MonacoHighlighter';

const GOTO_EDGE_KINDS = new Set(['GoTo', 'PerformCall', 'PerformThruCall']);

export class JumpController {
  private index: LineNodeIndex | null = null;
  private cfg: ControlFlowGraph | null = null;
  private dfg: DataFlowGraph | null = null;
  private storeUnsub: (() => void) | null = null;

  constructor(
    private readonly editor: monaco.editor.IStandaloneCodeEditor,
    private readonly highlighter: MonacoHighlighter,
  ) {
    this.storeUnsub = selectionStore.on(state => {
      if (!state.selectedAstNodeId && !state.selectedCfgBlockId && !state.selectedDfgNodeId) {
        this.highlighter.clearAll();
      }
    });
  }

  init(ast: AstNode, cfg: ControlFlowGraph, dfg: DataFlowGraph): void {
    this.index = new LineNodeIndex(ast);
    this.cfg = cfg;
    this.dfg = dfg;
    this.highlighter.clearAll();
    selectionStore.clearAll();
  }

  onAstNodeClick(nodeId: string, location: SourceLocation): void {
    selectionStore.selectAstNode(nodeId, { start: location.startLine, end: location.stopLine });
    this.highlighter.highlight(location.startLine, location.stopLine, 'highlight-node');
    this.editor.revealLineInCenter(location.startLine);
    this.editor.setPosition({ lineNumber: location.startLine, column: 1 });
  }

  onCfgBlockClick(blockId: string, location: SourceLocation | null): void {
    selectionStore.selectCfgBlock(blockId);
    if (!location) return;
    this.highlighter.highlight(location.startLine, location.stopLine, 'highlight-node');
    this.editor.revealLineInCenter(location.startLine);
    this.editor.setPosition({ lineNumber: location.startLine, column: 1 });
  }

  onGotoStatementClick(fromBlockId: string): void {
    const edge = this.cfg?.edges.find(
      e => e.fromBlockId === fromBlockId && GOTO_EDGE_KINDS.has(e.kind),
    );
    if (!edge) return;

    const toBlock = this.cfg?.blocks.find(b => b.id === edge.toBlockId);
    selectionStore.selectCfgBlock(edge.toBlockId);

    if (!toBlock?.location) return;
    this.highlighter.highlight(toBlock.location.startLine, toBlock.location.stopLine, 'highlight-jump');
    this.editor.revealLineInCenter(toBlock.location.startLine);
    this.editor.setPosition({ lineNumber: toBlock.location.startLine, column: 1 });
  }

  onDfgNodeClick(nodeId: string): void {
    const closureIds = this.dfg?.impactClosure[nodeId] ?? [];
    selectionStore.selectDfgNode(nodeId, closureIds);
    this.highlighter.clearAll();
  }

  onCursorMove(line: number): void {
    if (!this.index) return;
    const entry = this.index.lookup(line);
    if (entry) {
      selectionStore.selectAstNode(entry.nodeId, { start: entry.startLine, end: entry.stopLine });
      this.highlighter.clearAll();
    } else {
      selectionStore.clearAll();
    }
  }

  dispose(): void {
    this.storeUnsub?.();
  }
}
