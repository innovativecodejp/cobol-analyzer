import type { AnalyzeResult, MdiScore, SourceLocation } from './analyzeResult';

export interface CobolSource {
  fileName: string;
  source: string;
}

export interface DependencyNode {
  programName: string;
  fileName: string | null;
  mdi: MdiScore | null;
  isExternal: boolean;
  fanIn: number;
  fanOut: number;
}

export interface DependencyEdge {
  callerProgram: string;
  calleeProgram: string;
  callSites: SourceLocation[];
}

export interface ProgramDependencyGraph {
  nodes: DependencyNode[];
  edges: DependencyEdge[];
  hasCycle: boolean;
  hasDynamicCall: boolean;
}

export type MigrationStrategy =
  | 'BigBang'
  | 'Incremental'
  | 'StranglerFig'
  | 'NeedsStudy';

export interface MigrationRankingEntry {
  rank: number;
  programName: string;
  fileName: string;
  mdi: MdiScore;
  lineCount: number;
  paragraphCount: number;
  fanIn: number;
  fanOut: number;
  strategy: MigrationStrategy;
}

export interface ProjectAnalyzeResult {
  programs: AnalyzeResult[];
  dependencyGraph: ProgramDependencyGraph;
  ranking: { entries: MigrationRankingEntry[] };
  errors: string[];
}

export interface ExportReportRequest {
  fileName: string;
  source: string;
}

export interface ExportDesignRequest {
  sources: CobolSource[];
}
