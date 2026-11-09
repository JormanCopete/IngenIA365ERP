import { fireEvent, screen, waitFor } from '@testing-library/react'
import { instalarApiFalsa, montarRuta, perfilDePrueba, SESION } from '../pruebas/montaje'
import { establecerTokenDeSesion } from '../servicios/api'

function escribir(etiqueta: string | RegExp, valor: string) {
  fireEvent.change(screen.getByLabelText(etiqueta), { target: { value: valor } })
}

describe('identidad del asociado (US2)', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
    establecerTokenDeSesion(null)
  })

  it('activa la cuenta: documento, código, clave y textos, y pasa al selector', async () => {
    const api = instalarApiFalsa()
    api.responder('POST', '/activation/start', { cuerpo: { challengeId: 'd1', channels: [{ kind: 'email', masked: 'a•••@c•••.test' }] } })
    api.responder('POST', '/activation/send', { status: 202, cuerpo: { sentTo: 'a•••@c•••.test', expiresAt: SESION.idleExpiresAt } })
    api.responder('POST', '/activation/verify', {
      cuerpo: {
        activationToken: 'permiso', entityName: 'Cooperativa Uno',
        legalTexts: [{ kind: 'Terms', version: 2, content: 'Términos' }, { kind: 'DataPolicy', version: 1, content: 'Política' }],
      },
    })
    api.responder('POST', '/activation/complete', { status: 201, cuerpo: { session: SESION } })
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }, { tenantPublicId: 't2', displayName: 'Otra' }]) })

    montarRuta('/activar')
    escribir('Número de documento', '1.010.101.010')
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }))
    expect(await screen.findByText(/a•••@c•••\.test/)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }))
    await screen.findByLabelText('Código')
    escribir('Código', '123456')
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }))

    await screen.findByText(/Ahora crea tu clave/)
    escribir('Clave', 'Cafetal2026x')
    escribir('Repite la clave', 'Cafetal2026x')
    const activar = screen.getByRole('button', { name: 'Activar mi cuenta' })
    expect(activar).toBeDisabled()
    fireEvent.click(screen.getByLabelText(/acepto términos y condiciones/i))
    fireEvent.click(screen.getByLabelText(/acepto política de tratamiento de datos/i))
    fireEvent.click(activar)

    expect(await screen.findByRole('heading', { name: '¿Con qué cooperativa quieres continuar?' })).toBeInTheDocument()
    const completar = api.llamadas.find((l) => l.ruta === '/activation/complete')
    expect(completar?.cuerpo).toEqual({
      activationToken: 'permiso', password: 'Cafetal2026x',
      acceptances: [{ kind: 'Terms', version: 2 }, { kind: 'DataPolicy', version: 1 }],
    })
    expect(api.llamadas.find((l) => l.ruta === '/activation/start')?.cuerpo).toEqual({ documentType: 'CC', documentNumber: '1.010.101.010' })
  })

  it('sin datos de contacto en la entidad, indica acercarse a ella con su soporte (FR-019)', async () => {
    const api = instalarApiFalsa()
    api.responder('POST', '/activation/start', {
      cuerpo: { challengeId: 'd1', channels: [], needsEntityUpdate: true, entityName: 'Cooperativa Uno', support: { phone: '6025550000' } },
    })
    montarRuta('/activar')
    escribir('Número de documento', '123456')
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }))
    expect(await screen.findByText(/Cooperativa Uno no tiene registrado/)).toBeInTheDocument()
    expect(screen.getByText('Teléfono: 6025550000')).toBeInTheDocument()
  })

  it('en un dispositivo nuevo el ingreso pide un código y confía en el dispositivo', async () => {
    const api = instalarApiFalsa()
    api.responder('POST', '/auth/login', { cuerpo: { challengeId: 'd2', channels: [{ kind: 'email', masked: 'a•••@c•••.test' }] } })
    api.responder('POST', '/auth/login/send', { status: 202, cuerpo: { sentTo: 'a•••@c•••.test', expiresAt: SESION.idleExpiresAt } })
    api.responder('POST', '/auth/login/verify', { cuerpo: { session: SESION } })
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'A' }, { tenantPublicId: 't2', displayName: 'B' }]) })

    montarRuta('/ingreso?volver=%2Fperfil')
    escribir('Número de documento', '123456')
    escribir('Clave', 'Cafetal2026x')
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }))
    expect(await screen.findByRole('heading', { name: 'Confirma que eres tú' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }))
    await screen.findByLabelText('Código')
    escribir('Código', '654321')
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    await screen.findByRole('heading', { name: '¿Con qué cooperativa quieres continuar?' })
    expect(api.llamadas.find((l) => l.ruta === '/auth/login/verify')?.cuerpo).toEqual({ challengeId: 'd2', code: '654321', trustDevice: true })
  })

  it('un código incorrecto dice cuántos intentos quedan', async () => {
    const api = instalarApiFalsa()
    api.responder('POST', '/auth/login', { cuerpo: { challengeId: 'd3', channels: [{ kind: 'email', masked: 'x' }] } })
    api.responder('POST', '/auth/login/send', { status: 202, cuerpo: { sentTo: 'x', expiresAt: SESION.idleExpiresAt } })
    api.responder('POST', '/auth/login/verify', {
      status: 422, cuerpo: { code: 'Canal.Codigo.Incorrecto', message: 'El código no es correcto.', data: { intentosRestantes: 2 } },
    })
    montarRuta('/ingreso')
    escribir('Número de documento', '123456')
    escribir('Clave', 'Cafetal2026x')
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Enviar código' }))
    await screen.findByLabelText('Código')
    escribir('Código', '000000')
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('El código no es correcto.')
  })

  it('sin sesión, una ruta protegida lleva al ingreso con la ruta de regreso', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { status: 401, cuerpo: { code: 'Canal.Sesion.Requerida', message: 'Tu sesión terminó.' } })
    const { enrutador } = montarRuta('/perfil')
    await waitFor(() => expect(enrutador.state.location.pathname).toBe('/ingreso'))
    expect(enrutador.state.location.search).toBe('?volver=%2Fperfil')
  })

  it('con una sola cooperativa el selector entra directo', async () => {
    const api = instalarApiFalsa()
    let elegida: string | null = null
    api.responder('GET', '/me', () => ({ cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Única' }], elegida) }))
    api.responder('POST', '/session/tenant', (cuerpo) => {
      elegida = (cuerpo as { tenantPublicId: string }).tenantPublicId
      return { cuerpo: { tenant: {}, session: SESION } }
    })
    const { enrutador } = montarRuta('/cooperativas')
    await waitFor(() => expect(enrutador.state.location.pathname).toBe('/cooperativa'))
    expect(elegida).toBe('t1')
    expect(await screen.findByText('Estás en Única.')).toBeInTheDocument()
  })

  it('sin cooperativas habilitadas lo dice y deja salir', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([]) })
    montarRuta('/cooperativas')
    expect(await screen.findByRole('heading', { name: 'No tienes entidades activas' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Salir' })).toBeInTheDocument()
  })

  it('una ruta de cooperativa sin cooperativa elegida lleva al selector', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'A' }, { tenantPublicId: 't2', displayName: 'B' }]) })
    const { enrutador } = montarRuta('/cooperativa')
    await waitFor(() => expect(enrutador.state.location.pathname).toBe('/cooperativas'))
  })
})
