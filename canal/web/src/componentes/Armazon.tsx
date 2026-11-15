import { useState } from 'react'
import { Link, Outlet } from 'react-router'
import { useMarca } from '../marca/ContextoDeMarca'
import './Armazon.css'

export const NOMBRE_DEL_CANAL = 'Canal del asociado'

/** Nombre y logo de la cooperativa elegida; sin cooperativa, el nombre del canal. */
function RanuraDeMarca() {
  const { marca } = useMarca()
  const [logoFallido, setLogoFallido] = useState<string | null>(null)
  const nombre = marca.nombre ?? NOMBRE_DEL_CANAL
  const mostrarLogo = marca.logoUrl !== null && marca.logoUrl !== logoFallido

  return (
    <Link to="/" className="marca">
      {mostrarLogo && marca.logoUrl && (
        <img
          className="marca__logo"
          src={marca.logoUrl}
          alt=""
          onError={() => {
            console.warn(`[canal] No se pudo cargar el logo de la cooperativa (${marca.logoUrl}); se muestra el nombre.`)
            setLogoFallido(marca.logoUrl)
          }}
        />
      )}
      <span className="marca__nombre">{nombre}</span>
    </Link>
  )
}

/** Armazón de toda pantalla: encabezado con la marca, contenido y pie con la información legal. */
export function Armazon() {
  return (
    <div className="armazon">
      <header className="armazon__encabezado">
        <RanuraDeMarca />
      </header>
      <main className="armazon__contenido">
        <Outlet />
      </main>
      <footer className="armazon__pie">
        <nav aria-label="Información legal y ayuda">
          <ul className="armazon__enlaces">
            <li>
              <Link to="/ayuda/terminos">Términos y condiciones</Link>
            </li>
            <li>
              <Link to="/ayuda/politica">Política de tratamiento de datos</Link>
            </li>
            <li>
              <Link to="/ayuda/preguntas">Preguntas frecuentes</Link>
            </li>
          </ul>
        </nav>
      </footer>
    </div>
  )
}
