import { describe, it, expect } from 'vitest';
import { toD3Hierarchy } from './astAdapter';
import type { AstNode } from '../types/analyzeResult';

function buildAst(): AstNode {
  return {
    id: 'Program:1:0',
    nodeType: 'Program',
    category: 'Structure',
    location: { startLine: 1, startColumn: 0, stopLine: 20, stopColumn: 0 },
    children: [
      {
        id: 'Division:2:0',
        nodeType: 'Division',
        category: 'Unit',
        location: { startLine: 2, startColumn: 0, stopLine: 10, stopColumn: 0 },
        children: [
          {
            id: 'Statement:3:4',
            nodeType: 'Statement',
            category: 'Element',
            location: { startLine: 3, startColumn: 4, stopLine: 3, stopColumn: 20 },
            children: [],
          },
        ],
      },
    ],
  };
}

describe('astAdapter', () => {
  it('astAdapter_elementNodesInitiallyCollapsed', () => {
    const root = toD3Hierarchy(buildAst());

    expect(root.collapsed).toBe(false);
    expect(root.category).toBe('Structure');
    expect(root.children).toHaveLength(1);

    const division = root.children[0];
    expect(division.collapsed).toBe(false);
    expect(division.category).toBe('Unit');
    expect(division.children).toHaveLength(1);

    const statement = division.children[0];
    expect(statement.collapsed).toBe(true);
    expect(statement.category).toBe('Element');
    expect(statement.children).toEqual([]);
  });
});
