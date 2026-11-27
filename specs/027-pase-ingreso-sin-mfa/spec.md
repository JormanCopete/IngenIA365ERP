# 027 — Pase de ingreso sin segundo factor

**Rama**: `027-pase-ingreso-sin-mfa` · **Creada**: 2026-11-26 · **Estado**: borrador con las decisiones del dueño del 2026-11-26
**Base**: la 003 (identidad central y segundo factor), la recuperación por correo (2026-08-28), la política de métodos por cooperativa y la 017
(identidad con documento: el administrador ya no fija claves).

**Entrada** (pedido del dueño, 2026-11-26): «que por medio de un administrador de la entidad se pudiera habilitar el acceso al usuario sin MFA o
por medio de un correo que le llegue al usuario y pueda ingresar desde el correo». Decisiones: sólo enlace al correo, reutilizable hasta su
vencimiento, por defecto 12 horas (la jornada) y máximo 12.

## Problema

Quien olvidó el celular, se le dañó o lo cambió y no tiene los códigos de respaldo **no puede entrar ese día**. Hoy tiene tres salidas, y
ninguna le abre la sesión:

- **Recuperación por correo**: espera de 24 horas (mínimo 1) y al final **borra** su segundo factor para que lo inscriba de nuevo. Apagada de
  fábrica en cada cooperativa.
- **Reset administrativo**: dos aprobaciones de administradores distintos del solicitante; también borra el segundo factor.
- **Reset del maestro**: inmediato, pero pasa por el maestro y también borra.

Las tres sirven para «perdí la app para siempre». Para «hoy no tengo el celular» la persona pierde la jornada, y al día siguiente, con su
celular de vuelta, tiene que inscribir todo otra vez.

## Principio

- **Lo autoriza una persona de la cooperativa, y lo recibe sólo el usuario.** El administrador decide y deja el motivo; el enlace llega al
  correo verificado del usuario. El administrador nunca lo ve (la misma regla de la 017: el administrador no fija claves).
- **El pase reemplaza al código de la app, nunca a la contraseña.** Tener el correo no basta; hay que tener también la contraseña.
- **No borra nada.** La app y las passkeys del usuario siguen inscritas; mañana entra con ellas como siempre.
- **Dura una jornada y se gasta solo.** Vence, se anula al primer ingreso normal o lo anula un administrador.
- **De fábrica nada cambia**: ninguna cooperativa lo ofrece hasta que lo enciende.

## Escenarios de usuario y pruebas *(obligatorio)*

### HU1 — El administrador concede un pase y el usuario entra con él (P1)

Un usuario llama: dejó el celular en casa. El administrador de la cooperativa abre su ficha en Seguridad → Usuarios, pulsa «Permitir ingreso sin
segundo factor», escribe el motivo y confirma. Al usuario le llega un correo con un enlace. Lo abre, escribe usuario y contraseña y entra sin que
se le pida el código de la app. Dentro ve el aviso «Entraste con un pase temporal».

**Por qué primero**: es el pedido completo; sin esto lo demás no tiene uso.

**Prueba independiente**: con la opción encendida en una cooperativa, un administrador concede un pase a un operador con TOTP inscrito; el
operador entra por el enlace sólo con su contraseña y su TOTP sigue inscrito.

**Escenarios de aceptación**:

1. **Dado** una cooperativa con el pase encendido y un usuario con segundo factor inscrito, **cuando** un administrador concede el pase con
   motivo, **entonces** el usuario recibe en su correo verificado un enlace con la hora de vencimiento, el nombre de quien lo autorizó y el motivo.
2. **Dado** un pase vigente, **cuando** el usuario abre el enlace y escribe su contraseña correcta, **entonces** entra a la cooperativa sin que
   se le pida el código de la app.
3. **Dado** un pase vigente, **cuando** el usuario abre el enlace y escribe una contraseña errada, **entonces** no entra, recibe el mismo mensaje
   que cualquier clave errada y el intento cuenta para el bloqueo de su cuenta.
4. **Dado** un usuario que entró con el pase, **cuando** al día siguiente entra normalmente, **entonces** se le pide su app como siempre, que
   sigue inscrita.
5. **Dado** un pase concedido, **cuando** el administrador abre de nuevo la ficha, **entonces** ve el pase con su estado, vencimiento, motivo,
   quién lo dio y cuántas veces se usó, pero no ve el enlace.

---

### HU2 — El pase sirve toda la jornada (P1)

El usuario que entró con el pase sale a almorzar y la sesión se le cierra por inactividad. Al volver abre el mismo correo, el mismo enlace, escribe
su contraseña y vuelve a entrar sin llamar al administrador.

**Por qué**: con un enlace de un solo uso, cada cierre de sesión obligaría a pedir otro; el dueño eligió el enlace reutilizable.

**Prueba independiente**: un pase de 12 horas se usa dos veces con una hora de diferencia; las dos entran. A las 12 horas y un minuto el mismo
enlace ya no entra.

**Escenarios de aceptación**:

1. **Dado** un pase vigente ya usado, **cuando** el usuario lo vuelve a abrir antes del vencimiento, **entonces** entra de nuevo con su
   contraseña.
2. **Dado** un pase vencido, **cuando** el usuario abre el enlace, **entonces** se le dice que el pase venció y que entre con su app o pida otro
   pase al administrador; no se dice nada más sobre la cuenta.
3. **Dado** una sesión abierta con el pase, **cuando** el usuario trabaja, **entonces** la sesión se comporta como cualquier otra: inactividad de
   la cooperativa, duración máxima de 12 horas, ventanas nuevas que heredan la sesión.
4. **Dado** una sesión abierta con el pase, **cuando** el pase vence, **entonces** la sesión abierta no se corta por eso; se corta por sus
   límites de siempre.

---

### HU3 — El pase se anula solo o lo anula un administrador (P1)

**Por qué**: un enlace que sirve varias veces no puede quedar vivo más de lo necesario.

**Prueba independiente**: un pase vigente deja de servir después de que el usuario entra con su app; otro deja de servir cuando el administrador
pulsa «Anular».

**Escenarios de aceptación**:

1. **Dado** un pase vigente, **cuando** el usuario entra normalmente con su app o su passkey, **entonces** el pase queda anulado con el motivo
   «Entró con su segundo factor».
2. **Dado** un pase vigente, **cuando** cualquier administrador de la cooperativa pulsa «Anular» y escribe el motivo, **entonces** el enlace deja
   de servir de inmediato.
3. **Dado** un pase vigente, **cuando** se concede otro al mismo usuario en la misma cooperativa, **entonces** el anterior queda anulado como
   «Reemplazado».
4. **Dado** un pase anulado, **cuando** el usuario abre el enlace, **entonces** se le responde como a uno vencido.
5. **Dado** un usuario que cambia su contraseña, es bloqueado, desactivado o retirado de la cooperativa, **entonces** sus pases vivos se anulan.

---

### HU4 — Límites de quién da y quién recibe (P1)

**Por qué**: un administrador que se da pases a sí mismo, o dos administradores que se los dan entre sí, dejan sin valor el segundo factor de
quienes más permisos tienen.

**Escenarios de aceptación**:

1. **Dado** un administrador, **cuando** intenta concederse un pase a sí mismo, **entonces** se rechaza.
2. **Dado** un usuario cuyo rol en la cooperativa es administrador, **cuando** otro administrador de la cooperativa intenta concederle un pase,
   **entonces** se rechaza y se le dice que ese pase lo da el maestro.
3. **Dado** el maestro, **cuando** concede un pase a un administrador de una cooperativa con la opción encendida, **entonces** se concede igual
   que en HU1, con motivo.
4. **Dado** la cuenta del maestro, **cuando** alguien intenta concederle un pase, **entonces** se rechaza siempre.
5. **Dado** un usuario con el correo sin verificar, **cuando** se le intenta conceder un pase, **entonces** se rechaza y se sugiere el reset
   administrativo.
6. **Dado** un usuario sin segundo factor inscrito, **cuando** se le intenta conceder un pase, **entonces** se rechaza: no lo necesita, al entrar
   el sistema le pide inscribir uno.
7. **Dado** un usuario sin permiso `Security.Users.GrantMfaBypass`, **cuando** intenta conceder o anular un pase, **entonces** se rechaza.

---

### HU5 — Avisos y rastro (P2)

**Por qué**: el control de un pase dado por una sola persona es que los demás se enteren el mismo día.

**Escenarios de aceptación**:

1. **Dado** un pase concedido, **entonces** los demás administradores activos de la cooperativa reciben un correo con quién lo dio, a quién, el
   motivo y el vencimiento.
2. **Dado** un ingreso con el pase, **entonces** el usuario recibe un correo «Entraste con un pase temporal desde [equipo, IP, hora]; si no fuiste
   tú, cambia tu contraseña y avisa a tu administrador».
3. **Dado** cualquier concesión, uso (exitoso o fallido) o anulación, **entonces** queda en la auditoría con quién, cuándo, la IP y el motivo.
4. **Dado** una sesión abierta con el pase, **entonces** el usuario ve dentro del ERP el aviso «Entraste con un pase temporal; si perdiste tu
   app, configúrala de nuevo» con el botón que lleva a inscribirla.

---

### HU6 — La cooperativa decide si lo ofrece (P2)

En la política de segundo factor de la cooperativa hay un interruptor «Permitir pases de ingreso sin segundo factor» y la duración máxima en horas.

**Escenarios de aceptación**:

1. **Dado** una cooperativa nueva o existente, **entonces** la opción está apagada y el botón no aparece en Usuarios.
2. **Dado** la opción encendida, **cuando** el administrador concede un pase, **entonces** la duración propuesta es la máxima de la cooperativa
   (12 horas de fábrica) y puede elegir menos, nunca más de 12.
3. **Dado** que la cooperativa apaga la opción, **entonces** sus pases vivos se anulan y nadie puede conceder nuevos.

### Casos límite

- **Usuario en varias cooperativas**: el pase vale sólo para la cooperativa que lo dio. La sesión abierta con el pase entra a esa cooperativa y no
  puede cambiar a otra que exija segundo factor; para entrar a ésa necesita su app o un pase de ésa.
- **Cooperativa que restringe métodos** (por ejemplo, sólo passkey): la sesión con pase no demuestra ningún método, así que la guardia de métodos
  le pide inscribir uno aceptado antes de seguir, igual que con un código de respaldo.
- **Correo que no sale**: la concesión se guarda y se le dice al administrador que el correo no se pudo enviar, para que lo anule o lo reintente;
  nunca se le muestra el enlace.
- **Reenviar**: «Reenviar» manda otra vez el correo de un pase vigente; por tener el enlace en texto plano sólo al enviarlo, reenviar emite un
  enlace nuevo y anula el anterior.
- **Abuso**: un administrador no puede conceder más de 5 pases al mismo usuario en 7 días ni más de 20 pases por día en total; superado, se
  rechaza con el motivo.
- **Pase y recuperación por correo a la vez**: conviven. Entrar con el pase no cancela una recuperación en curso; entrar con la app sí, como hoy.
- **MAUI**: el enlace abre la web; en la app MAUI el usuario puede pegar el enlace o el código del pase en la pantalla del segundo factor.
- **Hora**: el vencimiento se muestra en hora de Colombia.

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **FR-001**: El sistema DEBE permitir a un usuario con el permiso `Security.Users.GrantMfaBypass` conceder, con motivo obligatorio (10 a 500
  caracteres), un pase de ingreso sin segundo factor a otro usuario de su cooperativa.
- **FR-002**: El pase DEBE enviarse sólo al correo verificado del usuario; ninguna respuesta, pantalla ni registro DEBE mostrar el enlace ni el
  secreto del pase al administrador.
- **FR-003**: El secreto del pase DEBE guardarse sólo como huella; el enlace en texto plano existe únicamente en el correo enviado.
- **FR-004**: La duración DEBE ser elegida al conceder, entre 1 hora y el máximo de la cooperativa; el máximo de la cooperativa DEBE estar entre 1
  y 12 horas, y es 12 de fábrica.
- **FR-005**: El pase DEBE poder usarse varias veces mientras esté vigente; cada uso DEBE exigir la contraseña correcta del usuario.
- **FR-006**: El pase DEBE reemplazar únicamente la verificación del segundo factor; las demás reglas del ingreso (bloqueo por intentos,
  usuario activo, documento obligatorio, política de sesión) DEBEN aplicarse igual.
- **FR-007**: Usar el pase NO DEBE borrar ni alterar los autenticadores del usuario.
- **FR-008**: La sesión abierta con el pase DEBE quedar marcada como «sin método demostrado», de modo que la política de métodos de la cooperativa
  se aplique como con un código de respaldo.
- **FR-009**: La sesión abierta con el pase DEBE quedar limitada a la cooperativa que lo concedió.
- **FR-010**: El pase DEBE anularse al vencer, al entrar el usuario con su segundo factor, al ser anulado por un administrador, al concederse otro
  al mismo usuario en la misma cooperativa, al cambiar la contraseña, al bloquearse o desactivarse la cuenta, al retirarse el usuario de la
  cooperativa y al apagar la cooperativa la opción.
- **FR-011**: El sistema DEBE rechazar: el pase a sí mismo, el pase de un administrador a otro administrador (sólo el maestro puede darlo), todo
  pase a la cuenta del maestro, el pase a quien no tiene correo verificado y el pase a quien no tiene segundo factor inscrito.
- **FR-012**: El sistema DEBE avisar por correo a los demás administradores activos de la cooperativa cada vez que se concede un pase.
- **FR-013**: El sistema DEBE avisar al usuario por correo cada vez que entra con un pase.
- **FR-014**: El sistema DEBE registrar en la auditoría la concesión, cada uso (exitoso o fallido), el reenvío y la anulación, con actor, IP y
  motivo.
- **FR-015**: Cada cooperativa DEBE poder encender o apagar la opción y fijar la duración máxima en su política de segundo factor; DEBE venir
  apagada.
- **FR-016**: La ficha del usuario DEBE mostrar sus pases (vigentes y anteriores) con estado, vencimiento, motivo, quién lo dio, usos y quién o
  qué lo anuló, y permitir «Anular» y «Reenviar» sobre el vigente.
- **FR-017**: Dentro del ERP, la sesión abierta con un pase DEBE mostrar un aviso permanente con acceso a inscribir un segundo factor.
- **FR-018**: El enlace vencido, anulado o inexistente DEBE responder el mismo mensaje genérico, sin revelar si la cuenta existe.
- **FR-019**: El sistema DEBE aplicar los topes contra abuso: 5 pases por usuario en 7 días y 20 pases por cooperativa por día.
- **FR-020**: El maestro DEBE poder conceder y anular pases a cualquier usuario no maestro de una cooperativa con la opción encendida, con
  motivo, desde su consola.
- **FR-021**: La pantalla del segundo factor, en web y en MAUI, DEBE aceptar el pase traído por el enlace o pegado a mano.

### Entidades clave

- **Pase de ingreso**: usuario, cooperativa, quién lo concedió, motivo, huella del secreto, concedido en, vence en, usos y último uso (fecha e IP),
  estado (vigente, vencido, anulado) y motivo de anulación con quién o qué lo anuló.
- **Política de segundo factor de la cooperativa** (existente): gana «permite pases» (apagado) y «duración máxima en horas» (12).

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: Un usuario sin su celular entra en menos de 5 minutos desde que el administrador concede el pase.
- **SC-002**: Con un solo pase, el usuario trabaja la jornada completa aunque su sesión se cierre varias veces, sin pedir otro.
- **SC-003**: Al día siguiente el usuario entra con su app sin inscribir nada (0 reinscripciones por pases).
- **SC-004**: El 100 % de las concesiones, usos y anulaciones quedan en la auditoría y llegan como aviso a los demás administradores.
- **SC-005**: Ningún pase sirve después de su vencimiento, de su anulación ni sin la contraseña correcta (verificado por pruebas).
- **SC-006**: En una cooperativa que no encendió la opción, nada cambia para nadie.

## Supuestos

- «Administrador de la cooperativa» es quien tiene el rol de administrador de la cooperativa; el permiso nuevo se le siembra a ese rol y no al
  operador.
- La sesión abierta con el pase usa la inactividad y la duración máxima vigentes; no se crea un límite de sesión propio.
- Los correos salen por el envío directo que ya usa la recuperación por correo, porque al entrar todavía no hay cooperativa elegida.
- La recuperación por correo de 24 horas, el reset de dos aprobaciones y el reset del maestro no cambian.
- El cambio de esquema es una migración nueva y aditiva en la base administrativa (constitución, Principio XII), fechada después de la base
  consolidada.
- No se hace nada con SMS ni WhatsApp.
