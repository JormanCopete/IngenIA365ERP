import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import './marca/tokens.css'
import './marca/base.css'
import { App } from './App'
import { LimiteDeErrores } from './componentes/LimiteDeErrores'
import { instalarGanchosDelCanal } from './plataforma'
import { crearClienteDeConsultas } from './servicios/consultas'

const raiz = document.getElementById('raiz')
if (!raiz) {
  throw new Error('No se encontró el elemento raíz de la aplicación.')
}

// Antes de montar React: la app puede avisar un enlace (el retorno de la pasarela) apenas carga la página.
instalarGanchosDelCanal()

// Sólo en memoria: los saldos no se guardan en el dispositivo (FR-030).
const clienteDeConsultas = crearClienteDeConsultas()

createRoot(raiz).render(
  <StrictMode>
    <LimiteDeErrores>
      <QueryClientProvider client={clienteDeConsultas}>
        <App />
      </QueryClientProvider>
    </LimiteDeErrores>
  </StrictMode>,
)
