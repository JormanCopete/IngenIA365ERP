import { useState } from 'react'
import { useRegisterSW } from 'virtual:pwa-register/react'
import './AvisoDeActualizacion.css'

/** Cada cuánto se pregunta si hay versión nueva con la PWA abierta mucho tiempo. */
const REVISION_CADA_MS = 60 * 60 * 1000

let revisionProgramada = false

function programarRevision(registro: ServiceWorkerRegistration): void {
  if (revisionProgramada) return
  revisionProgramada = true
  setInterval(() => {
    if (!navigator.onLine) return
    registro.update().catch((error: unknown) => {
      console.warn('[canal] No se pudo revisar si hay una versión nueva del canal.', error)
    })
  }, REVISION_CADA_MS)
}

/**
 * Aviso de versión nueva (vite-plugin-pwa con `registerType: 'prompt'`): la versión nueva no se activa sola en
 * medio de un pago, la persona decide cuándo.
 */
export function AvisoDeActualizacion() {
  const {
    needRefresh: [hayVersionNueva, setHayVersionNueva],
    updateServiceWorker,
  } = useRegisterSW({
    onRegisteredSW: (_url, registro) => {
      if (registro) programarRevision(registro)
    },
    onRegisterError: (error: unknown) => {
      console.error('[canal] No se pudo registrar el service worker; el canal funciona, pero sin el armazón sin conexión.', error)
    },
  })
  const [actualizando, setActualizando] = useState(false)
  const [falloAlActualizar, setFalloAlActualizar] = useState(false)

  if (!hayVersionNueva) return null

  const actualizar = () => {
    setActualizando(true)
    setFalloAlActualizar(false)
    updateServiceWorker(true).catch((error: unknown) => {
      console.error('[canal] No se pudo activar la versión nueva.', error)
      setActualizando(false)
      setFalloAlActualizar(true)
    })
  }

  return (
    <div className="aviso-de-actualizacion" role="status" aria-live="polite">
      <p className="aviso-de-actualizacion__texto">
        {falloAlActualizar
          ? 'No pudimos actualizar. Cierra el canal, ábrelo de nuevo y tendrás la versión nueva.'
          : 'Hay una versión nueva del canal.'}
      </p>
      <div className="aviso-de-actualizacion__acciones">
        <button type="button" className="boton boton--primario" onClick={actualizar} disabled={actualizando}>
          {actualizando ? 'Actualizando…' : 'Actualizar'}
        </button>
        <button type="button" className="boton boton--secundario" onClick={() => setHayVersionNueva(false)}>
          Más tarde
        </button>
      </div>
    </div>
  )
}
