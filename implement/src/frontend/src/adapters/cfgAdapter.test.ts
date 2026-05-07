import { describe, it, expect } from 'vitest';
import { toCfgData } from './cfgAdapter';
import type { ControlFlowGraph } from '../types/analyzeResult';

function buildCfg(): ControlFlowGraph {
  return {
    programName: 'MYPROG',
    blocks: [
      {
        id: 'block-1',
        paragraphName: 'PARA-A',
        statements: [
          { statementType: 'MOVE', location: { startLine: 1, startColumn: 0, stopLine: 1, stopColumn: 10 } },
        ],
        location: { startLine: 1, startColumn: 0, stopLine: 1, stopColumn: 10 },
      },
      {
        id: 'block-2',
        paragraphName: null,
        statements: [],
        location: null,
      },
    ],
    edges: [
      { fromBlockId: 'block-1', toBlockId: 'block-2', kind: 'FallThrough', isRecursive: false },
    ],
    entryBlockId: 'block-1',
    exitBlockIds: ['block-2'],
    hasAlter: false,
    hasRecursion: false,
  };
}

describe('cfgAdapter', () => {
  it('cfgAdapter_mapsBlocksToNodes', () => {
    const data = toCfgData(buildCfg());
    expect(data.nodes).toHaveLength(2);
    expect(data.nodes[0].id).toBe('block-1');
    expect(data.nodes[0].label).toBe('PARA-A');
    expect(data.nodes[1].label).toBe('block-2');
  });

  it('cfgAdapter_mapsEdgesToLinks', () => {
    const data = toCfgData(buildCfg());
    expect(data.links).toHaveLength(1);
    expect(data.links[0].source).toBe('block-1');
    expect(data.links[0].target).toBe('block-2');
  });

  it('cfgAdapter_entryNodeFlagged', () => {
    const data = toCfgData(buildCfg());
    const entry = data.nodes.find(n => n.id === 'block-1')!;
    expect(entry.isEntry).toBe(true);
    expect(data.nodes.find(n => n.id === 'block-2')!.isEntry).toBe(false);
  });

  it('cfgAdapter_exitNodesFlagged', () => {
    const data = toCfgData(buildCfg());
    const exit = data.nodes.find(n => n.id === 'block-2')!;
    expect(exit.isExit).toBe(true);
    expect(data.nodes.find(n => n.id === 'block-1')!.isExit).toBe(false);
  });

  it('cfgAdapter_preservesStatementsForNavigation', () => {
    const cfg = buildCfg();
    const data = toCfgData(cfg);
    const node = data.nodes.find(n => n.id === 'block-1')!;
    expect(node.statements).toEqual(cfg.blocks[0].statements);
    expect(node.location).toEqual(cfg.blocks[0].location);
    expect(node.statementCount).toBe(1);
  });

  it('cfgAdapter_mapsRecursiveFlag', () => {
    const cfg: ControlFlowGraph = {
      ...buildCfg(),
      edges: [{ fromBlockId: 'block-1', toBlockId: 'block-1', kind: 'PerformCall', isRecursive: true }],
      hasRecursion: true,
    };
    const data = toCfgData(cfg);
    expect(data.links[0].isRecursive).toBe(true);
  });
});
