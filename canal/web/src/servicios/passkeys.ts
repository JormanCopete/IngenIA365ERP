// Llaves de acceso (passkeys) de la PWA (T068). El servicio entrega las opciones en JSON con base64url; el navegador
// trabaja con ArrayBuffer. Aquí se hace la conversión en los dos sentidos, con los ayudantes del navegador cuando
// existen y a mano cuando no. En la app no se ofrecen: HybridWebView sirve desde un origen local.
import { api } from './api'
import type { RespuestaDeSesion } from './identidad'

/** ¿Este navegador puede usar llaves de acceso? */
export function passkeysDisponibles(): boolean {
  return !__MODO_APP__ && typeof window !== 'undefined' && typeof window.PublicKeyCredential === 'function' &&
    typeof navigator.credentials?.get === 'function'
}

export function base64urlABuffer(texto: string): ArrayBuffer {
  const base64 = texto.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(texto.length / 4) * 4, '=')
  const binario = atob(base64)
  const bytes = new Uint8Array(binario.length)
  for (let i = 0; i < binario.length; i++) bytes[i] = binario.charCodeAt(i)
  return bytes.buffer
}

export function bufferABase64url(buffer: ArrayBuffer | ArrayBufferView): string {
  const bytes = buffer instanceof ArrayBuffer ? new Uint8Array(buffer) : new Uint8Array(buffer.buffer, buffer.byteOffset, buffer.byteLength)
  let binario = ''
  for (const b of bytes) binario += String.fromCharCode(b)
  return btoa(binario).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

type OpcionesJson = Record<string, unknown>
type ConJson = typeof PublicKeyCredential & {
  parseRequestOptionsFromJSON?: (o: OpcionesJson) => PublicKeyCredentialRequestOptions
  parseCreationOptionsFromJSON?: (o: OpcionesJson) => PublicKeyCredentialCreationOptions
}

function convertirDescriptores(lista: unknown): PublicKeyCredentialDescriptor[] | undefined {
  if (!Array.isArray(lista)) return undefined
  return lista.map((d: { id: string; type: 'public-key'; transports?: AuthenticatorTransport[] }) => ({
    ...d,
    id: base64urlABuffer(d.id),
  }))
}

export function opcionesDeIngreso(o: OpcionesJson): PublicKeyCredentialRequestOptions {
  const pkc = PublicKeyCredential as ConJson
  if (pkc.parseRequestOptionsFromJSON) return pkc.parseRequestOptionsFromJSON(o)
  const permitidas = convertirDescriptores(o.allowCredentials)
  return {
    ...(o as unknown as PublicKeyCredentialRequestOptions),
    challenge: base64urlABuffer(o.challenge as string),
    ...(permitidas ? { allowCredentials: permitidas } : {}),
  }
}

export function opcionesDeRegistro(o: OpcionesJson): PublicKeyCredentialCreationOptions {
  const pkc = PublicKeyCredential as ConJson
  if (pkc.parseCreationOptionsFromJSON) return pkc.parseCreationOptionsFromJSON(o)
  const usuario = o.user as { id: string; name: string; displayName: string }
  const excluidas = convertirDescriptores(o.excludeCredentials)
  return {
    ...(o as unknown as PublicKeyCredentialCreationOptions),
    challenge: base64urlABuffer(o.challenge as string),
    user: { ...usuario, id: base64urlABuffer(usuario.id) },
    ...(excluidas ? { excludeCredentials: excluidas } : {}),
  }
}

/** La credencial en el JSON que espera el servicio (el `toJSON()` del navegador, o armado a mano). */
export function credencialAJson(c: PublicKeyCredential): unknown {
  const conJson = c as PublicKeyCredential & { toJSON?: () => unknown }
  if (typeof conJson.toJSON === 'function') return conJson.toJSON()
  const r = c.response as AuthenticatorAttestationResponse & AuthenticatorAssertionResponse
  const response: Record<string, string | null> = { clientDataJSON: bufferABase64url(r.clientDataJSON) }
  if ('attestationObject' in r && r.attestationObject) response.attestationObject = bufferABase64url(r.attestationObject)
  if ('authenticatorData' in r && r.authenticatorData) response.authenticatorData = bufferABase64url(r.authenticatorData)
  if ('signature' in r && r.signature) response.signature = bufferABase64url(r.signature)
  if ('userHandle' in r) response.userHandle = r.userHandle ? bufferABase64url(r.userHandle) : null
  return {
    id: c.id,
    rawId: bufferABase64url(c.rawId),
    type: c.type,
    response,
    clientExtensionResults: c.getClientExtensionResults(),
    authenticatorAttachment: c.authenticatorAttachment,
  }
}

/** Ingreso con llave de acceso: abre la sesión sin documento ni clave. */
export async function ingresarConPasskey(): Promise<RespuestaDeSesion> {
  const { ceremonyId, publicKey } = await api.post<{ ceremonyId: string; publicKey: OpcionesJson }>('/auth/passkey/options')
  const credencial = (await navigator.credentials.get({ publicKey: opcionesDeIngreso(publicKey) })) as PublicKeyCredential | null
  if (!credencial) throw new DOMException('No se eligió ninguna llave.', 'NotAllowedError')
  return api.post<RespuestaDeSesion>('/auth/passkey/verify', { ceremonyId, credential: credencialAJson(credencial) })
}

/** Registra una llave de acceso en este navegador (desde «Mis dispositivos»). */
export async function registrarPasskey(): Promise<{ id: string; label: string }> {
  const { ceremonyId, publicKey } = await api.post<{ ceremonyId: string; publicKey: OpcionesJson }>('/me/passkeys/options')
  const credencial = (await navigator.credentials.create({ publicKey: opcionesDeRegistro(publicKey) })) as PublicKeyCredential | null
  if (!credencial) throw new DOMException('No se creó ninguna llave.', 'NotAllowedError')
  return api.post<{ id: string; label: string }>('/me/passkeys', { ceremonyId, credential: credencialAJson(credencial) })
}

/** Mensaje para la persona cuando el navegador cancela o rechaza la ceremonia. */
export function mensajeDePasskey(error: unknown): string | null {
  if (error instanceof DOMException && (error.name === 'NotAllowedError' || error.name === 'AbortError')) {
    return 'Se canceló el uso de la llave de acceso.'
  }
  if (error instanceof DOMException && error.name === 'InvalidStateError') {
    return 'Este dispositivo ya tiene una llave de acceso para tu cuenta.'
  }
  return null
}
