// En las pruebas `virtual:pwa-register/react` apunta aquí (vite.config.ts, `test.alias`): el ejecutor de Vitest no
// carga los módulos virtuales de vite-plugin-pwa. Se comporta como la versión sin service worker del plugin.
import { useState } from 'react'
import type { RegisterSWOptions } from 'vite-plugin-pwa/types'

export function useRegisterSW(opciones: RegisterSWOptions = {}) {
  void opciones
  const needRefresh = useState(false)
  const offlineReady = useState(false)
  const updateServiceWorker = (recargar?: boolean): Promise<void> => {
    void recargar
    return Promise.resolve()
  }
  return { needRefresh, offlineReady, updateServiceWorker }
}
