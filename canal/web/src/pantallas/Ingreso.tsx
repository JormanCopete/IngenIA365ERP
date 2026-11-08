import { Link } from 'react-router'

/** Lugar del ingreso con documento y clave (entrega C1). */
export function Ingreso() {
  return (
    <section className="pagina">
      <h1>Ingresa con tu documento</h1>
      <p className="pagina__texto-suave">
        Aquí podrás ingresar con tu tipo y número de documento y tu clave. Esta opción todavía no está habilitada;
        mientras tanto, comunícate con tu cooperativa para cualquier consulta.
      </p>
      <p>
        <Link to="/">Volver al inicio</Link>
      </p>
    </section>
  )
}
