import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { instalarApiFalsa, montarRuta, perfilDePrueba } from '../../pruebas/montaje'
import type { Concepto } from '../../servicios/conceptos'
import type { Intento } from '../../servicios/pagos'

const abrirPasarela = vi.fn(async () => ({ resultado: 'returned' as const }))
vi.mock('../../plataforma', async (original) => {
  const real = await original<typeof import('../../plataforma')>()
  return { ...real, obtenerPlataforma: () => ({ ...real.crearPlataformaWeb(), abrirPasarela }) }
})

const DESTINO = { destinationPublicId: 'd1', sitePublicId: 's1', name: 'Avalpay', offerPse: true, offerCard: false }

function concepto(parcial: Partial<Concepto>): Concepto {
  return {
    conceptKey: 'LOAN:1', kind: 'Loan', lineCode: 'CONSUMO', description: 'Crédito CR-1 · Consumo', productPublicId: 'p1',
    productNumber: 'CR-1', balance: 5_400_000, pastDue: 320_000, defaultInterest: 12_500, cycleInstallment: 280_000,
    dueToday: 332_500, payoffAmount: 5_450_000, nextDueDate: '2026-12-15', daysPastDue: 12, destinationPublicId: 'd1',
    payable: true, modes: { due: true, partial: { minimum: 20_000 }, prepayment: { defaultChoice: 'ReduceInstallment' }, payoff: true },
    canCombine: true, installment: null, blockReasons: [], ...parcial,
  }
}

const APORTES = concepto({
  conceptKey: 'ACCOUNT:2', kind: 'Contribution', description: 'Aportes sociales', dueToday: 50_000, payoffAmount: null,
  modes: { due: true, partial: null, prepayment: null, payoff: false },
})

const pesos = (v: number) =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(v).replace(/\s/g, ' ')

/** El nombre del botón de pagar, sin importar el espacio duro del formato de moneda. */
const botonPagar = (v: number) => (nombre: string) => nombre.replace(/\s/g, ' ') === `Pagar ${pesos(v)}`

function intento(parcial: Partial<Intento>): Intento {
  return {
    attemptId: 'a1', reference: 'ABC2611090000000A7', status: 'pending', total: 382_500, currency: 'COP', items: [],
    createdAt: '2026-11-09T15:00:00Z', updatedAt: '2026-11-09T15:00:00Z', expiresAt: '2026-11-09T15:15:00Z',
    processUrl: 'https://checkout.ejemplo.test/session/1', approval: null, message: null, erpOutcome: 'not_sent',
    erpReceiptPublicId: null, support: { phone: '6025550000', email: 'soporte@coop.test' }, ...parcial,
  }
}

describe('pago en línea (US4)', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    abrirPasarela.mockClear()
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  function conConceptos(items: Concepto[], pendientes: unknown[] = []) {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }], 't1') })
    api.responder('GET', '/payables', { cuerpo: { asOf: '2026-12-05', items, notices: [], destinations: [DESTINO] } })
    api.responder('GET', '/payments/pending', { cuerpo: { items: pendientes, support: { phone: '6025550000' } } })
    return api
  }

  it('arma el pago, muestra el resumen y abre la pasarela con una sola clave (FR-033, FR-036)', async () => {
    const api = conConceptos([concepto({}), APORTES])
    api.responder('POST', '/payments', { status: 201, cuerpo: intento({}) })
    const { enrutador } = montarRuta('/cooperativa/pagar')

    fireEvent.click(await screen.findByRole('checkbox', { name: 'Crédito CR-1 · Consumo' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Aportes sociales' }))
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }))

    const tabla = await screen.findByRole('table')
    expect(within(tabla).getByText(pesos(332_500))).toBeInTheDocument()
    expect(within(tabla).getByText(pesos(382_500))).toBeInTheDocument()
    expect(screen.getByText('Avalpay')).toBeInTheDocument()

    const pagar = screen.getByRole('button', { name: botonPagar(382_500) })
    fireEvent.click(pagar)
    fireEvent.click(pagar)
    await waitFor(() => expect(abrirPasarela).toHaveBeenCalledTimes(1))
    const posts = api.llamadas.filter((l) => l.metodo === 'POST' && l.ruta === '/payments')
    expect(posts).toHaveLength(1)
    expect(posts[0]?.cabeceras.get('Idempotency-Key')).toBeTruthy()
    expect(posts[0]?.cuerpo).toEqual({
      items: [
        { conceptKey: 'LOAN:1', modality: 'Due', amount: 332_500, prepaymentChoice: null },
        { conceptKey: 'ACCOUNT:2', modality: 'Due', amount: 50_000, prepaymentChoice: null },
      ],
    })
    expect(abrirPasarela).toHaveBeenCalledWith('https://checkout.ejemplo.test/session/1', expect.stringMatching(/\/pagos\/a1\/retorno$/))
    await waitFor(() => expect(enrutador.state.location.pathname).toBe('/pagos/a1/retorno'))
  })

  it('valida el mínimo del pago parcial y lleva la elección del abono (FR-032a)', async () => {
    const api = conConceptos([concepto({})])
    api.responder('POST', '/payables/simulate', {
      cuerpo: {
        conceptKey: 'LOAN:1', modality: 'Prepayment', amount: 500_000, allowed: true, surplus: 0, newPlan: null, prepaymentChoice: 'ReduceInstallment',
        alternatives: [
          { choice: 'ReduceTerm', newPlan: { installment: 280_000, remainingInstallments: 14, nextDueDate: '2026-12-15' }, surplus: 0 },
          { choice: 'ReduceInstallment', newPlan: { installment: 250_000, remainingInstallments: 18, nextDueDate: '2026-12-15' }, surplus: 0 },
        ],
        notices: [],
      },
    })
    api.responder('POST', '/payments', { status: 201, cuerpo: intento({}) })
    montarRuta('/cooperativa/pagar?concepto=LOAN%3A1')

    expect(await screen.findByRole('checkbox', { name: 'Crédito CR-1 · Consumo' })).toBeChecked()
    fireEvent.click(screen.getByRole('radio', { name: 'Otro valor' }))
    fireEvent.change(screen.getByLabelText('Valor en pesos'), { target: { value: '1000' } })
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }))
    expect(await screen.findByText(`El mínimo para este producto es ${pesos(20_000)}.`)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('radio', { name: 'Abono a capital' }))
    fireEvent.change(screen.getByLabelText('Valor en pesos'), { target: { value: '500000' } })
    // La opción de la cooperativa va marcada; la simulación muestra el plan de cada una.
    expect(screen.getByRole('radio', { name: /Reducir el valor de la cuota/ })).toBeChecked()
    fireEvent.click(screen.getByRole('button', { name: 'Ver cómo queda' }))
    expect(await screen.findByText(/14 cuotas/)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('radio', { name: /Reducir el plazo/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }))
    fireEvent.click(await screen.findByRole('button', { name: botonPagar(500_000) }))

    await waitFor(() => expect(api.llamadas.some((l) => l.metodo === 'POST' && l.ruta === '/payments')).toBe(true))
    const post = api.llamadas.find((l) => l.metodo === 'POST' && l.ruta === '/payments')
    expect(post?.cuerpo).toEqual({ items: [{ conceptKey: 'LOAN:1', modality: 'Prepayment', amount: 500_000, prepaymentChoice: 'ReduceTerm' }] })
  })

  it('un concepto con pago en proceso no se puede elegir y muestra la referencia y el contacto (FR-041)', async () => {
    conConceptos([concepto({}), APORTES], [
      { conceptKey: 'LOAN:1', attemptId: 'a0', reference: 'ABC2611090000000Z1', total: 332_500, status: 'pending', approval: null, createdAt: '' },
    ])
    montarRuta('/cooperativa/pagar')

    expect(await screen.findByText('ABC2611090000000Z1')).toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: 'Crédito CR-1 · Consumo' })).not.toBeInTheDocument()
    expect(screen.getByText(/6025550000/)).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Aportes sociales' })).toBeEnabled()
  })

  it('con dos destinos pide elegir cuál pagar (FR-032)', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }], 't1') })
    api.responder('GET', '/payables', {
      cuerpo: {
        asOf: '2026-12-05', notices: [],
        items: [concepto({}), { ...APORTES, destinationPublicId: 'd2' }],
        destinations: [DESTINO, { ...DESTINO, destinationPublicId: 'd2', name: 'Otra pasarela' }],
      },
    })
    api.responder('GET', '/payments/pending', { cuerpo: { items: [], support: {} } })
    montarRuta('/cooperativa/pagar')

    expect(await screen.findByText(/se pagan en grupos separados/)).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Crédito CR-1 · Consumo' })).toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: 'Aportes sociales' })).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('radio', { name: /Otra pasarela/ }))
    expect(await screen.findByRole('checkbox', { name: 'Aportes sociales' })).toBeInTheDocument()
    expect(screen.queryByRole('checkbox', { name: 'Crédito CR-1 · Consumo' })).not.toBeInTheDocument()
  })

  it('pide aceptar los textos nuevos y el código, y reintenta con la misma clave (FR-034, FR-009)', async () => {
    const api = conConceptos([APORTES])
    let intentos = 0
    api.responder('POST', '/payments', (cuerpo) => {
      intentos += 1
      const c = cuerpo as { acceptances?: unknown[]; otpChallengeId?: string }
      if (!c.acceptances) {
        return { status: 409, cuerpo: { code: 'Canal.Pago.TextosPorAceptar', message: 'Acepta.', data: { legalTexts: [{ kind: 'Terms', version: 3, content: 'Nuevos términos' }] } } }
      }
      if (!c.otpChallengeId) return { status: 403, cuerpo: { code: 'Canal.Pago.CodigoRequerido', message: 'Código.' } }
      return { status: 201, cuerpo: intento({ total: 50_000 }) }
    })
    api.responder('POST', '/payments/otp', { cuerpo: { challengeId: 'c1', channels: [{ kind: 'email', masked: 'a•••@c•••.test' }] } })
    api.responder('POST', '/payments/otp/send', { status: 202, cuerpo: { sentTo: 'a•••@c•••.test', expiresAt: '' } })
    api.responder('POST', '/payments/otp/verify', { cuerpo: { verified: true } })
    montarRuta('/cooperativa/pagar')

    fireEvent.click(await screen.findByRole('checkbox', { name: 'Aportes sociales' }))
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }))
    const pagar = await screen.findByRole('button', { name: botonPagar(50_000) })
    fireEvent.click(pagar)

    const casilla = await screen.findByRole('checkbox', { name: /Leí y acepto términos y condiciones \(versión 3\)/ })
    expect(pagar).toBeDisabled()
    fireEvent.click(casilla)
    fireEvent.click(pagar)

    fireEvent.click(await screen.findByRole('button', { name: 'Enviar código' }))
    fireEvent.change(await screen.findByLabelText('Código'), { target: { value: '123456' } })
    fireEvent.click(screen.getByRole('button', { name: 'Verificar código' }))
    await waitFor(() => expect(pagar).toBeEnabled())
    fireEvent.click(pagar)

    await waitFor(() => expect(abrirPasarela).toHaveBeenCalled())
    const posts = api.llamadas.filter((l) => l.metodo === 'POST' && l.ruta === '/payments')
    expect(intentos).toBe(3)
    const claves = new Set(posts.map((p) => p.cabeceras.get('Idempotency-Key')))
    expect(claves.size).toBe(1)
    expect(posts[2]?.cuerpo).toMatchObject({ acceptances: [{ kind: 'Terms', version: 3 }], otpChallengeId: 'c1' })
  })

  it('el retorno consulta al servicio y muestra referencia, estado, valor y autorización (FR-038)', async () => {
    const api = conConceptos([])
    api.responder('GET', '/payments/a1', (_c, url) => {
      expect(url).toContain('return=1')
      return {
        cuerpo: intento({
          status: 'approved', processUrl: null,
          approval: { internalReference: 1, authorization: '3324451', receipt: '1', franchise: '_PSE_', paymentMethodName: 'PSE',
            issuerName: 'Banco de pruebas', approvedAt: '2026-11-09T15:20:00Z' },
        }),
      }
    })
    montarRuta('/pagos/a1/retorno?status=APPROVED')

    expect(await screen.findByText('Pago aprobado.')).toBeInTheDocument()
    const fila = (e: string) => screen.getByText(e).closest('div')
    expect(fila('Referencia')).toHaveTextContent('ABC2611090000000A7')
    expect(fila('Valor')).toHaveTextContent(`${pesos(382_500)} (COP)`)
    expect(fila('Autorización / CUS')).toHaveTextContent('3324451')
    expect(fila('Medio de pago')).toHaveTextContent('PSE')
  })

  it('un pendiente lo dice con el contacto de la cooperativa', async () => {
    const api = conConceptos([])
    api.responder('GET', '/payments/a1', { cuerpo: intento({}) })
    montarRuta('/pagos/a1/retorno')

    expect(await screen.findByText('Pago pendiente.')).toBeInTheDocument()
    expect(screen.getByText(/No vuelvas a pagar estos productos/)).toBeInTheDocument()
    expect(screen.getByText(/soporte@coop.test/)).toBeInTheDocument()
  })

  it('la ayuda muestra las preguntas de la cooperativa y de la pasarela (FR-057)', async () => {
    const api = conConceptos([])
    api.responder('GET', '/legal/faq', {
      cuerpo: {
        texts: [
          { kind: 'Faq', version: 2, content: '¿Cómo activo mi cuenta?', publishedAt: '2026-11-01T00:00:00Z' },
          { kind: 'GatewayFaq', version: 1, content: '¿Qué es PSE?', publishedAt: '2026-11-01T00:00:00Z' },
        ],
      },
    })
    montarRuta('/ayuda/preguntas')

    expect(await screen.findByRole('heading', { name: 'Sobre los pagos en línea' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Sobre el canal y tu cooperativa' })).toBeInTheDocument()
    expect(screen.getByText('¿Qué es PSE?')).toBeInTheDocument()
  })
})
