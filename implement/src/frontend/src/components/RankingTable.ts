import type { MdiRisk } from '../types/analyzeResult';
import type { MigrationRankingEntry, MigrationStrategy } from '../types/projectTypes';

const RISK_COLORS: Record<MdiRisk, string> = {
  Low: '#27ae60',
  Medium: '#f39c12',
  High: '#e67e22',
  Critical: '#e74c3c',
};

const STRATEGY_DESCRIPTIONS: Record<MigrationStrategy, string> = {
  BigBang: 'MDI スコアが低く、プログラム間依存も少ないため、ビッグバン移行が実現可能です。',
  Incremental: '中程度の複雑性または依存関係を持つため、段階的な移行を推奨します。',
  StranglerFig: '高い複雑性または多くのプログラム間依存が存在します。Strangler Fig パターンによる段階的置換が適切です。',
  NeedsStudy: 'MDI スコアが Critical レベルです。移行前に詳細な調査が必要です。',
};

export class RankingTable {
  constructor(private readonly container: HTMLElement) {}

  render(entries: MigrationRankingEntry[]): void {
    this.container.innerHTML = '';

    if (entries.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'project-empty';
      empty.textContent = 'ランキングはありません。';
      this.container.appendChild(empty);
      return;
    }

    const table = document.createElement('table');
    table.className = 'ranking-table';

    const header = table.createTHead();
    const headerRow = header.insertRow();
    for (const label of ['順位', 'プログラム', 'MDI', 'リスク', 'FanIn', 'FanOut', '推奨戦略']) {
      const th = document.createElement('th');
      th.textContent = label;
      headerRow.appendChild(th);
    }

    const body = table.createTBody();
    for (const entry of entries) {
      const row = body.insertRow();
      this.addText(row, String(entry.rank));
      this.addText(row, entry.programName);
      this.addText(row, entry.mdi.score.toFixed(1));
      this.addRisk(row, entry.mdi.risk);
      this.addText(row, String(entry.fanIn));
      this.addText(row, String(entry.fanOut));
      this.addStrategy(row, entry.strategy);
    }

    this.container.appendChild(table);
  }

  private addText(row: HTMLTableRowElement, text: string): void {
    const cell = row.insertCell();
    cell.textContent = text;
  }

  private addRisk(row: HTMLTableRowElement, risk: MdiRisk): void {
    const cell = row.insertCell();
    const badge = document.createElement('span');
    badge.className = 'risk-badge';
    badge.textContent = risk;
    badge.style.backgroundColor = RISK_COLORS[risk];
    cell.appendChild(badge);
  }

  private addStrategy(row: HTMLTableRowElement, strategy: MigrationStrategy): void {
    const cell = row.insertCell();
    const span = document.createElement('span');
    span.className = 'strategy-label';
    span.textContent = strategy;
    span.title = STRATEGY_DESCRIPTIONS[strategy];
    cell.appendChild(span);
  }
}
