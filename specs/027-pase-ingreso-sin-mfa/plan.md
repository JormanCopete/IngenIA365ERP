# Plan de implementación: 027 — Pase de ingreso sin segundo factor

**Rama**: `027-pase-ingreso-sin-mfa` · **Fecha**: 2026-11-26 · **Spec**: [spec.md](spec.md)
**Decisiones del dueño (2026-11-26)**: enlace sólo al correo, reutilizable, 12 h por defecto y como máximo; apagado de fábrica por
cooperativa; el pase a un administrador lo da sólo el maestro; topes 5 por usuario en 7 días y 20 por cooperativa al día; en MAUI el enlace
abre la web y en la app se puede pegar el enlace o el código en el desafío.

## Resumen

Un administrador (permiso `Security.Users.GrantMfaBypass`) concede a un usuario de su cooperativa un **pase de ingreso**: un secreto aleatorio
que viaja sólo en un enlace al correo verificado del usuario y se guarda como huella SHA-256 en `ADM_LoginPasses` (base administrativa). El
usuario abre el enlace (`/security/pase?t=…`), entra con su identificador y su contraseña por el **login de siempre** y, cuando el login
devuelve el desafío `MfaRequired`, el cliente canjea el pase en `POST /api/auth/mfa/pass` con el token de desafío. El servidor verifica el
pase y emite la sesión por el **mismo** `EmisorDeSesionTrasSegundoFactor`, con el método `Ninguno` y **limitada a la cooperativa del pase**.
Nada de los autenticadores se toca.

## Contexto técnico

- **Lenguaje/stack**: .NET 10, Minimal APIs con Carter, MediatR, EF Core (PostgreSQL y SQL Server), Blazor Shared (web y MAUI).
- **Almacenamiento**: base administrativa `IngenIA365ERP_Admin` (`ADM_LoginPasses`, dos columnas nuevas en `ADM_TenantMfaPolicies`).
- **Correo**: `IEmailSender` directo con `IIdentityEmailTemplates` (como la recuperación por correo): al canjear todavía no hay cooperativa
  elegida, y concesión y avisos son de identidad central.
- **Sesión**: claim nuevo `login_pass_tenant` en el access y campo `PaseDeIngresoCooperativa` en `CentralRefreshSession` (Redis, con valor
  por defecto `null`, retrocompatible).
- **Pruebas**: unitarias de dominio (`LoginPass`), de aplicación (concesión, canje, reglas, topes, anulación) y de arquitectura; una e2e al
  final.
- **Sin incógnitas abiertas** (ver [research.md](research.md)).

## Constitution Check

| Principio | Cómo se cumple |
|---|---|
| I Spec-first | spec con decisiones del dueño; este plan y tasks antes del código. |
| II Clean Architecture | Entidad en Domain/Entities/Admin; comandos en Application/Identity/PasesDeIngreso; endpoints en API; correo por puertos ya existentes. |
| III CQRS | Conceder, reenviar, anular, canjear y listar son comandos/consultas MediatR. |
| IV Multi-tenancy | El pase vive en la base administrativa con el `TenantId` (PublicId) de la cooperativa; la sesión del pase no puede cambiar de cooperativa. Nada se escribe en bases de cooperativa salvo la auditoría por `AuditoriaDeIdentidad`. |
| V Person | No aplica (identidad central). |
| VI PublicId | Rutas y respuestas por `PublicId` del pase, del usuario y de la cooperativa. |
| VII Soft-delete + auditoría | El pase no se borra: se anula con motivo; concesión, uso, reenvío y anulación se auditan. |
| VIII Validación dual | Validador FluentValidation (motivo 10–500, horas 1–12) y la misma validación en el diálogo. |
| IX Errores visibles | Correo que no sale se informa al administrador (`CorreoEnviado = false`); el canje fallido devuelve un mensaje genérico pero visible. |
| X Trazabilidad | Auditoría con IP y actor; aviso a los demás administradores. |
| XI Inmutabilidad | No aplica a contabilidad. |
| XII Migraciones | Una migración par **aditiva** en el árbol administrativo, fechada después de la última (`InactividadPorCooperativa`). |

Sin violaciones. Re-evaluado tras el diseño: sin cambios.

## Fase 1 — Dominio y esquema

- `LoginPass` (Domain/Entities/Admin): `CentralUserId`, `TenantId`, `GrantedByUserId`, `GrantedByMaster`, `Reason`, `TokenHash` (32 bytes),
  `SecurityStampAtGrant`, `GrantedAt`, `ExpiresAt`, `UseCount`, `LastUsedAt`, `LastUsedIp`, `VoidedAt`, `VoidedByUserId`, `VoidReason`.
  Reglas: `EstaVigente(ahora)`, `RegistrarUso`, `Anular(motivo)` idempotente; motivos fijos en `LoginPass.Motivos`.
- `TenantMfaPolicy`: `AllowLoginPass` (falso) y `LoginPassMaxHours` (12; 1–12) con `ConfigurarPasesDeIngreso`.
- Configuración EF, `IAdminDbContext.LoginPasses`, migración par `PasesDeIngreso` (PostgreSQL y SQL Server).

## Fase 2 — Conceder, reenviar, anular, listar (HU1, HU3, HU4, HU5, HU6)

- `ReglasDelPaseDeIngreso` (puro): quién puede dar a quién (a sí mismo no; a un administrador sólo el maestro; al maestro nunca; correo
  verificado; con segundo factor inscrito; cooperativa con la opción encendida; horas dentro del tope; topes contra abuso).
- `ConcederPaseDeIngresoCommand` (actor: administrador de la cooperativa activa o el maestro con la cooperativa en la ruta): anula el vivo
  anterior («Reemplazado»), genera el secreto con `ISecureTokenGenerator`, guarda, envía el correo al usuario y avisa a los demás
  administradores; devuelve el pase **sin** el enlace y `correoEnviado`.
- `ReenviarPaseDeIngresoCommand`: anula el vigente como «Reemplazado» y concede uno nuevo con el mismo motivo y el vencimiento restante.
- `AnularPaseDeIngresoCommand` con motivo.
- `ListarPasesDeIngresoQuery` por usuario (para la ficha) y por cooperativa (para la consola del maestro).
- Al apagar la opción en `UpdateTenantMfaPolicyCommand`, se anulan los vivos de la cooperativa («Opción apagada»).
- Permiso `Security.Users.GrantMfaBypass` en `DomainPermissionCatalogSeeder` (CompanyAdmin lo recibe por `*`).

## Fase 3 — Canje en el ingreso (HU1, HU2, HU3)

- `UsarPaseDeIngresoCommand` en `POST /api/auth/mfa/pass` con el token `mfa-verify`: huella → pase del **mismo** usuario, vigente; revalida
  en el momento (opción encendida, membresía activa, sello de seguridad igual al de la concesión — cubre cambio de clave y reset —, usuario
  activo); si algo falla lo anula con su motivo. Fallo: mensaje genérico `Identity.LoginPass.Invalid` y cuenta en el contador de intentos
  del segundo factor. Éxito: `RegistrarUso`, auditoría, correo al usuario, y `EmisorDeSesionTrasSegundoFactor.EmitirAsync` con
  `Ninguno` y `soloCooperativa`.
- `EmisorDeSesionTrasSegundoFactor`: parámetro opcional `OrigenDelIngreso` — con pase filtra las membresías a la cooperativa del pase, no
  cancela recuperaciones y sella `login_pass_tenant`; sin pase (TOTP, passkey o código de respaldo) **anula los pases vivos** del usuario
  («Entró con su segundo factor»).
- `ICentralJwtIssuer.IssueAccessToken` gana `Guid? paseDeIngresoCooperativa = null` → claim `login_pass_tenant`; `CentralRefreshSession`
  gana `PaseDeIngresoCooperativa`; el refresh lo arrastra; `SwitchTenant` y `SelectTenant` rechazan otra cooperativa
  (`Identity.LoginPass.TenantLocked`). `ICurrentCentralUserContext.PaseDeIngresoCooperativa`.
- `/api/auth/me` informa `enteredWithLoginPass` para el aviso.

## Fase 4 — Pantallas (HU1, HU5, HU6, FR-016, FR-017, FR-021)

- `/security/pase?t=` (MinimalLayout): guarda el pase en `CentralAuthClient.PaseDeIngreso` y lleva al login con el aviso «Entrás con un
  pase temporal: escribí tu usuario y tu contraseña».
- `MfaChallenge.razor`: con pase pendiente lo canjea solo; enlace «Tengo un pase de ingreso» que acepta el enlace o el código pegado
  (MAUI).
- Ficha del usuario (`/admin/usuarios/{id}`): botón «Permitir ingreso sin segundo factor» (diálogo con horas y motivo) junto a «Enviar
  enlace de recuperación», y la lista de pases con «Anular» y «Reenviar».
- `TenantMfaPolicyPage`: interruptor «Permitir pases de ingreso sin segundo factor» y horas máximas.
- Aviso permanente en el layout cuando la sesión entró con pase, con el botón a `/perfil/mfa`.
- Consola del maestro: sección en `/saas/...` (identidades) para conceder/anular por cooperativa.

## Fase 5 — Manual, CLAUDE.md, pruebas de punta a punta

- `docs/manual/pase-de-ingreso.md`; línea en CLAUDE.md.
- E2e `PaseDeIngresoTests` (una vez, al final): conceder, entrar con contraseña + pase, reutilizar, no cambiar de cooperativa, anular al
  entrar con TOTP, rechazos de HU4.

## Riesgos

- **El pase vuelve al segundo factor tan fuerte como el correo durante 12 h.** Mitigado: contraseña siempre, una persona lo autoriza con
  motivo, aviso a los demás administradores y al usuario en cada uso, sólo una cooperativa, se anula al primer ingreso normal.
- **El token en la URL** queda en el historial del navegador: es el mismo enlace que ya está en el correo; vence a las 12 h.
- **Sesiones vivas en Redis** sin el campo nuevo: `null` = sesión normal.

## Estructura

```text
src/Core/IngenIA365ERP.Domain/Entities/Admin/LoginPass.cs
src/Core/IngenIA365ERP.Application/Identity/PasesDeIngreso/
  ReglasDelPaseDeIngreso.cs  ConcederPaseDeIngreso.cs  AnularYReenviar.cs  ListarPasesDeIngreso.cs
  UsarPaseDeIngreso.cs  AvisosDelPaseDeIngreso.cs  AnuladorDePasesAlEntrar.cs
src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Admin/LoginPassConfiguration.cs
src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Admin/2026112…_PasesDeIngreso.cs
src/Infrastructure/IngenIA365ERP.Storage/Templates/LoginPass*.html
src/Presentation/IngenIA365ERP.API/Endpoints/PasesDeIngresoModule.cs
src/Presentation/IngenIA365ERP.Shared/Pages/Security/PaseDeIngreso.razor (+ cambios en MfaChallenge, Users, TenantMfaPolicyPage)
```

## Complexity Tracking

Sin desviaciones.
