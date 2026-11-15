import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { instalarApiFalsa, montarRuta, perfilDePrueba } from '../../pruebas/montaje'
import type { Concepto } from '../../servicios/conceptos'

function concepto(parcial: Partial<Concepto>): Concepto {
  return {
    conceptKey: 'LOAN:11111111-1111-1111-1111-111111111111',
    kind: 'Loan',
    lineCode: 'CONSUMO',
    description: 'Crédito CR-000123 · Consumo',
    productPublicId: '11111111-1111-1111-1111-111111111111',
    productNumber: 'CR-000123',
    balance: 5_400_000,
    pastDue: 320_000,
    defaultInterest: 12_500,
    cycleInstallment: 280_000,
    dueToday: 332_500,
    payoffAmount: 5_450_000,
    nextDueDate: '2026-12-15',
    daysPastDue: 12,
    destinationPublicId: 'd1',
    payable: true,
    modes: { due: true, partial: null, prepayment: null, payoff: true },
    canCombine: true,
    installment: null,
    blockReasons: [],
    ...parcial,
  }
}

const APORTES = concepto({
  conceptKey: 'ACCOUNT:22222222-2222-2222-2222-222222222222',
  kind: 'Contribution',
  lineCode: 'APORTES',
  description: 'Aportes AP-1 · Aportes sociales',
  balance: 900_000,
  pastDue: 0,
  defaultInterest: 0,
  cycleInstallment: 50_000,
  dueToday: 50_000,
  payoffAmount: null,
  daysPastDue: 0,
  nextDueDate: '2026-12-30',
  installment: { period: '2026-12', dueDate: '2026-12-30', amount: 50_000, pending: 50_000 },
})

const pesos = (v: number) => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(v).replace(/\s/g, ' ')

describe('conceptos del asociado (US3)', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  function conPerfil() {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }], 't1') })
    return api
  }

  it('lista los conceptos por tipo con la fecha de cálculo', async () => {
    const api = conPerfil()
    api.responder('GET', '/payables', {
      cuerpo: { asOf: '2026-12-05', items: [APORTES, concepto({ payable: false })], notices: ['Captaciones no respondió a tiempo.'] },
    })
    montarRuta('/cooperativa')

    expect(await screen.findByText('Valores calculados al 5 de diciembre de 2026.')).toBeInTheDocument()
    const creditos = screen.getByRole('region', { name: 'Créditos' })
    expect(within(creditos).getByText('Crédito CR-000123 · Consumo')).toBeInTheDocument()
    expect(within(creditos).getByText(pesos(332_500))).toBeInTheDocument()
    expect(within(creditos).getByText('12 días de mora')).toBeInTheDocument()
    expect(within(creditos).getByText('Sólo consulta')).toBeInTheDocument()
    const aportes = screen.getByRole('region', { name: 'Aportes' })
    expect(within(aportes).getByText('Aportes AP-1 · Aportes sociales')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Ahorros' })).not.toBeInTheDocument()
    expect(screen.getByText('Captaciones no respondió a tiempo.')).toBeInTheDocument()
  })

  it('el detalle de un crédito muestra saldo, vencido, mora, cuota, próxima fecha, exigible y cancelación', async () => {
    const api = conPerfil()
    api.responder('GET', '/payables', { cuerpo: { asOf: '2026-12-05', items: [concepto({})], notices: [] } })
    const { enrutador } = montarRuta('/cooperativa')

    fireEvent.click(await screen.findByText('Crédito CR-000123 · Consumo'))
    await waitFor(() => expect(enrutador.state.location.pathname).toBe('/cooperativa/conceptos/LOAN%3A11111111-1111-1111-1111-111111111111'))
    expect(await screen.findByRole('heading', { name: 'Crédito CR-000123 · Consumo' })).toBeInTheDocument()
    const fila = (etiqueta: string) => screen.getByText(etiqueta).closest('div')
    expect(fila('Saldo')).toHaveTextContent(pesos(5_400_000))
    expect(fila('Valor vencido')).toHaveTextContent(pesos(320_000))
    expect(fila('Interés de mora')).toHaveTextContent(pesos(12_500))
    expect(fila('Cuota')).toHaveTextContent(pesos(280_000))
    expect(fila('Próxima fecha de pago')).toHaveTextContent('15 de diciembre de 2026')
    expect(fila('Para pagar hoy')).toHaveTextContent(pesos(332_500))
    expect(fila('Para cancelar el crédito')).toHaveTextContent(pesos(5_450_000))
  })

  it('el detalle de aportes muestra la cuota pendiente', async () => {
    const api = conPerfil()
    api.responder('GET', '/payables', { cuerpo: { asOf: '2026-12-05', items: [APORTES], notices: [] } })
    montarRuta(`/cooperativa/conceptos/${encodeURIComponent(APORTES.conceptKey)}`)

    expect(await screen.findByRole('heading', { name: 'Aportes AP-1 · Aportes sociales' })).toBeInTheDocument()
    expect(screen.getByText('Cuota pendiente').closest('div')).toHaveTextContent(`2026-12: ${pesos(50_000)}`)
    expect(screen.queryByText('Para cancelar el crédito')).not.toBeInTheDocument()
  })

  it('un concepto que ya no está lo dice', async () => {
    const api = conPerfil()
    api.responder('GET', '/payables', { cuerpo: { asOf: '2026-12-05', items: [], notices: [] } })
    montarRuta('/cooperativa/conceptos/LOAN%3Aotro')
    expect(await screen.findByRole('heading', { name: 'Producto no disponible' })).toBeInTheDocument()
  })

  it('sin productos lo dice', async () => {
    const api = conPerfil()
    api.responder('GET', '/payables', { cuerpo: { asOf: '2026-12-05', items: [], notices: [] } })
    montarRuta('/cooperativa')
    expect(await screen.findByText(/No tienes productos para consultar/)).toBeInTheDocument()
  })

  it('con el ERP caído dice «información no disponible» y no muestra valores (FR-031)', async () => {
    const api = conPerfil()
    let llamadas = 0
    api.responder('GET', '/payables', () => {
      llamadas += 1
      return { status: 503, cuerpo: { code: 'Canal.Entidad.NoDisponible', message: 'No disponible.' } }
    })
    montarRuta('/cooperativa')

    expect(await screen.findByRole('heading', { name: 'Información no disponible' }, { timeout: 4000 })).toBeInTheDocument()
    expect(screen.queryByText(/Valores calculados/)).not.toBeInTheDocument()
    const antes = llamadas
    fireEvent.click(screen.getByRole('button', { name: 'Intentar de nuevo' }))
    await waitFor(() => expect(llamadas).toBeGreaterThan(antes))
  })
})
