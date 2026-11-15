/// <reference types="vite/client" />
/// <reference types="vite-plugin-pwa/react" />

/** Verdadero cuando el build corre dentro de la app MAUI (HybridWebView). */
declare const __MODO_APP__: boolean

/** Versión del paquete web (package.json), para el registro del dispositivo. */
declare const __VERSION_CANAL__: string

interface ImportMetaEnv {
  /**
   * Raíz de la API pública del canal. En la PWA es relativa (`/api/v1`, mismo origen); en la app, que corre
   * desde archivos locales, tiene que ser absoluta (`https://<host del canal>/api/v1`).
   */
  readonly VITE_CANAL_API_BASE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
