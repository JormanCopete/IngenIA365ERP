// Lo que depende de dónde corre el canal: en el navegador (PWA) o dentro de la app MAUI (HybridWebView).
// Ninguna pantalla pregunta por la plataforma: piden `obtenerPlataforma()` y usan la misma interfaz
// (contracts/pasarela-y-app.md, «Puente de la app»).
import { z } from 'zod'

export type NombreDePlataforma = 'web' | 'app'

export interface InfoDePlataforma {
  os: string
  version: string
  appVersion: string | null
}

export interface Biometria {
  disponible: boolean
  /** Tipo que informa el sistema (huella, rostro…), o `null` si no hay. */
  tipo: string | null
}

export interface ResultadoDePasarela {
  resultado: 'returned' | 'cancelled'
  url?: string
}

export interface Plataforma {
  readonly nombre: NombreDePlataforma
  plataforma(): Promise<InfoDePlataforma>
  /** Guarda la credencial de dispositivo (R13). Sólo en la app. */
  guardarCredencial(valor: string): Promise<void>
  /** Pide la biometría y, si pasa, devuelve la credencial guardada; `null` si se cancela o no hay. */
  leerCredencialConBiometria(motivo: string): Promise<string | null>
  borrarCredencial(): Promise<void>
  biometriaDisponible(): Promise<Biometria>
  /**
   * Lleva a la persona a la pasarela. En la app vuelve con el resultado; en la PWA la página se va y el retorno
   * llega como una carga nueva de `retorno`, así que la promesa no se resuelve.
   */
  abrirPasarela(url: string, retorno: string): Promise<ResultadoDePasarela>
  /** Token de notificaciones push del dispositivo, o `null` si no hay. */
  tokenPush(): Promise<string | null>
  /** Guarda o comparte un documento (el comprobante en PDF). */
  abrirDocumento(bytesBase64: string, nombre: string, tipo: string): Promise<void>
}

/** Error de la plataforma (puente con la app, capacidad que el navegador no tiene). El mensaje es para la persona. */
export class ErrorDePlataforma extends Error {
  readonly code: string

  constructor(code: string, message: string, opciones?: { cause?: unknown }) {
    super(message, opciones)
    this.name = 'ErrorDePlataforma'
    this.code = code
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Puente de la app: lo que expone MAUI en `window.HybridWebView` y lo que C# llama en `window.canal`.

export interface PuenteHybridWebView {
  InvokeDotNet(metodo: string, parametros?: unknown[]): Promise<unknown>
}

export interface GanchosDelCanal {
  /** App Link entrante (el retorno de la pasarela o el toque en una notificación). */
  alAbrirEnlace(url: string): void
  /** Notificación recibida con la app abierta. */
  alNotificacion(datos: unknown): void
}

declare global {
  interface Window {
    HybridWebView?: PuenteHybridWebView
    canal?: GanchosDelCanal
  }
}

/** Nombres de los métodos que C# registra con `SetInvokeJavaScriptTarget`. */
export const METODOS_DEL_PUENTE = {
  plataforma: 'Plataforma',
  guardarCredencial: 'GuardarCredencial',
  leerCredencialConBiometria: 'LeerCredencialConBiometria',
  borrarCredencial: 'BorrarCredencial',
  biometriaDisponible: 'BiometriaDisponible',
  abrirPasarela: 'AbrirPasarela',
  tokenPush: 'TokenPush',
  abrirDocumento: 'AbrirDocumento',
} as const

/** `app` si el build es el de la app o si la página corre dentro de HybridWebView; si no, `web`. */
export function detectarPlataforma(modoApp: boolean = __MODO_APP__): NombreDePlataforma {
  if (modoApp) return 'app'
  return typeof window !== 'undefined' && window.HybridWebView !== undefined ? 'app' : 'web'
}

// ---------------------------------------------------------------------------------------------------------------
// Versión web

const MENSAJE_SOLO_EN_LA_APP =
  'Guardar el ingreso en este dispositivo sólo está disponible en la app del canal. En el navegador ingresa con tu documento y clave.'

const MENSAJE_URL_DE_PAGO_INVALIDA =
  'La dirección de pago que recibimos no es válida. Vuelve a intentar el pago; si sigue pasando, comunícate con tu cooperativa.'

function esServidorLocal(nombre: string): boolean {
  return nombre === 'localhost' || nombre === '127.0.0.1' || nombre === '[::1]'
}

/** La pasarela sólo se abre por https; http se admite sólo hacia el propio equipo (simulador de desarrollo). */
export function validarUrlDePasarela(url: string): URL {
  let destino: URL
  try {
    destino = new URL(url)
  } catch (error) {
    throw new ErrorDePlataforma('Plataforma.UrlDePagoInvalida', MENSAJE_URL_DE_PAGO_INVALIDA, { cause: error })
  }
  const segura = destino.protocol === 'https:' || (destino.protocol === 'http:' && esServidorLocal(destino.hostname))
  if (!segura) {
    throw new ErrorDePlataforma('Plataforma.UrlDePagoInvalida', MENSAJE_URL_DE_PAGO_INVALIDA)
  }
  return destino
}

function bytesDesdeBase64(bytesBase64: string): Uint8Array<ArrayBuffer> {
  let binario: string
  try {
    binario = atob(bytesBase64)
  } catch (error) {
    throw new ErrorDePlataforma(
      'Plataforma.DocumentoIlegible',
      'No pudimos abrir el documento porque llegó dañado. Descárgalo de nuevo; si sigue pasando, comunícate con tu cooperativa.',
      { cause: error },
    )
  }
  const bytes = new Uint8Array(binario.length)
  for (let i = 0; i < binario.length; i++) bytes[i] = binario.charCodeAt(i)
  return bytes
}

/** Tiempo para que el navegador tome el archivo antes de liberar la URL temporal. */
const ESPERA_PARA_LIBERAR_ENLACE_MS = 30_000

/** Lo que la versión web usa del navegador (se inyecta en las pruebas). */
export type VentanaDelNavegador = Pick<Window, 'navigator' | 'location' | 'document' | 'setTimeout'> & {
  URL: Pick<typeof URL, 'createObjectURL' | 'revokeObjectURL'>
}

export function crearPlataformaWeb(ventana: VentanaDelNavegador = window): Plataforma {
  return {
    nombre: 'web',

    plataforma: () =>
      Promise.resolve({ os: 'web', version: ventana.navigator.userAgent, appVersion: __VERSION_CANAL__ }),

    guardarCredencial: () =>
      Promise.reject(new ErrorDePlataforma('Plataforma.SoloEnLaApp', MENSAJE_SOLO_EN_LA_APP)),

    // En el navegador no se guarda ninguna credencial de dispositivo: no hay nada que leer ni borrar.
    leerCredencialConBiometria: () => Promise.resolve(null),
    borrarCredencial: () => Promise.resolve(),

    biometriaDisponible: () => Promise.resolve({ disponible: false, tipo: null }),

    abrirPasarela: async (url) => {
      const destino = validarUrlDePasarela(url)
      ventana.location.assign(destino.toString())
      // La página se va hacia la pasarela; el resultado lo muestra la ruta de retorno al volver.
      return new Promise<ResultadoDePasarela>(() => undefined)
    },

    // Web Push llega en una entrega posterior: por ahora el navegador no da token.
    tokenPush: () => Promise.resolve(null),

    abrirDocumento: async (bytesBase64, nombre, tipo) => {
      const archivo = new Blob([bytesDesdeBase64(bytesBase64)], { type: tipo })
      const enlace = ventana.document.createElement('a')
      const direccion = ventana.URL.createObjectURL(archivo)
      enlace.href = direccion
      enlace.download = nombre
      enlace.rel = 'noopener'
      enlace.style.display = 'none'
      ventana.document.body.append(enlace)
      enlace.click()
      enlace.remove()
      ventana.setTimeout(() => ventana.URL.revokeObjectURL(direccion), ESPERA_PARA_LIBERAR_ENLACE_MS)
    },
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Versión app (MAUI HybridWebView)

const MENSAJE_PUENTE_NO_DISPONIBLE =
  'La app no terminó de cargar. Ciérrala, ábrela de nuevo e inténtalo otra vez.'

const MENSAJE_PUENTE_FALLO =
  'La app no pudo completar la operación en tu teléfono. Ciérrala, ábrela de nuevo e inténtalo otra vez; si sigue pasando, actualiza la app.'

const esquemaInfo = z.object({
  os: z.string(),
  version: z.string(),
  appVersion: z.string().nullable().optional(),
})

const esquemaBiometria = z.object({
  disponible: z.boolean(),
  tipo: z.string().nullable().optional(),
})

const esquemaPasarela = z.object({
  resultado: z.enum(['returned', 'cancelled']),
  url: z.string().nullable().optional(),
})

const esquemaTextoOpcional = z.string().nullable().optional()

/**
 * C# serializa con su propia política de nombres: si llegara `{ "Os": … }` en vez de `{ "os": … }` el contrato
 * fallaría sin que se note la causa. Se normaliza la primera letra de las claves del primer nivel.
 */
function normalizarClaves(valor: unknown): unknown {
  if (typeof valor !== 'object' || valor === null || Array.isArray(valor)) return valor
  return Object.fromEntries(
    Object.entries(valor).map(([clave, dato]) => [clave.charAt(0).toLowerCase() + clave.slice(1), dato]),
  )
}

export function crearPlataformaApp(
  obtenerPuente: () => PuenteHybridWebView | undefined = () => window.HybridWebView,
): Plataforma {
  async function invocar(metodo: string, parametros: unknown[] = []): Promise<unknown> {
    const puente = obtenerPuente()
    if (!puente) {
      console.error(`[canal] window.HybridWebView no existe al invocar ${metodo}.`)
      throw new ErrorDePlataforma('Plataforma.PuenteNoDisponible', MENSAJE_PUENTE_NO_DISPONIBLE)
    }
    try {
      return await puente.InvokeDotNet(metodo, parametros)
    } catch (error) {
      console.error(`[canal] El puente de la app falló en ${metodo}.`, error)
      throw new ErrorDePlataforma('Plataforma.PuenteFallo', MENSAJE_PUENTE_FALLO, { cause: error })
    }
  }

  async function invocarYValidar<T>(metodo: string, esquema: z.ZodType<T>, parametros: unknown[] = []): Promise<T> {
    const respuesta = await invocar(metodo, parametros)
    const resultado = esquema.safeParse(normalizarClaves(respuesta))
    if (!resultado.success) {
      console.error(`[canal] La app respondió a ${metodo} con una forma inesperada.`, resultado.error.issues)
      throw new ErrorDePlataforma('Plataforma.RespuestaInesperada', MENSAJE_PUENTE_FALLO, { cause: resultado.error })
    }
    return resultado.data
  }

  return {
    nombre: 'app',

    plataforma: async () => {
      const info = await invocarYValidar(METODOS_DEL_PUENTE.plataforma, esquemaInfo)
      return { os: info.os, version: info.version, appVersion: info.appVersion ?? null }
    },

    guardarCredencial: async (valor) => {
      await invocar(METODOS_DEL_PUENTE.guardarCredencial, [valor])
    },

    leerCredencialConBiometria: async (motivo) =>
      (await invocarYValidar(METODOS_DEL_PUENTE.leerCredencialConBiometria, esquemaTextoOpcional, [motivo])) ?? null,

    borrarCredencial: async () => {
      await invocar(METODOS_DEL_PUENTE.borrarCredencial)
    },

    biometriaDisponible: async () => {
      const biometria = await invocarYValidar(METODOS_DEL_PUENTE.biometriaDisponible, esquemaBiometria)
      return { disponible: biometria.disponible, tipo: biometria.tipo ?? null }
    },

    abrirPasarela: async (url, retorno) => {
      validarUrlDePasarela(url)
      const resultado = await invocarYValidar(METODOS_DEL_PUENTE.abrirPasarela, esquemaPasarela, [url, retorno])
      return resultado.url ? { resultado: resultado.resultado, url: resultado.url } : { resultado: resultado.resultado }
    },

    tokenPush: async () => (await invocarYValidar(METODOS_DEL_PUENTE.tokenPush, esquemaTextoOpcional)) ?? null,

    abrirDocumento: async (bytesBase64, nombre, tipo) => {
      await invocar(METODOS_DEL_PUENTE.abrirDocumento, [bytesBase64, nombre, tipo])
    },
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Elección y ganchos

let plataformaActual: Plataforma | undefined

/** La plataforma de esta ejecución (se decide una vez). */
export function obtenerPlataforma(): Plataforma {
  plataformaActual ??= detectarPlataforma() === 'app' ? crearPlataformaApp() : crearPlataformaWeb()
  return plataformaActual
}

type Oyente<T> = (valor: T) => void

/** Avisos guardados mientras nadie escucha (la app abrió por un enlace antes de montar React). */
const MAXIMO_PENDIENTES = 20

/** Cola por tipo: lo que llega antes de que React se suscriba (la app abrió por un enlace) no se pierde. */
function crearCanalDeEventos<T>(nombre: string) {
  const oyentes = new Set<Oyente<T>>()
  const pendientes: T[] = []

  function entregar(valor: T): void {
    for (const oyente of oyentes) {
      try {
        oyente(valor)
      } catch (error) {
        console.error(`[canal] Falló quien atendía ${nombre}.`, error)
      }
    }
  }

  return {
    emitir(valor: T): void {
      if (oyentes.size > 0) {
        entregar(valor)
        return
      }
      if (pendientes.length >= MAXIMO_PENDIENTES) {
        console.warn(`[canal] Se descartó ${nombre} antiguo: nadie lo atendió a tiempo.`, pendientes.shift())
      }
      pendientes.push(valor)
    },
    escuchar(oyente: Oyente<T>): () => void {
      oyentes.add(oyente)
      for (const valor of pendientes.splice(0)) entregar(valor)
      return () => {
        oyentes.delete(oyente)
      }
    },
  }
}

const enlaces = crearCanalDeEventos<string>('un enlace de la app')
const notificaciones = crearCanalDeEventos<unknown>('una notificación de la app')

/** Instala `window.canal` para que C# pueda avisar. Se llama al arrancar, antes de montar React. */
export function instalarGanchosDelCanal(ventana: Window = window): void {
  ventana.canal = {
    alAbrirEnlace: (url) => {
      if (typeof url !== 'string' || url.trim() === '') {
        console.error('[canal] La app avisó un enlace vacío o que no es texto.', url)
        return
      }
      enlaces.emitir(url)
    },
    alNotificacion: (datos) => notificaciones.emitir(datos),
  }
}

/** Escucha los enlaces que abren la app. Devuelve la función para dejar de escuchar. */
export function escucharEnlaces(oyente: Oyente<string>): () => void {
  return enlaces.escuchar(oyente)
}

/** Escucha las notificaciones que llegan con la app abierta. Devuelve la función para dejar de escuchar. */
export function escucharNotificaciones(oyente: Oyente<unknown>): () => void {
  return notificaciones.escuchar(oyente)
}

/**
 * Convierte un enlace entrante (`https://<host del canal>/pagos/…/retorno?…`) en una ruta interna. Sólo acepta
 * enlaces del mismo host del canal o rutas relativas; cualquier otro se descarta y se registra.
 */
export function rutaInternaDesdeEnlace(url: string, hostDelCanal: string | null): string | null {
  let destino: URL
  try {
    destino = new URL(url, 'https://canal.invalid')
  } catch (error) {
    console.error('[canal] El enlace recibido no es una dirección válida.', error)
    return null
  }
  const relativo = destino.hostname === 'canal.invalid'
  if (!relativo && (hostDelCanal === null || destino.hostname !== hostDelCanal)) {
    console.error(`[canal] Se ignoró un enlace de otro sitio: ${destino.hostname}.`)
    return null
  }
  return `${destino.pathname}${destino.search}${destino.hash}`
}
