import { describe, it, expect } from 'vitest';
import { MdiPanel } from './MdiPanel';
import type { MetricsResult } from '../types/analyzeResult';

function buildMetrics(risk: 'Low' | 'Medium' | 'High' | 'Critical'): MetricsResult {
  return {
    programName: 'MYPROG',
    cyclomaticComplexity: 1,
    ccPerParagraph: {},
    goToDensity: 0,
    alterCount: 0,
    maxNestingDepth: 1,
    redefinesDensity: 0,
    crossScopeDependencies: 0,
    mdi: {
      score: 5.0,
      risk,
      weightedContributions: { CC: 5.0, GD: 0, AD: 0, ND: 0, RD: 0, CS: 0 },
    },
  };
}

describe('MdiPanel', () => {
  it('mdiPanel_lowRisk_greenBadge', () => {
    const container = document.createElement('div');
    const panel = new MdiPanel(container);
    panel.render(buildMetrics('Low'));
    const badge = container.querySelector('.mdi-badge') as HTMLElement;
    // jsdom normalizes hex to rgb: #27ae60 = rgb(39, 174, 96)
    expect(badge.style.backgroundColor).toBe('rgb(39, 174, 96)');
  });

  it('mdiPanel_criticalRisk_redBadge', () => {
    const container = document.createElement('div');
    const panel = new MdiPanel(container);
    panel.render(buildMetrics('Critical'));
    const badge = container.querySelector('.mdi-badge') as HTMLElement;
    // jsdom normalizes hex to rgb: #e74c3c = rgb(231, 76, 60)
    expect(badge.style.backgroundColor).toBe('rgb(231, 76, 60)');
  });
});
