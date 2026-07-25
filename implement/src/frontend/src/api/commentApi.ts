import type {
  CommentInsertRequest,
  CommentInsertResult,
  CommentRemoveRequest,
  CommentRemoveResult,
} from '../types/commentTypes';
import { STATIC_MODE } from './staticData';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

const STATIC_DISABLED =
  'static mode: コメント挿入／削除はソース再解析（バックエンド）が前提のため無効です。';

async function postJson<TRequest, TResponse>(path: string, req: TRequest): Promise<TResponse> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json() as Promise<TResponse>;
}

export function insertComments(req: CommentInsertRequest): Promise<CommentInsertResult> {
  if (STATIC_MODE) return Promise.reject(new Error(STATIC_DISABLED));
  return postJson<CommentInsertRequest, CommentInsertResult>('/api/comment/insert', req);
}

export function previewRemove(req: CommentRemoveRequest): Promise<CommentRemoveResult> {
  if (STATIC_MODE) return Promise.reject(new Error(STATIC_DISABLED));
  return postJson<CommentRemoveRequest, CommentRemoveResult>('/api/comment/preview', req);
}

export function removeComments(req: CommentRemoveRequest): Promise<CommentRemoveResult> {
  if (STATIC_MODE) return Promise.reject(new Error(STATIC_DISABLED));
  return postJson<CommentRemoveRequest, CommentRemoveResult>('/api/comment/remove', req);
}
