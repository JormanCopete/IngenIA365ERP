# Tareas — 027 Pase de ingreso sin segundo factor

Pruebas de punta a punta una sola vez al final (como en las features anteriores).

## Fase 1 — Dominio y esquema
- [x] T001 `LoginPass` en `Domain/Entities/Admin/LoginPass.cs` (crear, vigente, uso, anulación idempotente, motivos)
- [x] T002 `TenantMfaPolicy`: `AllowLoginPass`, `LoginPassMaxHours`, `ConfigurarPasesDeIngreso`
- [x] T003 `LoginPassConfiguration` + columnas en `TenantMfaPolicyConfiguration`; `IAdminDbContext.LoginPasses` y `AdminDbContext`
- [x] T004 Migración par aditiva `PasesDeIngreso` (PostgreSQL y SQL Server, árbol administrativo)
- [x] T005 Pruebas de dominio `LoginPassTests` y de la política

## Fase 2 — Conceder, reenviar, anular, listar
- [x] T010 `ReglasDelPaseDeIngreso` (puro) + pruebas
- [x] T011 `AvisosDelPaseDeIngreso` (correo al usuario con enlace, a los administradores, aviso de uso) + plantillas HTML
- [x] T012 `ConcederPaseDeIngresoCommand` (+ validador) con topes y reemplazo
- [x] T013 `ReenviarPaseDeIngresoCommand`, `AnularPaseDeIngresoCommand`
- [x] T014 `ListarPasesDeIngresoQuery`
- [x] T015 Permiso `Security.Users.GrantMfaBypass`; eventos de auditoría
- [x] T016 `UpdateTenantMfaPolicyCommand`/`GetTenantMfaPolicyQuery`: campos nuevos y anulación al apagar
- [x] T017 Endpoints `/api/admin/users/{id}/login-passes` y `/api/saas/tenants/{id}/login-passes`; `loginPassMaxHours` en la ficha
- [ ] T018 Pruebas de conceder/anular/reenviar (las reglas tienen prueba unitaria; los manejadores, en la e2e T042)

## Fase 3 — Canje
- [x] T020 `ICentralJwtIssuer` claim `login_pass_tenant`; `CentralRefreshSession.PaseDeIngresoCooperativa`; contexto actual
- [x] T021 `EmisorDeSesionTrasSegundoFactor` con origen del ingreso (filtrar, no cancelar recuperación, anular pases al entrar con 2FA)
- [x] T022 `UsarPaseDeIngresoCommand` + `POST /api/auth/mfa/pass`
- [x] T023 Refresh arrastra la marca; `SwitchTenant` rechaza otra cooperativa (la sesión del pase nunca pasa por `SelectTenant`: sólo ve una cooperativa)
- [x] T024 `/api/auth/me` informa la marca
- [ ] T025 Pruebas del canje y del bloqueo de cooperativa (la lectura del enlace tiene prueba unitaria; el canje, en la e2e T042)

## Fase 4 — Pantallas
- [x] T030 Cliente: `CentralAuthClient.PaseDeIngreso`, `UsarPaseDeIngresoAsync`; clientes de la ficha y del maestro
- [x] T031 Enlace `/security/login?pase=` y aviso en el login (sin página aparte: el login guarda el pase)
- [x] T032 `MfaChallenge`: canje automático y «Tengo un pase de ingreso»
- [x] T033 Ficha del usuario: botón, diálogo y lista de pases
- [x] T034 `TenantMfaPolicyPage`: interruptor y horas
- [x] T035 Aviso permanente en el layout
- [x] T036 Consola del maestro

## Fase 5 — Cierre
- [x] T040 Manual `docs/manual/pase-de-ingreso.md`, CLAUDE.md
- [ ] T041 Suites sin contenedores verdes
- [ ] T042 E2e `PaseDeIngresoTests` (PostgreSQL y SQL Server)
- [ ] T043 Recorrido a mano en web y MAUI (del dueño)
