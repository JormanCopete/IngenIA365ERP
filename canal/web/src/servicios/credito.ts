import { api, nuevoIdentificador } from './api'

/** T144 (US13, FR C6): el simulador y la solicitud de crédito en línea. La simulación es la del ERP. */
export interface LineaDeCredito {
  publicId: string
  code: string
  name: string
}

export interface CuotaSimulada {
  number: number
  dueDate: string
  total: number
}

export interface SimulacionDeCredito {
  creditLineName: string
  amount: number
  installmentCount: number
  effectiveAnnualRate: number
  totals: { toPay: number; interest: number; insurance: number }
  installments: CuotaSimulada[]
  warnings: { code: string; message: string }[]
}

export interface SolicitudDeCredito {
  publicId: string
  number: string | null
  status: string
  createdAt: string
  creditLineName: string
  amount: number
  installments: number | null
  purpose: string | null
}

export interface PedidoDeSolicitud {
  creditLinePublicId: string
  amount: number
  installments: number
  purpose?: string
  monthlyIncome?: number
  monthlyExpenses?: number
}

/** Cómo ve el asociado el estado de su solicitud (sin los nombres internos del comité). */
export const ESTADO_DE_LA_SOLICITUD: Record<string, string> = {
  Draft: 'Recibida: tu cooperativa la revisará',
  Returned: 'Tu cooperativa necesita más información',
  Submitted: 'En estudio',
  PendingDecision: 'En estudio',
  Approved: 'Aprobada',
  Denied: 'No aprobada',
  Disbursed: 'Desembolsada',
  Granted: 'Aprobada',
  Cancelled: 'Anulada',
}

export const credito = {
  lineas: () => api.get<{ lines: LineaDeCredito[] }>('/credit/lines'),
  simular: (p: { creditLinePublicId: string; amount: number; installments: number }) => api.post<SimulacionDeCredito>('/credit/simulate', p),
  solicitar: (p: PedidoDeSolicitud, clave: string = nuevoIdentificador()) =>
    api.post<SolicitudDeCredito>('/credit/applications', p, { claveDeIdempotencia: clave }),
  solicitudes: () => api.get<{ applications: SolicitudDeCredito[] }>('/credit/applications'),
}
