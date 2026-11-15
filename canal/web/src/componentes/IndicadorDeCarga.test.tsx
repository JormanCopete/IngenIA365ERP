import { render, screen } from '@testing-library/react'
import { IndicadorDeCarga } from './IndicadorDeCarga'

describe('IndicadorDeCarga', () => {
  it('muestra el contenido y no anuncia nada cuando no carga', () => {
    const { container } = render(
      <IndicadorDeCarga cargando={false}>
        <p>Tus créditos</p>
      </IndicadorDeCarga>,
    )

    expect(screen.getByText('Tus créditos')).toBeVisible()
    expect(container.firstElementChild).toHaveAttribute('aria-busy', 'false')
    expect(screen.getByRole('status')).toHaveTextContent('')
    expect(screen.getByText('Tus créditos').parentElement).not.toHaveAttribute('inert')
  })

  it('mientras carga deja el contenido montado pero inerte y anuncia «Cargando…»', () => {
    const { container } = render(
      <IndicadorDeCarga cargando>
        <label>
          Valor a pagar
          <input defaultValue="150000" />
        </label>
      </IndicadorDeCarga>,
    )

    expect(container.firstElementChild).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByRole('status')).toHaveTextContent('Cargando…')
    const campo = screen.getByDisplayValue('150000')
    expect(campo).toBeInTheDocument()
    expect(campo.closest('.zona-de-carga__contenido')).toHaveAttribute('inert')
  })

  it('usa el texto que se le pasa', () => {
    render(<IndicadorDeCarga cargando texto="Consultando tus conceptos…" />)

    expect(screen.getByRole('status')).toHaveTextContent('Consultando tus conceptos…')
  })

  it('no desmonta el contenido al terminar de cargar (conserva lo escrito)', () => {
    const { rerender } = render(
      <IndicadorDeCarga cargando>
        <input aria-label="Referencia" defaultValue="" />
      </IndicadorDeCarga>,
    )
    const campo = screen.getByLabelText('Referencia') as HTMLInputElement
    campo.value = 'ABC123'

    rerender(
      <IndicadorDeCarga cargando={false}>
        <input aria-label="Referencia" defaultValue="" />
      </IndicadorDeCarga>,
    )

    expect(screen.getByLabelText('Referencia')).toBe(campo)
    expect(campo.value).toBe('ABC123')
    expect(screen.getByRole('status')).toHaveTextContent('')
  })
})
