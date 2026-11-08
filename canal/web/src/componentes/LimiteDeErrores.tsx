import { Component, type ErrorInfo, type ReactNode } from 'react'
import { detectarPlataforma } from '../plataforma'
import './LimiteDeErrores.css'

export const TITULO_DE_ERROR = 'No pudimos mostrar esta pantalla'
export const MENSAJE_DE_ERROR =
  'Ocurrió un problema inesperado en la aplicación. Vuelve al inicio e inténtalo de nuevo; si sigue pasando, comunícate con tu cooperativa.'

/** Recarga la aplicación en la pantalla de inicio (fuera del enrutador, por si fue él el que falló). */
export function volverAlInicioRecargando(): void {
  if (detectarPlataforma() === 'app') {
    window.location.hash = '#/'
    window.location.reload()
  } else {
    window.location.assign('/')
  }
}

interface Props {
  children: ReactNode
}

interface Estado {
  error: unknown
  hayError: boolean
}

/**
 * Último recurso: atrapa lo que falle fuera de las rutas (proveedores, el enrutador mismo). Los errores de una
 * pantalla los atiende `PantallaDeError` dentro del armazón.
 */
export class LimiteDeErrores extends Component<Props, Estado> {
  override state: Estado = { error: null, hayError: false }

  static getDerivedStateFromError(error: unknown): Estado {
    return { error, hayError: true }
  }

  override componentDidCatch(error: unknown, info: ErrorInfo): void {
    console.error('[canal] Error no controlado en la aplicación.', error, info.componentStack)
  }

  override render() {
    if (!this.state.hayError) return this.props.children
    return (
      <div className="limite-de-errores" role="alert">
        <h1>{TITULO_DE_ERROR}</h1>
        <p>{MENSAJE_DE_ERROR}</p>
        <button type="button" className="boton boton--primario" onClick={volverAlInicioRecargando}>
          Volver al inicio
        </button>
      </div>
    )
  }
}
