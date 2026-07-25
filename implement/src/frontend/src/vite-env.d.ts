/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** ライブ API のベース URL（ライブモードのみ使用）。 */
  readonly VITE_API_BASE?: string;
  /** '1' で静的データモード（バックエンド非依存・事前計算 JSON を読む）。 */
  readonly VITE_STATIC_DATA?: string;
  /** 静的データ（docs/data 相当）のベース URL。既定 '/data/'。 */
  readonly VITE_DATA_BASE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
