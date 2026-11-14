import { api } from './api'

/** T126 (US9, FR-055): los extractos y certificados que la cooperativa publica, generados por el ERP al pedirlos. */
export type TipoDeDocumento = 'Statement' | 'AccountStatement' | 'ContributionCertificate' | 'TaxCertificate' | 'DepositTaxCertificate'

export interface DocumentoPublicado {
  kind: TipoDeDocumento
  productPublicId: string | null
  productNumber: string | null
  description: string
  /** `period` (desde y hasta), `year` (un año terminado) o `none`. */
  parameter: 'period' | 'year' | 'none'
}

export interface PedidoDeDocumento {
  kind: TipoDeDocumento
  productPublicId?: string
  year?: number
  from?: string
  to?: string
}

export const documentos = {
  listar: () => api.get<{ documents: DocumentoPublicado[] }>('/documents'),
  emitir: (p: PedidoDeDocumento) => api.post<Blob>('/documents', p, { formato: 'archivo' }),
}

/** El nombre del archivo que se guarda. */
export function nombreDelArchivo(d: Pick<DocumentoPublicado, 'kind' | 'productNumber'>, sufijo: string): string {
  const base: Record<TipoDeDocumento, string> = {
    Statement: 'extracto',
    AccountStatement: 'extracto',
    ContributionCertificate: 'certificado-de-aportes',
    TaxCertificate: 'certificado-tributario',
    DepositTaxCertificate: 'certificado-tributario-ahorros',
  }
  return [base[d.kind], d.productNumber, sufijo].filter(Boolean).join('-') + '.pdf'
}
