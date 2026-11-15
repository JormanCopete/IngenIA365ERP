import { Link } from 'react-router'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { ErrorDelCanal } from '../../servicios/api'
import { mensajeParaElUsuario } from '../../servicios/consultas'
import {
  CODIGO_ENTIDAD_NO_DISPONIBLE,
  fechaLarga,
  GRUPOS,
  pesos,
  rutaDelConcepto,
  useConceptos,
  type Concepto,
} from '../../servicios/conceptos'
import { usePerfil } from '../../sesion/sesion'
import './Conceptos.css'

/**
 * T083 (US3; FR-029 a FR-031): inicio dentro de la cooperativa elegida, con sus créditos, aportes y ahorros por tipo y
 * la fecha a la que el ERP los calculó. Si el ERP no responde, lo dice y no muestra nada viejo.
 */
export function Conceptos() {
  const { data: perfil } = usePerfil()
  const elegida = perfil?.tenants.find((t) => t.tenantPublicId === perfil.tenant)
  const consulta = useConceptos(perfil?.tenant ?? null)

  return (
    <section className="pagina">
      <h1>Hola, {perfil?.account.firstNames}</h1>
      <p className="pagina__texto-suave">Estás en {elegida?.displayName ?? 'tu cooperativa'}.</p>
      <IndicadorDeCarga cargando={consulta.isFetching} texto="Consultando tus productos…">
        {consulta.isError ? (
          <NoDisponible error={consulta.error} reintentar={() => void consulta.refetch()} />
        ) : consulta.data ? (
          <>
            <p className="pagina__texto-suave">Valores calculados al {fechaLarga(consulta.data.asOf)}.</p>
            {consulta.data.notices.map((aviso) => (
              <p key={aviso} className="aviso aviso--advertencia">{aviso}</p>
            ))}
            {consulta.data.items.length === 0 ? (
              <div className="tarjeta">
                <p>No tienes productos para consultar en el canal de {elegida?.displayName ?? 'tu cooperativa'}.</p>
              </div>
            ) : (
              GRUPOS.map((grupo) => {
                const items = consulta.data.items.filter((i) => grupo.tipos.includes(i.kind))
                if (items.length === 0) return null
                return (
                  <section key={grupo.titulo} aria-labelledby={`grupo-${grupo.titulo}`} className="conceptos__grupo">
                    <h2 id={`grupo-${grupo.titulo}`}>{grupo.titulo}</h2>
                    <ul className="lista-simple">
                      {items.map((c) => (
                        <li key={c.conceptKey}>
                          <TarjetaDeConcepto concepto={c} />
                        </li>
                      ))}
                    </ul>
                  </section>
                )
              })
            )}
          </>
        ) : null}
      </IndicadorDeCarga>
      <p className="fila-de-botones">
        {consulta.data?.items.some((i) => i.payable) && (
          <Link to="/cooperativa/pagar" className="boton boton--primario">Pagar en línea</Link>
        )}
        <Link to="/cooperativa/pagos">Mis pagos</Link>
        <Link to="/cooperativa/documentos">Extractos y certificados</Link>
        <Link to="/cooperativa/credito">Solicitar un crédito</Link>
        <Link to="/perfil">Mi perfil</Link>
        {(perfil?.tenants.length ?? 0) > 1 && <Link to="/cooperativas">Cambiar de cooperativa</Link>}
      </p>
    </section>
  )
}

function TarjetaDeConcepto({ concepto: c }: { concepto: Concepto }) {
  const vencido = c.daysPastDue > 0 || c.pastDue > 0
  return (
    <Link to={rutaDelConcepto(c)} className="tarjeta conceptos__tarjeta">
      <span className="conceptos__titulo">{c.description}</span>
      <span className="conceptos__cifra">
        <span className="conceptos__etiqueta">Para pagar hoy</span>
        <strong>{pesos(c.dueToday)}</strong>
      </span>
      <span className="conceptos__detalle">
        {vencido && <span className="conceptos__mora">{c.daysPastDue > 0 ? `${c.daysPastDue} días de mora` : 'Tiene valores vencidos'}</span>}
        {c.nextDueDate && <span>Próxima fecha: {fechaLarga(c.nextDueDate)}</span>}
        {!c.payable && <span>Sólo consulta</span>}
      </span>
    </Link>
  )
}

/** FR-031: el ERP de la cooperativa no respondió. Nunca se muestran valores guardados. */
export function NoDisponible({ error, reintentar }: { error: unknown; reintentar: () => void }) {
  const caido = error instanceof ErrorDelCanal && (error.code === CODIGO_ENTIDAD_NO_DISPONIBLE || error.esTransitorio)
  return (
    <div className="tarjeta" role="alert">
      <h2>Información no disponible</h2>
      <p>
        {caido
          ? 'En este momento no podemos consultar la información de tu cooperativa. Intenta de nuevo en unos minutos.'
          : mensajeParaElUsuario(error)}
      </p>
      <p>
        <button type="button" className="boton boton--secundario" onClick={reintentar}>Intentar de nuevo</button>
      </p>
    </div>
  )
}
