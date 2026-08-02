import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { fileURLToPath, URL } from 'node:url';

// Human OS Employee Portal — a THIRD, deliberately separate frontend app
// (alongside human-os-web, the consumer/individual app on :5178, and
// human-os-enterprise, the HR/Manager console on :5179). This app is where
// employees of a company tenant take their assigned courses — same backend
// (func @ :7071), same CIAM login, but its own shell (no HR admin nav) and
// its own branding (not the consumer "Engram Academy" identity, which is
// aimed at independent students, not corporate employees).
//
// IMPORTANT: this port (5180) must be added as an extra SPA redirect URI
// on the `twinetstudiologin` Entra CIAM app registration (today it only
// has http://localhost:5178 and http://localhost:5179 registered) or MSAL
// login will fail with AADSTS50011. See src/auth/index.ts.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5180,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:7071',
        changeOrigin: true,
      },
    },
  },
});
