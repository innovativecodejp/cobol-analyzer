import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  __resetManifestCache,
  dataUrl,
  loadAnnotationReport,
  loadManifest,
  loadProgramResult,
  loadProgramSource,
  loadProject,
} from './staticData';

const MANIFEST = {
  corpus: {
    name: 'carddemo',
    description: 'AWS CardDemo',
    license: 'Apache-2.0',
    sourceUrl: 'https://example.com/carddemo',
    pinnedCommit: '59cc6c2fd7ebd7ef7925cad552a01a4b8b6e4d5e',
  },
  selection: { topN: 8, count: 9, totalPrograms: 31 },
  programs: [
    {
      rank: 1,
      programName: 'COACTUPC',
      fileName: 'COACTUPC.cbl',
      mdi: 24.3,
      risk: 'Low',
      strategy: 'BigBang',
      fanIn: 0,
      fanOut: 1,
      source: 'sources/COACTUPC.cbl',
      result: 'programs/COACTUPC.json',
      annotationReport: 'reports/COACTUPC-annotation-report.md',
      figures: { ast: 'figures/COACTUPC-ast.svg', cfg: 'figures/COACTUPC-cfg.svg', dfg: 'figures/COACTUPC-dfg.svg' },
    },
  ],
  migrationDesign: 'migration-design.md',
};

function stubFetch() {
  const fetchMock = vi.fn((input: string) => {
    const url = String(input);
    const body = (data: unknown, text?: string) =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(data),
        text: () => Promise.resolve(text ?? JSON.stringify(data)),
      });
    if (url.endsWith('manifest.json')) return body(MANIFEST);
    if (url.endsWith('programs/COACTUPC.json')) return body({ ast: null, cfg: null, dfg: null, metrics: null, errors: [] });
    if (url.endsWith('project.json')) return body({ programs: [], dependencyGraph: { nodes: [], edges: [], hasCycle: false, hasDynamicCall: false }, ranking: { entries: [] }, errors: [] });
    if (url.endsWith('COACTUPC.cbl')) return body(null, '       IDENTIFICATION DIVISION.');
    if (url.endsWith('COACTUPC-annotation-report.md')) return body(null, '# report');
    return Promise.resolve({ ok: false, status: 404, json: () => Promise.resolve(null), text: () => Promise.resolve('') });
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

beforeEach(() => {
  __resetManifestCache();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('staticData loader', () => {
  it('dataUrl resolves under /data/ and never touches API_BASE', () => {
    const url = dataUrl('programs/COACTUPC.json');
    expect(url).toBe('/data/programs/COACTUPC.json');
    expect(url).not.toContain('localhost:5000');
    expect(url).not.toContain('/api/');
  });

  it('loadProgramResult resolves precomputed JSON by program name', async () => {
    const fetchMock = stubFetch();

    const result = await loadProgramResult('COACTUPC');

    expect(result).toEqual({ ast: null, cfg: null, dfg: null, metrics: null, errors: [] });
    expect(fetchMock).toHaveBeenCalledWith('/data/programs/COACTUPC.json');
  });

  it('resolves program name case-insensitively', async () => {
    stubFetch();
    await expect(loadProgramResult('coactupc')).resolves.toBeDefined();
  });

  it('throws an explicit error for an unknown program key', async () => {
    stubFetch();
    await expect(loadProgramResult('DOES-NOT-EXIST')).rejects.toThrow('program not in demo set');
  });

  it('throws an explicit error when a static asset is missing (404)', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 404, text: () => Promise.resolve('') });
    vi.stubGlobal('fetch', fetchMock);
    await expect(loadManifest()).rejects.toThrow('static data not found');
  });

  it('never issues a request to API_BASE in static mode', async () => {
    const fetchMock = stubFetch();

    await loadManifest();
    await loadProgramResult('COACTUPC');
    await loadProgramSource('COACTUPC');
    await loadProject();
    await loadAnnotationReport('COACTUPC');

    for (const call of fetchMock.mock.calls) {
      const url = String(call[0]);
      expect(url).not.toContain('localhost:5000');
      expect(url.startsWith('/data/')).toBe(true);
    }
  });
});
