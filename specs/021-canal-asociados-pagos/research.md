# Research — Feature 021: canal digital del asociado y pagos en línea

Fecha: 2026-11-08. Base: `develop` `772195c4`. Cada decisión lleva lo que se eligió, por qué y lo que se descartó.
Las rutas citadas son del ERP en `develop`; lo de la pasarela, de la documentación pública de Placetopay
(docs.placetopay.dev, consultada el 2026-11-08) y no de la guía de certificación de Avalpay, que es confidencial.

## Punto de partida verificado en el código

- **Recaudo ya tiene el canal en línea a medias.** `ReceiptChannel.Online = 5`, `ReceiptDocumentKind.OnlineReceipt`,
  `CollectionAgreementKind.Gateway = 3`, archivos de pasarela (`RCD_GatewayFiles`/`Lines`) y su carga
  (`LoadCollectionFileCommand`, `Application/Receipts/Agreements/ArchivosDeConvenio.cs`), que aplica cada línea con
  `AplicacionNoInteractiva` (`Application/Receipts/BankItems/AplicacionNoInteractiva.cs`). La clase del convenio dice
  «sólo por archivo (C2 = A): no hay botón de pago en línea».
- **`AplicacionNoInteractiva.AplicarAsync(LineaNoInteractiva)`** ya hace todo lo que el aviso necesita: idempotencia
  por `Key` (`UK_RCD_Receipts_OperationKey`), listas restrictivas, prevalidación contable, cerrojos, modalidad por
  canal (`RCD_ChannelModalityRules`), `IContratoDeObligaciones.AplicarAsync` antes que `IContratoDeCuentas.ConsignarAsync`
  (FR-170), recibo, medios, porciones, excedentes a saldo a favor (`SaldosAFavor.Reconocer`) y el hecho del recibo.
  No abre transacción; corre en la del llamador.
- **Dos reglas de la 015 lo frenan hoy**: FR-174 («ningún proceso programado llama a los contratos; todo pago a nombre
  de la persona que carga o confirma») y FR-091 / C2 = A (pasarela sólo por archivo). La prueba
  `NingunProcesoAplicaPagosNiInformaResultados` prohíbe que una `ITareaProgramada` de Recaudo llame a los contratos.
- **No hay autenticación entre servicios**: un solo esquema JwtBearer RS256 de usuarios
  (`Infrastructure/IngenIA365ERP.Identity/DependencyInjection.cs`), y `TenantResolutionMiddleware` sólo acepta el claim
  `active_tenant_id`.
- **El bus de integración es sólo en proceso** (`COR_IntegrationMessages` + `DespachadorDeMensajes`): no hay relevo
  hacia afuera, ni eventos de cambio de persona o de contacto.
- **La lectura «qué puede pagar» ya existe** para la caja: `GetPayablesQuery` (`Receipts/CashDesk/CajaCommands.cs`,
  `GET /api/receipts/payables`) junta `IContratoDeObligaciones.ListarAsync`, `IContratoDeCuentas.ConsultarAsync`,
  `ListarExigiblesAsync` y conceptos varios en `LoQuePuedePagarDto`, y audita la lectura como sensible.
- **El prepago con elección existe**: `ModalidadDePago.ExtraordinaryPrincipal` + `EleccionDeAbono { ReduceTerm,
  ReduceInstallment }`; sin elección, la preferencia vigente (`LND_PrepaymentPreferences`); sin ninguna, saldo a favor.
- **Documentos**: `IssueLendingDocumentCommand` ya emite extracto de crédito (`Statement`), extracto de cuenta,
  certificados de aportes y tributarios (`ContributionCertificate`, `TaxCertificate`, `DepositTaxCertificate`) en
  modo `Download`; el recibo sale en PDF por `GetReceiptDocumentQuery(format=letter)`; el simulador vive en
  `Application/Lending/Origination/SimuladorDeCredito.cs`.
- **Lo que no existe**: proveedor real de SMS (`ICanalDeMensajeria` sólo simulado), notificaciones push, un módulo de
  «otros cobros» con saldo por persona (sólo cuentas por cobrar comerciales leídas de Contabilidad, que
  `AplicacionNoInteractiva` no aplica), y canal de origen en la solicitud de crédito (`CreatedByUserId` obligatorio).
- **Tablas `WEB_*` heredadas**: sin ningún uso fuera de su mapeo.

---

## R1 · Forma del sistema: satélite + API de servicio del ERP

**Decisión.** Tres piezas nuevas y un módulo nuevo del ERP:

1. **`canal/api`** — servicio Go con PostgreSQL propia (base `ingenia365_canal`, una sola para toda la plataforma):
   cuentas del canal, espejo de habilitaciones, dispositivos, sesiones, intentos de pago, avisos pendientes,
   suscripciones de push, auditoría del canal. Habla con la pasarela y con el ERP.
2. **`canal/web`** — React (PWA), servida por el mismo host del servicio Go (mismo origen: sin CORS, cookies
   `SameSite=Strict`).
3. **`canal/app`** — envoltura MAUI HybridWebView del mismo build de React.
4. **Módulo «Canal digital» del ERP** (`Application/Channel`, tablas `CDG_` en cada cooperativa): configuración,
   conceptos publicados, destinos, habilitaciones, novedades de contacto, avisos recibidos; pantallas de
   administración en Shared; y la **API de servicio** `/api/canal/v1/…` que el satélite consume.

**Por qué.** Es la decisión D1 del dueño y la única que mantiene intactos los Principios III, IV y X dentro del ERP:
el satélite nunca abre una base de cooperativa, y todo lo que escribe lo escribe el ERP con sus comandos MediatR.

**Descartado.** Go leyendo réplicas (duplica la lógica de saldos, la decisión del dueño lo descartó); un módulo .NET
dentro del ERP (descartado por el dueño, D2).

## R2 · Autenticación entre el satélite y el ERP

**Decisión.** Token de servicio **JWT ES256** de 60 s en ambos sentidos, con `iss`, `aud`, `sub` = cooperativa
(`tenantPublicId`), `jti` único y `iat`. Cada lado guarda la llave pública del otro (Secret de Kubernetes, rotación por
`kid`). En el ERP: un **segundo esquema de autenticación** `CanalServicio` que sólo vale en `/api/canal/v1/**`, una
política `CanalServicio` que exige ese esquema, un almacén de `jti` en Redis base 0 con TTL de 2 min (rechaza
repetidos) y una **resolución de cooperativa propia** para esas rutas: el `tenantPublicId` de la ruta debe coincidir
con el `sub` del token, se busca en `ITenantDirectory` y se llenan los mismos `HttpContext.Items` que llena
`TenantResolutionMiddleware` (que exime el prefijo). Además, `NetworkPolicy` de Kubernetes: sólo el pod del canal
alcanza `/api/canal/v1`, y Traefik no publica esa ruta hacia Internet.

**Por qué.** No existe nada entre servicios (verificado). Un JWT firmado reutiliza la librería y el pipeline de
autenticación que el ERP ya tiene, evita administrar certificados de cliente (mTLS) y deja la cooperativa amarrada a
la firma. ES256 en lugar de RS256 para no confundir llaves con las de usuarios.

**Descartado.** mTLS (rotación de certificados en tres ambientes, sin beneficio sobre una red interna con
NetworkPolicy); HMAC compartido (un secreto que conocen dos sistemas); reutilizar el JWT de usuario con un «usuario
técnico» (el código lo prohíbe: «no se le crea un usuario técnico»).

## R3 · Actor y enmienda acotada de FR-174 / FR-091 de la 015

**Decisión.** El aviso de pago lo ejecuta el ERP **a nombre del asociado que pagó**: nuevo `ActorKind.Member` con
`PersonPublicId` y `ExecutionChannel.Channel` (canal digital), creado por una fábrica `Actor.AsociadoPorCanal(personPublicId)`
que sólo puede usar el comando del aviso. `CashierUserId` queda nulo (ya es nulable en `AplicacionNoInteractiva`). La
auditoría registra el actor «asociado», la IP y el agente que el canal reenvía. La spec 021 enmienda:

- **FR-091**: los pagos por pasarela entran también en línea por el canal digital (además de por archivo).
- **FR-174**: añade la excepción «el pago que el propio pagador hace en línea se aplica a su nombre»; sigue prohibido
  que un proceso programado del ERP aplique pagos. La prueba de arquitectura no cambia: el aviso no es una
  `ITareaProgramada`, es una petición; se le agrega un caso que fija que **sólo** `RegisterChannelPaymentCommand` usa
  `Actor.AsociadoPorCanal`.

**Por qué.** FR-174 exige que detrás de cada pago haya una persona responsable; en el pago en línea esa persona es el
asociado, que se autenticó con dos factores y aceptó los términos. Lo que FR-174 quería evitar —un proceso que aplica
solo— sigue prohibido.

**Descartado.** Bandeja de revisión humana para cada pago (contradice D11 del dueño); un usuario técnico del ERP.

## R4 · Conciliación por la misma llave

**Decisión.** La llave idempotente del recibo en línea es `GW:{convenioPublicId:N}:{referencia}`, la **misma** que
usará la línea del archivo de la pasarela cuando Recaudo cargue el reporte del día con el flujo existente de R2. Al
cargarlo, lo ya aplicado en línea sale `AlreadyApplied` y lo aprobado que nunca se avisó se aplica (a nombre del
analista que carga, FR-174 sin excepción). La conciliación neta contra el extracto (comisiones) es la existente
(`ConciliacionDelConvenio`). Cambios: la carga del archivo de pasarela arma su `Key` con esa forma cuando la línea trae
la referencia del canal; y `GatewayFileLine` gana índice único `(AgreementId, GatewayReference)` filtrado a no borradas.

**Por qué.** No hay que construir otro conciliador: el de la 015 ya cuadra archivo, aplicación y extracto, y la
idempotencia por llave hace que el orden entre aviso y archivo no importe.

**Descartado.** Conciliador propio del canal (duplicaría `ConciliacionDelConvenio`); consultar la pasarela desde el
ERP (el ERP no debe tener sus credenciales).

## R5 · Lectura de conceptos

**Decisión.** Consulta nueva `GetChannelPayablesQuery(personPublicId, date)` en `Application/Channel`, que reutiliza
las piezas de `GetPayablesQuery` (no su endpoint de caja) y filtra por los **conceptos publicados** de la cooperativa:
créditos por línea (`ListarAsync`), aportes y ahorro contractual/programado por línea (`ListarExigiblesAsync`, que trae
`DueDate`, `Amount`, `Pending`). La próxima fecha de pago de un crédito sale de `IConsultaDeObligaciones.CuotasDeNominaAsync`
con `TodosLosCanales = true` (`ListarAsync` no la trae). Cada ítem lleva su `conceptKey`, el destino, las modalidades
permitidas, el mínimo y, para créditos, `PayoffAmount` y las opciones de prepago. La lectura se audita como la de caja
(`Receipts.Person.PayablesRead`, canal digital).

**Por qué.** Los valores salen del mismo código que usa la caja: lo que ve el asociado es lo que vería el cajero.

## R6 · Excedentes y obligaciones canceladas (aclaración 3)

**Decisión.** Mapeo de la aclaración al ERP:
- Obligación viva y sobra valor → el canal manda la parte sobrante como `ModalidadDePago.Advance` (anticipo a las
  próximas cuotas) **cuando el asociado eligió «exigible» o «parcial»**; si eligió abono a capital, va como
  `ExtraordinaryPrincipal` con su elección (R7).
- Obligación cancelada o concepto que ya no admite el valor → `AplicacionNoInteractiva` ya lo convierte en **saldo a
  favor del pagador** (`SaldosAFavor.Reconocer`), que Recaudo resuelve (devolver o aplicar) con
  `ResolverSaldoAFavorAsync`. **Ajuste propuesto a la spec**: el texto dice «partida por identificar», pero el pagador
  está identificado; el saldo a favor es la figura exacta para «Recaudo lo devuelve o reubica con autorización». Se deja
  anotado para confirmación del dueño; no cambia el comportamiento que él eligió.

## R7 · Prepago

**Decisión.** Para el abono a capital, el canal muestra las dos alternativas de `SimularAsync` (cuota y plazo
resultantes) con la preferencia vigente del crédito preseleccionada, y manda `EleccionDeAbono` en el aviso. Si el valor
alcanza el capital, el contrato lo trata como cancelación total. El tope legal (`TopeLegalDePrepago`) lo aplica Cartera.

## R8 · Habilitaciones: espejo por arrastre

**Decisión.** La habilitación vive en la base de la cooperativa (`CDG_ChannelEnablements`). El satélite mantiene un
**espejo mínimo** (cooperativa, `personPublicId`, tipo y número de documento, estado, fecha de habilitación) que
**arrastra** cada 60 s por cooperativa con cursor (`GET /api/canal/v1/tenants/{t}/enablements?since=`), más una
resincronización completa nocturna. Cada lectura o pago vuelve a pasar por el ERP, que verifica la habilitación en
vivo: el espejo sólo decide el selector y la entidad validadora, nunca da acceso a datos.

**Por qué.** No hay relevo hacia afuera y construir uno (destino HTTP + reintentos + orden) cuesta más que un arrastre
con cursor; FR-026 («desde la siguiente petición») se cumple porque el ERP es la puerta de cada dato.

**Política «todo asociado activo»**: el ERP la materializa en filas reales de habilitación al encenderla (carga
inicial) y luego con el hecho `RetiroDeAsociadoAprobadoV1` (nuevo consumidor en el módulo canal) y con el alta de
asociado. Así el espejo sólo ve filas, nunca reglas.

## R9 · Datos de contacto para activar

**Decisión.** El satélite no espeja correo ni celular. Al activar, pide en vivo a la entidad validadora
`GET …/people/{p}/activation-contact` → correo y celular enmascarados más un identificador de destino; el envío del
código lo hace el satélite y el ERP sólo audita la consulta. Después de activa, los datos de contacto son de la cuenta
del canal; los cambios se avisan a cada cooperativa con `POST …/contact-updates` (novedad en `CDG_ContactUpdates`,
visible en Personas; nunca sobrescribe `COR_People`). Antes de enviar a un dato del ERP se verifica la autorización de
datos del canal de contacto (`IAutorizacionDeDatos`, `ContactChannel`) y se informa si falta.

## R10 · Credenciales de la pasarela

**Decisión.** Viven **sólo en el satélite**, cifradas con AES-256-GCM y una llave de cifrado de llaves (KEK) que llega
por Secret de Kubernetes, versionada (`kek_version` en cada fila) para rotar. El administrador las digita en la
pantalla del ERP; el ERP las reenvía al satélite (`PUT /internal/v1/tenants/{t}/sites/{s}/credentials`, token de
servicio) sin guardarlas ni registrarlas; el satélite las prueba creando y consultando una sesión de prueba antes de
marcarlas activas. Lo que vuelve al ERP es «activa / falló + motivo + últimos 4 del login».

**Por qué.** Quien llama a la pasarela es el satélite; guardarlas en el ERP obligaría a viajar el secreto en cada uso.
**Descartado.** AWS KMS por fila (costo y latencia por operación, sin ganancia frente a un KEK en Secret con rotación).

## R11 · Integración con la pasarela (Placetopay WebCheckout)

**Decisión.** Adaptador `pasarela/placetopay` detrás de una interfaz `Pasarela` (crear sesión, consultar, reversar,
verificar notificación), para admitir otras después.

- **Autenticación**: `tranKey = Base64(SHA-256(nonceCrudo + seed + secretKey))`, `nonce = Base64(nonceCrudo)`, `seed`
  ISO 8601; la semilla debe estar a menos de 5 min del reloj de la pasarela (NTP en el nodo).
- **Crear sesión** `POST /api/session`: `payment.reference` (≤ 32, generada por el canal), `description`,
  `amount { currency: "COP", total, taxes[] }` (impuestos sólo si un concepto los tiene; `allowPartial = false`),
  `buyer` (documento, nombres, apellidos, correo, celular de la cuenta del canal), `expiration` (configurable 10–30
  min), `returnUrl` (`https://<host>/pagos/{intentoId}/retorno`), `ipAddress` (la del asociado: `CF-Connecting-IP`),
  `userAgent`, `locale = es_CO`, `paymentMethod` según los medios del sitio. Respuesta: `requestId`, `processUrl`.
- **Consultar** `POST /api/session/{requestId}`: el estado de la **sesión** decide (APPROVED, REJECTED, PENDING; los
  parciales no aplican porque no se usa pago parcial de la pasarela y se tratan como diferencia); de `payment[]` se toma
  el intento aprobado: `internalReference`, `authorization`, `receipt`, `franchise`, `paymentMethodName`, `issuerName`.
- **Notificación**: la URL se configura **por sitio** en la pasarela (no por petición) →
  `https://<host>/webhooks/placetopay/{sitioPublicId}`. Firma `SHA-256(requestId + status.status + status.date + secretKey)`
  con prefijo `sha256:`; sin prefijo, SHA-1 (obsoleto, se acepta mientras la pasarela no active SHA-256 en el sitio).
  Comparación en tiempo constante. Se responde 200 de inmediato y se encola. **La pasarela no reintenta la
  notificación**: la consulta periódica es obligatoria.
- **Consulta periódica**: intentos pendientes cada 15 min durante las primeras 24 h (certificación lo exige; en
  producción se mantiene, configurable), luego una vez al día hasta 7 días, después alerta a la entidad.
  **Pendiente de confirmar con el analista de Avalpay** que la frecuencia de 15 min es aceptable en producción
  (su guía habla de 24 h).
- **Reverso** `POST /api/transaction` con `action = reverse` e `internalReference`; sólo antes del corte diario
  (configurable, por defecto 20:00 hora Colombia).
- **Tipos de documento**: CC, CE, TI, NIT con las expresiones de la pasarela; PPT, PEP y PA del ERP se envían como
  `PPN` (verificar con el analista); RC no paga.
- **Ambientes**: la URL base es configuración por ambiente (pruebas de Avalpay ≠ producción); ninguna URL ni
  credencial de la entidad va al repositorio.

**Pendientes de verificar en el ambiente de pruebas** (no bloquean el plan): lista exacta de estados (si existe
`FAILED`), tipos de impuesto, ventana y reglas del reverso, URL de producción, tipos de documento extranjeros.

## R12 · Referencia del intento

**Decisión.** `{codigoCanalDeLaEntidad (3)}{AAMMDD}{secuencia base36 (8)}{dígito Luhn}` → 18 caracteres
alfanuméricos en mayúscula, único en la base del canal (índice único) y nunca reutilizado. El código de 3 letras lo
asigna la plataforma al encender el canal (no se deriva del nombre). Corto para dictarlo por teléfono.

## R13 · Identidad del canal

**Decisión.**
- **Clave**: bcrypt costo 12 (`golang.org/x/crypto/bcrypt`), política mínima 8 caracteres con letras y números y
  rechazo de las 10 000 más comunes.
- **Sesión**: token de acceso opaco de 5 min (inactividad) en cookie `HttpOnly; Secure; SameSite=Strict` en la PWA;
  en la app, el mismo token en `SecureStorage`. La sesión dura máximo 8 h desde el ingreso (aclaración 1); la rotación
  no la extiende.
- **Dispositivo de confianza**: al pasar el código en un dispositivo, se emite una credencial de dispositivo (256 bits,
  guardada hasheada); sin ella se pide código.
- **PWA**: passkeys WebAuthn con `go-webauthn/webauthn`, RP ID = el host del canal.
- **App**: los passkeys **no** sirven dentro de HybridWebView (origen local, no coincide con el RP ID). La app usa
  biometría nativa (Android `BiometricPrompt`, iOS `LocalAuthentication`) para liberar la credencial de dispositivo
  guardada en `SecureStorage`; la credencial queda atada al dispositivo y se revoca desde «mis dispositivos».
- **Bloqueos y límites**: 5 fallos → 15 min de bloqueo (configurable); códigos de 6 dígitos, 5 min, 3 intentos;
  límite por IP y por documento en activación, ingreso y recuperación; mismo tiempo de respuesta exista o no.

## R14 · Stack del satélite

**Decisión (Go).** Go 1.26 o la estable vigente al iniciar C1; `net/http` con el ruteador de la biblioteca estándar;
`pgx/v5` + `sqlc` (consultas tipadas, sin ORM); migraciones con `goose` (SQL `-- +goose Up/Down`, reversibles);
trabajos durables con **River** (cola en PostgreSQL, encolado transaccional): aviso al ERP con reintentos, consulta
periódica, arrastre de habilitaciones, avisos y correos; `log/slog` JSON con `tenant`, `cuenta`, `intento` y
correlación; `go-webauthn/webauthn`; envío de correo por el mismo relevo SMTP del ERP; métricas Prometheus.
Pruebas: `testing` + `testcontainers-go` (PostgreSQL) + un simulador HTTP de la pasarela.

**Decisión (React).** React 19 + TypeScript + Vite; React Router (rutas de navegador en la PWA, de hash en la app);
TanStack Query (sin caché persistente de saldos: FR-030); `react-hook-form` + `zod` (validación en el cliente, mensajes
en español); `vite-plugin-pwa` (sólo el armazón sin conexión); CSS con variables por marca de la entidad. Pruebas:
Vitest + Testing Library; Playwright para el flujo de pago contra el simulador.

**Decisión (MAUI).** Proyecto nuevo `canal/app` (no el `IngenIA365ERP.App` de funcionarios), .NET 10, HybridWebView
con el build de React en `Resources/Raw/wwwroot`, puente JS↔C# para biometría, almacenamiento seguro, push y abrir la
pasarela. La pasarela se abre con `WebAuthenticator` (Custom Tabs / `ASWebAuthenticationSession`) y vuelve por App Link
/ Universal Link a `https://<host>/pagos/{id}/retorno`; la app consulta el estado. Push con FCM (Android e iOS vía
APNs) por `Plugin.Firebase`; biometría con código de plataforma propio (sin plugin).

## R15 · SMS, correo y push

**Decisión.** Interfaz `Mensajero` en el satélite con correo (SMTP, siempre) y SMS (adaptador del proveedor que se
contrate; candidatos: AWS End User Messaging SMS por estar ya en AWS, Twilio, Hablame). El uso de SMS se cuenta por
cooperativa para facturar (aclaración 4). Push: FCM HTTP v1 desde Go para la app; Web Push (VAPID) para la PWA
instalada como mejora de C4. El proveedor de SMS se elige en C1 con cotización; mientras tanto, sólo correo.

## R16 · «Otros cobros»

**Decisión.** En C2 entran créditos, aportes y ahorro contractual/programado. Los **otros cobros** pasan a C2b: son
cuentas por cobrar comerciales (facturas pendientes leídas de Contabilidad, `CuentasPorCobrarDeRecaudo`), que es
donde caen los servicios por vehículo de la 020 si se facturan; exige extender `AplicacionNoInteractiva` para crear
`RCD_CommercialReceivableApplications` (hoy no lo hace). No hay módulo de «otros cobros con saldo por persona» y no se
crea uno.

## R17 · Solicitud de crédito en línea (C6)

**Decisión.** `CreditApplication` gana `IntakeChannel` (oficina / canal digital) y `CreatedByUserId` pasa a nulable
con `CreatedByMemberPublicId`; el comando del canal lo crea con `Actor.AsociadoPorCanal`. Es migración aditiva de la
base de cada cooperativa; se hace en C6, no antes.

## R18 · Despliegue

**Decisión.** Un host por ambiente: `asociados.ingenia365.com`, `asociados-qa.ingenia365.com`,
`asociados-dev.ingenia365.com` (un solo dominio para todas las cooperativas, respetando la regla de no tener
subdominios por cooperativa). Imágenes `ghcr.io/jormancopete/ingenia365erp/canal-api` y `canal-web` (o una sola con
la PWA embebida en el binario Go con `embed`; **se elige la sola**: un despliegue, mismo origen). Flujo de CI nuevo
`.github/workflows/canal.yml` filtrado a `canal/**`, que escribe el digest en `ingenia365-gitops`
`workloads/canal/overlays/{dev,qa,pdn}` (las superposiciones declaran la imagen desde el día uno). Base
`ingenia365_canal` en el PostgreSQL de cada clúster, con respaldo diario como las demás. App de tiendas: compilación y
firma fuera del CI público (las llaves de firma no van al repositorio); publicación manual.

## R19 · Tablas `WEB_*`

**Decisión.** No se usan ni se retiran en esta feature; su retiro es una migración destructiva aparte (Principio XII:
respaldo y segundo revisor), que se propone como seguimiento.
