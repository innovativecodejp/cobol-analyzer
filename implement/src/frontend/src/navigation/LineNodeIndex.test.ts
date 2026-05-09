import { describe, it, expect } from 'vitest';
import { LineNodeIndex } from './LineNodeIndex';
import type { AstNode } from '../types/analyzeResult';

function makeNode(id: string, startLine: number, stopLine: number, children: AstNode[] = []): AstNode {
  return {
    id,
    nodeType: 'Test',
    category: 'Structure',
    location: { startLine, startColumn: 0, stopLine, stopColumn: 0 },
    children,
  };
}

describe('LineNodeIndex', () => {
  it('lookup_returnsRootNodeForItsStartLine', () => {
    const root = makeNode('Program:1:0', 1, 10);
    const index = new LineNodeIndex(root);
    const entry = index.lookup(1);
    expect(entry?.nodeId).toBe('Program:1:0');
    expect(entry?.startLine).toBe(1);
    expect(entry?.stopLine).toBe(10);
  });

  it('lookup_prefersDeepestNodeOnSameLine', () => {
    const child = makeNode('Statement:1:4', 1, 1);
    const root = makeNode('Division:1:0', 1, 10, [child]);
    const index = new LineNodeIndex(root);
    const entry = index.lookup(1);
    expect(entry?.nodeId).toBe('Statement:1:4');
  });

  it('lookup_returnsUndefinedForUnindexedLine', () => {
    const root = makeNode('Program:1:0', 1, 10);
    const index = new LineNodeIndex(root);
    expect(index.lookup(99)).toBeUndefined();
  });
});
