export type NodeCategory = 'Structure' | 'Unit' | 'Element';

export interface AstNode {
  id: string;
  nodeType: string;
  category: NodeCategory;
  location: SourceLocation;
  children: AstNode[];
}

export interface SourceLocation {
  startLine: number; startColumn: number;
  stopLine: number; stopColumn: number;
}

export type CfgEdgeKind =
  | 'FallThrough' | 'ConditionalTrue' | 'ConditionalFalse'
  | 'GoTo' | 'PerformCall' | 'PerformReturn'
  | 'PerformThruCall' | 'PerformThruReturn';

export type DfgEdgeKind = 'Define' | 'Use' | 'Redefines' | 'GroupOf';

export type MdiRisk = 'Low' | 'Medium' | 'High' | 'Critical';

export interface ControlFlowGraph {
  programName: string;
  blocks: CfgBlock[];
  edges: CfgEdge[];
  entryBlockId: string;
  exitBlockIds: string[];
  hasAlter: boolean;
  hasRecursion: boolean;
}

export interface CfgStatement {
  statementType: string;
  location: SourceLocation;
}

export interface CfgBlock {
  id: string;
  paragraphName: string | null;
  statements: CfgStatement[];
  location: SourceLocation | null;
}

export interface CfgEdge {
  fromBlockId: string;
  toBlockId: string;
  kind: CfgEdgeKind;
  isRecursive: boolean;
}

export interface DataFlowGraph {
  programName: string;
  nodes: DfgNode[];
  edges: DfgEdge[];
  impactClosure: Record<string, string[]>;
}

export interface DfgNode {
  id: string; name: string;
  levelNumber: number; picture: string | null;
  isGroup: boolean;
}

export interface DfgEdge {
  fromId: string; toId: string;
  kind: DfgEdgeKind;
}

export interface MetricsResult {
  programName: string;
  cyclomaticComplexity: number;
  ccPerParagraph: Record<string, number>;
  goToDensity: number;
  alterCount: number;
  maxNestingDepth: number;
  redefinesDensity: number;
  crossScopeDependencies: number;
  mdi: MdiScore;
}

export interface MdiScore {
  score: number;
  risk: MdiRisk;
  weightedContributions: Record<string, number>;
}

export interface ParseError {
  line: number; column: number; message: string;
}

export interface AnalyzeResult {
  ast: AstNode | null;
  cfg: ControlFlowGraph | null;
  dfg: DataFlowGraph | null;
  metrics: MetricsResult | null;
  errors: ParseError[];
}
