import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { instalarApiFalsa, montarRuta, perfilDePrueba } from '../../pruebas/montaje'

describe('crédito en línea (US13)', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('simula con la línea publicada y envía la solicitud con su llave', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }], 't1') })
    api.responder('GET', '/credit/lines', { cuerpo: { lines: [{ publicId: 'l1', code: 'CON', name: 'Consumo' }] } })
    api.responder('GET', '/credit/applications', { cuerpo: { applications: [] } })
    api.responder('POST', '/credit/simulate', {
      cuerpo: {
        creditLineName: 'Consumo', amount: 5000000, installmentCount: 24, effectiveAnnualRate: 0.2,
        totals: { toPay: 6100000, interest: 1100000, insurance: 0 }, installments: [{ number: 1, dueDate: '2027-01-01', total: 254200 }], warnings: [],
      },
    })
    api.responder('POST', '/credit/applications', { cuerpo: { publicId: 's1', number: null, status: 'Draft', createdAt: '2026-12-01T15:00:00Z',
      creditLineName: 'Consumo', amount: 5000000, installments: 24, purpose: 'Estudio' } })
    montarRuta('/cooperativa/credito')

    fireEvent.change(await screen.findByLabelText('Monto (pesos)'), { target: { value: '5.000.000' } })
    fireEvent.click(screen.getByRole('button', { name: 'Simular' }))
    const simulacion = await screen.findByLabelText('Simulación')
    expect(within(simulacion).getByText(/24 cuotas/)).toBeInTheDocument()
    fireEvent.change(within(simulacion).getByLabelText('¿Para qué es el crédito? (opcional)'), { target: { value: 'Estudio' } })
    fireEvent.click(within(simulacion).getByRole('button', { name: 'Enviar solicitud' }))

    expect(await screen.findByText(/Recibimos tu solicitud/)).toBeInTheDocument()
    const envio = api.llamadas.find((l) => l.metodo === 'POST' && l.ruta === '/credit/applications')
    expect(envio?.cuerpo).toEqual({ creditLinePublicId: 'l1', amount: 5000000, installments: 24, purpose: 'Estudio' })
    expect(envio?.cabeceras.get('Idempotency-Key')).toBeTruthy()
    await waitFor(() => expect(api.llamadas.filter((l) => l.ruta === '/credit/applications' && l.metodo === 'GET').length).toBeGreaterThan(1))
  })
})
