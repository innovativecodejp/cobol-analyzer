import type { ExportDesignRequest, ExportReportRequest } from '../types/projectTypes';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';
const MARKDOWN_MIME = 'text/markdown;charset=utf-8';

async function postMarkdown<TRequest>(path: string, req: TRequest): Promise<string> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.text();
}

export function downloadAsFile(content: string, fileName: string, mimeType: string): void {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

export async function downloadAnnotationReport(req: ExportReportRequest): Promise<void> {
  const markdown = await postMarkdown('/api/export/annotation-report', req);
  const baseName = req.fileName.replace(/\.[^.]+$/, '') || 'program';
  downloadAsFile(markdown, `${baseName}-annotation-report.md`, MARKDOWN_MIME);
}

export async function downloadMigrationDesign(req: ExportDesignRequest): Promise<void> {
  const markdown = await postMarkdown('/api/export/migration-design', req);
  downloadAsFile(markdown, 'migration-design.md', MARKDOWN_MIME);
}
