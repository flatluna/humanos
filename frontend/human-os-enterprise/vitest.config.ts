import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  test: {
    root: path.resolve(import.meta.dirname),
    environment: 'jsdom',
    globals: true,
    setupFiles: [path.resolve(import.meta.dirname, './src/test/setup.ts')],
  },
});
