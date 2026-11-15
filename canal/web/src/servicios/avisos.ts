import { api } from './api'
import { obtenerPlataforma } from '../plataforma'

/** T134 (US10, FR-056): las preferencias de aviso de la cuenta y el registro del dispositivo para notificaciones. */
export type ClaseDeAviso = 'due_reminder' | 'payment_applied' | 'payment_rejected' | 'pending_resolved' | 'security'

export interface PreferenciaDeAviso {
  kind: ClaseDeAviso
  email: boolean
  push: boolean
  locked: boolean
}

export const NOMBRE_DEL_AVISO: Record<ClaseDeAviso, string> = {
  due_reminder: 'Cuota próxima a vencer',
  payment_applied: 'Pago registrado por la cooperativa',
  payment_rejected: 'Pago no aprobado',
  pending_resolved: 'Pago pendiente resuelto',
  security: 'Seguridad de tu cuenta (no se puede apagar)',
}

export const avisos = {
  preferencias: () => api.get<{ preferences: PreferenciaDeAviso[]; webPush: boolean }>('/notifications/preferences'),
  guardar: (ps: Pick<PreferenciaDeAviso, 'kind' | 'email' | 'push'>[]) =>
    api.put<{ preferences: PreferenciaDeAviso[]; webPush: boolean }>('/notifications/preferences', { preferences: ps }),
  llaveWebPush: () => api.get<{ publicKey: string }>('/notifications/webpush-key'),
  registrar: (cuerpo: unknown) => api.post<{ subscriptionId: string }>('/devices/push-token', cuerpo),
}

/** La llave VAPID (base64url) como la pide `pushManager.subscribe`. */
export function llaveComoBytes(base64url: string): Uint8Array<ArrayBuffer> {
  const relleno = '='.repeat((4 - (base64url.length % 4)) % 4)
  const binario = atob((base64url + relleno).replace(/-/g, '+').replace(/_/g, '/'))
  const bytes = new Uint8Array(new ArrayBuffer(binario.length))
  for (let i = 0; i < binario.length; i++) bytes[i] = binario.charCodeAt(i)
  return bytes
}

/**
 * Activa las notificaciones en este dispositivo: en la app, el token de FCM del puente; en la PWA, una suscripción de Web Push
 * del service worker con la llave del canal. Devuelve `false` si el dispositivo no las admite o la persona no dio permiso.
 */
export async function activarNotificaciones(): Promise<boolean> {
  const token = await obtenerPlataforma().tokenPush()
  if (token) {
    await avisos.registrar({ kind: 'fcm', token })
    return true
  }
  if (typeof Notification === 'undefined' || !('serviceWorker' in navigator) || typeof PushManager === 'undefined') return false
  if ((await Notification.requestPermission()) !== 'granted') return false
  const { publicKey } = await avisos.llaveWebPush()
  const registro = await navigator.serviceWorker.ready
  const suscripcion = await registro.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: llaveComoBytes(publicKey) })
  const json = suscripcion.toJSON()
  await avisos.registrar({ kind: 'webpush', subscription: { endpoint: json.endpoint, keys: { p256dh: json.keys?.p256dh, auth: json.keys?.auth } } })
  return true
}
