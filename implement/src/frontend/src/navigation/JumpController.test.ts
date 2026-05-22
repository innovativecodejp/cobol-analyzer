import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { JumpController } from './JumpController';
import { selectionStore } from '../store/SelectionStore';
import type { AstNode, ControlFlowGraph, DataFlowGraph } from '../types/analyzeResult';

function makeAst(): AstNode {
  return {
    id: 'Program:1:0',
    nodeType: 'Program',
    category: 'Structure',
    location: { startLine: 1, startColumn: 0, stopLine: 10, stopColumn: 0 },
    children: [
      {
        id: 'Statement:5:4',
        nodeType: 'Statement',
        category: 'Unit',
        location: { startLine: 5, startColumn: 4, stopLine: 5, stopColumn: 20 },
        children: [],
      },
    ],
  };
}

function makeAstAtLine(line: number): AstNode {
  return {
    id: 'Program:1:0',
    nodeType: 'Program',
    category: 'Structure',
    location: { startLine: 1, startColumn: 0, stopLine: 10, stopColumn: 0 },
    children: [
      {
        id: `Statement:${line}:4`,
        nodeType: 'Statement',
        category: 'Unit',
        location: { startLine: line, startColumn: 4, stopLine: line, stopColumn: 20 },
        children: [],
      },
    ],
  };
}

function makeCfg(): ControlFlowGraph {
  return {
    programName: 'TEST',
    blocks: [
      {
        id: 'block-1',
        paragraphName: 'MAIN',
        statements: [{ statementType: 'GOTO', location: { startLine: 3, startColumn: 0, stopLine: 3, stopColumn: 20 } }],
        location: { startLine: 1, startColumn: 0, stopLine: 5, stopColumn: 0 },
      },
      {
        id: 'block-2',
        paragraphName: 'TARGET',
        statements: [],
        location: { startLine: 7, startColumn: 0, stopLine: 9, stopColumn: 0 },
      },
    ],
    edges: [{ fromBlockId: 'block-1', toBlockId: 'block-2', kind: 'GoTo', isRecursive: false }],
    entryBlockId: 'block-1',
    exitBlockIds: ['block-2'],
    hasAlter: false,
    hasRecursion: false,
  };
}

function makeDfg(): DataFlowGraph {
  return {
    programName: 'TEST',
    nodes: [
      { id: 'WS-A', name: 'WS-A', levelNumber: 5, picture: null, isGroup: false },
      { id: 'WS-B', name: 'WS-B', levelNumber: 5, picture: null, isGroup: false },
    ],
    edges: [],
    impactClosure: { 'WS-A': ['WS-B'] },
  };
}

describe('JumpController', () => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let mockEditor: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let mockHighlighter: any;
  let controller: JumpController;

  beforeEach(() => {
    selectionStore.clearAll();
    mockEditor = { revealLineInCenter: vi.fn(), setPosition: vi.fn() };
    mockHighlighter = { highlight: vi.fn(), clearAll: vi.fn() };
    controller = new JumpController(mockEditor, mockHighlighter);
    controller.init(makeAst(), makeCfg(), makeDfg());
  });

  afterEach(() => {
    controller.dispose();
    selectionStore.clearAll();
  });

  it('onAstNodeClick_selectsNodeAndJumpsToLine', () => {
    const loc = { startLine: 3, startColumn: 0, stopLine: 3, stopColumn: 10 };
    controller.onAstNodeClick('node-1', loc);
    expect(selectionStore.getState().selectedAstNodeId).toBe('node-1');
    expect(mockEditor.revealLineInCenter).toHaveBeenCalledWith(3);
    expect(mockHighlighter.highlight).toHaveBeenCalledWith(3, 3, 'highlight-node');
  });

  it('init_rebuildsLineNodeIndex', () => {
    controller.init(makeAstAtLine(8), makeCfg(), makeDfg());
    controller.onCursorMove(8);

    expect(selectionStore.getState().selectedAstNodeId).toBe('Statement:8:4');
  });

  it('init_doesNotDuplicateSelectionSubscription', () => {
    controller.init(makeAstAtLine(8), makeCfg(), makeDfg());
    controller.init(makeAstAtLine(9), makeCfg(), makeDfg());

    vi.clearAllMocks();

    selectionStore.selectAstNode('node-1', { start: 1, end: 1 });
    selectionStore.clearAll();

    expect(mockHighlighter.clearAll).toHaveBeenCalledTimes(1);
  });

  it('onCursorMove_selectsAstNodeAtLine', () => {
    controller.onCursorMove(5);
    const state = selectionStore.getState();
    expect(state.selectedAstNodeId).toBe('Statement:5:4');
    expect(state.selectedAstLineRange).toEqual({ start: 5, end: 5 });
  });

  it('onCursorMove_noNode_storeCleared', () => {
    selectionStore.selectAstNode('old-node', { start: 1, end: 1 });
    controller.onCursorMove(99);

    expect(selectionStore.getState().selectedAstNodeId).toBeNull();
  });

  it('onCursorMove_suppressedAfterProgrammaticMove', () => {
    const loc = { startLine: 3, startColumn: 0, stopLine: 3, stopColumn: 10 };
    controller.onAstNodeClick('node-1', loc);
    controller.onCursorMove(5);

    expect(selectionStore.getState().selectedAstNodeId).toBe('node-1');
  });

  it('onGotoStatementClick_selectsTargetBlockAndJumps', () => {
    controller.onGotoStatementClick('block-1');
    const state = selectionStore.getState();
    expect(state.selectedCfgBlockId).toBe('block-2');
    expect(mockEditor.revealLineInCenter).toHaveBeenCalledWith(7);
    expect(mockHighlighter.highlight).toHaveBeenCalledWith(7, 9, 'highlight-jump');
  });

  it('onDfgNodeClick_selectsNodeWithImpactClosure', () => {
    controller.onDfgNodeClick('WS-A');
    const state = selectionStore.getState();
    expect(state.selectedDfgNodeId).toBe('WS-A');
    expect(state.impactClosureIds).toEqual(new Set(['WS-B']));
  });

  it('dispose_unsubscribesSelectionStoreListener', () => {
    selectionStore.selectAstNode('node-1', { start: 1, end: 1 });

    controller.dispose();
    vi.clearAllMocks();
    selectionStore.clearAll();

    expect(mockHighlighter.clearAll).not.toHaveBeenCalled();
  });

  it('dispose_isIdempotent', () => {
    expect(() => {
      controller.dispose();
      controller.dispose();
    }).not.toThrow();
  });
});
