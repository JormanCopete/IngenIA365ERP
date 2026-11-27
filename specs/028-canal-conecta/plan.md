# Plan — 028 Canal de emisión IngenIA Conecta

**Spec**: `spec.md` | **Contrato**: `contracts/canal-conecta.md` | **Tareas**: `tasks.md` | **Estado**: espera
aprobación

## 1. Resumen técnico

Un adaptador nuevo, `CanalConecta : ICanalDeEmisionElectronica, IDeclaraEsquemaDeCredenciales`, en
`Infrastructure/IngenIA365ERP.ElectronicInvoicing/Channels/Conecta/`, registrado **dos veces** con dos códigos:
`CONECTA-PRUEBAS` y `CONECTA`. Está hecho de piezas puras y probables por separado:

| Pieza | Qué hace | Dónde se prueba |
|---|---|---|
| `TraductorAConecta` (puro) | Canónico del ERP (v2) → `InvoiceRequest`/`NoteRequest` de Conecta | Unitarias con casos dorados JSON |
| `TraductorDeResultadosConecta` (puro) | Estado de Conecta, Problem Details y excepciones de transporte → `ResultadoDeCanal` | Unitarias, una por fila de las tablas del contrato §6–§7 |
| `ClienteConecta` | HTTP: token, emitir, consultar, descargar, verificar | Contra `ConectaStubHandler` (stub HTTP en memoria) |
| `TokensDeConecta` (singleton) | Caché del token por (extremo, client id, huella del secreto) | Unitarias con reloj falso |
| `ExtremosDeConecta` | Lista blanca de extremos de `appsettings` | Unitarias |

Lo demás del ERP **no cambia de forma**: la máquina de estados, el procesador, el circuito, las contingencias, los
casos a/b/c y la representación siguen igual. El adaptador encaja en el puerto.

Los cambios fuera del adaptador son pocos y acotados:

1. **Canónico v2**, aditivo: datos que Conecta exige y el v1 no trae. Son nombres y apellidos separados, nombres de
   municipio y departamento, código de departamento, código postal y tipo de organización. La versión del esquema se
   guarda en cada versión del documento, para que reconstruir una v1 siga dando los mismos bytes.
2. **Configuración**: dos columnas nulas en `COR_ElectronicEmissionSettings`: `ChannelTenantId` (Guid) y
   `ChannelEndpoint` (clave de la lista blanca). La migración par es aditiva.
3. **Guardia**: motivos nuevos (contrato §9) y una regla: `CONECTA` sólo en un proceso de producción.
4. **Permiso** `ElectronicInvoicing.Settings.ActivateProduction`, sembrado por la API al arrancar.
5. **Pantalla** `/admin/facturacion-electronica`: un selector de modo en vez del código de canal libre, campos de
   Conecta, confirmación `PRODUCCION` y el resultado de la verificación.
6. **GitOps**: montar el Secret `erp-fe-credenciales` y la sección `ElectronicInvoicing:Channels:CONECTA*` por
   overlay.
7. **(P3)** El endpoint del webhook.

## 2. Estado actual leído (2026-11-27, `develop` d810d07f)

- Puerto: `Application/ElectronicInvoicing/Channels/ICanalDeEmisionElectronica.cs` y `ContratosDelCanal.cs`, como en
  §3.1–§3.3 de `dian.md`. `ContextoDeCanal` ya trae `TenantPublicId`, ambiente, la clave de idempotencia y las
  credenciales resueltas.
- Registro: `CanalesDeEmision` resuelve por código, sin distinguir mayúsculas. `AgregarClientesDelCanal` ya registra
  los dos `HttpClient` por canal (emisión sin reintentos; consulta con `AddStandardResilienceHandler`). Hoy no lo usa
  nadie: lo estrena este canal.
- Credenciales: `CredencialesEnArchivo` lee `{tenantPublicId}.{channelCode}.json`, valida el esquema que declara el
  adaptador y guarda 5 minutos en caché por huella. El Secret **no está montado en ningún overlay de GitOps** (no
  aparece `erp-fe-credenciales` en `workloads/erp`).
- Guardia: `GuardiaDeEmisionFiscal.Decidir` ya bloquea `SIMULADO` en `Production` y la credencial no verificada.
  **No mira `ContingenciaDelFacturador`**: hoy un canal que no transmite la 03 dejaría numerar en contingencia (D8).
- `GuardiaDeCanalesExternos` (Lending, 013) le quita efecto legal a lo simulado en producción, con
  `CanalesExternosOptions.AmbienteDePruebas = !IsProduction()`. La facturación electrónica no la usa: su equivalente
  es la guardia fiscal. Este plan **reusa la misma opción del proceso** para negar `CONECTA` fuera de producción, sin
  duplicar la decisión.
- Conecta (repositorio hermano, sólo lectura):
  - `IssueInvoiceCommandValidator` y `IssueNoteCommandValidator` exigen `number == null`: «el consecutivo lo asigna la
    plataforma» (`IssueInvoice.cs:54`, `IssueNote.cs:63`).
  - Tiene un solo `PaymentMeans` por documento.
  - Las retenciones sólo existen en el documento soporte.
  - La tarifa del impuesto va en porcentaje (19), no en fracción.
  - `Idempotency-Key` admite hasta 64 caracteres `[A-Za-z0-9._:-]`.
  - Las rutas viven en un solo host por ambiente: `/v1` es la API y `/auth/realms` es Keycloak.

## 3. Decisiones (el dueño aprueba o cambia; la recomendada va primero)

### D1 — Numeración: **bloqueante**

El ERP numera al confirmar. Sobre eso descansan la contingencia 03, el reintento sin duplicar, los casos a y b con el
mismo número y la impresión del POS (§0 y §9 de `dian.md`). `ConfigureEmissionCommand` rechaza un canal sin
`AceptaNumeroDelErp`. **Conecta hoy no acepta el número**.

- **Recomendada — pedir a Conecta la «numeración del cliente»** por tenant y prefijo (cambio en Conecta365, no en
  este repo). Con ella, Conecta:
  - acepta `number` en `/invoices`, `/credit-notes` y `/debit-notes` cuando el prefijo está marcado así;
  - valida que el número esté dentro del rango vigente y que no esté expedido;
  - es idempotente por clave;
  - admite volver a enviar **el mismo número** con otra clave cuando el documento anterior con ese número quedó
    `Rejected` (casos a y b: la DIAN no consume un número rechazado, Res. 165/2023);
  - responde 409 `number-conflict` si el número ya está expedido o en curso con otra clave.

  Hasta que exista, el canal se construye y se prueba contra el stub, pero `CONECTA*` declara
  `AceptaNumeroDelErp = true` y **la verificación falla** con «Conecta no acepta la numeración del ERP para el prefijo
  X», detectado en `missing` o con una petición de prueba. Así ninguna cooperativa lo activa antes.
- Alternativa B — Conecta numera y el ERP adopta el número. Cambia el corazón del ERP: número después de confirmar,
  POS sin número al imprimir, contingencia 03 imposible. **Descartada.**
- Alternativa C — puente: el ERP envía `number = null` y comprueba que Conecta asigne el mismo. Se rompe con 30 cajas
  concurrentes (llegan en otro orden) y con el caso a (Conecta asignaría otro número). **Descartada.**

### D2 — Varios medios de pago

Conecta acepta uno solo; el ERP puede llevar varios (efectivo + tarjeta).

- **Recomendada**: pedir a Conecta `paymentMeans` como lista (el UBL admite `cac:PaymentMeans` 1..n). Mientras tanto,
  el adaptador envía la forma (contado/crédito) y el medio del pago de **mayor valor**. Deja todos los medios en
  `notes` y en `metadata`, y agrega un mensaje `Notificacion` `CONECTA-MEDIOS` a la transmisión.
- Alternativa: bloquear (`InvalidData`) las ventas con más de un medio. Detiene el POS: no se recomienda.

### D3 — Lo que el esquema de Conecta no expresa

- **Retenciones informativas** de la factura (las que practica el comprador): **se omiten**. No cambian el
  `PayableAmount`. Quedan en `metadata`.
- **Tributos por unidad** (`amountPerUnit`, p. ej. impuesto al consumo de bolsas): Conecta sólo recibe tarifa. El
  adaptador responde `InvalidData` **antes de enviar**, con regla `CONECTA-TRIBUTO-POR-UNIDAD` y el producto. Además
  la preparación (`readiness`) avisa si hay impuestos de ese tipo activos.

### D4 — Modos como códigos de canal: **bloqueante**

- **Recomendada**: dos códigos, `CONECTA-PRUEBAS` (sólo `Testing`) y `CONECTA` (sólo `Production`), con el mismo
  adaptador. Cada documento sella el modo, así:
  - las credenciales de pruebas y las reales son **archivos distintos** del Secret y no se pueden cruzar;
  - el circuito y la contingencia van por modo;
  - la guardia decide por código, como ya hace con `SIMULADO`.
- Alternativa: un solo código `CONECTA` y el ambiente en el nombre del archivo
  (`{tenant}.CONECTA.{ambiente}.json`). Cambia la convención de §11 y obliga a que `CredencialesEnArchivo` conozca el
  ambiente.

### D5 — Estado: consulta primero, webhook después

- **Recomendada**:
  - P1/P2 por consulta. Tras el 202, `EmitirAsync` consulta `GET /v1/documents/{id}` dentro de la misma llamada,
    cada `PollIntervalMs` (500) hasta `SyncWaitSeconds` (técnico, 8 s, nunca más que la espera del POS). Si decide,
    devuelve el resultado; si no, `InProcess` y sigue el procesador (15 s, 1 min, 2…). Consultar no es reenviar: la
    regla 7 se respeta.
  - P3: webhook firmado como aviso para consultar antes (§16 de `dian.md`: nunca fuente de verdad).
- Alternativa: webhook desde el primer día. Exige publicar una ruta anónima del ERP y registrar el webhook en Conecta
  por tenant: más piezas para un beneficio que la consulta ya da.

### D6 — Qué pasa con §15 (servicio central): **reemplazar**

**Recomendada**: §15 de `dian.md` se reescribe así: el modo «software propio» se obtiene **a través de Conecta**, no
de un servicio sin estado de la 010.

- Para el ERP, Conecta es un canal de modo `TechnologyProvider`. El ERP no arma UBL, no firma y no guarda certificado
  ni PIN.
- La clave técnica queda en Conecta (Vault), y `COR_DianResolutionChannels.TechnicalKey` no viaja.
- Si el tenant de Conecta usa su motor propio (`Direct`) o un proveedor (`ExternalProvider`), es asunto de Conecta.
  Cambiarlo no toca el ERP (FR-064 se cumple con más razón).
- Se retira la carpeta vacía `Channels/ServicioCentral` y la mención `CanalServicioCentral` de §2, §3.4 y del
  comentario de `DependencyInjection`.
- §16 queda: la decisión A3 se resuelve con Conecta como canal único, y Conecta elige el proveedor.
- La nómina electrónica (N3) **no** se decide aquí: §15 deja dicho que, si se va a Conecta, será otra feature.

Alternativa: convivir, dejando §15 como opción futura. Sostiene dos caminos para lo mismo y un contrato hermano sin
dueño: no se recomienda.

### D7 — Totales del ERP frente a los de Conecta

Conecta recalcula los totales. Si difieren, el XML (lo legal) diría otra cosa que la representación del ERP.

- **Recomendada**: pedir a Conecta un campo opcional `expectedTotals` (`payable`, `taxInclusive`) que rechace con 422
  `totals-mismatch` **antes de consumir y transmitir**. Hasta entonces, el adaptador compara los `totals` del 202 con
  los del canónico. Una diferencia distinta de cero agrega el mensaje `CONECTA-TOTALES` y levanta la alerta crítica
  `Dian.TotalesDelCanalDistintos`. El documento ya va en camino y la alerta lleva a corregirlo con el caso b.
- Alternativa: llamar `POST /v1/invoices/preview` antes de cada emisión. Duplica la latencia del POS y no está claro
  que funcione con el canal de proveedor.

### D8 — Contingencia 03 con Conecta

`CONECTA*` declara `ContingenciaDelFacturador = false` en esta entrega: `/v1/invoice-transcriptions` responde 409 con
un tenant de proveedor. Se agrega a la guardia el motivo `ContingencyNotSupported`: con un evento 03 abierto y un canal
que no transmite la 03, la venta en oficina se bloquea con salida («use el comprobante de contingencia en papel
cuando el canal lo admita; mientras tanto, espere a que se cierre el evento»). El POS mantiene lo que hace hoy: la
venta se confirma, queda pendiente y se transmite al volver el canal. Así nunca se numera en una resolución de
contingencia que nadie va a transmitir. La transmisión 03 por Conecta queda trazada para cuando el tenant emita por
motor propio.

### D9 — Correo al comprador

Con `EmailDeliveryBy = Erp` (defecto) el adaptador **no envía** `customer.email`. Así Conecta no entrega y no hay
doble correo; el documento queda en `Rendered` en Conecta. Con `Channel`, sí lo envía, y Conecta (o el proveedor, según
su `providerDeliversToCustomer`) entrega. `PuedeEnviarCorreo = true`. **Por cotejar** con el contador: que la DIAN no
exija el correo del adquirente en el XML cuando la entrega es por otro medio (anexo 1.9, el correo del adquirente es
opcional en el esquema de Conecta).

## 4. Arquitectura

```
ERP (por cooperativa)                                        Conecta (por ambiente)
EmitElectronicDocumentCommand / Procesador
   │ ICanalesDeEmision.Resolver(canal sellado)
   ▼
CanalConecta (CONECTA-PRUEBAS | CONECTA)
   ├─ ICredencialesDeCanal → /secrets/facturacion-electronica/{tenant}.{canal}.json  {clientId, clientSecret[, webhookSecret]}
   ├─ ExtremosDeConecta (appsettings: lista blanca) ← ChannelEndpoint de la vigencia
   ├─ TokensDeConecta ── POST {extremo}/auth/realms/conecta/protocol/openid-connect/token ─► Keycloak
   ├─ TraductorAConecta(canónico v2) ── POST {extremo}/v1/invoices|credit-notes|debit-notes ─► API /v1
   │      Idempotency-Key, X-Tenant-Id, Bearer
   ├─ GET /v1/documents/{id} · GET /v1/documents?prefix&number&family ─► estado
   ├─ GET /v1/documents/{id}/xml | /dian-response | /attached ─► artefactos
   └─ TraductorDeResultadosConecta → ResultadoDeCanal
```

El adaptador no conoce la base de datos. Lo que necesita de la vigencia (tenant de Conecta y extremo) le llega en
`ContextoDeCanal`, que crece con un campo opcional `Canal` (`ParametrosDelCanal`: `TenantId`, `Endpoint`). Lo arma
quien hoy arma el contexto desde la vigencia **sellada** en el documento.

## 5. Datos

| Cambio | Tipo | Detalle |
|---|---|---|
| `COR_ElectronicEmissionSettings.ChannelTenantId` | `uuid`/`uniqueidentifier` nulo | Tenant de Conecta |
| `COR_ElectronicEmissionSettings.ChannelEndpoint` | `varchar(40)` nulo | Clave de la lista blanca (`QA`, `PDN`) |
| `COR_ElectronicDocumentVersions.CanonicalSchemaVersion` | `smallint not null default 1` | Para reconstruir cada versión con su esquema |
| Permiso `ElectronicInvoicing.Settings.ActivateProduction` | semilla de la API | Sólo el rol administrador |

Migración par **aditiva** `CanalConecta` (PostgreSQL y SQL Server) con marca posterior a `EsquemaBase`. No toca filas
existentes y no se usa la base congelada `database/schema`.

## 6. Comprobación contra la constitución

| Principio | Cómo se cumple |
|---|---|
| I Spec-first | Este paquete se aprueba antes del código |
| II Clean Architecture | El adaptador vive en Infrastructure. Application sólo gana campos del contrato y motivos de la guardia. `ElProveedorTecnologicoSoloLoConoceSuAdaptador` se amplía a `Channels/Conecta` |
| III CQRS | Sin comandos nuevos salvo el webhook (P3), que es un comando |
| IV Multi-tenancy | Credenciales por cooperativa y canal, con la ruta derivada del tenant resuelto. El token y `X-Tenant-Id` son por cooperativa. Prueba con dos cooperativas |
| VI PublicId | Hacia Conecta viajan el `PublicId` del documento y la clave; nunca un `Id` interno |
| VII Auditoría | La configuración, la activación a producción y la verificación quedan auditadas, sin secretos en el diff |
| VIII Validación dual | La pantalla impide modos inválidos y el comando y la guardia los rechazan |
| IX Errores visibles | Cada traducción lleva mensaje humano y código crudo, y las alertas son las existentes más una nueva |
| XI Inmutabilidad | Transmisiones y versiones intactas; la columna nueva de versión se escribe al crearla |
| XII Migraciones | Par aditiva e idempotente |

## 7. Pruebas

- **Unitarias (Application.Tests/ElectronicInvoicing/Canales/Conecta)**:
  - casos dorados del traductor (factura contado de consumidor final, factura crédito a persona jurídica con
    descuento, varios medios, nota crédito total, nota débito, persona natural con nombres separados, tributo por
    unidad → `InvalidData`);
  - una prueba por fila de las tablas de traducción de resultados;
  - tokens (caché, renovación, 401 → una renovación);
  - extremos (lista blanca; un extremo desconocido → `ChannelUnavailable`, sin llamar);
  - la clave de idempotencia derivada (determinista, ≤ 64, del alfabeto).
- **Conformidad**: `CanalConectaConformidadTests : ConformidadDelCanal` contra `ConectaStubHandler`, que imita cada
  escenario: 202 → decisión en la espera, 202 → decisión tarde, 5xx, corte después de enviar, replay 200, 409, 422,
  401, 429, `Contingency` sin CUFE.
- **Guardia**: los motivos nuevos y las combinaciones de modo y ambiente; `CONECTA` con `AmbienteDePruebas = true` →
  `Blocked`.
- **Arquitectura**: la existente ampliada a `Channels/Conecta`;
  `LasCredencialesDeFacturacionNoTocanLaBase` (`ChannelTenantId` y `ChannelEndpoint` no son secretos, pero se prueba
  que no haya `ClientSecret` en entidades);
  `ElSecretoDeConectaSoloViajaALaListaBlanca` (ninguna URL de token se arma con un texto de la base).
- **Integración (Testcontainers, los dos motores)**: `CanalConectaTests`, con el API host y el stub en el
  `HttpClientFactory`:
  - factura validada con artefactos guardados;
  - `InProcess` → consulta → validada;
  - `NotFound` → reenvío con la misma clave sin duplicar en el stub;
  - rechazo y caso a con el mismo número;
  - nota crédito referida al CUFE;
  - dos cooperativas con credenciales distintas;
  - configuración `CONECTA` negada fuera de producción;
  - verificación que lee el ambiente.
- **E2E manual contra Conecta QA y PDN habilitación** (no en CI: no hay DIAN ni proveedor en CI). Va como receta en
  `docs/operaciones/conecta-puesta-en-marcha.md`: factura, nota crédito y nota débito de COOFLOPAL en `SETT`, y
  latencia p95 con varias cajas.

Siguiendo la costumbre del repo (memorias `feedback-e2e-solo-al-final` y `feedback-015-pruebas-al-final`), las suites
de integración corren **una vez al final**. Las unitarias y la compilación corren en cada incremento.

## 8. Despliegue (repositorio `ingenia365-gitops`, `workloads/erp/...`)

1. Componente `components/facturacion-electronica-credenciales`: volumen del Secret `erp-fe-credenciales`
   (`optional: true`, **sin `subPath`**) en `/secrets/facturacion-electronica/` del contenedor de la API, sólo
   lectura. Se incluye en los overlays de DEV, QA y PDN.
2. Variables por overlay (no secretas):
   - DEV y QA: `ElectronicInvoicing__Channels__CONECTA-PRUEBAS__Endpoints__QA=https://conecta-qa.ingenia365.com` y
     `…__Endpoints__PDN=https://conecta.ingenia365.com` (habilitación en PDN), **sin sección `CONECTA`**;
   - PDN: las de pruebas más
     `ElectronicInvoicing__Channels__CONECTA__Endpoints__PDN=https://conecta.ingenia365.com`.

   Un proceso sin la sección `CONECTA` no registra el canal real (doble seguro sobre la guardia).
3. El Secret se crea con `tools/scripts/crear-secreto-facturacion-electronica.ps1` (ya existe): `-Canal
   CONECTA-PRUEBAS -Campos clientId,clientSecret`. En PDN y canal `CONECTA` exige escribir `PRODUCCION`. **Nada de
   Conecta se toca.** El cliente API (`POST /v1/api-clients` en Conecta, rol `Operator`, scopes `documents:*`) lo
   crea el administrador del tenant en Conecta y entrega el secreto una vez: va directo al guion, nunca al chat.
4. Verificar la salida de red del pod de la API hacia `conecta*.ingenia365.com:443` (NetworkPolicy si existe).
5. Orden en PDN: imagen → GitOps (volumen y variables) → Secret → pantalla: modo «Conecta – pruebas», tenant
   `266f0ee4-…`, extremo `PDN` → verificar → asociar el prefijo `SETT` (resolución de `Testing`) al canal
   `CONECTA-PRUEBAS` → factura de prueba.

## 9. Entregas

| Entrega | Contenido | Depende de |
|---|---|---|
| **C0** | Especificación actualizada (`dian.md` §2, §3.4, §11, §15, §16; este paquete) | Aprobación |
| **C1** | Canónico v2, configuración (columnas, comando, DTO, migración), guardia y permiso | C0 |
| **C2** | Adaptador completo contra el stub: traductores, cliente, tokens, conformidad | C1 |
| **C3** | Pantalla, guion y documentación de operación, GitOps | C2 |
| **C4** | Integración en los dos motores y prueba manual en Conecta QA/PDN habilitación | C3 y **D1 en Conecta** |
| **C5** (opcional) | Webhook | C4 |

Un commit por entrega, con el trailer acordado. Sin push hasta que el dueño lo pida.
