# Estado de la plataforma y pendientes

> Corte: **2026-09-21**. Actualizar al cerrar cada pendiente.
> Complementa [despliegue-infraestructura.md](despliegue-infraestructura.md) (diseño e
> instalación) y, en el repositorio GitOps, `docs/backups.md` y
> `docs/mongo-replica-set.md`.

---

## 1. Qué está operativo

### Ambientes

| | DEV | QA | PRODUCCIÓN |
|---|---|---|---|
| ERP desplegado | ✅ | ✅ | ✅ |
| Réplicas API / Web | 1 / 1 | 1 / 1 | **2 / 2** |
| PostgreSQL | ✅ | ✅ | ✅ |
| MongoDB (replica set) | ✅ | ✅ | ✅ |
| Sincronización | automática con auto-reparación | automática | **manual** |

Producción responde en **https://app.ingenia365.com** — login verificado en
navegador, sin errores de consola. La IP del servidor no es visible desde
internet: todo entra por el túnel de Cloudflare.

**Producción corre desde el 2026-09-07 la misma generación que DEV y QA**
(`release` = `50ba799`, 108 commits promovidos de una vez): rediseño del segundo
factor con passkeys, llavero de DataProtection en `ADM_DataProtectionKeys`,
aprovisionamiento de una base por cooperativa y la nómina de la feature 005.
Se aplicaron 13 migraciones administrativas y 7 operativas por el Job PreSync de
Argo, con respaldo previo (`erp-db-pre-nomina-mfa-20260907`) y las tres consultas
de [despliegue-rediseno-mfa.md](despliegue-rediseno-mfa.md) respondidas contra la
base: 0 cooperativas, 0 adjuntos, maestro sin segundo factor. El 2026-09-09 hubo
que **resembrar al maestro** porque nadie recordaba la contraseña de agosto (Caso 3
de [rescate-del-administrador-maestro.md](rescate-del-administrador-maestro.md)); ese
mismo día inscribió su autenticador, entró dos veces y recibió sus diez códigos de
respaldo, y se otorgó `CREATEDB` al rol de la API (P13 cerrado). Con eso, lo único
que separa a producción de su primera cooperativa es P14.

Desde entonces producción se promueve commit a commit con un merge `Promover develop
a release: …` y sincronización manual de Argo.

**`release 0a309f3`** (2026-09-22 09:35–09:45 UTC, GitOps `8c9d2e7`, autorizado por el dueño con
«realiza merge a develop y también despliega en producción»): **contabilidad 009 E2** —consultas e
informes (13 vistas), libro auxiliar, estados financieros, presupuesto, cierre y reapertura del
ejercicio (`CI`), saldos de apertura (`AP`) y las e2e pendientes de E1— sobre `develop` `857d0e5`.
**Sin migraciones** (26 en `ingenia365erp` y `cooflopal` antes y después). Respaldos previos
`pg_dump -Fc` en `/root/respaldos/`: `ingenia365erp_admin-`, `ingenia365erp-` y
`cooflopal-20260922-pre-f009e2.dump` (72 KB / 1,4 MB / 1,5 MB, cabecera `PGDMP`; el nodo no tiene
`pg_restore` y el contenedor no acepta el archivo por `stdin`, así que esta vez no se contaron las
tablas). Diagnóstico antes: 0 documentos y 0 movimientos contables en las dos bases (la contabilidad
no está iniciada en `cooflopal`; 15 corridas de nómina intactas). CI de `develop` y `release`
verdes; sync manual de Argo `Succeeded` → `Healthy`; pods API `e676b852…` y Web `bc0927c4…`;
30 permisos `Accounting.*` en cada base; `/health/ready` 200; `app.ingenia365.com` 200; 0 `[ERR]`.
**Lo que sigue en producción**: T096 (el contador valida el CUIF y recorre E2 por rol), iniciar la
contabilidad en `cooflopal` y definir la cuenta de resultado antes del primer cierre anual
([contabilidad-primer-ejercicio.md](contabilidad-primer-ejercicio.md) §3, §7a).

**`release c388a17`** (2026-09-22 01:05–01:15 UTC, GitOps `8f8449b`, autorizado por el dueño con
«realiza commit, merge a develop e integrar produccion»; segundo revisor Jorman Copete): la feature 010
completa salvo N3 — **N1** (prima, cesantías anuales, vacaciones, liquidación definitiva, políticas,
festivos, saldos iniciales, ficha PILA/DIAN), **N4** (dispersión bancaria con formatos por banco en
Core) y **N2** (planilla PILA y procedimiento 2). Cuatro migraciones **aditivas** por el Job PreSync:
`NominaPrestacionesYDian`, `SettlementDeductionsUnicosEntreVivas`, `NominaDispersionBancaria`,
`NominaPilaYNominaElectronica` (`cooflopal` e `ingenia365erp`: 22 → 26). Respaldos previos
`/root/respaldos/{ingenia365erp_admin,ingenia365erp,cooflopal}-20260921-pre-f010.dump`. Verificado
contra la base: 15 corridas previas con `Kind = 0`, 11 empleados activos, permisos sembrados por la
API al arrancar (Payroll.Settlements 6, ServiceBonus 4, Pila 4, WithholdingRate 3, Disbursement 4,
Core.BankFileFormats 2), 2 formatos bancarios (`CSV-GENERICO`, `AVVILLAS-1`), 11 políticas, 54
festivos, 9 motivos de retiro; imágenes por digest iguales al overlay (api `be0ffb7f`); `/health/ready`
200, `app.ingenia365.com` 200, 0 `[ERR]` en la API tras el relevo. Antes, el mismo día, `develop`
`c7ddd8d` desplegó en DEV y QA por AutoMigrate (`coop_prueba` con las cuatro migraciones). El primer
CI de `develop` cayó porque git normalizaba a LF los `.esperado.txt` de la PILA (ancho fijo con CRLF):
`.gitattributes` con `*.esperado.txt -text`. **Lo que sigue en la cooperativa** (runbooks §4d/§4e y
`pila-primera-planilla.md`): cuentas de los 16 conceptos nuevos, saldos iniciales de prestaciones,
clase ARL y banco de dispersión en cada ficha, código ACH de los bancos, códigos PILA de EPS/fondos/
ARL/cajas, datos del aportante, cotejo del layout con el anexo v30 y validador de Aportes en Línea (T094),
layout de AV Villas (T147), QA por rol y validación de la contadora (T077, T107).

**`release fb8f016`** (2026-09-19 20:12–20:15 UTC, GitOps `56be5ec`, autorizado por el dueño con
«empújalo a PDN»): recurrentes de nómina sin duplicados. En `cooflopal` la contadora quedó con la
misma deducción registrada dos veces (Ids 2 y 3, 16:17 y 16:25 UTC, sin ninguna búsqueda de
empleado entre las dos): «Nueva recurrente» abría con el buscador y los resultados del empleado
anterior; cada cálculo generaba las dos novedades y anular una no servía porque el siguiente cálculo
la regeneraba. Desde este release el diálogo abre limpio, la misma orden responde
`Payroll.RecurringNoveltyDuplicate`, el cálculo no regenera una novedad anulada por una persona (sólo
la del descarte del borrador) y de dos recurrentes iguales entra la más antigua y avisa. Lleva también
`4d05e26` (sobre `Request.BodyInvalid` en el 400 de un cuerpo ilegible). Sin migraciones (22 en
`cooflopal` antes y después). Respaldos `*-20260919c-pre-recurrentes.dump` (23/288/288 tablas con
datos); `cooflopal` conserva 11 empleados, 14 corridas, 28 novedades activas y 12 recurrentes;
imágenes por digest iguales al overlay (api `7dc8c76b`, web `b7ce7f58`); `/health/ready` 200,
`app.ingenia365.com` 200, 0 errores en el log de la API tras el relevo. **Pendiente en la
cooperativa**: desactivar en Novedades › Recurrentes la segunda recurrente de FONDO DE SOLIDARIDAD del
empleado 8 (la de las 16:25 UTC) y recalcular la 1.ª quincena de agosto.

**`release d7e4d0e`** (2026-09-19 ~11:45 UTC, GitOps `01b8918`, autorizado por el dueño): arregla
la regresión que llevó `ff25948` a producción —Reportes de nómina caía con
`DeserializeUnableToConvertValue … $.columnas[0].tipo` porque el convertidor global de enums
pisaba el `[JsonConverter]` de `TipoDeColumna`—, la tabla de retención por plan de nómina que la
liquidación sí usa (`RetencionPorPlanDeNomina`: agrega `PayrollPlanId`, 0 filas en PDN) y los
enlaces del menú sin página (informes contables E2, Beneficiarios). Respaldos
`*-20260919b-pre-retencion-plan.dump` (23/288/288 tablas con datos); `cooflopal` conserva 11
empleados y 9 corridas; `/health/ready` 200.

**`release ff25948`** (2026-09-19 04:37–04:41 UTC, GitOps `12d1101`, autorizado por el dueño con
«sí, empujalo a PDN»): **features 008 y 009 juntas** —alta de persona en un paso, permisos de
maestros, contabilidad NIIF E1 con el CUIF oficial (2.110 cuentas), cuentas propias donde el
catálogo no trae hijos, enums por nombre en la API, fecha de ingreso editable, el Bearer sólo por
el handler—. Respaldos previos `pg_dump -Fc` en `/root/respaldos/` del nodo:
`ingenia365erp_admin-`, `ingenia365erp-` y `cooflopal-20260919-pre-f008-f009.dump` (leídos con
`pg_restore -l`: 23/292/292 tablas con datos). Diagnósticos antes: libros 0 documentos / 0
movimientos / 0 cuentas por concepto / 0 corridas aprobadas en las dos bases; banderas de persona
sin inconsistencias (cooflopal 11 personas). El Job PreSync corrió los tres pasos (admin sin
pendientes; operativa y `cooflopal` con las seis migraciones: `ReconciliarBanderasDerivadasDePersona`,
`UnaSolaFichaVivaPorPersona`, `MotivoDeRetiroComoTexto`, `ContabilidadNiif`,
`LongitudDeAuxiliarPorRango`, `NombreDeCuentaHasta200`; revisor XII de las dos destructivas:
Jorman Copete). Después: 29 tablas `ACC_*`, los dos PUC (2.110 y 1.869), 205 rubros, 18 tipos,
8 cruces y 103 permisos (`SEC_Permissions` 62 → 103) en las dos bases; `cooflopal` conserva sus
11 empleados y 9 corridas y las banderas siguen sin inconsistencias; `/health/ready` 200,
`app.ingenia365.com` 200, dos pods de API arrancados sin errores. **Lo que sigue en producción**:
el contador valida el CUIF (`POST /api/accounting/catalogs/PUC-SOLIDARIO/validate`), y en
`cooflopal` iniciar la contabilidad, crear las auxiliares y parametrizar las cuentas por concepto
de nómina antes de aprobar la próxima liquidación
([contabilidad-primer-ejercicio.md](contabilidad-primer-ejercicio.md)).
**`release 79a2ea6`** (2026-09-13 19:20, GitOps `5caace3`): indicador de carga por zona en las 37 pantallas de Core y Nómina (`IndicadorDeCarga` + `EstadoDeCarga`, [docs/manual/indicador-de-carga.md](../manual/indicador-de-carga.md)) y la prueba de arquitectura que lo exige en pantallas nuevas o modificadas. Antes, **`release 7d278f7`** (2026-09-13 17:00, GitOps `fd84f0d`, respaldos `*-pre-julio2026-20260913.dump`): la semilla base 2026 con la norma de julio de 2026 (`NormaJulio2026ComoBase`: HORAS_MES 210, recargo 0,90, extras 2,15/2,65 en `ingenia365erp` y `cooflopal`; las vigencias de julio se conservan con el mismo valor) y el tipo de columna de Reportes de nómina por nombre (la pantalla fallaba con `DeserializeUnableToConvertValue`). Antes, **`release 8cf247a` y `0c7a555`** (2026-09-13,
GitOps `6d947f0`/`bc49608`, respaldos `*-pre-rendimiento-20260913.dump`): la ronda 1 de
rendimiento —ver [specs/007-rendimiento-percibido](../../specs/007-rendimiento-percibido/spec.md)—:
pipeline de 9 min, Syncfusion por componente, estilos con huella, Serilog obediente, IP real
por `CF-Connecting-IP`, `no-store` bajo `/api`, ReadyToRun, índices de lectura, arranque del
cliente con indicador, y la base legal 2026 corregida por migración de datos
(`BaseLegal2026Corregida`, `HorasMesBase2026`; y por decisión del usuario del 2026-09-13 la base 2026 pasa a llevar directamente la norma de julio de 2026 —210 h, 0,90, 2,15/2,65— con `NormaJulio2026ComoBase`, ver [nomina-primer-periodo.md §4c](nomina-primer-periodo.md)). En GitOps `b177479`: CPU explícita (API 2, Web 1),
`LimitRange` sólo memoria, sondas sin retardo. Medido en PDN tras el despliegue: API
Started→Ready **39 s** (eran 100–140), `cpu.max 200000/100000`, 0 `Executed DbCommand`.
Antes, **`release e63bc38`** (2026-09-12 23:30,
GitOps `8a59081`, sincronizado a `d0b88ca`) llevó la feature 006 completa —periodicidades
decadal y semanal, sub-período y mes de imputación, «Aplica en» de las recurrentes,
«Descartar borrador», el centro de Reportes de nómina con Excel/PDF/Word, plan de nómina
en la ficha— más el arreglo del botón «Versiones» de Conceptos y las vigencias de ley
(`HORAS_MES` 210 desde el 15/07/2026; recargo dominical 0,90 y extras dominicales 2,15/2,65
desde el 01/07/2026). Migración `PeriodicidadesReglasYDescarte` aplicada por el Job PreSync
en `ingenia365erp` y `cooflopal` (respaldos `*-pre-f006-20260912-2246.dump`). **Ojo con la
base 2026 de producción**: la semilla no pisa vigencias existentes, así que `HORAS_MES`
2026-01-01→2026-07-14 quedó en **240** (no 220), `RECARGO_DOMINICAL` en 0,75 (no 0,80) y las
extras dominicales en 2,00/2,50 (no 2,05/2,55): corregirlas a mano en Parámetros legales y
Conceptos, o con una migración de datos, antes de liquidar un período de ese semestre.
`release ccef705` (2026-09-12, GitOps `777f930`) llevó los códigos alfanuméricos de catálogo (migración
`CodigosAlfanumericosEnCatalogos`, respaldos `*-pre-codigos-20260912-2002.dump`), largos y
obligatorios en los formularios, el aviso de código duplicado, el menú y los avisos abajo a la
derecha, y el icono/manifest de la app (`release 1c774ce`); `release 08c664e` (GitOps
`13cc30b`) llevó fondo de cesantías y caja de compensación en la ficha con la migración
`CajasDeCompensacionYFondoDeCesantiasEnLaFicha` (respaldos previos `pg_dump` en
`/root/respaldos/*-pre-cajas-20260912-1117.dump`; aplicada por el Job PreSync en
`ingenia365erp` y `cooflopal`); `release 407c85e` (GitOps `78da720`) la cuenta de correo
de respaldo; `release e620507` (2026-09-12, GitOps
`158da4b`) el reenvío de invitaciones fail-soft. Antes, **`release 47185e6`
(2026-09-11)**: clase de riesgo ARL en la ficha del empleado, semilla de las cinco
clases (`WorkRiskClassesSeeder`, corrió sola en la operativa de PDN: «5 fila(s)
insertadas») y cargador por `Code`; en QA se había visto que toda liquidación salía
«sin clase de riesgo ARL» (ver
[nomina-primer-periodo.md](nomina-primer-periodo.md) §1). Pods de API y Web en
los digests del commit GitOps `7ad9703`, `/api/health` 200 por el borde. En el
arranque de la API en PDN aparece dos veces «Cannot load library
libgssapi_krb5.so.2»: es Npgsql probando Kerberos en una imagen sin la librería,
sigue con contraseña y no es de esta entrega.

### Respaldos

| Ambiente | PostgreSQL | Retención | MongoDB |
|---|---|---|---|
| PDN | WAL continuo + diario 01:30 COT | 35 d | 02:15 COT |
| DEV | WAL continuo + diario 02:30 COT | 7 d | 02:45 COT |
| QA | WAL continuo + diario 03:00 COT | 14 d | 03:15 COT |

Destino: `s3://ingenia365-erp-backups`, con **Object Lock en modo GOVERNANCE a
40 días**, versionado, cifrado AES256 y credencial de permisos mínimos que **no
puede saltarse el bloqueo ni borrar versiones**.

**Catálogos separados por ambiente** (`erp-db-dev`, `erp-db-qa`, `erp-db-pdn`).
Los tres clústeres se llaman `erp-db`: sin esa separación se sobrescribirían
entre sí y solo se notaría al intentar restaurar.

#### Restauraciones probadas, no supuestas

| Motor | Evidencia |
|---|---|
| PostgreSQL | Respaldo real de producción restaurado en un clúster aparte. Un marcador escrito **18 minutos después** del respaldo base apareció en el restaurado: llegó por **replay de WAL**, lo que valida el PITR y no solo la copia diaria |
| MongoDB | 132 documentos restaurados con índices, conteo idéntico al origen, verificado con aserción automática |

> Un ensayo que solo restaure el respaldo base no distingue *"tengo PITR"* de
> *"tengo copias diarias"*. Esa diferencia son horas de datos perdidos.

### Observabilidad

- Producción emite métricas por la malla Tailscale al Prometheus de nonprod
  (modo **agente**: no evalúa reglas ni almacena, ~200 MB).
- **9 reglas de alerta** sobre métricas verificadas con valores reales.
- **Canal de Telegram funcionando**, probado de extremo a extremo.
- Interruptor de hombre muerto: si producción deja de reportar, eso mismo alerta.

> Alertmanager estaba instalado con `receiver: "null"`: **descartaba todas las
> alertas de todos los ambientes**. Había reglas, paneles y alertas disparándose,
> y nadie se enteraba de nada. Dos alertas reales llevaban días ocultas.

### Seguridad

- Repositorio de la aplicación y paquetes de imágenes en **privado** (verificado
  sin credenciales: 401 anónimo). Los clústeres descargan con `ghcr-pull`.
- SSH público cerrado en ambos servidores; administración solo por Tailscale.
- Claves de firma RS256 distintas por ambiente: un token de DEV no vale en
  producción.

### Correo saliente

- **Servidor**: `mail.notifica365.com:587` con STARTTLS, remitente
  `noresponder.ingenia365erp@notifica365.com`. Envío verificado por el usuario.
- **No es Microsoft 365**, pese a lo que sugiere el nombre: el servidor se
  identifica como Haraka/Poste.io autoalojado. Importa para operar — no hay
  consola de M365 ni contraseñas de aplicación, y la entregabilidad depende de la
  reputación de esa IP, no de la de Microsoft.
- **Credenciales por variable de entorno** (`Smtp__Username`, `Smtp__Password`),
  nunca en el repositorio. En Kubernetes vienen del Secret `erp-smtp`.
- **El usuario autenticado tiene que ser el buzón del remitente.** Poste.io rechaza
  enviar «como» otra dirección: «You are not allowed to send emails as X while logged
  as Y». Pasó en producción el 2026-09-12 con el Secret creado con `ingeniaerp@` y el
  remitente `noresponder.ingenia365erp@`; se corrigió recreando el Secret con la
  cuenta del remitente.
- **Cuenta de respaldo** (desde el 2026-09-12): sección `Smtp:Respaldo` con host,
  remitente y huella propios (`Smtp__Respaldo__*` en el overlay) y credenciales en el
  Secret `erp-smtp-respaldo` (`crear-secreto-smtp.ps1 -Ambiente pdn -Respaldo`).
  `SmtpEmailSender` agota los reintentos de la principal, lo registra como error y
  manda el mismo correo por la segunda cuenta con **su** remitente; si las dos fallan,
  el motivo trae ambos. En producción: principal `noresponder.ingenia365erp@`,
  respaldo `ingeniaerp@`, las dos en `mail.notifica365.com`, o sea que cubre un buzón
  bloqueado o una contraseña vencida, **no** una caída de ese servidor —para eso el
  respaldo tendría que ser otro relay, y la configuración lo admite—.
- **QA y PRODUCCIÓN envían correo real** (QA desde el 2026-09-04, PDN desde el
  2026-09-12): `Smtp__*` en su overlay y Secret `erp-smtp` en su namespace. **DEV no
  envía**: sin `Smtp__Host` ni Secret, y **no hay ningún capturador**
  (smtp4dev/MailHog) desplegado en el clúster. **En producción faltó hasta el
  2026-09-11 y mordió**: al registrar la primera
  cooperativa real (COOFLOPAL) la invitación de su administradora no salió
  (`SocketException 111` contra `localhost:25`, cuatro intentos, ~22 s) y el botón
  «Reenviar» respondía 500. Se cerró en dos pasos, en este orden: (1) el usuario
  creó el Secret con `tools/scripts/crear-secreto-smtp.ps1 -Ambiente pdn` —la
  contraseña se teclea oculta, no pasa por el repositorio ni por el chat—; (2) el
  overlay `pdn` recibió las claves `Smtp__*` (GitOps `5df005c`) y `erp-pdn` se
  sincronizó a mano: los pods de la API arrancaron con `Smtp__Host`, la huella y
  las credenciales del Secret. El orden importa: el Secret se lee al crear el pod
  (`optional: true`), así que crearlo después obliga a rotar los pods. El reenvío
  ya no responde 500: devuelve `correoEnviado=false` con el motivo (develop
  `d8f4c21`) y la invitación nueva queda para reintentar.
  clúster —una versión anterior de esta nota decía lo contrario, sobre un commit
  de GitOps que nunca se subió—. Cuando en DEV o PDN falla el envío, el ERP lo
  dice (`CorreoEnviado=false` con el motivo) y la invitación queda para reenviar.
- **Los enlaces del correo ya apuntan a donde deben**. Hasta esta ronda,
  `IdentityEmail:BaseUrl` no estaba declarado en ningún ambiente y regía el valor
  cableado `https://localhost:7200`: toda invitación y todo restablecimiento
  enviados desde las VPS llevaban un enlace muerto.
- Ver `docs/operaciones/correo-saliente.md` para operarlo.

### Interfaz

- **Sistema de diseño único** en
  `src/Presentation/IngenIA365ERP.Shared/wwwroot/css/tokens.css`. Las pantallas
  no declaran colores propios: se migraron 467 literales repartidos en 65
  archivos, más 45 escritos con nombre CSS (`color:red`). Cambiar la identidad
  visual del producto entero es cambiar ese archivo.
- **Tema claro/oscuro, densidad, escala tipográfica y contraste alto**,
  aplicados antes del primer pintado para que no haya destello. Se guardan en la
  cuenta del usuario (`ADM_UserSettings`), no sólo en el navegador.
- **Contraste verificado**: las 44 combinaciones de color y tema cumplen WCAG AA
  (mínimo 4,5:1). La comprobación se hizo midiendo estilos calculados en un
  navegador real, no leyendo el CSS.
- **Login a dos columnas** con panel promocional administrable desde
  `/administracion/promociones`. Si no hay contenido o la consulta falla, el
  panel no se dibuja y el login funciona igual.

- **Hoja de componentes** (`componentes.css`): las 199 pantallas estaban escritas
  contra clases que nunca se declararon —`.page-header` aparecía 129 veces sin
  existir—, y el estilo embebido era el parche. Ya no hay ninguna página con
  bloque `<style>` propio.

| Indicador | Antes | Después |
|---|---:|---:|
| Clases sin definición aplicable | 29 (224 usos) | 12 (12 usos) |
| Clases definidas por varias páginas a la vez | 12 | 0 |
| Páginas con bloque `<style>` embebido | 25 | 0 |
| Declaraciones `style=` embebidas | 924 | 307 |
| Medidas en píxeles literales | 1498 | 256 |
| Hojas de estilo con ámbito | 8 | 49 |

Defectos que la migración sacó a la luz, ya corregidos:

| Defecto | Efecto |
|---|---|
| `.kpi-value` en `#fff` sobre tarjeta blanca | Las cifras del dashboard eran invisibles en modo claro |
| `--ifx-*` y `--sf-grid-header-bg` nunca definidos | Cada `var(--ifx-border, #ccc)` caía siempre al literal |
| Calificación A–E con dos paletas distintas | La misma categoría se pintaba de un color en una pantalla y de otro en otra |
| Paneles azul oscuro incrustados | Restos de un tema anterior dentro de una aplicación clara |
| `auth-card` sin definir en ningún CSS | 14 pantallas del flujo de autenticación sin estilos |
| `.info-card` y `.label` definidas por 14 páginas | La misma pantalla cambiaba de aspecto según de dónde vinieras |
| Reglas de navegación sin `::deep` | Los encabezados de grupo de la barra lateral, centrados en vez de alineados, en todas las pantallas |
| SyncFusion dimensionado por `fluent2.css` | Los filtros se quedaban en 30px con «texto muy grande», justo para quien activó esa opción |

---

## 2. Pendientes

### 🔴 Prioridad alta

#### P1 — Desactivar la llave AWS comprometida

`AKIAQ3EGUSHPYS4PQB5X` circuló en texto plano. **Ya no la usa el ERP** (se
reemplazó por la del usuario `ingenia365-erp-backup`), pero sigue activa.

Consola AWS → IAM → usuario dueño → *Credenciales de seguridad* → **Desactivar**.
Confirmar antes que `polly-carteravirtual` tenga su reemplazo: esa aplicación sí
la estaba usando.

#### P2 — MongoDB sin redundancia de almacenamiento

La auditoría SARLAFT (retención obligatoria de 5 años) vive en `local-path`, es
decir **disco local del nodo**. Hay respaldo diario a S3, pero si ese disco muere
se pierde todo lo posterior al último volcado (RPO ≤ 24 h).

PostgreSQL no tiene este problema: archiva WAL de forma continua (RPO ≤ 5 min).

Opciones: almacenamiento replicado (Longhorn u otro), un segundo nodo, o llevar
MongoDB a un servicio gestionado.

#### P3 — Producción despliega por etiqueta móvil — ✅ cerrado el 2026-09-10

Los overlays usaban `:release` y `:develop`, etiquetas que se reapuntan.
Republicarlas no cambiaba el manifiesto, Argo decía `Synced` y los pods seguían
con la imagen anterior hasta un `rollout restart` a mano. Pasó tres veces en
producción (2026-08, 2026-09-07 y 2026-09-10) y no dejaba registro de qué
artefacto corría.

**Solución.** Los tres overlays fijan el **digest** de cada imagen (GitOps
`7c0faa6`, con los digests que corrían ese día para no desplegar nada), y el CI
de la aplicación los escribe en cada publicación: el job `gitops` de
`.github/workflows/ci.yml` toma el digest que devuelve `docker/build-push-action`,
lo pone en el overlay del ambiente (`develop` → `dev` y `qa`; `release` → `pdn`),
retira `newTag` y empuja el commit al repo GitOps. En DEV y QA Argo sincroniza
solo y rota los pods; en producción el commit queda esperando la aprobación
manual, y **lo que se aprueba es un digest**, no una etiqueta. Volver atrás es
revertir ese commit.

**Requiere un secreto que sólo puede crear el dueño del repo:** `GITOPS_TOKEN`
en `JormanCopete/IngenIA365ERP` (Settings → Secrets and variables → Actions), un
token de acceso de grano fino con permiso *Contents: Read and write* sobre
`JormanCopete/ingenia365-gitops` y nada más. Sin él, el job `gitops` **falla a
propósito** con un mensaje que lo dice: las imágenes se publican, pero ningún
ambiente las despliega. Un despliegue que no llegó y nadie notó es peor que un CI
rojo.

Ya no hace falta `rollout restart` después de promover. `imagePullPolicy: Always`
se conserva como red por si algún overlay volviera a una etiqueta.

Probado de punta a punta el mismo día: `develop cdb3ec6` → GitOps `0f11f18` → DEV
y QA rotaron solos en un minuto; `release 2c16a89` → GitOps `f1c8cd3` → Argo
mostró `OutOfSync` en los dos Deployments, se aprobó, el Job PreSync corrió con
el migrador por digest y los pods quedaron en `api@9a17c881`, `web@6c830f39`.
**La «rareza» de la primera sincronización, explicada y corregida** (GitOps
`cffb943`): el Job PreSync tenía `hook-delete-policy: BeforeHookCreation`, y
Argo CD 3.5.0, al tener que borrar el Job de la sincronización anterior (lo
conservábamos 24 h por TTL), pasaba a «waiting for deletion» y en el mismo
segundo cerraba la operación como `Succeeded` sin ejecutar el hook ni aplicar
nada. Por eso la primera sincronización tras otra reciente no desplegaba y la
segunda sí, y por eso la primera del día funcionaba (el Job anterior ya había
vencido). `generateName` no es salida: kustomize exige `name`, y con `name` Argo
lo ignora (comprobado). Con `HookSucceeded` el Job se borra al terminar bien y
nunca queda uno viejo que borrar; probado con dos sincronizaciones seguidas, las
dos ejecutaron el migrador. Contrapartida documentada en el propio manifiesto:
si el Job **falla**, se queda con sus logs y hay que borrarlo a mano antes de
reintentar (`k3s kubectl -n erp-pdn delete job erp-db-migrate`); en cualquier
otro momento ese `delete` responde `NotFound`, y eso es lo normal: no hay
ninguna migración fallida esperando.

#### P13 — El rol de la API no puede crear bases: ninguna cooperativa se aprovisiona

Registrar una cooperativa hace `CREATE DATABASE` con la conexión de la API
(`TenantDatabaseProvisioner`), y el rol `ingenia` de los tres clústeres
CloudNativePG **no tiene `CREATEDB`**: nació como `initdb.owner`, sin más
atributos. Sin el permiso la cooperativa queda registrada con
`ProvisioningState = Failed` y el motivo `permission denied to create database`;
la invitación al primer administrador sale igual y lo lleva a una cooperativa
sin base. Detectado el 2026-09-04 en QA, antes de registrar la primera.

El manifiesto de `erp-db` **no está en GitOps** (sólo el de ensayo de PDN) y
`spec.managed.roles` está vacío, así que el operador ni gestiona ni revierte el
rol: se otorga en vivo, una vez por clúster, como `postgres`:

```bash
ssh -i ~/.ssh/ingenia365_deploy root@100.94.218.42 "k3s kubectl -n erp-qa exec erp-db-1 -c postgres -- psql -U postgres -c 'ALTER ROLE ingenia CREATEDB'"
ssh -i ~/.ssh/ingenia365_deploy root@100.94.218.42 "k3s kubectl -n erp-dev exec erp-db-1 -c postgres -- psql -U postgres -c 'ALTER ROLE ingenia CREATEDB'"
ssh -i ~/.ssh/ingenia365_deploy root@100.104.190.76 "k3s kubectl -n erp-pdn exec erp-db-1 -c postgres -- psql -U postgres -c 'ALTER ROLE ingenia CREATEDB'"
```

Comprobar: `select rolcreatedb from pg_roles where rolname = 'ingenia'` debe dar
`t`. Es reversible con `NOCREATEDB`. Si una cooperativa ya quedó en `Failed`,
`POST /api/saas/tenants/{publicId}/provision` es idempotente y la repara.

| Ambiente | Estado |
|---|---|
| DEV | ✅ 2026-09-04 (`rolcreatedb = t`) |
| QA | ✅ 2026-09-04 — primera cooperativa `coop_prueba` aprovisionada en `Ready`, invitación entregada y aceptada |
| PDN | ✅ 2026-09-09 (`rolcreatedb = t`, verificado contra el clúster). Lo que sigue faltando antes de la primera cooperativa real es **P14** |

> Al aprovisionar apareció una carrera, una sola vez y sin daño:
> `NotificationEmailDispatcher` recorre `ListActiveAsync()` —que filtra por
> `IsActive` y no por `ProvisioningState`— y abrió `coop_prueba` cuando la base
> ya existía pero sus tablas no (`42P01 dbo.COR_Notifications does not exist`).
> Reintentó a los 15 s y no volvió a fallar. **Arreglado en `e7c96a2`** (2026-09-04):
> `ListActiveAsync` exige `ProvisioningState = Ready`, con prueba unitaria.

#### P14 — Las bases de cooperativa de producción no las migra nadie — ✅ cerrado el 2026-09-11

En producción `Database__AutoMigrate=false` a propósito, y el Job PreSync migraba
la base administrativa y la operativa, pero **neutralizaba el bucle de
cooperativas** (`--tenant __ninguna__`) porque ese bucle era del modelo anterior
—un esquema por cooperativa—. La primera cooperativa que se registrara habría
nacido con el esquema del día del alta y nadie le habría aplicado las migraciones
siguientes.

**Solución.** El migrador tiene un alcance nuevo, `migrate --scope cooperativas`,
que lee de la administrativa las cooperativas activas con base propia y llama por
cada una al **mismo aprovisionador idempotente** que usa la API con
`AutoMigrate=true` (crea la base si falta, migra, siembra lo paramétrico). Si una
falla, sigue con las demás y sale con código 3: como Job PreSync eso aborta la
sincronización y los pods viejos siguen sirviendo. El Job de producción tiene
ahora tres pasos secuenciales —administrativa, operativa, cooperativas— con los
dos primeros como `initContainers`. Probado en local contra `coop_alfa` y
`coop_beta` (ambas «migrada hasta RetiroDeVoucherTypeIdSombraEnDocumentos», 0
filas nuevas) y en producción el 2026-09-11 con el migrador de `release 430ee49`:
los tres pasos en orden, «BD admin: sin migraciones pendientes», «BD operativa:
sin migraciones pendientes», «Cooperativas con base propia: 0 de 0 activa(s)».
Como `HookSucceeded` borra el Job al terminar, para leer sus pasos se lanzó una
copia del mismo manifiesto sin anotaciones de Argo y se borró después.

#### P15 — Feature 009 (contabilidad NIIF): E1 y E2 en `develop` y en **producción** (`release ff25948` 2026-09-19 y `release 0a309f3` 2026-09-22); pendientes QA por rol y validación del contador (T096); E3/E4 en ramas posteriores

La **entrega E1 está en `develop`** (merge `6d0c89d`, 2026-09-15) y desplegada en **DEV y QA**
por el pipeline (`gitops` `9c614c8`, imágenes `api@8f36b0b7…`, `web@0c2fc64c…`). En los dos
namespaces la API aplicó `ContabilidadNiif` al arrancar y sembró los dos PUC (2.566 entradas),
205 rubros, 18 tipos de comprobante, 8 documentos cruce y los 30 permisos `Accounting.*`; las 29
tablas `ACC_*` nuevas están y ninguna heredada queda; `/health/ready` 200 y toda ruta contable
sin token responde 401. En QA, `coop_prueba` quedó migrada y sembrada igual. Antes del merge el
diagnóstico de libros dio 0 documentos y 0 movimientos en las tres bases. Pruebas: 1.036 sin
contenedores y 151 de integración con Docker (1 omitida). **Producción no se tocó.**

Lo que sigue exigiendo al dueño (memoria del proyecto y `specs/009-contabilidad-niif/tasks.md`):

1. ~~Designar el segundo revisor~~ Hecho el 2026-09-17: Jorman Copete, anotado en la cabecera de los
   dos archivos `*_ContabilidadNiif.cs` (marcador `MIGRACION-DESTRUCTIVA-APROBADA`). La migración retira 33 tablas heredadas (vacías en
   los tres ambientes según `diagnostico-libros.sql`) y **vacía `PAY_ConceptDefinitionAccounts`**.
2. **Validación de los dos PUC por el contador** (`puc-solidario.json` 2.110 entradas —el CUIF
   oficial desde el 2026-09-18, formato SIAC 2023-11-03; hasta entonces era una transcripción con
   695 códigos, varios inexistentes—, `puc-comercial.json` 1.869) con
   `POST /api/accounting/catalogs/{code}/validate` en QA (T096): bloqueante para producción. Al
   cambiar la versión del archivo, el arranque pone al día el catálogo sembrado y retira la
   validación anterior; el plan de una empresa se vuelve a copiar sólo si no tiene cuentas propias
   ni movimientos (queda en el log).
3. Tras desplegar, en cada cooperativa: iniciar la contabilidad, crear auxiliares, vincular
   EPS/ARL/fondos/cajas/bancos a su persona y **reparametrizar las cuentas por concepto de
   nómina** ([contabilidad-primer-ejercicio.md](contabilidad-primer-ejercicio.md)).
4. Escribir y correr las e2e contables (T056, T066, T077, T084, T091); `NominaE2E` ya usa
   `POST /api/accounting/setup/initialize` y las 151 de integración pasan.
5. QA manual por rol (administrador, Operador, sólo lectura) según `quickstart.md` §4; la
   interfaz de DEV/QA está detrás de Cloudflare Access y la recorre el usuario.

Producción sólo con «sí, empujalo», `pg_dump` previo y el diagnóstico de libros vacíos.
E2 (consultas, cierres, apertura), E3 (cartera, inventario, tesorería, CDT sobre el
contrato) y E4 (conciliación, impuestos, exógena, activos) van en ramas posteriores.

**E2 consultas e informes + presupuesto — hecha en la rama `009-e2-consultas-presupuesto`
(2026-09-20), sin merge a develop ni despliegue hasta que el dueño lo pida.** Pedido del dueño
tras revisar el módulo: los reportes contables de `/reportes` no funcionaban (cinco tarjetas a
rutas sin página), no había presupuesto ni consulta consolidada del tercero. Entrega: US5
completa (T097–T107) y US9 (T137–T140): trece vistas en `/api/reports/accounting/{vista}`
sobre `MovimientosContables` (único punto de lectura del libro), libro auxiliar con
profundización hasta la línea, balance de prueba, libros diario y mayor, relación de
comprobantes, estado de cuenta del tercero, documentos cruce pendientes, saldo diario promedio,
ESF/ERI/ECP/EFE por rubro NIIF con comparativo, presupuesto versionado en pesos con ejecución;
pantallas `/contabilidad/libro-auxiliar`, `/informes`, `/estados-financieros`, `/terceros`,
`/presupuesto`; Centro de Reportes y manual apuntando a rutas reales (la prueba de arquitectura
ahora también revisa los `NavigateTo` del centro y `ManualCatalogoTests` cada ruta y slug del
manual, que destapó 16 temas contables heredados rotos). Revisión adversarial de tres lentes con
19 hallazgos confirmados y corregidos (los graves: el cierre de años anteriores no formaba el
saldo inicial; los rubros se medían por la naturaleza de la cuenta y 3510/1899/6220 descuadraban
el ESF; el EFE anual restaba el resultado del año anterior; rangos de fecha sin tope; el
presupuesto sin alcance de sucursal). Pruebas: 1.231 sin contenedores (+166) y 21 e2e nuevas en la
colección «Contabilidad e2e» (191 de integración con Docker: 190 pasan, 1 omitida), que
destaparon dos defectos ajenos al módulo, ya corregidos: los emisores explícitos de auditoría
(contabilidad y nómina) escribían con el Id interno de la cooperativa en vez del PublicId y sus
eventos nunca llegaban a la consola; y el vínculo sucursal contable → oficina
(`COR_Branches.TenantBranchPublicId`), del que depende el alcance de sucursal, no lo escribía
ningún comando (ya lo aceptan `POST/PUT /api/core/branches`; falta el campo en la pantalla de
Agencias). Receta:
[contabilidad-primer-ejercicio.md](contabilidad-primer-ejercicio.md) §7b y §7c.

**E2 completa — US6 cierre del ejercicio y US13 saldos de apertura, más las e2e que E1 dejó
pendientes (2026-09-21, misma rama, tras traer `develop` con la feature 010).** El cierre
(`POST /api/accounting/periods/years/{year}/close`, permiso `Accounting.Periods.CloseYear`) exige
los doce meses cerrados, el anterior cerrado y la cuenta de resultado del ejercicio, y genera por el
contrato un `CI` del 31/12 (`Kind = Closing`, en período cerrado: el poster lo admite sólo por su
clase) que cancela las clases 4 a 7 por sucursal y centro de costo y lleva el excedente a la cuenta
de resultado; reabrir con motivo lo reversa en su misma fecha y clase y deja los meses cerrados; el
`CI` no se reversa desde Comprobantes. La apertura (`/api/accounting/opening`, pantalla
`/contabilidad/apertura`, permiso `Accounting.Opening.Manage`): plantilla xlsx con los encabezados
en la fila 1, importación que valida cada fila con las reglas de cuenta y con un error no guarda
nada, borrador `AP` fechado la víspera del primer período, una sola vigente hasta reversarla; el
dueño decidió saldos digitados para COOFLOPAL y la digitación también entra por `POST /drafts` con
tipo `AP`. Las reglas de línea conocen ahora la clase del documento (al cierre no se le exige
cuenta habilitada, tercero, cruce ni base; a la apertura sólo se le perdona el módulo). E2E nuevas:
`CierreDeEjercicioTests` y `AperturaTests` en cooperativas aisladas del mismo host (primer
ejercicio 2025 y 2026), `ContabilidadNiifTests` (T056, T066, T077, T091 sobre la compartida) y
`ContabilidadDeNominaTests` (T084, colección Nómina, junio y julio de 2027). Los emisores de
auditoría, que `develop` había corregido por su lado el mismo día, ya no caen al Id interno ni
siquiera sin cooperativa activa (era una base fantasma). Pruebas: 1.656 sin contenedores (+177
sobre la 010) y 211 de integración con Docker (210 pasan, 1 omitida). Receta:
[contabilidad-primer-ejercicio.md](contabilidad-primer-ejercicio.md) §7a. Tareas: T109–T118 y las
cinco e2e marcadas en `specs/009-contabilidad-niif/tasks.md`; de E1 y E2 sólo queda **T096**, que
es del dueño.

**Lo que sigue pendiente (del dueño o de otra rama)**: ~~merge a `develop` y despliegue~~ hecho el
2026-09-22 (`develop` `857d0e5`, `release 0a309f3`, arriba); QA manual `quickstart.md` §4 y §5 E2 por rol; **validación de los
dos PUC por el contador** (T096, bloqueante para usar la contabilidad en producción) y de que en el
CUIF las contras de activo (1408, 1499…) y los gastos por deterioro/depreciación (5115, 5120, 5415,
5420) estén alineados para que la «Diferencia» del EFE sea cero; definir en cada cooperativa la
cuenta de resultado del ejercicio (una auxiliar bajo 3505, donde el CUIF no trae hijos) antes del
primer cierre anual; el campo `tenantBranchPublicId` en la pantalla Maestros › Agencias; los eventos
de auditoría explícitos ya escritos en producción quedaron en bases Mongo `…_Audit_<id interno>` y
no se ven en la consola salvo que se migren; `RendimientoDeInformesTests` (SC-008, un millón de
líneas) sin escribir; MAUI no registra los clientes contables (pantallas contables inertes en la app
de escritorio, desde E1). E3 (cartera, inventario, tesorería y CDT por el contrato) y E4
(conciliación, impuestos, exógena, activos) van en ramas posteriores.
#### P16 — Feature 010 (nómina completa): N1, N2 y N4 en `develop` y en **producción** (`release c388a17`, 2026-09-22); N3 pendiente

La **entrega N1** —prima de servicios, cesantías e intereses del año, vacaciones y liquidación
definitiva, con políticas por empresa, festivos, saldos iniciales y ficha PILA/DIAN— está
**completa en la rama `010-nomina-prestaciones-pila-dian`** (desde `develop` `5ccb695`; spec-kit
completo: spec, clarify, plan con D-01..D-41, 153 tareas; T001–T075 cerradas el 2026-09-21).
Se construyó por olas en worktrees, se revisó con seis lentes adversariales y dos refutadores
por hallazgo (49 hallazgos únicos, 44 confirmados y corregidos en seis lotes —entre ellos
salario del último tramo pagado dos veces por la definitiva y la ordinaria, saldo inicial
sumado en vez de reemplazado, ajuste de provisión de vacaciones sobre toda la provisión,
recaudo de Cartera anidado en la transacción, AccountingAuditEmitter con el Id interno del
tenant—) y quedó verde: 200 Domain, 1.035 Application, 106 Architecture, 70 Shared y 161 de integración (160 pasan, 1 omitida)
con Docker (colección «Nomina e2e» con las cuatro e2e nuevas). La e2e completa atrapó además
un defecto que las aisladas no veían (empleado sin provisión acumulada dejaba la provisión en
negativo). Manual: [liquidaciones-especiales.md](../manual/liquidaciones-especiales.md); runbook:
[nomina-primer-periodo.md](nomina-primer-periodo.md) §4d. **`develop` y los ambientes no se han tocado.**

**Entrega N4 — dispersión bancaria** (2026-09-21, en la rama, sin merge): a pedido del dueño
(«deja la funcionalidad de dispersión configurada asociada a los bancos, para que más adelante se
puedan implementar los planos de los demás bancos y se pueda utilizar en contabilidad y tesorería»)
los formatos de archivo quedaron en **Core** (`COR_BankFileFormats`, ligados a `COR_Banks`, con
ámbito y vigencia; D-42) y el motor `FlatFileWriter` es genérico; nómina genera desde las cinco
relaciones de pago, «marcar enviado» paga a todos en una transacción y bloquea la reversa, y la
consignación de cesantías por fondo ya escribe con el mismo motor. D-10 quedó resuelto:
`COR_Banks.TransferCode` es el código ACH. Verde: 1.431 sin contenedores y la e2e
`DispersionTests` (flujo completo por HTTP; 162 de integración, 161 pasan, 1 omitida). Manual:
[dispersion-bancaria.md](../manual/dispersion-bancaria.md); runbook §4e. Migración aditiva
`NominaDispersionBancaria`. **Lo que aporta el dueño**: el layout real de AV Villas Empresas
(T147) se carga como dato en Maestros › Formatos bancarios; si exige un origen que no exista,
eso sí es programa. Los códigos ACH de los bancos de las fichas se digitan en Maestros › Bancos.

**Entrega N2 — PILA y procedimiento 2** (2026-09-21, en la rama, sin merge): planilla de aportes
con el layout de la Res. 2388 como dato versionado (sin cotejar todavía: alerta
`Pila.LayoutSinCotejar` hasta T094), motor puro con casos dorados byte a byte, validación con la
taxonomía del operador, generación versionada, cuadre contra la nómina antes de descargar y marca
de cargada; porcentaje fijo del art. 386 con explicación mes a mes y aprobación que cierra la
vigencia anterior. Verde: 1.479 sin contenedores y 164 e2e (163 pasan, 1 omitida). Migración
aditiva `NominaPilaYNominaElectronica` (con las tablas de N3). Runbook:
[pila-primera-planilla.md](pila-primera-planilla.md); manual:
[retencion-procedimiento-2.md](../manual/retencion-procedimiento-2.md). **Lo que aporta el
dueño**: cotejar el layout con el anexo v30 y una planilla pagada (el registro tipo 1 suma 358 y
el anexo declara 359; decimales de las tarifas; código del operador) y pasar el `.txt` por el
validador de Aportes en Línea con la cuenta de COOFLOPAL (SC-004); códigos PILA de EPS, fondos,
ARL y cajas; confirmación 8h de la contadora (secuencia del procedimiento 2). Dos hallazgos que
convienen saber: la nómina ordinaria redondea la ARL al múltiplo más cercano y la planilla al
superior (Decreto 780 art. 3.2.1.5) —la diferencia se muestra y se reconoce; si se quiere cero,
la política `Payroll.Rounding`—, y la exoneración del art. 114-1 se decide por lo devengado, no
por el IBC (un integral de 20 M no queda exonerado).

Lo que sigue y a quién le toca:

1. **Usuario**: confirmar la rama y autorizar el merge a `develop` (T076); el pipeline la lleva
   a DEV/QA y allí se verifica contra la base (`PAY_PayrollRuns.Kind = 0` en las previas y el
   mismo conteo, tablas nuevas, 19 recursos `Payroll.*` en `SEC_Permissions` —los siembra la
   API al arrancar, no el DbMigrator—, semillas 72–75, `legal-parameters/missing?process=Settlements`
   vacío). Nota: `NominaPrestacionesYDian` se corrigió en la rama antes de salir; sólo las bases
   locales la aplicaron con la versión anterior y ya se alinearon a mano.
2. **QA manual por rol** (`quickstart.md` §4) y **validación de la contadora** (SC-001: prima,
   cesantías e indemnización reconstruidas desde la explicación; confirmaciones 8a, 8b, 8d, 8e,
   8i, 8j de `research.md`, más D-31: días del disfrute por calendario comercial). Bloqueante
   para producción (T077).
3. **Producción** (T078) sólo con «sí, empujalo»: `pg_dump -Fc` por cooperativa y de
   `ingenia365erp_admin`, segundo revisor de las dos migraciones (aditivas) en su cabecera, y
   en `cooflopal` digitar antes lo de §4d del runbook (cuentas de los 16 conceptos, políticas,
   festivos, saldos iniciales al 30-11-2026 validados).
4. **N3** (nómina electrónica DIAN con el servicio central sin estado, modo «software propio»
   primero, T108–T135) sigue en la misma rama o en una rama hija; sus tablas ya existen por la
   migración de N2 (D-12). N2 (T079–T093, T095–T105) y N4 (T136–T146) ya están en la rama; T094
   (cotejo del layout y validador del operador), T106–T107 y T147 son del dueño.
   Lo que el dueño debe aportar antes: layout del archivo de AV Villas (se carga como dato), registro de cada
   cooperativa en el catálogo DIAN (SoftwareID, PIN, TestSetId) con su certificado y set de
   pruebas aceptado, acceso al validador de Aportes en Línea, SMMLV/UVT 2027.
5. Deuda conocida de N1 (en `plan.md` D-29 y el manual §9): los aportes patronales y
   provisiones del último tramo de una definitiva no los calcula ninguna corrida (N2 los toma
   de la corrida `Settlement`); los devengos variables de la definitiva no entran al promedio
   de su propia prima/cesantías; el archivo de consignación por fondo ya sale con el motor de N4 cuando el fondo tenga formato; recaudo en
   Cartera probado sólo en Application.Tests hasta que exista desembolso por HTTP.

### 🟡 Prioridad media

#### P4 — Sellado mensual regulatorio suspendido

El CronJob `mongo-sellado-mensual` (PDN) exporta el mes cerrado en **JSON legible
sin herramientas de Mongo** y lo bloquea con Object Lock en modo **COMPLIANCE**
por 5 años. Está `suspend: true`.

**Antes de activarlo**: piloto con retención de **1 día** sobre un prefijo de
prueba. COMPLIANCE no admite marcha atrás **ni para el dueño de la cuenta**; un
error de escala se paga completo durante cinco años.

Incluye `accessLogs` además de `auditLogs`: esa colección tiene TTL de 90 días en
el código, así que sin el sellado la trazabilidad de **quién consultó** datos de
asociados no existiría cuando la pidan.

#### P5 — DEV y QA nunca se abrieron en un navegador

Se validó que los pods quedaran `Ready` y que `/health/ready` respondiera. **Eso
no prueba que la interfaz funcione**: exactamente así sobrevivió hasta producción
el fallo de `blazor.web.js`, con el pod sano y la página en blanco.

Abrirlos por la malla y recorrer el login y algunas pantallas.

#### P6 — Ensayo de restauración periódico

Ambas restauraciones se probaron una vez. Un respaldo que no se restaura hace un
año vuelve a ser una suposición.

Manifiesto listo para repetir: `infrastructure/pdn/ensayo-restauracion.yaml`.
Conviene repetirlo al cambiar de versión de motor. **Trampa documentada**: el
clúster de ensayo **no debe declarar `spec.plugins`**, o archivaría WAL contra el
mismo `serverName` que producción y corrompería su catálogo — el ensayo
destruiría el respaldo que valida.

#### P7 — El CI no verifica que las imágenes arranquen

Comprueba que **construyan**. Hoy aparecieron dos fallos que un CI verde no
detecta: el migrador compilaba pero moría al ejecutarse (`No frameworks were
found`), y la Web arrancaba mostrando una página en blanco.

Se agregó una comprobación puntual en el `Dockerfile` de la Web, pero un paso de
CI que ejecute cada imagen (`--help`, *health check*) cerraría la clase completa.

### 🟢 Prioridad baja

#### P8 — Deuda de pruebas del feature 002

10 pruebas de integración fallan por deuda previa (T128): esperan el contrato de
login anterior al cambio y comparten estado estático entre hosts de prueba.

#### P9 — Tareas abiertas del feature 004

- **T033**: prueba de integración de aprovisionamiento de tenant
- **T034**: verificación manual del reintento con la base caída

#### P10 — Riesgos de seguridad en 194.163.161.8

Documentados durante el relevamiento, fuera del alcance de este proyecto por
decisión explícita. Ver la sección correspondiente en
[despliegue-infraestructura.md](despliegue-infraestructura.md).

#### P11 — Cuota de GitHub Actions

Al pasar el repositorio a privado, las corridas consumen la cuota del plan Free
(2.000 min/mes). Hasta el 2026-09-12 cada corrida sumaba **~30 min de job** (5 de
pruebas + 24 de `docker-build` + gitops) y tardaba 28–36 min de reloj. Desde el
2026-09-13 el pipeline compila una vez y las imágenes sólo empaquetan (ver
[despliegue-infraestructura.md](despliegue-infraestructura.md), «Imágenes»): la
primera corrida midió **9 min 2 s de reloj** (`build-and-test` 5:04 y `publish-web`
7:38 en paralelo; tres `docker` de ~50 s en paralelo; `gitops` 10 s) y ~16 min de
job. El techo es el `publish` del Web: 83 s de compilación y **341 s** de ILLink +
Brotli/Gzip del cliente WebAssembly en el runner (eran 500 s con el meta-paquete de
Syncfusion). Si aprieta la cuota, un runner autoalojado en la VPS de nonprod resuelve
y además cumple el objetivo original de no depender de los límites de Actions.

#### P12 — SDK anclado a 10.0.302

`global.json` fija esa versión porque **el SDK 10.0.400 no genera
`blazor.web.js`**. No se investigó si es un cambio intencional que requiere
ajustar el código o una regresión. Revisar al actualizar en vez de quedar
anclados sin saber por qué.

#### P13 — Fase 2 de la feature 008: los demás módulos con persona

Vendedores (Inventario) sigue registrando sólo sobre persona existente, y Proveedores, Clientes y
Terceros se marcan únicamente desde Personas. La receta está en
`docs/manual/alta-de-persona-desde-modulos.md` (`PersonaDialog` con `RolSimpleInicial`, picker
con `PermitirCrear`). Quedan además dos buscadores ad-hoc de personas
(`CarteraFinanciera/Recaudos.razor`, `SolicitudCredito.razor`) que la prueba
`LaPersonaSeEscribeEnUnSoloSitio` lista como heredados: al migrarlos a `PersonSearchPicker` se
sacan de la lista. Los demás módulos que buscan personas por `/api/core/people?search=` o
`/by-code` (Contabilidad, Tesorería, Cartera) están fuera del alcance de esa prueba.

#### P14 — Motivo de terminación de contrato: texto libre sobre `varchar(4)` — ✅ cerrado el 2026-09-13

`PAY_Employees.TerminationCause` era el código de 4 caracteres de SOLIDO y la pantalla lo pedía
como texto libre: cualquier motivo real hacía fallar «Terminar contrato» con un 500
(`22001: value too long for type character varying(4)`). Se vio al escribir la e2e de la 008. Como
nada en la aplicación lo interpreta como código (el detalle del empleado lo muestra tal cual), la
columna pasó a 120 caracteres (migración en par `MotivoDeRetiroComoTexto`, en la misma rama),
`TerminateEmployeeCommand` tiene validador (un motivo más largo responde 422, no 500) y las dos
pantallas que lo piden limitan el campo a 120. El `Down` de esa migración recorta a 4 y perdería
lo escrito: respaldar antes de revertir.

### Pendientes de correo (diferidos por decisión del usuario)

#### C1 — El servidor de correo usa el certificado de fábrica

`mail.notifica365.com` presenta el certificado autofirmado que trae Poste.io de
serie: sujeto = emisor = `O=Poste.io, L=Susice, C=CZ`, sin ningún nombre
alternativo — ni siquiera nombra al servidor. MailKit lo rechaza, así que sin
intervención el ERP no podría enviar.

Como parche se fijó su huella SHA-256 en la configuración: acepta **ese**
certificado y ninguno más. No es lo mismo que desactivar la validación —eso
aceptaría cualquiera, y por esos correos viajan enlaces de invitación y de
restablecimiento de contraseña, que son credenciales de un solo uso.

**Lo correcto es instalar un certificado válido**; Poste.io trae Let's Encrypt
integrado. Al hacerlo hay que **borrar la huella** de los dos sitios donde está:

- `src/Presentation/IngenIA365ERP.API/appsettings.Development.json`, dentro del
  destino `Infraestructura:Smtp:Microsoft365`
- `workloads/erp/base/kustomization.yaml` del repositorio de despliegue

> **Trampa a tener presente**: si el certificado se renueva o se reemplaza y la
> huella no se actualiza ni se borra, el envío deja de funcionar. No falla en
> silencio —queda un error explícito en el registro con las dos huellas, la
> recibida y la esperada— pero nadie lo mira hasta que alguien reporta que no le
> llegan las invitaciones. Con un certificado válido el problema desaparece,
> porque la huella deja de usarse.

Para releer la huella si hiciera falta:

```bash
openssl s_client -starttls smtp -connect mail.notifica365.com:587 -showcerts </dev/null 2>/dev/null | openssl x509 -noout -fingerprint -sha256
```

#### C2 — `ingenia365.com` no tiene DMARC

Cualquiera puede falsificar correos desde ese dominio. Ya estaba señalado como
riesgo crítico en `docs/operaciones/migracion-dns-cloudflare.md`, y ahora pesa
más porque el ERP envía invitaciones y restablecimientos de contraseña.

Conviene revisar SPF, DKIM y DMARC de **`notifica365.com`** también, que es el
dominio que firma los envíos. Con un servidor autoalojado y sin esos registros,
lo esperable es que Hotmail y Gmail manden las invitaciones a spam — y el
síntoma que va a llegar es «no me llegó el correo», el mismo que originó toda
esta ronda.

### Pendientes de la mejora visual

#### V1 — `Microsoft.OpenApi` con vulnerabilidad de gravedad alta

La restauración avisa `NU1903` sobre `Microsoft.OpenApi 2.4.1`
(GHSA-v5pm-xwqc-g5wc). Afecta a `IngenIA365ERP.API`. Subir a la versión
corregida y comprobar que el documento OpenAPI se sigue generando.

#### V2 — MAUI no registra los clientes de `Services/Security`

`MauiProgram.cs` no registra `CentralAuthClient` ni ninguno de los demás, así
que las ~20 pantallas de seguridad —incluidas las nuevas de preferencias y
promociones— fallan al inyectarlos en la app de escritorio y móvil. Es una
condición anterior a esta mejora, pero ahora afecta a más pantallas.

#### V3 — El CI no compila MAUI

Se excluyó porque exige un workload pesado. Un cambio en `Shared` puede romper
la app sin que nadie se entere; hoy se verifica a mano. Evaluar un job aparte,
aunque corra sólo en la rama `release`.

#### V4 — Archivos huérfanos de OneDrive

154 archivos `.fuse_hidden*` (~1 MB) en `src/Presentation`, con el contenido
anterior a la migración de color. No están versionados, no los referencia ningún
proyecto y no compilan, pero conservan los literales que ya no deberían existir y
van a ensuciar cualquier auditoría por `grep`. Conviene borrarlos **desde el
explorador con OneDrive pausado**: la carpeta está sincronizada y el borrado se
propaga a la nube.

#### V5 — Página de inicio y favoritos guardados pero no consumidos

`ui.pagina-inicio` y `ui.favoritos` se guardan y validan en el servidor, pero
todavía ninguna pantalla los lee: falta redirigir tras iniciar sesión y pintar
los favoritos en el menú.

---

## 3. Lecciones operativas

Todos los fallos de la puesta en marcha compartieron una forma: **el sistema
reportaba éxito mientras estaba roto**.

| Fallo | Cómo se veía |
|---|---|
| Alertmanager descartando todo | Reglas activas, paneles poblados, cero avisos |
| Archivado de WAL sin permiso de etiquetado | 102 fallos en 45 min, ninguna alerta |
| La Web sin su script de arranque | Pod `Ready`, HTTP 200, pantalla en blanco |
| Etiqueta móvil con `IfNotPresent` | Argo `Synced`, imagen de 41 h corriendo |
| Cuota sin margen para el excedente | Argo `Synced`, `Progressing` eterno |
| Métrica de respaldo deprecada en cero | Una alerta escrita sobre ella nunca dispararía |

**Lo que funcionó para encontrarlos**: comparar *estado real* contra *estado
esperado* — el digest de la imagen, el contenido del manifiesto, el DOM
renderizado, el objeto en el bucket — en vez de confiar en que un comando dijo
"OK".

Dos reglas que conviene sostener:

1. **Una sonda que pide `/` no distingue una aplicación viva de una página en
   blanco.** Verificar comportamiento, no disponibilidad.
2. **Un proceso que oculta su propio fallo cuesta más de diagnosticar que uno que
   no existe.** Preferir siempre el fallo ruidoso.
