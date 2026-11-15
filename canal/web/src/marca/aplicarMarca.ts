// Marca de la cooperativa elegida (FR-007, D14): sus colores reemplazan los tokens por defecto y su nombre y logo
// van en el encabezado. Un valor que no cumple el formato se descarta (y se avisa en la consola) para que una marca
// mal cargada en el ERP no deje la pantalla ilegible.

/** Lo que llega del servicio (`GET /me` → `tenants[].brand` más el `displayName`). */
export interface MarcaDeCooperativa {
  primary?: string | null
  secondary?: string | null
  logoUrl?: string | null
  displayName?: string | null
}

/** La marca ya validada, lista para el encabezado. */
export interface MarcaAplicada {
  nombre: string | null
  logoUrl: string | null
  primario: string | null
  secundario: string | null
}

export const MARCA_VACIA: MarcaAplicada = { nombre: null, logoUrl: null, primario: null, secundario: null }

const COLOR_HEX = /^#[0-9a-fA-F]{6}$/

const PROPIEDADES = [
  '--color-primario',
  '--color-sobre-primario',
  '--color-secundario',
  '--color-sobre-secundario',
] as const

export function esColorValido(color: string | null | undefined): color is string {
  return typeof color === 'string' && COLOR_HEX.test(color)
}

/** Luminancia relativa (WCAG 2.x) de un color `#RRGGBB`. */
function luminancia(color: string): number {
  const canal = (inicio: number) => {
    const valor = parseInt(color.slice(inicio, inicio + 2), 16) / 255
    return valor <= 0.04045 ? valor / 12.92 : ((valor + 0.055) / 1.055) ** 2.4
  }
  return 0.2126 * canal(1) + 0.7152 * canal(3) + 0.0722 * canal(5)
}

/**
 * Token del texto que va encima de un color de marca: el claro o el oscuro, el que más contraste dé
 * (frente a blanco y a un casi negro de luminancia ~0,007).
 */
export function textoSobre(color: string): string {
  const l = luminancia(color)
  const contrasteConBlanco = 1.05 / (l + 0.05)
  const contrasteConOscuro = (l + 0.05) / 0.057
  return contrasteConBlanco >= contrasteConOscuro ? 'var(--color-texto-sobre-oscuro)' : 'var(--color-texto-sobre-claro)'
}

function logoValido(url: string | null | undefined): string | null {
  if (typeof url !== 'string' || url.trim() === '') return null
  try {
    const destino = new URL(url, window.location.href)
    if (destino.protocol === 'https:' || destino.origin === window.location.origin) return destino.toString()
  } catch (error) {
    console.warn('[canal] El logo de la cooperativa no es una dirección válida; se muestra el nombre.', error)
    return null
  }
  console.warn('[canal] El logo de la cooperativa no se sirve por https; se muestra el nombre.')
  return null
}

function nombreValido(nombre: string | null | undefined): string | null {
  return typeof nombre === 'string' && nombre.trim() !== '' ? nombre.trim() : null
}

function colorDeMarca(etiqueta: string, color: string | null | undefined): string | null {
  if (color === null || color === undefined || color === '') return null
  if (esColorValido(color)) return color.toLowerCase()
  console.warn(`[canal] El color ${etiqueta} de la marca («${color}») no tiene el formato #RRGGBB; se usa el del canal.`)
  return null
}

/** Pone la marca en `:root`. Lo que no viene o no es válido vuelve al valor por defecto de tokens.css. */
export function aplicarMarca(
  marca: MarcaDeCooperativa | null | undefined,
  raiz: HTMLElement = document.documentElement,
): MarcaAplicada {
  restablecerMarca(raiz)
  if (!marca) return MARCA_VACIA

  const primario = colorDeMarca('primario', marca.primary)
  const secundario = colorDeMarca('secundario', marca.secondary)

  if (primario) {
    raiz.style.setProperty('--color-primario', primario)
    raiz.style.setProperty('--color-sobre-primario', textoSobre(primario))
    actualizarColorDelSistema(primario)
  }
  if (secundario) {
    raiz.style.setProperty('--color-secundario', secundario)
    raiz.style.setProperty('--color-sobre-secundario', textoSobre(secundario))
  }

  return { nombre: nombreValido(marca.displayName), logoUrl: logoValido(marca.logoUrl), primario, secundario }
}

/** Quita la marca (al salir o al cambiar de cooperativa). */
export function restablecerMarca(raiz: HTMLElement = document.documentElement): void {
  for (const propiedad of PROPIEDADES) raiz.style.removeProperty(propiedad)
  actualizarColorDelSistema(null)
}

let colorDelSistemaOriginal: string | null | undefined

/** Barra del navegador o del sistema (meta theme-color) con el color de la cooperativa. */
function actualizarColorDelSistema(color: string | null): void {
  const meta = document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')
  if (!meta) return
  if (colorDelSistemaOriginal === undefined) colorDelSistemaOriginal = meta.getAttribute('content')
  const valor = color ?? colorDelSistemaOriginal
  if (valor) meta.setAttribute('content', valor)
}
