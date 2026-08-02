# IngenIA365ERP — Índice de Documentación

## Proyecto
- **Nombre**: IngenIA365ERP (anteriormente SOLIDO ERP)
- **Tipo**: ERP Financiero SaaS Multi-Tenant para Cooperativas
- **Stack**: .NET 10.0.5, Blazor Hybrid MAUI + Web, SyncFusion 33.1.44, SQL Server, MongoDB, Redis

## Ubicaciones del Proyecto

| Ubicación | Ruta | Descripción |
|---|---|---|
| **PROYECTO ACTUAL** | D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\ | Solución activa de desarrollo |
| Proyecto original VB.NET | D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\ | Solución SOLIDO original (272 formularios WinForms) |
| Fase 1 (VB.NET → C#) | D:\...\SOLIDO\Plugins\PluginsComplete\ | ERP.Core consolidado (231 archivos, 149K líneas) |
| Fase 2 (origen) | D:\...\SOLIDO\IngenIA365ERP\ | Copia de trabajo anterior (ya migrada aquí) |

## Documentación por Fase

### Fase 0: Fundamentos
| Archivo | Descripción |
|---|---|
| `.specify/memory/constitution.md` | Constitución del proyecto — doce principios vinculantes (Spec-First, Clean Architecture, CQRS + MediatR, Multi-tenancy, Person centralizada, PublicId, Soft-delete + auditoría, Validación dual, Errores visibles, SIPLA/SARLAFT, Inmutabilidad contable, Migraciones idempotentes). v1.0.0 — Ratificada 2026-05-03. |
| `specs/001-cimientos-tecnicos/spec.md` | Spec funcional de la Fase 0 — 7 user stories, FRs y success criteria. |
| `specs/001-cimientos-tecnicos/plan.md` | Plan técnico de implementación (Application/Infrastructure/Presentation). |
| `specs/001-cimientos-tecnicos/research.md` | Investigación previa: librerías evaluadas, alternativas descartadas. |
| `specs/001-cimientos-tecnicos/data-model.md` | Modelo de datos: entidades, relaciones, índices. |
| `specs/001-cimientos-tecnicos/contracts/` | Contratos REST por módulo (`auth.md`, `users.md`, etc.). |
| `specs/001-cimientos-tecnicos/quickstart.md` | Recorrido end-to-end de las 7 user stories. |
| `specs/001-cimientos-tecnicos/tasks.md` | 142 tareas T001–T142 con estado y nota de cierre. |
| `docs/operaciones/dev-environment.md` | Cómo levantar el stack dev local (`docker compose -f docker/dev.yml`). |
| `docs/operaciones/slo.md` | **Service Level Objectives** — 99.5 % mensual, ventanas, error budget (T134). |
| `docs/operaciones/runbook-fase0.md` | **Runbook** de incidentes típicos: lockout, SMTP, Mongo down, rotación de claves (T135). |

### Fase 1: Consolidación VB.NET → C# (COMPLETADA)
| Archivo | Descripción |
|---|---|
| CLAUDE-FASE1.md | Instrucciones y progreso de Claude Code Fase 1 |
| INVENTARIO.md | Inventario de los 33 proyectos originales |
| CLASIFICACION.md | Clasificación de clases por módulo |
| DEPENDENCIAS.md | Grafo de dependencias entre proyectos |
| PROGRESO.md | Log de 8 lotes de migración |
| MAPA-DE-MIGRACION.md | Namespace viejo → nuevo |
| CONTEXTO-SOLUCION-ORIGINAL.md | Arquitectura completa del SOLIDO original |
| ANALISIS-CLUSTER.md | Análisis del cluster circular |
| REPORTE-FINAL-FASE1.md | Resumen ejecutivo Fase 1 |

### Fase 1 — Feature 002: Identidad central federada (COMPLETADA)
Cooperativas comparten una identidad central (ASP.NET Core Identity + JWT
RS256). Login sin combo box, MFA opt-in y obligatorio por tenant, invitaciones
con token de un solo uso, multi-empresa con switcher y default, admin de
empresa con gestión de membresías, master admin con onboarding atómico y
reset MFA forzado.

| Archivo | Descripción |
|---|---|
| `specs/002-identidad-central-federada/spec.md` | Spec funcional — 5 user stories (US1..US5) + Phase 4b Profile/Recovery. Funcional + non-functional requirements + assumptions. |
| `specs/002-identidad-central-federada/plan.md` | Plan técnico: arquitectura, dependencias, contratos REST, Complexity Tracking. |
| `specs/002-identidad-central-federada/research.md` | Decisiones de diseño: D-01 JWT RS256, D-03 issuer/audience, D-06 single-use con lock, D-07 cache de membresías, D-09 salvaguarda último admin, D-11 lockout progresivo. |
| `specs/002-identidad-central-federada/data-model.md` | Modelo de datos: `ADM_CentralUsers`, `ADM_TenantMemberships`, `ADM_Invitations`, `ADM_TenantMfaPolicies`, `ADM_CentralUserLoginAttempts`, `ADM_PasswordResetTokens`. Relaciones y RowVersion. |
| `specs/002-identidad-central-federada/quickstart.md` | Recorrido end-to-end con casos por user story. |
| `specs/002-identidad-central-federada/tasks.md` | Tareas T001-T128 con estado. |
| `specs/002-identidad-central-federada/contracts/auth.md` | Login/Refresh/Logout/Me + Mfa Verify. |
| `specs/002-identidad-central-federada/contracts/sessions.md` | Select tenant, switch tenant, default tenant. |
| `specs/002-identidad-central-federada/contracts/invitations.md` | Emisión por tenant admin y master, preview público, accept con XOR de 3 ramas, revocar. |
| `specs/002-identidad-central-federada/contracts/profile-and-recovery.md` | Enrollment MFA voluntario y forzado, change password con notification, forgot/reset password. |
| `docs/operaciones/manual-pruebas-identidad-central.md` | **Manual de pruebas técnico paso a paso**: setup del entorno, casos por user story con cURL/PowerShell copy-paste, verificación de audit log y JWT, troubleshooting, smoke test 5min y limpieza para re-pruebas. Audiencia: QA / devs. |
| `docs/operaciones/manual-funcional-usuario-final.md` | **Manual funcional para usuarios finales** (cooperativistas, admins de cooperativa, master admin). Sin jerga técnica — explica recibir invitación, login, MFA, multi-empresa, recuperación de contraseña, gestión de miembros y onboarding de cooperativas con casos de uso reales. |
| `docs/release-notes/002-identidad-central-federada/evidencia-pruebas.md` | **Evidencia de cierre (T127)**: ejecución end-to-end completa (2026-07-26 → 2026-08-01), 9 bugs corregidos con sus commits, 8 capturas de la UI, resultados de T118 (integration) y T124 (carga NBomber) y pendientes al cierre. |
| `docs/operaciones/convertir-manual-a-word.ps1` | Script PowerShell que usa pandoc para convertir ambos manuales `.md` a `.docx` editables en Word/LibreOffice/Google Docs. |

### Fase 1 — Feature 003: Remates de Identidad Central (EN CURSO)
Cierre funcional del feature 002: TenantSwitcher en el header con guardia de
cambios sin guardar, navegación por rol, recovery codes canjeables en el
desafío MFA + regeneración, sesión Web persistente ante F5 (sessionStorage),
adopción de sesión al aceptar invitación, consola de aprobaciones de MFA
reset con listado, limpieza de páginas duplicadas y la deuda de tests de
integración del 002.
| Archivo | Descripción |
|---|---|
| `specs/003-identidad-remates-ui/spec.md` | Spec funcional — 6 user stories (US1..US6), FR-101..FR-119, SC-101..SC-107. |
| `specs/003-identidad-remates-ui/plan.md` | Plan técnico: cero DDL y cero paquetes nuevos; Constitution Check. |
| `specs/003-identidad-remates-ui/research.md` | Decisiones D-01..D-10 (dirty-state, sessionStorage, claims para navegación, recovery codes nativos de Identity). |
| `specs/003-identidad-remates-ui/data-model.md` | Sin entidades nuevas — proyecciones sobre las del 002. |
| `specs/003-identidad-remates-ui/quickstart.md` | Recorridos de prueba por user story. |
| `specs/003-identidad-remates-ui/tasks.md` | Tareas T001-T049 con estado. |
| `specs/003-identidad-remates-ui/contracts/mfa-recovery-codes.md` | Canje one-shot en mfa/verify + regeneración con confirmación de identidad. |
| `specs/003-identidad-remates-ui/contracts/mfa-reset-requests.md` | Listado paginado de solicitudes de reset MFA para el aprobador. |

### Fase 2A: Rediseño de Base de Datos (COMPLETADA)
| Archivo | Descripción |
|---|---|
| PROPUESTA-REDISENO-BD.md | Propuesta de rediseño (270 → 272 tablas) |
| MAPEO-BD-VIEJO-NUEVO.md | Mapeo completo tabla vieja → nueva |

### Fase 2B-2D: Arquitectura y Frontend (COMPLETADA)
| Archivo | Descripción |
|---|---|
| ESTRUCTURA-WEB.md | Diagrama Clean Architecture |

### Fase 2E: Migración de Formularios (COMPLETADA)
| Archivo | Descripción |
|---|---|
| ANALISIS-FORMULARIOS.md | Análisis de 490+ formularios |
| PROGRESO-WEB.md | Progreso P1-P6 |

### Fase 3: Deployment (COMPLETADA)
| Archivo | Descripción |
|---|---|
| REPORTE-FINAL-MIGRACION.md | Resumen completo Fases 1-3 |

## Base de Datos
| Archivo | Ubicación | Descripción |
|---|---|---|
| DBDefinicion.sql | database/ | Esquema original SOLIDO |
| Schema_01 al _12 | database/schema/ | DDL nuevo IngenIA365ERP (272 tablas) |
| Migration_01 al _10 | database/migration/ | Scripts migración de datos |
| identity_tables.sql | database/seed/ | Tablas ASP.NET Identity |

## Totales del Proyecto
- 113 endpoints REST
- 136 páginas Blazor funcionales
- 16 reportes PDF (QuestPDF)
- 382 archivos Application
- 270 entidades de dominio
- 270 EF Core Configurations
- 140 DbSets
- 45 tests automatizados
- 0 errores de compilación
