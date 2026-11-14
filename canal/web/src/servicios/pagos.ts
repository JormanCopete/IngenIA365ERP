// Pagos en línea (US4; contracts/canal-api.md §A, «Dentro de la cooperativa elegida»). El canal crea el intento y la
// sesión en la pasarela; el resultado siempre se pregunta al servicio, nunca se lee de la URL de retorno (FR-038).
import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { RespuestaDeInicio, RespuestaDeEnvio, Soporte, TextoParaAceptar } from './identidad'

export type Modalidad = 'Due' | 'Partial' | 'Prepayment' | 'Payoff'
export type EleccionDeAbono = 'ReduceTerm' | 'ReduceInstallment'
export type EstadoDelIntento = 'created' | 'pending' | 'approved' | 'rejected' | 'failed' | 'expired'

/** Un destino de pago con los medios que ofrece (FR-006, FR-032). */
export interface Destino {
  destinationPublicId: string
  sitePublicId: string
  name: string
  offerPse: boolean
  offerCard: boolean
}

export interface ItemDelPago {
  conceptKey: string
  modality: Modalidad
  amount: number
  prepaymentChoice?: EleccionDeAbono | null
}

export interface Aprobacion {
  internalReference: number
  authorization: string
  receipt: string
  franchise: string
  paymentMethodName: string
  issuerName: string
  approvedAt: string
}

export interface Intento {
  attemptId: string
  reference: string
  status: EstadoDelIntento
  total: number
  currency: 'COP'
  items: (ItemDelPago & { description: string; kind: string })[]
  createdAt: string
  updatedAt: string
  expiresAt: string
  processUrl: string | null
  approval: Aprobacion | null
  message: string | null
  erpOutcome: 'not_sent' | 'sending' | 'applied' | 'in_review' | 'failed_retrying'
  erpReceiptPublicId: string | null
  support: Soporte
  /** FR-053: el pago se reversó por la pasarela (gateway) o lo devolvió la cooperativa por Tesorería (treasury). */
  reversedAt?: string | null
  reversalKind?: TipoDeReverso | null
}

export type TipoDeReverso = 'gateway' | 'treasury'

export interface PagoPendiente {
  conceptKey: string
  attemptId: string
  reference: string
  total: number
  status: EstadoDelIntento
  approval: Aprobacion | null
  createdAt: string
}

export interface Simulacion {
  conceptKey: string
  modality: Modalidad
  amount: number
  allowed: boolean
  surplus: number
  newPlan: { installment: number; remainingInstallments: number; nextDueDate: string | null } | null
  prepaymentChoice: EleccionDeAbono | null
  alternatives: { choice: EleccionDeAbono; newPlan: Simulacion['newPlan']; surplus: number }[]
  notices: string[]
}

export interface TextoLegal extends TextoParaAceptar {
  publishedAt: string
}

/** Códigos de error que las pantallas de pago atienden. */
export const CODIGOS_DE_PAGO = {
  pendiente: 'Canal.Pago.Pendiente',
  textos: 'Canal.Pago.TextosPorAceptar',
  codigo: 'Canal.Pago.CodigoRequerido',
} as const

export const pagos = {
  crear: (items: ItemDelPago[], clave: string, extra: { acceptances?: { kind: string; version: number }[]; otpChallengeId?: string } = {}) =>
    api.post<Intento>('/payments', { items, ...extra }, { claveDeIdempotencia: clave }),
  consultar: (id: string, retorno = false) => api.get<Intento>(`/payments/${encodeURIComponent(id)}${retorno ? '?return=1' : ''}`),
  historial: (limite = 20) => api.get<{ items: Intento[]; support: Soporte }>(`/payments?limit=${limite}`),
  pendientes: () => api.get<{ items: PagoPendiente[]; support: Soporte }>('/payments/pending'),
  simular: (pedido: { conceptKey: string; modality: Modalidad; amount: number; prepaymentChoice?: EleccionDeAbono | null }) =>
    api.post<Simulacion>('/payables/simulate', pedido),
  iniciarCodigo: () => api.post<RespuestaDeInicio>('/payments/otp', {}),
  enviarCodigo: (challengeId: string, channel: string) => api.post<RespuestaDeEnvio>('/payments/otp/send', { challengeId, channel }),
  verificarCodigo: (challengeId: string, code: string) => api.post<{ verified: boolean }>('/payments/otp/verify', { challengeId, code }),
  textosLegales: (tipo: 'terms' | 'policy' | 'faq') => api.get<{ texts: TextoLegal[] }>(`/legal/${tipo}`),
  comprobante: (id: string) => api.get<Blob>(`/payments/${encodeURIComponent(id)}/receipt`, { formato: 'archivo' }),
}

/** El aviso al ERP sigue en curso: aún no se sabe si quedó aplicado o en revisión. */
export const avisoEnCurso = (i: Pick<Intento, 'status' | 'erpOutcome'>) =>
  i.status === 'approved' && (i.erpOutcome === 'not_sent' || i.erpOutcome === 'sending' || i.erpOutcome === 'failed_retrying')

/** El contenido de un Blob en base64 (lo que pide `abrirDocumento`). */
export async function base64DeBlob(blob: Blob): Promise<string> {
  const bytes = new Uint8Array(await blob.arrayBuffer())
  let binario = ''
  for (let i = 0; i < bytes.length; i += 0x8000) binario += String.fromCharCode(...bytes.subarray(i, i + 0x8000))
  return btoa(binario)
}

export const clavePendientes = (cooperativa: string | null) => ['pagos-pendientes', cooperativa] as const

/** Los conceptos con un pago en proceso (FR-041). */
export function usePagosPendientes(cooperativa: string | null) {
  return useQuery({ queryKey: clavePendientes(cooperativa), queryFn: pagos.pendientes, enabled: cooperativa !== null })
}

export const NOMBRE_DE_LA_MODALIDAD: Record<Modalidad, string> = {
  Due: 'Lo que tienes para pagar hoy',
  Partial: 'Otro valor',
  Prepayment: 'Abono a capital',
  Payoff: 'Cancelar el total',
}

export const NOMBRE_DE_LA_ELECCION: Record<EleccionDeAbono, string> = {
  ReduceTerm: 'Reducir el plazo (pagas menos cuotas)',
  ReduceInstallment: 'Reducir el valor de la cuota',
}

export const NOMBRE_DEL_ESTADO: Record<EstadoDelIntento, string> = {
  created: 'En proceso',
  pending: 'Pendiente',
  approved: 'Aprobado',
  rejected: 'Rechazado',
  failed: 'Fallido',
  expired: 'Vencido',
}

/** Lo que el asociado ve de un pago reversado (FR-053). */
export const REVERSOS: Record<TipoDeReverso, { nombre: string; mensaje: string }> = {
  gateway: {
    nombre: 'Reversado',
    mensaje: 'Este pago se reversó: la pasarela devuelve el dinero al medio con que pagaste. Tu cooperativa no lo aplicó a tus productos.',
  },
  treasury: {
    nombre: 'Devuelto',
    mensaje: 'Tu cooperativa anuló este pago y te devuelve el dinero. Si tienes dudas sobre la devolución, comunícate con ella.',
  },
}

/** El estado que se muestra: el del intento, o el del reverso si lo hubo. */
export function estadoVisible(i: Pick<Intento, 'status' | 'reversalKind'>): { clave: string; nombre: string } {
  if (i.reversalKind) return { clave: i.reversalKind === 'gateway' ? 'reversed' : 'refunded', nombre: REVERSOS[i.reversalKind].nombre }
  return { clave: i.status, nombre: NOMBRE_DEL_ESTADO[i.status] }
}

/** Ruta de retorno de la pasarela (contracts/canal-api.md, «Retorno de la pasarela»). */
export const rutaDeRetorno = (id: string) => `/pagos/${encodeURIComponent(id)}/retorno`

const formatoFechaYHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'long', timeStyle: 'short' })

/** «9 de noviembre de 2026, 3:20 p. m.». */
export function fechaYHora(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : formatoFechaYHora.format(d)
}
