import { useId, useState } from 'react'
import { AvisoDeError } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { fechaLarga, pesos } from '../../servicios/conceptos'
import { NOMBRE_DE_LA_ELECCION, pagos, type EleccionDeAbono, type Simulacion } from '../../servicios/pagos'

/**
 * T096 (FR-032a): en un abono a capital, el asociado elige si reduce el plazo o el valor de la cuota. La opción de la
 * cooperativa va marcada; «Ver cómo queda» pide la simulación al ERP y muestra el plan de cada alternativa. Si el crédito
 * sólo admite una, sólo se ofrece esa.
 */
export function Prepago({
  conceptKey,
  valor,
  preseleccion,
  eleccion,
  alElegir,
}: {
  conceptKey: string
  valor: number
  preseleccion: EleccionDeAbono
  eleccion: EleccionDeAbono
  alElegir: (e: EleccionDeAbono) => void
}) {
  const nombre = useId()
  const [simulacion, setSimulacion] = useState<Simulacion | null>(null)
  const [simulada, setSimulada] = useState<number | null>(null)
  const [cargando, setCargando] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const simular = async () => {
    setCargando(true)
    setError(null)
    try {
      const s = await pagos.simular({ conceptKey, modality: 'Prepayment', amount: valor, prepaymentChoice: eleccion })
      setSimulacion(s)
      setSimulada(valor)
    } catch (e) {
      setError(e)
    } finally {
      setCargando(false)
    }
  }

  // Las alternativas que el ERP admite; sin simulación, las dos (el ERP rechaza al pagar la que no aplique).
  const opciones: EleccionDeAbono[] =
    simulacion && simulada === valor && simulacion.alternatives.length > 0
      ? simulacion.alternatives.map((a) => a.choice)
      : ['ReduceTerm', 'ReduceInstallment']
  const planDe = (e: EleccionDeAbono) =>
    simulada === valor ? simulacion?.alternatives.find((a) => a.choice === e)?.newPlan ?? null : null

  return (
    <fieldset className="campo pago__prepago">
      <legend className="campo__etiqueta">¿Qué prefieres con el abono?</legend>
      {opciones.map((o) => {
        const plan = planDe(o)
        return (
          <label key={o} className="campo--casilla">
            <input type="radio" name={nombre} value={o} checked={eleccion === o} onChange={() => alElegir(o)} />
            <span>
              {NOMBRE_DE_LA_ELECCION[o]}
              {o === preseleccion && <span className="pago__sugerida"> · sugerida por tu cooperativa</span>}
              {plan && (
                <span className="pago__plan">
                  Cuota de {pesos(plan.installment)}, {plan.remainingInstallments} cuotas
                  {plan.nextDueDate ? `, la próxima el ${fechaLarga(plan.nextDueDate)}` : ''}.
                </span>
              )}
            </span>
          </label>
        )
      })}
      <IndicadorDeCarga cargando={cargando} texto="Calculando…">
        <div className="fila-de-botones">
          <button type="button" className="boton boton--secundario" disabled={cargando || valor <= 0} onClick={() => void simular()}>
            Ver cómo queda
          </button>
        </div>
        {simulacion && simulada === valor &&
          simulacion.notices.map((n) => (
            <p key={n} className="aviso aviso--info">{n}</p>
          ))}
        <AvisoDeError error={error} />
      </IndicadorDeCarga>
    </fieldset>
  )
}
