/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// En modo «app» (empaquetado en MAUI HybridWebView) las rutas van por hash y no hay service worker:
// la app ya trae los archivos locales.
export default defineConfig(({ mode }) => ({
  plugins: [react()],
  define: {
    __MODO_APP__: JSON.stringify(mode === 'app'),
  },
  server: {
    port: 5180,
    proxy: {
      '/api': 'http://localhost:8080',
      '/webhooks': 'http://localhost:8080',
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: false,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/pruebas/configuracion.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
  },
}))
