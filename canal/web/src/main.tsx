import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import './marca/tokens.css'
import './marca/base.css'
import { App } from './App'
import { LimiteDeErrores } from './componentes/LimiteDeErrores'
import { detectarPlataforma, instalarGanchosDelCanal, obtenerPlataforma } from './plataforma'
import { establecerPlataformaDeLaApi } from './servicios/api'
import { crearClienteDeConsultas } from './servicios/consultas'

const raiz = document.getElementById('raiz')
if (!raiz) {
  throw new Error('No se encontró el elemento raíz de la aplicación.')
}

// Antes de montar React: la app puede avisar un enlace (el retorno de la pasarela) apenas carga la página.
instalarGanchosDelCanal()

// Sólo en memoria: los saldos no se guardan en el dispositivo (FR-030).
const clienteDeConsultas = crearClienteDeConsultas()

/**
 * En la app, la API debe saber que es la app (entrega el token de sesión en el cuerpo: la cookie no cruza desde los
 * archivos locales). Se pregunta al puente antes de montar; si no responde, se monta igual.
 */
async function prepararPlataforma(): Promise<void> {
  if (detectarPlataforma() !== 'app') return
  try {
    const info = await obtenerPlataforma().plataforma()
    establecerPlataformaDeLaApi(/ios|iphone|ipad/i.test(info.os) ? 'ios' : 'android')
  } catch (error) {
    console.error('[canal] La app no informó su sistema; se asume Android.', error)
    establecerPlataformaDeLaApi('android')
  }
}

await prepararPlataforma()

createRoot(raiz).render(
  <StrictMode>
    <LimiteDeErrores>
      <QueryClientProvider client={clienteDeConsultas}>
        <App />
      </QueryClientProvider>
    </LimiteDeErrores>
  </StrictMode>,
)
