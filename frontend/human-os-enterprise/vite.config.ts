import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath, URL } from 'node:url';

// Human OS Enterprise console — the B2B/HR-admin app, deliberately SEPARATE
// from frontend/human-os-web (the consumer app on :5178). Own shell, own
// sidebar, same backend (func @ :7071) and same CIAM login.
//
// IMPORTANT: this port (5179) must be added as an extra SPA redirect URI
// on the `twinetstudiologin` Entra CIAM app registration (today it only
// has http://localhost:5178 registered) or MSAL login will fail with
// AADSTS50011. See src/auth/index.ts.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5179,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:7071',
        changeOrigin: true,
      },
    },
  },
});
