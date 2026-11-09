import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router'
import { AvisoDeError, Campo, PasosDeCodigo } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { perfil as apiPerfil, type Dispositivo, type SesionAbierta } from '../../servicios/identidad'
import { mensajeDePasskey, passkeysDisponibles, registrarPasskey } from '../../servicios/passkeys'
import { esquemaCelular, esquemaClaveNueva, esquemaCorreo, type DatosDeClaveNueva } from '../../servicios/validaciones'
import { CLAVE_PERFIL, usePerfil, useSalir } from '../../sesion/sesion'
import { z } from 'zod'

const CLAVE_DISPOSITIVOS = ['dispositivos'] as const

const formatoFecha = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const fecha = (iso: string) => formatoFecha.format(new Date(iso))

/** T076 (FR-024, FR-027): datos de contacto, clave, dispositivos y sesiones de la cuenta del canal. */
export function Perfil() {
  const { data } = usePerfil()
  const salir = useSalir()
  if (!data) return null
  const cuenta = data.account
  return (
    <section className="pagina">
      <h1>Mi perfil</h1>
      <div className="tarjeta">
        <p>
          <strong>{`${cuenta.firstNames} ${cuenta.lastNames}`.trim()}</strong>
          <br />
          {cuenta.documentType} {cuenta.documentNumberMasked}
        </p>
        <p className="pagina__texto-suave">
          Estos datos son los de tu cuenta del canal. Si cambias tu correo o tu celular aquí, tus cooperativas reciben el
          aviso y deciden si actualizan sus registros.
        </p>
      </div>
      <CambioDeContacto campo="email" titulo="Correo" actual={cuenta.emailMasked} />
      <CambioDeContacto campo="mobile" titulo="Celular" actual={cuenta.mobileMasked} />
      <CambioDeClave />
      <DispositivosYSesiones passkeysDelServidor={data.passkeysAvailable} />
      <p className="fila-de-botones">
        {data.tenants.length > 1 && <Link to="/cooperativas">Cambiar de cooperativa</Link>}
        <button type="button" className="boton boton--secundario" onClick={() => void salir('salida')}>Salir</button>
      </p>
    </section>
  )
}

function CambioDeContacto({ campo, titulo, actual }: { campo: 'email' | 'mobile'; titulo: string; actual: string | null }) {
  const clienteDeConsultas = useQueryClient()
  const [abierto, setAbierto] = useState(false)
  const [desafio, setDesafio] = useState<{ id: string; enviadoA: string } | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [listo, setListo] = useState(false)
  const esquema = campo === 'email' ? esquemaCorreo : esquemaCelular
  const { register, handleSubmit, formState, reset } = useForm<z.input<typeof esquema>, unknown, z.output<typeof esquema>>({
    resolver: zodResolver(esquema),
    defaultValues: { value: '' },
  })

  const pedir = handleSubmit(async ({ value }) => {
    setError(null)
    try {
      const r = await apiPerfil.pedirCambioDeContacto(campo, value)
      setDesafio({ id: r.challengeId, enviadoA: r.sentTo })
    } catch (e) {
      setError(e)
    }
  })

  const cerrar = () => {
    setAbierto(false)
    setDesafio(null)
    setError(null)
    reset({ value: '' })
  }

  return (
    <div className="tarjeta">
      <h2>{titulo}</h2>
      <p>{actual ?? 'Sin registrar'}</p>
      {listo && <p className="aviso aviso--exito" role="status">Guardamos tu {titulo.toLowerCase()} nuevo y avisamos al anterior.</p>}
      {!abierto && (
        <p>
          <button type="button" className="boton boton--secundario" onClick={() => { setAbierto(true); setListo(false) }}>
            {actual ? `Cambiar ${titulo.toLowerCase()}` : `Agregar ${titulo.toLowerCase()}`}
          </button>
        </p>
      )}
      {abierto && !desafio && (
        <IndicadorDeCarga cargando={formState.isSubmitting} texto="Enviando el código…">
          <form className="formulario" onSubmit={(e) => void pedir(e)} noValidate>
            <Campo etiqueta={`${titulo} nuevo`} error={formState.errors.value}
              ayuda="Te enviaremos un código para confirmar que es tuyo.">
              {(p) => (
                <input className="campo__entrada" type={campo === 'email' ? 'email' : 'tel'}
                  autoComplete={campo === 'email' ? 'email' : 'tel-national'} {...p} {...register('value')} />
              )}
            </Campo>
            <AvisoDeError error={error} />
            <div className="fila-de-botones">
              <button type="submit" className="boton boton--primario" disabled={formState.isSubmitting}>Enviar código</button>
              <button type="button" className="boton boton--enlace" onClick={cerrar}>Cancelar</button>
            </div>
          </form>
        </IndicadorDeCarga>
      )}
      {abierto && desafio && (
        <PasosDeCodigo
          canales={[{ kind: campo === 'email' ? 'email' : 'sms', masked: desafio.enviadoA }]}
          enviadoA={desafio.enviadoA}
          enviar={(canal) => apiPerfil.reenviarCodigoDeContacto(desafio.id, canal)}
          verificar={async (codigo) => {
            await apiPerfil.confirmarCambioDeContacto(desafio.id, codigo)
            await clienteDeConsultas.invalidateQueries({ queryKey: CLAVE_PERFIL })
            cerrar()
            setListo(true)
          }}
          textoDelBoton="Confirmar"
        />
      )}
    </div>
  )
}

const esquemaCambioDeClave = z
  .object({ actual: z.string().min(1, 'Escribe tu clave actual.') })
  .and(esquemaClaveNueva)

function CambioDeClave() {
  const [abierto, setAbierto] = useState(false)
  const [listo, setListo] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const { register, handleSubmit, formState, reset } = useForm<{ actual: string } & DatosDeClaveNueva>({
    resolver: zodResolver(esquemaCambioDeClave),
    defaultValues: { actual: '', password: '', confirmacion: '' },
  })
  const enviar = handleSubmit(async ({ actual, password }) => {
    setError(null)
    try {
      await apiPerfil.cambiarClave(actual, password)
      reset()
      setAbierto(false)
      setListo(true)
    } catch (e) {
      setError(e)
    }
  })
  return (
    <div className="tarjeta">
      <h2>Clave</h2>
      {listo && <p className="aviso aviso--exito" role="status">Cambiaste tu clave y cerramos tus otras sesiones.</p>}
      {!abierto ? (
        <p>
          <button type="button" className="boton boton--secundario" onClick={() => { setAbierto(true); setListo(false) }}>
            Cambiar mi clave
          </button>
        </p>
      ) : (
        <IndicadorDeCarga cargando={formState.isSubmitting} texto="Guardando…">
          <form className="formulario" onSubmit={(e) => void enviar(e)} noValidate>
            <Campo etiqueta="Clave actual" error={formState.errors.actual}>
              {(p) => <input type="password" className="campo__entrada" autoComplete="current-password" {...p} {...register('actual')} />}
            </Campo>
            <Campo etiqueta="Clave nueva" error={formState.errors.password} ayuda="Al menos 8 caracteres, con letras y números.">
              {(p) => <input type="password" className="campo__entrada" autoComplete="new-password" {...p} {...register('password')} />}
            </Campo>
            <Campo etiqueta="Repite la clave nueva" error={formState.errors.confirmacion}>
              {(p) => <input type="password" className="campo__entrada" autoComplete="new-password" {...p} {...register('confirmacion')} />}
            </Campo>
            <AvisoDeError error={error} />
            <div className="fila-de-botones">
              <button type="submit" className="boton boton--primario" disabled={formState.isSubmitting}>Cambiar clave</button>
              <button type="button" className="boton boton--enlace" onClick={() => { setAbierto(false); reset() }}>Cancelar</button>
            </div>
          </form>
        </IndicadorDeCarga>
      )}
    </div>
  )
}

function DispositivosYSesiones({ passkeysDelServidor }: { passkeysDelServidor: boolean }) {
  const clienteDeConsultas = useQueryClient()
  const consulta = useQuery({ queryKey: CLAVE_DISPOSITIVOS, queryFn: apiPerfil.dispositivos })
  const [mensaje, setMensaje] = useState<string | null>(null)
  const actualizar = () => clienteDeConsultas.invalidateQueries({ queryKey: CLAVE_DISPOSITIVOS })
  const revocar = useMutation({ mutationFn: (d: Dispositivo) => apiPerfil.revocarDispositivo(d.id), onSuccess: actualizar })
  const cerrar = useMutation({ mutationFn: (s: SesionAbierta) => apiPerfil.cerrarSesion(s.id), onSuccess: actualizar })
  const llave = useMutation({
    mutationFn: registrarPasskey,
    onSuccess: async () => {
      setMensaje('Guardamos la llave de acceso de este dispositivo.')
      await actualizar()
    },
  })
  const ocupado = consulta.isFetching || revocar.isPending || cerrar.isPending || llave.isPending
  const errorDeLlave = llave.error ? (mensajeDePasskey(llave.error) ?? llave.error) : null

  return (
    <div className="tarjeta">
      <h2>Dispositivos y sesiones</h2>
      <IndicadorDeCarga cargando={ocupado} texto="Cargando…">
        {consulta.error && <AvisoDeError error={consulta.error} />}
        {consulta.data && (
          <>
            <h3>Dispositivos</h3>
            {consulta.data.devices.length === 0 ? (
              <p className="pagina__texto-suave">No hay dispositivos de confianza.</p>
            ) : (
              <ul className="lista-simple">
                {consulta.data.devices.map((d) => (
                  <li key={d.id} className="fila-de-botones">
                    <span>
                      {d.label}
                      {d.current && ' (este)'}
                      <br />
                      <span className="pagina__texto-suave">Último uso: {fecha(d.lastSeenAt)}</span>
                    </span>
                    <button type="button" className="boton boton--enlace" onClick={() => revocar.mutate(d)} disabled={ocupado}>
                      Quitar
                    </button>
                  </li>
                ))}
              </ul>
            )}
            <h3>Sesiones abiertas</h3>
            <ul className="lista-simple">
              {consulta.data.sessions.map((s) => (
                <li key={s.id} className="fila-de-botones">
                  <span>
                    {s.label}
                    {s.current && ' (esta sesión)'}
                    <br />
                    <span className="pagina__texto-suave">Desde {fecha(s.createdAt)}</span>
                  </span>
                  {!s.current && (
                    <button type="button" className="boton boton--enlace" onClick={() => cerrar.mutate(s)} disabled={ocupado}>
                      Cerrar
                    </button>
                  )}
                </li>
              ))}
            </ul>
          </>
        )}
        <AvisoDeError error={revocar.error ?? cerrar.error ?? errorDeLlave} />
        {mensaje && <p className="aviso aviso--exito" role="status">{mensaje}</p>}
        {passkeysDelServidor && passkeysDisponibles() && (
          <p>
            <button type="button" className="boton boton--secundario" onClick={() => llave.mutate()} disabled={ocupado}>
              Crear una llave de acceso en este dispositivo
            </button>
          </p>
        )}
      </IndicadorDeCarga>
    </div>
  )
}
