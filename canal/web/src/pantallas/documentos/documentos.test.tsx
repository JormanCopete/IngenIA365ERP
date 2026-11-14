import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { instalarApiFalsa, montarRuta, perfilDePrueba } from '../../pruebas/montaje'

const abrirDocumento = vi.fn(async () => undefined)
vi.mock('../../plataforma', async (original) => {
  const real = await original<typeof import('../../plataforma')>()
  return { ...real, obtenerPlataforma: () => ({ ...real.crearPlataformaWeb(), abrirDocumento }) }
})

describe('extractos y certificados (US9)', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('lista lo publicado y descarga el extracto con su período (FR-055)', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }], 't1') })
    api.responder('GET', '/documents', {
      cuerpo: {
        documents: [
          { kind: 'Statement', productPublicId: 'p1', productNumber: 'CR-1', description: 'Extracto del crédito CR-1', parameter: 'period' },
          { kind: 'ContributionCertificate', productPublicId: null, productNumber: null, description: 'Certificado de aportes', parameter: 'none' },
        ],
      },
    })
    let pedido: Record<string, unknown> | undefined
    api.responder('POST', '/documents', (c) => {
      pedido = c as Record<string, unknown>
      return { cuerpo: {} }
    })
    montarRuta('/cooperativa/documentos')

    const extracto = await screen.findByRole('article', { name: 'Extracto del crédito CR-1' })
    expect(screen.getByRole('article', { name: 'Certificado de aportes' })).toBeInTheDocument()
    fireEvent.change(within(extracto).getByLabelText('Desde'), { target: { value: '2026-09-01' } })
    fireEvent.click(within(extracto).getByRole('button', { name: 'Descargar PDF' }))

    await waitFor(() => expect(abrirDocumento).toHaveBeenCalledWith(expect.any(String), expect.stringMatching(/^extracto-CR-1-2026-09-01_/), 'application/pdf'))
    expect(pedido).toMatchObject({ kind: 'Statement', productPublicId: 'p1', from: '2026-09-01' })
  })

  it('sin documentos publicados lo dice', async () => {
    const api = instalarApiFalsa()
    api.responder('GET', '/me', { cuerpo: perfilDePrueba([{ tenantPublicId: 't1', displayName: 'Cooperativa Uno' }], 't1') })
    api.responder('GET', '/documents', { cuerpo: { documents: [] } })
    montarRuta('/cooperativa/documentos')
    expect(await screen.findByText(/no tiene documentos publicados/)).toBeInTheDocument()
  })
})
