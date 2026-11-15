import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { AvisoDeError, Campo } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { nuevoIdentificador } from '../../servicios/api'
import { pesos } from '../../servicios/conceptos'
import { credito, ESTADO_DE_LA_SOLICITUD, type SimulacionDeCredito } from '../../servicios/credito'
import { fechaYHora } from '../../servicios/pagos'
import { usePerfil } from '../../sesion/sesion'
import '../conceptos/Conceptos.css'

/**
 * T144 (US13): el asociado simula un crédito con las líneas que su cooperativa publica (la cuota es la del ERP) y envía la solicitud, que llega a
 * la cooperativa como borrador para que la revise y la radique. Ve el estado de las que envió.
 */
export function Credito() {
  const { data: perfil } = usePerfil()
  const tenant = perfil?.tenant ?? null
  const clienteDeConsultas = useQueryClient()
  const lineas = useQuery({ queryKey: ['credito-lineas', tenant], queryFn: credito.lineas, enabled: tenant !== null })
  const solicitudes = useQuery({ queryKey: ['credito-solicitudes', tenant], queryFn: credito.solicitudes, enabled: tenant !== null })

  const [linea, setLinea] = useState('')
  const [monto, setMonto] = useState('')
  const [cuotas, setCuotas] = useState('24')
  const [destino, setDestino] = useState('')
  const [ingresos, setIngresos] = useState('')
  const [simulacion, setSimulacion] = useState<SimulacionDeCredito | null>(null)
  const [clave, setClave] = useState(nuevoIdentificador)
  const [trabajando, setTrabajando] = useState<'simular' | 'enviar' | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [enviada, setEnviada] = useState(false)

  const lineaElegida = linea || lineas.data?.lines[0]?.publicId || ''
  const valor = Number(monto.replace(/\D/g, ''))
  const plazo = Number(cuotas)

  const simular = async () => {
    setTrabajando('simular')
    setError(null)
    setEnviada(false)
    try {
      setSimulacion(await credito.simular({ creditLinePublicId: lineaElegida, amount: valor, installments: plazo }))
      setClave(nuevoIdentificador())
    } catch (e) {
      setSimulacion(null)
      setError(e)
    } finally {
      setTrabajando(null)
    }
  }

  const enviar = async () => {
    setTrabajando('enviar')
    setError(null)
    try {
      const ingreso = Number(ingresos.replace(/\D/g, ''))
      await credito.solicitar({
        creditLinePublicId: lineaElegida, amount: valor, installments: plazo,
        ...(destino.trim() ? { purpose: destino.trim() } : {}),
        ...(ingreso > 0 ? { monthlyIncome: ingreso } : {}),
      }, clave)
      setEnviada(true)
      setSimulacion(null)
      await clienteDeConsultas.invalidateQueries({ queryKey: ['credito-solicitudes', tenant] })
    } catch (e) {
      setError(e)
    } finally {
      setTrabajando(null)
    }
  }

  const primeraCuota = simulacion?.installments[0]

  return (
    <section className="pagina">
      <p>
        <Link to="/cooperativa">← Mis productos</Link>
      </p>
      <h1>Solicitar un crédito</h1>
      <IndicadorDeCarga cargando={lineas.isFetching || trabajando !== null} texto={trabajando === 'enviar' ? 'Enviando tu solicitud…' : 'Calculando…'}>
        {lineas.isError ? (
          <AvisoDeError error={lineas.error} />
        ) : lineas.data && lineas.data.lines.length === 0 ? (
          <div className="tarjeta">
            <p>Tu cooperativa no tiene líneas de crédito para solicitar en el canal.</p>
          </div>
        ) : (
          <div className="tarjeta">
            <Campo etiqueta="Línea de crédito">
              {(props) => (
                <select {...props} value={lineaElegida} onChange={(e) => { setLinea(e.target.value); setSimulacion(null) }}>
                  {lineas.data?.lines.map((l) => (
                    <option key={l.publicId} value={l.publicId}>{l.name}</option>
                  ))}
                </select>
              )}
            </Campo>
            <Campo etiqueta="Monto (pesos)">
              {(props) => <input {...props} inputMode="numeric" value={monto} onChange={(e) => { setMonto(e.target.value); setSimulacion(null) }} />}
            </Campo>
            <Campo etiqueta="Número de cuotas">
              {(props) => <input {...props} inputMode="numeric" value={cuotas} onChange={(e) => { setCuotas(e.target.value); setSimulacion(null) }} />}
            </Campo>
            <p className="fila-de-botones">
              <button type="button" className="boton boton--secundario" disabled={!lineaElegida || valor <= 0 || plazo <= 0 || trabajando !== null}
                onClick={() => void simular()}>
                Simular
              </button>
            </p>
            {simulacion && primeraCuota && (
              <div aria-label="Simulación">
                <p>
                  Cuota: <strong>{pesos(primeraCuota.total)}</strong> · {simulacion.installmentCount} cuotas · tasa{' '}
                  {(simulacion.effectiveAnnualRate * 100).toLocaleString('es-CO', { maximumFractionDigits: 2 })} % E.A. · total a pagar{' '}
                  {pesos(simulacion.totals.toPay)}
                </p>
                {simulacion.warnings.map((w) => (
                  <p key={w.code} className="aviso aviso--advertencia">{w.message}</p>
                ))}
                <Campo etiqueta="¿Para qué es el crédito? (opcional)">
                  {(props) => <input {...props} maxLength={300} value={destino} onChange={(e) => setDestino(e.target.value)} />}
                </Campo>
                <Campo etiqueta="Ingresos mensuales (opcional)">
                  {(props) => <input {...props} inputMode="numeric" value={ingresos} onChange={(e) => setIngresos(e.target.value)} />}
                </Campo>
                <p className="pagina__texto-suave">
                  Esta es una simulación. Tu cooperativa estudia la solicitud y te contacta; puede pedirte más información o documentos.
                </p>
                <button type="button" className="boton boton--primario" disabled={trabajando !== null} onClick={() => void enviar()}>
                  Enviar solicitud
                </button>
              </div>
            )}
            {enviada && <p role="status" className="aviso aviso--info">Recibimos tu solicitud. Tu cooperativa la revisará y te contactará.</p>}
            <AvisoDeError error={error} />
          </div>
        )}
      </IndicadorDeCarga>

      <h2>Mis solicitudes</h2>
      <IndicadorDeCarga cargando={solicitudes.isFetching} texto="Consultando tus solicitudes…">
        {solicitudes.data && solicitudes.data.applications.length === 0 ? (
          <p className="pagina__texto-suave">No has enviado solicitudes por el canal.</p>
        ) : (
          <ul className="lista-simple">
            {solicitudes.data?.applications.map((s) => (
              <li key={s.publicId} className="tarjeta" aria-label={`Solicitud ${s.creditLineName}`}>
                <strong>{s.creditLineName}</strong> · {pesos(s.amount)} · {fechaYHora(s.createdAt)}
                <br />
                {ESTADO_DE_LA_SOLICITUD[s.status] ?? s.status}
              </li>
            ))}
          </ul>
        )}
      </IndicadorDeCarga>
    </section>
  )
}
