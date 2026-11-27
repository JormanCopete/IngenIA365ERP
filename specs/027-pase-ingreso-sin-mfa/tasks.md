# Tareas — 027 Pase de ingreso sin segundo factor

Pruebas de punta a punta una sola vez al final (como en las features anteriores).

## Fase 1 — Dominio y esquema
- [ ] T001 `LoginPass` en `Domain/Entities/Admin/LoginPass.cs` (crear, vigente, uso, anulación idempotente, motivos)
- [ ] T002 `TenantMfaPolicy`: `AllowLoginPass`, `LoginPassMaxHours`, `ConfigurarPasesDeIngreso`
- [ ] T003 `LoginPassConfiguration` + columnas en `TenantMfaPolicyConfiguration`; `IAdminDbContext.LoginPasses` y `AdminDbContext`
- [ ] T004 Migración par aditiva `PasesDeIngreso` (PostgreSQL y SQL Server, árbol administrativo)
- [ ] T005 Pruebas de dominio `LoginPassTests` y de la política

## Fase 2 — Conceder, reenviar, anular, listar
- [ ] T010 `ReglasDelPaseDeIngreso` (puro) + pruebas
- [ ] T011 `AvisosDelPaseDeIngreso` (correo al usuario con enlace, a los administradores, aviso de uso) + plantillas HTML
- [ ] T012 `ConcederPaseDeIngresoCommand` (+ validador) con topes y reemplazo
- [ ] T013 `ReenviarPaseDeIngresoCommand`, `AnularPaseDeIngresoCommand`
- [ ] T014 `ListarPasesDeIngresoQuery`
- [ ] T015 Permiso `Security.Users.GrantMfaBypass`; eventos de auditoría
- [ ] T016 `UpdateTenantMfaPolicyCommand`/`GetTenantMfaPolicyQuery`: campos nuevos y anulación al apagar
- [ ] T017 Endpoints `/api/admin/users/{id}/login-passes` y `/api/saas/tenants/{id}/login-passes`; `canGrantLoginPass` en la ficha
- [ ] T018 Pruebas de aplicación de conceder/anular/reenviar

## Fase 3 — Canje
- [ ] T020 `ICentralJwtIssuer` claim `login_pass_tenant`; `CentralRefreshSession.PaseDeIngresoCooperativa`; contexto actual
- [ ] T021 `EmisorDeSesionTrasSegundoFactor` con origen del ingreso (filtrar, no cancelar recuperación, anular pases al entrar con 2FA)
- [ ] T022 `UsarPaseDeIngresoCommand` + `POST /api/auth/mfa/pass`
- [ ] T023 Refresh arrastra la marca; `SwitchTenant`/`SelectTenant` rechazan otra cooperativa
- [ ] T024 `/api/auth/me` informa la marca
- [ ] T025 Pruebas de aplicación del canje y del bloqueo de cooperativa

## Fase 4 — Pantallas
- [ ] T030 Cliente: `CentralAuthClient.PaseDeIngreso`, `UsarPaseAsync`, `LoginPassesClient`
- [ ] T031 `/security/pase` y aviso en el login
- [ ] T032 `MfaChallenge`: canje automático y «Tengo un pase de ingreso»
- [ ] T033 Ficha del usuario: botón, diálogo y lista de pases
- [ ] T034 `TenantMfaPolicyPage`: interruptor y horas
- [ ] T035 Aviso permanente en el layout
- [ ] T036 Consola del maestro

## Fase 5 — Cierre
- [ ] T040 Manual `docs/manual/pase-de-ingreso.md`, CLAUDE.md
- [ ] T041 Suites sin contenedores verdes
- [ ] T042 E2e `PaseDeIngresoTests` (PostgreSQL y SQL Server)
- [ ] T043 Recorrido a mano en web y MAUI (del dueño)
