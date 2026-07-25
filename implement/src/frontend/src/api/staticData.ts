import type { AnalyzeResult } from '../types/analyzeResult';
import type { ProjectAnalyzeResult } from '../types/projectTypes';

// 事前計算 JSON（デモ C）を「プログラム名でキーに」読む静的ローダ（仕様 §6-1）。
// バックエンド不要。API_BASE には一切アクセスしない（同一オリジンの静的資産のみ fetch）。

export interface ProgramEntry {
  rank: number;
  programName: string;
  fileName: string;
  mdi: number;
  risk: string;
  strategy: string;
  fanIn: number;
  fanOut: number;
  source: string;
  result: string;
  annotationReport: string;
  figures: { ast: string; cfg: string; dfg: string };
}

export interface DemoManifest {
  corpus: { name: string; description: string; license: string; sourceUrl: string; pinnedCommit: string };
  selection: { topN: number; count: number; totalPrograms: number };
  programs: ProgramEntry[];
  migrationDesign: string;
}

/** '1' のとき静的データモード。 */
export const STATIC_MODE = import.meta.env.VITE_STATIC_DATA === '1';

const DATA_BASE = (import.meta.env.VITE_DATA_BASE ?? '/data/').replace(/\/*$/, '/');

/** 静的データの URL を解決する（API_BASE は使わない）。 */
export function dataUrl(relativePath: string): string {
  return DATA_BASE + relativePath.replace(/^\//, '');
}

async function getJson<T>(relativePath: string): Promise<T> {
  const res = await fetch(dataUrl(relativePath));
  if (!res.ok) throw new Error(`static data not found: ${relativePath} (${res.status})`);
  return res.json() as Promise<T>;
}

async function getText(relativePath: string): Promise<string> {
  const res = await fetch(dataUrl(relativePath));
  if (!res.ok) throw new Error(`static data not found: ${relativePath} (${res.status})`);
  return res.text();
}

let manifestCache: DemoManifest | null = null;

export async function loadManifest(): Promise<DemoManifest> {
  if (manifestCache === null) manifestCache = await getJson<DemoManifest>('manifest.json');
  return manifestCache;
}

function findEntry(manifest: DemoManifest, key: string): ProgramEntry {
  const upper = key.toUpperCase();
  const entry = manifest.programs.find(
    p => p.programName.toUpperCase() === upper || p.fileName.toUpperCase() === upper,
  );
  if (!entry) throw new Error(`program not in demo set: ${key}`);
  return entry;
}

export async function loadProgramResult(programKey: string): Promise<AnalyzeResult> {
  const manifest = await loadManifest();
  return getJson<AnalyzeResult>(findEntry(manifest, programKey).result);
}

export async function loadProgramSource(programKey: string): Promise<string> {
  const manifest = await loadManifest();
  return getText(findEntry(manifest, programKey).source);
}

export async function loadProject(): Promise<ProjectAnalyzeResult> {
  return getJson<ProjectAnalyzeResult>('project.json');
}

export async function loadAnnotationReport(programKey: string): Promise<{ content: string; fileName: string }> {
  const manifest = await loadManifest();
  const entry = findEntry(manifest, programKey);
  return {
    content: await getText(entry.annotationReport),
    fileName: `${entry.programName}-annotation-report.md`,
  };
}

export async function loadMigrationDesign(): Promise<{ content: string; fileName: string }> {
  const manifest = await loadManifest();
  return { content: await getText(manifest.migrationDesign), fileName: 'migration-design.md' };
}

/** テスト用にマニフェストキャッシュを破棄する。 */
export function __resetManifestCache(): void {
  manifestCache = null;
}
