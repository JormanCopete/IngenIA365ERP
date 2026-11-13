import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { Link, useParams } from 'react-router'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { AvisoDeError } from '../../componentes/Formulario'
import { pesos } from '../../servicios/conceptos'
import { claveDeConceptos } from '../../servicios/conceptos'
import { avisoEnCurso, clavePendientes, fechaYHora, NOMBRE_DEL_ESTADO, pagos, type Intento } from '../../servicios/pagos'
import { usePerfil } from '../../sesion/sesion'
import { ContactoDeSoporte } from './Carrito'
import { BotonDeComprobante } from './Comprobante'
import '../conceptos/Conceptos.css'
import './Pago.css'

/** Cada cuánto se vuelve a preguntar mientras el pago sigue pendiente. */
export const ESPERA_DE_PENDIENTE_MS = 10_000

/**
 * T098, T107 (FR-038, FR-041, FR-046, FR-048): `/pagos/:id/retorno`. Lo que se muestra lo responde el servicio, que pregunta a la pasarela;
 * nada sale de la URL. Pendiente: lo dice con el contacto de la cooperativa y vuelve a preguntar solo.
 */
export function Retorno() {
  const { id = '' } = useParams()
  const { data: perfil } = usePerfil()
  const cliente = useQueryClient()
  const primera = useRef(true)
  const consulta = useQuery<Intento>({
    queryKey: ['pago', id],
    // La primera consulta avisa que el asociado acaba de volver (un vencido puede haberse aprobado después).
    queryFn: () => {
      const retorno = primera.current
      primera.current = false
      return pagos.consultar(id, retorno)
    },
    // Sigue preguntando mientras la pasarela no decide y, aprobado, mientras el ERP no ha registrado el pago (T107).
    refetchInterval: (q) =>
      q.state.data && (q.state.data.status === 'pending' || q.state.data.status === 'created' || avisoEnCurso(q.state.data))
        ? ESPERA_DE_PENDIENTE_MS
        : false,
  })
  const intento = consulta.data
  const terminado = intento && intento.status !== 'pending' && intento.status !== 'created'

  // Al terminar, los conceptos y los pendientes cambian: se vuelven a pedir al entrar a ellos.
  useEffect(() => {
    if (terminado) {
      void cliente.invalidateQueries({ queryKey: claveDeConceptos(perfil?.tenant ?? null) })
      void cliente.invalidateQueries({ queryKey: clavePendientes(perfil?.tenant ?? null) })
    }
  }, [terminado, cliente, perfil?.tenant])

  return (
    <section className="pagina">
      <h1>Resultado de tu pago</h1>
      <IndicadorDeCarga cargando={consulta.isFetching && !intento} texto="Consultando el resultado con la pasarela…">
        {consulta.isError && !intento ? (
          <AvisoDeError error={consulta.error} />
        ) : intento ? (
          <Resultado intento={intento} />
        ) : null}
      </IndicadorDeCarga>
      <p className="fila-de-botones">
        <Link to="/cooperativa">Volver a mis productos</Link>
      </p>
    </section>
  )
}

const TITULOS: Record<Intento['status'], { texto: string; clase: string }> = {
  approved: { texto: 'Pago aprobado', clase: 'aviso--exito' },
  pending: { texto: 'Pago pendiente', clase: 'aviso--advertencia' },
  created: { texto: 'Pago en proceso', clase: 'aviso--advertencia' },
  rejected: { texto: 'Pago rechazado', clase: 'aviso--error' },
  failed: { texto: 'El pago no se pudo completar', clase: 'aviso--error' },
  expired: { texto: 'La sesión de pago venció', clase: 'aviso--error' },
}

function Resultado({ intento: i }: { intento: Intento }) {
  const titulo = TITULOS[i.status]
  const fecha = i.approval?.approvedAt ?? i.updatedAt
  const filas: [string, string][] = [
    ['Referencia', i.reference],
    ['Estado', NOMBRE_DEL_ESTADO[i.status]],
    ['Fecha y hora', fechaYHora(fecha)],
    ['Valor', `${pesos(i.total)} (COP)`],
  ]
  if (i.approval) {
    filas.push(['Autorización / CUS', i.approval.authorization])
    if (i.approval.paymentMethodName) filas.push(['Medio de pago', i.approval.paymentMethodName])
    if (i.approval.issuerName) filas.push(['Banco o emisor', i.approval.issuerName])
  }
  return (
    <>
      <p className={`aviso ${titulo.clase}`} role="status">
        <strong>{titulo.texto}.</strong>{' '}
        {i.status === 'approved' && 'Tu cooperativa recibirá el pago y lo aplicará a tus productos.'}
        {(i.status === 'pending' || i.status === 'created') &&
          'La pasarela todavía no confirma el resultado. No vuelvas a pagar estos productos mientras tanto; te mostraremos el resultado aquí apenas llegue.'}
        {(i.status === 'rejected' || i.status === 'failed') && (i.message ?? 'No se hizo ningún cobro.')}
        {i.status === 'expired' && 'No se hizo ningún cobro. Puedes intentarlo de nuevo.'}
      </p>
      <dl className="tarjeta conceptos__datos">
        {filas.map(([etiqueta, valor]) => (
          <div key={etiqueta} className="conceptos__dato">
            <dt>{etiqueta}</dt>
            <dd>{valor}</dd>
          </div>
        ))}
      </dl>
      {i.status === 'approved' && <RegistroEnLaCooperativa intento={i} />}
      {(i.status === 'pending' || i.status === 'created' || i.erpOutcome === 'in_review') && <ContactoDeSoporte soporte={i.support} />}
      {(i.status === 'rejected' || i.status === 'failed' || i.status === 'expired') && (
        <p>
          <Link to="/cooperativa/pagar" className="boton boton--secundario">Intentar de nuevo</Link>
        </p>
      )}
    </>
  )
}

/** Aprobado: lo que pasó en la cooperativa —registrando, aplicado con su comprobante, o recibido y en revisión (FR-046)—. */
function RegistroEnLaCooperativa({ intento: i }: { intento: Intento }) {
  if (avisoEnCurso(i)) {
    return <p className="aviso aviso--info">Tu cooperativa está registrando el pago. En unos momentos tendrás el comprobante.</p>
  }
  return (
    <>
      {i.erpOutcome === 'in_review' ? (
        <p className="aviso aviso--advertencia">
          <strong>Pago recibido, en revisión.</strong> Tu cooperativa recibió el dinero; una parte no se pudo aplicar a tus productos y
          la está revisando. No tienes que volver a pagar.
        </p>
      ) : (
        <p className="aviso aviso--exito">Tu cooperativa aplicó el pago a tus productos.</p>
      )}
      <p>
        <BotonDeComprobante intento={i} />
      </p>
    </>
  )
}
