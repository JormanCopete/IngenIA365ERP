// Ayudantes de las pruebas de pantallas: monta las rutas reales con un enrutador en memoria y responde las llamadas a
// la API con un doble de fetch que contesta por método y ruta.
import { render } from '@testing-library/react'
import { QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { ProveedorDeMarca } from '../marca/ContextoDeMarca'
import { rutas } from '../rutas'
import { crearClienteDeConsultas } from '../servicios/consultas'

export type Respondedor = (cuerpo: unknown, url: string) => { status?: number; cuerpo?: unknown } | undefined

export interface ApiFalsa {
  llamadas: { metodo: string; ruta: string; cuerpo: unknown; cabeceras: Headers }[]
  responder: (metodo: string, ruta: string, r: Respondedor | { status?: number; cuerpo?: unknown }) => void
}

/** Instala un fetch falso. Una ruta sin respuesta registrada responde 404 con el sobre. */
export function instalarApiFalsa(): ApiFalsa {
  const respuestas = new Map<string, Respondedor>()
  const api: ApiFalsa = {
    llamadas: [],
    responder: (metodo, ruta, r) => respuestas.set(`${metodo} ${ruta}`, typeof r === 'function' ? r : () => r),
  }
  vi.stubGlobal('fetch', vi.fn(async (entrada: RequestInfo | URL, init?: RequestInit) => {
    const url = String(entrada)
    const ruta = url.replace(/^\/api\/v1/, '').split('?')[0] ?? ''
    const metodo = init?.method ?? 'GET'
    const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined
    api.llamadas.push({ metodo, ruta, cuerpo, cabeceras: new Headers(init?.headers) })
    const r = respuestas.get(`${metodo} ${ruta}`)?.(cuerpo, url) ?? {
      status: 404,
      cuerpo: { code: 'Canal.Ruta.NoExiste', message: 'Recurso no encontrado.' },
    }
    const status = r.status ?? 200
    if (status === 204) return new Response(null, { status })
    return new Response(JSON.stringify(r.cuerpo ?? {}), { status, headers: { 'Content-Type': 'application/json' } })
  }))
  return api
}

export function montarRuta(ruta: string) {
  const enrutador = createMemoryRouter(rutas, { initialEntries: [ruta] })
  const cliente = crearClienteDeConsultas()
  render(
    <QueryClientProvider client={cliente}>
      <ProveedorDeMarca>
        <RouterProvider router={enrutador} />
      </ProveedorDeMarca>
    </QueryClientProvider>,
  )
  return { enrutador, cliente }
}

export const SESION = {
  inactivityMinutes: 5,
  idleExpiresAt: new Date(Date.now() + 5 * 60_000).toISOString(),
  expiresAt: new Date(Date.now() + 8 * 3_600_000).toISOString(),
  tenantSelected: false,
}

export function perfilDePrueba(tenants: { tenantPublicId: string; displayName: string }[], tenant: string | null = null) {
  return {
    account: {
      firstNames: 'Ana', lastNames: 'Prueba', documentType: 'CC', documentNumberMasked: '••••••1010',
      emailMasked: 'a•••@c•••.test', mobileMasked: null,
    },
    tenants: tenants.map((t) => ({ ...t, brand: {}, support: {}, inactivityMinutes: 5 })),
    tenant,
    session: { ...SESION, tenantSelected: tenant !== null },
    passkeysAvailable: false,
  }
}
