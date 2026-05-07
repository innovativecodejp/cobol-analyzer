import type { DataFlowGraph, DfgEdgeKind } from '../types/analyzeResult';

export interface D3DfgNode {
  id: string;
  name: string;
  levelNumber: number;
  isGroup: boolean;
  hasRedefines: boolean;
}

export interface D3DfgLink {
  source: string;
  target: string;
  kind: DfgEdgeKind;
}

export interface D3DfgData {
  nodes: D3DfgNode[];
  links: D3DfgLink[];
}

export function toDfgData(dfg: DataFlowGraph): D3DfgData {
  const redefinesFromIds = new Set(
    dfg.edges.filter(e => e.kind === 'Redefines').map(e => e.fromId),
  );
  const nodes: D3DfgNode[] = dfg.nodes.map(n => ({
    id: n.id,
    name: n.name,
    levelNumber: n.levelNumber,
    isGroup: n.isGroup,
    hasRedefines: redefinesFromIds.has(n.id),
  }));
  const links: D3DfgLink[] = dfg.edges.map(e => ({
    source: e.fromId,
    target: e.toId,
    kind: e.kind,
  }));
  return { nodes, links };
}
