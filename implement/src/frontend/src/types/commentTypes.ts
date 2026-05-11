export interface InsertionSpec {
  targetLine: number;
  tag: string;
  value: string;
  message: string;
}

export interface CommentInsertRequest {
  source: string;
  insertions: InsertionSpec[];
}

export interface CommentInsertResult {
  source: string;
  insertedCount: number;
  warnings: Array<{ line: number; message: string }>;
}

export interface CommentRemoveRequest {
  source: string;
  pattern: string;
}

export interface CommentRemoveResult {
  source: string;
  removedCount: number;
  removedLines: Array<{ lineNumber: number; content: string }>;
  patternError: string | null;
}
