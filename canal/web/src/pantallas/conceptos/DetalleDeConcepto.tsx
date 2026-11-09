import { Link, useParams } from 'react-router'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { fechaLarga, pesos, useConceptos, type Concepto } from '../../servicios/conceptos'
import { usePerfil } from '../../sesion/sesion'
import { NoDisponible } from './Conceptos'
import './Conceptos.css'

/**
 * T083 (US3): el detalle de un concepto —saldo, vencido, mora, cuota, próxima fecha, exigible y lo que lo cancela—, con
 * los valores de la misma consulta del listado (no se guardan en ningún lado).
 */
export function DetalleDeConcepto() {
  const { clave } = useParams()
  const { data: perfil } = usePerfil()
  const consulta = useConceptos(perfil?.tenant ?? null)
  const concepto = consulta.data?.items.find((i) => i.conceptKey === clave)

  return (
    <section className="pagina">
      <p>
        <Link to="/cooperativa">← Mis productos</Link>
      </p>
      <IndicadorDeCarga cargando={consulta.isFetching} texto="Consultando el producto…">
        {consulta.isError ? (
          <NoDisponible error={consulta.error} reintentar={() => void consulta.refetch()} />
        ) : consulta.data && !concepto ? (
          <div className="tarjeta">
            <h1>Producto no disponible</h1>
            <p>Este producto ya no aparece en el canal. Puede que se haya cancelado o que tu cooperativa lo haya retirado.</p>
          </div>
        ) : concepto && consulta.data ? (
          <Detalle concepto={concepto} fecha={consulta.data.asOf} />
        ) : null}
      </IndicadorDeCarga>
    </section>
  )
}

function Detalle({ concepto: c, fecha }: { concepto: Concepto; fecha: string }) {
  const esCredito = c.kind === 'Loan'
  const filas: [string, string][] = esCredito
    ? [
        ['Saldo', pesos(c.balance)],
        ['Valor vencido', pesos(c.pastDue)],
        ['Interés de mora', pesos(c.defaultInterest)],
        ['Cuota', pesos(c.cycleInstallment)],
        ['Días de mora', String(c.daysPastDue)],
        ['Próxima fecha de pago', c.nextDueDate ? fechaLarga(c.nextDueDate) : 'Sin cuotas por vencer'],
        ['Para pagar hoy', pesos(c.dueToday)],
        ...(c.payoffAmount !== null ? [['Para cancelar el crédito', pesos(c.payoffAmount)] as [string, string]] : []),
      ]
    : [
        ['Saldo', pesos(c.balance)],
        ['Cuota', pesos(c.cycleInstallment)],
        ['Valor vencido', pesos(c.pastDue)],
        ...(c.installment
          ? [
              ['Cuota pendiente', `${c.installment.period}: ${pesos(c.installment.pending)}`] as [string, string],
              ['Vence', fechaLarga(c.installment.dueDate)] as [string, string],
            ]
          : [['Cuotas pendientes', 'Ninguna'] as [string, string]]),
        ['Para pagar hoy', pesos(c.dueToday)],
      ]

  return (
    <>
      <h1>{c.description}</h1>
      <p className="pagina__texto-suave">Valores calculados al {fechaLarga(fecha)}.</p>
      <dl className="tarjeta conceptos__datos">
        {filas.map(([etiqueta, valor]) => (
          <div key={etiqueta} className="conceptos__dato">
            <dt>{etiqueta}</dt>
            <dd>{valor}</dd>
          </div>
        ))}
      </dl>
      {c.blockReasons.map((motivo) => (
        <p key={motivo} className="aviso aviso--advertencia">{motivo}</p>
      ))}
      {!c.payable && (
        <p className="aviso aviso--info">
          Este producto es sólo de consulta en el canal. Para pagarlo, comunícate con tu cooperativa.
        </p>
      )}
    </>
  )
}
