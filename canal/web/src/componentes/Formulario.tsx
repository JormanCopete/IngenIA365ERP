import { useId, useState, type ReactNode } from 'react'
import { useForm, type FieldError, type UseFormRegisterReturn } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { ErrorDelCanal } from '../servicios/api'
import { mensajeParaElUsuario } from '../servicios/consultas'
import { TIPOS_DE_DOCUMENTO, type CanalOfrecido, type RespuestaDeEnvio, type TipoDeCanal } from '../servicios/identidad'
import { esquemaCodigo } from '../servicios/validaciones'
import { IndicadorDeCarga } from './IndicadorDeCarga'

/** Mensaje de error de la operación, anunciado al lector de pantalla. Un texto se muestra tal cual. */
export function AvisoDeError({ error }: { error: unknown }) {
  if (!error) return null
  return (
    <p className="aviso aviso--error" role="alert">
      {typeof error === 'string' ? error : mensajeParaElUsuario(error)}
    </p>
  )
}

interface PropsDeCampo {
  etiqueta: string
  error?: FieldError | undefined
  ayuda?: ReactNode | undefined
  children: (props: { id: string; 'aria-invalid': boolean; 'aria-describedby'?: string | undefined }) => ReactNode
}

/** Etiqueta, entrada, ayuda y error con los atributos de accesibilidad conectados. */
export function Campo({ etiqueta, error, ayuda, children }: PropsDeCampo) {
  const id = useId()
  const idAyuda = `${id}-ayuda`
  const idError = `${id}-error`
  const describe = [ayuda ? idAyuda : null, error ? idError : null].filter(Boolean).join(' ') || undefined
  return (
    <div className="campo">
      <label className="campo__etiqueta" htmlFor={id}>
        {etiqueta}
      </label>
      {children({ id, 'aria-invalid': Boolean(error), 'aria-describedby': describe })}
      {ayuda && (
        <span className="campo__ayuda" id={idAyuda}>
          {ayuda}
        </span>
      )}
      {error?.message && (
        <span className="campo__error" id={idError}>
          {error.message}
        </span>
      )}
    </div>
  )
}

/** Tipo y número de documento. */
export function CamposDeDocumento({
  tipo,
  numero,
  errores,
}: {
  tipo: UseFormRegisterReturn
  numero: UseFormRegisterReturn
  errores: { documentType?: FieldError | undefined; documentNumber?: FieldError | undefined }
}) {
  return (
    <>
      <Campo etiqueta="Tipo de documento" error={errores.documentType}>
        {(p) => (
          <select className="campo__entrada" {...p} {...tipo}>
            {TIPOS_DE_DOCUMENTO.map((t) => (
              <option key={t.codigo} value={t.codigo}>
                {t.nombre}
              </option>
            ))}
          </select>
        )}
      </Campo>
      <Campo etiqueta="Número de documento" error={errores.documentNumber}>
        {(p) => <input className="campo__entrada" inputMode="text" autoComplete="username" {...p} {...numero} />}
      </Campo>
    </>
  )
}

const NOMBRE_DEL_CANAL: Record<TipoDeCanal, string> = { email: 'Correo', sms: 'Mensaje de texto (SMS)' }

interface PropsDePasosDeCodigo {
  canales: CanalOfrecido[]
  enviar: (canal: TipoDeCanal) => Promise<RespuestaDeEnvio>
  verificar: (codigo: string) => Promise<void>
  /** Contenido extra junto al código (p. ej. «confiar en este dispositivo»). */
  extra?: ReactNode | undefined
  textoDelBoton?: string | undefined
  /** Si ya se envió (el cambio de contacto envía al pedirlo). */
  enviadoA?: string | undefined
}

/**
 * Elegir por dónde recibir el código (FR-011a: si hay más de un canal, la persona elige), enviarlo y escribirlo. Con
 * un solo canal, se ofrece enviar directo.
 */
export function PasosDeCodigo({ canales, enviar, verificar, extra, textoDelBoton = 'Continuar', enviadoA }: PropsDePasosDeCodigo) {
  const [canal, setCanal] = useState<TipoDeCanal>(canales[0]?.kind ?? 'email')
  const [enviado, setEnviado] = useState<string | null>(enviadoA ?? null)
  const [enviando, setEnviando] = useState(false)
  const [errorDeEnvio, setErrorDeEnvio] = useState<unknown>(null)
  const formulario = useForm<{ code: string }>({ resolver: zodResolver(esquemaCodigo), defaultValues: { code: '' } })
  const [errorDeVerificacion, setErrorDeVerificacion] = useState<unknown>(null)
  const nombreDelGrupo = useId()

  const pedirCodigo = async () => {
    setEnviando(true)
    setErrorDeEnvio(null)
    try {
      const r = await enviar(canal)
      setEnviado(r.sentTo)
      formulario.reset({ code: '' })
    } catch (e) {
      setErrorDeEnvio(e)
    } finally {
      setEnviando(false)
    }
  }

  const alVerificar = formulario.handleSubmit(async ({ code }) => {
    setErrorDeVerificacion(null)
    try {
      await verificar(code)
    } catch (e) {
      setErrorDeVerificacion(e)
      if (e instanceof ErrorDelCanal && (e.code === 'Canal.Codigo.Vencido' || e.code === 'Canal.Codigo.Agotado')) {
        setEnviado(null)
      }
    }
  })

  if (!enviado) {
    return (
      <IndicadorDeCarga cargando={enviando} texto="Enviando el código…">
        <div className="formulario">
          {canales.length > 1 ? (
            <fieldset className="campo">
              <legend className="campo__etiqueta">¿Por dónde quieres recibir el código?</legend>
              {canales.map((c) => (
                <label key={c.kind} className="campo--casilla">
                  <input type="radio" name={nombreDelGrupo} value={c.kind} checked={canal === c.kind}
                    onChange={() => setCanal(c.kind)} />
                  {NOMBRE_DEL_CANAL[c.kind]}: {c.masked}
                </label>
              ))}
            </fieldset>
          ) : (
            <p>
              Te enviaremos un código a {canales[0] ? `tu ${NOMBRE_DEL_CANAL[canales[0].kind].toLowerCase()} ${canales[0].masked}` : 'tu correo'}.
            </p>
          )}
          <AvisoDeError error={errorDeVerificacion ?? errorDeEnvio} />
          <div className="fila-de-botones">
            <button type="button" className="boton boton--primario" onClick={() => void pedirCodigo()} disabled={enviando}>
              Enviar código
            </button>
          </div>
        </div>
      </IndicadorDeCarga>
    )
  }

  return (
    <IndicadorDeCarga cargando={formulario.formState.isSubmitting} texto="Verificando…">
      <form className="formulario" onSubmit={(e) => void alVerificar(e)} noValidate>
        <p className="aviso aviso--info">
          Enviamos un código de 6 dígitos a {enviado}. Vence en 5 minutos.
        </p>
        <Campo etiqueta="Código" error={formulario.formState.errors.code}>
          {(p) => (
            <input className="campo__entrada campo__entrada--codigo" inputMode="numeric" autoComplete="one-time-code"
              maxLength={6} {...p} {...formulario.register('code')} />
          )}
        </Campo>
        {extra}
        <AvisoDeError error={errorDeVerificacion} />
        <div className="fila-de-botones">
          <button type="submit" className="boton boton--primario" disabled={formulario.formState.isSubmitting}>
            {textoDelBoton}
          </button>
          <button type="button" className="boton boton--enlace" onClick={() => void pedirCodigo()} disabled={enviando}>
            Enviar otro código
          </button>
        </div>
      </form>
    </IndicadorDeCarga>
  )
}
