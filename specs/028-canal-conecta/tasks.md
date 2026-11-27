# Tareas — 028 Canal de emisión IngenIA Conecta

Nada se empieza hasta que el dueño apruebe `plan.md` (sobre todo D1 y D4). Las suites de integración corren **una vez
al final** (C4); la compilación y las unitarias, en cada entrega. Un commit por entrega; sin push hasta que el dueño
lo pida.

## C0 — Especificación

- [ ] T001 `dian.md`: §2 (pieza `Channels/Conecta`, retirar `ServicioCentral`), §3.4 (filas `CONECTA-PRUEBAS` y
  `CONECTA`), §11 (esquema de la credencial de Conecta, lista blanca de extremos), §15 reescrito «modo software propio
  a través de Conecta» (D6), §16 (A3 resuelto), §18 (pruebas nuevas), §19 (pendientes nuevos)
- [ ] T002 `CLAUDE.md`: párrafo corto de la 028 (modos, credencial, salvaguardas, D1 pendiente en Conecta)
- [ ] T003 Pedido escrito para Conecta (D1, D2, D7) en `specs/028-canal-conecta/pedido-a-conecta.md`, con los
  contratos exactos que el ERP espera. Se entrega a la sesión o equipo de Conecta; aquí no se toca ese repositorio

## C1 — Canónico v2, configuración, guardia y permiso

- [ ] T010 Canónico v2: `ParteCanonica.GivenNames/FamilyNames/TradeName`; `DireccionCanonica.CityName/DepartmentCode/
  DepartmentName/PostalCode`; `VersionDelEsquema = 2`; el serializador omite los campos v2 al escribir una v1
- [ ] T011 `ConstructorDelCanonico` llena v2 desde la copia fiscal y DIVIPOLA; `EntradaDeDocumentoElectronico` y
  `FuenteDeEmisionDeInventario` (y las demás fuentes) entregan nombres y apellidos
- [ ] T012 `COR_ElectronicDocumentVersions.CanonicalSchemaVersion` (escrita al crear la versión); `ReconstruccionDel
  Canonico` construye con la versión guardada; prueba: una v1 reconstruida da el mismo SHA-256
- [ ] T013 `COR_ElectronicEmissionSettings.ChannelTenantId`, `ChannelEndpoint`; DTO, `GetEmissionSettingsQuery`
- [ ] T014 Migración par aditiva `CanalConecta` (PostgreSQL y SQL Server), con marca posterior a `EsquemaBase`
- [ ] T015 `ContextoDeCanal.Canal` (`ParametrosDelCanal(TenantId, Endpoint)`); lo arma quien arma el contexto, desde la
  vigencia **sellada**
- [ ] T016 `ConfigureEmissionCommand`: campos nuevos, cuatro rechazos del contrato §9, `ConfirmProduction`, permiso
  `ElectronicInvoicing.Settings.ActivateProduction` (sembrado por la API al arrancar, sólo para el administrador)
- [ ] T017 `GuardiaDeEmisionFiscal.Decidir`: motivos `ChannelEnvironmentMismatch`, `ProductionChannelNotAllowedHere`
  (lee `CanalesExternosOptions.AmbienteDePruebas`) y `ContingencyNotSupported` (D8)
- [ ] T018 Pruebas: guardia (combinaciones de modo, ambiente y proceso), configuración, canónico v2 (casos dorados
  nuevos; los v1 intactos)

## C2 — Adaptador

- [ ] T020 `OpcionesDeCanal`: `Endpoints`, `SyncWaitSeconds`, `PollIntervalMs`; `ExtremosDeConecta` (lista blanca,
  https salvo Development)
- [ ] T021 DTOs de Conecta en el adaptador (copia mínima de `InvoiceRequest`, `NoteRequest`, `DocumentSummary`,
  `IssueInvoiceResult`, Problem Details), sin referenciar el repositorio Conecta365
- [ ] T022 `TraductorAConecta` (puro, contrato §4) + casos dorados JSON en
  `tests/IngenIA365ERP.Application.Tests/ElectronicInvoicing/Canales/Conecta/Casos/`
- [ ] T023 `TraductorDeResultadosConecta` (puro, contrato §6–§7) + una prueba por fila
- [ ] T024 `TokensDeConecta` (caché por extremo, cliente y huella; renovación) + pruebas con reloj falso
- [ ] T025 `ClienteConecta` sobre los dos `HttpClient` de `AgregarClientesDelCanal` (emisión sin reintentos,
  consulta con resiliencia); clave de idempotencia UUID v5
- [ ] T026 `CanalConecta` (dos registros: `CONECTA-PRUEBAS` siempre; `CONECTA` sólo con su sección y en un proceso de
  producción), `IDeclaraEsquemaDeCredenciales`, espera síncrona tras el 202, comprobación del número y del ambiente,
  artefactos, `ProbarAsync` (contrato §9)
- [ ] T027 Alertas: `Dian.TotalesDelCanalDistintos` (D7) y uso de las existentes para `CONECTA-NUMERO`,
  `CONECTA-AMBIENTE`, `CONECTA-DUPLICADO`
- [ ] T028 `ConectaStubHandler` (tests) con los escenarios del plan §7; `CanalConectaConformidadTests :
  ConformidadDelCanal`
- [ ] T029 Arquitectura: `ElProveedorTecnologicoSoloLoConoceSuAdaptador` ampliada; `ElSecretoDeConectaSoloViajaALa
  ListaBlanca`; `LasCredencialesDeFacturacionNoTocanLaBase` revisada
- [ ] T030 `DependencyInjection`: registro y retiro de la mención a `CanalServicioCentral`; borrar
  `Channels/ServicioCentral/.gitkeep`

## C3 — Pantalla, operación y GitOps

- [ ] T040 `/admin/facturacion-electronica`:
  - selector de modo (Simulado / Conecta – pruebas / Conecta – real) que fija canal y ambiente;
  - tenant de Conecta;
  - extremo de la lista que publica la API (`GET /api/electronic-invoicing/channels`: códigos, extremos y si el
    real está disponible en esta instalación);
  - confirmación `PRODUCCION` con el permiso;
  - resultado de «Verificar credencial»;
  - `IndicadorDeCarga` por zona
- [ ] T041 `FacturacionElectronicaClient` y DTOs de Shared; pruebas de Shared (textos y estados del modo)
- [ ] T042 `crear-secreto-facturacion-electronica.ps1`: ejemplo para `CONECTA-PRUEBAS`/`CONECTA` (campos
  `clientId,clientSecret[,webhookSecret]`), `PRODUCCION` también para el canal `CONECTA`
- [ ] T043 `docs/operaciones/conecta-puesta-en-marcha.md`:
  - crear el cliente API en Conecta;
  - cargar el Secret;
  - configurar el modo;
  - asociar el prefijo;
  - verificar;
  - primera factura;
  - paso a real con su lista de chequeo;
  - cómo volver a Simulado
- [ ] T044 `docs/manual/`: sección de modos en el manual de facturación electrónica
- [ ] T045 GitOps (`ingenia365-gitops`, rama propia, sin push sin OK):
  - componente `facturacion-electronica-credenciales` (volumen del Secret sin `subPath`, `optional: true`);
  - variables por overlay (pruebas en DEV, QA y PDN; real sólo en PDN);
  - revisión de la salida de red

## C4 — Verificación final

- [ ] T050 Integración `CanalConectaTests` en PostgreSQL y SQL Server (plan §7)
- [ ] T051 Suites completas sin contenedores y compilación de la solución en cero errores
- [ ] T052 Prueba manual en Conecta QA y en Conecta PDN con el tenant en habilitación (requiere D1 en Conecta y el
  cliente API): factura, nota crédito y nota débito de COOFLOPAL en `SETT`; latencia con varias cajas; registro de
  resultados en la receta
- [ ] T053 Revisión del cotejo pendiente (tarifa 0 = exento, escala del porcentaje de descuento,
  `referencedInvoice.id`, régimen 48/49, correo del adquirente) y ajuste del traductor

## C5 — Webhook (opcional, P3)

- [ ] T060 `ConectaWebhookCommand` + endpoint anónimo, verificación HMAC, deduplicación, adelanto de `NextAttemptAt`
- [ ] T061 Pruebas: firma válida, vencida, alterada, repetida; cooperativa ajena
- [ ] T062 Receta: alta del webhook en Conecta y su secreto en el Secret
