import { act, fireEvent, render, screen } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { AvisoDeSesion } from './AvisoDeSesion'
import { ProveedorDeMarca } from '../marca/ContextoDeMarca'
import { crearClienteDeConsultas } from '../servicios/consultas'
import { instalarApiFalsa } from '../pruebas/montaje'

describe('AvisoDeSesion (T077)', () => {
  let reloj = 0
  const ahora = () => reloj

  beforeEach(() => {
    vi.useFakeTimers()
    reloj = Date.parse('2026-12-05T15:00:00Z')
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
  })
  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  function montar(maximoEnMinutos = 480) {
    const sesion = {
      inactivityMinutes: 5,
      idleExpiresAt: new Date(reloj + 5 * 60_000).toISOString(),
      expiresAt: new Date(reloj + maximoEnMinutos * 60_000).toISOString(),
      tenantSelected: true,
    }
    const enrutador = createMemoryRouter(
      [
        { path: '/cooperativa', element: <AvisoDeSesion sesion={sesion} ahora={ahora} /> },
        { path: '/ingreso', element: <p>Pantalla de ingreso</p> },
      ],
      { initialEntries: ['/cooperativa'] },
    )
    render(
      <QueryClientProvider client={crearClienteDeConsultas()}>
        <ProveedorDeMarca>
          <RouterProvider router={enrutador} />
        </ProveedorDeMarca>
      </QueryClientProvider>,
    )
    return enrutador
  }

  async function avanzar(ms: number) {
    reloj += ms
    await act(async () => {
      await vi.advanceTimersByTimeAsync(ms)
    })
  }

  it('avisa un minuto antes, «Seguir trabajando» renueva y a cero sale al ingreso con la ruta', async () => {
    const api = instalarApiFalsa()
    api.responder('POST', '/session/keepalive', { cuerpo: {} })
    api.responder('POST', '/auth/logout', { status: 204 })
    const enrutador = montar()

    await avanzar(3 * 60_000)
    expect(screen.queryByRole('alertdialog')).toBeNull()
    await avanzar(45_000) // corte a las 4:40 (5 min menos el margen de 20 s): a las 3:45 queda menos de un minuto
    expect(screen.getByRole('alertdialog')).toHaveTextContent('¿Sigues ahí?')

    fireEvent.click(screen.getByRole('button', { name: 'Seguir trabajando' }))
    await avanzar(1000)
    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(api.llamadas.some((l) => l.ruta === '/session/keepalive')).toBe(true)

    await avanzar(5 * 60_000)
    expect(enrutador.state.location.pathname).toBe('/ingreso')
    expect(enrutador.state.location.search).toContain('motivo=inactividad')
    expect(enrutador.state.location.search).toContain('volver=%2Fcooperativa')
    expect(api.llamadas.some((l) => l.ruta === '/auth/logout')).toBe(true)
  })

  it('al llegar al máximo no ofrece seguir', async () => {
    const api = instalarApiFalsa()
    api.responder('POST', '/auth/logout', { status: 204 })
    montar(4)
    await avanzar(3 * 60_000 + 1000)
    expect(screen.getByRole('alertdialog')).toHaveTextContent('Tu sesión está por terminar')
    expect(screen.queryByRole('button', { name: 'Seguir trabajando' })).toBeNull()
  })
})
