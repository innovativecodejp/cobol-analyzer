import type {
  ControlFlowGraph,
  CfgEdgeKind,
  CfgStatement,
  SourceLocation,
} from '../types/analyzeResult';

export interface D3Node {
  id: string;
  label: string;
  statementCount: number;
  statements: CfgStatement[];
  location: SourceLocation | null;
  isEntry: boolean;
  isExit: boolean;
}

export interface D3Link {
  source: string;
  target: string;
  kind: CfgEdgeKind;
  isRecursive: boolean;
}

export interface D3CfgData {
  nodes: D3Node[];
  links: D3Link[];
}

export function toCfgData(cfg: ControlFlowGraph): D3CfgData {
  const exitSet = new Set(cfg.exitBlockIds);
  const nodes: D3Node[] = cfg.blocks.map(b => ({
    id: b.id,
    label: b.paragraphName ?? b.id,
    statementCount: b.statements.length,
    statements: b.statements,
    location: b.location,
    isEntry: b.id === cfg.entryBlockId,
    isExit: exitSet.has(b.id),
  }));
  const links: D3Link[] = cfg.edges.map(e => ({
    source: e.fromBlockId,
    target: e.toBlockId,
    kind: e.kind,
    isRecursive: e.isRecursive,
  }));
  return { nodes, links };
}
