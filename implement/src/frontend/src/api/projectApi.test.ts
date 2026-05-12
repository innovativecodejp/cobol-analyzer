import { afterEach, describe, expect, it, vi } from 'vitest';
import { analyzeProject } from './projectApi';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('projectApi', () => {
  it('analyzeProject_callsCorrectEndpoint', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({
        programs: [],
        dependencyGraph: { nodes: [], edges: [], hasCycle: false, hasDynamicCall: false },
        ranking: { entries: [] },
        errors: [],
      }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await analyzeProject([{ fileName: 'PROG-A.cbl', source: 'source' }]);

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/project/analyze'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ sources: [{ fileName: 'PROG-A.cbl', source: 'source' }] }),
      }),
    );
  });
});
