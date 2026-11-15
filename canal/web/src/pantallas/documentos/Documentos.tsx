import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { AvisoDeError, Campo } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { obtenerPlataforma } from '../../plataforma'
import { documentos, nombreDelArchivo, type DocumentoPublicado } from '../../servicios/documentos'
import { base64DeBlob } from '../../servicios/pagos'
import { usePerfil } from '../../sesion/sesion'
import '../conceptos/Conceptos.css'

/** «aaaa-mm-dd» de una fecha local. */
function iso(d: Date): string {
  const dos = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${dos(d.getMonth() + 1)}-${dos(d.getDate())}`
}

/**
 * T126 (US9, FR-055): los extractos y certificados que la cooperativa publica para el asociado. Cada uno lo genera el ERP en el momento con
 * el mismo motor de la oficina; el canal no los guarda. En la PWA lo guarda el navegador; en la app, el sistema.
 */
export function Documentos() {
  const { data: perfil } = usePerfil()
  const tenant = perfil?.tenant ?? null
  const consulta = useQuery({ queryKey: ['documentos', tenant], queryFn: documentos.listar, enabled: tenant !== null })

  return (
    <section className="pagina">
      <p>
        <Link to="/cooperativa">← Mis productos</Link>
      </p>
      <h1>Extractos y certificados</h1>
      <IndicadorDeCarga cargando={consulta.isFetching} texto="Consultando tus documentos…">
        {consulta.isError ? (
          <AvisoDeError error={consulta.error} />
        ) : consulta.data && consulta.data.documents.length === 0 ? (
          <div className="tarjeta">
            <p>Tu cooperativa no tiene documentos publicados para ti en el canal.</p>
          </div>
        ) : (
          <ul className="lista-simple">
            {consulta.data?.documents.map((d) => (
              <li key={`${d.kind}-${d.productPublicId ?? 'persona'}`}>
                <TarjetaDeDocumento documento={d} />
              </li>
            ))}
          </ul>
        )}
      </IndicadorDeCarga>
    </section>
  )
}

function TarjetaDeDocumento({ documento: d }: { documento: DocumentoPublicado }) {
  const hoy = new Date()
  const haceTresMeses = new Date(hoy.getFullYear(), hoy.getMonth() - 3, hoy.getDate())
  const [desde, setDesde] = useState(iso(haceTresMeses))
  const [hasta, setHasta] = useState(iso(hoy))
  const [año, setAño] = useState(hoy.getFullYear() - 1)
  const [descargando, setDescargando] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const descargar = async () => {
    setDescargando(true)
    setError(null)
    try {
      const pdf = await documentos.emitir({
        kind: d.kind,
        ...(d.productPublicId ? { productPublicId: d.productPublicId } : {}),
        ...(d.parameter === 'period' ? { from: desde, to: hasta } : {}),
        ...(d.parameter === 'year' ? { year: año } : {}),
      })
      const sufijo = d.parameter === 'period' ? `${desde}_${hasta}` : d.parameter === 'year' ? String(año) : iso(hoy)
      await obtenerPlataforma().abrirDocumento(await base64DeBlob(pdf), nombreDelArchivo(d, sufijo), 'application/pdf')
    } catch (e) {
      setError(e)
    } finally {
      setDescargando(false)
    }
  }

  return (
    <article className="tarjeta" aria-label={d.description}>
      <h2>{d.description}</h2>
      {d.parameter === 'period' && (
        <div className="fila-de-campos">
          <Campo etiqueta="Desde">
            {(props) => <input {...props} type="date" value={desde} max={hasta} onChange={(e) => setDesde(e.target.value)} />}
          </Campo>
          <Campo etiqueta="Hasta">
            {(props) => <input {...props} type="date" value={hasta} max={iso(hoy)} onChange={(e) => setHasta(e.target.value)} />}
          </Campo>
        </div>
      )}
      {d.parameter === 'year' && (
        <Campo etiqueta="Año">
          {(props) => (
            <select {...props} value={año} onChange={(e) => setAño(Number(e.target.value))}>
              {[1, 2, 3, 4, 5].map((n) => (
                <option key={n} value={hoy.getFullYear() - n}>
                  {hoy.getFullYear() - n}
                </option>
              ))}
            </select>
          )}
        </Campo>
      )}
      <IndicadorDeCarga cargando={descargando} texto="Generando el documento…">
        <button type="button" className="boton boton--secundario" onClick={() => void descargar()} disabled={descargando}>
          Descargar PDF
        </button>
        <AvisoDeError error={error} />
      </IndicadorDeCarga>
    </article>
  )
}
