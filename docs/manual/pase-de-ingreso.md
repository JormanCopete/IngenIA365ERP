# Pase de ingreso sin segundo factor

> Feature 027 (pedido del dueño del 2026-11-26). Especificación: `specs/027-pase-ingreso-sin-mfa/`.

Para quien **hoy** no tiene a mano su app de códigos ni sus códigos de respaldo: dejó el celular, se le descargó, lo está cambiando. Un
administrador de la cooperativa le concede un **pase de ingreso**; a la persona le llega un enlace al correo y con él, más su contraseña, entra
sin el código de la app **durante la jornada**. Su app sigue inscrita: al día siguiente entra con ella como siempre.

Si la persona perdió la app **para siempre**, el pase no es la salida: use la recuperación por correo, el reset con dos aprobaciones o el
reset del maestro, que retiran el segundo factor para inscribirlo de nuevo.

## 1. Encender la opción (una vez por cooperativa)

**Seguridad → Política de segundo factor → Pases de ingreso sin segundo factor**: marque «Permitir pases de ingreso sin segundo factor» y la
duración máxima (12 horas de fábrica, de 1 a 12). **Viene apagado.** Al apagarlo se anulan los pases vigentes.

## 2. Conceder un pase

**Seguridad → Usuarios → la ficha de la persona → Permitir ingreso sin segundo factor**: la duración (por defecto la máxima) y el motivo (10 a
500 caracteres). Hace falta el permiso `Security.Users.GrantMfaBypass` (el rol «Administrador de Cooperativa» lo trae).

- El enlace le llega **sólo a la persona**, a su correo verificado. Usted no lo ve.
- Los **demás administradores** reciben un aviso con quién lo dio, a quién, el motivo y el vencimiento.
- Si el correo no sale, la pantalla lo dice: reenvíe o anule.

No se puede conceder:

| Caso | Por qué |
|---|---|
| A usted mismo | Un pase autoconcedido deja sin valor su propio segundo factor |
| A otro administrador | Lo da sólo el **administrador maestro** (consola de identidades), para que dos administradores no se habiliten entre sí |
| Al administrador maestro | Nunca |
| A quien no tiene el correo verificado | El enlace no tendría a dónde ir; use el reset con dos aprobaciones |
| A quien no tiene segundo factor | No lo necesita: al entrar se le pide inscribirlo |
| Más de 5 a la misma persona en 7 días, o más de 20 en la cooperativa en un día | Topes contra abuso; si pasa tanto, la persona perdió la app |

## 3. Entrar con el pase

1. La persona abre el enlace del correo: llega al ingreso con el aviso «Vas a entrar con un pase de ingreso».
2. Escribe su usuario y su contraseña. **No se le pide el código**: entra a la cooperativa del pase.
3. Dentro ve el aviso «Entraste con un pase temporal», con el acceso para configurar su segundo factor si lo perdió.
4. Cada ingreso con el pase le llega como correo («Entraste con un pase temporal desde…»).

Si la sesión se cierra (inactividad, cerrar el navegador), **vuelve a abrir el mismo enlace** mientras esté vigente.

En la **app del celular** el enlace abre la web. Para usarlo en la app: en la pantalla del código, «Tengo un pase de ingreso de mi
administrador», y pegue el enlace o el pase.

La sesión del pase es como cualquier otra (inactividad de la cooperativa, 12 horas como máximo), con dos diferencias:

- **Sólo entra a la cooperativa que dio el pase.** No puede cambiar a otra; para eso necesita su app.
- **No demuestra ningún método.** Si la cooperativa acepta sólo un método concreto (por ejemplo, sólo passkey), se le pide inscribirlo, como
  con un código de respaldo.

## 4. Cuándo deja de servir

| Cuándo | Estado en la ficha |
|---|---|
| Vence | Vencido |
| La persona entra con su app o su passkey | Entró con su app |
| Un administrador lo anula (con motivo) | Anulado |
| Se concede o reenvía otro a la misma persona | Reemplazado |
| La persona cambia su contraseña o se le reinicia el segundo factor | Cambió su clave |
| La cuenta se desactiva o la persona sale de la cooperativa | Anulado |
| La cooperativa apaga la opción | Opción apagada |

Un pase vencido o anulado responde «El pase de ingreso no es válido o ya venció», sin decir nada más de la cuenta. Una sesión ya abierta con el
pase **no se corta** cuando vence: termina por sus límites de siempre.

**Reenviar** manda un enlace nuevo con el mismo vencimiento y deja el anterior sin servir: el enlace sólo existe en el correo, así que no hay
cómo mandar el mismo.

## 5. Rastro

Cada concesión, reenvío, anulación y uso (exitoso o fallido) queda en la auditoría de la cooperativa (`Identity.LoginPass.*`) con quién, cuándo
y desde qué dirección. La ficha de la persona muestra sus pases con estado, vencimiento, quién lo dio, motivo y usos.

## 6. El maestro

En **Administración → Identidades → la persona → Pase de ingreso sin segundo factor** elige la cooperativa, la duración y el motivo. Es la vía
para los administradores de una cooperativa. La cooperativa debe tener la opción encendida.
