import * as d3 from 'd3';
import type { D3Node, D3Link, D3CfgData } from '../adapters/cfgAdapter';
import type { SourceLocation } from '../types/analyzeResult';
import { selectionStore, type SelectionState } from '../store/SelectionStore';

const EDGE_COLOR: Record<string, string> = {
  FallThrough: '#808080',
  ConditionalTrue: '#27ae60',
  ConditionalFalse: '#e74c3c',
  GoTo: '#8e44ad',
  PerformCall: '#2980b9',
  PerformReturn: '#2980b9',
  PerformThruCall: '#2980b9',
  PerformThruReturn: '#2980b9',
};

const EDGE_DASH: Record<string, string> = {
  FallThrough: 'none',
  ConditionalTrue: 'none',
  ConditionalFalse: 'none',
  GoTo: '6,3',
  PerformCall: 'none',
  PerformReturn: '2,3',
  PerformThruCall: '6,3',
  PerformThruReturn: '2,3',
};

const NAVIGATE_TYPES = new Set(['GOTO', 'PERFORM_THRU', 'PERFORM', 'PERFORM_LOOP']);

const MAX_BLOCKS = 200;

type SimNode = D3Node & d3.SimulationNodeDatum;
type SimLink = Omit<D3Link, 'source' | 'target'> & d3.SimulationLinkDatum<SimNode>;

export class CfgGraph {
  private readonly svg: d3.Selection<SVGSVGElement, unknown, null, undefined>;
  private readonly g: d3.Selection<SVGGElement, unknown, null, undefined>;
  private readonly container: HTMLElement;
  private unsub: (() => void) | null = null;
  private onNodeClick?: (blockId: string, location: SourceLocation | null) => void;
  private onStatementClick?: (blockId: string, statementType: string) => void;
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

  setOnNodeClick(handler: (blockId: string, location: SourceLocation | null) => void): void {
    this.onNodeClick = handler;
  }

  setOnStatementClick(handler: (blockId: string, statementType: string) => void): void {
    this.onStatementClick = handler;
  }

  setOnBackgroundClick(handler: () => void): void {
    this.onBackgroundClick = handler;
  }

  private applySelection(state: SelectionState): void {
    this.g.selectAll<SVGGElement, SimNode>('g.node')
      .classed('selected', d => d.id === state.selectedCfgBlockId)
      .classed('dimmed', d => state.selectedCfgBlockId !== null && d.id !== state.selectedCfgBlockId);
  }

  render(data: D3CfgData): void {
    this.g.selectAll('*').remove();
    this.svg.selectAll('defs').remove();

    if (data.nodes.length > MAX_BLOCKS) {
      this.g.append('text')
        .attr('x', 20).attr('y', 40)
        .attr('font-size', '14px').attr('fill', '#e74c3c')
        .text(`ノード数が多すぎるため表示を省略しています（${data.nodes.length} ブロック）`);
      return;
    }

    const W = this.container.clientWidth || 600;
    const H = this.container.clientHeight || 400;

    const defs = this.svg.append('defs');
    defs.append('marker')
      .attr('id', 'cfg-arrow')
      .attr('viewBox', '0 -5 10 10')
      .attr('refX', 10).attr('refY', 0)
      .attr('markerWidth', 6).attr('markerHeight', 6)
      .attr('orient', 'auto')
      .append('path').attr('d', 'M0,-5L10,0L0,5').attr('fill', '#555');

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
      .attr('marker-end', 'url(#cfg-arrow)');

    const linkLabel = this.g.selectAll<SVGTextElement, SimLink>('text.link-label')
      .data(links)
      .join('text')
      .attr('class', 'link-label')
      .attr('font-size', '9px')
      .attr('fill', d => EDGE_COLOR[d.kind] ?? '#808080')
      .text(d => d.kind.toLowerCase());

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
        this.onNodeClick?.(d.id, d.location);
      });

    nodeGroup.append('rect')
      .attr('width', 120).attr('height', 40)
      .attr('x', -60).attr('y', -20)
      .attr('rx', 6).attr('ry', 6)
      .attr('fill', d => d.isEntry ? '#27ae60' : d.isExit ? '#e67e22' : '#2e86c1')
      .attr('stroke', '#aaa').attr('stroke-width', 1);

    nodeGroup.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', '0.3em')
      .attr('font-size', '11px')
      .attr('fill', 'white')
      .text(d => d.label);

    nodeGroup.append('text')
      .attr('text-anchor', 'end')
      .attr('x', 55).attr('y', 16)
      .attr('font-size', '9px')
      .attr('fill', 'rgba(255,255,255,0.8)')
      .text(d => String(d.statementCount));

    // N3: nav labels rendered in a top-level layer so they are never obscured by node rects
    type NavLabelDatum = { block: SimNode; statementType: string; idx: number };
    const navLabelData: NavLabelDatum[] = [];
    nodes.forEach(d => {
      d.statements
        .filter(s => NAVIGATE_TYPES.has(s.statementType))
        .forEach((s, idx) => navLabelData.push({ block: d, statementType: s.statementType, idx }));
    });

    const navLayer = this.g.append('g');
    const navLabels = navLayer
      .selectAll<SVGTextElement, NavLabelDatum>('text')
      .data(navLabelData)
      .join('text')
      .attr('text-anchor', 'middle')
      .attr('font-size', '9px')
      .attr('fill', '#8e44ad')
      .style('cursor', 'pointer')
      .text(d => `→ ${d.statementType}`)
      .on('click', (event, d) => {
        event.stopPropagation();
        this.onStatementClick?.(d.block.id, d.statementType);
      });

    simulation.on('tick', () => {
      linkSel
        .attr('x1', d => (d.source as SimNode).x ?? 0)
        .attr('y1', d => (d.source as SimNode).y ?? 0)
        .attr('x2', d => (d.target as SimNode).x ?? 0)
        .attr('y2', d => (d.target as SimNode).y ?? 0);

      linkLabel
        .attr('x', d => (((d.source as SimNode).x ?? 0) + ((d.target as SimNode).x ?? 0)) / 2)
        .attr('y', d => (((d.source as SimNode).y ?? 0) + ((d.target as SimNode).y ?? 0)) / 2);

      nodeGroup.attr('transform', d => `translate(${d.x ?? 0},${d.y ?? 0})`);

      navLabels.attr('x', d => d.block.x ?? 0)
        .attr('y', d => (d.block.y ?? 0) + 28 + d.idx * 12);
    });

    this.applySelection(selectionStore.getState());
  }

  clear(): void {
    this.unsub?.();
    this.unsub = null;
    this.g.selectAll('*').remove();
  }
}
