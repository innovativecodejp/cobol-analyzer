import type { CobolSource, ProjectAnalyzeResult } from '../types/projectTypes';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

export async function analyzeProject(sources: CobolSource[]): Promise<ProjectAnalyzeResult> {
  const res = await fetch(`${API_BASE}/api/project/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sources }),
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json() as Promise<ProjectAnalyzeResult>;
}
