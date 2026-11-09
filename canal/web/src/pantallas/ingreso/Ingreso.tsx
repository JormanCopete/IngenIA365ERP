import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { AvisoDeError, Campo, CamposDeDocumento, PasosDeCodigo } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { alAbrirSesion, ingreso, pideCodigo, type RespuestaDeInicio, type RespuestaDeSesion } from '../../servicios/identidad'
import { ingresarConPasskey, mensajeDePasskey, passkeysDisponibles } from '../../servicios/passkeys'
import { esquemaIngreso, type DatosDeIngreso } from '../../servicios/validaciones'
import { CLAVE_PERFIL, rutaDeRegreso } from '../../sesion/sesion'

const MENSAJE_POR_MOTIVO: Record<string, string> = {
  inactividad: 'Cerramos tu sesión por inactividad. Ingresa de nuevo para continuar donde ibas.',
  maximo: 'Tu sesión llegó a su máximo de 8 horas. Ingresa de nuevo para continuar.',
  vencida: 'Tu sesión terminó. Ingresa de nuevo para continuar.',
  salida: 'Saliste de tu cuenta.',
}

/**
 * T074: ingreso con documento y clave (FR-021). En un dispositivo nuevo pide además un código; con «confiar en este
 * dispositivo» los siguientes ingresos no lo piden. En la PWA también se puede entrar con una llave de acceso.
 */
export function Ingreso() {
  const [parametros] = useSearchParams()
  const navegar = useNavigate()
  const clienteDeConsultas = useQueryClient()
  const volver = rutaDeRegreso(parametros.get('volver'))
  const motivo = parametros.get('motivo')
  const [desafio, setDesafio] = useState<RespuestaDeInicio | null>(null)
  const [confiar, setConfiar] = useState(true)
  const [error, setError] = useState<unknown>(null)
  const [conLlave, setConLlave] = useState(false)

  const entrar = async (sesion: RespuestaDeSesion) => {
    await alAbrirSesion(sesion)
    await clienteDeConsultas.invalidateQueries({ queryKey: CLAVE_PERFIL })
    void navegar(`/cooperativas?volver=${encodeURIComponent(volver)}`, { replace: true })
  }

  const { register, handleSubmit, formState } = useForm<DatosDeIngreso>({
    resolver: zodResolver(esquemaIngreso),
    defaultValues: { documentType: 'CC', documentNumber: '', password: '' },
  })
  const enviar = handleSubmit(async ({ password, ...doc }) => {
    setError(null)
    try {
      const r = await ingreso.entrar(doc, password)
      if (pideCodigo(r)) setDesafio(r)
      else await entrar(r)
    } catch (e) {
      setError(e)
    }
  })

  const entrarConLlave = async () => {
    setConLlave(true)
    setError(null)
    try {
      await entrar(await ingresarConPasskey())
    } catch (e) {
      const mensaje = mensajeDePasskey(e)
      setError(mensaje ?? e)
    } finally {
      setConLlave(false)
    }
  }

  if (desafio) {
    return (
      <section className="pagina">
        <h1>Confirma que eres tú</h1>
        <p className="pagina__texto-suave">Es la primera vez que ingresas desde este dispositivo. Te enviaremos un código.</p>
        <PasosDeCodigo
          canales={desafio.channels}
          enviar={(canal) => ingreso.enviar(desafio.challengeId, canal)}
          verificar={async (codigo) => entrar(await ingreso.verificar(desafio.challengeId, codigo, confiar))}
          textoDelBoton="Ingresar"
          extra={
            <label className="campo--casilla">
              <input type="checkbox" checked={confiar} onChange={(e) => setConfiar(e.target.checked)} />
              Confiar en este dispositivo (no volveremos a pedirte código aquí). No lo marques en un equipo compartido.
            </label>
          }
        />
        <p>
          <button type="button" className="boton boton--enlace" onClick={() => setDesafio(null)}>Volver</button>
        </p>
      </section>
    )
  }

  return (
    <section className="pagina">
      <h1>Ingresa con tu documento</h1>
      {motivo && MENSAJE_POR_MOTIVO[motivo] && <p className="aviso aviso--info" role="status">{MENSAJE_POR_MOTIVO[motivo]}</p>}
      <IndicadorDeCarga cargando={formState.isSubmitting || conLlave} texto="Ingresando…">
        <form className="formulario" onSubmit={(e) => void enviar(e)} noValidate>
          <CamposDeDocumento tipo={register('documentType')} numero={register('documentNumber')} errores={formState.errors} />
          <Campo etiqueta="Clave" error={formState.errors.password}>
            {(p) => <input type="password" className="campo__entrada" autoComplete="current-password" {...p} {...register('password')} />}
          </Campo>
          <AvisoDeError error={error} />
          <div className="fila-de-botones">
            <button type="submit" className="boton boton--primario" disabled={formState.isSubmitting}>Ingresar</button>
            {passkeysDisponibles() && (
              <button type="button" className="boton boton--secundario" onClick={() => void entrarConLlave()} disabled={conLlave}>
                Ingresar con llave de acceso
              </button>
            )}
          </div>
        </form>
      </IndicadorDeCarga>
      <p>
        <Link to="/recuperar">Olvidé mi clave</Link>
      </p>
      <p>
        ¿Primera vez? <Link to="/activar">Activa tu cuenta</Link>
      </p>
    </section>
  )
}
