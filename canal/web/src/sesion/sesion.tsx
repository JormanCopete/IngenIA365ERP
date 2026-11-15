// Estado de la sesión del asociado: el perfil (GET /me), la cooperativa elegida con su marca y la salida. El perfil
// vive sólo en la caché en memoria de TanStack Query (FR-030); salir la limpia entera.
import { useCallback, useEffect } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Navigate, Outlet, useLocation, useNavigate } from 'react-router'
import { ErrorDelCanal } from '../servicios/api'
import { ingreso, perfil as apiPerfil, type Perfil } from '../servicios/identidad'
import { useMarca } from '../marca/ContextoDeMarca'
import { IndicadorDeCarga } from '../componentes/IndicadorDeCarga'
import { AvisoDeSesion } from '../componentes/AvisoDeSesion'
import { mensajeParaElUsuario } from '../servicios/consultas'

export const CLAVE_PERFIL = ['perfil'] as const

/** Motivo de una salida, para el mensaje en la pantalla de ingreso. */
export type MotivoDeSalida = 'inactividad' | 'maximo' | 'vencida' | 'salida'

export function usePerfil() {
  return useQuery<Perfil, ErrorDelCanal>({
    queryKey: CLAVE_PERFIL,
    queryFn: apiPerfil.leer,
    retry: (fallos, error) => error.esTransitorio && fallos < 1,
    staleTime: 30_000,
  })
}

/** La ruta actual, para volver a ella tras ingresar. */
export function rutaActual(ubicacion: { pathname: string; search: string }): string {
  return `${ubicacion.pathname}${ubicacion.search}`
}

/** Sólo rutas internas del canal: un `volver` que apunte afuera se ignora. */
export function rutaDeRegreso(valor: string | null, porDefecto = '/cooperativa'): string {
  if (!valor || !valor.startsWith('/') || valor.startsWith('//')) return porDefecto
  return valor
}

/** Cierra la sesión (en el servidor si todavía vive), limpia la memoria y lleva al ingreso. */
export function useSalir() {
  const clienteDeConsultas = useQueryClient()
  const navegar = useNavigate()
  const { establecerMarca } = useMarca()
  return useCallback(
    async (motivo: MotivoDeSalida, volver?: string) => {
      try {
        await ingreso.salir()
      } catch (error) {
        // La sesión puede haber terminado ya en el servidor; salir siempre sale.
        console.warn('[canal] El cierre de sesión en el servidor falló; se sale igual.', error)
      }
      clienteDeConsultas.clear()
      establecerMarca(null)
      const parametros = new URLSearchParams({ motivo })
      if (volver && motivo !== 'salida') parametros.set('volver', volver)
      void navegar(`/ingreso?${parametros.toString()}`, { replace: true })
    },
    [clienteDeConsultas, establecerMarca, navegar],
  )
}

/** Aplica la marca de la cooperativa elegida (o la del canal si no hay). */
function useMarcaDeLaSesion(datos: Perfil | undefined) {
  const { establecerMarca } = useMarca()
  const elegida = datos?.tenants.find((t) => t.tenantPublicId === datos.tenant) ?? null
  const clave = elegida ? `${elegida.tenantPublicId}` : ''
  useEffect(() => {
    establecerMarca(elegida ? { ...elegida.brand, displayName: elegida.displayName } : null)
    // La marca cambia sólo cuando cambia la cooperativa elegida.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clave, establecerMarca])
}

/** Rutas que exigen sesión: sin ella, al ingreso con la ruta de regreso. Monta el aviso de vencimiento. */
export function RequiereSesion() {
  const ubicacion = useLocation()
  const { data, error, isPending } = usePerfil()
  useMarcaDeLaSesion(data)

  if (isPending) {
    return (
      <IndicadorDeCarga cargando texto="Cargando tu información…">
        <div className="pagina pagina--minima" />
      </IndicadorDeCarga>
    )
  }
  if (error) {
    if (error.status === 401) {
      return <Navigate to={`/ingreso?volver=${encodeURIComponent(rutaActual(ubicacion))}`} replace />
    }
    return (
      <section className="pagina">
        <p className="aviso aviso--error" role="alert">
          {mensajeParaElUsuario(error)}
        </p>
      </section>
    )
  }
  return (
    <>
      <Outlet />
      <AvisoDeSesion sesion={data.session} />
    </>
  )
}

/** Rutas de una cooperativa: sin cooperativa elegida, al selector. */
export function RequiereCooperativa() {
  const ubicacion = useLocation()
  const { data } = usePerfil()
  if (!data?.tenant) {
    return <Navigate to={`/cooperativas?volver=${encodeURIComponent(rutaActual(ubicacion))}`} replace />
  }
  return <Outlet />
}
