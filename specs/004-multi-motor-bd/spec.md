# Feature Specification: Soporte Multi-Motor de Base de Datos (PostgreSQL / SQL Server)

**Feature Branch**: `004-multi-motor-bd`

**Created**: 2026-08-03

**Status**: Draft

**Input**: User description: "Soporte multi-motor de base de datos (PostgreSQL / SQL Server) para IngenIA365ERP. El sistema tendrá dos modalidades de despliegue: (1) SaaS/Web en la nube usando PostgreSQL como motor, y (2) on-premise en servidor del cliente donde el cliente elige entre PostgreSQL o SQL Server. Selección de motor por configuración sin cambios de código, validada al arranque con fail-fast; migraciones por proveedor; migraciones automáticas resilientes al arranque con protección para producción; sistema de seeding paramétrico (obligatorio, idempotente) y de pruebas/demo (configurable por ambiente); entorno local con ambos motores para verificación de paridad."

## Clarifications

### Session 2026-08-03

- Q: ¿Cuál es la estrategia para la fuente de verdad del esquema (destino del corpus DDL T-SQL existente)? → A: No existe ninguna implementación/instalación actual que preservar — se comienza desde cero: las migraciones generadas desde el modelo de datos de la aplicación son la única fuente de verdad para ambos motores; el corpus DDL T-SQL queda congelado como referencia histórica; no se requiere baseline para instalaciones existentes.
- Q: ¿Qué alcance de verificación de paridad exige la v1? → A: Esquema completo aprovisionable en ambos motores desde el día uno; la verificación funcional profunda (suite de integración al 100 % en ambos motores) se concentra en los módulos que hoy tienen suites (identidad central, admin, seguridad, núcleo); los demás módulos quedan aprovisionados con smoke tests y elevan su nivel de verificación en features posteriores.
- Q: ¿Qué hace la aplicación con `AutoMigrate = false` y migraciones pendientes? → A: Fail-fast — la aplicación se niega a arrancar; el error enumera las migraciones pendientes y sugiere aplicar los scripts del DBA o habilitar `AutoMigrate`.
- Q: ¿Sobre qué alcance corre el seed paramétrico en cada arranque? → A: BD administrativa + todos los esquemas de tenant existentes (verificación/completado idempotente en cada arranque); el alta de un tenant nuevo siembra su esquema al crearlo.
- Q: ¿Qué motor es el default en desarrollo local y el primario en CI? → A: PostgreSQL (paridad dev↔producción SaaS); SQL Server queda verificado por la suite dual y disponible en la orquestación local para reproducir escenarios on-premise.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Despliegue con el motor elegido por configuración (Priority: P1)

Un operador de despliegue (el equipo SaaS de IngenIA en la nube, o el administrador TI de un cliente on-premise) instala el ERP indicando en la configuración qué motor de base de datos usar — PostgreSQL o SQL Server — y con qué cadenas de conexión. La aplicación arranca y opera completa con el motor seleccionado, sin recompilar ni tocar código. Si la configuración es inválida (motor desconocido, cadena de conexión faltante para el motor elegido), la aplicación se niega a arrancar con un mensaje que indica exactamente qué corregir.

**Why this priority**: es la capacidad que define el feature — sin selección de motor operativa no existe modalidad on-premise con SQL Server ni modalidad SaaS con PostgreSQL. Todo lo demás (migraciones, seeds) depende de que el sistema sepa contra qué motor trabaja.

**Independent Test**: desplegar la misma build dos veces en limpio — una configurada con PostgreSQL y otra con SQL Server — y verificar que ambas instancias arrancan, sirven el login central y operan los flujos básicos. Arrancar una tercera vez con `Provider` inválido y verificar que el proceso termina con error explicativo sin quedar a medio arrancar.

**Acceptance Scenarios**:

1. **Given** una instalación limpia configurada con `Provider = "PostgreSQL"` y cadena de conexión válida, **When** la aplicación arranca, **Then** todas las operaciones de datos (identidad central, datos de tenant, listados, escrituras) funcionan contra PostgreSQL sin cambios de código.
2. **Given** la misma build configurada con `Provider = "SqlServer"`, **When** la aplicación arranca, **Then** el sistema opera completo contra SQL Server con el mismo comportamiento funcional.
3. **Given** una configuración con `Provider = "Oracle"` (valor no soportado), **When** la aplicación intenta arrancar, **Then** el arranque falla inmediatamente con un mensaje que enumera los valores válidos, y ningún componente queda escuchando peticiones.
4. **Given** una configuración con `Provider = "PostgreSQL"` pero sin cadena de conexión para PostgreSQL, **When** la aplicación intenta arrancar, **Then** el arranque falla con un mensaje que identifica la cadena faltante (sin exigir la cadena del motor NO seleccionado).
5. **Given** una instalación operando con un motor, **When** el operador cambia el `Provider` por configuración y reinicia, **Then** el sistema apunta al otro motor sin recompilación (la coherencia de los datos entre motores es responsabilidad del operador; el sistema no migra datos entre motores).

---

### User Story 2 - Aprovisionamiento automático y seguro del esquema (Priority: P1)

Al arrancar con la migración automática habilitada, la aplicación lleva la estructura de las bases de datos (la BD administrativa central y los esquemas de tenant) al nivel requerido por la versión desplegada, sobre el motor configurado. Si la base de datos aún no está disponible (arranque en contenedores, orden de arranque no garantizado), la aplicación reintenta durante una ventana razonable antes de rendirse con error claro. En producción, el operador puede deshabilitar la migración automática y, en su lugar, obtener scripts SQL idempotentes por motor para que un DBA los revise y aplique manualmente.

**Why this priority**: sin aprovisionamiento del esquema en ambos motores no hay instalación posible en PostgreSQL — hoy todo el corpus de esquema existe solo para SQL Server. Es la segunda mitad inseparable del P1.

**Independent Test**: partir de un servidor PostgreSQL vacío, arrancar la aplicación con migración automática habilitada y verificar que el esquema completo (administrativo + tenant) queda creado y el sistema operativo; repetir en SQL Server vacío. Arrancar con la base de datos apagada y verificar los reintentos y la recuperación al encenderla. Generar los scripts para DBA y verificar que aplicados a mano producen el mismo esquema.

**Acceptance Scenarios**:

1. **Given** un motor limpio (sin bases de datos del producto) y migración automática habilitada, **When** la aplicación arranca, **Then** el esquema completo se crea en el motor configurado y el sistema queda operativo (login del master admin posible) sin intervención manual.
2. **Given** una instalación existente con esquema de una versión anterior, **When** arranca una versión nueva con migración automática habilitada, **Then** solo se aplican los cambios pendientes, quedan registrados en el log cuáles se aplicaron, y los datos existentes se conservan.
3. **Given** la base de datos no disponible al momento del arranque, **When** la aplicación arranca, **Then** reintenta la conexión con espera progresiva durante una ventana configurable; si la BD aparece dentro de la ventana, continúa normalmente; si no, termina con un error que distingue "BD inaccesible" de "migración fallida".
4. **Given** un ambiente de producción con migración automática deshabilitada y migraciones pendientes, **When** la aplicación arranca, **Then** el sistema lo detecta, lo reporta con claridad (qué migraciones faltan) y NO modifica el esquema por su cuenta.
5. **Given** un operador que necesita entregar cambios a un DBA, **When** solicita los scripts de migración, **Then** obtiene scripts SQL idempotentes específicos del motor configurado, aplicables múltiples veces sin error.
6. **Given** varias instancias de la aplicación arrancando simultáneamente contra la misma base de datos (escalado horizontal), **When** ambas intentan migrar, **Then** solo una aplica los cambios y las demás esperan o continúan sin corromper el esquema.

---

### User Story 3 - Datos maestros garantizados en todo ambiente (seed paramétrico) (Priority: P2)

En cualquier ambiente — desarrollo, QA o producción — y sobre cualquiera de los dos motores, el sistema garantiza que los datos maestros necesarios para operar existen: roles, permisos, monedas, tipos de documento, parámetros del sistema y plan de cuentas base. Este seeding es idempotente (ejecutarlo N veces no duplica nada), respeta el orden de dependencias, y puede correr tanto al arranque como bajo demanda.

**Why this priority**: una instalación con esquema pero sin datos maestros no es operable (no hay roles que asignar, ni monedas que usar, ni plan de cuentas donde asentar). Es el paso que convierte "esquema creado" en "sistema utilizable". Depende de US1/US2 pero es previa a cualquier uso real.

**Independent Test**: sobre una instalación recién aprovisionada en cada motor, ejecutar el seed paramétrico y verificar que los catálogos maestros quedan completos; re-ejecutarlo dos veces más y verificar cero duplicados y cero errores; borrar una fila maestra y re-ejecutar, verificando que se restaura.

**Acceptance Scenarios**:

1. **Given** una instalación con esquema recién creado y `RunParametricSeed` habilitado, **When** la aplicación arranca, **Then** los catálogos maestros (roles, permisos, monedas, tipos de documento, parámetros, plan de cuentas base) quedan poblados y el sistema es operable.
2. **Given** una instalación ya sembrada, **When** el seed paramétrico se ejecuta de nuevo (arranque siguiente o bajo demanda), **Then** no se crean duplicados, los registros existentes modificados por el cliente no se pisan, y los maestros faltantes se completan.
3. **Given** un fallo a mitad del seeding (p. ej. caída de la BD), **When** el proceso se interrumpe, **Then** no quedan datos maestros a medias que dejen al sistema en estado inconsistente, y una re-ejecución posterior completa lo pendiente.
4. **Given** un administrador con privilegios de plataforma, **When** invoca el seeding bajo demanda (por comando o por endpoint administrativo protegido), **Then** el seed corre con el mismo comportamiento idempotente y queda registrado en auditoría quién lo ejecutó y cuándo.

---

### User Story 4 - Datos de demostración controlados por ambiente (seed de pruebas) (Priority: P2)

En desarrollo y QA, el sistema puede poblarse con datos de ejemplo (clientes, productos, facturas, movimientos) para probar y demostrar sin cargar datos a mano. En producción, esos datos demo están deshabilitados por defecto y solo se cargan si el operador lo activa explícitamente por configuración (caso típico: un ambiente de capacitación del cliente).

**Why this priority**: acelera el ciclo de desarrollo/QA y habilita ambientes de capacitación, pero el sistema opera sin ella. El riesgo que mitiga (datos demo contaminando producción) exige la salvaguarda del opt-in explícito.

**Independent Test**: en un ambiente de desarrollo, verificar que los datos demo se cargan por defecto; en una configuración de producción, verificar que NO se cargan; activar el flag explícito en producción y verificar que entonces sí; re-ejecutar y verificar idempotencia.

**Acceptance Scenarios**:

1. **Given** un ambiente de desarrollo o QA con la configuración por defecto, **When** la aplicación arranca tras el seed paramétrico, **Then** los datos demo quedan cargados en orden de dependencias (maestros antes que transaccionales).
2. **Given** un ambiente de producción con la configuración por defecto, **When** la aplicación arranca, **Then** NO se carga ningún dato demo, y el log deja constancia de que el seed de pruebas fue omitido por política de ambiente.
3. **Given** un ambiente de producción donde el operador activó explícitamente `RunTestSeed`, **When** la aplicación arranca, **Then** los datos demo se cargan y la activación queda registrada en auditoría.
4. **Given** datos demo ya cargados, **When** el seed de pruebas se re-ejecuta, **Then** no se duplican registros.

---

### User Story 5 - Paridad funcional verificable entre motores (Priority: P3)

Un desarrollador o QA puede levantar localmente ambos motores (con la herramienta de orquestación local del proyecto), ejecutar la suite de pruebas contra cada uno, y confirmar que el comportamiento funcional es idéntico. Las diferencias entre motores (tipos de datos, generación de identificadores, ordenamientos/collations, funciones de fecha, índices) están resueltas de forma centralizada, no dispersas por el código.

**Why this priority**: es la red de seguridad que evita que "soportamos dos motores" degrade a "funciona en el motor que probamos". Sin verificación de paridad, cada release arriesga regresiones silenciosas en el motor menos usado.

**Independent Test**: con la orquestación local, levantar PostgreSQL y SQL Server simultáneamente, correr la suite de integración dos veces (una por motor) y verificar 100 % de éxito en ambas; revisar que las diferencias por motor viven en un único lugar identificable de la base de código.

**Acceptance Scenarios**:

1. **Given** el entorno local del proyecto, **When** el desarrollador levanta la orquestación de ambos motores, **Then** ambos quedan disponibles en puertos distintos y la aplicación puede apuntarse a cualquiera cambiando solo configuración.
2. **Given** la suite de pruebas de integración existente (identidad central, admin, seguridad, núcleo), **When** se ejecuta contra cada motor, **Then** el 100 % de las pruebas pasa en ambos con los mismos resultados funcionales; los módulos sin suite cuentan al menos con verificación de aprovisionamiento (smoke) en ambos motores.
3. **Given** una nueva entidad o cambio de esquema introducido por un desarrollador, **When** genera los artefactos de migración, **Then** el proceso documentado produce los artefactos para AMBOS motores y la omisión de uno es detectable (falla la verificación de paridad).

---

### Edge Cases

- **Apuntar el `Provider` a un motor donde existe una BD creada por el otro motor** (p. ej. cambiar a PostgreSQL contra un esquema restaurado de SQL Server): el sistema debe detectar el historial de migraciones incompatible y fallar con mensaje claro, nunca "reparar" ni mezclar.
- **Migración automática habilitada en dos instancias simultáneas** (Kubernetes/replicas): una sola aplica; sin corrupción ni deadlocks permanentes.
- **BD disponible pero credenciales sin permisos de DDL**: el intento de migrar falla con un error que distingue "sin permisos" de "migración corrupta", y sugiere el modo scripts-para-DBA.
- **`RunTestSeed = true` accidental en producción con datos reales**: los datos demo se insertan bajo identificadores/marcas reconocibles, y la activación queda auditada con actor y timestamp — el incidente es rastreable y los datos demo identificables para limpieza.
- **Seed paramétrico contra un esquema desactualizado** (migraciones pendientes con AutoMigrate=false): el seeding no debe correr sobre un esquema viejo; se omite con error claro que indica aplicar migraciones primero.
- **Diferencias de collation/case-sensitivity**: búsquedas y unicidad que hoy son case-insensitive en SQL Server deben comportarse igual en PostgreSQL (emails, códigos, identificadores de negocio).
- **Aprovisionamiento de un tenant nuevo en runtime** (registro de cooperativa por el master admin): la creación del esquema del tenant debe funcionar en el motor activo, no solo en SQL Server.
- **Interrupción a mitad de una migración larga**: al re-arrancar, el sistema continúa o reporta el estado con precisión; nunca queda un esquema a medias sin diagnóstico.
- **Cadenas de conexión con secretos**: las cadenas nunca aparecen completas en logs ni mensajes de error (se enmascaran credenciales).

## Requirements *(mandatory)*

### Functional Requirements

#### Selección de motor y configuración

- **FR-001**: El sistema MUST permitir seleccionar el motor de base de datos (`PostgreSQL` o `SqlServer`) exclusivamente por configuración (archivo de configuración y/o variables de entorno, con las variables de entorno tomando precedencia), sin cambios de código ni recompilación.
- **FR-002**: La configuración MUST agrupar bajo una sección única: el proveedor activo, las cadenas de conexión por proveedor, el flag de migración automática (`AutoMigrate`) y los flags de seeding (`RunParametricSeed`, `RunTestSeed`).
- **FR-003**: Al arrancar, el sistema MUST validar la configuración y fallar de inmediato (fail-fast, proceso terminado, sin quedar escuchando) cuando: (a) el proveedor no es uno de los valores soportados, o (b) falta la cadena de conexión del proveedor seleccionado. El mensaje de error MUST identificar el problema exacto y los valores válidos.
- **FR-004**: La ausencia de cadena de conexión para el proveedor NO seleccionado NEVER es un error.
- **FR-005**: El comportamiento funcional del sistema (reglas de negocio, validaciones, resultados de consultas, unicidad, ordenamientos relevantes al negocio) MUST ser el mismo con cualquiera de los dos motores; las diferencias entre motores se resuelven de forma centralizada y NEVER se filtran a la lógica de negocio.
- **FR-006**: Las cadenas de conexión y credenciales MUST poder suministrarse por mecanismos de secretos (variables de entorno, secret stores) y NEVER aparecer completas en logs, trazas ni mensajes de error.

#### Aprovisionamiento de esquema y migraciones

- **FR-007**: El sistema MUST poder crear desde cero, sobre cualquiera de los dos motores, el esquema completo del producto: la base administrativa central y el esquema operativo de cada tenant (modelo schema-per-tenant preservado en ambos motores).
- **FR-008**: El sistema MUST mantener artefactos de migración específicos por motor, generados desde una única fuente de verdad del modelo de datos, de forma que un cambio de esquema produzca los artefactos de ambos motores mediante el proceso documentado.
- **FR-009**: Con `AutoMigrate = true`, el sistema MUST aplicar al arranque las migraciones pendientes del motor activo, registrando en el log cada migración aplicada (nombre y resultado).
- **FR-010**: El arranque con la base de datos temporalmente no disponible MUST reintentar la conexión con espera progresiva durante una ventana configurable antes de fallar; el fallo final MUST distinguir "base de datos inaccesible" de "migración fallida".
- **FR-011**: Con `AutoMigrate = false` y migraciones pendientes, el sistema MUST detectarlo y negarse a arrancar (fail-fast, sin modificar el esquema), con un error que enumere las migraciones pendientes y sugiera aplicar los scripts del DBA o habilitar `AutoMigrate`.
- **FR-012**: El sistema MUST ofrecer un mecanismo para generar scripts SQL idempotentes por motor, aptos para revisión y aplicación manual por un DBA, que produzcan el mismo esquema que la migración automática.
- **FR-013**: Cuando varias instancias arrancan concurrentemente contra la misma base de datos, MUST aplicarse las migraciones una sola vez, sin corrupción; las demás instancias esperan o continúan según el resultado.
- **FR-014**: El aprovisionamiento del esquema de un tenant nuevo en runtime (alta de cooperativa) MUST funcionar sobre el motor activo con el mismo flujo que la instalación inicial.

#### Seeding

- **FR-015**: El sistema MUST distinguir dos categorías de seed: **paramétrico** (datos maestros imprescindibles para operar: roles, permisos, monedas, tipos de documento, parámetros del sistema, plan de cuentas base) y **de pruebas** (datos demo: clientes, productos, facturas, movimientos de ejemplo).
- **FR-016**: El seed paramétrico MUST ser idempotente: ejecutable N veces sin duplicar registros ni fallar; completa lo faltante y NEVER pisa los valores que el cliente haya personalizado sobre registros maestros existentes.
- **FR-017**: El seed de pruebas MUST estar habilitado por defecto en ambientes de desarrollo y QA, deshabilitado por defecto en producción, y activable en producción solo por configuración explícita; toda activación en producción MUST quedar auditada (actor de la configuración efectiva, timestamp).
- **FR-018**: Ambos seeds MUST ejecutarse en orden de dependencias (maestros antes que transaccionales), ser transaccionales por unidad coherente (una interrupción no deja datos a medias que inutilicen el sistema) y comportarse igual en ambos motores.
- **FR-019**: Ambos seeds MUST poder ejecutarse (a) al arranque según los flags de configuración y (b) bajo demanda mediante un comando de operación o un endpoint administrativo protegido restringido al administrador master; toda ejecución bajo demanda MUST quedar registrada en auditoría con actor, alcance y resultado.
- **FR-019a**: El seed paramétrico al arranque MUST cubrir la base administrativa central Y todos los esquemas de tenant existentes (verificación/completado idempotente por tenant); el alta de un tenant nuevo en runtime MUST sembrar su esquema como parte del aprovisionamiento (junto con FR-014).
- **FR-020**: El seed de pruebas MUST marcar sus registros de forma reconocible (identificable como dato demo) para poder localizarlos y depurarlos si llegaran a un ambiente equivocado.
- **FR-021**: El seeding MUST rehusarse a correr sobre un esquema con migraciones pendientes, con un mensaje que indique aplicar las migraciones primero.

#### Operación y observabilidad

- **FR-022**: El sistema MUST exponer health checks que reporten el estado de la conectividad con la base de datos del motor activo (y de las demás dependencias ya monitoreadas), distinguiendo "vivo" de "listo para servir".
- **FR-023**: El log de arranque MUST dejar constancia de: motor seleccionado, resultado de la validación de configuración, migraciones aplicadas u omitidas, y seeds ejecutados u omitidos con su razón.
- **FR-024**: El proyecto MUST incluir una orquestación local que levante ambos motores simultáneamente (puertos distintos) para desarrollo y verificación de paridad, y configuraciones de ejemplo por ambiente (desarrollo, QA, producción) que demuestren las combinaciones válidas.
- **FR-025**: El proceso para desarrolladores (generar migraciones por motor, aplicarlas, ejecutar seeds manualmente, generar scripts para DBA) MUST estar documentado con comandos exactos reproducibles.

### Key Entities *(include if feature involves data)*

- **Configuración de base de datos**: define el proveedor activo, las cadenas de conexión por proveedor y las políticas de arranque (`AutoMigrate`, `RunParametricSeed`, `RunTestSeed`). Vive en configuración por ambiente, no en la base de datos.
- **Artefacto de migración por motor**: representación versionada de un cambio de esquema para un motor concreto, generada desde la fuente única del modelo; su historial aplicado queda registrado en cada base de datos.
- **Módulo de seed**: unidad de siembra con categoría (paramétrico | pruebas), orden de dependencia y alcance (base administrativa | esquema de tenant); su ejecución queda trazada en el log y, cuando es bajo demanda o en producción, en auditoría.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Una misma build del producto se despliega operativa sobre PostgreSQL y sobre SQL Server cambiando únicamente configuración — cero cambios de código, cero recompilación — verificado en cada release.
- **SC-002**: Una instalación desde cero (motor vacío → esquema completo → datos maestros → login del master admin exitoso) se completa sin intervención manual en menos de 10 minutos en cualquiera de los dos motores.
- **SC-003**: El 100 % de la suite de pruebas de integración existente (identidad central, admin, seguridad, núcleo) pasa contra ambos motores en cada release; el resto de los módulos cuenta con verificación de aprovisionamiento (smoke) en ambos motores.
- **SC-004**: El 100 % de los arranques con configuración inválida termina en fallo inmediato con mensaje accionable; cero casos de sistema "a medio arrancar" sirviendo peticiones sin base de datos válida.
- **SC-005**: Cero datos de demostración presentes en ambientes de producción sin activación explícita registrada en auditoría.
- **SC-006**: Ejecutar los seeds 3 veces consecutivas produce exactamente el mismo estado que ejecutarlos una vez (cero duplicados, cero errores).
- **SC-007**: Con la base de datos indisponible al arranque y recuperada dentro de la ventana de reintentos, el sistema queda operativo sin intervención manual en el 100 % de los casos probados.
- **SC-008**: Un cambio de esquema nuevo genera artefactos para ambos motores siguiendo el proceso documentado; la verificación de paridad detecta el 100 % de las omisiones de un motor.

## Assumptions

- **Alcance de motores**: solo las bases de datos **relacionales** del producto (base administrativa central y esquemas de tenant) participan del multi-motor. MongoDB (auditoría) y Redis (caché) quedan fuera del alcance y no cambian.
- **Un solo motor por instalación**: la base administrativa y todos los tenants de una instalación usan el mismo motor. No se soporta modo mixto (Admin en un motor, tenants en otro) en v1.
- **Sin migración de datos entre motores**: cambiar el `Provider` de una instalación existente apunta a otra base de datos; el traslado de datos entre motores (SQL Server → PostgreSQL o viceversa) es una operación de migración fuera del alcance de este feature.
- **Fuente de verdad del esquema** (clarificado 2026-08-03): el modelo de datos de la aplicación es la fuente única desde la que se generan los artefactos de migración de ambos motores. El corpus DDL T-SQL existente (`database/schema/`, `database/migration/`) queda congelado como referencia histórica y deja de evolucionar; el principio constitucional XII (idempotencia, documentación, reversibilidad declarada) se cumple mediante los scripts generados para DBA.
- **Sin instalaciones previas** (clarificado 2026-08-03): no existe ninguna implementación/instalación productiva actual — el despliegue es greenfield. No se requiere línea base (baseline) ni adopción de esquemas legados; los entornos de desarrollo existentes pueden recrearse desde cero con el nuevo mecanismo.
- **Modalidad SaaS**: en la nube el motor será PostgreSQL; SQL Server queda como opción exclusivamente on-premise. La elección no altera funcionalidad visible para el usuario final.
- **Default de desarrollo y CI** (clarificado 2026-08-03): PostgreSQL es el motor por defecto en desarrollo local y el primario en CI (paridad con la modalidad SaaS); SQL Server se ejercita mediante la suite dual y queda disponible en la orquestación local para reproducir escenarios on-premise.
- **Seguridad del endpoint de seed**: el endpoint administrativo de seeding bajo demanda se restringe al administrador master de la plataforma con la autorización central existente.
- **Ambiente se determina por la configuración estándar del producto** (Development / QA / Production); los defaults de `RunTestSeed` derivan del ambiente y solo el flag explícito los sobreescribe.
- **Rendimiento**: no se exige paridad exacta de rendimiento entre motores; sí que ambos cumplan los objetivos de rendimiento ya definidos por el producto (p. ej. los umbrales de login de la Fase 1).
