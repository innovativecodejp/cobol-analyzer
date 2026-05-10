import { describe, it, expect, beforeAll, afterEach } from 'vitest';
import { AstTree } from './AstTree';
import { selectionStore } from '../store/SelectionStore';
import type { AstNodeWithMeta } from '../adapters/astAdapter';

function buildAst(): AstNodeWithMeta {
  return {
    id: 'Program:1:0',
    nodeType: 'Program',
    category: 'Structure',
    location: { startLine: 1, startColumn: 0, stopLine: 10, stopColumn: 0 },
    collapsed: false,
    children: [
      {
        id: 'Division:2:0',
        nodeType: 'Division',
        category: 'Unit',
        location: { startLine: 2, startColumn: 0, stopLine: 8, stopColumn: 0 },
        collapsed: false,
        children: [
          {
            id: 'Statement:3:4',
            nodeType: 'Statement',
            category: 'Element',
            location: { startLine: 3, startColumn: 4, stopLine: 3, stopColumn: 20 },
            collapsed: true,
            children: [],
          },
        ],
      },
    ],
  };
}

beforeAll(() => {
  Object.defineProperty(SVGSVGElement.prototype, 'width', {
    configurable: true,
    get: () => ({ baseVal: { value: 600 } }),
  });
  Object.defineProperty(SVGSVGElement.prototype, 'height', {
    configurable: true,
    get: () => ({ baseVal: { value: 500 } }),
  });
});

afterEach(() => {
  selectionStore.clearAll();
  document.body.innerHTML = '';
});

describe('AstTree', () => {
  it('clickingNode_togglesCollapseAndInvokesHandler', () => {
    const container = document.createElement('div');
    Object.defineProperty(container, 'clientHeight', { value: 500, configurable: true });
    document.body.appendChild(container);

    const tree = new AstTree(container);
    const root = buildAst();
    let clickedNodeId: string | null = null;
    tree.setOnNodeClick(nodeId => {
      clickedNodeId = nodeId;
    });

    tree.render(root);
    const divisionNode = Array.from(container.querySelectorAll<SVGGElement>('g.node'))
      .find(node => node.textContent?.includes('Division'));
    expect(divisionNode).toBeDefined();

    divisionNode!.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(clickedNodeId).toBe('Division:2:0');
    expect(root.children[0].collapsed).toBe(true);
    expect(container.textContent).not.toContain('Statement');

    tree.clear();
  });
});
