import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

const raiz = document.getElementById('raiz')
if (!raiz) {
  throw new Error('No se encontró el elemento raíz de la aplicación.')
}

createRoot(raiz).render(
  <StrictMode>
    <main>Canal del asociado</main>
  </StrictMode>,
)
