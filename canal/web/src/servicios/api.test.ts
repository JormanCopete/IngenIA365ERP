import { api, ErrorDelCanal, MENSAJE_SIN_CONEXION, pedir } from './api'

function respuestaJson(cuerpo: unknown, status = 200, cabeceras: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(cuerpo), {
    status,
    headers: { 'Content-Type': 'application/json', ...cabeceras },
  })
}

describe('api', () => {
  const fetchFalso = vi.fn<typeof fetch>()

  beforeEach(() => {
    fetchFalso.mockReset()
    vi.stubGlobal('fetch', fetchFalso)
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  function peticionEnviada(): { url: string; init: RequestInit; cabeceras: Headers } {
    const [url, init] = fetchFalso.mock.calls[0] ?? []
    return { url: String(url), init: init ?? {}, cabeceras: new Headers(init?.headers) }
  }

  it('llama a /api/v1 con cookies, JSON y un id de correlación', async () => {
    fetchFalso.mockResolvedValue(respuestaJson({ tenants: [] }))

    const datos = await api.get<{ tenants: unknown[] }>('/me')

    expect(datos).toEqual({ tenants: [] })
    const { url, init, cabeceras } = peticionEnviada()
    expect(url).toBe('/api/v1/me')
    expect(init.method).toBe('GET')
    expect(init.credentials).toBe('include')
    expect(cabeceras.get('Accept')).toBe('application/json')
    expect(cabeceras.get('X-Correlation-Id')).toMatch(/^[0-9a-f-]{36}$/)
    expect(cabeceras.has('Idempotency-Key')).toBe(false)
  })

  it('manda el cuerpo como JSON y la clave de idempotencia cuando se pide', async () => {
    fetchFalso.mockResolvedValue(respuestaJson({ attemptId: 'a1' }, 201))

    await api.post('/payments', { items: [] }, { claveDeIdempotencia: 'clave-1' })

    const { init, cabeceras } = peticionEnviada()
    expect(init.method).toBe('POST')
    expect(init.body).toBe('{"items":[]}')
    expect(cabeceras.get('Content-Type')).toBe('application/json')
    expect(cabeceras.get('Idempotency-Key')).toBe('clave-1')
  })

  it('convierte el sobre de error en ErrorDelCanal con código, mensaje, datos y estado', async () => {
    fetchFalso.mockResolvedValue(
      respuestaJson(
        { code: 'Canal.Pago.Pendiente', message: 'Ya tienes un pago en proceso.', data: { reference: 'ABC' } },
        409,
        { 'X-Correlation-Id': 'corr-servidor' },
      ),
    )

    const error = await pedir('/payments', { metodo: 'POST', cuerpo: {} }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ErrorDelCanal)
    const errorDelCanal = error as ErrorDelCanal
    expect(errorDelCanal.code).toBe('Canal.Pago.Pendiente')
    expect(errorDelCanal.message).toBe('Ya tienes un pago en proceso.')
    expect(errorDelCanal.data).toEqual({ reference: 'ABC' })
    expect(errorDelCanal.status).toBe(409)
    expect(errorDelCanal.idDeCorrelacion).toBe('corr-servidor')
    expect(errorDelCanal.esDelCliente).toBe(true)
  })

  it('sin sobre, da un mensaje en español según el estado (nunca «Error 502»)', async () => {
    fetchFalso.mockResolvedValue(new Response('<html>Bad Gateway</html>', { status: 502 }))

    const error = (await api.get('/payables').catch((e: unknown) => e)) as ErrorDelCanal

    expect(error.code).toBe('Canal.RespuestaInesperada')
    expect(error.status).toBe(502)
    expect(error.message).toBe('El servicio no está disponible en este momento. Inténtalo de nuevo en unos minutos.')
    expect(error.esTransitorio).toBe(true)
    expect(console.error).toHaveBeenCalled()
  })

  it('si la petición no llega al servidor, lo dice en español con estado 0', async () => {
    fetchFalso.mockRejectedValue(new TypeError('Failed to fetch'))

    const error = (await api.get('/me').catch((e: unknown) => e)) as ErrorDelCanal

    expect(error).toBeInstanceOf(ErrorDelCanal)
    expect(error.code).toBe('Canal.SinConexion')
    expect(error.message).toBe(MENSAJE_SIN_CONEXION)
    expect(error.status).toBe(0)
    expect(error.esDeRed).toBe(true)
    expect(error.cause).toBeInstanceOf(TypeError)
  })

  it('una cancelación no se convierte en error de red', async () => {
    const controlador = new AbortController()
    controlador.abort()
    const cancelacion = new DOMException('Cancelada', 'AbortError')
    fetchFalso.mockRejectedValue(cancelacion)

    await expect(api.get('/me', { senal: controlador.signal })).rejects.toBe(cancelacion)
  })

  it('un 204 devuelve undefined', async () => {
    fetchFalso.mockResolvedValue(new Response(null, { status: 204 }))

    await expect(api.post('/auth/logout')).resolves.toBeUndefined()
  })

  it('una respuesta correcta que no es JSON se informa con un mensaje claro', async () => {
    fetchFalso.mockResolvedValue(new Response('no es json', { status: 200 }))

    const error = (await api.get('/me').catch((e: unknown) => e)) as ErrorDelCanal

    expect(error.code).toBe('Canal.RespuestaIlegible')
    expect(error.message).toMatch(/^Recibimos una respuesta que no pudimos leer/)
  })

  it('pide un archivo como Blob', async () => {
    fetchFalso.mockResolvedValue(
      new Response(new TextEncoder().encode('%PDF-1.7'), { status: 200, headers: { 'Content-Type': 'application/pdf' } }),
    )

    const archivo = await api.get<Blob>('/payments/a1/receipt', { formato: 'archivo' })

    expect(archivo.type).toBe('application/pdf')
    expect(await archivo.text()).toBe('%PDF-1.7')
    expect(new Headers(fetchFalso.mock.calls[0]?.[1]?.headers).get('Accept')).toBe('*/*')
  })
})
