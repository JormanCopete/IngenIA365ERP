import { useEffect, useRef, useState } from 'react'
import { useLocation } from 'react-router'
import { escucharSesion } from '../servicios/api'
import { sesion as apiSesion, type EstadoDeSesion } from '../servicios/identidad'
import { mensajeParaElUsuario } from '../servicios/consultas'
import { rutaActual, useSalir } from '../sesion/sesion'
import './AvisoDeSesion.css'

/** Cuánto antes del corte aparece el aviso (FR-027: al minuto 4 de 5). */
export const ANTICIPACION_DEL_AVISO_MS = 60_000
/**
 * El servidor anota la actividad cada 15 s como mucho; el reloj de aquí corre con ese margen para no creer viva una
 * sesión que el servidor ya cortó.
 */
export const MARGEN_DEL_SERVIDOR_MS = 20_000

interface Props {
  sesion: EstadoDeSesion
  /** Reloj inyectable para las pruebas. */
  ahora?: () => number
}

/**
 * T077: aviso de vencimiento de la sesión. Lleva los dos relojes: la inactividad (5 minutos o lo que diga la
 * cooperativa, hasta 15), que se reinicia con cada respuesta del servidor, y el máximo de 8 horas desde el ingreso,
 * que nada alarga. Un minuto antes del corte avisa; «Seguir trabajando» renueva la inactividad. A cero, sale al
 * ingreso guardando la ruta para volver.
 */
export function AvisoDeSesion({ sesion, ahora = Date.now }: Props) {
  const ubicacion = useLocation()
  const salir = useSalir()
  const ventanaMs = sesion.inactivityMinutes * 60_000
  const maximo = Date.parse(sesion.expiresAt)
  const [venceInactividad, setVenceInactividad] = useState(
    () => Math.min(Date.parse(sesion.idleExpiresAt), ahora() + ventanaMs) - MARGEN_DEL_SERVIDOR_MS,
  )
  const [instante, setInstante] = useState(ahora)
  const [renovando, setRenovando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const salido = useRef(false)
  const ruta = rutaActual(ubicacion)

  const porMaximo = maximo <= venceInactividad
  const restante = Math.min(venceInactividad, maximo) - instante

  // Cada respuesta del servidor renueva la inactividad; «vencida» sale enseguida.
  useEffect(
    () =>
      escucharSesion((evento) => {
        if (evento === 'vencida') {
          if (!salido.current) {
            salido.current = true
            void salir('vencida', ruta)
          }
          return
        }
        setVenceInactividad(ahora() + ventanaMs - MARGEN_DEL_SERVIDOR_MS)
      }),
    [ahora, ventanaMs, salir, ruta],
  )

  useEffect(() => {
    const id = window.setInterval(() => setInstante(ahora()), 1000)
    return () => window.clearInterval(id)
  }, [ahora])

  useEffect(() => {
    if (restante <= 0 && !salido.current) {
      salido.current = true
      void salir(porMaximo ? 'maximo' : 'inactividad', ruta)
    }
  }, [restante, porMaximo, salir, ruta])

  const seguir = async () => {
    setRenovando(true)
    setError(null)
    try {
      await apiSesion.seguir()
      setInstante(ahora())
    } catch (e) {
      setError(mensajeParaElUsuario(e))
    } finally {
      setRenovando(false)
    }
  }

  if (restante > ANTICIPACION_DEL_AVISO_MS) return null
  const segundos = Math.max(0, Math.ceil(restante / 1000))

  return (
    <div className="aviso-de-sesion" role="alertdialog" aria-modal="true" aria-labelledby="aviso-de-sesion-titulo"
      aria-describedby="aviso-de-sesion-texto">
      <div className="aviso-de-sesion__caja">
        <h2 id="aviso-de-sesion-titulo">{porMaximo ? 'Tu sesión está por terminar' : '¿Sigues ahí?'}</h2>
        <p id="aviso-de-sesion-texto">
          {porMaximo
            ? `Por tu seguridad, la sesión dura como máximo 8 horas. Se cerrará en ${segundos} segundos; vuelve a ingresar para continuar.`
            : `Por tu seguridad, cerraremos la sesión en ${segundos} segundos si no hay actividad.`}
        </p>
        {error && (
          <p className="aviso aviso--error" role="alert">
            {error}
          </p>
        )}
        <div className="aviso-de-sesion__acciones">
          {!porMaximo && (
            <button type="button" className="boton boton--primario" onClick={() => void seguir()} disabled={renovando} autoFocus>
              {renovando ? 'Un momento…' : 'Seguir trabajando'}
            </button>
          )}
          <button
            type="button"
            className="boton boton--secundario"
            onClick={() => {
              salido.current = true
              void salir('salida')
            }}
          >
            Salir ahora
          </button>
        </div>
      </div>
    </div>
  )
}
