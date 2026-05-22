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
  private programmaticMoveUntil = 0;

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

  private suppressN2(): void {
    // Suppress N2 (cursor→diagram) for 300ms to prevent it from clearing N1/N3 highlight
    this.programmaticMoveUntil = Date.now() + 300;
  }

  onAstNodeClick(nodeId: string, location: SourceLocation): void {
    this.suppressN2();
    selectionStore.selectAstNode(nodeId, { start: location.startLine, end: location.stopLine });
    this.highlighter.highlight(location.startLine, location.stopLine, 'highlight-node');
    this.editor.revealLineInCenter(location.startLine);
    this.editor.setPosition({ lineNumber: location.startLine, column: 1 });
  }

  onCfgBlockClick(blockId: string, location: SourceLocation | null): void {
    this.suppressN2();
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
    this.suppressN2();
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
    // Skip if we just did a programmatic jump (N1/N3) to avoid clearing Monaco highlight
    if (Date.now() < this.programmaticMoveUntil) return;
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
    this.storeUnsub = null;
    this.highlighter.clearAll();
  }
}
