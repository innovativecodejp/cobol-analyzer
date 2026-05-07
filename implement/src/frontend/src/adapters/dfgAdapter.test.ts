import { describe, it, expect } from 'vitest';
import { toDfgData } from './dfgAdapter';
import type { DataFlowGraph } from '../types/analyzeResult';

function buildDfg(): DataFlowGraph {
  return {
    programName: 'MYPROG',
    nodes: [
      { id: 'WS-A', name: 'WS-A', levelNumber: 5, picture: 'X(10)', isGroup: false },
      { id: 'WS-GRP', name: 'WS-GRP', levelNumber: 1, picture: null, isGroup: true },
    ],
    edges: [
      { fromId: 'WS-A', toId: 'WS-GRP', kind: 'GroupOf' },
    ],
    impactClosure: {},
  };
}

describe('dfgAdapter', () => {
  it('dfgAdapter_mapsNodesToD3Nodes', () => {
    const data = toDfgData(buildDfg());
    expect(data.nodes).toHaveLength(2);
    const nodeA = data.nodes.find(n => n.id === 'WS-A')!;
    expect(nodeA.name).toBe('WS-A');
    expect(nodeA.isGroup).toBe(false);
    expect(nodeA.levelNumber).toBe(5);
    const grp = data.nodes.find(n => n.id === 'WS-GRP')!;
    expect(grp.isGroup).toBe(true);
  });

  it('dfgAdapter_redefinesEdgeSetsFlag', () => {
    const dfg: DataFlowGraph = {
      ...buildDfg(),
      edges: [
        { fromId: 'WS-A', toId: 'WS-GRP', kind: 'Redefines' },
      ],
    };
    const data = toDfgData(dfg);
    const nodeA = data.nodes.find(n => n.id === 'WS-A')!;
    expect(nodeA.hasRedefines).toBe(true);
    const grp = data.nodes.find(n => n.id === 'WS-GRP')!;
    expect(grp.hasRedefines).toBe(false);
  });
});
