import { Link } from 'react-router'
import { usePerfil } from '../../sesion/sesion'

/**
 * Inicio dentro de la cooperativa elegida. La consulta de los conceptos (créditos, aportes y ahorros) llega con la
 * siguiente entrega (US3); mientras tanto, la persona ve con qué cooperativa está y sus accesos.
 */
export function InicioDeCooperativa() {
  const { data } = usePerfil()
  const elegida = data?.tenants.find((t) => t.tenantPublicId === data.tenant)
  return (
    <section className="pagina">
      <h1>Hola, {data?.account.firstNames}</h1>
      <p className="pagina__texto-suave">Estás en {elegida?.displayName ?? 'tu cooperativa'}.</p>
      <div className="tarjeta">
        <p>Muy pronto podrás consultar aquí tus créditos, aportes y ahorros, y pagarlos en línea.</p>
      </div>
      <p className="fila-de-botones">
        <Link to="/perfil">Mi perfil</Link>
        {(data?.tenants.length ?? 0) > 1 && <Link to="/cooperativas">Cambiar de cooperativa</Link>}
      </p>
    </section>
  )
}
