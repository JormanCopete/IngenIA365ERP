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
| `.specify/memory/constitution.md` | Constitución del proyecto — doce principios vinculantes (Spec-First, Clean Architecture, CQRS + MediatR, Multi-tenancy, Person centralizada, PublicId, Soft-delete + auditoría, Validación dual, Errores visibles, SIPLA/SARLAFT, Inmutabilidad contable, Migraciones idempotentes). **v2.0.0** — Ratificada 2026-05-03, enmendada 2026-08-22 y corregida 2026-08-25: el Principio IV pasó de esquema por cooperativa a **una base de datos por cooperativa**, con alcance en SQL, MongoDB y Redis. |
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
| `docs/operaciones/despliegue-rediseno-mfa.md` | **El documento a tener delante el día del despliegue.** Producción no tiene nada del rediseño del segundo factor: son 9 migraciones administrativas, una de datos y otra que mueve el llavero. Lleva delante las tres consultas que hay que correr antes, las dos variables sin las cuales la API no arranca, el orden, y por qué el rollback es volver la imagen dejando la base adelantada. |
| `docs/operaciones/adjuntos-en-s3.md` | **Dónde viven los adjuntos y cómo se llega a ellos.** El bucket `s3://ingenia365-erp-attachments` (aparte del de respaldos, con papelera de 90 días y CORS). Desde la feature 011 el archivo no pasa por el servidor: el navegador sube y baja directo con autorizaciones firmadas. La API accede con credenciales temporales de una hora (IAM Roles Anywhere, activo en DEV y QA), y trae cómo emitir, renovar, probar y revocar los certificados. |
| `docs/operaciones/llavero-dataprotection.md` | **Leer antes de desplegar el llavero en base.** Qué cifra DataProtection (segundos factores y clave de cada adjunto) y por qué se perdía en cada rotación de pod. Lleva delante la decisión: si no hay adjuntos que duelan, aceptar la pérdida es la respuesta correcta y **no cuesta ningún paso manual**. El rescate pod a pod es la otra rama, no el camino por defecto. |
| `docs/operaciones/rescate-del-administrador-maestro.md` | **La única cuenta que nadie más puede rescatar.** Por qué las tres vías de recuperación fallan para el maestro, el interruptor de configuración que lo saca de una política de plataforma mal puesta, y el SQL de último recurso — con backup y segundo par de ojos. |
| `docs/operaciones/nomina-primer-periodo.md` | **Nómina (feature 005): antes de la primera liquidación de una cooperativa.** Qué deja la semilla (plan, 40 conceptos, 33 parámetros con vigencia, comprobante `NM`, permisos) y qué no (cuentas por concepto, período contable, afiliaciones); cómo comprobarlo contra la base del ambiente; qué hacer ante `Payroll.LegalParameterMissing`; cómo cargar la vigencia de un año nuevo y reaplicar la semilla. |
| `docs/operaciones/contabilidad-primer-ejercicio.md` | **Contabilidad (feature 009): antes del primer comprobante de una cooperativa.** Qué deja la semilla (dos PUC, 205 rubros, 18 tipos, 8 cruces, 30 permisos), qué hace y exige la migración destructiva `ContabilidadNiif` (guarda, segundo revisor, vacía las cuentas por concepto), el orden: iniciar → auxiliares → vincular entidades → parametrizar nómina → primer `NM` → cierre mensual; y la tabla de síntomas. |
| `docs/operaciones/retirada-de-mfasecret.md` | **Pendiente, no ejecutado.** Cómo vaciar la columna heredada del segundo factor y retirar el modo compatibilidad. Destructiva: exige backup y segundo revisor (Principio XII). El orden es contraintuitivo y está explicado. |

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
| `docs/manual/indicador-de-carga.md` | **Cómo decir «cargando…» en una pantalla**: el componente `IndicadorDeCarga` + `EstadoDeCarga`, la receta de cuatro pasos, las reglas (una zona por cosa, texto que dice qué pasa, errores visibles) y cómo se aplicó a Core y Nómina para repetirlo en los demás módulos. |
| `docs/manual/alta-de-persona-desde-modulos.md` | **Cómo un módulo registra persona y rol en un paso** (feature 008): las tres reglas (la persona se escribe en un solo sitio, las banderas derivadas las pone el servidor, toda ruta exige permiso), las piezas compartidas (`PersonaDialog`, `PersonaCampos`, `PersonSearchPicker`, `PermisosDelUsuario`) y la receta para sumar Vendedores, Proveedores, Clientes y Terceros. |
| `docs/manual/liquidaciones-especiales.md` | **Prima, cesantías anuales, vacaciones y liquidación definitiva** (feature 010, N1): el ciclo común (calcular con explicación → excluidos con razón → aprobar contra la provisión al corte → relación de pago → reversar), qué hace cada una, los descuentos de Cartera en la definitiva, un solo pagador del último tramo, políticas por empresa, festivos, saldos iniciales, permisos por tipo y cómo se prueba (casos dorados, e2e). |
| `docs/manual/dispersion-bancaria.md` | **Archivos planos de pago por banco** (feature 010, N4): qué tener antes (código ACH de cada banco, cuenta bancaria del plan, empresa, ficha con cuenta), generar → vista previa → descargar → marcar enviado (todos pagados en una transacción) → pendientes y anulación, cómo cargar el formato de un banco como dato en Maestros › Formatos bancarios (orígenes, `map`, montos, vigencias y versiones), qué preguntarle al banco, y por qué el formato vive en Core para cesantías, tesorería y contabilidad. |
| `docs/manual/retencion-procedimiento-2.md` | **Porcentaje fijo de retención del procedimiento 2** (feature 010, N2): los doce meses anteriores con la prima y sin cesantías, la depuración en la secuencia de la política, el divisor (13 o los meses de vinculación), la tabla vigente o la del plan, aprobar cerrando la vigencia anterior, y cómo se prueba con casos dorados. |
| `docs/operaciones/pila-primera-planilla.md` | **Runbook de la primera planilla PILA** (feature 010, N2): qué tener por cooperativa (empresa, datos del aportante, códigos PILA de las administradoras, fichas, parámetros), cómo cotejar el layout con el anexo v30 y una planilla pagada, validar → generar → cuadrar (la diferencia de redondeo de la ARL es esperable) → cargar en Aportes en Línea → marcar cargada, qué hacer con cada inconsistencia y el cambio de abril de 2027. |
| `docs/manual/contabilidad-contrato-de-contabilizacion.md` | **Cómo contabiliza un módulo** (feature 009): el único camino al libro es `AccountingPoster`; el molde de Nómina, las doce comprobaciones con su código de error, terceros institucionales, reversión por el módulo dueño y la lista de lo que no se hace (ni `Add` fuera del contrato, ni numerar, ni `Remove`, ni saldos guardados). |
| `docs/manual/README.md` | **Manual del sistema dentro de la aplicación** (`/manual` y el botón «¿Cómo se hace?» de la barra superior): una guía por opción del menú, buscable, con modo guiado paso a paso y enlace a cada pantalla. El documento explica cómo está hecho, cómo agregar o corregir una guía y la convención de capturas (`wwwroot/img/manual/<slug>/paso-<n>.png`). |
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
| `docs/operaciones/manual-pruebas-funcional-remates-identidad.md` | **Manual de pruebas funcional del feature 003**: 7 pruebas completas de principio a fin (switcher, navegación por rol, recovery codes, sesión ante F5, invitación E2E, reset de MFA, smoke de regresión) con escenarios felices, negativos y de borde, actores, tabla de credenciales viva y registro de resultados. Audiencia: QA funcional. |
| `docs/release-notes/003-identidad-remates-ui/evidencia-pruebas.md` | Evidencia de cierre del 003: suites automatizadas, recorrido T047 con capturas y bugs corregidos en la corrida. |

### Fase 1 — Feature 004: Multi-Motor de Base de Datos (EN CURSO)
PostgreSQL o SQL Server por configuración (sección `Database`), misma build sin
recompilar: migraciones EF por proveedor como fuente única de verdad (el corpus
DDL de `database/schema|migration` quedó CONGELADO), inicializador de arranque
con reintentos y lock nativo, framework de seeding paramétrico/demo, CLI
`DbMigrator migrate/seed/script` y endpoint master `POST /api/saas/database/seed`.
| Archivo | Descripción |
|---|---|
| `specs/004-multi-motor-bd/spec.md` | Spec — 5 user stories, FR-001..FR-025, SC-001..SC-008, 5 clarifications. |
| `specs/004-multi-motor-bd/plan.md` | Plan técnico: Constitution Check 12/12, estructura, Complexity Tracking (transición del principio XII). |
| `specs/004-multi-motor-bd/research.md` | Decisiones D-01..D-13 + resultado del spike de schema-per-tenant. |
| `specs/004-multi-motor-bd/contracts/` | Configuración `Database`, endpoints `saas/database/*` y CLI. |
| `docs/operaciones/ci-multi-motor.md` | Matriz de CI por proveedor y gate de paridad de migraciones. |
| `docs/operaciones/limpieza-datos-demo.md` | Procedimiento para depurar datos `system:seed-demo`. |
| `database/schema/README-CONGELADO.md` | Declaración de congelamiento del corpus DDL histórico. |

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

## Operaciones e Infraestructura

| Archivo | Ubicación | Descripción |
|---|---|---|
| **estado-y-pendientes.md** | docs/operaciones/ | **Estado de los tres ambientes y lista de pendientes priorizada. Empezar por acá.** |
| despliegue-infraestructura.md | docs/operaciones/ | Diseño de la infraestructura y bitácora de instalación |
| migracion-dns-cloudflare.md | docs/operaciones/ | Guía paso a paso de la migración de DNS |
| politica-iam-respaldos.json | docs/operaciones/ | Política IAM del usuario de respaldos (permisos mínimos) |
| politica-iam-adjuntos.json | docs/operaciones/ | Política **transitoria** del usuario IAM de adjuntos, hasta que las credenciales temporales lleguen a producción: borra objetos pero no versiones, no lista y tiene negado el bucket de respaldos. Se retira con el usuario (T075) |
| plantillas/adjuntos-roles-anywhere.yaml | docs/operaciones/ | Pila de CloudFormation de IAM Roles Anywhere: trust anchor con la CA propia, perfil de una hora y un rol por ambiente acotado a su prefijo (feature 011) |

Los manifiestos de Kubernetes, el diseño de respaldos (`backups.md`) y el manual
operativo de MongoDB (`mongo-replica-set.md`) viven en el repositorio **privado**
`ingenia365-gitops`.

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
