# Research — 020 Transporte, servicios por vehículo y conectores externos

**Fecha**: 2026-11-08 · **Spec**: [spec.md](spec.md) · **Plan**: [plan.md](plan.md)

Cada decisión sale de leer el código de `develop` (`772195c4`). Las decisiones del dueño se citan
por su letra (A–L) o su FR. Formato: **Decisión** · **Por qué** · **Alternativas descartadas**.

---

## R1. Habilitación de módulos por empresa

**Decisión**:
- Tabla nueva `ADM_TenantModules` en la base administrativa: cooperativa, código del módulo
  (`TRN`), habilitado, quién, cuándo, motivo. Una fila por cooperativa y módulo; cada cambio queda
  en la auditoría.
- La lee un servicio `IModulosHabilitados` con caché en memoria de 60 s, que se invalida al
  escribir en la misma réplica.
- La puerta tiene tres capas:
  1. **API**: un `AddEndpointFilter<FiltroDeModuloHabilitado>` en el grupo de cada módulo
     habilitable (`/api/transport`, sus plantillas y sus informes). Sin módulo responde
     **403 `Module.NotEnabled`** con el sobre de error. El filtro no mira permisos: así el maestro,
     que se salta `RequirePermission` (`PermissionAuthorizationFilter.cs:57-60`), tampoco entra a
     un módulo apagado.
  2. **Permisos**: `PermisosDeLaPeticion.ResolverSinMemoAsync` retira los códigos `Transport.*`
     cuando el módulo está apagado. El cliente ya no los recibe en `/permissions/mine`.
  3. **Menú**: `MyPermissionsDto` gana `EnabledModules`. `NavMenu` envuelve el grupo Transporte en
     un componente `<ModuloHabilitado Codigo="TRN">`.
- Pantallas y rutas del maestro: `/saas/modulos` y `GET/PUT /api/saas/tenants/{id}/modules`, en un
  grupo con filtro de maestro, como `NationalValuesEndpoints.cs:33-42`.

**Por qué**:
- No existe nada parecido (`ADM_TenantSettings` existe pero nadie la usa y no está en
  `IAdminDbContext`).
- Una tabla propia deja el historial claro y sirve para cualquier módulo futuro (FR-004).
- Ponerlo en una columna de `ADM_Tenants` arrastraría el mapeo de Finbuckle (`AdminDbContext.cs:182-188`).
- 403 con código, en lugar del 404 indistinguible, por el Principio IX: el usuario debe saber por
  qué no entra.

**Alternativas descartadas**:
- `ADM_TenantSettings` con clave `Modules.TRN.Enabled`: tabla genérica sin uso, sin motivo ni
  historia.
- Sólo ocultar por permisos: el maestro la atravesaría y un rol personalizado con `*` también.

## R2. Prefijo `TRN_` y lugar en la arquitectura

**Decisión**:
- Las tablas del módulo llevan el prefijo nuevo `TRN_`. Requiere una enmienda PATCH de los
  Estándares Técnicos de la constitución (v2.0.x), sin tocar principios.
- Código:
  - `Domain/Entities/Transport`, `Domain/Transport` (reglas puras);
  - `Application/Transport`;
  - `API/Endpoints/Transport`;
  - `Shared/Pages/Transporte`.
- Módulo de auditoría `Transport`, con regla `(".Transport", Transport)` antes de `.Core` en
  `ModuloDeAuditoria.cs`.
- Los conectores no son de Transporte: van en `Core` (`COR_`) y en la plataforma (`ADM_`), porque
  mañana los usa otro módulo.

**Por qué**: el dueño quiere un módulo de Transporte que una cooperativa pueda no tener. Separar el
prefijo deja ver de un vistazo qué es de quién, y cumple el Principio IV: todo en la base de la
cooperativa.

**Alternativas**: `COR_Vehicles` (mezcla un módulo opcional con el núcleo).

## R3. Vehículo: unicidad, estado e historia

**Decisión**:
- **`TRN_Vehicles`** guarda placa, número interno, código radio-móvil, ficha, vinculación,
  mínimos de rodamiento, `Status` (Active/Inactive/Retired), `OwnerPersonId`, `StatusSince` y
  `OwnerSince`.
- **Unicidad**: índices únicos **filtrados a vivos** (no `Retired`, no `IsDeleted`) sobre `Plate`,
  `InternalNumber` y `RadioCode` (este sólo si no es nulo). Es la técnica de
  `UK_PAY_Employees_PersonId` y funciona en los dos motores.
  - La placa se normaliza: mayúsculas, sin espacios ni guiones.
  - Una carrera entre dos usuarios se traduce del índice a `Vehicle.PlateDuplicate`, igual que
    `Person.TaxIdDuplicate`.
- **`TRN_VehicleEvents`** es la historia. Es un hecho inmutable (`IHechoInmutable`): tipo
  (StatusChanged, OwnerChanged, Registered), fecha efectiva, valor anterior y nuevo, motivo y
  usuario.
- **Estado y propietario sólo cambian por su comando** (`ChangeVehicleStatusCommand`,
  `ChangeVehicleOwnerCommand`). `UpdateVehicleCommand` no los lleva en su contrato; es la misma
  lección de la feature 008 con las banderas.
- **Reingreso** de un vehículo retirado: registro nuevo (FR-010), igual que el empleado.

**Por qué**: las reglas del dueño (B, C) y el patrón ya probado en Nómina y Personas.

**Alternativas**:
- Historia en JSON dentro del vehículo: no se consulta ni se audita bien.
- Unicidad sin filtro: impediría reingresar una placa.

## R4. Propietario y clase de vinculación

**Decisión**:
- El propietario es `COR_People.Id`. La ficha busca y crea por `PersonSearchPicker` y
  `PersonaDialog`; no hay otra vía (prueba `LaPersonaSeEscribeEnUnSoloSitio`).
- **La clase de vinculación no se guarda**: se calcula al leer, «asociado» si la persona tiene
  afiliación vigente y «externo» si no.
  - La regla de afiliación vigente se copia como hoy en Cartera y Comercial
    (`PersonaDeOriginacion.cs:19-39`): los módulos copian la regla en vez de referenciarse.
  - El evento de cambio de propietario sí sella la clase que tenía cada lado en esa fecha.
- **Deudas del saliente** (FR-015): `GET /api/transport/vehicles/{id}/owner-debts` consulta el
  contrato de Cartera, `IContratoDeObligaciones.ListarAsync` filtrado a vencidas. Sin Cartera
  activa, consulta las cuentas por cobrar comerciales (`CuentasPorCobrarComerciales`). Informa,
  no bloquea.
- **No se agrega bandera de propietario a la persona**: ser dueño es una relación, no un rol.

**Por qué**: el dueño decidió que se deduce. Guardarla la dejaría vieja cuando el asociado se
retire.

## R5. Conductor

**Decisión**:
- `TRN_Drivers` (`PersonId`, estado) y `TRN_DriverAssignments` (conductor, vehículo, desde, hasta).
- Bandera nueva **`Person.IsDriver`**, escrita sólo por la clase `RolDeConductor`. Crea, restaura y
  retira el rol, pone y quita la bandera y no guarda: el modelo de `RolDeVendedor` (feature 008,
  fase 2).
- Alta en un paso con persona nueva: `RegisterDriverWithPersonCommand`, como
  `RegisterSalespersonWithPersonCommand`.
- `DeletePersonCommand` gana una consulta por puerto, `IUsoDePersonaEnTransporte`. Bloquea si la
  persona es propietaria o conductora vigente de un vehículo vivo (FR-017).

**Por qué**: la constitución (Principio V) exige banderas en sincronía con su fila hija, y la
feature 008 dejó la receta.

## R6. Catálogos

**Decisión**:
- **Una sola tabla `TRN_CatalogItems`** con `Kind` (Brand, VehicleClass, BodyType, Fuel, Modality,
  ServiceClass, TransitAuthority, RollingGroup, EquipmentType), `Code`, `Name`, `IsActive` e
  índice único `(Kind, Code)` entre vivos.
- **Los tipos de documento van en tabla propia, `TRN_DocumentTypes`**, porque tienen campos
  (`IsRequired`, `WarningDays`, `IsSystem`).
- `CodigoDeCatalogo` con largo corto (10). En `BuscarCodigoDeCatalogoQuery` se registra un caso
  por tipo: `transporte-marca`, `transporte-clase`… (`CampoCodigo` ya funciona por nombre).
- **Semilla parametric, Order 94**, `TransportCatalogSeeder`: clases RUNT comunes, combustibles,
  modalidades del Decreto 1079 de 2015 (individual, colectivo, especial, mixto, intermunicipal,
  carga), clases de servicio (público, particular, oficial) y los siete tipos de documento del FR-020.
  - Siembra sólo en cooperativas con el módulo habilitado, leyendo la base administrativa como
    `NationalValuesReplicaSeeder`.
  - Al habilitar el módulo después, el mismo sembrador corre para esa cooperativa desde el comando
    de habilitación (`IProvisionadorDeModulo`).
- Sin semilla quedan **marca y grupo de rodamiento** (son de cada cooperativa).

**Por qué**:
- Nueve catálogos de dos campos con nueve CRUD serían 36 rutas idénticas.
- La tabla única con `Kind` deja un solo CRUD, una pantalla con pestañas y una plantilla.

**Alternativas**: tabla por catálogo (patrón habitual del repo, pero sin campos propios no se
justifica).

## R7. Documentos del vehículo y vencimientos

**Decisión**:
- `TRN_VehicleDocuments`: tipo, número, emisor, expedición, inicio, vencimiento.
  - **Uno vigente por tipo**: registrar uno nuevo marca `SupersededAt` en el anterior.
  - Se edita sólo para corregir; corregir no crea historia.
- **Vencimientos**: consulta pura `VencimientosDeDocumentos` (Domain) que clasifica cada vehículo
  activo y cada tipo de su empresa:
  - Vencido;
  - PorVencer (`WarningDays`);
  - Vigente;
  - Falta (obligatorio sin documento).
- Vista exportable `vencimientos` en `/api/reports/transport/{vista}`. Nada bloquea (FR-023).

## R8. Plantillas masivas

**Decisión**: catálogo de plantillas del módulo, `CatalogoDePlantillasDeTransporte`, como el de
Cartera (`CatalogoDePlantillasDeCartera.cs`). Las plantillas son:

| Clave | Llave | Reusa |
|---|---|---|
| `transport.vehicles` | placa | `VehicleRegistrar` (alta y edición unitarias) |
| `transport.documents` | placa + tipo + número | `RegistrarDocumento` |
| `transport.drivers` | tipo y número de documento + placa | `RolDeConductor` + asignación |
| `transport.changes` | placa + acción (`activar`, `inactivar`, `retirar`, `propietario`) + fecha + motivo + documento del nuevo propietario | los comandos de estado y propietario |
| `transport.subscriptions` | placa + código de producto | `SuscripcionDeServicio` |

- Todas usan el `EjecutorDeImportacion` (revisar o aplicar, todo o nada, `?existing=update|keep`).
- Ninguna crea personas: un propietario o conductor inexistente es un error de fila. Lo vigila una
  prueba de arquitectura nueva, `LasPlantillasDeTransporteUsanElAltaUnitaria`, gemela de la de
  asociados.
- Rutas: `MapPlantilla` bajo cada grupo. El componente `ImportarPlantilla` va en cada pantalla, y
  una página `/transporte/plantillas` las lista.

## R9. Servicios, suscripciones y precio

**Decisión**:
- **`TRN_Services`**: el producto de Comercial (`ProductKind.Service`) que es servicio de vehículo,
  con:
  - prioridad (FR-056);
  - código de la línea de Cartera para su obligación (FR-058);
  - medio de pago a crédito para asociados y para externos (`AssociateCredit` / `CustomerCredit`);
  - conector ligado (opcional, FR-044);
  - activo.
- **`TRN_Subscriptions`** (vehículo, servicio, desde, hasta) y **`TRN_SubscriptionPrices`**
  (suscripción, precio, desde, hasta).
- **Precio**: si la suscripción tiene precio vigente en la fecha de la factura, ése; si no, el que
  resuelve Comercial (`ResolvePriceQuery`).
  - El precio pactado viaja en la línea con una marca nueva de `SalesLineInput`,
    **`PrecioPactado`**. Hoy un precio menor al de lista cuenta como descuento y pide aprobación
    (`PrecificacionDeVenta`).
  - `PrecioPactado` sólo lo acepta un borrador que nace de un proceso (`OrigenDelBorrador.Proceso`),
    no de la pantalla de ventas, y queda registrado en la línea como «precio pactado de
    suscripción».
- **Política de cobro a mitad de mes** (FR-034): `TRN_BillingPolicies` con vigencia, `FullMonth`
  (defecto) o `Prorated` por días. También guarda:
  - el tipo de documento de Comercial con que factura (factura electrónica o documento no
    electrónico, lo que la empresa tenga);
  - la bodega o punto de venta de servicios.

**Por qué**:
- El dueño eligió «producto + ajuste por vehículo».
- Sin la marca de precio pactado, cada precio especial pediría aprobación de descuento cada mes.

**Alternativas**:
- Una lista de precios por vehículo: cientos de listas.
- Rebajar como descuento con aprobación previa: no escala.

## R10. Facturación del período: una factura por vehículo, una forma de pago por servicio

**Decisión**:
- `RunServiceBillingCommand(period, mode: Batch|Single, vehiclePublicId?, paymentMode: Credit|Cash)`.
- **Recorre en tandas.** Cada vehículo va en su propia unidad de trabajo, con estos pasos:
  1. Calcula sus cargos: suscripciones vigentes × servicios activos, menos los ya facturados del
     período.
  2. Arma un borrador de venta de Comercial por `ISender` (`SaveInventoryDraftCommand`) con una
     línea por servicio.
  3. **A crédito, una `DocumentPayment` a crédito por cada línea**, por el total de la línea con
     impuestos. Cada una lleva el `SuggestedLineCode` del servicio y vence a fin de mes (una cuota).
  4. Lo confirma (`ConfirmInventoryDocumentCommand`).
  5. Registra los `TRN_ServiceCharges`.
  6. Un fallo deja el vehículo en el resumen con su motivo y la tanda sigue:
     - `ElectronicInvoicing.NotReady`;
     - crédito provisional pendiente de aprobación;
     - `Inventory.Price.NotFound`;
     - persona sin datos de facturación.
- **Comercial ya emite un `VentaACreditoRegistrada` por cada pago a crédito.** Cartera hace una
  `Loan` por cada uno (`NacimientoDeObligacionDeVenta.cs:142-163`). Así **cada servicio nace como su
  propia obligación** (FR-058) sin tocar Cartera. La obligación conserva `SourcePaymentPublicId`, y
  Transporte guarda ese mismo `PaymentPublicId` en su cargo.
- **De contado** (FR-032), un pago de caja por cada línea:
  - exige sesión de caja de Comercial abierta del usuario (`Payments.CashSessionRequired`);
  - los cargos nacen pagados, en la misma unidad de trabajo.
- **Retenciones** (raras en personas naturales): si `AmountDue` ≠ Σ líneas, la diferencia se
  reparte proporcional entre los pagos de las líneas y el residuo del redondeo va a la última.
  Valor de la línea = valor de su pago.
- **Idempotencia**: índice único filtrado en `TRN_ServiceCharges (VehicleId, ServiceId, Period)`
  entre cargos no anulados (FR-031). La factura lleva `ExternalReference = "TRN:{vehiculo}:{aaaamm}"`.
  Si la confirmación cae después de guardar el borrador, el reintento encuentra el borrador por esa
  referencia y lo confirma en vez de crear otro.
- **El orden de las líneas y de los pagos sigue la prioridad del servicio.** Las obligaciones nacen
  en ese orden. Los canales que reparten entre obligaciones de un deudor por antigüedad siguen así
  la prioridad (FR-056, ver R13).
- **Ejecución** (corregido en implementación, 2026-11-08): la corrida en lote **no** es una tarea de
  fondo. Comercial exige que el mensaje de una venta a crédito lo origine la persona (T11 de la 012:
  `VentaACreditoRegistrada` no admite proceso) y el crédito exige su permiso
  (`Inventory.Sales.SellOnCredit`), que en segundo plano no hay. La corrida se abre con
  `POST /billing/runs` y la pantalla la avanza por tandas de 20 vehículos con
  `POST /billing/runs/{id}/next` mientras siga abierta; el cursor queda en la corrida, así que se
  puede continuar. Uno individual corre en línea.
- **Precio pactado** (implementado, T095): va en `SaveInventoryDraftLine.AgreedPrice`, una propiedad
  que **no se lee del JSON** (`[JsonIgnore]`), así sólo la pone el servidor. `BorradorDeVenta` la toma
  como precio de lista fijado de la línea (impuestos incluidos): no hay descuento ni aprobación.
- **Medios a crédito** (T099): Comercial exige que un medio sin pago parcial cubra todo lo que falta;
  con un pago a crédito por servicio, los medios del servicio deben admitir pago parcial. Lo valida la
  configuración del servicio (`TransportService.CreditMeansNotPartial`).

**Por qué**:
- La factura por vehículo la decidió el dueño.
- Una forma de pago por servicio lleva a «una obligación por servicio» (decisión del 2026-11-08)
  **sin cambiar Cartera**.
- Además, el cajero ya elige qué obligación paga y banco, convenio y nómina ya pagan por obligación.

**Alternativas**:
- Un pago a crédito por factura y un reparto propio de Transporte: lo descartó el dueño.
- Un tipo de obligación «servicio» en Cartera con conceptos por servicio: `LoanConcept` es un enum
  fijo y tocar la prelación de Cartera es más riesgo que valor.

## R11. Las obligaciones de servicio en Cartera

**Decisión**:
- La línea de Cartera de cada servicio la crea la empresa: de colocación, **marcada `IsService`**
  (feature 019), sin interés ni seguro, con `AllowsCommercialOrigin` y `AllowsNonAssociates` si
  factura a externos.
- `TRN_Services.CreditLineCode` la nombra, y la facturación valida al configurar el servicio que
  esa línea cumpla esas condiciones.
- El runbook explica cómo crearla.

**Por qué**:
- Con `IsService` las obligaciones quedan fuera de centrales, deterioro general y SICSES.
- Sin interés, «saldada» equivale exactamente a «valor de la línea pagado».

## R12. Cómo se entera Transporte de un pago: destinos opcionales en la bandeja

**Hecho**:
- Hoy los hechos de pago sólo van a Contabilidad: `PagoAplicadoV1` y `PagoReversadoV1` de Cartera,
  `ReciboEmitidoV1` y `ReciboAnuladoV1` de Recaudo, `DocumentoAnuladoV1` de Comercial.
- `EmisorDeMensajes` crea **una sola entrega por contenido** (`ResolverDestino`, líneas 132-138 y
  202). La tabla ya admite varias, por el único `(MessageId, Destination)`.

**Decisión**:
- Ampliar el catálogo con **destinos opcionales**: `TipoDeMensaje.DestinosOpcionales`.
  - Al emitir, el emisor crea además una entrega por cada destino opcional **registrado y habilitado
    en el registro de integración de la cooperativa**, en ese instante.
  - Sin participante no se crea nada: una cooperativa sin Transporte no paga ni una fila.
  - Las entregas obligatorias no cambian.
- `TRN` se agrega como destino opcional de los cinco mensajes:
  - `PagoAplicado`
  - `PagoReversado`
  - `ReciboEmitido`
  - `ReciboAnulado`
  - `DocumentoAnulado`
- El consumidor `DestinoTransporte` implementa `IDestinoDeMensajes` con `Destino = "TRN"` y
  descarta en una consulta por índice lo que no es suyo. Así resuelve cada mensaje:

| Mensaje | Cómo encuentra el cargo | Qué hace |
|---|---|---|
| `PagoAplicado` | `Loan.PublicId` → cargo por `LoanPublicId` | si no está, pregunta a Cartera por puerto (`IConsultaDeObligaciones.PorPagoDeVentaAsync`) el `SourcePaymentPublicId` y lo guarda |
| `PagoReversado` | igual | reabre |
| `ReciboEmitido` | aplicaciones `CommercialReceivable` → cargo por número de factura | — |
| `ReciboAnulado` | asignaciones guardadas por recibo | — |
| `DocumentoAnulado` | factura de Comercial | anula los cargos (sin pago) o los deja «revisar» (con envío) |

- Para que las respuestas mezcladas no rompan el «uno por destino», el participante `TRN` se
  registra con `IntegrationParticipantClass.Transport` (valor nuevo) al habilitar el módulo, desde
  `IProvisionadorDeModulo`.

**Por qué**:
- Es el cambio más chico que respeta la bandeja: transaccional, idempotente, con reintentos.
- No obliga a Cartera ni a Recaudo a conocer Transporte; sólo el catálogo declara el interés.
- Sirve a cualquier módulo futuro que quiera escuchar pagos.

**Alternativas**:
- Mensajes nuevos de Cartera «obligación saldada» hacia `TRN`: Cartera conocería a Transporte y
  habría que duplicarlos para la ruta sin Cartera.
- Sondeo periódico: no cumple los 2 minutos del SC-004 y barre cooperativas.

## R13. Cuándo una línea queda saldada (FR-045, FR-046, FR-056)

**Decisión**:
- **`TRN_ChargeAllocations`**: hecho inmutable por cada importe aplicado a un cargo (fuente:
  `CarteraPayment`, `CommercialReceipt` o `CashAtInvoice`; referencias del pago o recibo; importe).
  Las reversas son filas negativas con referencia a la original.
- **El cargo queda `Paid` cuando Σ asignaciones ≥ valor.**
  - **Ruta Cartera**: obligación por servicio, así que la asignación es el importe de `Principal`
    de `PagoAplicadoV1.Amounts`, y `PaidOff` lo confirma.
  - **Ruta sin Cartera (FR-059)**: el recibo aplica un importe a la factura y Transporte lo reparte
    entre los cargos pendientes **por prioridad del servicio y orden de línea**. Es una función pura
    de Domain, `RepartoPorPrioridad`.
- **Elección del cajero**:
  - Ruta Cartera: es natural, porque elige la obligación.
  - Ruta sin Cartera: la caja paga facturas, no líneas, así que aplica la prioridad, y la pantalla
    de facturación lo advierte (FR-059).
  - Agregar elección de línea en caja queda fuera: el dueño eligió obligación por servicio.
- **Canales no interactivos con Cartera**: convenio y nómina ya pagan por obligación. Un abono
  bancario identificado sólo por persona sigue la regla vigente de Recaudo entre obligaciones
  (`AplicacionNoInteractiva`).
  - Como las obligaciones nacen en orden de prioridad, una regla por antigüedad la respeta.
  - **Verificado en implementación (T098, 2026-11-08)**: `AplicacionNoInteractiva` no reparte sola.
    Cada línea llega con sus partes ya elegidas (crédito, cuenta o concepto y su valor) por quien
    identifica el abono, la partida o el giro; Recaudo sólo aplica lo elegido y lo que sobra queda como
    saldo a favor del pagador. Como cada servicio es su propia obligación, quien identifica el abono
    elige qué servicio paga, igual que el cajero. No se cambia Recaudo; el runbook lo explica.

## R14. Una línea saldada se notifica una sola vez

**Decisión**:
- Al pasar a `Paid`, Transporte emite en la misma unidad de trabajo un mensaje
  **`SolicitudDeConectorV1`** (origen `TRN`, destino `CON`).
  - Contiene: conector, versión, variables ya resueltas (placa, número interno, radio-móvil, valor,
    recibo, factura, fecha, período, servicio, observación) y el **identificador externo**.
  - Clave de evento: `Cargo:{cargo:N}`.
  - Sólo si el servicio tiene conector ligado y el conector está habilitado y configurado. Si no, el
    cargo queda «sin conector» y no se emite nada (borde de la spec).
- **Identificador externo** = `TRN_ServiceCharges.ExternalId`, un GUID que se genera al crear el
  cargo y nunca cambia. Se repite en todos los reintentos y reprocesos (FR-048, SC-005).
- **Reversa después de notificar** (FR-051): el cargo vuelve a `Open`, su envío queda
  `PaymentVoided` («pago anulado: revisar en el sistema externo») y sale una alerta al
  administrador por `DestinatariosDeIntegracion`, ampliado con el permiso
  `Transport.Notifications.Manage`.
  - **Si se vuelve a pagar, no se notifica de nuevo** (el proveedor nunca revirtió). Queda
    «pagado otra vez: revisar» y el administrador cierra a mano con motivo.

**Por qué**: el proveedor no permite deshacer. Notificar dos veces es peor que pedir revisión.

## R15. Conectores: definición en la plataforma, réplica en la cooperativa

**Decisión**:
- **Base administrativa**:
  - `ADM_Connectors` (código, nombre, activo);
  - `ADM_ConnectorVersions`: definición inmutable en JSON validado al guardar, con:
    - `auth`: `None`, `LoginToken {path, method, bodyTemplate, tokenPath, expiresInSeconds?}`,
      `HeaderKey {header, prefix}` o `Basic`;
    - `operation`: `{method, path, bodyTemplate}`;
    - `variables`: nombre, origen y formato;
    - `responses`: regla `{status, contains?} → outcome`;
    - `maxAttempts`;
    - `timeoutSeconds`;
  - `ADM_TenantConnectors` (habilitación por cooperativa, con motivo).
- **Cambiar la definición crea versión** (FR-039).
- **Réplica**: al habilitar un conector o publicar una versión, `IProvisionadorDeConectores` copia
  la versión vigente a `COR_Connectors` y `COR_ConnectorVersions` de cada cooperativa habilitada.
  - Mismo patrón que `ADM_PlatformNationalValues` → `NationalValuesReplicaSeeder` (Order 88). El
    sembrador `ConnectorReplicaSeeder` (Order 95) resincroniza al arrancar.
  - El consumidor lee sólo la base de la cooperativa (Principio IV: nada de cruzar a la
    administrativa en una petición de cooperativa).
- **Deshabilitar** marca la réplica inactiva. Las entregas pendientes responden `Retry` con
  «conector deshabilitado» (FR-049, «en espera») y siguen en la bandeja.
- **Simulación** (FR-040): `POST /api/saas/connectors/{id}/versions/{v}/simulate` con variables de
  ejemplo. Corre el mismo `RenderizadorDePlantilla` puro de Domain que el envío. Devuelve el cuerpo
  y la URL final y no abre ninguna conexión.
- **Plantilla**: JSON con marcadores `{{variable}}`. Lo renderiza Domain con escape JSON por tipo
  (entero, decimal, texto, fecha con formato). **No hay expresiones ni lógica**: una variable
  desconocida es un error al guardar la versión.

**Por qué**:
- El dueño quiere conectores como datos, definidos por el maestro y habilitados por empresa (K, L).
- Replicar evita que el trabajo de fondo de una cooperativa lea la base administrativa en cada
  envío, y conserva la versión con que salió cada envío.

**Alternativas**: leer `ADM_` desde el consumidor (cruza bases en un contexto de cooperativa);
definición por cooperativa (el dueño quiere que la defina el maestro).

## R16. Credenciales cifradas en la base (decisión del dueño, FR-057)

**Decisión**:
- **`COR_ConnectorCredentials`** (conector, `ProtectedPayload`, `SetBy`, `SetAt`, `LastVerifiedAt`,
  `LastVerificationOk`).
  - Cifrado con DataProtection, propósito `IngenIA365ERP.Connectors.Credential.v1`, con el llavero
    que ya vive en `ADM_DataProtectionKeys` y cifra TOTP y claves de adjuntos.
  - `[NoAuditar]` en la carga útil.
  - Ninguna DTO la incluye: `GET` devuelve sólo `hasCredentials`, quién y cuándo.
- **Prueba de arquitectura**: `LasCredencialesDeCanalesExternosNoTocanLaBase` gana una excepción
  nominal y única para `ConnectorCredential.ProtectedPayload`.
  - Prueba nueva `LasCredencialesDeConectoresNoSalen`: ningún DTO, `TablaExportable` ni plantilla
    de log toca esa propiedad.
  - Otra que fija que sólo `AlmacenDeCredencialesDeConector` llama a `Unprotect`.
- **El token** de un `LoginToken` vive sólo en memoria (`IMemoryCache` por cooperativa y conector,
  hasta `expiresIn` − 60 s). Nunca se persiste. Con dos réplicas son dos inicios de sesión, lo cual
  es aceptable.

**Riesgo aceptado**: el llavero vive en la base administrativa. Un respaldo de la base
administrativa más uno de la cooperativa bastan para descifrar. Es el mismo riesgo que ya tienen los
secretos TOTP. Queda escrito en el runbook.

## R17. El adaptador HTTP y la defensa contra SSRF

**Decisión**:
- **Puerto en Application**: `IEjecutorDeConector.EjecutarAsync(SolicitudHttp, CancellationToken)`.
- **Adaptador** en `IngenIA365ERP.ExternalChannels/Connectors`, con un cliente con nombre
  `"conectores"`:
  - escritura **sin reintentos** del lado de `HttpClient` (los reintentos son de la bandeja);
  - conexión 5 s y total `timeoutSeconds` (defecto 30, máximo 60);
  - es la primera llamada saliente real del producto.
- **SSRF** (FR-038):
  - *Al guardar*: sólo `https`, host DNS o IP pública, sin credenciales en la URL.
  - *Al conectar*: `SocketsHttpHandler.ConnectCallback` resuelve y **rechaza** loopback, privadas
    (RFC 1918), enlace local, CGNAT 100.64/10 (la malla Tailscale de los clústeres), ULA IPv6 y
    metadatos 169.254.169.254. Así se cierra el *DNS rebinding*.
  - Redirecciones apagadas.
  - `Conectores:PermitirRedesPrivadas` existe sólo para pruebas. El arranque falla si vale `true`
    en Production.
- **Circuito por cooperativa y conector**: se reutiliza `CircuitoDeCanalExterno`.
- **Lo que se registra**: método, URL sin consulta, código, primeros 2 KB de la respuesta, duración.
  El cuerpo enviado se registra **sin** las variables marcadas `sensitive` y nunca con cabeceras
  de autorización.
- Prueba de arquitectura `SoloElAdaptadorDeConectoresHablaHttp`: ningún otro tipo de Application ni
  de API usa `HttpClient` para conectores.

## R18. El consumidor `CON` y el tablero de envíos

**Decisión**:
- **`DestinoConectores`** (`Destino = "CON"`):
  1. Lee `SolicitudDeConectorV1`.
  2. Carga la réplica del conector y la credencial.
  3. Renderiza y llama.
  4. Traduce la respuesta por el mapa:
     - Done → `Processed`;
     - DoneWithWarning → `Processed` con aviso;
     - Retry → `Retry`;
     - ReAuth → borra el token y reintenta **una vez** en la misma unidad; si vuelve 401, `Retry`;
     - ManualReview o no previsto → `Rejected`.
- **Tope de intentos**: la bandeja reintenta sin fin. El consumidor mira
  `UnidadDeConsumo.IntentosLeidos` y, al llegar a `maxAttempts` (defecto 12, ≈ 2 h con la espera
  máxima de 15 min), responde `Rejected` con `Conector.ReintentosAgotados`.
- **`COR_ConnectorRequests`**: estado del envío para el tablero.
  - Columnas: mensaje, conector y versión, cargo de origen (texto `TRN:{cargo}`), identificador
    externo, estado, última respuesta, aviso, marca de pago anulado.
  - Estados: Pending, Sent, SentWithWarning, Retrying, Review, OnHold, PaymentVoided, ResolvedManually.
- **`COR_ConnectorCalls`**: un hecho por intento (código HTTP, extracto, duración, versión). Es lo
  que la bandeja no guarda (`IntegrationDeliveryAttempt` sin HTTP).
- **Tablero**:
  - `/transporte/envios`, que lee `COR_ConnectorRequests` unido a `TRN_ServiceCharges` (FR-049),
    exportable.
  - **Reintentar**: `Review` usa `ReprocessMessagesCommand` con destino `CON`, que ya existe, y
    `Retrying` usa el comando nuevo `RetryConnectorRequestNowCommand`, que pone `NextAttemptAt = now`.
  - **Cerrar a mano**: `ResolveConnectorRequestCommand` con motivo (FR-050).
- **Auditoría** (FR-052): `Transport.Connector.Called` por intento, sin credenciales, con el cuerpo
  saneado (R17).

## R19. Permisos

**Decisión**: catálogo `TransportPermissionCatalogSeeder`, llamado desde `PhaseZeroSecuritySeeder`.

| Código | Operador | Admin |
|---|---|---|
| `Transport.Vehicles.View` / `.Export` | ✔ (View) | ✔ |
| `Transport.Vehicles.Create` / `.Update` | ✔ | ✔ |
| `Transport.Vehicles.ChangeStatus` | ✔ (activar/inactivar) | ✔ |
| `Transport.Vehicles.ChangeOwner` / `.Retire` | | ✔ |
| `Transport.Vehicles.Import` | | ✔ |
| `Transport.Documents.View` / `.Manage` | ✔ | ✔ |
| `Transport.Drivers.View` / `.Manage` | ✔ | ✔ |
| `Transport.Catalogs.Manage` | | ✔ |
| `Transport.Services.View` / `.Manage` (suscripciones) | ✔ | ✔ |
| `Transport.ServiceSetup.Manage` (servicio, prioridad, línea, conector) | | ✔ |
| `Transport.Billing.View` / `.Run` | View | ✔ |
| `Transport.Notifications.View` / `.Manage` | View | ✔ |
| `Core.Connectors.View` / `.Configure` (credenciales) | | ✔ |

- El maestro no lleva permisos: sus rutas `/api/saas/*` van tras el filtro de maestro.
- `LosEndpointsProtegidosExigenPermiso` suma `Endpoints\Transport\*.cs`.

## R20. Pruebas

**Decisión**: siguiendo las memorias del dueño («e2e sólo al final», «pruebas sólo al final»), se
compila por fases con `-m:1` y las suites corren completas una vez al terminar:
- **Domain**: casos de `RepartoPorPrioridad`, `VencimientosDeDocumentos`, `RenderizadorDePlantilla`,
  `MapaDeRespuestas`, normalización de placa, prorrateo.
- **Application**: handlers con dobles.
- **Architecture**: las pruebas nuevas y las enmendadas.
- **Integración**: Testcontainers, más un **proveedor falso** HTTP levantado en la fixture (Kestrel
  en un puerto local, permitido sólo con `Conectores:PermitirRedesPrivadas=true` en el host de
  prueba) que imita el ejemplo de la spec: login con token, asignar, 400 «ya tiene vigencia», 500,
  401.
- **El recorrido e2e** cubre:
  1. habilitar;
  2. cargar vehículos;
  3. suscribir;
  4. facturar a crédito;
  5. pagar en caja la obligación del servicio;
  6. ver el envío;
  7. anular el pago y ver «pago anulado».
- **No hay pruebas de navegador** (como en todo el repo).

## Riesgos que el dueño debe conocer

1. **Comercial y la factura electrónica deben estar listos en la cooperativa.** Confirmar una
   factura exige el paso fiscal (`ElectronicInvoicing.NotReady`) y hoy el canal DIAN real (T732) no
   está construido.
   - Si la cooperativa no puede emitir factura electrónica, la facturación de servicios usa el tipo
     de documento no electrónico que tenga configurado (R9).
   - Comercial (Inventario) **todavía no está activo** en la cooperativa de referencia.
2. **Pago parcial en la ruta sin Cartera**: se salda por prioridad, no por elección del cajero.
3. **Llavero en la base administrativa** (R16).
4. **Volumen**: con Transporte habilitado, cada pago de Cartera de esa cooperativa genera una entrega
   `TRN`, que se descarta en milisegundos si no es de un servicio.
