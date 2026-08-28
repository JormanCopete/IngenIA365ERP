# Contract: Tenant MFA Policy

**Módulo Carter**: `TenantMfaPolicyModule.cs`
**Rutas**: `/api/tenants/{tenantPublicId}/mfa-policy`

La política dice **dos** cosas independientes: si se exige segundo factor
(`isRequired`) y **qué métodos se aceptan** (`metodosAceptados`). Los métodos sólo
se consultan cuando se exige — una cooperativa que no exige nada no puede rechazar
a nadie por cómo entró.

Métodos válidos: `"Totp"` (app de códigos) y `"WebAuthn"` (llave o passkey). Son
los mismos literales que devuelve `GET /api/profile/mfa/credentials` en el campo
`tipo`. **No existe un literal para el correo**: es una vía de recuperación, no un
factor de ingreso, y no poder escribirlo es lo que impide que alguien lo habilite
como tal.

---

## GET /api/tenants/{tenantPublicId}/mfa-policy

**Authorization**: admin del tenant o master.

**Response** `200 OK`:

```json
{
  "tenantPublicId": "...",
  "isRequired": false,
  "activatedAt": null,
  "deactivatedAt": null,
  "metodosAceptados": ["Totp", "WebAuthn"]
}
```

Sin fila de política se responde `isRequired: false` y **todos** los métodos. «No
hay política» no puede significar «no exige» en un campo y «no acepta nada» en el
otro.

---

## PUT /api/tenants/{tenantPublicId}/mfa-policy

**Authorization**: admin del tenant o master.

**Request**:

```json
{
  "isRequired": true,
  "metodosAceptados": ["WebAuthn"],
  "permitirRecuperacionPorCorreo": false,
  "horasDeDemora": 24
}
```

`permitirRecuperacionPorCorreo` **nace apagada**. Encenderla es aceptar que quien
controle un buzón pueda, con la contraseña y una espera, retirarle el segundo
factor a una persona — es decir, que el segundo factor valga lo que valga el
buzón. Las mitigaciones que lo hacen defendible están en
[auth.md](auth.md#recuperación-del-segundo-factor-por-correo), y ninguna es
opcional.

`horasDeDemora` tiene mínimo 1 (`Validation.TenantMfaPolicy.DemoraInsuficiente`).
Cero no es «recuperación rápida»: es el diseño sin demora, donde no hay aviso que
llegue a tiempo ni cancelación posible.

**Con varias cooperativas manda la más estricta**: basta una que exija segundo
factor y no permita esta vía para que no se pueda, y la demora aplicada es la más
larga. Si bastara una permisiva, cualquiera entraría en la cooperativa exigente
pidiendo la recuperación «por» la otra.

`metodosAceptados` **omitido o `null` deja los métodos como estaban** — no los
reinicia. Es semántica parcial dentro de un PUT, a sabiendas: en un PUT de
reemplazo el campo ausente significaría reset, y eso ampliaría o restringiría los
métodos sin que nadie lo pidiera, dejando gente fuera por una llamada que sólo
quería encender la exigencia.

**Response** `200 OK`:

```json
{ "miembrosSinMetodoAceptado": 3 }
```

Cuántas personas de la cooperativa **tienen** segundo factor pero ninguno de los
métodos que la política acepta ahora. No es un error —el sistema las manda a
inscribir— pero es el número que separa a una administradora que decide informada
de una que encierra a su cooperativa por accidente. No cuenta a quien no tiene
ningún segundo factor: a esas personas ya las afectaba `isRequired`.

- `422` con `Validation.TenantMfaPolicy.SinMetodos` si se exige MFA con la lista
  vacía. Es un estado imposible: la única salida que ofrece el sistema —«inscribí
  uno de estos»— sería mentira, y la cooperativa entera quedaría encerrada con una
  sola llamada.
- `403` con `Tenant.Forbidden` si quien llama no administra esa cooperativa.

**Side effects**:

- Invalida la caché de membresías del tenant en Redis.
- En el próximo login, quien no tenga **ningún método aceptado** recibe
  `MfaEnrollmentRequired` con `metodosAceptados`, en vez de que se le pida un
  código que iba a verificar bien para nada.
- **Las sesiones abiertas dejan de renovarse.** En su próximo `/api/auth/refresh`
  se reevalúa la política y, si el método sellado ya no vale, se responde
  `Tenant.MfaMethodNotAccepted` y la persona vuelve por el login. Sin eso el
  refresh se renueva a sí mismo cada doce horas indefinidamente y la política no
  llegaría a morder nunca a quien ya estaba dentro — que suele ser todo el mundo.

**Auditoría**: `TenantMfaPolicy.Activated`, `TenantMfaPolicy.Deactivated`, o
`TenantMfaPolicy.MethodsChanged` cuando cambian los métodos sin cambiar la
exigencia. Los tres con `OldValuesJson`/`NewValuesJson` poblados: una auditoría
que dice «cambió la política» sin decir de qué a qué no sirve el día que haya que
reconstruir por qué media cooperativa se quedó fuera.

---

## Qué se comprueba, y dónde

La regla vive en un solo sitio (`GuardiaDeMetodos.Evaluar`) y la consultan las
**siete** puertas que acuñan un token con cooperativa resuelta: los dos
auto-selects tras el segundo factor, `select-tenant`, `switch-tenant`, el ascenso
de sesión tras inscribir, aceptar una invitación y el refresh.

Dos reglas del guardián que parecen detalles de implementación y son decisiones:

1. **La máscara sólo muerde si `isRequired`.** Sin esto, una cooperativa que no
   exige nada rechazaría a todos sus miembros: ninguno tiene método sellado.
2. **Una máscara que acepta todo no mira el método.** Es lo que impide que el día
   del despliegue todas las sesiones vivas —emitidas antes de que el dato
   existiera— caigan a la vez en «te falta inscribir» sin que ninguna política
   haya cambiado.

**Nunca se rechaza sin salida**: el veredicto es «adelante» o «te falta inscribir
alguno de estos», con la lista. Por eso la máscara vacía se rechaza al escribir y
no al leer.

---

## Política de la plataforma (el administrador maestro)

**Rutas**: `GET`/`PUT /api/saas/mfa-policy`. Sólo el maestro.

Decide **qué** métodos acepta la plataforma para esa cuenta. **No hay campo para
«si se exige»** y no lo va a haber: que el maestro tenga segundo factor es una
constante, y hacerlo editable pondría el interruptor de apagar la seguridad de la
cuenta más poderosa al alcance de quien consiga usarla una vez.

```json
{ "metodosAceptados": ["Totp", "WebAuthn"], "rescateActivo": false, "changedAt": null }
```

- `409` con `Saas.PlatformMfaPolicy.TeDejariaFuera` si el maestro no tiene ningún
  autenticador de los que la política aceptaría. Se comprueba **antes** de guardar
  porque es la única cuenta que nadie más puede rescatar.
- `422` con `Validation.PlatformMfaPolicy.SinMetodos` si la lista va vacía.

**El rescate**: la clave de configuración `Mfa:PlataformaSinRestriccion`. Puesta a
`true`, la política guardada se ignora y se aceptan todos los métodos; el campo
`rescateActivo` de la respuesta lo dice en voz alta, para que nadie crea que una
restricción está en vigor cuando no lo está. Existe porque `ForceMfaReset` exige
ser maestro —o sea, el maestro sólo puede rescatarse a sí mismo— y la doble
aprobación corre sobre la base de una cooperativa donde él no existe. Se escribió
y se probó antes que la política: un rescate que llega después del riesgo no es un
rescate.
