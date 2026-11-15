import { defineConfig, devices } from '@playwright/test'

// T155 (feature 021): recorridos de la PWA en un navegador real. La API del canal y la pasarela se simulan en el
// navegador (e2e/canalFalso.ts); el servicio Go y el ERP tienen sus propias pruebas de integración.
const PUERTO = 5181

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: `http://localhost:${PUERTO}`,
    locale: 'es-CO',
    timezoneId: 'America/Bogota',
    // Las peticiones del service worker no pasan por page.route.
    serviceWorkers: 'block',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'escritorio', use: { ...devices['Desktop Chrome'] } },
    { name: 'telefono', use: { ...devices['Pixel 7'] } },
  ],
  webServer: {
    command: `npx vite --port ${PUERTO} --strictPort`,
    url: `http://localhost:${PUERTO}`,
    reuseExistingServer: false,
    timeout: 120_000,
  },
})
