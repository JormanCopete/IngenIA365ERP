import { useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { AvisoDeError, PasosDeCodigo } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { obtenerPlataforma } from '../../plataforma'
import { ErrorDelCanal, nuevaClaveDeIdempotencia } from '../../servicios/api'
import { pesos, type Concepto } from '../../servicios/conceptos'
import type { CanalOfrecido, Soporte, TextoParaAceptar } from '../../servicios/identidad'
import {
  CODIGOS_DE_PAGO,
  NOMBRE_DE_LA_ELECCION,
  NOMBRE_DE_LA_MODALIDAD,
  pagos,
  rutaDeRetorno,
  type Destino,
  type ItemDelPago,
} from '../../servicios/pagos'
import { AvisoDePagoPendiente } from './Carrito'

const NOMBRE_DEL_TEXTO: Record<string, string> = { Terms: 'Términos y condiciones', DataPolicy: 'Política de tratamiento de datos' }

/** «PSE o tarjeta», «PSE», «tarjeta» o vacío. */
function medios(d: Destino | undefined): string {
  if (!d) return ''
  return [d.offerPse ? 'PSE' : null, d.offerCard ? 'tarjeta' : null].filter(Boolean).join(' o ')
}

/**
 * T097 (FR-033, FR-034, FR-036, FR-009): el detalle de lo que se va a pagar, el total en pesos, la pasarela, los textos que
 * haya que aceptar y el código si la cooperativa lo exige. El botón se deshabilita al primer clic y la misma clave de
 * idempotencia acompaña todos los reintentos de esta confirmación: confirmar dos veces es un solo intento.
 */
export function Resumen({
  items,
  conceptos,
  destino,
  soporte,
  alVolver,
}: {
  items: ItemDelPago[]
  conceptos: Concepto[]
  destino: Destino | undefined
  soporte: Soporte | undefined
  alVolver: () => void
}) {
  const navegar = useNavigate()
  const clave = useRef(nuevaClaveDeIdempotencia())
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [textos, setTextos] = useState<TextoParaAceptar[]>([])
  const [aceptados, setAceptados] = useState<Record<string, boolean>>({})
  const [codigo, setCodigo] = useState<{ challengeId: string; canales: CanalOfrecido[]; verificado: boolean } | null>(null)
  const total = items.reduce((s, i) => s + i.amount, 0)
  const descripcion = (k: string) => conceptos.find((c) => c.conceptKey === k)?.description ?? k
  const todosAceptados = textos.every((t) => aceptados[`${t.kind}:${t.version}`])

  const confirmar = async () => {
    if (enviando) return
    setEnviando(true)
    setError(null)
    try {
      const intento = await pagos.crear(items, clave.current, {
        ...(textos.length > 0 ? { acceptances: textos.map((t) => ({ kind: t.kind, version: t.version })) } : {}),
        ...(codigo?.verificado ? { otpChallengeId: codigo.challengeId } : {}),
      })
      if (!intento.processUrl) {
        // El intento ya existía con esta clave y su sesión terminó: el retorno muestra cómo quedó.
        void navegar(rutaDeRetorno(intento.attemptId))
        return
      }
      const retorno = new URL(rutaDeRetorno(intento.attemptId), window.location.origin).toString()
      const r = await obtenerPlataforma().abrirPasarela(intento.processUrl, retorno)
      // En la app la pasarela abre en una pestaña segura y vuelve aquí; en la PWA la página ya se fue.
      void navegar(rutaDeRetorno(intento.attemptId))
      if (r.resultado === 'cancelled') return
    } catch (e) {
      if (e instanceof ErrorDelCanal && e.code === CODIGOS_DE_PAGO.textos) {
        const vigentes = (e.data as { legalTexts?: TextoParaAceptar[] } | null)?.legalTexts ?? []
        setTextos(vigentes)
        setAceptados({})
      } else if (e instanceof ErrorDelCanal && e.code === CODIGOS_DE_PAGO.codigo) {
        try {
          const inicio = await pagos.iniciarCodigo()
          setCodigo({ challengeId: inicio.challengeId, canales: inicio.channels, verificado: false })
        } catch (e2) {
          setError(e2)
        }
      } else {
        setError(e)
      }
      setEnviando(false)
    }
  }

  const pendiente =
    error instanceof ErrorDelCanal && error.code === CODIGOS_DE_PAGO.pendiente
      ? (error.data as { reference: string; total: number; status: string; authorization?: string } | null)
      : null
  const esperandoCodigo = codigo !== null && !codigo.verificado

  return (
    <IndicadorDeCarga cargando={enviando} texto="Preparando el pago…">
      <div className="formulario">
        <table className="pago__tabla">
          <caption className="pagina__texto-suave">Resumen de tu pago{destino ? ` · ${destino.name}` : ''}</caption>
          <thead>
            <tr>
              <th scope="col">Producto</th>
              <th scope="col" className="pago__valor">Valor</th>
            </tr>
          </thead>
          <tbody>
            {items.map((i) => (
              <tr key={i.conceptKey}>
                <td>
                  {descripcion(i.conceptKey)}
                  <span className="pago__modalidad">
                    {NOMBRE_DE_LA_MODALIDAD[i.modality]}
                    {i.prepaymentChoice ? ` · ${NOMBRE_DE_LA_ELECCION[i.prepaymentChoice]}` : ''}
                  </span>
                </td>
                <td className="pago__valor">{pesos(i.amount)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <th scope="row">Total (pesos colombianos, COP)</th>
              <td className="pago__valor"><strong>{pesos(total)}</strong></td>
            </tr>
          </tfoot>
        </table>
        <p className="pago__pasarela">
          <span className="pago__marca-pasarela">{destino?.name ?? 'Pasarela de pagos'}</span>
          El pago lo procesa la pasarela segura{medios(destino) ? ` por ${medios(destino)}` : ''}. Tus datos de pago no pasan por el
          canal ni por tu cooperativa.
        </p>
        <p className="pagina__texto-suave">
          Al pagar aceptas los <Link to="/ayuda/terminos">términos y condiciones</Link> y la{' '}
          <Link to="/ayuda/politica">política de tratamiento de datos</Link> de tu cooperativa.
        </p>

        {textos.map((t) => {
          const k = `${t.kind}:${t.version}`
          const nombre = NOMBRE_DEL_TEXTO[t.kind] ?? t.kind
          return (
            <div key={k} className="campo">
              <span className="campo__etiqueta">{nombre} (versión {t.version})</span>
              <div className="texto-legal" tabIndex={0} aria-label={nombre}>{t.content}</div>
              <label className="campo--casilla">
                <input type="checkbox" checked={Boolean(aceptados[k])} onChange={(e) => setAceptados((a) => ({ ...a, [k]: e.target.checked }))} />
                Leí y acepto {nombre.toLowerCase()} (versión {t.version}).
              </label>
            </div>
          )
        })}

        {esperandoCodigo && (
          <>
            <p className="aviso aviso--info">Tu cooperativa pide un código para confirmar el pago.</p>
            <PasosDeCodigo
              canales={codigo.canales}
              enviar={(canal) => pagos.enviarCodigo(codigo.challengeId, canal)}
              verificar={async (c) => {
                await pagos.verificarCodigo(codigo.challengeId, c)
                setCodigo({ ...codigo, verificado: true })
              }}
              textoDelBoton="Verificar código"
            />
          </>
        )}

        {pendiente ? <AvisoDePagoPendiente pendiente={pendiente} soporte={soporte} /> : <AvisoDeError error={error} />}

        <div className="fila-de-botones">
          <button type="button" className="boton boton--primario" onClick={() => void confirmar()}
            disabled={enviando || esperandoCodigo || !todosAceptados || items.length === 0 || pendiente !== null}>
            Pagar {pesos(total)}
          </button>
          <button type="button" className="boton boton--enlace" onClick={alVolver} disabled={enviando}>
            Cambiar lo que voy a pagar
          </button>
        </div>
      </div>
    </IndicadorDeCarga>
  )
}
