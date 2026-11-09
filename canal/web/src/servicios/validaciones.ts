// Reglas de los formularios de identidad (zod), con los mensajes en español. El servicio vuelve a validar todo: aquí
// sólo se evita mandar lo que de seguro no pasa.
import { z } from 'zod'
import { TIPOS_DE_DOCUMENTO } from './identidad'

const codigosDeTipo = TIPOS_DE_DOCUMENTO.map((t) => t.codigo) as [string, ...string[]]

export const esquemaDocumento = z.object({
  documentType: z.enum(codigosDeTipo, { error: 'Elige el tipo de documento.' }),
  documentNumber: z
    .string()
    .trim()
    .min(1, 'Escribe el número de documento.')
    .regex(/^[0-9A-Za-z.\s-]{3,25}$/, 'Escribe sólo números (y letras si es pasaporte).'),
})

export type DatosDeDocumento = z.infer<typeof esquemaDocumento>

export const esquemaIngreso = esquemaDocumento.extend({
  password: z.string().min(1, 'Escribe tu clave.'),
})

export type DatosDeIngreso = z.infer<typeof esquemaIngreso>

export const esquemaCodigo = z.object({
  code: z
    .string()
    .trim()
    .regex(/^\d{6}$/, 'El código tiene 6 dígitos.'),
})

/** La política del canal (R13): 8 a 72 caracteres, con letras y números. Las comunes las rechaza el servicio. */
export const esquemaClaveNueva = z
  .object({
    password: z
      .string()
      .min(8, 'La clave debe tener al menos 8 caracteres.')
      .max(72, 'Usa como máximo 72 caracteres.')
      .regex(/\p{L}/u, 'La clave debe combinar letras y números.')
      .regex(/\p{N}/u, 'La clave debe combinar letras y números.'),
    confirmacion: z.string(),
  })
  .refine((d) => d.password === d.confirmacion, { path: ['confirmacion'], message: 'Las dos claves no coinciden.' })

export type DatosDeClaveNueva = z.infer<typeof esquemaClaveNueva>

export const esquemaCorreo = z.object({
  value: z.string().trim().pipe(z.email('Escribe un correo válido.')),
})

export const esquemaCelular = z.object({
  value: z
    .string()
    .transform((v) => v.replace(/\D/g, '').replace(/^57(?=3\d{9}$)/, ''))
    .pipe(z.string().regex(/^3\d{9}$/, 'Escribe un celular de 10 dígitos que empiece por 3.')),
})
