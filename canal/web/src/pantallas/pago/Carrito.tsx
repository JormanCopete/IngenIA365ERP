import { useId } from 'react'
import { Campo } from '../../componentes/Formulario'
import { pesos, type Concepto } from '../../servicios/conceptos'
import type { Soporte } from '../../servicios/identidad'
import {
  NOMBRE_DE_LA_MODALIDAD,
  NOMBRE_DEL_ESTADO,
  type Destino,
  type EleccionDeAbono,
  type ItemDelPago,
  type Modalidad,
  type PagoPendiente,
} from '../../servicios/pagos'
import { Prepago } from './Prepago'

/** Lo que el asociado eligió para un concepto. `valor` es el texto del campo (se valida al continuar). */
export interface Eleccion {
  modalidad: Modalidad
  valor: string
  abono: EleccionDeAbono
}

/** Las modalidades que el concepto admite hoy, en orden de preferencia. */
export function modalidadesDe(c: Concepto): Modalidad[] {
  const m: Modalidad[] = []
  if (c.modes.due && c.dueToday > 0) m.push('Due')
  if (c.modes.partial) m.push('Partial')
  if (c.modes.prepayment) m.push('Prepayment')
  if (c.modes.payoff && c.payoffAmount !== null && c.payoffAmount > 0) m.push('Payoff')
  return m
}

/** La elección inicial de un concepto. */
export function eleccionInicial(c: Concepto): Eleccion {
  return { modalidad: modalidadesDe(c)[0] ?? 'Due', valor: '', abono: c.modes.prepayment?.defaultChoice ?? 'ReduceTerm' }
}

/** El valor que se cobra: fijo en «exigible» y «cancelación», el escrito en las demás. NaN si no es un entero. */
export function valorDe(c: Concepto, e: Eleccion): number {
  if (e.modalidad === 'Due') return Math.round(c.dueToday)
  if (e.modalidad === 'Payoff') return Math.round(c.payoffAmount ?? 0)
  const limpio = e.valor.replace(/[.\s$]/g, '')
  return /^\d{1,12}$/.test(limpio) ? Number(limpio) : Number.NaN
}

/** El problema del valor elegido, o null. */
export function problemaDe(c: Concepto, e: Eleccion): string | null {
  const v = valorDe(c, e)
  if (!Number.isFinite(v) || v <= 0) return 'Escribe un valor en pesos, sin decimales.'
  if (e.modalidad === 'Partial' && c.modes.partial && v < Math.ceil(c.modes.partial.minimum)) {
    return `El mínimo para este producto es ${pesos(Math.ceil(c.modes.partial.minimum))}.`
  }
  return null
}

/** Arma los ítems del pedido. */
export function itemsDelPago(conceptos: Concepto[], seleccion: Record<string, Eleccion>): ItemDelPago[] {
  return conceptos
    .filter((c) => seleccion[c.conceptKey])
    .map((c) => {
      const e = seleccion[c.conceptKey] as Eleccion
      return {
        conceptKey: c.conceptKey,
        modality: e.modalidad,
        amount: valorDe(c, e),
        prepaymentChoice: e.modalidad === 'Prepayment' ? e.abono : null,
      }
    })
}

/**
 * T095 (FR-032, FR-041): elegir los conceptos de un mismo destino, la modalidad y el valor de cada uno. Si hay varios
 * destinos, se dice que se pagan por separado y se elige uno. Un concepto con un pago en proceso no se puede elegir.
 */
export function Carrito({
  conceptos,
  destinos,
  destino,
  alElegirDestino,
  pendientes,
  soporte,
  seleccion,
  alCambiar,
  mostrarProblemas,
}: {
  conceptos: Concepto[]
  destinos: Destino[]
  destino: string | null
  alElegirDestino: (d: string) => void
  pendientes: PagoPendiente[]
  soporte: Soporte | undefined
  seleccion: Record<string, Eleccion>
  alCambiar: (clave: string, e: Eleccion | null) => void
  mostrarProblemas: boolean
}) {
  const nombreDelGrupo = useId()
  const usados = destinos.filter((d) => conceptos.some((c) => c.destinationPublicId === d.destinationPublicId))
  const delDestino = conceptos.filter((c) => c.destinationPublicId === destino)

  return (
    <>
      {usados.length > 1 && (
        <fieldset className="campo">
          <legend className="campo__etiqueta">Tus productos se pagan en grupos separados. ¿Cuál quieres pagar ahora?</legend>
          {usados.map((d) => (
            <label key={d.destinationPublicId} className="campo--casilla">
              <input type="radio" name={nombreDelGrupo} checked={destino === d.destinationPublicId}
                onChange={() => alElegirDestino(d.destinationPublicId)} />
              {d.name}: {conceptos.filter((c) => c.destinationPublicId === d.destinationPublicId).map((c) => c.description).join(', ')}
            </label>
          ))}
        </fieldset>
      )}
      <ul className="lista-simple">
        {delDestino.map((c) => (
          <li key={c.conceptKey}>
            <ConceptoDelCarrito
              concepto={c}
              pendiente={pendientes.find((p) => p.conceptKey === c.conceptKey)}
              soporte={soporte}
              eleccion={seleccion[c.conceptKey]}
              alCambiar={(e) => alCambiar(c.conceptKey, e)}
              mostrarProblemas={mostrarProblemas}
            />
          </li>
        ))}
      </ul>
    </>
  )
}

function ConceptoDelCarrito({
  concepto: c,
  pendiente,
  soporte,
  eleccion,
  alCambiar,
  mostrarProblemas,
}: {
  concepto: Concepto
  pendiente: PagoPendiente | undefined
  soporte: Soporte | undefined
  eleccion: Eleccion | undefined
  alCambiar: (e: Eleccion | null) => void
  mostrarProblemas: boolean
}) {
  const idModalidad = useId()
  const modalidades = modalidadesDe(c)
  if (pendiente) {
    return (
      <div className="tarjeta pago__concepto">
        <span className="conceptos__titulo">{c.description}</span>
        <AvisoDePagoPendiente pendiente={pendiente} soporte={soporte} />
      </div>
    )
  }
  const problema = eleccion && mostrarProblemas ? problemaDe(c, eleccion) : null
  return (
    <div className="tarjeta pago__concepto">
      <label className="campo--casilla">
        <input type="checkbox" checked={Boolean(eleccion)} onChange={(e) => alCambiar(e.target.checked ? eleccionInicial(c) : null)}
          disabled={modalidades.length === 0} />
        <span className="conceptos__titulo">{c.description}</span>
      </label>
      {eleccion && (
        <div className="pago__eleccion">
          <fieldset className="campo">
            <legend className="campo__etiqueta">¿Cuánto quieres pagar?</legend>
            {modalidades.map((m) => (
              <label key={m} className="campo--casilla">
                <input type="radio" name={idModalidad} checked={eleccion.modalidad === m}
                  onChange={() => alCambiar({ ...eleccion, modalidad: m })} />
                {NOMBRE_DE_LA_MODALIDAD[m]}
                {m === 'Due' && `: ${pesos(Math.round(c.dueToday))}`}
                {m === 'Payoff' && c.payoffAmount !== null && `: ${pesos(Math.round(c.payoffAmount))}`}
              </label>
            ))}
          </fieldset>
          {(eleccion.modalidad === 'Partial' || eleccion.modalidad === 'Prepayment') && (
            <Campo etiqueta="Valor en pesos" error={problema ? { type: 'validate', message: problema } : undefined}
              ayuda={eleccion.modalidad === 'Partial' && c.modes.partial ? `Mínimo ${pesos(Math.ceil(c.modes.partial.minimum))}.` : undefined}>
              {(p) => (
                <input className="campo__entrada" inputMode="numeric" value={eleccion.valor}
                  onChange={(e) => alCambiar({ ...eleccion, valor: e.target.value })} {...p} />
              )}
            </Campo>
          )}
          {eleccion.modalidad === 'Prepayment' && c.modes.prepayment && (
            <Prepago conceptKey={c.conceptKey} valor={Number.isFinite(valorDe(c, eleccion)) ? valorDe(c, eleccion) : 0}
              preseleccion={c.modes.prepayment.defaultChoice} eleccion={eleccion.abono}
              alElegir={(abono) => alCambiar({ ...eleccion, abono })} />
          )}
          {problema && eleccion.modalidad !== 'Partial' && eleccion.modalidad !== 'Prepayment' && (
            <p className="aviso aviso--error" role="alert">{problema}</p>
          )}
        </div>
      )}
    </div>
  )
}

/** FR-041: el aviso de pago en proceso, con referencia, valor, autorización y el contacto de la cooperativa. */
export function AvisoDePagoPendiente({
  pendiente,
  soporte,
}: {
  pendiente: { reference: string; total: number; status: string; approval?: { authorization: string } | null; authorization?: string }
  soporte: Soporte | undefined
}) {
  const autorizacion = pendiente.approval?.authorization ?? pendiente.authorization
  const estado = NOMBRE_DEL_ESTADO[pendiente.status as keyof typeof NOMBRE_DEL_ESTADO] ?? pendiente.status
  return (
    <div className="aviso aviso--advertencia" role="status">
      <p>
        Tienes un pago en proceso para este producto: referencia <strong>{pendiente.reference}</strong> por {pesos(pendiente.total)}
        {` (${estado.toLowerCase()}`}
        {autorizacion ? `, autorización ${autorizacion}` : ''}). Espera su resultado antes de pagar de nuevo.
      </p>
      <ContactoDeSoporte soporte={soporte} />
    </div>
  )
}

export function ContactoDeSoporte({ soporte }: { soporte: Soporte | undefined }) {
  if (!soporte?.phone && !soporte?.email) return null
  return (
    <p>
      Si tienes dudas, comunícate con tu cooperativa
      {soporte.phone ? ` al ${soporte.phone}` : ''}
      {soporte.phone && soporte.email ? ' o' : ''}
      {soporte.email ? ` a ${soporte.email}` : ''}
      {soporte.hours ? ` (${soporte.hours})` : ''}.
    </p>
  )
}
