import type { CobolSource, ProjectAnalyzeResult } from '../types/projectTypes';
import { STATIC_MODE, loadProject } from './staticData';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

/**
 * ライブモード: 複数ソースを API で一括解析する。
 * 静的モード: 事前計算済みプロジェクト結果（dependencyGraph / ranking）を読む（sources は無視）。
 */
export async function analyzeProject(sources: CobolSource[]): Promise<ProjectAnalyzeResult> {
  if (STATIC_MODE) return loadProject();

  const res = await fetch(`${API_BASE}/api/project/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sources }),
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json() as Promise<ProjectAnalyzeResult>;
}
