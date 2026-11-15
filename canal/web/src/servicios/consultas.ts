// TanStack Query del canal. Los saldos y conceptos viven sólo en memoria (FR-030): este módulo no conecta ningún
// «persister» ni escribe en localStorage/IndexedDB, y al salir de la sesión se limpia con `queryClient.clear()`.
import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { ErrorDelCanal } from './api'
import { ErrorDePlataforma } from '../plataforma'

/** Reintentos automáticos de una consulta tras el primer fallo, sólo si el fallo es de red o del servidor. */
const REINTENTOS_TRANSITORIOS = 1

export const MENSAJE_ERROR_INESPERADO =
  'La aplicación encontró un problema inesperado. Recarga la página e inténtalo de nuevo; si sigue pasando, comunícate con tu cooperativa.'

/** Decide si TanStack Query vuelve a intentar una consulta: nunca ante un 4xx (repetir la misma petición no sirve). */
export function debeReintentar(fallosPrevios: number, error: unknown): boolean {
  if (error instanceof ErrorDelCanal) {
    return error.esTransitorio && fallosPrevios < REINTENTOS_TRANSITORIOS
  }
  return false
}

/** Texto para mostrarle a la persona: el del servidor (ya en español) o uno accionable si el error es nuestro. */
export function mensajeParaElUsuario(error: unknown): string {
  if (error instanceof ErrorDelCanal || error instanceof ErrorDePlataforma) {
    return error.message
  }
  return MENSAJE_ERROR_INESPERADO
}

/** Registra en la consola lo que falla, para que ningún error pase en silencio. */
export function registrarError(origen: string, error: unknown): void {
  if (error instanceof ErrorDelCanal && error.esDelCliente) {
    // Rechazo de negocio (sesión vencida, pago pendiente…): la pantalla lo muestra; aquí basta un aviso.
    console.warn(`[canal] ${origen}: ${error.code} (${error.status}, correlación ${error.idDeCorrelacion ?? 'sin dato'}).`)
    return
  }
  console.error(`[canal] ${origen} falló.`, error)
}

export function crearClienteDeConsultas(): QueryClient {
  return new QueryClient({
    queryCache: new QueryCache({
      onError: (error, consulta) => registrarError(`La consulta ${JSON.stringify(consulta.queryKey)}`, error),
    }),
    mutationCache: new MutationCache({
      onError: (error, _variables, _contexto, mutacion) =>
        registrarError(`La operación ${JSON.stringify(mutacion.options.mutationKey ?? 'sin clave')}`, error),
    }),
    defaultOptions: {
      queries: {
        retry: debeReintentar,
        // Los saldos cambian con cada pago: se consideran frescos poco tiempo y se vuelven a pedir al volver a la pestaña.
        staleTime: 10_000,
        gcTime: 5 * 60_000,
        refetchOnWindowFocus: true,
        refetchOnReconnect: true,
      },
      mutations: {
        // Una operación (un pago) nunca se repite sola: el reintento lo decide la persona, con la misma clave de
        // idempotencia.
        retry: false,
      },
    },
  })
}
