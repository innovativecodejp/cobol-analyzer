import { afterEach, describe, expect, it, vi } from 'vitest';

// 静的モード（STATIC_MODE=true）で各 api 境界が API_BASE を叩かず静的ローダへ委譲することを検証（§6-1 / §9）。
vi.mock('./staticData', () => ({
  STATIC_MODE: true,
  loadProgramResult: vi.fn().mockResolvedValue({ ast: null, cfg: null, dfg: null, metrics: null, errors: [] }),
  loadProject: vi.fn().mockResolvedValue({
    programs: [],
    dependencyGraph: { nodes: [], edges: [], hasCycle: false, hasDynamicCall: false },
    ranking: { entries: [] },
    errors: [],
  }),
  loadAnnotationReport: vi.fn().mockResolvedValue({ content: '# report', fileName: 'X-annotation-report.md' }),
  loadMigrationDesign: vi.fn().mockResolvedValue({ content: '# design', fileName: 'migration-design.md' }),
}));

import { analyze } from './analyzeApi';
import { insertComments, previewRemove, removeComments } from './commentApi';
import { downloadAnnotationReport, downloadMigrationDesign } from './exportApi';
import { analyzeProject } from './projectApi';

function stubFetchThatMustNotBeCalled(): ReturnType<typeof vi.fn> {
  const fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function mockDownload(): void {
  vi.stubGlobal('URL', { ...URL, createObjectURL: vi.fn(() => 'blob:mock'), revokeObjectURL: vi.fn() });
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
}

afterEach(() => {
  // 注: vi.restoreAllMocks() は vi.mock ファクトリの mockResolvedValue も消すため使わない。
  vi.unstubAllGlobals();
});

describe('api boundaries in static mode', () => {
  it('analyze() refuses arbitrary re-analysis without hitting the backend', async () => {
    const fetchMock = stubFetchThatMustNotBeCalled();
    await expect(analyze('SOME SOURCE')).rejects.toThrow('static mode');
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('analyzeProject() returns precomputed project data without fetching API_BASE', async () => {
    const fetchMock = stubFetchThatMustNotBeCalled();
    const result = await analyzeProject([]);
    expect(result.ranking.entries).toEqual([]);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('downloadAnnotationReport() saves precomputed markdown without fetching API_BASE', async () => {
    const fetchMock = stubFetchThatMustNotBeCalled();
    mockDownload();
    await downloadAnnotationReport({ fileName: 'COACTUPC.cbl', source: '' });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalled();
  });

  it('downloadMigrationDesign() saves precomputed markdown without fetching API_BASE', async () => {
    const fetchMock = stubFetchThatMustNotBeCalled();
    mockDownload();
    await downloadMigrationDesign({ sources: [] });
    expect(fetchMock).not.toHaveBeenCalled();
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalled();
  });

  it('comment insert/preview/remove are disabled in static mode', async () => {
    const fetchMock = stubFetchThatMustNotBeCalled();
    await expect(insertComments({ source: '', tags: [] } as never)).rejects.toThrow('static mode');
    await expect(previewRemove({ source: '' } as never)).rejects.toThrow('static mode');
    await expect(removeComments({ source: '' } as never)).rejects.toThrow('static mode');
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
