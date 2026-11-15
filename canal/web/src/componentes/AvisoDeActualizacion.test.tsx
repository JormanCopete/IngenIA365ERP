import { useState } from 'react'
import { fireEvent, render, screen } from '@testing-library/react'
import { AvisoDeActualizacion } from './AvisoDeActualizacion'

const estado = vi.hoisted(() => ({
  hayVersionNueva: false,
  actualizar: vi.fn<(recargar?: boolean) => Promise<void>>(),
}))

vi.mock('virtual:pwa-register/react', () => ({
  useRegisterSW: () => {
    // Igual que el hook real: un estado de React que el componente puede apagar.
    const needRefresh = useState(estado.hayVersionNueva)
    const offlineReady = useState(false)
    return { needRefresh, offlineReady, updateServiceWorker: estado.actualizar }
  },
}))

describe('AvisoDeActualizacion', () => {
  beforeEach(() => {
    estado.hayVersionNueva = false
    estado.actualizar.mockReset()
  })

  it('no muestra nada si no hay versión nueva', () => {
    const { container } = render(<AvisoDeActualizacion />)

    expect(container).toBeEmptyDOMElement()
  })

  it('avisa la versión nueva y actualiza cuando la persona lo pide', () => {
    estado.hayVersionNueva = true
    estado.actualizar.mockReturnValue(new Promise(() => undefined))
    render(<AvisoDeActualizacion />)

    expect(screen.getByRole('status')).toHaveTextContent('Hay una versión nueva del canal.')
    fireEvent.click(screen.getByRole('button', { name: 'Actualizar' }))

    expect(estado.actualizar).toHaveBeenCalledWith(true)
    expect(screen.getByRole('button', { name: 'Actualizando…' })).toBeDisabled()
  })

  it('«Más tarde» cierra el aviso sin actualizar', () => {
    estado.hayVersionNueva = true
    render(<AvisoDeActualizacion />)

    fireEvent.click(screen.getByRole('button', { name: 'Más tarde' }))

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(estado.actualizar).not.toHaveBeenCalled()
  })

  it('si no puede actualizar lo dice y lo registra', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    estado.hayVersionNueva = true
    estado.actualizar.mockRejectedValue(new Error('sin service worker en espera'))
    render(<AvisoDeActualizacion />)

    fireEvent.click(screen.getByRole('button', { name: 'Actualizar' }))

    expect(await screen.findByText(/No pudimos actualizar/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Actualizar' })).toBeEnabled()
    expect(error).toHaveBeenCalled()
    error.mockRestore()
  })
})
