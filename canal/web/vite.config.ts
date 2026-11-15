/// <reference types="vitest/config" />
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { defineConfig, loadEnv, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

const paquete = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf8')) as { version: string }

/** Lee un color por defecto de src/marca/tokens.css: el manifiesto y la barra del sistema no repiten el valor. */
function colorDeTokens(variable: string): string {
  const tokens = readFileSync(new URL('./src/marca/tokens.css', import.meta.url), 'utf8')
  const encontrado = new RegExp(`${variable}:\\s*(#[0-9a-fA-F]{6})`).exec(tokens)?.[1]
  if (!encontrado) throw new Error(`No se encontró ${variable} en src/marca/tokens.css.`)
  return encontrado
}

/** `%COLOR_TEMA%` de index.html con el primario por defecto (antes de que Vite procese el HTML). */
function colorDelTemaEnHtml(colorTema: string): Plugin {
  return {
    name: 'canal-color-del-tema',
    transformIndexHtml: { order: 'pre', handler: (html) => html.replaceAll('%COLOR_TEMA%', colorTema) },
  }
}

/**
 * En la app, el script del puente que sirve HybridWebView y que crea `window.HybridWebView`. Va al final del
 * procesamiento (Vite no lo empaqueta); al ser un script clásico corre antes que el módulo de la aplicación, que es diferido.
 */
function puenteDeLaApp(): Plugin {
  return {
    name: 'canal-puente-de-la-app',
    transformIndexHtml: {
      order: 'post',
      handler: () => [{ tag: 'script', attrs: { src: '_framework/hybridwebview.js' }, injectTo: 'head' }],
    },
  }
}

// Rutas del servicio que nunca resuelve la PWA: van siempre a la red, sin el index.html de respaldo.
const RUTAS_DEL_SERVIDOR = [/^\/api(\/|$)/, /^\/webhooks(\/|$)/, /^\/internal(\/|$)/, /^\/salud(\/|$)/]

// En modo «app» (empaquetado en MAUI HybridWebView) las rutas van por hash y no hay service worker:
// la app ya trae los archivos locales.
export default defineConfig(({ mode }) => {
  const esApp = mode === 'app'
  const colorPrimario = colorDeTokens('--color-primario')
  const colorFondo = colorDeTokens('--color-fondo')

  const entorno = loadEnv(mode, process.cwd(), 'VITE_')
  if (esApp && !/^https:\/\//i.test(entorno.VITE_CANAL_API_BASE ?? '')) {
    console.warn(
      '[canal] Build de la app sin VITE_CANAL_API_BASE absoluta (https://<host del canal>/api/v1): ' +
        'la app no podrá llamar a la API desde sus archivos locales.',
    )
  }

  return {
    plugins: [
      react(),
      colorDelTemaEnHtml(colorPrimario),
      esApp && puenteDeLaApp(),
      VitePWA({
        // En la app no hay service worker, manifiesto ni registro: el plugin queda apagado del todo y el
        // módulo `virtual:pwa-register/react` resuelve a su versión inerte.
        disable: esApp,
        registerType: 'prompt',
        injectRegister: false,
        // Los íconos de public/ ya entran por globPatterns; así no se repiten en la lista de precarga.
        includeManifestIcons: false,
        manifest: {
          id: '/',
          name: 'Canal del asociado',
          short_name: 'Asociados',
          description: 'Consulta tus productos en tu cooperativa y paga en línea.',
          lang: 'es-CO',
          dir: 'ltr',
          start_url: '/',
          scope: '/',
          display: 'standalone',
          theme_color: colorPrimario,
          background_color: colorFondo,
          categories: ['finance'],
          icons: [
            { src: 'iconos/icono-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
            { src: 'iconos/icono-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
            { src: 'iconos/icono-mascara-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
          ],
        },
        workbox: {
          // Sólo el armazón de la aplicación. Ninguna respuesta de la API se guarda (FR-030): no hay runtimeCaching.
          globPatterns: ['**/*.{js,css,html,svg,png}'],
          navigateFallback: 'index.html',
          navigateFallbackDenylist: RUTAS_DEL_SERVIDOR,
          runtimeCaching: [],
          cleanupOutdatedCaches: true,
          // US10 (T132): el manejador de los avisos por Web Push.
          importScripts: ['avisos-sw.js'],
        },
        devOptions: { enabled: false },
      }),
    ],
    define: {
      __MODO_APP__: JSON.stringify(esApp),
      __VERSION_CANAL__: JSON.stringify(paquete.version),
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
      // El ejecutor de Vitest no carga los módulos virtuales de vite-plugin-pwa.
      alias: {
        'virtual:pwa-register/react': fileURLToPath(new URL('./src/pruebas/registro-pwa-inerte.ts', import.meta.url)),
      },
    },
  }
})
