import type { ReactNode } from 'react'
import './IndicadorDeCarga.css'

interface Props {
  cargando: boolean
  /** Lo que se anuncia y se ve mientras carga. */
  texto?: string
  children?: ReactNode
}

/**
 * Indicador de carga por zona: toda grilla, formulario o panel que espera datos va dentro. El contenido sigue
 * montado (no pierde lo escrito ni la posición) pero queda inerte bajo un velo mientras `cargando`.
 * La región `role="status"` existe siempre para que el lector de pantalla anuncie el cambio.
 */
export function IndicadorDeCarga({ cargando, texto = 'Cargando…', children }: Props) {
  return (
    <div className="zona-de-carga" aria-busy={cargando}>
      <div className="zona-de-carga__contenido" inert={cargando}>
        {children}
      </div>
      <div
        className={cargando ? 'zona-de-carga__velo' : 'solo-lectores'}
        role="status"
        aria-live="polite"
      >
        {cargando && (
          <>
            <span className="zona-de-carga__giro" aria-hidden="true" />
            <span className="zona-de-carga__texto">{texto}</span>
          </>
        )}
      </div>
    </div>
  )
}
