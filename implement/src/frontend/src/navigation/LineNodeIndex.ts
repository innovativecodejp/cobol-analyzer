import type { AstNode } from '../types/analyzeResult';

export interface LineEntry {
  nodeId: string;
  startLine: number;
  stopLine: number;
  depth: number;
}

export class LineNodeIndex {
  private readonly map = new Map<number, LineEntry>();

  constructor(root: AstNode) {
    this.walk(root, 0);
  }

  private walk(node: AstNode, depth: number): void {
    const line = node.location.startLine;
    const existing = this.map.get(line);
    // Higher depth = more specific child node takes priority for same start line
    if (!existing || depth > existing.depth) {
      this.map.set(line, {
        nodeId: node.id,
        startLine: node.location.startLine,
        stopLine: node.location.stopLine,
        depth,
      });
    }
    for (const child of node.children) {
      this.walk(child, depth + 1);
    }
  }

  lookup(line: number): LineEntry | undefined {
    return this.map.get(line);
  }
}
