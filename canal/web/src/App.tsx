import { useEffect } from 'react'
import { RouterProvider, type RouterProviderProps } from 'react-router/dom'
import { AvisoDeActualizacion } from './componentes/AvisoDeActualizacion'
import { ProveedorDeMarca } from './marca/ContextoDeMarca'
import { escucharEnlaces, rutaInternaDesdeEnlace } from './plataforma'
import { obtenerEnrutador, type Enrutador } from './rutas'
import { raizDeLaApi } from './servicios/api'

/** Host del canal: el de la API cuando es absoluta (la app), si no el de la página (la PWA). */
export function hostDelCanal(): string | null {
  const raiz = raizDeLaApi()
  if (/^https?:\/\//i.test(raiz)) return new URL(raiz).hostname
  return typeof window === 'undefined' ? null : window.location.hostname
}

const registrarErrorDeRuta: NonNullable<RouterProviderProps['onError']> = (error, info) => {
  console.error(`[canal] Error en la pantalla ${info.location.pathname}.`, error, info.errorInfo?.componentStack ?? '')
}

/** Los App Links (retorno de la pasarela, toque en una notificación) llegan por `window.canal.alAbrirEnlace`. */
function useEnlacesDeLaApp(enrutador: Enrutador) {
  useEffect(
    () =>
      escucharEnlaces((url) => {
        const ruta = rutaInternaDesdeEnlace(url, hostDelCanal())
        if (ruta === null) return
        enrutador.navigate(ruta).catch((error: unknown) => {
          console.error(`[canal] No se pudo abrir ${ruta} desde un enlace de la app.`, error)
        })
      }),
    [enrutador],
  )
}

export function App({ enrutador = obtenerEnrutador() }: { enrutador?: Enrutador }) {
  useEnlacesDeLaApp(enrutador)
  return (
    <ProveedorDeMarca>
      <RouterProvider router={enrutador} onError={registrarErrorDeRuta} />
      {!__MODO_APP__ && <AvisoDeActualizacion />}
    </ProveedorDeMarca>
  )
}
