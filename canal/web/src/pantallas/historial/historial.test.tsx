import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { instalarApiFalsa, montarRuta, perfilDePrueba } from '../../pruebas/montaje'
import type { EstadoDelIntento, Intento } from '../../servicios/pagos'

const abrirDocumento = vi.fn(async () => undefined)
vi.mock('../../plataforma', async (original) => {
  const real = await original<typeof import('../../plataforma')>()
  return { ...real, obtenerPlataforma: () => ({ ...real.crearPlataformaWeb(), abrirDocumento }) }
})

const pesos = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(v).replace(/\s/g, ' ')

function intento(n: number, status: EstadoDelIntento, extra: Partial<Intento> = {}): Intento {
  return {
    attemptId: `a${n}`, reference: `ABC26120100000${n}0X`, status, total: 10_000 * n, currency: 'COP', items: [],
    createdAt: `2026-12-0${n}T15:00:00Z`, updatedAt: `2026-12-0${n}T15:00:00Z`, expiresAt: '', processUrl: null, approval: null, message: null,
    erpOutcome: 'not_sent', erpReceiptPublicId: null, support: {}, ...extra,
  }
}

describe('historial de pagos (US8)', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('muestra seis pagos en el orden del servicio con todos los campos y el comprobante del aplicado (FR-054)', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }], 't1') })
    const aprobado = intento(4, 'approved', {
      erpOutcome: 'applied', erpReceiptPublicId: 'r4',
      approval: { internalReference: 1, authorization: '3324451', receipt: '', franchise: '_PSE_', paymentMethodName: 'PSE', issuerName: '',
        approvedAt: '2026-12-04T15:20:00Z' },
    })
    const items = [intento(6, 'expired'), intento(5, 'failed'), aprobado, intento(3, 'rejected'), intento(2, 'pending'), intento(1, 'created')]
    api.responder('GET', '/payments', (_c, url) => {
      expect(url).toContain('limit=20')
      return { cuerpo: { items, support: {} } }
    })
    api.responder('GET', '/payments/a4/receipt', { cuerpo: {} })
    montarRuta('/cooperativa/pagos')

    const pagos = await screen.findAllByRole('article')
    expect(pagos).toHaveLength(6)
    expect(pagos.map((p) => p.getAttribute('aria-label'))).toEqual(items.map((i) => `Pago ${i.reference}`))
    const fila = pagos[2] as HTMLElement
    expect(within(fila).getByText('Aprobado')).toBeInTheDocument()
    expect(within(fila).getByText(`${pesos(40_000)} COP`.replace(/\s/g, ' '), { normalizer: (t) => t.replace(/\s/g, ' ') })).toBeInTheDocument()
    expect(within(fila).getByText('Autorización / CUS').closest('div')).toHaveTextContent('3324451')
    expect(within(fila).getByText('Fecha y hora').closest('div')).toHaveTextContent('2026')
    expect(within(pagos[0] as HTMLElement).queryByRole('button', { name: 'Comprobante' })).not.toBeInTheDocument()

    fireEvent.click(within(fila).getByRole('button', { name: 'Comprobante' }))
    await waitFor(() => expect(abrirDocumento).toHaveBeenCalledWith(expect.any(String), `comprobante-${aprobado.reference}.pdf`, 'application/pdf'))
  })

  it('sin pagos lo dice', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }], 't1') })
    api.responder('GET', '/payments', { cuerpo: { items: [], support: {} } })
    montarRuta('/cooperativa/pagos')
    expect(await screen.findByText(/Todavía no has hecho pagos/)).toBeInTheDocument()
  })
})
