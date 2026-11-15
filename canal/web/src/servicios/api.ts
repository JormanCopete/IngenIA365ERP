// Cliente HTTP de la API pública del canal (`/api/v1`, contracts/canal-api.md §A).
//
// Toda respuesta de error del servicio trae el sobre `{ code, message, data }` con el mensaje ya en español; este
// módulo lo convierte en `ErrorDelCanal` para que ninguna pantalla tenga que leer un `Response`. Cuando no hay sobre
// (un proxy, una caída) el mensaje sale de `mensajePorEstado`, nunca un «Error 400» a secas.

/** Sobre de error del canal: `{ "code": "Canal.Pago.Pendiente", "message": "…", "data": {…} }`. */
export interface SobreDeError {
  code: string
  message: string
  data?: Record<string, unknown> | null
}

export const CODIGO_SIN_CONEXION = 'Canal.SinConexion'
export const CODIGO_RESPUESTA_INESPERADA = 'Canal.RespuestaInesperada'
export const CODIGO_RESPUESTA_ILEGIBLE = 'Canal.RespuestaIlegible'

export const MENSAJE_SIN_CONEXION =
  'No pudimos comunicarnos con el servidor. Revisa tu conexión e inténtalo de nuevo.'

const CABECERA_CORRELACION = 'X-Correlation-Id'
const CABECERA_IDEMPOTENCIA = 'Idempotency-Key'
const CABECERA_PLATAFORMA = 'X-Canal-Plataforma'

/** Código con el que el servicio dice que no hay sesión (o que terminó). */
export const CODIGO_SESION_REQUERIDA = 'Canal.Sesion.Requerida'

// ---------------------------------------------------------------------------------------------------------------
// Sesión en la app. En la PWA la sesión viaja en una cookie HttpOnly que este código no ve; la app, que llama desde
// archivos locales, recibe el token en el cuerpo y lo manda en `Authorization`. Sólo vive en memoria.

let tokenDeSesion: string | null = null
let plataformaDeLaApi: 'android' | 'ios' | null = null

/** Token de la sesión de la app (`null` al salir). En la PWA no se usa. */
export function establecerTokenDeSesion(token: string | null): void {
  tokenDeSesion = token
}

/** En la app, «android» o «ios»: el servicio entrega entonces el token y la credencial en el cuerpo. */
export function establecerPlataformaDeLaApi(plataforma: 'android' | 'ios' | null): void {
  plataformaDeLaApi = plataforma
}

type OyenteDeSesion = (evento: 'actividad' | 'vencida') => void
const oyentesDeSesion = new Set<OyenteDeSesion>()

/**
 * Avisos para el reloj de la sesión: «actividad» tras cada respuesta del servidor que cuenta como uso, «vencida»
 * cuando el servidor dice que la sesión terminó. Devuelve la función para dejar de escuchar.
 */
export function escucharSesion(oyente: OyenteDeSesion): () => void {
  oyentesDeSesion.add(oyente)
  return () => oyentesDeSesion.delete(oyente)
}

function avisarSesion(evento: 'actividad' | 'vencida'): void {
  for (const oyente of oyentesDeSesion) {
    try {
      oyente(evento)
    } catch (error) {
      console.error('[canal] Un oyente de la sesión falló.', error)
    }
  }
}

// Rutas que no alargan la sesión: la consulta de su estado (la hace el aviso) y las de ingreso.
function cuentaComoActividad(ruta: string): boolean {
  return !ruta.startsWith('/session') || ruta.startsWith('/session/keepalive') || ruta.startsWith('/session/tenant')
}

/** Error de una llamada a la API. `status` es 0 cuando la petición no llegó al servidor. */
export class ErrorDelCanal extends Error {
  readonly code: string
  readonly data: Readonly<Record<string, unknown>> | null
  readonly status: number
  /** Código de seguimiento de la petición, para que soporte la encuentre en el registro. */
  readonly idDeCorrelacion: string | null

  constructor(
    datos: {
      code: string
      message: string
      status: number
      data?: Record<string, unknown> | null
      idDeCorrelacion?: string | null
    },
    opciones?: { cause?: unknown },
  ) {
    super(datos.message, opciones)
    this.name = 'ErrorDelCanal'
    this.code = datos.code
    this.status = datos.status
    this.data = datos.data ?? null
    this.idDeCorrelacion = datos.idDeCorrelacion ?? null
  }

  /** La petición no llegó al servidor (sin red, DNS, CORS, servidor apagado). */
  get esDeRed(): boolean {
    return this.status === 0
  }

  /** El servidor rechazó la petición por algo de la petición misma (4xx): reintentarla igual no sirve. */
  get esDelCliente(): boolean {
    return this.status >= 400 && this.status < 500
  }

  /** Falla del servidor o de la red: puede servir reintentar. */
  get esTransitorio(): boolean {
    return this.status === 0 || this.status >= 500
  }
}

export type MetodoHttp = 'GET' | 'POST' | 'PUT' | 'DELETE'

export interface OpcionesDePeticion {
  metodo?: MetodoHttp
  /** Se envía como JSON. */
  cuerpo?: unknown
  /** Cabecera `Idempotency-Key`: la misma clave en un reintento o doble clic no crea un segundo pago (FR-036). */
  claveDeIdempotencia?: string
  /** Cancela la petición (TanStack Query la pasa al desmontar o invalidar). */
  senal?: AbortSignal
  /** `json` (por defecto) o `archivo` para un PDF u otro binario. */
  formato?: 'json' | 'archivo'
}

/** Raíz de la API: relativa en la PWA, absoluta en la app (ver `VITE_CANAL_API_BASE`). */
export function raizDeLaApi(): string {
  const configurada = import.meta.env.VITE_CANAL_API_BASE
  const raiz = configurada && configurada.trim() !== '' ? configurada.trim() : '/api/v1'
  return raiz.endsWith('/') ? raiz.slice(0, -1) : raiz
}

/** Identificador aleatorio (UUID v4). `crypto.randomUUID` sólo existe en contextos seguros. */
export function nuevoIdentificador(): string {
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  const bytes = crypto.getRandomValues(new Uint8Array(16))
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x40
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

/** Clave para `Idempotency-Key`. Se genera una vez por intención del usuario (no por reintento). */
export function nuevaClaveDeIdempotencia(): string {
  return nuevoIdentificador()
}

/** Mensaje en español cuando el servidor no mandó el sobre (proxy, balanceador, caída). */
export function mensajePorEstado(status: number): string {
  if (status === 401) return 'Tu sesión terminó. Ingresa de nuevo para continuar.'
  if (status === 403) return 'No tienes permiso para hacer esta operación.'
  if (status === 404) return 'No encontramos lo que buscas. Puede que ya no esté disponible.'
  if (status === 408 || status === 504)
    return 'El servidor tardó demasiado en responder. Inténtalo de nuevo en unos minutos.'
  if (status === 409)
    return 'La operación no se completó porque la información cambió mientras tanto. Actualiza la pantalla e inténtalo de nuevo.'
  if (status === 413) return 'Lo que enviaste es demasiado grande. Revisa la información e inténtalo de nuevo.'
  if (status === 429) return 'Hiciste demasiados intentos seguidos. Espera unos minutos e inténtalo de nuevo.'
  if (status >= 500)
    return 'El servicio no está disponible en este momento. Inténtalo de nuevo en unos minutos.'
  if (status >= 400)
    return 'No pudimos procesar la información enviada. Revísala e inténtalo de nuevo.'
  return 'No pudimos completar la operación. Inténtalo de nuevo; si el problema sigue, comunícate con tu cooperativa.'
}

function esSobreDeError(valor: unknown): valor is SobreDeError {
  if (typeof valor !== 'object' || valor === null) return false
  const posible = valor as Record<string, unknown>
  const datosValidos =
    posible.data === undefined ||
    posible.data === null ||
    (typeof posible.data === 'object' && !Array.isArray(posible.data))
  return (
    typeof posible.code === 'string' &&
    posible.code !== '' &&
    typeof posible.message === 'string' &&
    posible.message.trim() !== '' &&
    datosValidos
  )
}

function esCancelacion(error: unknown, senal: AbortSignal | undefined): boolean {
  return senal?.aborted === true || (error instanceof DOMException && error.name === 'AbortError')
}

async function leerSobre(respuesta: Response): Promise<SobreDeError | null> {
  let texto: string
  try {
    texto = await respuesta.text()
  } catch (error) {
    console.error('[canal] No se pudo leer el cuerpo de la respuesta de error.', error)
    return null
  }
  if (texto.trim() === '') return null
  try {
    const valor: unknown = JSON.parse(texto)
    return esSobreDeError(valor) ? valor : null
  } catch (error) {
    console.error('[canal] La respuesta de error no es JSON.', error)
    return null
  }
}

/**
 * Hace una petición a la API del canal. Devuelve el JSON (o `undefined` en un 204) y lanza `ErrorDelCanal`
 * en cualquier otro caso. La cancelación (AbortSignal) se propaga tal cual, sin convertirla en error de red.
 */
export async function pedir<T>(ruta: string, opciones: OpcionesDePeticion = {}): Promise<T> {
  const { metodo = 'GET', cuerpo, claveDeIdempotencia, senal, formato = 'json' } = opciones
  const idDeCorrelacion = nuevoIdentificador()

  const cabeceras = new Headers({
    Accept: formato === 'json' ? 'application/json' : '*/*',
    [CABECERA_CORRELACION]: idDeCorrelacion,
  })
  if (cuerpo !== undefined) cabeceras.set('Content-Type', 'application/json')
  if (claveDeIdempotencia) cabeceras.set(CABECERA_IDEMPOTENCIA, claveDeIdempotencia)
  if (plataformaDeLaApi) cabeceras.set(CABECERA_PLATAFORMA, plataformaDeLaApi)
  if (tokenDeSesion) cabeceras.set('Authorization', `Bearer ${tokenDeSesion}`)

  const url = `${raizDeLaApi()}${ruta.startsWith('/') ? ruta : `/${ruta}`}`

  let respuesta: Response
  try {
    respuesta = await fetch(url, {
      method: metodo,
      headers: cabeceras,
      credentials: 'include',
      ...(cuerpo !== undefined ? { body: JSON.stringify(cuerpo) } : {}),
      ...(senal ? { signal: senal } : {}),
    })
  } catch (error) {
    if (esCancelacion(error, senal)) throw error
    console.error(`[canal] ${metodo} ${ruta} no llegó al servidor (correlación ${idDeCorrelacion}).`, error)
    throw new ErrorDelCanal(
      { code: CODIGO_SIN_CONEXION, message: MENSAJE_SIN_CONEXION, status: 0, idDeCorrelacion },
      { cause: error },
    )
  }

  const correlacion = respuesta.headers.get(CABECERA_CORRELACION) ?? idDeCorrelacion

  if (!respuesta.ok) {
    const sobre = await leerSobre(respuesta)
    if (respuesta.status === 401 && sobre?.code === CODIGO_SESION_REQUERIDA) avisarSesion('vencida')
    if (sobre) {
      throw new ErrorDelCanal({
        code: sobre.code,
        message: sobre.message,
        status: respuesta.status,
        data: sobre.data ?? null,
        idDeCorrelacion: correlacion,
      })
    }
    console.error(
      `[canal] ${metodo} ${ruta} respondió ${respuesta.status} sin el sobre de error (correlación ${correlacion}).`,
    )
    throw new ErrorDelCanal({
      code: CODIGO_RESPUESTA_INESPERADA,
      message: mensajePorEstado(respuesta.status),
      status: respuesta.status,
      data: { status: respuesta.status },
      idDeCorrelacion: correlacion,
    })
  }

  if (cuentaComoActividad(ruta)) avisarSesion('actividad')
  if (respuesta.status === 204) return undefined as T

  try {
    if (formato === 'archivo') return (await respuesta.blob()) as T
    const texto = await respuesta.text()
    return (texto.trim() === '' ? undefined : JSON.parse(texto)) as T
  } catch (error) {
    if (esCancelacion(error, senal)) throw error
    console.error(`[canal] ${metodo} ${ruta}: la respuesta no se pudo leer (correlación ${correlacion}).`, error)
    throw new ErrorDelCanal(
      {
        code: CODIGO_RESPUESTA_ILEGIBLE,
        message:
          'Recibimos una respuesta que no pudimos leer. Inténtalo de nuevo; si el problema sigue, comunícate con tu cooperativa.',
        status: respuesta.status,
        idDeCorrelacion: correlacion,
      },
      { cause: error },
    )
  }
}

type OpcionesSinMetodo = Omit<OpcionesDePeticion, 'metodo' | 'cuerpo'>

/** Atajos por método. */
export const api = {
  get: <T>(ruta: string, opciones?: OpcionesSinMetodo) => pedir<T>(ruta, { ...opciones, metodo: 'GET' }),
  post: <T>(ruta: string, cuerpo?: unknown, opciones?: OpcionesSinMetodo) =>
    pedir<T>(ruta, { ...opciones, metodo: 'POST', cuerpo }),
  put: <T>(ruta: string, cuerpo?: unknown, opciones?: OpcionesSinMetodo) =>
    pedir<T>(ruta, { ...opciones, metodo: 'PUT', cuerpo }),
  delete: <T>(ruta: string, opciones?: OpcionesSinMetodo) => pedir<T>(ruta, { ...opciones, metodo: 'DELETE' }),
}
