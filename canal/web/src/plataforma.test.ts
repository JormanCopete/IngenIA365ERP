import {
  crearPlataformaApp,
  crearPlataformaWeb,
  detectarPlataforma,
  ErrorDePlataforma,
  escucharEnlaces,
  instalarGanchosDelCanal,
  METODOS_DEL_PUENTE,
  rutaInternaDesdeEnlace,
  type PuenteHybridWebView,
  type VentanaDelNavegador,
} from './plataforma'

afterEach(() => {
  delete window.HybridWebView
  vi.restoreAllMocks()
})

describe('detectarPlataforma', () => {
  it('es web en el navegador', () => {
    expect(detectarPlataforma(false)).toBe('web')
  })

  it('es app cuando existe window.HybridWebView', () => {
    window.HybridWebView = { InvokeDotNet: vi.fn() }
    expect(detectarPlataforma(false)).toBe('app')
  })

  it('es app cuando el build es el de la app', () => {
    expect(detectarPlataforma(true)).toBe('app')
  })

  it('por defecto usa el modo del build (__MODO_APP__ es falso en las pruebas)', () => {
    expect(detectarPlataforma()).toBe('web')
  })
})

/** Ventana mínima con lo que usa la versión web. */
function ventanaFalsa() {
  const assign = vi.fn<(url: string) => void>()
  const createObjectURL = vi.fn<(objeto: Blob) => string>(() => 'blob:canal/1')
  const revokeObjectURL = vi.fn<(url: string) => void>()
  const setTimeoutFalso = vi.fn((manejador: () => void) => {
    manejador()
    return 0
  })
  const ventana = {
    navigator: { userAgent: 'Prueba/1.0' },
    location: { assign },
    document,
    URL: { createObjectURL, revokeObjectURL },
    setTimeout: setTimeoutFalso,
  } as unknown as VentanaDelNavegador
  return { ventana, assign, createObjectURL, revokeObjectURL }
}

describe('plataforma web', () => {
  it('se identifica como web con la versión del paquete', async () => {
    const { ventana } = ventanaFalsa()
    const web = crearPlataformaWeb(ventana)

    expect(web.nombre).toBe('web')
    await expect(web.plataforma()).resolves.toEqual({ os: 'web', version: 'Prueba/1.0', appVersion: __VERSION_CANAL__ })
  })

  it('no ofrece biometría, push ni credencial de dispositivo', async () => {
    const web = crearPlataformaWeb(ventanaFalsa().ventana)

    await expect(web.biometriaDisponible()).resolves.toEqual({ disponible: false, tipo: null })
    await expect(web.tokenPush()).resolves.toBeNull()
    await expect(web.leerCredencialConBiometria('Ingresar')).resolves.toBeNull()
    await expect(web.borrarCredencial()).resolves.toBeUndefined()
    const error = await web.guardarCredencial('x').catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ErrorDePlataforma)
    expect((error as ErrorDePlataforma).code).toBe('Plataforma.SoloEnLaApp')
    expect((error as ErrorDePlataforma).message).toMatch(/sólo está disponible en la app/)
  })

  it('abre la pasarela llevando la página a su dirección', () => {
    const { ventana, assign } = ventanaFalsa()
    const web = crearPlataformaWeb(ventana)

    void web.abrirPasarela('https://pasarela.example/session/1', 'https://canal.example/pagos/1/retorno')

    expect(assign).toHaveBeenCalledWith('https://pasarela.example/session/1')
  })

  it('admite http sólo hacia el propio equipo (simulador de desarrollo)', async () => {
    const { ventana, assign } = ventanaFalsa()
    const web = crearPlataformaWeb(ventana)

    void web.abrirPasarela('http://localhost:8080/simulada/1', '/pagos/1/retorno')
    expect(assign).toHaveBeenCalledWith('http://localhost:8080/simulada/1')

    await expect(web.abrirPasarela('http://pasarela.example/1', '/r')).rejects.toBeInstanceOf(ErrorDePlataforma)
    await expect(web.abrirPasarela('javascript:alert(1)', '/r')).rejects.toBeInstanceOf(ErrorDePlataforma)
    await expect(web.abrirPasarela('no es una url', '/r')).rejects.toThrow(
      /La dirección de pago que recibimos no es válida/,
    )
    expect(assign).toHaveBeenCalledTimes(1)
  })

  it('abre un documento descargándolo con su nombre y tipo', async () => {
    const { ventana, createObjectURL, revokeObjectURL } = ventanaFalsa()
    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    const web = crearPlataformaWeb(ventana)

    await web.abrirDocumento(btoa('%PDF-1.7'), 'comprobante.pdf', 'application/pdf')

    expect(createObjectURL).toHaveBeenCalledTimes(1)
    const archivo = createObjectURL.mock.calls[0]?.[0] as Blob
    expect(archivo.type).toBe('application/pdf')
    expect(await archivo.text()).toBe('%PDF-1.7')
    expect(clic).toHaveBeenCalledTimes(1)
    const enlace = clic.mock.contexts[0] as HTMLAnchorElement
    expect(enlace.download).toBe('comprobante.pdf')
    expect(enlace.isConnected).toBe(false)
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:canal/1')
  })

  it('un documento dañado da un mensaje claro', async () => {
    const web = crearPlataformaWeb(ventanaFalsa().ventana)

    await expect(web.abrirDocumento('%%%no-base64%%%', 'x.pdf', 'application/pdf')).rejects.toThrow(
      /llegó dañado/,
    )
  })
})

describe('plataforma app', () => {
  function puenteFalso(respuestas: Record<string, unknown> = {}) {
    const InvokeDotNet = vi.fn<PuenteHybridWebView['InvokeDotNet']>((metodo) => Promise.resolve(respuestas[metodo] ?? null))
    return { puente: { InvokeDotNet }, InvokeDotNet }
  }

  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  it('llama al puente con los nombres del contrato', async () => {
    const { puente, InvokeDotNet } = puenteFalso({
      Plataforma: { os: 'android', version: '15', appVersion: '1.0.0' },
      LeerCredencialConBiometria: 'credencial',
      BiometriaDisponible: { disponible: true, tipo: 'huella' },
      AbrirPasarela: { resultado: 'returned', url: 'https://canal.example/pagos/1/retorno' },
      TokenPush: 'token-fcm',
    })
    const app = crearPlataformaApp(() => puente)

    expect(app.nombre).toBe('app')
    await expect(app.plataforma()).resolves.toEqual({ os: 'android', version: '15', appVersion: '1.0.0' })
    await app.guardarCredencial('secreto')
    await expect(app.leerCredencialConBiometria('Ingresar al canal')).resolves.toBe('credencial')
    await app.borrarCredencial()
    await expect(app.biometriaDisponible()).resolves.toEqual({ disponible: true, tipo: 'huella' })
    await expect(app.abrirPasarela('https://pasarela.example/1', 'https://canal.example/pagos/1/retorno')).resolves.toEqual({
      resultado: 'returned',
      url: 'https://canal.example/pagos/1/retorno',
    })
    await expect(app.tokenPush()).resolves.toBe('token-fcm')
    await app.abrirDocumento('JVBERi0=', 'comprobante.pdf', 'application/pdf')

    expect(InvokeDotNet.mock.calls).toEqual([
      [METODOS_DEL_PUENTE.plataforma, []],
      ['GuardarCredencial', ['secreto']],
      ['LeerCredencialConBiometria', ['Ingresar al canal']],
      ['BorrarCredencial', []],
      ['BiometriaDisponible', []],
      ['AbrirPasarela', ['https://pasarela.example/1', 'https://canal.example/pagos/1/retorno']],
      ['TokenPush', []],
      ['AbrirDocumento', ['JVBERi0=', 'comprobante.pdf', 'application/pdf']],
    ])
  })

  it('acepta las claves en PascalCase que produce C# por defecto', async () => {
    const { puente } = puenteFalso({ BiometriaDisponible: { Disponible: false, Tipo: null } })

    await expect(crearPlataformaApp(() => puente).biometriaDisponible()).resolves.toEqual({
      disponible: false,
      tipo: null,
    })
  })

  it('cancelar la biometría devuelve null', async () => {
    const { puente } = puenteFalso({ LeerCredencialConBiometria: null })

    await expect(crearPlataformaApp(() => puente).leerCredencialConBiometria('Ingresar')).resolves.toBeNull()
  })

  it('sin puente, avisa que la app no terminó de cargar', async () => {
    const error = await crearPlataformaApp(() => undefined)
      .tokenPush()
      .catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ErrorDePlataforma)
    expect((error as ErrorDePlataforma).code).toBe('Plataforma.PuenteNoDisponible')
    expect(console.error).toHaveBeenCalled()
  })

  it('una excepción de C# se informa y se convierte en un mensaje para la persona', async () => {
    const puente = { InvokeDotNet: vi.fn(() => Promise.reject(new Error('NullReferenceException'))) }

    const error = (await crearPlataformaApp(() => puente)
      .guardarCredencial('x')
      .catch((e: unknown) => e)) as ErrorDePlataforma

    expect(error.code).toBe('Plataforma.PuenteFallo')
    expect(error.message).toMatch(/^La app no pudo completar la operación/)
    expect(error.cause).toBeInstanceOf(Error)
    expect(console.error).toHaveBeenCalled()
  })

  it('una respuesta con otra forma no pasa en silencio', async () => {
    const { puente } = puenteFalso({ AbrirPasarela: { resultado: 'ok' } })

    const error = (await crearPlataformaApp(() => puente)
      .abrirPasarela('https://pasarela.example/1', '/r')
      .catch((e: unknown) => e)) as ErrorDePlataforma

    expect(error.code).toBe('Plataforma.RespuestaInesperada')
    expect(console.error).toHaveBeenCalled()
  })
})

describe('ganchos de la app (window.canal)', () => {
  it('guarda el enlace que llega antes de que alguien escuche y lo entrega al suscribirse', () => {
    instalarGanchosDelCanal(window)
    window.canal?.alAbrirEnlace('https://canal.example/pagos/1/retorno')

    const recibidos: string[] = []
    const dejarDeEscuchar = escucharEnlaces((url) => recibidos.push(url))
    window.canal?.alAbrirEnlace('https://canal.example/pagos/2/retorno')
    dejarDeEscuchar()

    expect(recibidos).toEqual(['https://canal.example/pagos/1/retorno', 'https://canal.example/pagos/2/retorno'])
  })

  it('registra y descarta un enlace vacío', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    instalarGanchosDelCanal(window)
    const recibidos: string[] = []
    const dejarDeEscuchar = escucharEnlaces((url) => recibidos.push(url))

    window.canal?.alAbrirEnlace('')
    dejarDeEscuchar()

    expect(recibidos).toEqual([])
    expect(error).toHaveBeenCalled()
  })
})

describe('rutaInternaDesdeEnlace', () => {
  it('convierte un enlace del canal en ruta interna', () => {
    expect(rutaInternaDesdeEnlace('https://canal.example/pagos/1/retorno?x=1', 'canal.example')).toBe(
      '/pagos/1/retorno?x=1',
    )
    expect(rutaInternaDesdeEnlace('/pagos/1/retorno', null)).toBe('/pagos/1/retorno')
  })

  it('descarta enlaces de otro sitio', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    expect(rutaInternaDesdeEnlace('https://otro.example/pagos/1', 'canal.example')).toBeNull()
    expect(rutaInternaDesdeEnlace('//otro.example/x', 'canal.example')).toBeNull()
  })
})
