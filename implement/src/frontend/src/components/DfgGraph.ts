import * as d3 from 'd3';
import type { D3DfgNode, D3DfgLink, D3DfgData } from '../adapters/dfgAdapter';
import { selectionStore, type SelectionState } from '../store/SelectionStore';

const EDGE_COLOR: Record<string, string> = {
  Define: '#e74c3c',
  Use: '#2980b9',
  Redefines: '#e67e22',
  GroupOf: '#808080',
};

const EDGE_DASH: Record<string, string> = {
  Define: 'none',
  Use: 'none',
  Redefines: '6,3',
  GroupOf: 'none',
};

const ARROW_KINDS = new Set(['Define', 'Use']);

type SimNode = D3DfgNode & d3.SimulationNodeDatum;
type SimLink = Omit<D3DfgLink, 'source' | 'target'> & d3.SimulationLinkDatum<SimNode>;

export class DfgGraph {
  private readonly svg: d3.Selection<SVGSVGElement, unknown, null, undefined>;
  private readonly g: d3.Selection<SVGGElement, unknown, null, undefined>;
  private readonly container: HTMLElement;
  private unsub: (() => void) | null = null;
  private onNodeClick?: (nodeId: string) => void;
  private onBackgroundClick?: () => void;

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

    this.svg.on('click', () => {
      this.onBackgroundClick?.();
    });

    this.unsub = selectionStore.on(state => this.applySelection(state));
  }

  setOnNodeClick(handler: (nodeId: string) => void): void {
    this.onNodeClick = handler;
  }

  setOnBackgroundClick(handler: () => void): void {
    this.onBackgroundClick = handler;
  }

  private applySelection(state: SelectionState): void {
    const hasSelection = state.selectedDfgNodeId !== null;
    this.g.selectAll<SVGGElement, SimNode>('g.node')
      .classed('selected', d => d.id === state.selectedDfgNodeId)
      .classed('impact', d => state.impactClosureIds.has(d.id))
      .classed('dimmed', d =>
        hasSelection && d.id !== state.selectedDfgNodeId && !state.impactClosureIds.has(d.id),
      );
  }

  render(data: D3DfgData): void {
    this.g.selectAll('*').remove();
    this.svg.selectAll('defs').remove();

    const W = this.container.clientWidth || 600;
    const H = this.container.clientHeight || 400;

    const defs = this.svg.append('defs');
    for (const kind of ['define', 'use']) {
      const color = kind === 'define' ? '#e74c3c' : '#2980b9';
      defs.append('marker')
        .attr('id', `dfg-arrow-${kind}`)
        .attr('viewBox', '0 -5 10 10')
        .attr('refX', 14).attr('refY', 0)
        .attr('markerWidth', 6).attr('markerHeight', 6)
        .attr('orient', 'auto')
        .append('path').attr('d', 'M0,-5L10,0L0,5').attr('fill', color);
    }

    const nodes: SimNode[] = data.nodes.map(n => ({ ...n }));
    const nodeById = new Map(nodes.map(n => [n.id, n]));
    const links: SimLink[] = data.links.map(l => ({
      ...l,
      source: nodeById.get(l.source) ?? l.source,
      target: nodeById.get(l.target) ?? l.target,
    }));

    const simulation = d3.forceSimulation<SimNode>(nodes)
      .force('link', d3.forceLink<SimNode, SimLink>(links).id(d => d.id).distance(80))
      .force('charge', d3.forceManyBody<SimNode>().strength(-200))
      .force('center', d3.forceCenter(W / 2, H / 2));

    const linkSel = this.g.selectAll<SVGLineElement, SimLink>('line.link')
      .data(links)
      .join('line')
      .attr('class', 'link')
      .attr('stroke', d => EDGE_COLOR[d.kind] ?? '#808080')
      .attr('stroke-dasharray', d => EDGE_DASH[d.kind] ?? 'none')
      .attr('stroke-width', 1.5)
      .attr('marker-end', d =>
        ARROW_KINDS.has(d.kind) ? `url(#dfg-arrow-${d.kind.toLowerCase()})` : 'none',
      );

    const nodeDrag = d3.drag<SVGGElement, SimNode>()
      .on('start', (event, d) => {
        if (!event.active) simulation.alphaTarget(0.3).restart();
        d.fx = d.x ?? null;
        d.fy = d.y ?? null;
      })
      .on('drag', (event, d) => {
        d.fx = event.x;
        d.fy = event.y;
      })
      .on('end', (event, d) => {
        if (!event.active) simulation.alphaTarget(0);
        d.fx = null;
        d.fy = null;
      });

    const nodeGroup = this.g.selectAll<SVGGElement, SimNode>('g.node')
      .data(nodes)
      .join('g')
      .attr('class', 'node')
      .call(nodeDrag)
      .on('click', (event, d) => {
        event.stopPropagation();
        this.onNodeClick?.(d.id);
      });

    nodeGroup.append('circle')
      .attr('r', d => d.isGroup ? 14 : 8)
      .attr('fill', '#2e86c1')
      .attr('stroke', d => d.hasRedefines ? '#e74c3c' : '#aaa')
      .attr('stroke-width', d => d.hasRedefines ? 2 : 1);

    nodeGroup.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', d => (d.isGroup ? 14 : 8) + 13)
      .attr('font-size', '10px')
      .attr('fill', '#333')
      .text(d => d.name);

    simulation.on('tick', () => {
      linkSel
        .attr('x1', d => (d.source as SimNode).x ?? 0)
        .attr('y1', d => (d.source as SimNode).y ?? 0)
        .attr('x2', d => (d.target as SimNode).x ?? 0)
        .attr('y2', d => (d.target as SimNode).y ?? 0);

      nodeGroup.attr('transform', d => `translate(${d.x ?? 0},${d.y ?? 0})`);
    });

    this.applySelection(selectionStore.getState());
  }

  clear(): void {
    this.unsub?.();
    this.unsub = null;
    this.g.selectAll('*').remove();
  }
}
