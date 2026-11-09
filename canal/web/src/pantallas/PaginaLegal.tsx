import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { IndicadorDeCarga } from '../componentes/IndicadorDeCarga'
import { AvisoDeError } from '../componentes/Formulario'
import { fechaLarga } from '../servicios/conceptos'
import { pagos } from '../servicios/pagos'
import { usePerfil } from '../sesion/sesion'

export type TipoDeTextoLegal = 'terminos' | 'politica' | 'preguntas'

const TEXTOS: Record<TipoDeTextoLegal, { titulo: string; descripcion: string; ruta: 'terms' | 'policy' | 'faq' }> = {
  terminos: {
    titulo: 'Términos y condiciones',
    ruta: 'terms',
    descripcion:
      'Los términos y condiciones de uso del canal los publica tu cooperativa. Los verás aquí, en su versión vigente, cuando ingreses y elijas tu cooperativa.',
  },
  politica: {
    titulo: 'Política de tratamiento de datos',
    ruta: 'policy',
    descripcion:
      'La política de tratamiento de datos personales la publica tu cooperativa. La verás aquí, en su versión vigente, cuando ingreses y elijas tu cooperativa.',
  },
  preguntas: {
    titulo: 'Preguntas frecuentes',
    ruta: 'faq',
    descripcion:
      'Las preguntas frecuentes sobre el canal y los pagos en línea las publica tu cooperativa. Las verás aquí cuando ingreses y elijas tu cooperativa.',
  },
}

const SUBTITULOS: Record<string, string> = {
  Faq: 'Sobre el canal y tu cooperativa',
  GatewayFaq: 'Sobre los pagos en línea',
}

/**
 * T099 (FR-057): términos, política y preguntas frecuentes —las de la cooperativa y las de la pasarela—, en la versión
 * vigente que publica la cooperativa elegida. Sin sesión o sin cooperativa elegida, dice dónde los verá. Se llega desde
 * el pie de página.
 */
export function PaginaLegal({ tipo }: { tipo: TipoDeTextoLegal }) {
  const { titulo, descripcion, ruta } = TEXTOS[tipo]
  const { data: perfil } = usePerfil()
  const tenant = perfil?.tenant ?? null
  const textos = useQuery({
    queryKey: ['legal', ruta, tenant],
    queryFn: () => pagos.textosLegales(ruta),
    enabled: tenant !== null,
  })
  const elegida = perfil?.tenants.find((t) => t.tenantPublicId === tenant)

  return (
    <section className="pagina">
      <h1>{titulo}</h1>
      {tenant === null ? (
        <p className="pagina__texto-suave">{descripcion}</p>
      ) : (
        <IndicadorDeCarga cargando={textos.isFetching} texto="Cargando…">
          {textos.isError ? (
            <AvisoDeError error={textos.error} />
          ) : textos.data && textos.data.texts.length === 0 ? (
            <p className="pagina__texto-suave">{elegida?.displayName ?? 'Tu cooperativa'} todavía no publica este texto en el canal.</p>
          ) : (
            textos.data?.texts.map((t) => (
              <article key={`${t.kind}:${t.version}`} className="campo">
                {tipo === 'preguntas' && <h2>{SUBTITULOS[t.kind] ?? t.kind}</h2>}
                <p className="pagina__texto-suave">
                  Versión {t.version}
                  {t.publishedAt ? `, publicada el ${fechaLarga(t.publishedAt.slice(0, 10))}` : ''}.
                </p>
                <div className="texto-legal" tabIndex={0} aria-label={tipo === 'preguntas' ? SUBTITULOS[t.kind] ?? titulo : titulo}>
                  {t.content}
                </div>
              </article>
            ))
          )}
        </IndicadorDeCarga>
      )}
      <p>
        <Link to={tenant ? '/cooperativa' : '/'}>Volver al inicio</Link>
      </p>
    </section>
  )
}
