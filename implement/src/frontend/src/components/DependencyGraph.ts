import * as d3 from 'd3';
import type { DependencyEdge, DependencyNode, ProgramDependencyGraph } from '../types/projectTypes';

type SimNode = DependencyNode & d3.SimulationNodeDatum;
type SimLink = Omit<DependencyEdge, 'callerProgram' | 'calleeProgram'> & d3.SimulationLinkDatum<SimNode>;

function nodeColor(node: DependencyNode): string {
  if (node.isExternal || node.mdi === null) return '#808080';
  switch (node.mdi.risk) {
    case 'Critical': return '#e74c3c';
    case 'High': return '#e67e22';
    case 'Medium': return '#f39c12';
    case 'Low': return '#27ae60';
  }
}

export class DependencyGraph {
  private readonly svg: d3.Selection<SVGSVGElement, unknown, null, undefined>;
  private readonly g: d3.Selection<SVGGElement, unknown, null, undefined>;

  constructor(private readonly container: HTMLElement) {
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

  render(graph: ProgramDependencyGraph): void {
    this.g.selectAll('*').remove();
    this.svg.selectAll('defs').remove();

    if (graph.nodes.length === 0) {
      this.g.append('text')
        .attr('x', 20)
        .attr('y', 36)
        .attr('fill', '#555')
        .text('依存グラフはありません。');
      return;
    }

    const width = this.container.clientWidth || 640;
    const height = this.container.clientHeight || 420;

    const defs = this.svg.append('defs');
    defs.append('marker')
      .attr('id', 'project-call-arrow')
      .attr('viewBox', '0 -5 10 10')
      .attr('refX', 24)
      .attr('refY', 0)
      .attr('markerWidth', 6)
      .attr('markerHeight', 6)
      .attr('orient', 'auto')
      .append('path')
      .attr('d', 'M0,-5L10,0L0,5')
      .attr('fill', '#555');

    if (graph.hasCycle || graph.hasDynamicCall) {
      const warning = this.g.append('text')
        .attr('x', 16)
        .attr('y', 24)
        .attr('fill', '#e67e22')
        .attr('font-size', '12px');
      warning.text([
        graph.hasCycle ? '循環依存あり' : '',
        graph.hasDynamicCall ? '動的CALLあり' : '',
      ].filter(Boolean).join(' / '));
    }

    const nodes: SimNode[] = graph.nodes.map(n => ({ ...n }));
    const nodeById = new Map(nodes.map(n => [n.programName, n]));
    const links: SimLink[] = graph.edges.map(edge => ({
      callSites: edge.callSites,
      source: nodeById.get(edge.callerProgram) ?? edge.callerProgram,
      target: nodeById.get(edge.calleeProgram) ?? edge.calleeProgram,
    }));

    const simulation = d3.forceSimulation<SimNode>(nodes)
      .force('link', d3.forceLink<SimNode, SimLink>(links).id(d => d.programName).distance(140))
      .force('charge', d3.forceManyBody<SimNode>().strength(-420))
      .force('center', d3.forceCenter(width / 2, height / 2));

    const linkSel = this.g.selectAll<SVGLineElement, SimLink>('line.project-link')
      .data(links)
      .join('line')
      .attr('class', 'project-link')
      .attr('stroke', '#777')
      .attr('stroke-width', 1.4)
      .attr('marker-end', 'url(#project-call-arrow)');

    const linkLabel = this.g.selectAll<SVGTextElement, SimLink>('text.project-link-label')
      .data(links)
      .join('text')
      .attr('class', 'project-link-label')
      .attr('font-size', '10px')
      .attr('fill', '#555')
      .text(d => String(d.callSites.length));

    const drag = d3.drag<SVGGElement, SimNode>()
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

    const nodeGroup = this.g.selectAll<SVGGElement, SimNode>('g.project-node')
      .data(nodes)
      .join('g')
      .attr('class', 'project-node')
      .call(drag);

    nodeGroup.append('circle')
      .attr('r', 22)
      .attr('fill', nodeColor)
      .attr('stroke', '#333')
      .attr('stroke-width', 1);

    nodeGroup.append('text')
      .attr('text-anchor', 'middle')
      .attr('y', 36)
      .attr('font-size', '11px')
      .attr('fill', '#222')
      .text(d => d.programName);

    nodeGroup.append('text')
      .attr('text-anchor', 'middle')
      .attr('dy', '0.35em')
      .attr('font-size', '11px')
      .attr('fill', '#fff')
      .text(d => d.isExternal || d.mdi === null ? '?' : d.mdi.score.toFixed(0));

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
    });
  }

  clear(): void {
    this.g.selectAll('*').remove();
  }
}
