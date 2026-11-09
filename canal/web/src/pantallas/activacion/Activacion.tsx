import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link, useNavigate } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { AvisoDeError, Campo, CamposDeDocumento, PasosDeCodigo } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { activacion, alAbrirSesion, type RespuestaDeInicio, type Soporte, type TextoParaAceptar } from '../../servicios/identidad'
import { esquemaClaveNueva, esquemaDocumento, type DatosDeClaveNueva, type DatosDeDocumento } from '../../servicios/validaciones'
import { CLAVE_PERFIL } from '../../sesion/sesion'

type Paso =
  | { tipo: 'documento' }
  | { tipo: 'codigo'; inicio: RespuestaDeInicio }
  | { tipo: 'sinDatos'; entidad: string; soporte?: Soporte | undefined }
  | { tipo: 'clave'; permiso: string; entidad: string; textos: TextoParaAceptar[] }
  | { tipo: 'yaActiva' }

const NOMBRE_DEL_TEXTO: Record<string, string> = {
  Terms: 'Términos y condiciones',
  DataPolicy: 'Política de tratamiento de datos',
}

/**
 * T073: activar la cuenta del canal (FR-016 a FR-020). La persona escribe su documento, recibe un código en el correo
 * o el celular que tiene su entidad validadora, crea su clave y acepta los textos.
 */
export function Activacion() {
  const [paso, setPaso] = useState<Paso>({ tipo: 'documento' })

  return (
    <section className="pagina">
      <h1>Activa tu cuenta</h1>
      {paso.tipo === 'documento' && <PasoDocumento alIniciar={(inicio) => setPaso(inicio.needsEntityUpdate
        ? { tipo: 'sinDatos', entidad: inicio.entityName ?? 'tu cooperativa', soporte: inicio.support }
        : { tipo: 'codigo', inicio })} />}
      {paso.tipo === 'codigo' && (
        <>
          <p className="pagina__texto-suave">
            Para confirmar que eres tú, te enviaremos un código a los datos que tiene registrados tu cooperativa.
          </p>
          <PasosDeCodigo
            canales={paso.inicio.channels}
            enviar={(canal) => activacion.enviar(paso.inicio.challengeId, canal)}
            verificar={async (codigo) => {
              const r = await activacion.verificar(paso.inicio.challengeId, codigo)
              if (r.alreadyActive) {
                setPaso({ tipo: 'yaActiva' })
                return
              }
              setPaso({ tipo: 'clave', permiso: r.activationToken ?? '', entidad: r.entityName ?? '', textos: r.legalTexts ?? [] })
            }}
          />
        </>
      )}
      {paso.tipo === 'sinDatos' && <SinDatos entidad={paso.entidad} soporte={paso.soporte} />}
      {paso.tipo === 'clave' && <PasoClave permiso={paso.permiso} entidad={paso.entidad} textos={paso.textos} />}
      {paso.tipo === 'yaActiva' && (
        <div className="aviso aviso--info" role="status">
          <p>Ya tienes una cuenta en el canal.</p>
          <p className="fila-de-botones">
            <Link to="/ingreso" className="boton boton--primario">Ingresar</Link>
            <Link to="/recuperar">Recuperar mi clave</Link>
          </p>
        </div>
      )}
      <p>
        ¿Ya activaste tu cuenta? <Link to="/ingreso">Ingresa aquí</Link>
      </p>
    </section>
  )
}

function PasoDocumento({ alIniciar }: { alIniciar: (r: RespuestaDeInicio) => void }) {
  const [error, setError] = useState<unknown>(null)
  const { register, handleSubmit, formState } = useForm<DatosDeDocumento>({
    resolver: zodResolver(esquemaDocumento),
    defaultValues: { documentType: 'CC', documentNumber: '' },
  })
  const enviar = handleSubmit(async (datos) => {
    setError(null)
    try {
      alIniciar(await activacion.iniciar(datos))
    } catch (e) {
      setError(e)
    }
  })
  return (
    <IndicadorDeCarga cargando={formState.isSubmitting} texto="Buscando tus datos…">
      <form className="formulario" onSubmit={(e) => void enviar(e)} noValidate>
        <CamposDeDocumento tipo={register('documentType')} numero={register('documentNumber')} errores={formState.errors} />
        <AvisoDeError error={error} />
        <div className="fila-de-botones">
          <button type="submit" className="boton boton--primario" disabled={formState.isSubmitting}>Continuar</button>
        </div>
      </form>
    </IndicadorDeCarga>
  )
}

/** FR-019: la entidad validadora no tiene a dónde enviar el código; no hay otro camino que acercarse a ella. */
function SinDatos({ entidad, soporte }: { entidad: string; soporte?: Soporte | undefined }) {
  return (
    <div className="tarjeta" role="status">
      <p>
        {entidad} no tiene registrado un correo o un celular donde enviarte el código. Acércate o comunícate con ella para
        actualizar tus datos y vuelve a intentarlo.
      </p>
      {soporte && (soporte.phone || soporte.email || soporte.hours) && (
        <ul className="lista-simple">
          {soporte.phone && <li>Teléfono: {soporte.phone}</li>}
          {soporte.email && <li>Correo: {soporte.email}</li>}
          {soporte.hours && <li>Horario: {soporte.hours}</li>}
        </ul>
      )}
    </div>
  )
}

function PasoClave({ permiso, entidad, textos }: { permiso: string; entidad: string; textos: TextoParaAceptar[] }) {
  const navegar = useNavigate()
  const clienteDeConsultas = useQueryClient()
  const [aceptados, setAceptados] = useState<Record<string, boolean>>({})
  const [error, setError] = useState<unknown>(null)
  const [vigentes, setVigentes] = useState(textos)
  const { register, handleSubmit, formState } = useForm<DatosDeClaveNueva>({
    resolver: zodResolver(esquemaClaveNueva),
    defaultValues: { password: '', confirmacion: '' },
  })
  const todosAceptados = vigentes.every((t) => aceptados[`${t.kind}:${t.version}`])

  const enviar = handleSubmit(async ({ password }) => {
    setError(null)
    try {
      const sesion = await activacion.completar(permiso, password, vigentes.map((t) => ({ kind: t.kind, version: t.version })))
      await alAbrirSesion(sesion)
      await clienteDeConsultas.invalidateQueries({ queryKey: CLAVE_PERFIL })
      void navegar('/cooperativas', { replace: true })
    } catch (e) {
      // Si los textos cambiaron mientras tanto, el servicio manda los vigentes: se muestran para aceptarlos de nuevo.
      const datos = (e as { data?: { legalTexts?: TextoParaAceptar[] } }).data
      if (datos?.legalTexts) {
        setVigentes(datos.legalTexts)
        setAceptados({})
      }
      setError(e)
    }
  })

  return (
    <IndicadorDeCarga cargando={formState.isSubmitting} texto="Activando tu cuenta…">
      <form className="formulario" onSubmit={(e) => void enviar(e)} noValidate>
        <p className="aviso aviso--exito">Listo, confirmamos que eres tú{entidad ? ` (${entidad})` : ''}. Ahora crea tu clave.</p>
        <Campo etiqueta="Clave" error={formState.errors.password} ayuda="Al menos 8 caracteres, con letras y números.">
          {(p) => <input type="password" className="campo__entrada" autoComplete="new-password" {...p} {...register('password')} />}
        </Campo>
        <Campo etiqueta="Repite la clave" error={formState.errors.confirmacion}>
          {(p) => <input type="password" className="campo__entrada" autoComplete="new-password" {...p} {...register('confirmacion')} />}
        </Campo>
        {vigentes.map((t) => {
          const clave = `${t.kind}:${t.version}`
          const nombre = NOMBRE_DEL_TEXTO[t.kind] ?? t.kind
          return (
            <div key={clave} className="campo">
              <span className="campo__etiqueta">{nombre}</span>
              <div className="texto-legal" tabIndex={0} aria-label={nombre}>{t.content}</div>
              <label className="campo--casilla">
                <input type="checkbox" checked={Boolean(aceptados[clave])}
                  onChange={(e) => setAceptados((a) => ({ ...a, [clave]: e.target.checked }))} />
                Leí y acepto {nombre.toLowerCase()} (versión {t.version}).
              </label>
            </div>
          )
        })}
        <AvisoDeError error={error} />
        <div className="fila-de-botones">
          <button type="submit" className="boton boton--primario" disabled={formState.isSubmitting || !todosAceptados}>
            Activar mi cuenta
          </button>
        </div>
      </form>
    </IndicadorDeCarga>
  )
}
