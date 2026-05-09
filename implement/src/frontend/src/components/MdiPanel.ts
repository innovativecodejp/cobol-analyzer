import type { MetricsResult, MdiRisk } from '../types/analyzeResult';

const RISK_COLORS: Record<MdiRisk, string> = {
  Low: '#27ae60',
  Medium: '#f39c12',
  High: '#e67e22',
  Critical: '#e74c3c',
};

const METRICS_ORDER = ['CC', 'GD', 'AD', 'ND', 'RD', 'CS'];

function getRawValue(metrics: MetricsResult, key: string): number {
  const map: Record<string, number> = {
    CC: metrics.cyclomaticComplexity,
    GD: metrics.goToDensity,
    AD: metrics.alterCount,
    ND: metrics.maxNestingDepth,
    RD: metrics.redefinesDensity,
    CS: metrics.crossScopeDependencies,
  };
  return map[key] ?? 0;
}

export class MdiPanel {
  private readonly container: HTMLElement;

  constructor(container: HTMLElement) {
    this.container = container;
  }

  render(metrics: MetricsResult): void {
    this.container.innerHTML = '';

    const risk = metrics.mdi.risk;

    const score = document.createElement('span');
    score.className = 'mdi-score';
    score.textContent = `Score: ${metrics.mdi.score.toFixed(1)}`;

    const badge = document.createElement('span');
    badge.className = 'mdi-badge';
    badge.textContent = `Risk: ${risk}`;
    badge.style.backgroundColor = RISK_COLORS[risk];

    const bars = document.createElement('div');
    bars.className = 'mdi-bars';

    const contributions = metrics.mdi.weightedContributions;
    const maxContrib = Math.max(1, ...METRICS_ORDER.map(k => contributions[k] ?? 0));

    for (const key of METRICS_ORDER) {
      const contrib = contributions[key] ?? 0;
      const raw = getRawValue(metrics, key);
      const barHeight = Math.round((contrib / maxContrib) * 40);

      const item = document.createElement('div');
      item.className = 'mdi-bar-item';

      const fill = document.createElement('div');
      fill.className = 'mdi-bar-fill';
      fill.style.height = `${barHeight}px`;

      const label = document.createElement('span');
      const rawDisplay = Math.round(raw * 1000) / 1000;
      label.textContent = `${key}: ${rawDisplay}`;

      item.appendChild(fill);
      item.appendChild(label);
      bars.appendChild(item);
    }

    this.container.appendChild(score);
    this.container.appendChild(badge);
    this.container.appendChild(bars);
  }
}
