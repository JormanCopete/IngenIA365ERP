// Llamadas de identidad del canal (contracts/canal-api.md §A, «Identidad»). Las pantallas no arman rutas ni cuerpos:
// usan estas funciones. Ningún secreto queda en el navegador: en la PWA la sesión y la credencial de dispositivo
// viajan en cookies HttpOnly; en la app el token se queda en memoria y la credencial va al almacén seguro.
import { api, establecerTokenDeSesion } from './api'
import { obtenerPlataforma } from '../plataforma'

export type TipoDeCanal = 'email' | 'sms'

export interface CanalOfrecido {
  kind: TipoDeCanal
  masked: string
}

export interface Soporte {
  phone?: string | null
  email?: string | null
  hours?: string | null
}

export interface RespuestaDeInicio {
  challengeId: string
  channels: CanalOfrecido[]
  needsEntityUpdate?: boolean
  entityName?: string
  support?: Soporte
}

export interface RespuestaDeEnvio {
  sentTo: string
  expiresAt: string
}

export interface EstadoDeSesion {
  inactivityMinutes: number
  idleExpiresAt: string
  expiresAt: string
  tenantSelected: boolean
}

export interface RespuestaDeSesion {
  session: EstadoDeSesion
  token?: string
  deviceCredential?: string
}

export interface TextoParaAceptar {
  kind: string
  version: number
  content: string
}

export interface MarcaDelServicio {
  primary?: string | null
  secondary?: string | null
  logoUrl?: string | null
}

export interface CooperativaDelAsociado {
  tenantPublicId: string
  displayName: string
  brand: MarcaDelServicio
  support: Soporte
  inactivityMinutes: number
}

export interface Perfil {
  account: {
    firstNames: string
    lastNames: string
    documentType: string
    documentNumberMasked: string
    emailMasked: string | null
    mobileMasked: string | null
  }
  tenants: CooperativaDelAsociado[]
  tenant: string | null
  session: EstadoDeSesion
  passkeysAvailable: boolean
}

export interface Dispositivo {
  id: string
  kind: 'browser' | 'android' | 'ios'
  label: string
  createdAt: string
  lastSeenAt: string
  trusted: boolean
  passkey: boolean
  current: boolean
}

export interface SesionAbierta {
  id: string
  deviceId: string | null
  label: string
  createdAt: string
  lastActivityAt: string
  current: boolean
}

export interface Documento {
  documentType: string
  documentNumber: string
}

/** Tipos de documento con los que se entra (el catálogo del ERP que sirve para entrar). */
export const TIPOS_DE_DOCUMENTO = [
  { codigo: 'CC', nombre: 'Cédula de ciudadanía' },
  { codigo: 'CE', nombre: 'Cédula de extranjería' },
  { codigo: 'TI', nombre: 'Tarjeta de identidad' },
  { codigo: 'PA', nombre: 'Pasaporte' },
  { codigo: 'PPT', nombre: 'Permiso por protección temporal' },
  { codigo: 'PEP', nombre: 'Permiso especial de permanencia' },
] as const

/** Al abrir una sesión: en la app guarda el token en memoria y la credencial de dispositivo en el almacén seguro. */
export async function alAbrirSesion(respuesta: RespuestaDeSesion): Promise<void> {
  if (respuesta.token) establecerTokenDeSesion(respuesta.token)
  if (respuesta.deviceCredential) {
    try {
      await obtenerPlataforma().guardarCredencial(respuesta.deviceCredential)
    } catch (error) {
      // Sin la credencial guardada, el próximo ingreso pide código: incómodo, no grave.
      console.warn('[canal] No se pudo guardar la credencial del dispositivo.', error)
    }
  }
}

// ------------------------------------------------------------------------------------------------- activación --

export const activacion = {
  iniciar: (doc: Documento) => api.post<RespuestaDeInicio>('/activation/start', doc),
  enviar: (challengeId: string, channel: TipoDeCanal) =>
    api.post<RespuestaDeEnvio>('/activation/send', { challengeId, channel }),
  verificar: (challengeId: string, code: string) =>
    api.post<{ activationToken?: string; entityName?: string; legalTexts?: TextoParaAceptar[]; alreadyActive?: boolean }>(
      '/activation/verify',
      { challengeId, code },
    ),
  completar: (activationToken: string, password: string, acceptances: { kind: string; version: number }[]) =>
    api.post<RespuestaDeSesion>('/activation/complete', { activationToken, password, acceptances }),
}

// ---------------------------------------------------------------------------------------------------- ingreso --

/** El ingreso abre sesión (dispositivo de confianza) o pide un código (dispositivo nuevo). */
export type RespuestaDeIngreso = RespuestaDeSesion | RespuestaDeInicio

export function pideCodigo(r: RespuestaDeIngreso): r is RespuestaDeInicio {
  return 'challengeId' in r
}

export const ingreso = {
  entrar: (doc: Documento, password: string, deviceCredential?: string) =>
    api.post<RespuestaDeIngreso>('/auth/login', { ...doc, password, ...(deviceCredential ? { deviceCredential } : {}) }),
  enviar: (challengeId: string, channel: TipoDeCanal) =>
    api.post<RespuestaDeEnvio>('/auth/login/send', { challengeId, channel }),
  verificar: (challengeId: string, code: string, trustDevice: boolean) =>
    api.post<RespuestaDeSesion>('/auth/login/verify', { challengeId, code, trustDevice }),
  salir: async () => {
    try {
      await api.post<undefined>('/auth/logout')
    } finally {
      establecerTokenDeSesion(null)
    }
  },
}

export const recuperacion = {
  iniciar: (doc: Documento) => api.post<RespuestaDeInicio>('/auth/recovery/start', doc),
  enviar: (challengeId: string, channel: TipoDeCanal) =>
    api.post<RespuestaDeEnvio>('/auth/recovery/send', { challengeId, channel }),
  verificar: (challengeId: string, code: string) =>
    api.post<{ recoveryToken: string }>('/auth/recovery/verify', { challengeId, code }),
  completar: (recoveryToken: string, password: string) =>
    api.post<undefined>('/auth/recovery/complete', { recoveryToken, password }),
}

// ------------------------------------------------------------------------------------------- sesión y perfil --

export const sesion = {
  estado: () => api.get<EstadoDeSesion>('/session'),
  seguir: () => api.post<EstadoDeSesion>('/session/keepalive'),
  elegirCooperativa: (tenantPublicId: string) =>
    api.post<{ tenant: CooperativaDelAsociado; session: EstadoDeSesion }>('/session/tenant', { tenantPublicId }),
}

export const perfil = {
  leer: () => api.get<Perfil>('/me'),
  pedirCambioDeContacto: (field: 'email' | 'mobile', value: string) =>
    api.post<{ challengeId: string; sentTo: string; expiresAt: string }>('/me/contact', { field, value }),
  reenviarCodigoDeContacto: (challengeId: string, channel: TipoDeCanal) =>
    api.post<RespuestaDeEnvio>('/me/contact/send', { challengeId, channel }),
  confirmarCambioDeContacto: (challengeId: string, code: string) =>
    api.post<{ field: string; masked: string }>('/me/contact/verify', { challengeId, code }),
  cambiarClave: (currentPassword: string, newPassword: string) =>
    api.post<undefined>('/me/password', { currentPassword, newPassword }),
  dispositivos: () => api.get<{ devices: Dispositivo[]; sessions: SesionAbierta[] }>('/me/devices'),
  revocarDispositivo: (id: string) => api.delete<undefined>(`/me/devices/${encodeURIComponent(id)}`),
  cerrarSesion: (id: string) => api.delete<undefined>(`/me/sessions/${encodeURIComponent(id)}`),
}
