import { createBrowserRouter, createHashRouter, type RouteObject } from 'react-router'
import { Armazon } from './componentes/Armazon'
import { Inicio } from './pantallas/Inicio'
import { Activacion } from './pantallas/activacion/Activacion'
import { Conceptos } from './pantallas/conceptos/Conceptos'
import { DetalleDeConcepto } from './pantallas/conceptos/DetalleDeConcepto'
import { Historial } from './pantallas/historial/Historial'
import { Documentos } from './pantallas/documentos/Documentos'
import { Credito } from './pantallas/credito/Credito'
import { Pagar } from './pantallas/pago/Pagar'
import { Retorno } from './pantallas/pago/Retorno'
import { Ingreso } from './pantallas/ingreso/Ingreso'
import { Recuperar } from './pantallas/ingreso/Recuperar'
import { Perfil } from './pantallas/perfil/Perfil'
import { Selector } from './pantallas/selector/Selector'
import { RequiereCooperativa, RequiereSesion } from './sesion/sesion'
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
          { path: 'activar', element: <Activacion /> },
          { path: 'recuperar', element: <Recuperar /> },
          {
            element: <RequiereSesion />,
            children: [
              { path: 'cooperativas', element: <Selector /> },
              { path: 'perfil', element: <Perfil /> },
              {
                element: <RequiereCooperativa />,
                children: [
                  { path: 'cooperativa', element: <Conceptos /> },
                  { path: 'cooperativa/conceptos/:clave', element: <DetalleDeConcepto /> },
                  { path: 'cooperativa/pagar', element: <Pagar /> },
                  { path: 'cooperativa/pagos', element: <Historial /> },
                  { path: 'cooperativa/documentos', element: <Documentos /> },
                  { path: 'cooperativa/credito', element: <Credito /> },
                  // Retorno de la pasarela (contracts/canal-api.md): en la app es la misma URL por App Link.
                  { path: 'pagos/:id/retorno', element: <Retorno /> },
                ],
              },
            ],
          },
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
