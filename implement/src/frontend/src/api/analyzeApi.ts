import type { AnalyzeResult } from '../types/analyzeResult';
import { STATIC_MODE, loadProgramResult } from './staticData';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

/**
 * ライブモード: 任意ソースを API で解析する。
 * 静的モードでは任意ソースの再解析は不可（バックエンド前提）。プログラム名で
 * 事前計算結果を読む {@link loadProgramByName} を使うこと。
 */
export async function analyze(source: string): Promise<AnalyzeResult> {
  if (STATIC_MODE)
    throw new Error('static mode: 任意ソースの解析は不可。loadProgramByName(programName) を使用してください。');

  const res = await fetch(`${API_BASE}/api/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ source }),
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json() as Promise<AnalyzeResult>;
}

/** 静的モード: デモ対象集合のプログラム名で事前計算 AnalyzeResult を読む。 */
export function loadProgramByName(programName: string): Promise<AnalyzeResult> {
  return loadProgramResult(programName);
}
