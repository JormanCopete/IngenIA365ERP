import { act, render, screen } from '@testing-library/react'
import { createMemoryRouter } from 'react-router'
import { App } from './App'
import { instalarGanchosDelCanal } from './plataforma'
import { rutas } from './rutas'

describe('App', () => {
  it('monta el armazón con la marca del canal', async () => {
    render(<App enrutador={createMemoryRouter(rutas, { initialEntries: ['/'] })} />)

    expect(await screen.findByRole('banner')).toHaveTextContent('Canal del asociado')
    expect(screen.getByRole('main')).toBeInTheDocument()
  })

  it('un enlace que llega desde la app (window.canal.alAbrirEnlace) lleva a su pantalla', async () => {
    instalarGanchosDelCanal(window)
    const enrutador = createMemoryRouter(rutas, { initialEntries: ['/'] })
    render(<App enrutador={enrutador} />)
    await screen.findByRole('heading', { name: 'Te damos la bienvenida' })

    act(() => {
      window.canal?.alAbrirEnlace(`${window.location.origin}/ayuda/preguntas`)
    })

    expect(await screen.findByRole('heading', { name: 'Preguntas frecuentes' })).toBeInTheDocument()
    expect(enrutador.state.location.pathname).toBe('/ayuda/preguntas')
  })

  it('ignora un enlace de otro sitio', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    instalarGanchosDelCanal(window)
    const enrutador = createMemoryRouter(rutas, { initialEntries: ['/'] })
    render(<App enrutador={enrutador} />)
    await screen.findByRole('heading', { name: 'Te damos la bienvenida' })

    act(() => {
      window.canal?.alAbrirEnlace('https://otro.example/ayuda/preguntas')
    })

    expect(enrutador.state.location.pathname).toBe('/')
    expect(error).toHaveBeenCalled()
    error.mockRestore()
  })
})
