# Análisis del sistema actual — insumo de la feature 017

**Fecha**: 2026-10-27 · **Base**: `origin/develop` `f7237aaa` · Sólo código y datos de
prueba locales; no se consultó ningún ambiente.

Este documento es el «Paso 0» del borrador del dueño: lo que hay hoy, contrastado con lo que
el borrador supone. Las rutas son relativas a `src/`.

## 1. Lo que el borrador supone y no es así

| El borrador dice | Hoy es | Consecuencia para la spec |
|---|---|---|
| Existe **una** tabla de personas/usuarios con tipo y número de documento, nombre, apellido, correo y usuario | Hay **tres** sitios y ninguno tiene todo: `ADM_CentralUsers` en la base administrativa (sólo correo, clave, segundo factor, estado), `SEC_Users` en **cada** cooperativa (usuario, correo, `IdentificationNumber` texto libre de 20, `PersonId` opcional) y `COR_People` en **cada** cooperativa (documento, nombres, celular) | La «Persona global» del borrador **no puede** ser `COR_People`: hay una por cooperativa (Principio IV). La identidad global tiene que vivir en la base administrativa y enlazarse a la persona de cada cooperativa por id interno |
| La persona es una sola en todo Ingenia365 | Constitución v2.0.0, Principio IV: una base por cooperativa, aislamiento físico, **nunca** joins entre cooperativas ni tablas `ADM_*` replicadas en una cooperativa. Principio V: `COR_People` es la maestra de datos personales **dentro** de la cooperativa | Dos niveles: la **identidad de acceso** (central, una por ser humano) y la **persona de la cooperativa** (una por cooperativa, la de siempre). El documento existe en los dos y hay que decidir quién manda |
| Usuario = documento (CC) o tipo+número | `UserName` central = el correo (`AspNetCoreIdentityProvider.cs:87`); `SEC_Users.Username` = el correo (`TenantUserProvisioner.cs:145`) | El usuario derivado es un **identificador nuevo**; no puede reutilizar `UserName` sin tocar a todos los existentes |
| CC 123456 y CE 123456 son personas distintas | `UK_COR_People_TaxId` es único sobre **el número solo**, sin el tipo y sin filtrar eliminadas (`Persistence/Configurations/Core/PersonConfiguration.cs:114`) | Dentro de una cooperativa hoy **no pueden coexistir**. O se acepta esa limitación local o se cambia el índice de `COR_People` (migración por cooperativa) |
| Catálogo CC, CE, PPT, PEP, TI, PA | `COR_People.IdType` es texto de **2** caracteres con lista cerrada en la interfaz: `C, CE, TI, RC, NI, PA, PE` (`Shared/Services/Core/CatalogosDePersona.cs:14`); **no hay PPT**; la cédula es `C`, no `CC`; no hay tabla de catálogo. La facturación electrónica tiene su propio mapeo a la DIAN que sí conoce PPT (48) pero ningún `IdType` lo apunta | El catálogo de tipos de documento es nuevo y tiene que convivir con los códigos de `COR_People`. `PPT` (3 letras) no cabe en la columna actual |
| Correo verificado | `EmailConfirmed` existe en la identidad central; sólo lo pone en `true` el registro por invitación y las semillas; **nadie lo exige** al entrar | Exigir «sólo correos verificados» sin migración de datos expulsaría a quien tenga `EmailConfirmed = false` |
| Correo único en base de datos | El índice de `NormalizedEmail` central **no es único**; la unicidad es de aplicación (`RequireUniqueEmail = true`, `CentralIdentityServiceCollectionExtensions.cs:45`). Normalización = `ToUpperInvariant` de Identity, sin collation | Antes de poner un índice único hay que diagnosticar duplicados (ver `diagnostico-de-datos.md`) |
| El administrador de empresa no cambia claves | **Hoy sí las cambia**: `POST /api/admin/users/{id}/reset-password` (`AdminResetPasswordCommandHandler`) fija la clave **central** de la persona, que vale para **todas** sus cooperativas | Retirar o reemplazar esto es un cambio de comportamiento para los administradores actuales; decisión del dueño |
| Retirar de una empresa cierra sus sesiones en esa empresa | `DisableUser` y `RevokeMembership` revocan la membresía y los refresh **de la cooperativa**, pero no los de la sesión central, y buscan a la identidad central **por correo** (`DisableUserCommandHandler.cs:96,128`), anotando como autor al usuario retirado (`:137`) | Hueco existente; entra en la spec como requisito y se arregla de paso |
| Bloqueo y rate limit por identificador y por IP | Bloqueo progresivo en Redis **por correo normalizado** (`RedisLoginAttemptCounter.cs:74`); rate limit sólo por IP (`CF-Connecting-IP`, 10/min en login) | El contador tiene que contar **por identidad**, no por texto digitado: si no, alternar correo y usuario duplica los intentos |
| Tiempo de respuesta homogéneo | Un correo desconocido responde sin calcular ningún hash (`LoginCommandHandler.cs:76`) | Hoy se puede enumerar correos por tiempo. Requisito nuevo |

## 2. Dónde el correo es llave hoy

- **Ingreso**: `LoginCommandHandler` → `FindByEmailAsync`; el validador exige formato de correo
  (`LoginCommandValidator.cs:9`).
- **Recuperación de clave**: `RequestPasswordResetCommandHandler.cs:32` por correo; el enlace va a
  `user.Email`.
- **Invitaciones**: `ADM_Invitations.NormalizedEmail`; aceptar con sesión abierta exige que el correo
  del JWT sea el de la invitación (`AcceptInvitationCommandHandler.cs:356`).
- **Cooperativa ↔ central**: `TenantUserProvisioner` (por `CentralUserId` y, para filas viejas, por
  correo), `PermisosDeLaPeticion.cs:105` (ídem), `DisableUser` y `AdminResetPassword` (sólo por correo).
- **Claims**: el JWT central lleva `email`; `CurrentUserService.UserName` lo usa como nombre del autor
  en la auditoría.
- **Contadores e historial**: llaves Redis de bloqueo y `ADM_CentralUserLoginAttempts.NormalizedEmail`.
- **Etiqueta TOTP**: la app autenticadora muestra el correo.
- **Lecturas**: muchas vistas muestran `u.Email ?? u.Username` como nombre de la persona.

Ninguna tabla de negocio referencia a la persona por correo: los módulos usan `PersonId` (int) o el
`Id` de `SEC_Users`. El riesgo está en la **capa de identidad**, no en el negocio.

## 3. Lo que ya existe y la feature reusa

- Membresía `ADM_TenantMemberships` con estados `Invited, Active, Suspended, Revoked` y único
  `(CentralUserId, TenantId)` entre vivas. Falta un estado de «requiere revisión».
- Invitación `ADM_Invitations`: token de un solo uso guardado como SHA-256, vence a los 7 días
  (configurable), reenviar y cancelar (`/reenviar`, `DELETE`), estados
  `Pending, Accepted, Expired, Revoked, Superseded`, trabajo horario de vencimiento. Hoy sólo
  captura el **correo** (y «como administradora» si invita el maestro).
- Aceptación con tres ramas: sesión abierta, registro nuevo (sólo clave), credenciales existentes;
  después la puerta del segundo factor (`MfaRequired` / `MfaEnrollmentRequired`).
- Selector de cooperativa tras autenticar; las membresías no activas no aparecen.
- Auditoría en Mongo por cooperativa (`{prefijo}_{tenant}`) y `{prefijo}_Global` sin cooperativa;
  los eventos `Invitation.Issued.*`, `Revoked`, `Superseded` están definidos pero **no se emiten**
  explícitamente (sólo el `AuditBehavior` genérico).
- Unicidad de documento en `COR_People` incluso frente a eliminadas, con `restore`
  (`Person.TaxIdDeleted`, feature 008).

## 4. Calidad de datos observada en local

En las semillas y pruebas no aparecen duplicados de correo que difieran sólo en mayúsculas, pero
**no prueban nada** sobre los ambientes: la identidad central no tiene índice único de correo, así
que un alta concurrente o una carga directa pudo dejar dos. `SEC_Users.Email` no es único por
cooperativa, `IdentificationNumber` es texto libre sin normalizar y `COR_People.TaxId` tampoco se
normaliza en el servidor (sólo se recorta en la pantalla). Las consultas que el dueño debe correr
en cada ambiente están en `diagnostico-de-datos.md`.
