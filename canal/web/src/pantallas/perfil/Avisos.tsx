import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { AvisoDeError } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { activarNotificaciones, avisos, NOMBRE_DEL_AVISO, type PreferenciaDeAviso } from '../../servicios/avisos'
import './Avisos.css'

/**
 * T134 (US10, FR-056): qué avisos recibe la persona, por correo y por notificación. Los de seguridad se muestran pero no se
 * apagan. «Activar notificaciones» registra este teléfono o este navegador.
 */
export function Avisos() {
  const clienteDeConsultas = useQueryClient()
  const consulta = useQuery({ queryKey: ['avisos-preferencias'], queryFn: avisos.preferencias })
  const [cambios, setCambios] = useState<Record<string, Pick<PreferenciaDeAviso, 'email' | 'push'>>>({})
  const [guardando, setGuardando] = useState(false)
  const [activando, setActivando] = useState(false)
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [error, setError] = useState<unknown>(null)

  const valor = (p: PreferenciaDeAviso) => cambios[p.kind] ?? { email: p.email, push: p.push }
  const cambiar = (p: PreferenciaDeAviso, campo: 'email' | 'push', v: boolean) =>
    setCambios((c) => ({ ...c, [p.kind]: { ...valor(p), [campo]: v } }))

  const guardar = async () => {
    setGuardando(true)
    setError(null)
    setMensaje(null)
    try {
      const datos = await avisos.guardar(Object.entries(cambios).map(([kind, v]) => ({ kind: kind as PreferenciaDeAviso['kind'], ...v })))
      clienteDeConsultas.setQueryData(['avisos-preferencias'], datos)
      setCambios({})
      setMensaje('Guardamos tus preferencias de aviso.')
    } catch (e) {
      setError(e)
    } finally {
      setGuardando(false)
    }
  }

  const activar = async () => {
    setActivando(true)
    setError(null)
    setMensaje(null)
    try {
      setMensaje((await activarNotificaciones())
        ? 'Listo: este dispositivo recibirá tus avisos.'
        : 'Este dispositivo no permite notificaciones o no diste el permiso. Seguirás recibiendo los avisos por correo.')
    } catch (e) {
      setError(e)
    } finally {
      setActivando(false)
    }
  }

  return (
    <div className="tarjeta" aria-label="Avisos">
      <h2>Avisos</h2>
      <IndicadorDeCarga cargando={consulta.isFetching || guardando || activando} texto={activando ? 'Activando las notificaciones…' : 'Cargando tus avisos…'}>
        {consulta.isError ? (
          <AvisoDeError error={consulta.error} />
        ) : (
          <table className="avisos__tabla">
            <thead>
              <tr>
                <th>Aviso</th>
                <th>Correo</th>
                <th>Notificación</th>
              </tr>
            </thead>
            <tbody>
              {consulta.data?.preferences.map((p) => (
                <tr key={p.kind}>
                  <td>{NOMBRE_DEL_AVISO[p.kind]}</td>
                  <td>
                    <input type="checkbox" aria-label={`${NOMBRE_DEL_AVISO[p.kind]} por correo`} checked={valor(p).email} disabled={p.locked}
                      onChange={(e) => cambiar(p, 'email', e.target.checked)} />
                  </td>
                  <td>
                    <input type="checkbox" aria-label={`${NOMBRE_DEL_AVISO[p.kind]} por notificación`} checked={valor(p).push} disabled={p.locked}
                      onChange={(e) => cambiar(p, 'push', e.target.checked)} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <p className="fila-de-botones">
          <button type="button" className="boton boton--primario" disabled={Object.keys(cambios).length === 0 || guardando} onClick={() => void guardar()}>
            Guardar avisos
          </button>
          <button type="button" className="boton boton--secundario" disabled={activando} onClick={() => void activar()}>
            Activar notificaciones en este dispositivo
          </button>
        </p>
        {mensaje && <p role="status">{mensaje}</p>}
        <AvisoDeError error={error} />
      </IndicadorDeCarga>
    </div>
  )
}
