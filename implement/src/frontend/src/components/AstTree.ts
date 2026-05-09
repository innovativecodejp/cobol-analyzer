import * as d3 from 'd3';
import type { AstNodeWithMeta } from '../adapters/astAdapter';
import type { SourceLocation } from '../types/analyzeResult';
import { selectionStore, type SelectionState } from '../store/SelectionStore';

const NODE_COLORS: Record<string, string> = {
  Structure: '#1a4fa8',
  Unit: '#2e86c1',
  Element: '#808080',
};

export class AstTree {
  private readonly svg: d3.Selection<SVGSVGElement, unknown, null, undefined>;
  private readonly g: d3.Selection<SVGGElement, unknown, null, undefined>;
  private readonly container: HTMLElement;
  private readonly zoom: d3.ZoomBehavior<SVGSVGElement, unknown>;
  private initialized = false;
  private unsub: (() => void) | null = null;
  private onNodeClick?: (nodeId: string, location: SourceLocation) => void;

  constructor(container: HTMLElement) {
    this.container = container;
    this.svg = d3.select(container).append('svg')
      .attr('width', '100%')
      .attr('height', '100%');
    this.g = this.svg.append('g');

    this.zoom = d3.zoom<SVGSVGElement, unknown>().on('zoom', event => {
      this.g.attr('transform', event.transform);
    });
    this.svg.call(this.zoom);

    this.unsub = selectionStore.on(state => this.applySelection(state));
  }

  setOnNodeClick(handler: (nodeId: string, location: SourceLocation) => void): void {
    this.onNodeClick = handler;
  }

  private applySelection(state: SelectionState): void {
    this.g.selectAll<SVGGElement, d3.HierarchyPointNode<AstNodeWithMeta>>('g.node')
      .classed('selected', d => d.data.id === state.selectedAstNodeId)
      .classed('dimmed', d => state.selectedAstNodeId !== null && d.data.id !== state.selectedAstNodeId);
  }

  render(root: AstNodeWithMeta): void {
    this.g.selectAll('*').remove();

    const treeLayout = d3.tree<AstNodeWithMeta>().nodeSize([28, 160]);
    const pointRoot = treeLayout(
      d3.hierarchy<AstNodeWithMeta>(root, d => (d.collapsed ? null : d.children)),
    );

    if (!this.initialized) {
      this.initialized = true;
      const nodes = pointRoot.descendants();
      const minX = d3.min(nodes, d => d.x) ?? 0;
      const maxX = d3.max(nodes, d => d.x) ?? 0;
      const h = this.container.clientHeight || 500;
      const initTransform = d3.zoomIdentity.translate(
        40,
        h / 2 - (minX + maxX) / 2,
      );
      this.svg.call(this.zoom.transform, initTransform);
    }

    const linkGen = d3.linkHorizontal<
      d3.HierarchyPointLink<AstNodeWithMeta>,
      d3.HierarchyPointNode<AstNodeWithMeta>
    >()
      .x(n => n.y)
      .y(n => n.x);

    this.g.selectAll<SVGPathElement, d3.HierarchyPointLink<AstNodeWithMeta>>('path.link')
      .data(pointRoot.links())
      .join('path')
      .attr('class', 'link')
      .attr('fill', 'none')
      .attr('stroke', '#ccc')
      .attr('d', linkGen);

    const nodeGroup = this.g
      .selectAll<SVGGElement, d3.HierarchyPointNode<AstNodeWithMeta>>('g.node')
      .data(pointRoot.descendants())
      .join('g')
      .attr('class', 'node')
      .attr('transform', d => `translate(${d.y},${d.x})`)
      .style('cursor', 'pointer')
      .on('click', (_event, d) => {
        this.onNodeClick?.(d.data.id, d.data.location);
      })
      .on('dblclick', (_event, d) => {
        d.data.collapsed = !d.data.collapsed;
        this.render(root);
      });

    nodeGroup.append('circle')
      .attr('r', 6)
      .attr('fill', d => NODE_COLORS[d.data.category] ?? '#808080');

    nodeGroup.append('text')
      .attr('dx', 10)
      .attr('dy', 4)
      .attr('font-size', '11px')
      .attr('fill', '#333')
      .text(d => d.data.nodeType);

    this.applySelection(selectionStore.getState());
  }

  clear(): void {
    this.unsub?.();
    this.unsub = null;
    this.g.selectAll('*').remove();
  }

  getContainer(): HTMLElement {
    return this.container;
  }
}
