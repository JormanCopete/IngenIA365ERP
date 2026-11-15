import { isRouteErrorResponse, useNavigate, useRouteError } from 'react-router'
import { MENSAJE_DE_ERROR, TITULO_DE_ERROR } from '../componentes/LimiteDeErrores'
import { PaginaNoEncontrada } from './PaginaNoEncontrada'

/**
 * Pantalla de error de las rutas. El registro en la consola lo hace `RouterProvider onError` (una vez por error);
 * aquí sólo se muestra el mensaje y la salida.
 */
export function PantallaDeError() {
  const error = useRouteError()
  const navegar = useNavigate()

  if (isRouteErrorResponse(error) && error.status === 404) {
    return <PaginaNoEncontrada />
  }

  return (
    <section className="limite-de-errores" role="alert">
      <h1>{TITULO_DE_ERROR}</h1>
      <p>{MENSAJE_DE_ERROR}</p>
      <button
        type="button"
        className="boton boton--primario"
        onClick={() => {
          Promise.resolve(navegar('/')).catch((fallo: unknown) =>
            console.error('[canal] No se pudo volver al inicio.', fallo),
          )
        }}
      >
        Volver al inicio
      </button>
    </section>
  )
}
