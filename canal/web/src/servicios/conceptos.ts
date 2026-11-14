// Conceptos del asociado en la cooperativa elegida (contracts/canal-api.md §A, «Dentro de la cooperativa elegida»).
// Los valores los calcula el ERP en cada consulta; aquí sólo viven en la memoria de TanStack Query (FR-030).
import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { Destino } from './pagos'

export type TipoDeConcepto = 'Loan' | 'Contribution' | 'ContractualSaving' | 'ProgrammedSaving' | 'CommercialReceivable'

export interface ModosDePago {
  due: boolean
  partial: { minimum: number } | null
  prepayment: { defaultChoice: 'ReduceTerm' | 'ReduceInstallment' } | null
  payoff: boolean
}

export interface CuotaDeCuenta {
  period: string
  dueDate: string
  amount: number
  pending: number
}

export interface Concepto {
  conceptKey: string
  kind: TipoDeConcepto
  lineCode: string
  description: string
  productPublicId: string
  productNumber: string
  balance: number
  pastDue: number
  defaultInterest: number
  cycleInstallment: number
  dueToday: number
  payoffAmount: number | null
  nextDueDate: string | null
  daysPastDue: number
  destinationPublicId: string | null
  payable: boolean
  modes: ModosDePago
  canCombine: boolean
  installment: CuotaDeCuenta | null
  blockReasons: string[]
}

export interface Conceptos {
  asOf: string
  items: Concepto[]
  notices: string[]
  /** Los destinos de pago que usan los conceptos (FR-032: cada destino se paga por separado). */
  destinations: Destino[]
}

/** El ERP de la cooperativa no respondió: no se muestra ningún valor viejo (FR-031). */
export const CODIGO_ENTIDAD_NO_DISPONIBLE = 'Canal.Entidad.NoDisponible'

export const conceptos = {
  listar: () => api.get<Conceptos>('/payables'),
}

/** La clave de la consulta incluye la cooperativa: al cambiar de cooperativa no se mezclan. */
export const claveDeConceptos = (cooperativa: string | null) => ['conceptos', cooperativa] as const

export function useConceptos(cooperativa: string | null) {
  return useQuery({
    queryKey: claveDeConceptos(cooperativa),
    queryFn: conceptos.listar,
    enabled: cooperativa !== null,
  })
}

/** Los grupos en que se muestran, en este orden. */
export const GRUPOS: { titulo: string; tipos: TipoDeConcepto[] }[] = [
  { titulo: 'Créditos', tipos: ['Loan'] },
  { titulo: 'Aportes', tipos: ['Contribution'] },
  { titulo: 'Ahorros', tipos: ['ContractualSaving', 'ProgrammedSaving'] },
  // C2b (T128): las facturas pendientes de cuentas por cobrar comerciales.
  { titulo: 'Facturas pendientes', tipos: ['CommercialReceivable'] },
]

const formatoPesos = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })

/** «$ 332.500». */
export function pesos(valor: number): string {
  return formatoPesos.format(valor)
}

const formatoFechaLarga = new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'long', year: 'numeric' })

/** «5 de diciembre de 2026» a partir de «2026-12-05», sin correr el día por la zona horaria. */
export function fechaLarga(iso: string): string {
  const [a, m, d] = iso.split('-').map(Number)
  if (!a || !m || !d) return iso
  return formatoFechaLarga.format(new Date(a, m - 1, d))
}

/** El segmento de ruta del detalle de un concepto. */
export const rutaDelConcepto = (c: Pick<Concepto, 'conceptKey'>) => `/cooperativa/conceptos/${encodeURIComponent(c.conceptKey)}`
