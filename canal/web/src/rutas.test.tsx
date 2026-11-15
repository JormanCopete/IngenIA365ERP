import { useEffect } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { createMemoryRouter, type RouteObject } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { Armazon } from './componentes/Armazon'
import { ProveedorDeMarca, useMarca } from './marca/ContextoDeMarca'
import { Inicio } from './pantallas/Inicio'
import { PantallaDeError } from './pantallas/PantallaDeError'
import { rutas } from './rutas'
import { crearClienteDeConsultas } from './servicios/consultas'

function montar(ruta: string, rutasDePrueba: RouteObject[] = rutas) {
  const enrutador = createMemoryRouter(rutasDePrueba, { initialEntries: [ruta] })
  // La página legal pregunta el perfil (sin sesión, sólo describe dónde verá el texto).
  render(
    <QueryClientProvider client={crearClienteDeConsultas()}>
      <ProveedorDeMarca>
        <RouterProvider router={enrutador} />
      </ProveedorDeMarca>
    </QueryClientProvider>,
  )
  return enrutador
}

describe('rutas', () => {
  it('las pantallas tienen su propio límite de error dentro del armazón', () => {
    const armazon = rutas[0]
    expect(armazon?.errorElement).toBeDefined()
    expect(armazon?.children?.[0]?.errorElement).toBeDefined()
  })

  it('el inicio invita a ingresar con el documento', async () => {
    montar('/')

    expect(await screen.findByRole('heading', { name: 'Te damos la bienvenida' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ingresa con tu documento' })).toHaveAttribute('href', '/ingreso')
    expect(screen.getByRole('banner')).toHaveTextContent('Canal del asociado')
  })

  it('el pie enlaza términos, política y preguntas frecuentes', async () => {
    montar('/')

    const pie = await screen.findByRole('navigation', { name: 'Información legal y ayuda' })
    expect(pie).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Términos y condiciones' })).toHaveAttribute('href', '/ayuda/terminos')
    expect(screen.getByRole('link', { name: 'Política de tratamiento de datos' })).toHaveAttribute(
      'href',
      '/ayuda/politica',
    )
    expect(screen.getByRole('link', { name: 'Preguntas frecuentes' })).toHaveAttribute('href', '/ayuda/preguntas')
  })

  it.each([
    ['/ayuda/terminos', 'Términos y condiciones'],
    ['/ayuda/politica', 'Política de tratamiento de datos'],
    ['/ayuda/preguntas', 'Preguntas frecuentes'],
  ])('%s dice que el contenido lo publica la cooperativa', async (ruta, titulo) => {
    montar(ruta)

    expect(await screen.findByRole('heading', { name: titulo })).toBeInTheDocument()
    expect(screen.getByText(/publica tu cooperativa/)).toBeInTheDocument()
  })

  it('una dirección que no existe muestra «Página no encontrada» dentro del armazón', async () => {
    montar('/no/existe')

    expect(await screen.findByRole('heading', { name: 'Página no encontrada' })).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()
  })

  it('el encabezado muestra la marca de la cooperativa', async () => {
    function ConMarca() {
      const { establecerMarca } = useMarca()
      useEffect(() => establecerMarca({ displayName: 'Cooperativa de prueba', primary: '#0b5d1e' }), [establecerMarca])
      return null
    }
    render(
      <ProveedorDeMarca>
        <ConMarca />
        <RouterProvider router={createMemoryRouter(rutas, { initialEntries: ['/'] })} />
      </ProveedorDeMarca>,
    )

    expect(await screen.findByRole('banner')).toHaveTextContent('Cooperativa de prueba')
    expect(document.documentElement.style.getPropertyValue('--color-primario')).toBe('#0b5d1e')
  })

  it('un error en una pantalla muestra el mensaje en español y deja volver al inicio', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
    function PantallaRota(): never {
      throw new Error('fallo de prueba')
    }
    // Misma forma que `rutas`: armazón y, dentro, el grupo de pantallas con su propio errorElement.
    const rutasConFallo: RouteObject[] = [
      {
        path: '/',
        element: <Armazon />,
        errorElement: <PantallaDeError />,
        children: [
          {
            errorElement: <PantallaDeError />,
            children: [
              { index: true, element: <Inicio /> },
              { path: 'rota', element: <PantallaRota /> },
            ],
          },
        ],
      },
    ]
    const enrutador = montar('/rota', rutasConFallo)

    expect(await screen.findByRole('heading', { name: 'No pudimos mostrar esta pantalla' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent(/Vuelve al inicio e inténtalo de nuevo/)
    expect(screen.getByRole('banner')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Volver al inicio' }))

    expect(await screen.findByRole('heading', { name: 'Te damos la bienvenida' })).toBeInTheDocument()
    expect(enrutador.state.location.pathname).toBe('/')
    vi.restoreAllMocks()
  })
})
