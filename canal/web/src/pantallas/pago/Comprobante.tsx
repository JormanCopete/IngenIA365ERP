import { useState } from 'react'
import { AvisoDeError } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { obtenerPlataforma } from '../../plataforma'
import { base64DeBlob, pagos, type Intento } from '../../servicios/pagos'

/**
 * FR-048: descarga el comprobante del ERP de un pago aplicado. En la PWA lo guarda el navegador; en la app lo guarda o comparte el
 * sistema (`AbrirDocumento`). Sin recibo del ERP no muestra nada.
 */
export function BotonDeComprobante({ intento: i, etiqueta = 'Descargar comprobante' }: { intento: Pick<Intento, 'attemptId' | 'reference' | 'erpReceiptPublicId'>; etiqueta?: string }) {
  const [descargando, setDescargando] = useState(false)
  const [error, setError] = useState<unknown>(null)
  if (!i.erpReceiptPublicId) return null
  const descargar = async () => {
    setDescargando(true)
    setError(null)
    try {
      const pdf = await pagos.comprobante(i.attemptId)
      await obtenerPlataforma().abrirDocumento(await base64DeBlob(pdf), `comprobante-${i.reference}.pdf`, 'application/pdf')
    } catch (e) {
      setError(e)
    } finally {
      setDescargando(false)
    }
  }
  return (
    <IndicadorDeCarga cargando={descargando} texto="Descargando el comprobante…">
      <button type="button" className="boton boton--secundario" onClick={() => void descargar()} disabled={descargando}>
        {etiqueta}
      </button>
      <AvisoDeError error={error} />
    </IndicadorDeCarga>
  )
}
