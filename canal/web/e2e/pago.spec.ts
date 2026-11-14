import { expect, test, type Page } from '@playwright/test'
import { instalarCanalFalso } from './canalFalso'

// T155 (feature 021, US1–US5): la asociada activa su cuenta, ingresa, consulta lo que debe, paga uno aprobado y uno
// que queda pendiente, y los ve en su historial. La API del canal y la pasarela son el canal falso (e2e/canalFalso.ts).

async function activar(page: Page) {
  await page.goto('/activar')
  await expect(page.getByRole('heading', { name: 'Activa tu cuenta' })).toBeVisible()
  await page.getByLabel('Número de documento').fill('1010')
  await page.getByRole('button', { name: 'Continuar' }).click()
  await page.getByRole('button', { name: 'Enviar código' }).click()
  await page.getByRole('textbox', { name: 'Código' }).fill('123456')
  await page.getByRole('button', { name: 'Continuar' }).click()

  await page.getByLabel('Clave', { exact: true }).fill('ClaveSegura2026')
  await page.getByLabel('Repite la clave').fill('ClaveSegura2026')
  const activarCuenta = page.getByRole('button', { name: 'Activar mi cuenta' })
  await expect(activarCuenta).toBeDisabled()
  for (const casilla of await page.getByRole('checkbox', { name: /Leí y acepto/ }).all()) await casilla.check()
  await activarCuenta.click()
}

async function pagar(page: Page, concepto: string, decision: 'Aprobar' | 'Dejar pendiente') {
  await page.goto('/cooperativa/pagar')
  await page.getByRole('checkbox', { name: concepto }).check()
  await page.getByRole('button', { name: 'Continuar' }).click()
  await expect(page.getByRole('table')).toBeVisible()
  await page.getByRole('button', { name: /^Pagar / }).click()
  await expect(page.getByRole('heading', { name: 'Pasarela simulada' })).toBeVisible()
  await page.getByRole('link', { name: decision }).click()
  await expect(page).toHaveURL(/\/pagos\/a\d+\/retorno/)
}

test('activación, ingreso, consulta, pago aprobado, pendiente e historial', async ({ page }) => {
  const canal = await instalarCanalFalso(page)

  await activar(page)
  // Una sola cooperativa: entra directo a sus productos.
  await expect(page.getByText('Crédito CR-1 · Consumo')).toBeVisible()
  await expect(page.getByText('Aportes sociales')).toBeVisible()

  // Sale y vuelve a entrar desde un dispositivo nuevo: pide el código.
  await page.evaluate(() => fetch('/api/v1/auth/logout', { method: 'POST' }))
  await page.goto('/ingreso')
  await page.getByLabel('Número de documento').fill('1010')
  await page.getByLabel('Clave', { exact: true }).fill('ClaveSegura2026')
  await page.getByRole('button', { name: 'Ingresar', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Confirma que eres tú' })).toBeVisible()
  await page.getByRole('button', { name: 'Enviar código' }).click()
  await page.getByRole('textbox', { name: 'Código' }).fill('123456')
  await page.getByRole('button', { name: 'Ingresar', exact: true }).click()
  await expect(page.getByText('Crédito CR-1 · Consumo')).toBeVisible()

  // Pago aprobado: el retorno pregunta al servicio y ofrece el comprobante.
  await pagar(page, 'Crédito CR-1 · Consumo', 'Aprobar')
  await expect(page.getByText('Pago aprobado.')).toBeVisible()
  await expect(page.getByText('3324451')).toBeVisible()

  // Pago pendiente: lo dice con el contacto de la cooperativa, y el concepto queda bloqueado para otro pago.
  await pagar(page, 'Aportes sociales', 'Dejar pendiente')
  await expect(page.getByText('Pago pendiente.')).toBeVisible()
  await expect(page.getByText(/soporte@coop\.test/).first()).toBeVisible()
  const [aprobado, pendiente] = canal.intentos
  if (!aprobado || !pendiente) throw new Error('Se esperaban dos intentos.')
  await page.goto('/cooperativa/pagar')
  await expect(page.getByText(pendiente.reference)).toBeVisible()
  await expect(page.getByRole('checkbox', { name: 'Aportes sociales' })).toHaveCount(0)

  // Historial: los dos, el más reciente primero.
  await page.goto('/cooperativa/pagos')
  await expect(page.getByRole('heading', { name: 'Mis pagos' })).toBeVisible()
  const pagos = page.getByRole('article')
  await expect(pagos).toHaveCount(2)
  await expect(pagos.nth(0)).toContainText(pendiente.reference)
  await expect(pagos.nth(1)).toContainText(aprobado.reference)
  await expect(pagos.nth(1)).toContainText('3324451')

  expect(canal.desconocidas).toEqual([])
})
