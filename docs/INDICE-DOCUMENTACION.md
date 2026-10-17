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
| `docs/operaciones/adjuntos-en-s3.md` | **Dónde viven los adjuntos y cómo se llega a ellos.** El bucket `s3://ingenia365-erp-attachments` (aparte del de respaldos, con papelera de 90 días y CORS). Desde la feature 011 el archivo no pasa por el servidor: el navegador sube y baja directo con autorizaciones firmadas. La API accede con credenciales temporales de una hora (IAM Roles Anywhere, en los tres ambientes desde el 2026-09-24), y trae cómo emitir, renovar, probar y revocar los certificados. |
| `docs/operaciones/despliegue-adjuntos-directos.md` | **Cómo se llevó la feature 011 a producción (hecho el 2026-09-24, `release e0c97b9`).** Qué sube (con las correcciones que esperan en develop), por qué conviene entrar directo con credenciales temporales —y desactivar ya la llave filtrada, que no usa nadie—, qué mirar antes (adjuntos en el disco: 0 el 2026-09-23), el orden, la verificación y el rollback. |
| `docs/operaciones/llavero-dataprotection.md` | **Leer antes de desplegar el llavero en base.** Qué cifra DataProtection (segundos factores y clave de cada adjunto) y por qué se perdía en cada rotación de pod. Lleva delante la decisión: si no hay adjuntos que duelan, aceptar la pérdida es la respuesta correcta y **no cuesta ningún paso manual**. El rescate pod a pod es la otra rama, no el camino por defecto. |
| `docs/operaciones/rescate-del-administrador-maestro.md` | **La única cuenta que nadie más puede rescatar.** Por qué las tres vías de recuperación fallan para el maestro, el interruptor de configuración que lo saca de una política de plataforma mal puesta, y el SQL de último recurso — con backup y segundo par de ojos. |
| `docs/operaciones/nomina-primer-periodo.md` | **Nómina (feature 005): antes de la primera liquidación de una cooperativa.** Qué deja la semilla (plan, 40 conceptos, 33 parámetros con vigencia, comprobante `NM`, permisos) y qué no (cuentas por concepto, período contable, afiliaciones); cómo comprobarlo contra la base del ambiente; qué hacer ante `Payroll.LegalParameterMissing`; cómo cargar la vigencia de un año nuevo y reaplicar la semilla. |
| `docs/operaciones/contabilidad-primer-ejercicio.md` | **Contabilidad (feature 009): antes del primer comprobante de una cooperativa.** Qué deja la semilla (dos PUC, 205 rubros, 18 tipos, 8 cruces, 30 permisos), qué hace y exige la migración destructiva `ContabilidadNiif` (guarda, segundo revisor, vacía las cuentas por concepto), el orden: iniciar → auxiliares → vincular entidades → parametrizar nómina → primer `NM` → cierre mensual; y la tabla de síntomas. |
| `docs/operaciones/retirada-de-mfasecret.md` | **Pendiente, no ejecutado.** Cómo vaciar la columna heredada del segundo factor y retirar el modo compatibilidad. Destructiva: exige backup y segundo revisor (Principio XII). El orden es contraintuitivo y está explicado. |

### Fase 1: Consolidación VB.NET → C# (COMPLETADA)
| Archivo | Descripción |
|---|---|
| CLAUDE-FASE1.md | Instrucciones y progreso de Claude Code Fase 1 |
| INVENTARIO.md | Listado de los 33 proyectos VB.NET originales de SOLIDO (inventario de la solución, Fase 1). **No es el módulo de inventario**: ése es la feature 012, más abajo |
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
| `docs/manual/contabilidad-contrato-de-contabilizacion.md` | **Cómo contabiliza un módulo** (feature 009): el único camino al libro es `AccountingPoster`; el molde de Nómina, las doce comprobaciones con su código de error, terceros institucionales, reversión por el módulo dueño y la lista de lo que no se hace (ni `Add` fuera del contrato, ni numerar, ni `Remove`, ni saldos guardados). §8 Inventario por mensajes (012) y §9 **Cartera por hechos** (013, US7): matriz, validación previa, modos y lotes, regla de cierre y comprobantes nuevos en vez de reversión. |
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

### Feature 012: Inventario comercial (EN CURSO — entrega I1 terminada en la rama `012-inventario-comercial`, sin merge ni despliegue)
Módulo comercial nuevo en reemplazo del traslado a medias de SOLIDO: catálogo, bodegas con tránsito,
kardex inmutable y costeo, compras, traslados, conteos y puesta en marcha bodega por bodega (I1); después
integración con Contabilidad por mensajes (I2), ventas y POS (I3), documentos electrónicos DIAN (I4),
compras completas y costeo avanzado (I5) y comercio ampliado (I6). Trae la plataforma de mensajes,
procesos por cooperativa, parámetros con vigencia, aprobaciones, alertas y auditoría encadenada.

| Archivo | Entrega | Descripción |
|---|---|---|
| `specs/012-inventario-comercial/spec.md` | — | Spec — 17 historias, FR-001..FR-101, SC-001..SC-025. |
| `specs/012-inventario-comercial/plan.md` | — | Plan técnico, entregas I1–I6 e IC, Constitution Check. |
| `specs/012-inventario-comercial/decisiones-transversales.md` | — | Nombres canónicos y decisiones T1–T52; manda sobre cualquier otra redacción. Preguntas abiertas al dueño en §4. |
| `specs/012-inventario-comercial/research.md` · `data-model.md` · `quickstart.md` · `tasks.md` | — | Investigación R1–R34, modelo de datos, recorrido de ensayo por entrega y tareas. |
| `specs/012-inventario-comercial/contracts/` | — | `api.md` (rutas, permisos, errores), `mensajes.md` (catálogo de mensajes y estados de entrega), `plantillas.md` (las dieciséis plantillas, única fuente de su orden y columnas), `contabilidad.md`, `dian.md`. |
| `specs/012-inventario-comercial/diagnostico-inventario-heredado.sql` | I1 | Diagnóstico de sólo lectura de las 23 tablas heredadas por base, antes del retiro. |
| `docs/manual/inventario-documentos-y-kardex.md` | I1 | **Cómo se agrega una clase o un tipo de documento** sobre `INV_Documents` y el ciclo común; el flujo canónico de confirmación, el kardex con un solo escritor, el cerrojo en orden fijo con la numeración al final, la precisión de cada número, la idempotencia y la anulación como documento nuevo. |
| `docs/manual/plataforma-de-integracion.md` | I1 | **Receta para que otro módulo use la plataforma**: la bandeja de salida (y lo que falta abrir para que emita otro módulo que Inventario), `IEjecutorEnCooperativa` y el actor «Proceso de integración», tareas programadas, parámetros con vigencia, aprobaciones con montos por permiso y alertas. |
| `docs/manual/plantillas-de-importacion.md` | I1 | **La mecánica común de las plantillas** (encabezados en la fila 1, revisión y aplicación todo o nada, errores por hoja, fila y columna, `ImportResultDto`) y cómo se agrega una plantilla nueva. |
| `docs/operaciones/inventario-retiro-heredado.md` | I1 | **Runbook de la migración destructiva** `RetiroDelInventarioHeredado`: diagnóstico por base, `pg_dump` `*-pre-f012.dump`, la fila de aprobación, el segundo revisor, qué hace la guarda, el `Down()` y el retorno desde el respaldo. |
| `docs/operaciones/inventario-puesta-en-marcha.md` | I1 (el cuadre, I2) | **Runbook de la puesta en marcha**: semillas, plantillas en orden, conteo físico, cifras de SOLIDO, saldo inicial con aprobación, excepción de puesta en marcha, comparativos, marcha paralela y activación bodega por bodega (en producción, sólo con I2). |
| `docs/operaciones/valores-nacionales.md` | 013, fase 2 | **Valores nacionales**: el maestro registra y corrige en el registro central, la plantilla y el paquete entre ambientes, la réplica a cada cooperativa, la proyección de Nómina (`Nomina.ValorNacionalDistinto`), la revisión «pendiente de validar» y los faltantes. |
| `docs/operaciones/canales-externos.md` | 013, fase 2 | **Canales externos**: el Secret `erp-canales-externos` y su guion, `SIMULADO` y `CARGA-PROPIA`, el efecto legal en producción y el circuito. |
| `docs/manual/cartera-gobierno-y-seguridad.md` | 013, fase 4 (K1) | **Gobierno y seguridad de Cartera**: alcance por oficina y línea que falla cerrado, matriz de atribuciones y montos operativos, órganos, sesiones, votos y actas, segregación por persona, vinculados, autorizaciones y auditoría; la cola de mensajes al deudor con la ventana de la Ley 2300, plantillas y el canal simulado; y la preparación para la activación (roles, personas de los usuarios, oficinas vinculadas). |
| `docs/manual/cartera-lineas-de-credito.md` | 013, fase 5 (K1, US1) | **Líneas de crédito y usura**: versiones que no tocan los créditos vivos, la usura contra la efectiva con cargos (plan tipo por banda, seguros que computan, real/360, tasa variable), bandas, seguros, garantías y documentos, el concepto tributario del interés con su soporte (D20), las equivalencias de Comercio, la plantilla `lending.credit-lines` y qué pasa cuando llega una usura nueva (bloqueo, alerta y versión que lo limpia). |
| `docs/manual/cartera-originacion.md` | 013, fase 6 (K1, US2) | **Originación**: simular sin registrar nada, radicar con documentos, autorizaciones y la preferencia sellada en la solicitud, qué valida el sistema y lo que no admite excepción (capacidad, autorización, listas, usura), el nivel efectivo sobre el monto consolidado, decidir individual o con acta (vigencia de la aprobación), cupos y disponible, bloqueos y embargos, grupos conectados, la liberación de listas por el oficial y por qué en producción una consulta simulada a centrales no sirve para aprobar (T73). |
| `docs/manual/cartera-desembolso-y-garantias.md` | 013, fase 7 (K1, US3) | **Desembolso, garantías y pagaré**: garantías y su firmeza, pagaré físico o desmaterializado y por qué en producción el depósito simulado nunca perfecciona (T73), los requisitos que impiden desembolsar (usura del día, aprobación vigente, segregación), tramos que regeneran el plan, recogida y reestructuración, la orden de giro con `PaidByFile` y el archivo de desembolsos, el resultado del giro con su reverso desde el origen, el giro nuevo y la anulación del crédito pendiente de giro, y la tesorería de prueba. |
| `docs/manual/cartera-causacion-y-liquidacion.md` | 013, fase 8 (K1, US4) | **Causación diaria y liquidación**, para la contadora: convenciones de días, equivalencia de tasas y tope de usura; la causación del día (d − 1, d] por concepto y cuota, la puesta al día de los días sin correr, el día no hábil, la suspensión y el reajuste; las partes que llegan a Contabilidad; la liquidación a (D, R) reproducible con su huella y su explicación; la corrección por valor nacional; la verificación del libro (`Cartera.LibroDescuadrado`) y cómo leer un caso dorado. |
| `docs/operaciones/cartera-proceso-diario.md` | 013, fase 8 (K1, US4) | **El proceso diario de Cartera**: opciones `Lending:Process` y el arrendamiento `lending.process`, tandas con cerrojo que salta lo ocupado, ver, ordenar y reintentar corridas, un día sin causar y las alertas de `cartera.vigilancia`, la corrección por valor nacional, `POST /integrity/verify` y el volumen (50.000 créditos en menos de 30 minutos, partición por año al pasar de 20.000, D13). |
| `docs/manual/cartera-pagos-y-contrato-de-obligaciones.md` | 013, fase 9 (K1, US5) | **Pagos y contrato de obligaciones**: por qué Cartera no aplica pagos por pantalla, las cinco operaciones del contrato y sus respuestas, las formas interactiva y no interactiva, las siete modalidades y la prelación, anticipos y saldos a favor con su resolución, la preferencia del deudor (Ley 2032), fecha valor (`Pagos.DiasMaximosFechaValor`, Nómina exenta) y atribución antes de un cierre, firmeza por clase de medio, el reverso con pagos posteriores y quién reversa qué, Nómina y sus no descontados, qué responde antes de activar y la auditoría por `?operationKey=` (intento y hecho, «Revertido con el recaudador»). |
| `docs/operaciones/cartera-dobles-de-prueba.md` | 013, fases 7 y 9 (K1, US3 y US5) | **Los dobles de prueba de Cartera** (tesorería y recaudador): cómo se habilitan (`Lending:TestDoubles:Enabled` y las filas `TST`/`TES` en `/admin/participantes-de-integracion`), por qué no existen en producción y la API no arranca si se piden allí, las rutas `/api/lending/test-doubles/*`, la pantalla y los problemas frecuentes. |
| `docs/manual/cartera-ventas-a-credito.md` | 013, fase 10 (K1, US6) | **Ventas a crédito de Comercial**: el cupo rotativo y la consulta de cupo y estado (sin escribir listas), la reserva del número dentro de la confirmación y por qué puede no haberla, cómo nace la obligación de una venta (siempre, con desembolso en especie), la validación fallida que bloquea sin anular la venta y su confirmación por excepción, notas, anulaciones y reemplazos, ventas hasta el corte, mensajes no reprocesables y lo que Comercial guarda (número y estado, nunca saldos). |
| `docs/operaciones/cartera-ventas-a-credito.md` | 013, fase 10 (K1, US6) | **Ventas a crédito en operación**: qué igualar en los parámetros de Comercial y en Cartera al activar (`Cartera.IntegracionHabilitadaDesde`, línea comercial, equivalencias, cupos), el orden de cerrojos de la reserva dentro del POS (C24, G6), la prueba de volumen y los problemas frecuentes. |
| `docs/manual/cartera-integracion-contable.md` | 013, fase 11 (K1, US7) | **Integración contable de Cartera**, para la contadora: Cartera no conoce cuentas y Contabilidad contabiliza sus hechos con la matriz `ACC_LendingPostingRules`; roles, exigidas y pesos (Línea 512 … Hecho 2 sólo en resultados; el centro se asigna), cómo diligenciar la plantilla desde las filas sugeridas, tipos de comprobante, los quince asientos de ejemplo por rol, qué hacer con un rechazo (bandeja, reproceso, quién corrige), la conciliación por conjuntos de cuentas, el cierre de mes con Cartera, los indicadores regulatorios y lo pendiente de validar (C2, C4, C6, C7, C8, C10, C77, C81 y la cancelación de la orden del suspenso capitalizado). |
| `docs/manual/cartera-riesgo-y-cierre-mensual.md` | 013, fase 12 (K1, US9) | **Calificación, deterioro y cierre mensual**: la informativa (no se guarda) y la oficial del último cierre; candidatas, arrastre por deudor y modalidad SARC, subcategorías y categoría contable; el deterioro individual del Anexo 1 (sin aportes mientras rija D19) y el general, en delta y frente al base en el primer cierre; la calificación por análisis (sólo peor); la política SARC del consejo (sólo endurece, se valida con acta); el cierre y sus hechos (reclasificación, deterioro, PV), el reporte de cambios, las marcas «pendiente de validar» y «modelo obligatorio no disponible», la reapertura con versión siguiente, el pago con fecha valor de un mes cerrado y lo que valida la contadora (C2, C4, E8, E9). |
| `docs/operaciones/cartera-cierre-mensual.md` | 013, fase 12 (K1, US9) | **El cierre mensual en operación**: lo que se prepara antes (indicadores, matriz, análisis), los requisitos y su código, ordenar y seguir la corrida, reintentar, cuadrar con Contabilidad, cerrar el período contable después del de Cartera, reabrir antes del contable y los problemas frecuentes. |
| `docs/operaciones/cartera-puesta-en-marcha.md` | 013, fase 13 (K1, US8) | **La puesta en marcha de Cartera**: lo que debe existir antes (retiro, personas, valores nacionales y Anexo 1, perfil, roles, alcances, órganos, oficinas vinculadas, apertura contable, matriz, `RecaudoPorAplicar`, `DESC_CARTERA`, patrimonio técnico, Recaudo y Tesorería reales); el orden de las plantillas; la carga por partes con varios archivos y la carga de ensayo sin activar (su efecto en Nómina, Personas y el cierre contable); cuadre, conciliación y aceptación con acta; cobros provisionales; la activación en C+1 con el plazo, el mes contable y el acta (qué declara), y su verificación con SQL en los dos motores; lo que Comercial ajusta el mismo día; los primeros días y el primer cierre contra el base; qué hacer si falla. |
| `docs/manual/plantillas-de-importacion.md` §5a | 013, fase 13 (K1, US8) | **Las plantillas de Cartera**: las 17 partes de la migración sobre la ruta de la carga, varios archivos por parte (tandas, `replace`), los cobros provisionales que sólo se revisan y el orden de carga, remitiendo a `specs/013-cartera-financiera/contracts/plantillas.md` §5–§6. |
| `docs/manual/cartera-documentos-al-deudor.md` | 013, fase 14 (K1, US13) | **Documentos al deudor**: plan de pagos, paz y salvo de una obligación o general y certificado de saldo emitidos en un paso con número, código de verificación y QR; cuándo se niega el paz y salvo (saldo, venta en reproceso) y la mención de las ventas fuera de Cartera; la firmeza por clase de medio; quién verifica (sólo usuarios de la cooperativa, D21); reimprimir el mismo PDF, enviar por correo, anulación manual y automática al reversar; firmante, logotipo e imagen de la firma; lo que llega con K5. |
| `docs/manual/cartera-novedades-y-condonaciones.md` | 013, fase 16 (K3, US11) | **Novedades, condonaciones y eventos del deudor**: causa calculada (reestructuración o modificación) y mecanismo; simular, consentimientos de codeudores y garantes con su aceptación firmada, radicar, decidir y aplicar con las dos salidas de quien no consintió y la excepción de cobertura; crédito enlazado en novación y refinanciación; condonaciones con su enrutamiento y el deterioro; fallecimiento con la reclamación al seguro, retiro, embargo e insolvencia (acuerdo como novedad, fracaso); lo pendiente del dueño. |
| `docs/manual/cartera-castigo-y-evaluacion.md` | 013, fase 17 (K3, US14) | **Castigo y evaluación de cartera**: requisitos de la norma (ya no es asociado, aportes cruzados, informe motivado, concepto de irrecuperabilidad) y de la política (`Castigo.*`); armar el lote con sus soportes, acta del consejo, aplicar (`CreditoCastigado` en `PV`, causación en control desde el día siguiente), reporte al supervisor con su plazo, recuperaciones y la vista `charge-offs`; evaluación de cartera (preparar, recomendación del comité de riesgos, aprobación del consejo, aplicar) y su efecto desde el cierre del mes de efecto; lo pendiente del dueño. |
| `docs/manual/cartera-cobranza-y-aviso-previo.md` | 013, fase 18 (K4, US12) | **Cobranza, aviso previo y reclamos**: etapas versionadas y el paso 7 del ciclo, reglas de reparto y agenda del gestor, gestiones inmutables con la ventana de la Ley 2300 (el contacto directo fija el canal de la semana), promesas y acuerdos de pago sólo sobre lo vencido, gastos de cobranza que se informan antes de causarse, cobro jurídico, campañas con constancia de los excluidos, aviso previo por episodio (dos comunicaciones con saldo hasta el umbral; `SIMULADO` no es constancia en producción), reclamos con aceptación tácita y lo pendiente de validar. |
| `docs/manual/cartera-informes-y-tablero.md` | 013, fase 20 (K5, US15) | **Informes de gestión y tablero**: el centro de informes por entrega (filtros, datos del deudor, permisos de C84 y la auditoría de cada exportación), las definiciones de calidad, cosechas y concentración **a validar por la contadora**, `Informes.DiasCarteraVencida` y las nueve fichas del tablero por oficina y línea dentro del alcance. |
| `docs/operaciones/cartera-despliegue-k4.md` | 013, fase 18 (K4, US12) | **Desplegar K4**: la semilla de etapas al arrancar (`CollectionStagesSeeder`, Order 92), la corrida que registra la etapa migrada (`MigratedCollectionStages`, tarea de una sola vez `cartera.etapas-migradas-k4` u orden manual): cómo dispararla, verla terminar, repetirla sin duplicar y leer el evento resumen con la tabla de equivalencia; SMS y WhatsApp por cooperativa (sin adaptador real todavía) y la validación de las etapas sembradas. |
| `docs/operaciones/cartera-sicses-y-centrales.md` | 013, fase 21 (K5, US16) | **El formato SICSES de cartera y los archivos para centrales**: antes de desplegar (`CarteraInformes`, permisos, `Centrales.CodigosDeSuscriptor`, plazo por nivel), generar y cargar el SICSES de un mes y el archivo de una central, la marca D28 que nunca va en el archivo, qué hacer con cada motivo de exclusión, cotejar un layout (versión nueva del JSON con `verified`, exige despliegue) y regenerar los casos dorados con `CARTERA_ESCRIBIR_ESPERADO=1`. |
| `docs/operaciones/auditoria-cadena-de-sellos.md` | I1 | **Auditoría con entrega garantizada**: `COR_AuditOutbox`, cabezas y anclas, el reenviador, la verificación de integridad, qué hacer ante un hallazgo y cómo rotar la clave de anclas. |

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
