import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { useConceptos, type Concepto } from '../../servicios/conceptos'
import { usePagosPendientes } from '../../servicios/pagos'
import { usePerfil } from '../../sesion/sesion'
import { NoDisponible } from '../conceptos/Conceptos'
import { Carrito, eleccionInicial, itemsDelPago, modalidadesDe, problemaDe, type Eleccion } from './Carrito'
import { Resumen } from './Resumen'
import '../conceptos/Conceptos.css'
import './Pago.css'

/**
 * US4: armar el pago (T095, T096) y confirmarlo (T097). Los valores salen siempre de la consulta en vivo al ERP; lo
 * elegido sólo vive en esta pantalla. `?concepto=` preselecciona el producto desde su detalle.
 */
export function Pagar() {
  const [parametros] = useSearchParams()
  const { data: perfil } = usePerfil()
  const tenant = perfil?.tenant ?? null
  const elegida = perfil?.tenants.find((t) => t.tenantPublicId === tenant)
  const consulta = useConceptos(tenant)
  const pendientes = usePagosPendientes(tenant)

  const [paso, setPaso] = useState<'armar' | 'resumen'>('armar')
  const [seleccion, setSeleccion] = useState<Record<string, Eleccion> | null>(null)
  const [destinoElegido, setDestino] = useState<string | null>(null)
  const [mostrarProblemas, setMostrarProblemas] = useState(false)

  const pagables: Concepto[] = (consulta.data?.items ?? []).filter((c) => c.payable && c.destinationPublicId && modalidadesDe(c).length > 0)
  const destinos = consulta.data?.destinations ?? []
  const preseleccionado = pagables.find((c) => c.conceptKey === parametros.get('concepto'))
  const destino = destinoElegido ?? preseleccionado?.destinationPublicId ?? pagables[0]?.destinationPublicId ?? null
  const listaPendientes = pendientes.data?.items ?? []

  // La primera vez, el producto que viene del detalle ya va marcado.
  const elegidos: Record<string, Eleccion> =
    seleccion ??
    (preseleccionado && !listaPendientes.some((p) => p.conceptKey === preseleccionado.conceptKey)
      ? { [preseleccionado.conceptKey]: eleccionInicial(preseleccionado) }
      : {})
  const delDestino = pagables.filter((c) => c.destinationPublicId === destino)
  const items = itemsDelPago(delDestino, elegidos)
  const problemas = delDestino.some((c) => elegidos[c.conceptKey] && problemaDe(c, elegidos[c.conceptKey] as Eleccion))

  const cambiar = (clave: string, e: Eleccion | null) => {
    const siguiente = Object.fromEntries(Object.entries(elegidos).filter(([k]) => k !== clave))
    if (e) siguiente[clave] = e
    setSeleccion(siguiente)
  }

  const continuar = () => {
    setMostrarProblemas(true)
    if (items.length > 0 && !problemas) setPaso('resumen')
  }

  return (
    <section className="pagina">
      <p>
        <Link to="/cooperativa">← Mis productos</Link>
      </p>
      <h1>{paso === 'armar' ? 'Pagar en línea' : 'Confirma tu pago'}</h1>
      <IndicadorDeCarga cargando={consulta.isFetching || pendientes.isFetching} texto="Consultando tus productos…">
        {consulta.isError ? (
          <NoDisponible error={consulta.error} reintentar={() => void consulta.refetch()} />
        ) : !consulta.data ? null : pagables.length === 0 ? (
          <div className="tarjeta">
            <p>No tienes productos para pagar en línea en {elegida?.displayName ?? 'tu cooperativa'}.</p>
          </div>
        ) : paso === 'armar' ? (
          <>
            <Carrito
              conceptos={pagables}
              destinos={destinos}
              destino={destino}
              alElegirDestino={(d) => {
                setDestino(d)
                setSeleccion({})
                setMostrarProblemas(false)
              }}
              pendientes={listaPendientes}
              soporte={pendientes.data?.support ?? elegida?.support}
              seleccion={elegidos}
              alCambiar={cambiar}
              mostrarProblemas={mostrarProblemas}
            />
            <div className="fila-de-botones">
              <button type="button" className="boton boton--primario" onClick={continuar} disabled={items.length === 0}>
                Continuar
              </button>
            </div>
          </>
        ) : (
          <Resumen
            items={items}
            conceptos={pagables}
            destino={destinos.find((d) => d.destinationPublicId === destino)}
            soporte={pendientes.data?.support ?? elegida?.support}
            alVolver={() => setPaso('armar')}
          />
        )}
      </IndicadorDeCarga>
    </section>
  )
}
