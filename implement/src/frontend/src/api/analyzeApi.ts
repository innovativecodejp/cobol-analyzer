import type { AnalyzeResult } from '../types/analyzeResult';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

export async function analyze(source: string): Promise<AnalyzeResult> {
  const res = await fetch(`${API_BASE}/api/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ source }),
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json() as Promise<AnalyzeResult>;
}
