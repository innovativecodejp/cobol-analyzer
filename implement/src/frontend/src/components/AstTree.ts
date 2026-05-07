import * as d3 from 'd3';
import type { AstNodeWithMeta } from '../adapters/astAdapter';

const NODE_COLORS: Record<string, string> = {
  Structure: '#1a4fa8',
  Unit: '#2e86c1',
  Element: '#808080',
};

export class AstTree {
  private readonly svg: d3.Selection<SVGSVGElement, unknown, null, undefined>;
  private readonly g: d3.Selection<SVGGElement, unknown, null, undefined>;
  private readonly container: HTMLElement;

  constructor(container: HTMLElement) {
    this.container = container;
    this.svg = d3.select(container).append('svg')
      .attr('width', '100%')
      .attr('height', '100%');
    this.g = this.svg.append('g');

    this.svg.call(
      d3.zoom<SVGSVGElement, unknown>().on('zoom', event => {
        this.g.attr('transform', event.transform);
      }),
    );
  }

  render(root: AstNodeWithMeta): void {
    this.g.selectAll('*').remove();

    const treeLayout = d3.tree<AstNodeWithMeta>().nodeSize([28, 160]);
    const pointRoot = treeLayout(
      d3.hierarchy<AstNodeWithMeta>(root, d => (d.collapsed ? null : d.children)),
    );

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
  }

  clear(): void {
    this.g.selectAll('*').remove();
  }

  getContainer(): HTMLElement {
    return this.container;
  }
}
