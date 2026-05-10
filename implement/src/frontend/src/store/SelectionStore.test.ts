import { describe, it, expect, beforeEach } from 'vitest';
import { selectionStore } from './SelectionStore';

describe('SelectionStore', () => {
  beforeEach(() => {
    selectionStore.clearAll();
  });

  it('selectAstNode_firesChangeEvent', () => {
    const received: (string | null)[] = [];
    const unsub = selectionStore.on(state => {
      received.push(state.selectedAstNodeId);
    });
    selectionStore.selectAstNode('node-1', { start: 5, end: 7 });
    unsub();

    expect(received).toEqual(['node-1']);
  });

  it('selectAstNode_setsLineRange', () => {
    selectionStore.selectCfgBlock('block-1');
    selectionStore.selectAstNode('node-1', { start: 5, end: 7 });
    const state = selectionStore.getState();
    expect(state.selectedAstNodeId).toBe('node-1');
    expect(state.selectedAstLineRange).toEqual({ start: 5, end: 7 });
    expect(state.selectedCfgBlockId).toBeNull();
    expect(state.selectedDfgNodeId).toBeNull();
  });

  it('clearAll_resetsState', () => {
    selectionStore.selectAstNode('node-1', { start: 1, end: 1 });
    selectionStore.clearAll();
    const state = selectionStore.getState();

    expect(state.selectedAstNodeId).toBeNull();
    expect(state.selectedAstLineRange).toBeNull();
    expect(state.selectedCfgBlockId).toBeNull();
    expect(state.selectedDfgNodeId).toBeNull();
    expect(state.impactClosureIds).toEqual(new Set());
  });

  it('selectDfgNode_setsImpactClosureIds', () => {
    selectionStore.selectDfgNode('WS-A', ['WS-B', 'WS-C']);
    const state = selectionStore.getState();
    expect(state.selectedDfgNodeId).toBe('WS-A');
    expect(state.impactClosureIds).toEqual(new Set(['WS-B', 'WS-C']));
    expect(state.selectedAstNodeId).toBeNull();
  });
});
