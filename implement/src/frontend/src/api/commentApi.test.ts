import { afterEach, describe, expect, it, vi } from 'vitest';
import { insertComments, previewRemove, removeComments } from './commentApi';

function mockFetch(body: unknown): ReturnType<typeof vi.fn> {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    json: () => Promise.resolve(body),
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('commentApi', () => {
  it('insertComments_callsCorrectEndpoint', async () => {
    const fetchMock = mockFetch({ source: 'updated', insertedCount: 1, warnings: [] });

    await insertComments({
      source: 'source',
      insertions: [{ targetLine: 1, tag: 'MDI', value: 'HIGH', message: 'review' }],
    });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/comment/insert'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          source: 'source',
          insertions: [{ targetLine: 1, tag: 'MDI', value: 'HIGH', message: 'review' }],
        }),
      }),
    );
  });

  it('previewRemove_callsPreviewEndpoint', async () => {
    const fetchMock = mockFetch({ source: 'source', removedCount: 0, removedLines: [], patternError: null });

    await previewRemove({ source: 'source', pattern: '\\[MDI:.*?\\]' });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/comment/preview'),
      expect.objectContaining({ method: 'POST' }),
    );
  });

  it('removeComments_callsRemoveEndpoint', async () => {
    const fetchMock = mockFetch({ source: 'updated', removedCount: 1, removedLines: [], patternError: null });

    await removeComments({ source: 'source', pattern: '\\[MDI:.*?\\]' });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/comment/remove'),
      expect.objectContaining({ method: 'POST' }),
    );
  });
});
