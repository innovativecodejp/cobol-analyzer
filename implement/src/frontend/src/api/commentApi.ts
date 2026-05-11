import type {
  CommentInsertRequest,
  CommentInsertResult,
  CommentRemoveRequest,
  CommentRemoveResult,
} from '../types/commentTypes';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000';

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
  return postJson<CommentInsertRequest, CommentInsertResult>('/api/comment/insert', req);
}

export function previewRemove(req: CommentRemoveRequest): Promise<CommentRemoveResult> {
  return postJson<CommentRemoveRequest, CommentRemoveResult>('/api/comment/preview', req);
}

export function removeComments(req: CommentRemoveRequest): Promise<CommentRemoveResult> {
  return postJson<CommentRemoveRequest, CommentRemoveResult>('/api/comment/remove', req);
}
