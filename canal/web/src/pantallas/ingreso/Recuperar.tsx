import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Link } from 'react-router'
import { AvisoDeError, Campo, CamposDeDocumento, PasosDeCodigo } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { recuperacion, type RespuestaDeInicio } from '../../servicios/identidad'
import { esquemaClaveNueva, esquemaDocumento, type DatosDeClaveNueva, type DatosDeDocumento } from '../../servicios/validaciones'

type Paso = { tipo: 'documento' } | { tipo: 'codigo'; inicio: RespuestaDeInicio } | { tipo: 'clave'; permiso: string } | { tipo: 'listo' }

/**
 * T074 (FR-023): recuperar la clave con un código a los datos de contacto de la cuenta del canal. No abre sesión: al
 * terminar, la persona ingresa con la clave nueva y todas sus sesiones abiertas se cierran.
 */
export function Recuperar() {
  const [paso, setPaso] = useState<Paso>({ tipo: 'documento' })
  return (
    <section className="pagina">
      <h1>Recupera tu clave</h1>
      {paso.tipo === 'documento' && <PasoDocumento alIniciar={(inicio) => setPaso({ tipo: 'codigo', inicio })} />}
      {paso.tipo === 'codigo' && (
        <PasosDeCodigo
          canales={paso.inicio.channels}
          enviar={(canal) => recuperacion.enviar(paso.inicio.challengeId, canal)}
          verificar={async (codigo) => {
            const r = await recuperacion.verificar(paso.inicio.challengeId, codigo)
            setPaso({ tipo: 'clave', permiso: r.recoveryToken })
          }}
        />
      )}
      {paso.tipo === 'clave' && <PasoClave permiso={paso.permiso} alTerminar={() => setPaso({ tipo: 'listo' })} />}
      {paso.tipo === 'listo' && (
        <div className="aviso aviso--exito" role="status">
          <p>Cambiamos tu clave y cerramos tus sesiones abiertas.</p>
          <p>
            <Link to="/ingreso" className="boton boton--primario">Ingresar</Link>
          </p>
        </div>
      )}
      {paso.tipo !== 'listo' && (
        <p>
          <Link to="/ingreso">Volver al ingreso</Link>
        </p>
      )}
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
      alIniciar(await recuperacion.iniciar(datos))
    } catch (e) {
      setError(e)
    }
  })
  return (
    <IndicadorDeCarga cargando={formState.isSubmitting} texto="Un momento…">
      <form className="formulario" onSubmit={(e) => void enviar(e)} noValidate>
        <p className="pagina__texto-suave">Te enviaremos un código al correo o celular de tu cuenta del canal.</p>
        <CamposDeDocumento tipo={register('documentType')} numero={register('documentNumber')} errores={formState.errors} />
        <AvisoDeError error={error} />
        <div className="fila-de-botones">
          <button type="submit" className="boton boton--primario" disabled={formState.isSubmitting}>Continuar</button>
        </div>
      </form>
    </IndicadorDeCarga>
  )
}

function PasoClave({ permiso, alTerminar }: { permiso: string; alTerminar: () => void }) {
  const [error, setError] = useState<unknown>(null)
  const { register, handleSubmit, formState } = useForm<DatosDeClaveNueva>({
    resolver: zodResolver(esquemaClaveNueva),
    defaultValues: { password: '', confirmacion: '' },
  })
  const enviar = handleSubmit(async ({ password }) => {
    setError(null)
    try {
      await recuperacion.completar(permiso, password)
      alTerminar()
    } catch (e) {
      setError(e)
    }
  })
  return (
    <IndicadorDeCarga cargando={formState.isSubmitting} texto="Guardando tu clave…">
      <form className="formulario" onSubmit={(e) => void enviar(e)} noValidate>
        <Campo etiqueta="Clave nueva" error={formState.errors.password} ayuda="Al menos 8 caracteres, con letras y números.">
          {(p) => <input type="password" className="campo__entrada" autoComplete="new-password" {...p} {...register('password')} />}
        </Campo>
        <Campo etiqueta="Repite la clave" error={formState.errors.confirmacion}>
          {(p) => <input type="password" className="campo__entrada" autoComplete="new-password" {...p} {...register('confirmacion')} />}
        </Campo>
        <AvisoDeError error={error} />
        <div className="fila-de-botones">
          <button type="submit" className="boton boton--primario" disabled={formState.isSubmitting}>Cambiar mi clave</button>
        </div>
      </form>
    </IndicadorDeCarga>
  )
}
