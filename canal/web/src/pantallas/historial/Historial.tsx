import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { AvisoDeError } from '../../componentes/Formulario'
import { pesos } from '../../servicios/conceptos'
import { estadoVisible, fechaYHora, pagos, REVERSOS, rutaDeRetorno, type Intento } from '../../servicios/pagos'
import { usePerfil } from '../../sesion/sesion'
import { BotonDeComprobante } from '../pago/Comprobante'
import '../conceptos/Conceptos.css'
import './Historial.css'

/** Cuántos pagos se muestran (la certificación pide al menos cinco). */
export const PAGOS_EN_EL_HISTORIAL = 20

/**
 * T110 (US8, FR-054, FR-048): los pagos del asociado en la cooperativa elegida, del más reciente al más antiguo, con fecha y hora,
 * referencia, autorización o CUS, estado y valor en pesos, y el comprobante de cada pago aplicado.
 */
export function Historial() {
  const { data: perfil } = usePerfil()
  const tenant = perfil?.tenant ?? null
  const consulta = useQuery({
    queryKey: ['pagos-historial', tenant],
    queryFn: () => pagos.historial(PAGOS_EN_EL_HISTORIAL),
    enabled: tenant !== null,
  })

  return (
    <section className="pagina">
      <p>
        <Link to="/cooperativa">← Mis productos</Link>
      </p>
      <h1>Mis pagos</h1>
      <IndicadorDeCarga cargando={consulta.isFetching} texto="Consultando tus pagos…">
        {consulta.isError ? (
          <AvisoDeError error={consulta.error} />
        ) : consulta.data && consulta.data.items.length === 0 ? (
          <div className="tarjeta">
            <p>Todavía no has hecho pagos en línea en esta cooperativa.</p>
          </div>
        ) : (
          <ol className="lista-simple historial">
            {consulta.data?.items.map((i) => (
              <li key={i.attemptId}>
                <FilaDelHistorial intento={i} />
              </li>
            ))}
          </ol>
        )}
      </IndicadorDeCarga>
    </section>
  )
}

function FilaDelHistorial({ intento: i }: { intento: Intento }) {
  const fecha = i.approval?.approvedAt ?? i.createdAt
  const estado = estadoVisible(i)
  return (
    <article className="tarjeta historial__pago" aria-label={`Pago ${i.reference}`}>
      <div className="conceptos__cifra">
        <span className={`historial__estado historial__estado--${estado.clave}`}>{estado.nombre}</span>
        <strong>{pesos(i.total)} COP</strong>
      </div>
      <dl className="conceptos__datos">
        <div className="conceptos__dato">
          <dt>Fecha y hora</dt>
          <dd>{fechaYHora(fecha)}</dd>
        </div>
        <div className="conceptos__dato">
          <dt>Referencia</dt>
          <dd>{i.reference}</dd>
        </div>
        <div className="conceptos__dato">
          <dt>Autorización / CUS</dt>
          <dd>{i.approval?.authorization || '—'}</dd>
        </div>
      </dl>
      {i.reversalKind ? (
        <p className="aviso aviso--info">{REVERSOS[i.reversalKind].mensaje}</p>
      ) : (
        i.status === 'approved' && i.erpOutcome === 'in_review' && (
          <p className="aviso aviso--advertencia">Pago recibido, en revisión por tu cooperativa.</p>
        )
      )}
      <div className="fila-de-botones">
        <Link to={rutaDeRetorno(i.attemptId)}>Ver detalle</Link>
        {!i.reversalKind && <BotonDeComprobante intento={i} etiqueta="Comprobante" />}
      </div>
    </article>
  )
}
