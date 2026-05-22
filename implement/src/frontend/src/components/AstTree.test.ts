import { describe, it, expect, beforeAll, afterEach, vi } from 'vitest';
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
  vi.useRealTimers();
  selectionStore.clearAll();
  document.body.innerHTML = '';
});

describe('AstTree', () => {
  it('astTree_singleClickCallsOnNodeClick', () => {
    vi.useFakeTimers();
    const container = document.createElement('div');
    Object.defineProperty(container, 'clientHeight', { value: 500, configurable: true });
    document.body.appendChild(container);

    const tree = new AstTree(container);
    const root = buildAst();
    const clickedNodeIds: string[] = [];
    tree.setOnNodeClick(nodeId => {
      clickedNodeIds.push(nodeId);
    });

    tree.render(root);
    const divisionNode = Array.from(container.querySelectorAll<SVGGElement>('g.node'))
      .find(node => node.textContent?.includes('Division'));
    expect(divisionNode).toBeDefined();

    divisionNode!.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(clickedNodeIds).toEqual([]);
    vi.advanceTimersByTime(250);
    expect(clickedNodeIds).toEqual(['Division:2:0']);
    expect(root.children[0].collapsed).toBe(false);
    expect(container.textContent).toContain('Statement');

    tree.clear();
  });

  it('astTree_doubleClickDoesNotCallOnNodeClick', () => {
    vi.useFakeTimers();
    const container = document.createElement('div');
    Object.defineProperty(container, 'clientHeight', { value: 500, configurable: true });
    document.body.appendChild(container);

    const tree = new AstTree(container);
    const root = buildAst();
    const clickedNodeIds: string[] = [];
    tree.setOnNodeClick(nodeId => {
      clickedNodeIds.push(nodeId);
    });

    tree.render(root);
    const divisionNode = Array.from(container.querySelectorAll<SVGGElement>('g.node'))
      .find(node => node.textContent?.includes('Division'));
    expect(divisionNode).toBeDefined();

    divisionNode!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    divisionNode!.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    vi.advanceTimersByTime(250);

    expect(clickedNodeIds).toEqual([]);
    expect(root.children[0].collapsed).toBe(true);

    tree.clear();
  });

  it('astTree_doubleClickTogglesCollapsed', () => {
    const container = document.createElement('div');
    Object.defineProperty(container, 'clientHeight', { value: 500, configurable: true });
    document.body.appendChild(container);

    const tree = new AstTree(container);
    const root = buildAst();

    tree.render(root);
    const renderedNodeCountBefore = container.querySelectorAll('g.node').length;
    const divisionNode = Array.from(container.querySelectorAll<SVGGElement>('g.node'))
      .find(node => node.textContent?.includes('Division'));
    expect(divisionNode).toBeDefined();

    divisionNode!.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));

    const renderedNodeCountAfter = container.querySelectorAll('g.node').length;
    expect(root.children[0].collapsed).toBe(true);
    expect(root.children[0].children).toHaveLength(1);
    expect(renderedNodeCountAfter).toBeLessThan(renderedNodeCountBefore);
    expect(container.textContent).not.toContain('Statement');

    tree.clear();
  });
});
