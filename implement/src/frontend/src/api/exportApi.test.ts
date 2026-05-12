import { afterEach, describe, expect, it, vi } from 'vitest';
import { downloadAnnotationReport, downloadAsFile, downloadMigrationDesign } from './exportApi';

function mockFetch(markdown = '# report'): ReturnType<typeof vi.fn> {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    text: () => Promise.resolve(markdown),
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function mockDownload(): void {
  vi.stubGlobal('URL', {
    ...URL,
    createObjectURL: vi.fn(() => 'blob:mock'),
    revokeObjectURL: vi.fn(),
  });
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
}

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('exportApi', () => {
  it('downloadAnnotationReport_callsCorrectEndpoint', async () => {
    const fetchMock = mockFetch();
    mockDownload();

    await downloadAnnotationReport({ fileName: 'PROG-A.cbl', source: 'source' });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/export/annotation-report'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ fileName: 'PROG-A.cbl', source: 'source' }),
      }),
    );
  });

  it('downloadMigrationDesign_callsCorrectEndpoint', async () => {
    const fetchMock = mockFetch();
    mockDownload();

    await downloadMigrationDesign({ sources: [{ fileName: 'PROG-A.cbl', source: 'source' }] });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/api/export/migration-design'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ sources: [{ fileName: 'PROG-A.cbl', source: 'source' }] }),
      }),
    );
  });

  it('downloadAsFile_createsBlobAndAnchor', () => {
    mockDownload();

    downloadAsFile('# report', 'report.md', 'text/markdown');

    expect(URL.createObjectURL).toHaveBeenCalled();
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalled();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:mock');
  });
});
