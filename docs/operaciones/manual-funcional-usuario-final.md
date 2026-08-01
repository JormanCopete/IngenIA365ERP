---
title: "Manual de Usuario — Identidad Central de IngenIA365ERP"
author: "IngenIA365 — Equipo de Producto"
date: "Junio 2026"
subject: "Guía funcional para cooperativistas, administradores de cooperativa y administradores del SaaS"
keywords: [identidad, cooperativa, login, MFA, invitación, contraseña]
lang: es-CO
---

# Manual de Usuario — Identidad Central

> **¿Para quién es esta guía?**
> Para cualquier persona que use IngenIA365ERP: cooperativistas que reciben
> una invitación, administradores de cooperativa que gestionan a su equipo,
> y el administrador general del producto que da de alta nuevas
> cooperativas.

> **Lo que NO encontrarás aquí**: jerga técnica, código, comandos. Si buscas
> eso, ve a `manual-pruebas-identidad-central.md` o a la spec.

---

## 1. ¿Qué es la "Identidad Central"?

Antes, cada cooperativa que usaba el sistema tenía sus propios usuarios. Si
trabajabas para dos cooperativas, tenías que recordar dos contraseñas, dos
correos, y elegir en cuál querías entrar al iniciar sesión.

Ahora, **tu identidad vive una sola vez** en IngenIA365ERP:

- **Un solo correo + una sola contraseña**, sin importar cuántas cooperativas
  uses.
- **Sin combo box molesto** en la pantalla de login — el sistema sabe a qué
  cooperativas perteneces.
- **Si tienes acceso a varias**, elige cuál quieres usar después de entrar,
  y puedes cambiar entre ellas sin volver a loguearte.
- **Doble factor (MFA)** opcional para ti, o exigido por tu cooperativa si
  así lo decide su administrador.

---

## 2. Personas en el sistema

Tres tipos de usuarios. La diferencia importa para saber qué puedes hacer.

### 👤 Cooperativista (miembro regular)

Trabajas en una o más cooperativas. Iniciás sesión, ves tu información,
operás dentro del módulo que corresponde a tu cargo (cartera, contabilidad,
inventario, etc.). No administrás usuarios.

### 👔 Administrador de cooperativa

Eres responsable de tu cooperativa: invitas nuevos miembros, suspendes a
quienes ya no trabajan ahí, asignas o quitas el rol de administrador a tus
colegas, y decides si tu cooperativa exige doble factor para todos.

**No** podés crear cooperativas nuevas — eso lo hace el administrador del
SaaS. **No** podés ver datos de otra cooperativa que no sea la tuya.

### 🛡️ Administrador del SaaS (Master admin)

Eres el responsable técnico del producto IngenIA365ERP. Tu trabajo es:

- Dar de alta nuevas cooperativas (clientes).
- Asignar el primer administrador de cada cooperativa.
- Ayudar en recuperación operativa (por ejemplo, resetear el doble factor
  cuando alguien pierde su celular y ya no puede entrar).

---

## 3. Tu primer día en el sistema

### 3.1 Recibís una invitación por correo

El administrador de tu cooperativa (o el master admin) ingresó tu correo en
el sistema. Vas a recibir un mensaje como este:

> **De:** no-reply@ingenia365.com
> **Asunto:** Invitación a Coop. Solidaria — IngenIA365ERP
>
> Hola Ana,
>
> **Carlos Ramírez** te invitó a unirte a **Coop. Solidaria** en IngenIA365ERP.
> Para aceptar la invitación y configurar tu acceso, haz clic en el siguiente
> enlace:
>
> [Aceptar invitación]
>
> Importante: esta invitación expira el **07/06/2026 14:30 UTC**. Después de
> esa fecha tendrás que pedir una nueva.

✅ **Qué hacer**: hacé clic en "Aceptar invitación". El enlace solo se puede
usar una vez.

⚠️ **Si no esperabas este correo**: ignoralo. Nadie creará una cuenta a tu
nombre solo porque recibiste el mensaje.

### 3.2 La pantalla "Aceptar invitación"

El enlace te lleva a una página con el nombre de la cooperativa que te
invitó. Lo que ves depende de si ya tenías cuenta en IngenIA365ERP:

#### Caso A — Es tu primera vez en el sistema

Verás un formulario para crear tu contraseña:

```
┌─────────────────────────────────────────┐
│  Únete a Coop. Solidaria                │
│                                         │
│  Te invitaron como ana.perez@coop.com  │
│                                         │
│  Para activar tu cuenta, crea una       │
│  contraseña.                            │
│                                         │
│  Contraseña nueva:                      │
│  [_________________________________]    │
│  → Aceptable — considera 16+ caracteres │
│                                         │
│  Repite la contraseña:                  │
│  [_________________________________]    │
│                                         │
│  [ Crear cuenta y entrar ]              │
└─────────────────────────────────────────┘
```

📋 **Reglas para tu contraseña**:

- Mínimo 12 caracteres.
- El sistema verifica que **no aparezca en filtraciones públicas** de
  contraseñas robadas. Si te dice "esta contraseña aparece en filtraciones",
  cambiala — no es seguro usarla.
- Recomendado: una frase corta y memorable, NO una palabra del diccionario.
  Ejemplo: `MiCooperativaEnPasto2026` o `AzulNoeLargo!Pizza`.

#### Caso B — Ya tenías cuenta en IngenIA365ERP (otra cooperativa)

El sistema reconoce tu correo. Verás:

```
┌─────────────────────────────────────────┐
│  Únete a Coop. Solidaria                │
│                                         │
│  Esta cuenta ya está registrada.        │
│  Confirma con tu contraseña para unirte.│
│                                         │
│  Contraseña:                            │
│  [_________________________________]    │
│                                         │
│  [ Confirmar y entrar ]                 │
└─────────────────────────────────────────┘
```

Solo necesitás escribir tu contraseña **existente** (la misma que ya usabas
en la otra cooperativa). El sistema te suma la nueva membresía sin pedirte
crear una contraseña nueva.

#### Caso C — Estás logueada en otra cooperativa y la invitación es para vos

Si abrís el enlace mientras ya tenías sesión activa, vas a ver un solo botón
"Confirmar incorporación a Coop. Solidaria" — sin pedir contraseña. El
sistema reusa tu sesión actual.

### 3.3 Después de aceptar

Si la cooperativa que te invitó:

- **No exige doble factor**, entrás directo a su panel principal.
- **Sí exige doble factor**, te llevará a la pantalla para configurarlo
  antes de continuar (ver §5).

---

## 4. Iniciar sesión cada día

Vas a `https://app.ingenia365.com/security/login` (o el dominio que tu
cooperativa te indique). Verás:

```
┌─────────────────────────────────────────┐
│  Iniciar sesión                         │
│                                         │
│  Correo:                                │
│  [_________________________________]    │
│                                         │
│  Contraseña:                            │
│  [_________________________________]    │
│                                         │
│  [ Continuar ]                          │
│                                         │
│  ¿Olvidaste tu contraseña?              │
└─────────────────────────────────────────┘
```

✅ **Ingresa tu correo + contraseña**. ¡No hay combo de cooperativa! El
sistema decide qué pantalla mostrarte después según tu situación.

### 4.1 Lo que puede pasar después de presionar "Continuar"

| Situación | Pantalla siguiente | Qué hacer |
|-----------|-------------------|-----------|
| Tenés una sola cooperativa y sin MFA | **Panel principal** de esa cooperativa | Trabajar normalmente |
| Tenés MFA configurado | "Verifica tu código" | Abrir tu app de autenticación y escribir los 6 dígitos |
| Tenés varias cooperativas, ninguna por defecto | "Selecciona tu empresa" | Clic en la que quieras usar |
| Tenés varias y una marcada como default | **Panel principal** de la default | Trabajar (podés cambiar después) |
| Tu cooperativa exige MFA y vos no tenés | "Tu empresa requiere doble factor" | Configurar MFA antes de continuar |
| Ya no perteneces a ninguna cooperativa | "No tienes acceso a ninguna empresa" | Pedir una invitación |
| Contraseña incorrecta | Mensaje "Correo o contraseña incorrectos" | Reintentar (después de 5 fallos te bloquea 60s) |

### 4.2 ¿Por qué a veces el sistema me bloquea?

Para protegerte de intentos de robo de cuenta, el sistema cuenta tus intentos
fallidos:

- **5 fallos seguidos** → te bloquea por 1 minuto.
- **10 fallos** → 5 minutos.
- **15 fallos** → 15 minutos.
- **20+ fallos** → 1 hora. Si esto pasa, contactá a tu administrador.

Cuando entrás bien, el contador se resetea.

> 💡 **Tip**: si te bloquearon por error, esperá el tiempo indicado y volvé a
> intentar. No reinicies la página varias veces — eso no acelera el desbloqueo.

---

## 5. Configurar el doble factor (MFA)

### 5.1 ¿Qué es y por qué importa?

El doble factor (también llamado MFA, 2FA, autenticación en dos pasos) suma
una capa de seguridad: además de tu contraseña, necesitás un **código de 6
dígitos** que genera una app en tu celular.

**Por qué activarlo**: si alguien roba tu contraseña, no puede entrar sin tu
celular.

**Cuándo es obligatorio**:

- Si tu cooperativa lo activó como política, el sistema te lo va a pedir en
  tu próximo login.
- Si no, es **opcional** — pero recomendado.

### 5.2 Apps que podés usar

Cualquier app TOTP estándar:

- **Google Authenticator** (Android / iOS) — más común.
- **Microsoft Authenticator** (Android / iOS).
- **Authy** (multiplataforma, sincroniza entre dispositivos).
- **1Password** o **Bitwarden** (si ya los usás como gestor de contraseñas,
  tienen MFA integrado).

### 5.3 Pasos para activarlo (voluntario, desde tu perfil)

1. Una vez logueado, vas a **Perfil → Doble factor**, o directamente a
   `https://app.ingenia365.com/profile/mfa`.

2. Hacé clic en **"Comenzar"**. El sistema genera un código secreto único
   para vos:

   ```
   ┌─────────────────────────────────────────┐
   │  Configura tu app                       │
   │                                         │
   │  1. Abre tu app de autenticación.       │
   │  2. Agrega una nueva cuenta y escanea   │
   │     este código:                        │
   │                                         │
   │      JBSWY3DPEHPK3PXP                   │
   │                                         │
   │  3. Ingresa el código de 6 dígitos      │
   │     que muestre la app.                 │
   │                                         │
   │  Código de la app:                      │
   │  [ 1 2 3 4 5 6 ]                        │
   │                                         │
   │  [ Activar ]                            │
   └─────────────────────────────────────────┘
   ```

3. Abrí tu app de autenticación en el celular.
4. Agregá una cuenta nueva y pegale el código secreto (o si tu app soporta
   QR, escaneá el QR que muestra la pantalla).
5. La app te muestra un código de 6 dígitos que cambia cada 30 segundos.
6. Escribí el código actual y presioná **"Activar"**.

### 5.4 ¡Importante! Códigos de respaldo

Después de activar, vas a ver una lista de **10 códigos de respaldo** como:

```
AB12-CD34
9F88-7E22
4A56-CC09
...
```

⚠️ **GUARDÁ ESTOS CÓDIGOS** en un lugar seguro:

- Imprimirlos y guardarlos en una caja fuerte.
- Guardarlos en tu gestor de contraseñas.
- Una nota cifrada.

Estos códigos te permiten entrar si **perdés acceso al celular** (lo
rompiste, lo cambiaste, te lo robaron). **Solo se muestran una vez** —
después de cerrar esa pantalla no podés volver a verlos.

### 5.5 Configuración forzada por la cooperativa

Si tu cooperativa activó la política, al intentar entrar verás:

```
┌─────────────────────────────────────────┐
│  ℹ️  Tu empresa requiere verificación   │
│      en dos pasos.                      │
│                                         │
│  Configura tu app de autenticación para │
│  continuar.                             │
│                                         │
│  [ Comenzar ]                           │
└─────────────────────────────────────────┘
```

No podés saltarte este paso — la app no te dejará entrar al panel principal
hasta que termines la configuración.

### 5.6 Desactivar MFA

Si MFA es opcional para vos:

1. **Perfil → Doble factor → Desactivar**.
2. El sistema te pide confirmar con tu contraseña actual.
3. Listo.

⚠️ Si **alguna** de las cooperativas a las que perteneces exige MFA, el
sistema te mostrará:

> **No puedes desactivar MFA**: la empresa 'Coop. Solidaria' lo exige.

---

## 6. Olvidé mi contraseña

### 6.1 Pedir el enlace de recuperación

1. En la pantalla de login, hacé clic en **"¿Olvidaste tu contraseña?"**.
2. Escribí tu correo y presioná **"Enviar enlace"**.
3. Vas a ver siempre el mismo mensaje:

   > **Revisa tu correo**
   >
   > Si ana.perez@coop.com tiene una cuenta, te enviaremos un enlace para
   > restablecer tu contraseña.

   ☝️ Este mensaje aparece **incluso si te equivocaste de correo**. Es a
   propósito: el sistema no le dice a un atacante si un correo está
   registrado o no.

4. Si tu correo está bien, vas a recibir un mensaje en tu bandeja con un
   enlace que **expira en 1 hora**.

### 6.2 Restablecer la contraseña

1. Hacé clic en el enlace del correo.
2. Escribí una contraseña nueva (mínimo 12 chars, no Pwned).
3. Repetila.
4. Presioná **"Restablecer contraseña"**.
5. El sistema te lleva a la pantalla de login con un banner verde:
   *"Tu contraseña fue restablecida. Inicia sesión con tu nueva contraseña."*

⚠️ **Efectos del reset**:

- Tus sesiones activas en **otros dispositivos** se cierran. Si tenías la
  app abierta en otra computadora, te va a sacar la próxima vez que recargue.
- El enlace **solo se puede usar una vez**. Si llegás dos veces al mismo
  enlace, la segunda vez verás *"Este enlace ya fue usado"*.

---

## 7. Cambiar tu contraseña

Diferente del reset: este flujo es para cuando **sí recordás** la
contraseña actual pero querés cambiarla (por ejemplo cada 90 días, o tras
una sospecha).

1. **Perfil → Cambiar contraseña** (`/profile/password`).
2. Escribí tu contraseña actual + la nueva + repetí la nueva.
3. Presioná **"Cambiar contraseña"**.
4. Vas a recibir un correo de confirmación con los detalles del cambio
   (fecha, IP, dispositivo). Si vos no hiciste el cambio, ese correo te
   alerta — pedí un reset inmediato.

---

## 8. Trabajar con varias cooperativas

### 8.1 La pantalla "Selecciona tu empresa"

Si pertenecés a más de una cooperativa y no tenés una marcada como default,
después del login (y MFA si aplica) verás:

```
┌─────────────────────────────────────────┐
│  Selecciona tu empresa                  │
│                                         │
│  Estás vinculado a varias cooperativas. │
│  Elige con cuál vas a trabajar ahora.   │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │ Coop. Solidaria                 │   │
│  └─────────────────────────────────┘   │
│  ┌─────────────────────────────────┐   │
│  │ Coop. del Pacífico       [Admin]│   │
│  └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

Hacé clic en la que quieras y entrás directamente.

### 8.2 Cambiar de cooperativa sin volver a loguearte

En el encabezado de cualquier pantalla, hay un selector con el nombre de la
cooperativa actual. Hacé clic ahí, elegí otra de tu lista y el sistema te
mueve.

⚠️ **Aviso si tenés cambios sin guardar**: si estabas editando algo, te
preguntará si querés perderlo o guardarlo primero.

### 8.3 Fijar tu cooperativa por defecto

Si trabajás 90% del tiempo en una cooperativa, podés saltarte la pantalla
de selección:

1. **Perfil → Empresa por defecto**.
2. En la lista, hacé clic en **"Establecer"** junto a la cooperativa que
   más usás.
3. La próxima vez que entres, irás directo a esa.

Podés cambiar el default cuando quieras, o quitarlo (botón **"Quitar
default"**) para volver a ver la pantalla de selección.

### 8.4 ¿Qué pasa si la cooperativa default ya no me deja entrar?

Si tu administrador suspendió o revocó tu membresía con la cooperativa que
tenías como default, el sistema:

- **Limpia silenciosamente** el default (queda en blanco).
- Te muestra la pantalla de selección con las cooperativas que **sí** te
  permiten entrar.
- Si ya no perteneces a ninguna, ves la pantalla "No tienes acceso a
  ninguna empresa".

---

## 9. Soy administrador de cooperativa

### 9.1 Invitar a un nuevo miembro

1. **Administración → Miembros** (`/admin/tenant/{id}/members`).
2. Botón **"Invitar miembro"**.
3. Ingresá su correo. Presioná **"Enviar invitación"**.
4. El sistema envía el correo automáticamente. La persona aparece en tu
   lista con estado **"Invitado"** (no "Activo" todavía).
5. Cuando acepte el enlace, su estado cambia a **"Activo"**.

⚠️ **Solo el master admin puede invitar a alguien como administrador de tu
cooperativa**. Vos podés invitar miembros regulares; si querés promover a
uno a administrador, hacelo después de que acepten (ver §9.4).

### 9.2 Gestionar tus miembros

La tabla muestra cada miembro con su estado. Las acciones disponibles
dependen del estado:

| Estado | Acciones disponibles |
|--------|---------------------|
| Invitado | Cancelar invitación (Revocar) |
| Activo | Suspender, Revocar, Promover a admin, Degradar |
| Suspendido | Reactivar, Revocar |
| Revocado | Sin acciones (estado final) |

#### Suspender vs Revocar

- **Suspender** = pausa temporal. Podés reactivar a la persona cuando
  quieras y vuelve a entrar con sus mismas credenciales.
- **Revocar** = saca a la persona de la cooperativa. Sus accesos quedan
  cerrados. Si querés que vuelva, tenés que invitarla de nuevo.

### 9.3 Salvaguarda del último administrador

Si vos sos el **único administrador activo** y tratás de degradarte o
revocarte, el sistema te lo va a impedir:

> ❌ **No puedes degradarte: eres el único administrador activo.**

Esto evita que la cooperativa quede sin administrador. Primero **promové a
otro miembro** y después sí podés degradarte vos.

### 9.4 Promover y degradar administradores

#### Promover

1. En la tabla de miembros, junto al miembro regular activo, clic en
   **"Promover"**.
2. Listo — ahora también es administrador y puede gestionar al equipo.

#### Degradar

1. En la tabla, junto al admin que querés degradar, clic en **"Degradar"**.
2. El sistema verifica que no sea el último admin (si lo es, te avisa y
   bloquea la acción).

### 9.5 Activar política "MFA obligatorio"

1. **Administración → Política MFA** (`/admin/tenant/{id}/mfa-policy`).
2. Verás el estado actual ("Opcional" o "Obligatorio").
3. Si activás "Obligatorio":

   ⚠️ **Lo que pasa**:
   - Los miembros que **ya tienen MFA** no notan nada.
   - Los miembros que **NO tienen MFA** serán redirigidos a la pantalla de
     configuración la próxima vez que entren. No podrán acceder al sistema
     hasta configurarlo.
   - **Las sesiones activas no se cierran inmediatamente** — el efecto se
     aplica en el próximo login.

4. Si la desactivás más adelante, los miembros pueden quitarse el MFA si
   ninguna otra cooperativa donde participen lo exige.

---

## 10. Soy administrador del SaaS (Master admin)

### 10.1 Dar de alta una nueva cooperativa

1. **SaaS → Nueva empresa** (`/saas/register-tenant`).
2. Llená el formulario:

   ```
   ┌──────────────────────────────────────────────┐
   │  Registrar nueva empresa                     │
   │                                              │
   │  Razón social:                               │
   │  [Cooperativa Solidaria SAS              ]   │
   │                                              │
   │  Nombre comercial:                           │
   │  [Coop. Solidaria                        ]   │
   │                                              │
   │  NIT:                                        │
   │  [900111222                              ]   │
   │                                              │
   │  Schema (slug interno):                      │
   │  [tenant_coop_solidaria                  ]   │
   │                                              │
   │  Correo de contacto:                         │
   │  [contacto@coop-solidaria.com            ]   │
   │                                              │
   │  Correo del primer administrador:            │
   │  [ana.perez@coop-solidaria.com           ]   │
   │                                              │
   │  [ Crear empresa y enviar invitación ]       │
   └──────────────────────────────────────────────┘
   ```

3. Presioná **"Crear empresa y enviar invitación"**. En un solo paso el
   sistema:

   - Crea la cooperativa.
   - Envía la invitación de administrador al correo indicado, con permisos
     de administrador ya incluidos.

4. La pantalla de confirmación muestra cuándo expira la invitación.

⚠️ **NIT duplicado**: si ya existe una cooperativa con ese NIT, el sistema
te avisa y rechaza el registro.

### 10.2 Resetear MFA de un usuario (recuperación operativa)

Caso típico: un cooperativista te llama porque **perdió el celular** donde
tenía la app de doble factor, y tampoco guardó los códigos de respaldo. No
puede entrar.

**Antes de hacer cualquier cosa**:

1. **Verificar la identidad** del usuario por canal alterno (videollamada,
   llamada al teléfono de su empresa, etc.). Esta es la línea de defensa
   contra ingeniería social — un atacante podría hacerse pasar por un
   cooperativista para entrar.

2. **Documentá el incidente** (en tu sistema interno, ticket o lo que uses).

#### Proceso

1. **SaaS → Resetear MFA** (`/saas/force-mfa-reset`).
2. Necesitás el **ID interno del usuario**. Lo encontrás en la BD
   `ADM_CentralUsers` o pidiéndolo al admin de la cooperativa.
3. Pegá el ID + escribí una **razón** específica de mínimo 10 caracteres:

   > **Ejemplo de razón correcta**: "Luis Martínez perdió celular en
   > Cooperativa de Pasto, verificado por videollamada el 06/06/2026.
   > Ticket #TKT-2026-348."
   >
   > **Razón insuficiente**: "test", "reset", "ok".

4. Presioná **"Resetear MFA"**.

#### Efectos

- El MFA del usuario queda desactivado.
- Sus sesiones activas en cualquier dispositivo se cierran.
- En su próximo login, si su cooperativa exige MFA, irá a la pantalla de
  configuración para reconfigurarlo.
- La acción queda **auditada** con tu nombre, fecha y la razón que
  escribiste. Esto es revisable durante auditorías SARLAFT.

⚠️ **Este reset NO cambia la contraseña** del usuario. Solo el MFA. Si
sospechás que la cuenta está comprometida (no solo el celular perdido),
combiná con un **reset de contraseña administrativo** (vía SQL — fuera del
alcance de este manual).

---

## 11. Preguntas frecuentes

**P: ¿Cuántas veces puedo intentar entrar antes de que me bloqueen?**
R: 5 intentos fallidos te bloquean 1 minuto. Después escala: 10 → 5 min,
15 → 15 min, 20 → 1 hora. El contador se resetea con cada inicio exitoso.

**P: ¿Puedo cambiar mi correo?**
R: Esta versión no tiene flujo de auto-servicio para cambiar correo. Si
necesitás cambiarlo, contactá al master admin.

**P: Si trabajo en 3 cooperativas, ¿necesito 3 MFAs distintos?**
R: No. El MFA es **uno por persona**, no por cooperativa. Un único código
de 6 dígitos te sirve para cualquier login en cualquier cooperativa.

**P: ¿Qué pasa si me invitan a una cooperativa y nunca acepto el enlace?**
R: La invitación expira a los 7 días (configurable por el master). Después
queda marcada como "Expirada" y no podés usarla. Pedile al admin que te
envíe una nueva.

**P: ¿El sistema me avisa cuando alguien cambia mi contraseña?**
R: Sí. Cada cambio de contraseña dispara un correo de notificación con la
fecha, la dirección IP y el navegador desde donde se hizo el cambio. Si vos
no hiciste el cambio, ese correo es tu alerta de seguridad.

**P: ¿Mis códigos de respaldo de MFA caducan?**
R: No. Pero **cada código se usa una sola vez**. Si los usás todos, tenés
que regenerarlos desde tu perfil.

**P: ¿Por qué a veces el sistema me pide que confirme con mi contraseña?**
R: En acciones sensibles (cambiar contraseña, desactivar MFA, cambiar el
correo de respaldo), re-confirmamos tu contraseña aunque ya estés logueado.
Esto te protege si dejaste la sesión abierta y otra persona toma tu
computadora.

---

## 12. Solución de problemas

### ❌ "Correo o contraseña incorrectos" pero estoy segura de mis datos

- Verificá que **caps lock** no esté activado.
- Tu correo es **insensible a mayúsculas** (ana@x.co = ANA@X.CO), pero la
  contraseña sí distingue.
- Si configuraste recientemente MFA, asegurate que estás escribiendo bien
  el código (los códigos cambian cada 30 segundos — escribilo rápido).
- Si nada funciona, usá el flujo "¿Olvidaste tu contraseña?".

### ❌ "Cuenta bloqueada temporalmente"

Esperá el tiempo que te indique el mensaje (60 segundos para el primer
bloqueo). Reintentar antes de tiempo no acelera el desbloqueo.

### ❌ "La contraseña aparece en filtraciones públicas"

Tu contraseña fue parte de algún data leak conocido (HaveIBeenPwned).
Aunque te la sepas, no es segura — cualquiera con una lista de contraseñas
filtradas podría adivinarla. Elegí otra.

### ❌ "No tienes acceso a ninguna empresa"

Tus membresías fueron revocadas o nunca aceptaste una invitación.
Contactá al admin de la cooperativa donde deberías estar.

### ❌ El código de mi app de MFA no es aceptado

- Asegurate que la hora de tu celular esté **sincronizada** con la red. Si
  tu reloj está desfasado, los códigos no coincidirán.
- Verificá que estás mirando la cuenta correcta en la app (si tenés varios
  servicios con MFA).
- Si el problema persiste, usá un código de respaldo (los que guardaste al
  activar MFA).
- Si tampoco tenés los códigos de respaldo, contactá al master admin para
  hacer un reset de MFA.

### ❌ No recibí el correo de invitación / reset

- Revisá la carpeta de **spam o promociones**.
- Verificá con quien te invitó que escribió bien tu correo.
- El correo viene de `no-reply@ingenia365.com`. Marcalo como contacto seguro
  para que no caiga en spam.
- Si pasaron más de 5 minutos y nada, pedí que reenvíen.

### ❌ "Acabo de cambiar mi contraseña pero me sigue pidiendo la vieja en otra app"

Si tenés IngenIA365ERP abierto en varios dispositivos, el cambio de
contraseña los cierra a todos. La próxima vez que abras la app en cualquier
dispositivo, tenés que volver a loguearte con la nueva contraseña.

---

## 13. Glosario para usuarios finales

- **Cooperativa / Empresa / Tenant**: la organización donde trabajás dentro
  de IngenIA365ERP. Cada cooperativa-cliente del SaaS es un "tenant".

- **Membresía**: tu vínculo con una cooperativa. Podés tener varias.

- **Doble factor / MFA / 2FA / TOTP**: el código de 6 dígitos que genera tu
  app móvil.

- **Identidad central**: la representación única de tu persona en el
  sistema. Tu correo, contraseña y MFA están vinculados a tu identidad, no
  a una cooperativa específica.

- **Master admin**: el administrador general del producto IngenIA365ERP
  (no de una cooperativa).

- **Códigos de respaldo**: los 10 códigos que guardás cuando activás MFA,
  para usar si perdés acceso al celular.

- **Invitación**: el enlace por correo que te permite unirte a una
  cooperativa.

- **Token**: el código secreto dentro del enlace de invitación o de reset.
  Tiene tiempo de vida limitado y un solo uso.

---

## 14. ¿Necesitás ayuda?

- **Si sos cooperativista**: tu primer punto de contacto es el
  administrador de tu cooperativa.
- **Si sos admin de cooperativa**: para incidencias técnicas (sistema
  caído, errores extraños), contactá al equipo de soporte de IngenIA365.
- **Si sos master admin**: estás solo 💪. Mirá los runbooks técnicos en
  `docs/operaciones/` y la documentación de spec en
  `specs/002-identidad-central-federada/`.

---

**Última revisión**: junio 2026 · IngenIA365ERP v2.0 (Feature 002).
