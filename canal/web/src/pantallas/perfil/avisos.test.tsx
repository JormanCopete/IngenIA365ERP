import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { instalarApiFalsa, montarRuta, perfilDePrueba } from '../../pruebas/montaje'
import { llaveComoBytes } from '../../servicios/avisos'

const preferencias = [
  { kind: 'due_reminder', email: true, push: true, locked: false },
  { kind: 'payment_applied', email: true, push: true, locked: false },
  { kind: 'payment_rejected', email: true, push: true, locked: false },
  { kind: 'pending_resolved', email: true, push: true, locked: false },
  { kind: 'security', email: true, push: true, locked: true },
]

describe('avisos del perfil (US10)', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('apaga un aviso por correo y deja la seguridad bloqueada (FR-056)', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }], 't1') })
    api.responder('GET', '/notifications/preferences', { cuerpo: { preferences: preferencias, webPush: true } })
    let guardado: unknown
    api.responder('PUT', '/notifications/preferences', (c) => {
      guardado = c
      return { cuerpo: { preferences: preferencias, webPush: true } }
    })
    montarRuta('/perfil')

    const tarjeta = await screen.findByLabelText('Avisos', { selector: 'div' })
    const seguridad = await within(tarjeta).findByLabelText(/Seguridad de tu cuenta .* por correo/)
    expect(seguridad).toBeDisabled()
    fireEvent.click(within(tarjeta).getByLabelText('Cuota próxima a vencer por correo'))
    fireEvent.click(within(tarjeta).getByRole('button', { name: 'Guardar avisos' }))

    await waitFor(() => expect(guardado).toEqual({ preferences: [{ kind: 'due_reminder', email: false, push: true }] }))
    expect(await within(tarjeta).findByText('Guardamos tus preferencias de aviso.')).toBeInTheDocument()
  })

  it('convierte la llave VAPID de base64url', () => {
    expect(Array.from(llaveComoBytes('AQID_-8'))).toEqual([1, 2, 3, 255, 239])
  })
})
