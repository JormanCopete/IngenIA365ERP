import { createContext, use, useCallback, useMemo, useState, type ReactNode } from 'react'
import { aplicarMarca, MARCA_VACIA, type MarcaAplicada, type MarcaDeCooperativa } from './aplicarMarca'

interface ValorDeMarca {
  marca: MarcaAplicada
  /** Aplica la marca de la cooperativa elegida; `null` vuelve a la del canal. */
  establecerMarca: (marca: MarcaDeCooperativa | null) => void
}

const ContextoDeMarca = createContext<ValorDeMarca | null>(null)

export function ProveedorDeMarca({ children }: { children: ReactNode }) {
  const [marca, setMarca] = useState<MarcaAplicada>(MARCA_VACIA)

  const establecerMarca = useCallback((nueva: MarcaDeCooperativa | null) => {
    setMarca(aplicarMarca(nueva))
  }, [])

  const valor = useMemo(() => ({ marca, establecerMarca }), [marca, establecerMarca])
  return <ContextoDeMarca value={valor}>{children}</ContextoDeMarca>
}

export function useMarca(): ValorDeMarca {
  const valor = use(ContextoDeMarca)
  if (!valor) {
    throw new Error('useMarca se usó fuera de <ProveedorDeMarca>.')
  }
  return valor
}
