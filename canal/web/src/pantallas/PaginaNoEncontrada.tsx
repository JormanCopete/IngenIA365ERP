import { Link } from 'react-router'

export function PaginaNoEncontrada() {
  return (
    <section className="pagina">
      <h1>Página no encontrada</h1>
      <p className="pagina__texto-suave">
        La dirección que abriste no existe o cambió. Revisa el enlace o vuelve al inicio para seguir desde allí.
      </p>
      <p>
        <Link to="/" className="boton boton--primario">
          Volver al inicio
        </Link>
      </p>
    </section>
  )
}
