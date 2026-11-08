import { createBrowserRouter, createHashRouter, type RouteObject } from 'react-router'
import { Armazon } from './componentes/Armazon'
import { Inicio } from './pantallas/Inicio'
import { Ingreso } from './pantallas/Ingreso'
import { PaginaLegal } from './pantallas/PaginaLegal'
import { PaginaNoEncontrada } from './pantallas/PaginaNoEncontrada'
import { PantallaDeError } from './pantallas/PantallaDeError'
import { detectarPlataforma, type NombreDePlataforma } from './plataforma'

/**
 * Rutas del canal. Las pantallas van en una ruta sin camino con su propio `errorElement`, para que un error en una
 * de ellas deje el encabezado y el pie; el del armazón atiende lo que falle en el armazón mismo.
 */
export const rutas: RouteObject[] = [
  {
    path: '/',
    element: <Armazon />,
    errorElement: <PantallaDeError />,
    children: [
      {
        errorElement: <PantallaDeError />,
        children: [
          { index: true, element: <Inicio /> },
          { path: 'ingreso', element: <Ingreso /> },
          { path: 'ayuda/terminos', element: <PaginaLegal tipo="terminos" /> },
          { path: 'ayuda/politica', element: <PaginaLegal tipo="politica" /> },
          { path: 'ayuda/preguntas', element: <PaginaLegal tipo="preguntas" /> },
          { path: '*', element: <PaginaNoEncontrada /> },
        ],
      },
    ],
  },
]

/**
 * En la PWA, rutas de navegador (`/pagos/…/retorno` es una URL real a la que vuelve la pasarela); en la app, que
 * sirve archivos locales, rutas de hash.
 */
export function crearEnrutador(plataforma: NombreDePlataforma = detectarPlataforma()) {
  return plataforma === 'app' ? createHashRouter(rutas) : createBrowserRouter(rutas)
}

export type Enrutador = ReturnType<typeof crearEnrutador>

let enrutadorUnico: Enrutador | undefined

/** El enrutador se crea una sola vez, fuera del árbol de React. */
export function obtenerEnrutador(): Enrutador {
  enrutadorUnico ??= crearEnrutador()
  return enrutadorUnico
}
