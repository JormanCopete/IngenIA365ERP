import { ErrorDelCanal, MENSAJE_SIN_CONEXION } from './api'
import { crearClienteDeConsultas, debeReintentar, MENSAJE_ERROR_INESPERADO, mensajeParaElUsuario } from './consultas'
import { ErrorDePlataforma } from '../plataforma'

const errorConEstado = (status: number) =>
  new ErrorDelCanal({ code: 'Canal.Prueba', message: 'Mensaje del servidor.', status })

describe('consultas', () => {
  it('no reintenta un rechazo 4xx', () => {
    expect(debeReintentar(0, errorConEstado(400))).toBe(false)
    expect(debeReintentar(0, errorConEstado(401))).toBe(false)
    expect(debeReintentar(0, errorConEstado(409))).toBe(false)
  })

  it('reintenta una sola vez un fallo de red o del servidor', () => {
    expect(debeReintentar(0, errorConEstado(0))).toBe(true)
    expect(debeReintentar(0, errorConEstado(503))).toBe(true)
    expect(debeReintentar(1, errorConEstado(503))).toBe(false)
  })

  it('no reintenta un error que no viene de la API (un defecto de la aplicación)', () => {
    expect(debeReintentar(0, new TypeError('x is undefined'))).toBe(false)
  })

  it('el mensaje para la persona es el del servidor o uno accionable', () => {
    expect(mensajeParaElUsuario(errorConEstado(409))).toBe('Mensaje del servidor.')
    expect(
      mensajeParaElUsuario(new ErrorDelCanal({ code: 'Canal.SinConexion', message: MENSAJE_SIN_CONEXION, status: 0 })),
    ).toBe(MENSAJE_SIN_CONEXION)
    expect(mensajeParaElUsuario(new ErrorDePlataforma('Plataforma.X', 'Texto de la plataforma.'))).toBe(
      'Texto de la plataforma.',
    )
    expect(mensajeParaElUsuario(new Error('undefined is not a function'))).toBe(MENSAJE_ERROR_INESPERADO)
  })

  it('el cliente no reintenta operaciones y no tiene persistencia', () => {
    const cliente = crearClienteDeConsultas()
    const opciones = cliente.getDefaultOptions()

    expect(opciones.mutations?.retry).toBe(false)
    expect(opciones.queries?.retry).toBe(debeReintentar)
    expect(localStorage.length).toBe(0)
  })
})
