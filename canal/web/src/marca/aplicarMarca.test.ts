import { aplicarMarca, esColorValido, MARCA_VACIA, restablecerMarca, textoSobre } from './aplicarMarca'

describe('aplicarMarca', () => {
  const raiz = document.documentElement

  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
  })

  afterEach(() => {
    restablecerMarca(raiz)
    vi.restoreAllMocks()
  })

  it('valida colores #RRGGBB', () => {
    expect(esColorValido('#1F3B57')).toBe(true)
    expect(esColorValido('#1f3b57')).toBe(true)
    expect(esColorValido('#fff')).toBe(false)
    expect(esColorValido('rojo')).toBe(false)
    expect(esColorValido('#1f3b57; background: url(x)')).toBe(false)
    expect(esColorValido(null)).toBe(false)
  })

  it('pone los colores de la cooperativa con su texto de contraste', () => {
    const marca = aplicarMarca(
      { primary: '#0B5D1E', secondary: '#F2C94C', displayName: ' Cooperativa de prueba ', logoUrl: 'https://cdn.example/logo.png' },
      raiz,
    )

    expect(raiz.style.getPropertyValue('--color-primario')).toBe('#0b5d1e')
    expect(raiz.style.getPropertyValue('--color-sobre-primario')).toBe('var(--color-texto-sobre-oscuro)')
    expect(raiz.style.getPropertyValue('--color-secundario')).toBe('#f2c94c')
    expect(raiz.style.getPropertyValue('--color-sobre-secundario')).toBe('var(--color-texto-sobre-claro)')
    expect(marca).toEqual({
      nombre: 'Cooperativa de prueba',
      logoUrl: 'https://cdn.example/logo.png',
      primario: '#0b5d1e',
      secundario: '#f2c94c',
    })
  })

  it('descarta un color inválido, lo avisa y deja el del canal', () => {
    aplicarMarca({ primary: 'red', secondary: '#123' }, raiz)

    expect(raiz.style.getPropertyValue('--color-primario')).toBe('')
    expect(raiz.style.getPropertyValue('--color-secundario')).toBe('')
    expect(console.warn).toHaveBeenCalledTimes(2)
  })

  it('no acepta un logo por http', () => {
    const marca = aplicarMarca({ logoUrl: 'http://cdn.example/logo.png' }, raiz)

    expect(marca.logoUrl).toBeNull()
    expect(console.warn).toHaveBeenCalled()
  })

  it('al cambiar de cooperativa no quedan restos de la anterior', () => {
    aplicarMarca({ primary: '#0b5d1e', secondary: '#f2c94c' }, raiz)
    aplicarMarca({ primary: '#222222' }, raiz)

    expect(raiz.style.getPropertyValue('--color-primario')).toBe('#222222')
    expect(raiz.style.getPropertyValue('--color-secundario')).toBe('')
  })

  it('sin marca vuelve a la del canal', () => {
    aplicarMarca({ primary: '#0b5d1e' }, raiz)

    expect(aplicarMarca(null, raiz)).toEqual(MARCA_VACIA)
    expect(raiz.style.getPropertyValue('--color-primario')).toBe('')
  })

  it('elige el texto que más contrasta', () => {
    expect(textoSobre('#ffffff')).toBe('var(--color-texto-sobre-claro)')
    expect(textoSobre('#000000')).toBe('var(--color-texto-sobre-oscuro)')
    expect(textoSobre('#1f3b57')).toBe('var(--color-texto-sobre-oscuro)')
  })
})
