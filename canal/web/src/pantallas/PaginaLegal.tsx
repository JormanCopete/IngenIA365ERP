import { Link } from 'react-router'

export type TipoDeTextoLegal = 'terminos' | 'politica' | 'preguntas'

const TEXTOS: Record<TipoDeTextoLegal, { titulo: string; descripcion: string }> = {
  terminos: {
    titulo: 'Términos y condiciones',
    descripcion:
      'Los términos y condiciones de uso del canal los publica tu cooperativa. Los verás aquí, en su versión vigente, cuando ingreses y elijas tu cooperativa.',
  },
  politica: {
    titulo: 'Política de tratamiento de datos',
    descripcion:
      'La política de tratamiento de datos personales la publica tu cooperativa. La verás aquí, en su versión vigente, cuando ingreses y elijas tu cooperativa.',
  },
  preguntas: {
    titulo: 'Preguntas frecuentes',
    descripcion:
      'Las preguntas frecuentes sobre el canal y los pagos en línea las publica tu cooperativa. Las verás aquí cuando ingreses y elijas tu cooperativa.',
  },
}

/** Textos legales (FR-057). El contenido viene de la cooperativa (`GET /legal/{kind}`) en la entrega C1. */
export function PaginaLegal({ tipo }: { tipo: TipoDeTextoLegal }) {
  const { titulo, descripcion } = TEXTOS[tipo]
  return (
    <section className="pagina">
      <h1>{titulo}</h1>
      <p className="pagina__texto-suave">{descripcion}</p>
      <p>
        <Link to="/">Volver al inicio</Link>
      </p>
    </section>
  )
}
