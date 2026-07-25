import { defineConfig } from 'vite';

// GitHub Pages プロジェクトサイトはサブパス配信のため base を注入可能にする。
//   ローカル/テスト:                    base = '/'
//   Pages（デモ C を docs/app/ 配下へ）: VITE_BASE=/cobol-analyzer/app/ vite build
export default defineConfig({
  base: process.env.VITE_BASE || '/',
  test: {
    environment: 'jsdom',
    globals: true,
  },
});
