import type { ExportDesignRequest, ExportReportRequest } from '../types/projectTypes';
import { STATIC_MODE, loadAnnotationReport, loadMigrationDesign } from './staticData';

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

/**
 * 注釈レポートをダウンロードする。
 * 静的モードでは任意ソースの再生成は不可。事前計算済み Markdown（プログラム名でキー）を保存する（§6-3）。
 */
export async function downloadAnnotationReport(req: ExportReportRequest): Promise<void> {
  if (STATIC_MODE) {
    const { content, fileName } = await loadAnnotationReport(req.fileName);
    downloadAsFile(content, fileName, MARKDOWN_MIME);
    return;
  }

  const markdown = await postMarkdown('/api/export/annotation-report', req);
  const baseName = req.fileName.replace(/\.[^.]+$/, '') || 'program';
  downloadAsFile(markdown, `${baseName}-annotation-report.md`, MARKDOWN_MIME);
}

/**
 * 移行設計書をダウンロードする。
 * 静的モードでは事前計算済みプロジェクト移行設計書を保存する（sources は無視・§6-3）。
 */
export async function downloadMigrationDesign(req: ExportDesignRequest): Promise<void> {
  if (STATIC_MODE) {
    const { content, fileName } = await loadMigrationDesign();
    downloadAsFile(content, fileName, MARKDOWN_MIME);
    return;
  }

  const markdown = await postMarkdown('/api/export/migration-design', req);
  downloadAsFile(markdown, 'migration-design.md', MARKDOWN_MIME);
}
