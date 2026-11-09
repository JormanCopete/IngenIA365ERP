import { Link } from 'react-router'

export function Inicio() {
  return (
    <section className="pagina">
      <h1>Te damos la bienvenida</h1>
      <p className="pagina__texto-suave">
        Consulta tus créditos, aportes y ahorros en tu cooperativa y paga en línea de forma segura.
      </p>
      <p className="fila-de-botones">
        <Link to="/ingreso" className="boton boton--primario">
          Ingresa con tu documento
        </Link>
        <Link to="/activar" className="boton boton--secundario">
          Activa tu cuenta
        </Link>
      </p>
    </section>
  )
}
