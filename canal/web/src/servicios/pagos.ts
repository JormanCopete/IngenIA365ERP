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
}

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
  pendientes: () => api.get<{ items: PagoPendiente[]; support: Soporte }>('/payments/pending'),
  simular: (pedido: { conceptKey: string; modality: Modalidad; amount: number; prepaymentChoice?: EleccionDeAbono | null }) =>
    api.post<Simulacion>('/payables/simulate', pedido),
  iniciarCodigo: () => api.post<RespuestaDeInicio>('/payments/otp', {}),
  enviarCodigo: (challengeId: string, channel: string) => api.post<RespuestaDeEnvio>('/payments/otp/send', { challengeId, channel }),
  verificarCodigo: (challengeId: string, code: string) => api.post<{ verified: boolean }>('/payments/otp/verify', { challengeId, code }),
  textosLegales: (tipo: 'terms' | 'policy' | 'faq') => api.get<{ texts: TextoLegal[] }>(`/legal/${tipo}`),
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

/** Ruta de retorno de la pasarela (contracts/canal-api.md, «Retorno de la pasarela»). */
export const rutaDeRetorno = (id: string) => `/pagos/${encodeURIComponent(id)}/retorno`

const formatoFechaYHora = new Intl.DateTimeFormat('es-CO', { dateStyle: 'long', timeStyle: 'short' })

/** «9 de noviembre de 2026, 3:20 p. m.». */
export function fechaYHora(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : formatoFechaYHora.format(d)
}
