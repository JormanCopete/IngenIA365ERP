# La plataforma de integración: mensajes, procesos, parámetros, aprobaciones y alertas

> Feature 012 (inventario comercial), fases 2 y 3 más la entrega I1. Complementa
> [contracts/mensajes.md](../../specs/012-inventario-comercial/contracts/mensajes.md) (el catálogo de
> mensajes, su sobre y sus estados), [contracts/api.md](../../specs/012-inventario-comercial/contracts/api.md)
> §7, §15 y §16 y [decisiones-transversales.md](../../specs/012-inventario-comercial/decisiones-transversales.md)
> T5–T12, T21, T33–T35 y T39. Aquellos son el contrato; esto es la receta para el módulo que quiera usar
> estas piezas (Tesorería, Cartera, lo que venga).

La 012 no sólo trae un inventario: trae cinco piezas de plataforma que están en `Application/Common` y
que no saben nada de inventario. Inventario es su primer usuario. Esta receta dice cómo se usan, qué
reglas no se negocian y **qué hay que abrir antes** de que otro módulo pueda usarlas, porque en I1 no
todas quedaron genéricas.

## 1. La regla en una frase

**Lo que un módulo le cuenta a otro nace como mensaje en la misma transacción que la operación; lo que
corre sin persona corre dentro de una cooperativa, con un actor que se llama «Proceso de integración».**
Un módulo no escribe las tablas de otro (Inventario no toca Contabilidad ni Cartera:
`InventarioNoConoceContabilidadNiCartera`) y ningún trabajo de fondo abre una base por su cuenta.

## 2. La bandeja de salida (T7–T9)

Tres tablas en la base de cada cooperativa:

| Tabla | Qué es |
|---|---|
| `COR_IntegrationMessages` | el mensaje: tipo, versión, `Kind` (`Business` o `Informational`), origen (módulo, clase, tipo, número, `OriginEventKey`), relacionado, sucursal, centro, tercero, `PayloadJson` y su `PayloadSha256`, quién lo originó. Hecho inmutable. |
| `COR_IntegrationMessageDeliveries` | una entrega por destino (`Accounting`, `Lending`), con su modo (`Online`, `Batch`, `NotPosted`, `Always`), su estado y sus intentos |
| `COR_IntegrationMessageDependencies` | las aristas: un mensaje no se entrega antes que el último de cada cadena de la que depende |

**El único que escribe la bandeja es `EmisorDeMensajes`** (`Application/Common/Integration`), y
**nunca guarda**: agrega el mensaje, sus entregas y sus dependencias a la unidad de trabajo del documento,
y el `SaveChanges` de la operación lo guarda todo junto. No hay operación confirmada sin sus mensajes ni
mensaje sin operación. El molde, tal como lo usa la confirmación de inventario (paso 9 del flujo canónico):

```csharp
var solicitud = new SolicitudDeEmision(
    origen,                        // OrigenDeEmision: clase y tipo como TEXTO, número visible, fecha, sucursal, tercero
    ClavesDeEvento.Confirmacion,   // la forma que admite el tipo (contracts/mensajes.md §10.1)
    contenidos,                    // records <Tipo>V1 del catálogo; todos forman una unidad por destino
    new ModoDeEntrega.Sellado(DeliveryMode.Online),   // o Heredado(originalPublicId) en un relacionado
    Relacionado: null,             // el documento que éste anula, corrige o ajusta
    ValidacionPrevia: resultado);  // lo que dijo la validación previa, sellado en cada mensaje
await emisor.EmitirAsync(solicitud, ct);   // no guarda
// … el resto de la operación …
await db.SaveChangesAsync(ct);             // documento + mensajes, o nada
```

Lo que el emisor comprueba, y por qué cada cosa es una **excepción** (un defecto del programa, no del
usuario):

1. cada contenido está en `CatalogoDeMensajesV1` —que dice su tipo, versión, destino y `Kind`— y la
   `OriginEventKey` tiene la forma que el tipo admite;
2. no hay doble emisión de `(OriginPublicId, Type, OriginEventKey)`, ni en la unidad de trabajo ni en la
   base; el índice único es la última defensa;
3. el origen es una **persona**: quien confirmó (con aprobación, el último aprobador) u ordenó. El actor
   de proceso **nunca** firma un mensaje;
4. el modo de un mensaje de negocio a Contabilidad: el sellado o, en un relacionado (una anulación, un
   derivado), el del original, copiado sin volver a leer el parámetro (FR-075, FR-079). Los informativos y
   todo lo de Cartera nacen `Always`/`Pending`;
5. la serialización con `OpcionesDeMensajes` y el hash sobre esos mismos bytes.

Los mensajes se versionan: un cambio de forma es un record `…V2` nuevo en el catálogo, nunca editar el
`V1` (§12 del contrato).

### Lo que en I1 todavía no está

> **Actualizado por la feature 013 (fase 2):** lo que sigue describe I1. El despachador llegó con I2 y el emisor dejó de ser de
> Inventario con la 013: ver la sección 9.

- **No hay despachador.** `DespachadorDeMensajes`, que lee `Integration:Dispatcher` y `Integration:Retries`
  y entrega por el camino común de MediatR, es de **I2**. En I1 los mensajes nacen, quedan `Pending` (o
  `InBatch`, o `NotApplicable` si el modo es «no pasa») y **nadie los entrega**. Son correctos y quedan
  esperando: cuando llegue I2, saldrán en orden. `ISenalDeMensajes` ya avisa desde el `SavedChanges` del
  contexto; nadie la escucha todavía.
- **El emisor es de Inventario.** `EmisorDeMensajes.ModuloDeOrigen` es la constante `"INV"` y el catálogo
  (`CatalogoDeMensajesV1`) vive en `Contracts/Inventory` con los veinte tipos del comercio. Para que
  Tesorería o Cartera emitan, hace falta **antes**: (a) que el módulo de origen venga en la solicitud (o en
  el `OrigenDeEmision`) en vez de ser constante; (b) un catálogo por módulo de origen, o uno común con los
  tipos de cada módulo, sin que el emisor dependa de los tipos de Inventario; (c) sus destinos en
  `IntegrationDestinations`. Es un cambio acotado al emisor y a la validación de la clave de evento; no hay
  que tocar las tablas.
- El consumidor contable (matriz de reglas, validación previa, comprobante por documento o por lote) es de
  I2 y vive del lado de Contabilidad; los mensajes a Cartera esperan la spec de Cartera (D-02).

## 3. Trabajos de fondo: `IEjecutorEnCooperativa` y el actor de proceso (T5, T6, T10, T47)

Todo lo que corre sin petición —despachar, reenviar la auditoría, tareas programadas, el correo— opera
**una cooperativa a la vez** por `IEjecutorEnCooperativa.EjecutarAsync(cooperativa, actor, origen,
trabajo)`. La implementación de la API (`API/Integration/EjecutorEnCooperativa`):

- crea un ámbito de servicios **nuevo por llamada**: el `ApplicationDbContext` y los comandos nacen y
  mueren con el trabajo, nunca compartidos entre cooperativas;
- fija el `ContextoAmbiental` (la cooperativa, su base, el actor) antes de crear el ámbito y lo restaura
  al terminar;
- **lanza** si hay `HttpContext`: una orden manual sólo encola, el trabajo lo corre el proceso;
- **salta** la cooperativa cuya base tiene migraciones pendientes (`ResultadoEnCooperativa.Omitida`, con
  aviso en el log) sin detener a las demás.

El actor de segundo plano es `Actor.ProcesoDeIntegracion(origen)`: `Kind = Process`, nombre «Proceso de
integración», sin IP, canal `Process`, y un origen que dice de qué se trata (`Mensaje:{id}`,
`Lote:{número}` o `Tarea:{nombre}`). No tiene fila en `SEC_Users` y no se le crea un usuario técnico. Un
lote que ordena una persona corre con esa persona, no con el proceso. `IActorActual` devuelve uno u otro;
**la segregación compara `Actor.UserId` (`SEC_Users.Id`), nunca el entero del token**
(`LaSegregacionNoUsaUserIdDelToken`; ver B1 en
[estado-y-pendientes.md](../operaciones/estado-y-pendientes.md)).

**Una tarea programada nueva** (lo más común para otro módulo):

```csharp
public sealed class MiRevisionDiaria : ITareaProgramada
{
    public string Nombre => "tesoreria.revision";               // va en el origen Tarea:{Nombre}
    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultima) =>
        ahoraLocal.TimeOfDay >= new TimeSpan(6, 0, 0) && ultima?.Date != ahoraLocal.Date;   // pura
    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var sender = servicios.GetRequiredService<ISender>();
        await sender.Send(new MiComandoDeRevision(), ct);        // escribe SÓLO por comandos
    }
}
// API/Program.cs, junto a las demás:
builder.Services.AddSingleton<ITareaProgramada, MiRevisionDiaria>();
```

`ProgramadorDeTareas` espera a que la base esté lista, recorre las cooperativas activas, toma el
arrendamiento `scheduled.tasks` de cada una (si lo tiene otra réplica, la salta) y corre cada tarea que
le toca en su propia llamada al ejecutor. Una tarea que lanza se registra y sigue la siguiente. **La
última corrida se recuerda por proceso**: tras un reinicio o un cambio de réplica la tarea puede volver a
correr, así que tiene que ser idempotente. En I1 corren tres: `inventario.integridad` (02:00),
`compras.eventos-radian` (06:00) y la revisión de reorden (desde `Integration:ReorderReview:StartHour`).

Reglas que fija `NingunTrabajoDeFondoOperaSinCooperativa`: todo `BackgroundService` está declarado (o
como trabajo por cooperativa o como excepción con motivo); el que usa `ISender`, `ITenantDirectory` o el
contexto de datos recibe el ejecutor y no guarda por su cuenta; `AddHostedService` sólo en
`API/Program.cs` (el DbMigrator no arranca trabajos). Cada trabajo se apaga con su
`Integration:*:Enabled`; la fixture de pruebas los apaga todos y conduce cada pasada a mano.

**Arrendamientos** (`COR_BackgroundLeases`, `IArrendamientos`): cinco nombres fijos sembrados por la
migración (`integration.dispatch`, `audit.forward`, `einvoicing.process`, `scheduled.tasks`,
`email.dispatch`). Evitan que dos réplicas hagan el mismo trabajo sobre la misma cooperativa, pero **la
exactitud nunca depende de ellos**: la dan el `RowVersion`, el recibo único del destino y la cabeza de la
cadena de auditoría. Un trabajo nuevo con arrendamiento propio exige agregar su nombre a
`NombresDeArrendamiento` y su fila en una migración.

**Los comandos de consumo no tienen ruta.** Registrar el resultado de una entrega, contabilizar mensajes o
un lote, levantar una alerta: los corre el proceso por `ISender`, nunca una persona por HTTP
(`LosComandosDeConsumoNoTienenRuta`).

## 4. Parámetros con vigencia (T21)

`COR_ParameterVersions` guarda, por (módulo, clave, ámbito), valores **con vigencia** (`ValidFrom`,
`ValidTo`). Cambiar un parámetro es agregar una vigencia nueva con motivo, nunca editar la anterior, y
todo cálculo lee el valor **a la fecha de la operación**.

- **Un solo lector**, `ILectorDeParametros.LeerAsync(modulo, clave, fecha, ambito, ambitoId)`: cae de la
  excepción del ámbito (bodega, tipo de documento, punto, caja) al general y del general al **defecto
  seguro** de la definición. Un valor guardado que la definición ya no admite es `Parameters.ValueNotAllowed`,
  nunca el defecto. Memoriza por petición.
- **Un solo escritor**, `AddParameterVersionCommandHandler` (`POST /api/inventory/parameters/{module}/{key}/versions` y
  `AgregarVigenciasAsync` para las plantillas que escriben vigencias en su misma transacción).
- Quien lea la tabla por su cuenta se salta la caída y la validación: `LosParametrosSeLeenEnUnSoloSitio`.

**Las claves son un catálogo cerrado** (`Domain/Common/Parametros/CatalogoDeParametros`): lo que no está
ahí no se puede registrar (`Parameters.KeyNotFound`). Hoy une tres módulos —`INV`
(`ParametrosDeInventario`), tributario y facturación electrónica—. Un módulo nuevo agrega su clase de
definiciones con `Eleccion`, `SiNo`, `Entero` o `NumeroDecimal` (valores admitidos, defecto seguro,
ámbitos, entrega desde la que tiene efecto) y la suma a `CatalogoDeParametros.Modulos` y `.Todas`.

Dos ganchos por módulo, cada uno con **una sola implementación registrada**:

- `IReglasDeParametros`: reglas antes de guardar (el método de costeo sólo cambia al inicio de un período
  abierto; el modo de paso se escribe por cadena; nada en período cerrado). Hoy la registra
  `ReglasDePlataformaDeInventario`. Un segundo módulo con reglas propias obliga a componerlas (un
  despachador por módulo), no a reemplazar la de inventario;
- `IResolutorDeAmbitoDeParametro`: traduce el `scopePublicId` de un ámbito a su Id interno respetando el
  alcance del usuario; lo implementa el módulo dueño de la entidad.

Los parámetros que son **valores legales** (UVT, tarifas) no van aquí disfrazados de configuración: el
comercio no tiene valores legales en el código (`ElComercioNoTieneValoresLegalesFijos`) y la UVT se lee
en un solo sitio (`LaUvtSeLeeEnUnSoloSitio`).

## 5. Aprobaciones y montos máximos (T33, T34)

Un solo motor para todo lo que pide aprobación: `IMotorDeAprobaciones` (`Application/Common/Approvals`),
sobre `COR_ApprovalPolicies` (+ `…PolicyLevels`, con vigencia y versión), `COR_ApprovalRequests` y
`COR_ApprovalDecisions` (hecho inmutable). El motor **no conoce los módulos**: decide con el evaluador puro
de la política y le pide a una **fuente** lo que es del módulo.

Para que un módulo pida aprobación:

1. Un **sujeto** (`ApprovalSubjects`: hoy `DocumentConfirmation`, `DiscountOverCap`, `ProvisionalCredit`,
   `TransferDiscrepancy`, `PurchaseMatchException`) y un **tipo de fuente** (`ApprovalSourceTypes`).
2. Una `IFuenteDeAprobacion` registrada (`services.AddScoped<IFuenteDeAprobacion, MiFuente>()`), que sabe:
   confirmar en la transacción del último aprobador repitiendo sus comprobaciones (`AlAprobarAsync`; si
   falla, la decisión no queda), devolver a borrador ante un rechazo o un retiro (`AlDevolverAsync`), decir
   si lo aprobado está al alcance de quien decide (`EnAlcanceAsync`) y describirse para la bandeja.
3. En el comando del módulo: `EvaluarAsync` (política vigente a la fecha de la operación —la del tipo gana
   sobre la general— y monto máximo del permiso) y, si hace falta, `SolicitarAsync` con la **huella** de lo
   que se aprueba (`HuellaDeOperacion.Calcular`): si el contenido cambia, la aprobación deja de valer. Ni
   evaluar ni solicitar guardan: la solicitud se guarda con la operación que la pide. Lo aprobado queda
   congelado y, en inventario, **sin número**.

La decisión (`POST /api/inventory/approvals/{id}/decide`) exige el permiso del nivel y el alcance (si no,
404), segrega por `SEC_Users.Id` (quien crea y quien pide no aprueban lo suyo), admite al aprobador
**presente** identificado con su passkey o su TOTP en el equipo de quien pidió
(`POST …/approvals/{id}/presence-challenge`), y el rechazo lleva motivo. Quien pidió o creó lo aprobado
puede retirarlo (`POST …/approvals/{id}/withdraw`) y vuelve a borrador.

**Montos máximos** (`SEC_PermissionAmountLimits`, `/api/inventory/amount-limits`): un tope en pesos por
(rol, permiso). Sólo los permisos de `PermisosLimitables` lo admiten (hoy `Inventory.Purchases.Confirm`,
`.Adjustments.Confirm`, `.Sales.Confirm`, `.Sales.SellOnCredit`). Si el monto supera el tope y la política
tiene un nivel, ese nivel se fuerza (`reason: AmountLimit`); si no hay nivel que forzar,
`Inventory.Approval.AmountExceedsLimit`. Otro módulo que quiera topes agrega sus permisos a esa lista.

## 6. Alertas (T39)

Un catálogo cerrado de tipos (`TiposDeAlerta`, sembrado por `AlertTypesSeeder` en `COR_AlertTypes`): la
cooperativa cambia destinatarios (permisos), canales y umbrales, **no inventa tipos**. Las alertas viven en
`COR_Alerts` y se entregan como **notificaciones** existentes (`COR_Notifications`, que ganó
`AlertPublicId`), y por correo si el tipo tiene ese canal.

- Levantar: `IAlertas.LevantarAsync(new AlertaALevantar(tipo, asunto, cuerpo, entidad…, DedupKey))` desde un
  comando que detecta la condición en su propia unidad de trabajo, o `RaiseAlertCommand` (sin ruta) desde un
  proceso. Con una pendiente de la misma condición (`DedupKey`), suma la ocurrencia en vez de crear otra.
- Destinatarios por permiso **y alcance** (`IDestinatariosPorPermiso`); sin nadie, llega a `CompanyAdmin`
  con `WithoutRecipient`, nunca se pierde.
- Cerrar: una persona la atiende con nota (`AttendAlertCommand`); un proceso, cuando la causa desaparece
  (`AtenderPorProcesoAsync`).

Un tipo nuevo es una entrada en `TiposDeAlerta.Todos` (código, módulo, qué la dispara, permisos
destinatarios, canales, severidad, entrega) y la semilla lo agrega sola.

## 7. Qué NO hacer

- No escribir `COR_IntegrationMessages` ni sus entregas fuera de `EmisorDeMensajes`; no guardar dentro del
  emisor ni emitir después del `SaveChanges`.
- No emitir un mensaje como «Proceso de integración».
- No leer ni escribir tablas de otro módulo para ahorrarse un mensaje.
- No correr trabajo de fondo fuera de `IEjecutorEnCooperativa`, ni guardar desde la tarea sin pasar por un
  comando.
- No leer `COR_ParameterVersions` a mano ni editar una vigencia.
- No comparar `ICurrentUserService.UserId` para segregar.
- No publicar ruta para un comando de consumo.

## 8. Dónde está cada cosa

| | |
|---|---|
| Bandeja | `Application/Common/Integration/{EmisorDeMensajes,SolicitudDeEmision,ClavesDeEvento,ClavesDeLote,ISenalDeMensajes}.cs`; catálogo en `Contracts/Inventory/CatalogoDeMensajesV1.cs` |
| Ejecución | `Application/Common/Execution/{IEjecutorEnCooperativa,Actor,ITareaProgramada,IArrendamientos,NombresDeArrendamiento}.cs`; `API/Integration/{EjecutorEnCooperativa,ProgramadorDeTareas,IntegrationOptions}.cs` |
| Parámetros | `Domain/Common/Parametros/CatalogoDeParametros.cs`, `Domain/Inventory/Parameters/ParametrosDeInventario.cs`; `Application/Common/Parameters/` |
| Aprobaciones | `Application/Common/Approvals/` (`IMotorDeAprobaciones`, `MotorDeAprobaciones`, `IFuenteDeAprobacion`, `PermisosLimitables`) |
| Alertas | `Application/Common/Alerts/` (`IAlertas`, `TiposDeAlerta`, `RaiseAlert`) |
| Configuración | sección `Integration` de `API/appsettings.json` (validada al arrancar) |
| Migración | `PlataformaParaInventario` (aditiva, par PostgreSQL / SQL Server) |
| Pruebas de arquitectura | `InventarioNoConoceContabilidadNiCartera`, `NingunTrabajoDeFondoOperaSinCooperativa`, `LosComandosDeConsumoNoTienenRuta`, `LosParametrosSeLeenEnUnSoloSitio`, `LaSegregacionNoUsaUserIdDelToken`, `LosHechosInmutablesNoSeModifican` |

## 9. Feature 013: varios emisores, el proceso como origen y el registro de participantes

> Feature 013 (cartera financiera), fase 2 (T10–T19; C16, C18, C35, C36; T0068–T0078, T0100). Contrato:
> [specs/013-cartera-financiera/contracts/mensajes.md](../../specs/013-cartera-financiera/contracts/mensajes.md) §1–§9, §13 y §14 y
> data-model parte 3 §4.6 y §4.11. Lo de esta sección **amplía** las secciones 2 y 3: nada de lo que hacía Inventario cambió de bytes
> (`ContratosDeMensajesTests` sigue igual).

### 9.1 La bandeja tiene varios emisores

- **El módulo de origen sale del tipo, nunca de la solicitud.** `TipoDeMensaje` declara `OriginModule` (`INV`, `CAR`, `TES`, `REC`;
  `ModulosDeOrigen`), sus `Destinos` y si `AdmiteProceso`. El catálogo es uno, **agregado**: `CatalogoDeMensajes` une
  `CatalogoDeMensajesV1` (los veinte de Inventario, sin mover), `CatalogoDeMensajesDeCarteraV1` (los hechos de Cartera a Contabilidad y lo
  que Cartera manda a Comercial, Tesorería y Recaudo) y `CatalogoDeMensajesHaciaCarteraV1` (lo que otros le mandan a Cartera).
  `ElCatalogoDeMensajesEsUnico` fija que `(Type, Version)` no se repita y que todo record de `Contracts/` esté en el catálogo.
- Una solicitud que mezcla módulos se rechaza (excepción: es un defecto del programa).
- Un tipo con varios destinos exige `SolicitudDeEmision.Destino`.
- `SolicitudDeEmision.CadenaDeOrden` pone el mensaje en la cadena de otro (`ChainRootPublicId`): así las respuestas de Cartera a
  Comercial salen en el orden de la venta que las originó.
- La constante `EmisorDeMensajes.ModuloDeOrigen = "INV"` ya no existe. El único escritor de la bandeja sigue siendo `EmisorDeMensajes`
  y sigue sin guardar (`SoloElEmisorEscribeLaBandeja`).

### 9.2 El proceso como origen (T11): la excepción a «el mensaje lo origina una persona»

La regla de la sección 2 (punto 3) sigue para todo lo de Inventario, Tesorería y Recaudo. El usuario de origen se resuelve así:

1. la **persona** que actúa;
2. si no hay, `SolicitudDeEmision.UsuarioDeOrigen` (la persona que ordenó, cuando el comando corre después);
3. si tampoco, el **proceso** (`OriginActorKind = Process`, `OriginUserCentralId` nulo, nombre «Proceso de integración»), **sólo** si el
   tipo tiene `AdmiteProceso = true`. Si no lo admite, el emisor lanza y no escribe nada.

`AdmiteProceso` es una lista cerrada: los tipos de Cartera de contracts/mensajes.md §6 (la causación diaria, los cierres, los tres
contingentes de A3…), y `false` en todo `INV`, `TES` y `REC` (`EmisionesDeProcesoPermitidas`). La migración `PlataformaParaCartera` agrega
`COR_IntegrationMessages.OriginActorKind` (`DEFAULT 1`, persona) y admite `OriginUserCentralId` nulo.

### 9.3 El registro de participantes y la habilitación de destinos (T16)

Un destino ya no «está» porque exista su consumidor: **cuenta** si tiene una fila en `COR_IntegrationParticipants` habilitada y vigente a
hoy, su implementación está en el despliegue con la versión que adoptó, y no es un doble de prueba en producción. Lo lee
`IRegistroDeIntegracion`; lo escribe `EscritorDelRegistroDeIntegracion`, que agrega sin guardar (la activación de Cartera habilita su
destino en la misma transacción).

La migración `PlataformaParaCartera` siembra, en cada cooperativa:

| Participante | Papel | Destino | Habilitado | Adopta |
|---|---|---|---|---|
| `CNT` (Contabilidad) | destino | `Accounting` | sí | lo que declara su consumidor |
| `INV` (Comercial) | destino | `Inventory` | sí | `ObligacionCreada` v1, `ObligacionRechazada` v1 |
| `NOM` (Nómina) | recaudador | — | sí | `ProveedorDeObligaciones` v1 |
| `CAR` (Cartera) | destino | `Lending` | **no**, hasta la activación | `VentaACreditoRegistrada` v1 y v2, `AjusteDeVentaACredito` v1, `ResultadoDelGiro` v1 |

**Sin la fila `CNT` el despachador ya no recorre `Accounting`**: por eso la migración va antes que cualquier despliegue de este código.

Pantalla **Administración › Participantes de la integración** (`/admin/participantes-de-integracion`; `Core.IntegrationParticipants.View`
y `.Manage`), rutas `/api/core/integration-participants` (+ `{id}/retire`). Habilitar, inhabilitar o retirar **cierra** la vigencia la
víspera y abre otra: la historia queda. Los dobles de prueba (`TST`, `IsTestDouble`) se registran por cooperativa fuera de producción
(`Integration.Participant.TestDoubleInProduction` en producción); una versión que el despliegue no implementa responde
`Integration.Participant.VersionNotSupported`; dos vigencias del mismo participante, papel y destino no se cruzan
(`Integration.Participant.Overlaps`). Las claves derivadas del recaudador (`:reverso`, `:destino`, `TST:{guid}`) viven en
`ClavesDeRecaudador`.

Lo que va a un destino que no cuenta queda **`Pending` sin intentos ni alertas**: así espera una venta a crédito de Comercial hasta que se
active Cartera. `GetSalesDocumentCreditQuery` («¿hay destino?») pregunta al registro, no a la existencia del consumidor.

### 9.4 `ValidationFailed` (T17, C18)

Un consumidor que recibe un mensaje bien formado pero que no puede aceptar por una regla de negocio responde
`ResultadoDeConsumo.ValidationFailed(referencia, número, código, motivo)`. `RegisterDeliveryResultCommand` lleva la entrega a
`DeliveryStatus.ValidationFailed` **directo** desde `Pending` o `InBatch`, deja el intento `ValidationFailed = 5` y levanta
`Integracion.ValidacionFallida` con destinatarios del **módulo de origen** (`DestinatariosDeIntegracion`). No se reintenta: el origen
corrige y emite otro mensaje.

Para quien consume por lotes o necesita mirar antes, `IMensajesEntrantes.PendientesAsync(destino, tipos, hastaFechaDeOperacion, página)`
lista sin consumir ni crear intentos; `MensajeEntrante` trae la clase del documento como texto (`OriginDocumentClassText`,
`RelatedDocumentClassText`) y `UnidadDeConsumo` su `OriginModule`.

### 9.5 Los turnos del despachador (T15, T19)

`DespachadorDeMensajes` recorre sólo los destinos con consumidor **y** fila que cuenta (`DestinosHabilitadosAsync`, ya sin leer
`ParametrosDeInventario.CarteraIntegracionHabilitadaDesde`), y los **turna**: una tanda por destino en cada vuelta, dentro del presupuesto
de la pasada (`Integration:Dispatcher:BudgetSeconds`). Así el atraso de Contabilidad no se come el tiempo de la respuesta a Comercial. El
arranque falla con `Integration.Destination.DuplicateConsumer` si dos `IDestinoDeMensajes` declaran el mismo destino
(`ConsumidoresPorDestino.Validar`, en `Program.cs` después de `Build()`; `UnSoloConsumidorPorDestino`).

Del lado contable, `PlanearLote` separa por `OriginModule`, `VersionesAceptadas` es por módulo y la lista de pendientes del cierre es
`ListPendingIntegrationMessagesQuery(módulo)`. La bandeja (`/api/inventory/messages*`) elige su vista por módulo
(`IVistaDeBandejaDelModulo`; la de Inventario no cambió).

### 9.6 Dónde está cada cosa (013)

| | |
|---|---|
| Catálogo agregado | `Application/Common/Integration/CatalogoDeMensajes.cs`, `Contracts/Lending/` |
| Registro | `Application/Common/Integration/{IRegistroDeIntegracion,RegistroDeIntegracion,EscritorDelRegistroDeIntegracion,ContratosDeIntegracion,ClavesDeRecaudador}.cs`; `Application/Core/IntegrationParticipants/`; `API/Endpoints/Core/IntegrationParticipantsEndpoints.cs` |
| Consumo | `Application/Common/Integration/{IMensajesEntrantes,MensajesEntrantes,RegisterDeliveryResultCommand,DestinatariosDeIntegracion,ConsumidoresPorDestino}.cs` |
| Migración | `PlataformaParaCartera` (par PostgreSQL / SQL Server, con la semilla del registro y el arrendamiento `lending.process`) |
| Pruebas | Application `EmisorDeMensajesConVariosModulosTests`, `RegistroDeIntegracionTests`, `ConsumoConValidacionFallidaTests`, `MensajesEntrantesPendientesTests`; arquitectura `ElCatalogoDeMensajesEsUnico`, `EmisionesDeProcesoPermitidas`, `UnSoloConsumidorPorDestino`, `SoloElEmisorEscribeLaBandeja`; e2e `PlataformaParaCarteraTests` |
