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

- [x] T010 Canónico v2: `ParteCanonica.GivenNames/FamilyNames` y `DireccionCanonica.CityName/DepartmentCode/
  DepartmentName`, con `JsonIgnore(WhenWritingNull)`; `VersionConDatosDeConecta = 2`
- [x] T011 `ConstructorDelCanonico` llena v2 (`NombresDePersona`); `FotoFiscalDeEntrada` y `FuenteDeEmisionDeInventario`
  entregan nombres, municipio y departamento (DIVIPOLA)
- [x] T012 **Sin columna** (ajuste de C1): `EsquemaDelCanonico.Para(canal sellado)`, v2 sólo con `CONECTA*`. La prueba
  comprueba que una v1 con los datos nuevos en la entrada da los mismos bytes
- [x] T013 `COR_ElectronicEmissionSettings.ChannelTenantId`, `ChannelEndpoint`; DTO y `CanalDisponibleDto` con ambiente
  exigido, extremos y si es de producción
- [x] T014 Migración par aditiva `CanalConecta` (PostgreSQL y SQL Server)
- [x] T015 `ContextoDeCanal.Canal` (`ParametrosDelCanal`) desde la vigencia sellada, en los cuatro sitios que lo arman;
  `IDeclaraParametrosDelCanal` y `ReglasDeParametrosDelCanal` en Application
- [x] T016 `ConfigureEmissionCommand`: campos nuevos, rechazos del contrato §9, `ConfirmProduction`, permiso
  `ElectronicInvoicing.Settings.ActivateProduction` (sembrado con el catálogo de facturación electrónica)
- [x] T017 `GuardiaDeEmisionFiscal.Decidir`: `ChannelEnvironmentMismatch`, `ProductionChannelNotAllowedHere` (lee
  `CanalesExternosOptions.AmbienteDePruebas`; sin ella asume pruebas) y `ChannelParametersMissing`. D8 va en
  `ContingenciaDeLaDian.AbrirDeFacturacionAsync` (`NotSupportedByChannel`)
- [x] T018 Pruebas: `ParametrosDelCanalTests` (configuración, guardia, 03, esquema, nombres, departamento) y tres
  casos v2 en `ConstructorDelCanonicoTests`; las 1.006 de facturación electrónica y permisos pasan

## C2 — Adaptador

- [x] T020 `OpcionesDeCanal`: `Endpoints`, `SyncWaitSeconds`, `PollIntervalMs` y la compuerta de D1
  `ConectaAceptaNumeroDelErp` (falsa por defecto: la verificación falla con `CONECTA-NUMERACION-DEL-ERP` hasta que
  Conecta acepte el número del ERP y operaciones la encienda). La lista blanca exige https, salvo un servidor local
- [x] T021 DTOs de Conecta en `ModeloDeConecta.cs`, con los nombres del OpenAPI, sin referenciar el repositorio Conecta365
- [x] T022 `TraductorAConecta` (puro). Las pruebas afirman campo por campo sobre el JSON enviado, sin archivos dorados
  aparte: número del ERP, tenant, porcentajes, nombres separados, régimen, D2, D3, D9, notas con factura referida
- [x] T023 `TraductorDeResultadosConecta` (puro); una prueba por fila de la tabla de errores
- [x] T024 `TokensDeConecta` (caché por extremo, cliente y huella del secreto; un pedido a la vez; renovación tras 401)
- [x] T025 Transporte dentro de `CanalConecta` sobre los dos `HttpClient` de `AgregarClientesDelCanal`; clave de
  idempotencia UUID v5 de la clave del ERP
- [x] T026 `CanalConecta`:
  - dos registros: `CONECTA-PRUEBAS` con extremos; `CONECTA` además en un proceso de producción, por el parámetro
    nuevo `procesoDeProduccion` de `AddElectronicInvoicing`;
  - espera síncrona tras el 202;
  - comprobación del número y del ambiente;
  - «validado» sólo con los tres artefactos;
  - QR del `sts:QRCode`;
  - `ProbarAsync` con ambiente, NIT, `missing` y D1
- [x] T027 Sin alerta nueva. `CONECTA-NUMERO`, `CONECTA-AMBIENTE` y `CONECTA-DUPLICADO` son `InvalidData`, que ya
  levanta `Dian.DocumentoRechazado`. La diferencia de totales (D7, puente) queda como notificación `CONECTA-TOTALES`
  en la transmisión. El adaptador no tiene `IAlertas`; la alerta crítica llega con el pedido D7 de Conecta, que
  rechaza antes de consumir el número
- [x] T028 `ConectaStubHandler` y `CanalConectaConformidadTests : ConformidadDelCanal`, más `CanalConectaTests`
  (71 pruebas del canal en total)
- [x] T029 Arquitectura: `ElSecretoDeConectaSoloViajaALaListaBlanca` (no lee la base, un solo sitio convierte texto en
  URL y lo toma de la lista, no registra secretos). `ElProveedorTecnologicoSoloLoConoceSuAdaptador` y
  `LasCredencialesDeFacturacionNoTocanLaBase` siguen verdes sin cambios: 930 de arquitectura
- [x] T030 `DependencyInjection`: registro de Conecta y retiro de `Channels/ServicioCentral`

## C3 — Pantalla, operación y GitOps

- [x] T040 `/admin/facturacion-electronica`:
  - el selector de canal muestra el nombre de cada modo; un canal que exige ambiente lo fija;
  - con Conecta, campos «Cooperativa en Conecta» y «Servicio» (las claves de `GET /settings` → `availableChannels[].endpoints`;
    sin ruta nueva);
  - para el modo real, aviso de validez legal y confirmación `PRODUCCION`; «Guardar» sólo con todo completo;
  - la vigencia actual muestra servicio y cooperativa;
  - el resultado de «Verificar credencial» ya mostraba los mensajes del canal;
  - `IndicadorDeCarga` sin cambios
- [x] T041 DTOs de Shared (`VigenciaDeEmisionDto`, `CanalDisponibleDto`, `ConfiguracionDeEmisionRequest`) y prueba del
  cliente: manda cooperativa, servicio y confirmación, nunca URL ni secreto
- [x] T042 Guion del Secret: ejemplos de `CONECTA-PRUEBAS` y `CONECTA`; `CONECTA` sólo con `-Ambiente pdn`, que ya
  pedía `PRODUCCION`
- [x] T043 `docs/operaciones/conecta-puesta-en-marcha.md` (índice de documentación actualizado)
- [x] T044 Manual: sección «Los modos de IngenIA Conecta» y aviso inicial actualizado
- [x] T045 GitOps, rama `erp/028-canal-conecta` de `ingenia365-gitops` (commit `c19373c`, **sin push**):
  - componente `facturacion-electronica`;
  - literales por overlay. Las secciones van con guion bajo (`CONECTA_PRUEBAS`), porque `envFrom` puede descartar
    variables con guion; la API acepta los dos (`ElectronicInvoicingOptions.Canal`);
  - `kubectl kustomize` de los tres overlays sin errores.

  Queda pendiente revisar la salida de red en el clúster (no hay NetworkPolicy de salida en `workloads/erp`).

## C4 — Verificación final

- [x] T050 Integración `CanalConectaE2E` (la fixture registra `CONECTA-PRUEBAS` contra el `ConectaStubHandler` enlazado
  de las unitarias): 3 de 3 en **PostgreSQL** y 3 de 3 en **SQL Server**. Cubre:
  - configuración y verificación (que además compara el NIT);
  - factura con el número del ERP y validada;
  - reintento sin duplicar;
  - nota crédito referida al CUFE;
  - modo real inexistente fuera de producción;
  - credencial de otra cooperativa que no sirve.
- [x] T051 Solución completa sin errores. Pasan Domain 3.183, Application 9.050, Architecture 930 y Shared 729.

  En la corrida de las 15 e2e de facturación electrónica en PostgreSQL hubo dos fallas ajenas a la 028:
  - `CanalYConcurrenciaTests.Dos_emisiones_concurrentes…` pasó al repetirla;
  - `DocumentosElectronicosTests.Una_resolucion_de_1_a_10…` cae en la nota crédito con
    `Payments.CashSessionRequired` (regla de caja, no de este cambio). **Falla igual en `develop` d810d07f sin la
    028** (corrida de control del 2026-11-27): es preexistente.
- [x] T051b D1 en Conecta (v0.1.4, 2026-11-28). Ajustes del adaptador al contrato final:
  - el 409 `number-conflict` trae `status` dos veces y ya no deja ilegible el problema;
  - el número ocupado por otro documento no se adopta (`CONECTA-NUMERO-OCUPADO`);
  - `numbering-out-of-range` y `client-numbering` tienen su regla;
  - la verificación exige `clientAssignsNumber` y muestra el contador.

  GitOps `0c23c4e` enciende la compuerta sólo para `CONECTA-PRUEBAS` en PDN.
- [ ] T052 Prueba manual en Conecta PDN con el tenant en habilitación. Ya no la bloquea D1; falta el cliente API de
  COOFLOPAL, desplegar la 028 y numerar `SETT` desde 98598: factura, nota crédito y nota débito en `SETT`,
  latencia con varias cajas, resultados en la receta
- [ ] T053 Cotejo con el contador (tarifa 0 = exento, escala del descuento, `referencedInvoice.id`, régimen 48/49, correo
  del adquirente), con `/preview` de Conecta, antes del modo real

## C5 — Webhook (opcional, P3)

- [ ] T060 `ConectaWebhookCommand` + endpoint anónimo, verificación HMAC, deduplicación, adelanto de `NextAttemptAt`
- [ ] T061 Pruebas: firma válida, vencida, alterada, repetida; cooperativa ajena
- [ ] T062 Receta: alta del webhook en Conecta y su secreto en el Secret
