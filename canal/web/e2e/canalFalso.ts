import type { Page, Route } from '@playwright/test'

// Un canal de prueba en el navegador (T155): responde `/api/v1/*` con el contrato de contracts/canal-api.md §A y
// guarda el estado de una asociada (activación, sesión, cooperativa elegida, intentos). La pasarela es una página
// propia con dos botones —aprobar o dejar pendiente— que vuelve a la ruta de retorno como lo haría Avalpay.

const COOPERATIVA = 't-pruebas'
const DESTINO = { destinationPublicId: 'd1', sitePublicId: 's1', name: 'Avalpay', offerPse: true, offerCard: false }
const SOPORTE = { phone: '6025550000', email: 'soporte@coop.test', hours: 'Lunes a viernes de 8 a 5' }
const TEXTOS = [
  { kind: 'Terms', version: 1, content: 'Términos de uso del canal de pruebas.' },
  { kind: 'DataPolicy', version: 1, content: 'Política de tratamiento de datos de pruebas.' },
]

export interface Intento {
  attemptId: string
  reference: string
  status: 'pending' | 'approved'
  total: number
  items: { conceptKey: string; modality: string; amount: number; prepaymentChoice: string | null; description: string; kind: string }[]
  createdAt: string
}

export interface CanalFalso {
  intentos: Intento[]
  /** Peticiones que el canal falso no conoce (para que la prueba lo diga en vez de colgarse). */
  desconocidas: string[]
}

function concepto(conceptKey: string, description: string, dueToday: number) {
  return {
    conceptKey, kind: conceptKey.startsWith('LOAN') ? 'Loan' : 'Contribution', lineCode: 'CONSUMO', description, productPublicId: conceptKey,
    productNumber: conceptKey.split(':')[1], balance: 5_400_000, pastDue: 0, defaultInterest: 0, cycleInstallment: dueToday, dueToday,
    payoffAmount: null, nextDueDate: '2026-12-15', daysPastDue: 0, destinationPublicId: 'd1', payable: true,
    modes: { due: true, partial: null, prepayment: null, payoff: false }, canCombine: true, installment: null, blockReasons: [],
  }
}

const CONCEPTOS = [concepto('LOAN:1', 'Crédito CR-1 · Consumo', 332_500), concepto('ACCOUNT:2', 'Aportes sociales', 50_000)]

export async function instalarCanalFalso(page: Page): Promise<CanalFalso> {
  const estado: CanalFalso = { intentos: [], desconocidas: [] }
  let activa = false
  let conSesion = false
  let cooperativaElegida = false
  let secuencia = 0

  const sesion = () => {
    const ahora = Date.now()
    return {
      inactivityMinutes: 5, idleExpiresAt: new Date(ahora + 5 * 60_000).toISOString(),
      expiresAt: new Date(ahora + 12 * 3_600_000).toISOString(), tenantSelected: cooperativaElegida,
    }
  }
  const cooperativa = { tenantPublicId: COOPERATIVA, displayName: 'Cooperativa de Pruebas', brand: {}, support: SOPORTE, inactivityMinutes: 5 }
  const perfil = () => ({
    account: { firstNames: 'Ana', lastNames: 'Prueba', documentType: 'CC', documentNumberMasked: '••••••1010', emailMasked: 'a•••@c•••.test', mobileMasked: null },
    tenants: [cooperativa], tenant: cooperativaElegida ? COOPERATIVA : null, session: sesion(), passkeysAvailable: false,
  })
  const vista = (i: Intento) => ({
    ...i, currency: 'COP', updatedAt: i.createdAt, expiresAt: new Date(Date.parse(i.createdAt) + 15 * 60_000).toISOString(),
    processUrl: i.status === 'pending' ? `http://localhost:5181/pasarela-simulada/${i.attemptId}` : null,
    approval: i.status === 'approved'
      ? { internalReference: 1598634446, authorization: '3324451', receipt: 'R1', franchise: '_PSE_', paymentMethodName: 'PSE',
          issuerName: 'Banco de pruebas', approvedAt: i.createdAt }
      : null,
    message: null, erpOutcome: i.status === 'approved' ? 'applied' : 'not_sent', erpReceiptPublicId: i.status === 'approved' ? 'r1' : null,
    support: SOPORTE, reversedAt: null, reversalKind: null,
  })

  const json = (route: Route, cuerpo: unknown, status = 200) =>
    route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(cuerpo) })
  const error = (route: Route, status: number, code: string, message: string) => json(route, { code, message, data: null }, status)

  await page.route('**/api/v1/**', async (route) => {
    const peticion = route.request()
    const url = new URL(peticion.url())
    const ruta = url.pathname.replace(/^\/api\/v1/, '')
    const metodo = peticion.method()
    const llave = `${metodo} ${ruta}`

    if (llave === 'POST /activation/start') return json(route, { challengeId: 'act-1', channels: [{ kind: 'email', masked: 'a•••@c•••.test' }] })
    if (llave === 'POST /activation/send' || llave === 'POST /auth/login/send')
      return json(route, { sentTo: 'a•••@c•••.test', expiresAt: new Date(Date.now() + 300_000).toISOString() }, 202)
    if (llave === 'POST /activation/verify')
      return json(route, { alreadyActive: false, activationToken: 'permiso-1', entityName: 'Cooperativa de Pruebas', legalTexts: TEXTOS })
    if (llave === 'POST /activation/complete') {
      const cuerpo = peticion.postDataJSON() as { acceptances: unknown[] }
      if (cuerpo.acceptances.length !== TEXTOS.length) return error(route, 409, 'Canal.Textos.PorAceptar', 'Acepta los textos vigentes.')
      activa = true
      conSesion = true
      return json(route, { session: sesion() })
    }
    if (llave === 'POST /auth/login') {
      if (!activa) return error(route, 401, 'Canal.Ingreso.Invalido', 'El documento o la clave no son correctos.')
      return json(route, { challengeId: 'ing-1', channels: [{ kind: 'email', masked: 'a•••@c•••.test' }] })
    }
    if (llave === 'POST /auth/login/verify') {
      conSesion = true
      return json(route, { session: sesion() })
    }
    if (llave === 'POST /auth/logout') {
      conSesion = false
      cooperativaElegida = false
      return route.fulfill({ status: 204 })
    }

    if (!conSesion) return error(route, 401, 'Canal.Sesion.Requerida', 'Ingresa de nuevo.')

    if (llave === 'GET /session' || llave === 'POST /session/keepalive') return json(route, sesion())
    if (llave === 'POST /session/tenant') {
      cooperativaElegida = true
      return json(route, { tenant: cooperativa, session: sesion() })
    }
    if (llave === 'GET /me') return json(route, perfil())

    if (!cooperativaElegida) return error(route, 409, 'Canal.Cooperativa.Requerida', 'Elige una cooperativa.')

    if (llave === 'GET /payables') return json(route, { asOf: '2026-12-05', items: CONCEPTOS, notices: [], destinations: [DESTINO] })
    if (llave === 'GET /payments/pending') {
      const items = estado.intentos.filter((i) => i.status === 'pending').flatMap((i) =>
        i.items.map((it) => ({ conceptKey: it.conceptKey, attemptId: i.attemptId, reference: i.reference, total: i.total, status: i.status,
          approval: null, createdAt: i.createdAt })))
      return json(route, { items, support: SOPORTE })
    }
    if (llave === 'GET /payments') return json(route, { items: [...estado.intentos].reverse().map(vista), support: SOPORTE })
    if (llave === 'POST /payments') {
      const cuerpo = peticion.postDataJSON() as { items: { conceptKey: string; modality: string; amount: number; prepaymentChoice: string | null }[] }
      secuencia += 1
      const intento: Intento = {
        attemptId: `a${secuencia}`, reference: `TST261205${String(secuencia).padStart(8, '0')}X`, status: 'pending',
        total: cuerpo.items.reduce((s, i) => s + i.amount, 0),
        items: cuerpo.items.map((i) => ({ ...i, description: CONCEPTOS.find((c) => c.conceptKey === i.conceptKey)?.description ?? '', kind: 'Loan' })),
        createdAt: new Date().toISOString(),
      }
      estado.intentos.push(intento)
      return json(route, vista(intento), 201)
    }
    const consulta = /^\/payments\/([^/]+)$/.exec(ruta)
    if (metodo === 'GET' && consulta) {
      const intento = estado.intentos.find((i) => i.attemptId === consulta[1])
      return intento ? json(route, vista(intento)) : error(route, 404, 'Canal.Pago.NoExiste', 'No encontramos ese pago.')
    }

    estado.desconocidas.push(llave)
    return error(route, 404, 'Canal.Ruta.NoExiste', `El canal falso no conoce ${llave}.`)
  })

  // La pasarela simulada: «Aprobar» aprueba el intento; las dos opciones vuelven a la ruta de retorno.
  await page.route('**/pasarela-simulada/**', async (route) => {
    const url = new URL(route.request().url())
    const [, , id, decision] = url.pathname.split('/')
    const intento = estado.intentos.find((i) => i.attemptId === id)
    if (decision && intento) {
      if (decision === 'aprobar') intento.status = 'approved'
      return route.fulfill({
        status: 200, contentType: 'text/html',
        body: `<!doctype html><script>location.replace('/pagos/${id}/retorno?status=${decision === 'aprobar' ? 'APPROVED' : 'PENDING'}')</script>`,
      })
    }
    return route.fulfill({
      status: 200, contentType: 'text/html',
      body: `<!doctype html><html lang="es"><title>Pasarela simulada</title><h1>Pasarela simulada</h1>
        <p>Pago ${intento?.reference ?? ''} por ${intento?.total ?? 0}</p>
        <a href="/pasarela-simulada/${id}/aprobar">Aprobar</a> <a href="/pasarela-simulada/${id}/pendiente">Dejar pendiente</a></html>`,
    })
  })

  return estado
}
