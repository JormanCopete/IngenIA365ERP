import { render, screen } from '@testing-library/react'
import { LimiteDeErrores, MENSAJE_DE_ERROR } from './LimiteDeErrores'

function Rota(): never {
  throw new Error('fallo fuera de las rutas')
}

describe('LimiteDeErrores', () => {
  it('muestra el contenido cuando no hay error', () => {
    render(
      <LimiteDeErrores>
        <p>Todo bien</p>
      </LimiteDeErrores>,
    )

    expect(screen.getByText('Todo bien')).toBeInTheDocument()
  })

  it('ante un error muestra el mensaje en español, la salida al inicio y lo registra', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined)

    render(
      <LimiteDeErrores>
        <Rota />
      </LimiteDeErrores>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent(MENSAJE_DE_ERROR)
    expect(screen.getByRole('button', { name: 'Volver al inicio' })).toBeInTheDocument()
    expect(error).toHaveBeenCalledWith(
      '[canal] Error no controlado en la aplicación.',
      expect.objectContaining({ message: 'fallo fuera de las rutas' }),
      expect.any(String),
    )
    error.mockRestore()
  })
})
