/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// En desarrollo, Vite hace de proxy hacia la API: el navegador ve un único origen,
// por lo que la cookie del refresh token (SameSite=Strict) funciona sin configurar CORS.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: process.env.VITE_API_PROXY ?? 'http://localhost:5080', changeOrigin: false },
      '/health': { target: process.env.VITE_API_PROXY ?? 'http://localhost:5080' },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
    css: false,
  },
});
