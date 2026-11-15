import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { AvisoDeError } from '../../componentes/Formulario'
import { IndicadorDeCarga } from '../../componentes/IndicadorDeCarga'
import { sesion, type CooperativaDelAsociado } from '../../servicios/identidad'
import { esColorValido } from '../../marca/aplicarMarca'
import { CLAVE_PERFIL, rutaDeRegreso, usePerfil, useSalir } from '../../sesion/sesion'
import './Selector.css'

/**
 * T075 (FR-025): con qué cooperativa continuar. Con una sola entra directo; sin ninguna, lo dice y deja salir o ver el
 * perfil. Se puede volver aquí para cambiar de cooperativa sin volver a ingresar.
 */
export function Selector() {
  const { data } = usePerfil()
  const [parametros] = useSearchParams()
  const volver = rutaDeRegreso(parametros.get('volver'))
  const navegar = useNavigate()
  const clienteDeConsultas = useQueryClient()
  const salir = useSalir()
  const [eligiendo, setEligiendo] = useState<string | null>(null)
  const [error, setError] = useState<unknown>(null)
  const automatico = useRef(false)
  const cooperativas = data?.tenants ?? []

  const elegir = async (c: CooperativaDelAsociado) => {
    setEligiendo(c.tenantPublicId)
    setError(null)
    try {
      await sesion.elegirCooperativa(c.tenantPublicId)
      // Lo de una cooperativa no se mezcla con lo de otra: se descarta todo menos el perfil.
      clienteDeConsultas.removeQueries({ predicate: (q) => q.queryKey[0] !== CLAVE_PERFIL[0] })
      await clienteDeConsultas.invalidateQueries({ queryKey: CLAVE_PERFIL })
      void navegar(volver, { replace: true })
    } catch (e) {
      setError(e)
      setEligiendo(null)
    }
  }

  // Con una sola cooperativa, y sin otra elegida, se entra directo.
  const unica = cooperativas.length === 1 ? cooperativas[0] : undefined
  useEffect(() => {
    if (unica && !data?.tenant && !automatico.current) {
      automatico.current = true
      void elegir(unica)
    }
    // elegir cambia en cada render; basta con la cooperativa única.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [unica, data?.tenant])

  if (cooperativas.length === 0) {
    return (
      <section className="pagina">
        <h1>No tienes entidades activas</h1>
        <p className="pagina__texto-suave">
          Ninguna cooperativa te tiene habilitado en el canal en este momento. Si crees que es un error, comunícate con tu
          cooperativa.
        </p>
        <p className="fila-de-botones">
          <Link to="/perfil">Ver mi perfil</Link>
          <button type="button" className="boton boton--secundario" onClick={() => void salir('salida')}>Salir</button>
        </p>
      </section>
    )
  }

  return (
    <section className="pagina">
      <h1>¿Con qué cooperativa quieres continuar?</h1>
      <IndicadorDeCarga cargando={eligiendo !== null} texto="Entrando…">
        <ul className="lista-simple selector">
          {cooperativas.map((c) => (
            <li key={c.tenantPublicId}>
              <button
                type="button"
                className="selector__opcion"
                style={esColorValido(c.brand.primary) ? { borderInlineStartColor: c.brand.primary } : undefined}
                aria-current={data?.tenant === c.tenantPublicId ? 'true' : undefined}
                disabled={eligiendo !== null}
                onClick={() => void elegir(c)}
              >
                {c.brand.logoUrl?.startsWith('https://') && <img className="selector__logo" src={c.brand.logoUrl} alt="" />}
                <span className="selector__nombre">{c.displayName}</span>
                {data?.tenant === c.tenantPublicId && <span className="selector__actual">Actual</span>}
              </button>
            </li>
          ))}
        </ul>
      </IndicadorDeCarga>
      <AvisoDeError error={error} />
    </section>
  )
}
