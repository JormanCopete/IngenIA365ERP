# Consolidación de las migraciones EF — propuesta para decidir

> **Estado: DECIDIDA, NO APLICADA.** El 2026-11-14 el dueño aceptó todas las recomendaciones de la §9. El día 0 (§8)
> **espera a que estén en develop** las dos ramas con migraciones sin integrar —`021-canal-asociados-pagos` y
> `claude/012-compra-directa-xml`, las únicas del repositorio con migraciones que develop no tiene— para que la base las
> absorba (decisión 3). Prototipo en la rama local `claude/consolidar-migraciones` (sin push ni merge), probado sólo en
> contenedores desechables de esta máquina; **nada de esto se ha ejecutado en DEV, QA ni PDN**. Cambia la regla vigente
> de CLAUDE.md («todo cambio de esquema es una migración nueva, nunca una regeneración», T0802 de la 013 y T225 de la
> 014) con el texto de la decisión 1, en el mismo PR del día 0, y en producción exige el Principio XII (respaldo y
> segundo revisor, que es el dueño, decisión 9).
>
> Fecha: 2026-11-14. Base del prototipo: develop `c1388817`.

## 1. El problema, medido

Cada proyecto de migraciones (`IngenIA365ERP.Persistence.Migrations.PostgreSql` y `.SqlServer`) tiene 211 archivos
`.cs`: **202,7 MB y 5,1 millones de líneas de C#**. Casi todo son los `*.Designer.cs`: cada migración lleva una foto
completa del modelo (de 1,8 a 3,4 MB), y hay 104 por motor (87 del árbol operativo y 17 del administrativo).

| Medida (esta máquina, Release, `--no-incremental`) | PostgreSQL | SQL Server |
|---|---|---|
| Compilar el proyecto de migraciones | 54,9 s | 57,8 s |
| Memoria pico del compilador (`csc`) | **14,1 GB** | **14,4 GB** |
| DLL resultante | 58,1 MB | 58,7 MB |
| Crear una base operativa vacía (87 migraciones) | 45,8 s | 66,7 s |
| Aprovisionar una cooperativa (crear, migrar, sembrar) | 53,4 s | 85,2 s |

En el runner de GitHub (15 GiB) compilar los dos a la vez agotaba la memoria: el publish llegó a 2–3 h y build-and-test
moría con «shutdown signal». El PR #74 los compila de a uno y bajó el publish a 9–12 min, pero el problema sigue
creciendo: el pico del compilador va con el tamaño del proyecto (14 GB con 202 MB en esta máquina) y **cada migración
nueva suma otra foto de ~3,4 MB por motor**, así que el margen frente a los 15 GiB del runner se gasta con cada
feature. En local, las e2e crean unas 160 cooperativas por corrida y cada una paga las 87 migraciones.

## 2. La propuesta

Reemplazar las 87 migraciones del árbol operativo de cada motor por **una migración base**, `EsquemaBase`, que crea
sola lo mismo que creaban ellas. Las 87 salen del compilado y **siguen en git**. Las bases que ya existen no ejecutan
nada: sólo reciben una fila en su historial.

### 2.1 Qué contiene la base

- **La migración que genera EF desde el modelo actual** (`20261114224300_EsquemaBase` en PostgreSQL,
  `20261114224314_EsquemaBase` en SQL Server): 664 `CreateTable` con sus índices y llaves. La instantánea que EF
  regenera al crearla es **idéntica byte a byte** a la de develop en los dos motores: el modelo no cambió.
- **`EsquemaBase.Complementos.cs`**: lo que hacían las absorbidas y el modelo no conoce, llamado desde el `Up()`:
  - la extensión `pg_trgm` (PostgreSQL), al principio, antes de los índices GIN con `gin_trgm_ops`;
  - **16 valores por defecto heredados**: columnas que las absorbidas agregaron con `defaultValue` y que el modelo no
    declara (`PAY_Employees.PayrollPlanId`, `COR_People.Username`, `RCD_ReceivedChecks.IsOpeningLoad`…). EF siempre
    manda esas columnas, pero un `INSERT` escrito a mano que las omita funciona hoy en las bases existentes; sin ellos
    fallaría en una cooperativa nueva. Salen de comparar catálogos (§4) y son las **únicas** diferencias de esquema que
    había;
  - **las 16 sentencias que sembraban filas fijas** (72 filas en 12 tablas: el plan de nómina por defecto, los
    arrendamientos de fondo, los participantes de integración y sus contratos, los `Setup` de Cartera, Recaudo y
    Tesorería, los tipos de documento de Recaudo, las categorías de flujo de caja…), copiadas tal cual de git y en su
    orden.

  Todo lo demás que esas migraciones hacían a mano no hace nada sobre una base nueva y quedó fuera: rellenos de columnas
  nuevas (`UPDATE` sobre filas que en una base nueva no existen), copias desde tablas que nacen vacías y las guardas de
  las migraciones destructivas.
- **`EsquemaBase.Absorbidas.cs`**: la lista de las 87 migraciones absorbidas de ese motor.

Los dos archivos de apoyo se **generan** con `tools/scripts/consolidar-migraciones/` desde git; los generadores son
deterministas (volver a correrlos reproduce exactamente lo del commit).

### 2.2 Cómo quedan marcadas las bases que ya existen

Una sola pieza, `ConsolidacionDeMigraciones` (`Persistence/Initialization`), mira el historial de una base y lo deja
como la consolidación supone. Se llama **antes y después de cada `MigrateAsync`** en los tres sitios que migran: el
arranque con `AutoMigrate` (`DatabaseInitializerHostedService`), el aprovisionador de cooperativas
(`TenantDatabaseProvisioner`, que usan el alta por la API, el arranque y `DbMigrator migrate --scope cooperativas`) y el
`DbMigrator` (`migrate --scope admin|tenants|all`). Escribe sólo en `__EFMigrationsHistory`, con el SQL que el propio EF
genera para su historial, y es idempotente.

| Estado del historial | Qué hace |
|---|---|
| Vacío o sin tabla (base nueva) | Nada antes; EF aplica la base; **después sella** las 87 absorbidas. |
| Tiene las 87 y no la base (vino por el camino viejo) | **Marca**: inserta la fila de la base. No ejecuta DDL. `[Migraciones.BaseConsolidadaMarcada]` |
| Tiene la base y le faltan absorbidas (sello interrumpido) | Inserta las que falten. `[Migraciones.AbsorbidasSelladas]` |
| Tiene la base y las 87 | Nada. |
| Tiene historial, le faltan absorbidas y no tiene la base (**a medias**) | **Se niega sin escribir**: `[Database.ConsolidacionIncompleta]`. Marcarla le escondería para siempre las migraciones que no tiene; se pone al día con la versión anterior y se vuelve a desplegar. |

**Por qué se sellan las absorbidas en una base nueva** (y no sólo la fila de la base): así **todas las bases quedan con
el mismo historial** —87 absorbidas más la base— vengan de donde vengan. Eso da dos cosas:

1. **Vuelta atrás**: la versión anterior a la consolidación se puede volver a desplegar sobre cualquier base, también las
   creadas después, sin que intente migrar nada (comprobado, §4.4).
2. **Los sembradores que preguntan por una migración por su nombre** siguen funcionando sin tocarlos: diez del árbol
   operativo (`CarteraNucleo`, `InventarioComercialNucleo`, `VentasYPuntoDeVenta`, `CarteraCobranza`, `PlataformaParaCartera`,
   `IntegracionContableDeRecaudoYTesoreria`…) no siembran si no la encuentran. Sin el sello, una cooperativa nueva
   nacería sin esas semillas.

### 2.3 El guarda, el DbMigrator y AutoMigrate

- **`PendingMigrationsGuard`** (arranque con `AutoMigrate=false`, sembrado): **sin cambios**. Una base sin marcar ve
  la base como pendiente y el arranque se niega, igual que hoy con cualquier migración pendiente. El Job PreSync del
  DbMigrator marca antes de que roten los pods, así que en un despliegue normal no se ve.
- **`EjecutorEnCooperativa`**: sin cambios; omite la cooperativa sin marcar hasta que se marque, como con cualquier
  pendiente.
- **`DbMigrator`**: `migrate` marca y sella (arriba). Comando nuevo **`consolidacion`**, de **sólo lectura**: dice, base
  por base (administrativa, operativa y cada cooperativa activa), en qué estado está; sale con **4** si alguna está a
  medias. Es el diagnóstico previo de cada ambiente.
- **`AutoMigrate=true`** (local, e2e): marca y sella solo al arrancar.
- **`DbMigrator script`** (script SQL idempotente para un DBA): genera la base. **No sirve para una base existente**
  sin antes insertar la fila de la base; hoy no se usa en ningún ambiente (todos usan el Job). Si se necesitara, es un
  agregado chico (el `INSERT` condicionado a tener las 87).

### 2.4 El árbol administrativo no se consolida

17 migraciones y 1,3 MB por motor (sus Designer son de 45 a 108 KB): consolidarlo no cambia nada medible y toca la base
con las identidades, las credenciales MFA y su traslado de datos. `ConsolidacionDeMigraciones` lo admite si algún día
hace falta (una subclase más de `MigracionesAbsorbidas`).

### 2.5 Las pruebas

| Prueba | Cambio |
|---|---|
| `LasMigracionesCongeladasNoSeRegeneran` | Fija ahora las huellas de la base, su Designer, sus complementos y su lista (8 en vez de 148), que las dos listas nombren las mismas 87, y que **ninguna migración operativa se ordene antes de la base** (una rama vieja que se integre después correría primero en una cooperativa nueva, sobre una base sin tablas). |
| `PrincipioXII_MigracionesDestructivas` | Las cuatro pruebas de los retiros destructivos históricos (cartera, captaciones, centro de costo del asociado, tesorería) se reemplazan por «la base sólo crea». La guarda general sigue mirando toda migración nueva. |
| `LaTesoreriaHeredadaNoVuelve`, `LaCarteraHeredadaNoVuelve` | Miran todos los Designer vigentes (la base y las que vengan) y la instantánea, en vez de «desde el retiro». |
| `LasMigracionesDe017SonAditivas` | Queda la administrativa (`IdentidadConDocumento`); `UsuarioDeLaPersona` se absorbió. |
| `RetiroDeLosDoblesDePruebaEsSoloDatos` | Retirada (su migración se absorbió). |
| `MigracionYSemillaDicenLaMismaNormaTests` | Se retiran las dos que leían el archivo de `NominaPrestacionesYDian`; queda la del sembrador. |
| e2e de los retiros (tesorería, cartera, captaciones) | Se retiran las que bajaban la base a antes del retiro para probar la guarda (4 pruebas) y `SacarElRetiroAsync`; quedan las de rutas y pantallas por HTTP. |
| `RetiroDelInventarioHeredadoTests`, `ComercioAmpliadoMigracionTests` | Retiradas (recorrían el historial hacia atrás). |
| `MigracionIdempotenteTests` (017) | Queda «migrar otra vez no cambia nada» y comprueba el sello; se retiran el relleno y la reversa de `UsuarioDeLaPersona`. |
| **`ConsolidacionDeMigracionesTests`** (nueva, e2e) | Base nueva sellada; base «vieja» marcada sin ejecutar nada; sello interrumpido completado; base a medias rechazada sin escribir. Corre en el motor de `DB_PROVIDER`. |

Lo retirado protegía el paso **único** de las bases reales por esas migraciones; ya pasaron todas, y una base nueva nace
sin lo que ellas borraban.

## 3. Lo que no cambia

El modelo, las entidades, las configuraciones, los datos de las bases existentes, el Job PreSync, `add-migration.ps1`
(las migraciones nuevas se siguen generando en par, con marca posterior a la base), `check-migration-parity.ps1` y la
compilación del CI (sigue compilando las migraciones de a una; ahora tarda segundos).

## 4. Evidencia de equivalencia

Todo en contenedores desechables (`postgres:17`, `mcr.microsoft.com/mssql/server:2022-latest`), con el DbMigrator de
develop (camino viejo) y el de la rama (camino nuevo). La evidencia cruda (catálogos, datos, diferencias, extractos de
registro y mediciones) quedó en la carpeta de notas compartida de la sesión que encargó esto, en
`consolidacion-evidencia/`; los guiones para repetirla, en `tools/scripts/consolidar-migraciones/`.

### 4.1 Una base creada por cada camino tiene el mismo esquema y los mismos datos

| Comparación | PostgreSQL | SQL Server |
|---|---|---|
| Instantánea regenerada contra la de develop | idéntica | idéntica |
| Catálogo normalizado (tablas, columnas, tipos, nulos, defaults, identidad, restricciones, índices, secuencias, extensiones, funciones, disparadores, vistas) | **21.917 líneas, idénticas** | **21.247 líneas, idénticas** |
| Filas que dejan las migraciones (sin `PublicId`/`CreatedAt`/`UpdatedAt`/`RowVersion`) | **72, idénticas** (con sus `Id`) | **72, idénticas** |
| `pg_dump --schema-only` crudo | 746 líneas distintas: **todas** son el orden de columnas dentro de los `CREATE TABLE` de 91 tablas y los dos testigos aleatorios `\restrict` de `pg_dump` 17. Ordenando columnas, las 665 tablas y el resto del volcado son iguales. | — |

La primera comparación de catálogos (antes de agregar los valores por defecto heredados) dio exactamente 16 diferencias
en cada motor, todas del mismo tipo; son las que ahora restituye la base.

### 4.2 Una cooperativa aprovisionada por cada camino es la misma

`DbMigrator migrate --scope cooperativas` sobre una cooperativa registrada sin base (crea, migra y siembra), en cada
camino: **mismo catálogo y las mismas 6.255 filas** en los dos motores; sólo 4 filas de `COR_ApprovalPolicies` difieren,
y únicamente en el `PublicId` aleatorio de otra fila que referencian. Las dos sembraron 6.037 filas: los sembradores que
buscan migraciones por nombre las encontraron gracias al sello.

### 4.3 Una base que vino por el camino viejo no ejecuta nada y queda marcada

Copia de la base vieja (`CREATE DATABASE … TEMPLATE` / `BACKUP`+`RESTORE`) migrada con el DbMigrator nuevo, en los dos
motores: `[Migraciones.BaseConsolidadaMarcada] … tenía las 87 migraciones … se anotó la base en el historial sin ejecutar
nada`, `sin migraciones pendientes`, **ningún `CREATE`/`ALTER` en el registro**, catálogo **idéntico** al de la original y
88 filas en el historial. Una segunda corrida no hace nada.

### 4.4 Vuelta atrás

El DbMigrator **de develop** sobre la base creada por el camino nuevo y sobre la base marcada, en los dos motores:
`sin migraciones pendientes`, código 0.

### 4.5 Una base a medias se rechaza

Una copia de la base vieja sin la última absorbida en su historial: `consolidacion` la informa `Incompleta` (sale con 4);
`migrate --scope cooperativas` marca las demás y en ésa lanza `[Database.ConsolidacionIncompleta] … faltan 1 … la
primera: 20261113005937_CodificacionDeProductos` sin escribir (el historial sigue con 86 filas) y sale con 3, que en el
Job PreSync aborta la sincronización.

### 4.6 Pruebas

- Arquitectura: 894 de 894. Application: 7.515 de 7.515.
- e2e, subconjunto que ejercita el camino nuevo de punta a punta (arranque con AutoMigrate, cooperativas por la API, la
  prueba nueva): **6 de 6 en PostgreSQL y 6 de 6 en SQL Server**.
- e2e completa en PostgreSQL: §4.7. La completa en SQL Server **no se corrió** (queda para el día 0, §8).

### 4.7 Suite e2e completa en PostgreSQL

787 pruebas en 1 h 25 min (Debug, con otra suite corriendo a la vez en la máquina): **750 pasan, 20 omitidas (las de
siempre: volumen, variables de entorno), 17 fallan, y ninguna por la consolidación**:

- **13 fallan igual en develop**: son exactamente las 13 fallas de una corrida completa de develop (`ea955e02`) hecha el
  mismo día en esta máquina por otra sesión (Inventario: compra directa, RADIAN, ensayo de I1, informes; Cartera: SICSES,
  recaudo por nómina, informes K1, castigo; contabilización por mensajes; aprobación multinivel).
- **4 fueron de carga** y pasan aisladas en la rama (15 de 15, la clase entera de alertas incluida): la concurrencia de
  la facturación electrónica, una de alertas de plataforma y dos de ahorro contractual. Estas dos últimas cayeron en un
  momento en que el PostgreSQL del contenedor no aceptaba conexiones (`57P03`, recuperándose de un proceso caído), con
  dos suites corriendo a la vez en una VM de Docker de 16 GB.

Cada cooperativa que crea la suite nace de la base y queda sellada: 303 altas, todas `migrada hasta
20261114224300_EsquemaBase`. Las filas sembradas van de 6.137 a 6.179 según lo que cada prueba pide al darla de alta, con
la misma distribución que en el registro de develop (151 y 215 altas con 6.137, 59 y 61 con 6.141, 19 y 19 con 6.175…).

La otra sesión crea las cooperativas de prueba copiando una base plantilla migrada una vez por host (11–17 s de migrar
las 87 en el proceso de la API). Las dos ideas se suman: con la consolidación, esa plantilla también se migra más rápido.

## 5. Mediciones

| | Antes (develop) | Después (base) |
|---|---|---|
| Código C# por proyecto de migraciones | 202,7 MB, 5,1 M líneas, 211 archivos | **11,6 MB**, ~258 mil líneas, 41 archivos |
| DLL de migraciones | 58,1 / 58,7 MB | **10,0 / 10,0 MB** |
| Compilar en Release (PostgreSQL / SQL Server) | 54,9 s / 57,8 s | **15,8 s / 14,6 s** |
| Memoria pico del compilador | 14,1 / 14,4 GB | **1,5 / 1,7 GB** |
| Crear una base operativa vacía (PostgreSQL / SQL Server) | 45,8 s / 66,7 s | **16,0 s / 18,2 s** |
| Aprovisionar una cooperativa (PostgreSQL / SQL Server) | 53,4 s / 85,2 s | **22,7 s / 29,6 s** |
| Marcar una base existente | — | ~8 s (casi todo es arrancar el proceso y construir el modelo) |
| Alta de una cooperativa en las e2e (API ya arrancada; mediana) | 14,4 s (Release, 30 altas) | **5,8 s** (Debug y con carga, 303 altas) |

Medido en Windows 11 (16 núcleos, 63 GB), sin compilación compartida (`UseSharedCompilation=false`). En el runner de
GitHub (4 núcleos, 15 GiB) no se midió: lo esperado es que el paso que compila las migraciones de a una baje de 6–9 min
a menos de un minuto, y que el pico deje de depender de cuántas migraciones haya hasta la próxima consolidación. Se mide
en la primera corrida del CI después de integrar.

## 6. Diferencias conocidas que se aceptan

- **Orden físico de las columnas** en 91 de 665 tablas: las bases viejas tienen al final las columnas que se agregaron
  después; una cooperativa nueva las tiene en el orden del modelo. Nada del código depende de eso (se buscaron
  `INSERT … VALUES` sin columnas, `SELECT *`, `COPY` y `BULK INSERT` fuera de las migraciones: no hay). `pg_dump`/
  `pg_restore` usan nombres de columna. El riesgo que queda es un `INSERT` escrito a mano sin lista de columnas.
- **El `ProductVersion` de las filas selladas** es el de EF al sellar (10.0.15), no el original de cada migración. EF no lo
  usa para decidir nada.

## 7. Riesgos y cómo se cubren

1. **Ramas con migraciones que nacieron antes de la base.** Hoy hay tres sin integrar, todas con marca **anterior** a la
   base del prototipo: `021-canal-asociados-pagos` (`20261108190612_CanalDigital`, `20261114042152_SolicitudDesdeElCanal`)
   y `claude/012-compra-directa-xml` (`20261114154126_EquivalenciasDelProveedor`). Integradas después, correrían antes que
   la base en una cooperativa nueva. La prueba `Ninguna_migracion_operativa_se_ordena_antes_de_la_base` lo impide. Hay
   que integrarlas **antes** de consolidar (la base las absorbe) o regenerarlas después con marca nueva (decisión 3).
2. **Una base a medias en algún ambiente.** Se ve antes de desplegar con `DbMigrator consolidacion` (sólo lectura); si
   aparece, se pone al día con la versión anterior.
3. **Volver a crecer.** Cada migración nueva vuelve a sumar ~3,4 MB por motor. El mecanismo admite reconsolidar: la
   próxima base absorbe a ésta y a las que vengan (la lista crece; las bases marcadas con ésta tienen todas). Propuesta:
   reconsolidar cuando el árbol pase de ~30 migraciones o una vez por trimestre (decisión 8).
4. **Migraciones en vuelo durante el despliegue.** Desde el día de la consolidación hasta que llegue a PDN no se integra
   ninguna migración nueva (congelamiento corto).
5. **Pruebas retiradas.** Cubrían el paso único de las bases por migraciones que ya corrieron en todas; siguen en git.

## 8. Pasos DEV → QA → PDN (cuando el dueño decida)

**Día 0, local** (una sesión, sobre el último develop):

1. Integrar o regenerar las ramas con migraciones pendientes (decisión 3). Congelar las migraciones en develop.
2. Regenerar la base con `tools/scripts/consolidar-migraciones/README.md`, comprobar la equivalencia con sus guiones en
   los dos motores (catálogos y datos idénticos, base vieja marcada sin DDL, vuelta atrás) y actualizar las huellas.
3. Suites sin contenedores verdes; e2e completa en PostgreSQL y en SQL Server.
4. Etiquetar el último commit con las 87 en el árbol (`migraciones-historicas-2026-11`, decisión 4). PR a develop con la
   evidencia; CI verde, anotando el tiempo de su paso de migraciones.

**En cada ambiente, DEV primero, después QA, después PDN** (con la imagen del DbMigrator de la versión consolidada):

1. **Respaldo** (Principio XII), inmediatamente antes: `pg_dump -Fc` de la base administrativa, la operativa y **cada
   base de cooperativa** a `/root/respaldos/<base>-AAAAMMDD-pre-consolidacion.dump`, con su tamaño y la cabecera
   `PGDMP` anotados, como en los despliegues anteriores (por ejemplo [captaciones-despliegue.md](captaciones-despliegue.md) §1).
2. **Diagnóstico de sólo lectura**: `DbMigrator consolidacion` contra el ambiente (un pod efímero con la imagen nueva y
   las variables del Job). Esperado: `admin: NoAplica`, operativa y cada cooperativa `PorMarcar`. Si alguna sale
   `Incompleta` (código 4), **se para**: se migra con la versión anterior y se repite. Si una cooperativa existente sale
   `BaseNueva`, su base no existe, no responde o no tiene historial: también se para y se revisa (el Job la crearía).
3. **Desplegar** como siempre (sincronizar en Argo CD). El Job PreSync corre `migrate`: en su registro, una línea
   `[Migraciones.BaseConsolidadaMarcada]` por la operativa y por cada cooperativa, `sin migraciones pendientes`, y ningún
   `CREATE`/`ALTER`.
4. **Verificar**: `DbMigrator consolidacion` otra vez → todas `Alineada`; `/health` 200; en DEV y QA, crear una cooperativa
   de prueba y comparar su catálogo con el de una existente (`catalogo-pg.sql`): idénticos salvo el orden de columnas.
5. **PDN además**: autorización expresa del dueño para la fecha, segundo revisor del PR anotado en su descripción
   (Principio XII), y una ventana de poco uso.

**Vuelta atrás**, en el orden en que conviene intentarla:

1. **Código**: volver a desplegar la versión anterior (las imágenes de antes). Funciona sobre las bases marcadas y sobre
   las creadas después (historial sellado) sin migrar nada (§4.4).
2. **Historial**, sólo si se quiere dejar cada base exactamente como antes: `DELETE FROM dbo."__EFMigrationsHistory"
   WHERE "MigrationId" = '<id de la base>'` en cada base marcada. La versión anterior no lo necesita.
3. **Respaldo**: restaurar el `pre-consolidacion.dump` de una base sólo si algo más se dañó (la consolidación no cambia
   datos ni esquema de una base existente).
4. **Repositorio**: revertir el merge; las 87 vuelven del historial de git.

## 9. Decisiones del dueño

**Tomadas el 2026-11-14: el dueño aceptó todas las recomendaciones.** En resumen:

| # | Decidido |
|---|---|
| 1 | Se consolida; CLAUDE.md cambia con el texto de abajo en el PR del día 0 (con la fecha real). |
| 2 | Sólo el árbol operativo. |
| 3 | Primero se integran la 021 y la compra directa por XML; después se consolida todo junto. |
| 4 | Las 87 salen del árbol y quedan en git con la etiqueta `migraciones-historicas-2026-11`. |
| 5 | Se conservan los 16 valores por defecto heredados. |
| 6 | Se sellan las absorbidas en las bases nuevas. |
| 7 | Se acepta el retiro de las pruebas de la §2.5. |
| 8 | Se reconsolida al pasar de ~30 migraciones o cada trimestre. |
| 9 | El diagnóstico `consolidacion` se corre a mano antes de sincronizar cada ambiente. El **segundo revisor** para PDN es el dueño (como en la 014, D15). |

Lo que se planteó:

1. **¿Consolidar?** Exige cambiar la regla de CLAUDE.md sobre migraciones congeladas. Texto propuesto para reemplazar las
   menciones de T0802/T225: «Desde la consolidación del AAAA-MM-DD el árbol operativo empieza en `EsquemaBase` (una por
   motor), que absorbió las 87 anteriores (siguen en git, etiqueta `migraciones-historicas-2026-11`). Lo congelado es la
   base, su Designer, sus complementos y su lista de absorbidas; todo cambio de esquema es una migración nueva, nunca una
   regeneración de la base. Las bases existentes sólo recibieron la fila de la base; las nuevas llevan sellado el historial
   completo. Se reconsolida según la decisión 8.»
2. **Alcance**: sólo el árbol operativo (**recomendado**) o también el administrativo.
3. **Cuándo, frente a las ramas con migraciones** (021 canal del asociado, 012 compra directa por XML): integrarlas
   primero y consolidar todo junto (**recomendado**: la base las absorbe y nadie regenera nada) o consolidar ya y que esas
   ramas regeneren sus migraciones con marca nueva.
4. **Los archivos de las 87**: fuera del árbol, en git con etiqueta (**recomendado**: el clon y el CI dejan de bajar
   ~400 MB de texto) o en una carpeta excluida del compilado.
5. **Los 16 valores por defecto heredados**: conservarlos en la base para que las cooperativas nuevas sean iguales a las
   existentes (**recomendado**; si se quieren quitar, una migración aparte para todas las bases a la vez).
6. **Sellar las absorbidas en las bases nuevas** (**recomendado**: da la vuelta atrás y mantiene los sembradores sin
   tocar). La alternativa es cambiar los diez sembradores para que pregunten por la tabla y no por la migración.
7. **Aceptar el retiro de las pruebas** de la §2.5.
8. **Reconsolidar periódicamente**: al pasar de ~30 migraciones o cada trimestre (**propuesta**), con el mismo
   procedimiento.
9. **Principio XII en PDN**: quién es el segundo revisor, y si el diagnóstico `consolidacion` se corre a mano antes de
   sincronizar (**recomendado**) o se confía en que el Job aborta ante una base a medias.

## 10. Observación aparte (no es parte de esta propuesta)

El Principio IV dice que el arranque de la API **nunca** migra las bases de las cooperativas existentes, pero con
`AutoMigrate=true` `DatabaseInitializerHostedService.PonerAlDiaLasCooperativasAsync` las aprovisiona (y migra) al
arrancar. En los ambientes del clúster `AutoMigrate` está apagado y lo hace el Job, así que en la práctica no ocurre fuera
de local; queda anotado para revisarlo aparte.
