---

description: "Tareas de la feature 011: adjuntos con subida y descarga directas al almacén"
---

# Tasks: Adjuntos con subida y descarga directas al almacén

**Input**: documentos de diseño en `specs/011-adjuntos-s3-prefirmadas/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md), [contracts/almacen.md](contracts/almacen.md), [quickstart.md](quickstart.md)

**Tests**: incluidas. El plan las pide explícitamente (research R17): Application con un almacén
falso, integración con MinIO (necesita Docker Desktop encendido) y tres reglas de Architecture. Dentro
de cada historia, las pruebas se escriben primero y **tienen que fallar** antes de implementar.

**Organization**: por historia de usuario. El orden de las fases es el **orden de entrega** del plan:
E1 = Fundacional + US3; después US1, US2 y US4 (flujo directo); US5 (credencial) puede avanzar en
paralelo y **tiene que estar terminada antes de producción** (FR-048).

**Revisión del 2026-09-23**: se aplicaron las nueve correcciones del análisis de consistencia (C1, F1
con la opción A, G1, G2, O1, O2, I1, G3, G4 y G5). Entraron tres tareas nuevas —T001, T065 y T081— y
el resto se renumeró.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin depender de tareas pendientes)
- **[Story]**: historia a la que pertenece (US1…US5)
- Las rutas son relativas a la raíz del repositorio

---

## Phase 1: Setup

**Purpose**: cortar ya la exposición de la llave, confirmar lo que el diseño da por supuesto y
preparar las herramientas de prueba.

- [ ] T001 Rotar **ya** la llave filtrada de adjuntos, sin esperar a US5, en este orden:
  - *Replanteo (2026-09-23)*: la llave ya no la usa ningún ambiente (DEV y QA con el sidecar; el código de producción es anterior a `S3BlobStore`). En vez de rotarla, **desactivarla ya** tras mirar su «Último uso», y borrar el usuario después de T075; producción entra directo con credenciales temporales (docs/operaciones/despliegue-adjuntos-directos.md). La llave de P1 sigue aparte.
  - crear una clave nueva para `ingenia365-erp-adjuntos` en la consola;
  - instalarla con `tools/scripts/crear-bucket-adjuntos.ps1 -SoloSecreto`;
  - relevar los pods de la API en los tres ambientes;
  - **recién después**, desactivar la filtrada.

  Desactivar también la llave de P1, verificando antes que no la use `s3-backup-creds`. Registrarlo en docs/operaciones/estado-y-pendientes.md. Desactivar la vieja antes de instalar la nueva dejaría sin adjuntos a DEV y QA (research R19)
- [X] T002 [P] Espiga S1 (research R1): con AWSSDK.S3 4.0.103, armar un POST prefirmado con `ContentLengthRangeCondition` [n, n], `ExactMatchCondition` sobre `Content-Type` y el campo `x-amz-checksum-sha256`, y subir contra MinIO y contra `ingenia365-erp-attachments/dev/`, probando otro tamaño, otro tipo y otra huella. Comprobar además qué pasa con una subida que empezó antes de vencer la autorización y termina después. Anotar el resultado y la decisión final en specs/011-adjuntos-s3-prefirmadas/research.md
- [ ] T003 [P] Espiga S3 (research R15): leer `window.location.origin` dentro de BlazorWebView en Windows y Android desde src/Presentation/IngenIA365ERP.App, y anotar los orígenes reales en specs/011-adjuntos-s3-prefirmadas/contracts/almacen.md (CORS)
  - *Avance (2026-09-23)*: confirmado en el código de `dotnet/maui` (rama `net10.0`): `https://0.0.0.1` en Windows y Android, `app://0.0.0.1` en iOS y Mac (contracts/almacen.md). El guion del bucket ya los pone en el CORS; hay que volver a correrlo. Verlo en un dispositivo queda para T081.
- [X] T004 Agregar `Testcontainers.Minio` (misma versión mayor que los otros Testcontainers) a tests/IngenIA365ERP.API.IntegrationTests/IngenIA365ERP.API.IntegrationTests.csproj y crear el fixture tests/IngenIA365ERP.API.IntegrationTests/Attachments/MinioFixture.cs, con el bucket versionado y la colección «Almacén MinIO»
- [X] T005 Crear los límites configurables `MaxBytes` (25 MB), `SubidaMinutos` (5, tope 5) y `DescargaSegundos` (60, tope 300) en src/Core/IngenIA365ERP.Application/Attachments/Common/LimitesDeAdjuntos.cs —en Application y no en `AttachmentStorageSettings`, porque los aplican validadores y comandos de Application (Principio II)—, enlazados a la sección `AttachmentStorage` y validados al arrancar desde src/Infrastructure/IngenIA365ERP.Storage/DependencyInjection.cs; reflejarlo en src/Presentation/IngenIA365ERP.API/appsettings.json. El validador de `UploadAttachmentCommand` pasa a leer el máximo configurado; `AttachmentPolicy.MaxBytes` queda como valor por defecto

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: modelo, almacén y reglas que usan todas las historias. Además cierra ya los dos huecos de
seguridad del Contexto: la subida sin comprobar el dueño y la lectura contable sin permiso.

**⚠️ CRITICAL**: ninguna historia empieza antes de terminar esta fase.

- [X] T006 [P] Crear los enums `FormatoDeAdjunto` y `EstadoDeAdjunto`, con los valores de data-model.md, en src/Core/IngenIA365ERP.Domain/Enums/Core/FormatoDeAdjunto.cs y src/Core/IngenIA365ERP.Domain/Enums/Core/EstadoDeAdjunto.cs
- [X] T007 Agregar `Format`, `Status`, `UploadExpiresAt`, `ConfirmedAt`, `ConfirmedBy` y `RejectionReason` a src/Core/IngenIA365ERP.Domain/Entities/Core/Attachment.cs
- [X] T008 Mapear las seis columnas en src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Core/AttachmentConfiguration.cs: `Format` con valor por defecto 1, `Status` con 2, `RejectionReason` de 300 y `ConfirmedBy` de 100
- [X] T009 Generar el par de migraciones `AdjuntosDirectos` con tools/scripts/add-migration.ps1, en src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/Application/ y src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/Application/. Revisar que sólo agregue las seis columnas —el scaffold de N4 arrastró un cambio ajeno— y que `Down` las quite. *Hecho con la fábrica de diseño como proyecto de arranque (`--startup-project` = el propio proyecto de migraciones): el script arranca la API, que en un worktree no tiene la cadena de conexión porque `appsettings.Development.json` está en `.gitignore`. Resultado verificado: seis columnas y 24 líneas de snapshot por proveedor, nada ajeno*
- [X] T010 Ampliar src/Core/IngenIA365ERP.Application/Common/Interfaces/Storage/IBlobStore.cs con `FirmarSubidaAsync`, `FirmarDescargaAsync`, `ConsultarAsync` (si existe y su tamaño) y `LeerInicioAsync` (los primeros n bytes), junto con los tipos `AutorizacionDeSubida`, `EnlaceDeDescarga` y `EstadoDelObjeto`. Agregar además la abstracción `IAlmacenLocal`, con recibir y leer por token, para las rutas de desarrollo (R16)
- [X] T011 Implementar en src/Infrastructure/IngenIA365ERP.Storage/Services/S3BlobStore.cs:
  - el POST prefirmado (R1, con lo que decidió T002);
  - el GET prefirmado con `ResponseHeaderOverrides`: `Content-Disposition` según RFC 5987, `Content-Type` con `charset` opcional y `Cache-Control: no-store` (R2);
  - el HEAD y el GET con `Range`;
  - el 403 de GET y HEAD tratado como `FileNotFoundException` (R4).
- [X] T012 Implementar las mismas operaciones, más `IAlmacenLocal`, en src/Infrastructure/IngenIA365ERP.Storage/Services/LocalEncryptedFileStore.cs para `Provider = Local`, con tokens de DataProtection que llevan operación, clave, tipo, tamaño, huella y vencimiento (el vencimiento va dentro del token: no hizo falta el paquete de `ITimeLimitedDataProtector`), y que apuntan a `/api/attachments/local-blob/{token}` (R16). Los archivos `Direct` se guardan sin cifrar
- [X] T013 Crear las rutas de desarrollo de src/Presentation/IngenIA365ERP.API/Endpoints/Attachments/LocalBlobEndpoints.cs como **reenvíos puros al `ISender`** (Principio III):
  - `POST` → `RecibirSubidaLocalCommand(token, Stream)`;
  - `GET` → `LeerBlobLocalQuery(token)`.

  Los dos viven en src/Core/IngenIA365ERP.Application/Attachments/Local/, con sus validadores (Principio VIII), sobre `IAlmacenLocal`. Se registran sólo si `AttachmentStorage:Provider == Local` **y el ambiente no es Production** (contracts/api.md §10). *Hubo que sumar `/api/attachments/local-blob/` a las rutas exentas de `TenantResolutionMiddleware`: sin eso, el POST anónimo moría con 401 antes de llegar a la ruta*
- [X] T014 [P] Agregar `OwnerNotAllowed`, `OwnerLocked`, `NotAvailable`, `UseDownloadLink` y `Busy` a src/Core/IngenIA365ERP.Application/Attachments/Common/AttachmentErrorCodes.cs
- [X] T015 Reescribir src/Core/IngenIA365ERP.Application/Attachments/Common/AdjuntosDeModulo.cs con las reglas de data-model.md y adaptar a sus llamadores (DownloadAttachment, DeleteAttachment y ListAttachments):
  - lectura por módulo: `AccountingDocument` exige `Accounting.Vouchers.View` (FR-025);
  - `PuedeSubirAsync`: sólo a un `AccountingDocument` que exista en la cooperativa, con `Accounting.Vouchers.Create`; los tipos que genera un módulo quedan prohibidos y el resto no está habilitado (FR-019);
  - `PuedeBorrarAsync`: un comprobante sólo mientras esté en borrador.
- [X] T016 [P] Ampliar tests/IngenIA365ERP.Application.Tests/Attachments/AdjuntosDeModuloTests.cs con:
  - leer un soporte contable sin `Accounting.Vouchers.View`;
  - subir a un tipo de módulo, a un documento inexistente, a otra cooperativa y sin permiso de escritura;
  - borrar según el estado del comprobante.
- [X] T017 Registrar en src/Presentation/IngenIA365ERP.API/Program.cs el limitador de concurrencia `adjuntos` (`AddRateLimiter` + `AddConcurrencyLimiter`, 32 permisos y cola de 64 configurables, 429 con el sobre `Attachments.Busy`) y `UseRateLimiter`, y aplicarlo al grupo de src/Presentation/IngenIA365ERP.API/Endpoints/AttachmentsModule.cs
- [X] T018 Retirar la ruta `POST /api/attachments` (subida multipart a través de la API) de src/Presentation/IngenIA365ERP.API/Endpoints/AttachmentsModule.cs, sin alias. Hoy no la usa ninguna pantalla, y quitarla cierra el hueco del Contexto (punto 8) hasta que exista la subida segura de US1
- [X] T019 Reescribir tests/IngenIA365ERP.API.IntegrationTests/Attachments/AttachmentAuthorizationTests.cs y tests/IngenIA365ERP.API.IntegrationTests/Attachments/AttachmentEncryptionAtRestTests.cs, que sembraban por la ruta retirada. Sembrar con un sembrador **sólo de pruebas**, tests/IngenIA365ERP.API.IntegrationTests/Attachments/SembradorDeAdjuntosAnteriores.cs, que cifra con `IAttachmentCipher`, sube con `IBlobStore.PutAsync` y crea la fila con `Format = AppEncrypted`. Así siguen probando el formato anterior aunque T062 cambie `UploadAttachmentCommand`
  - *Hecho (2026-09-23)*: la prueba de cifrado en reposo no sembraba por la ruta (usa el almacén directo) y sólo cambió en T013. Las pruebas con sesión sobre el formato anterior quedaron en `AdjuntosDelFormatoAnteriorTests` (colección «Contabilidad e2e»: bajar, listar, y el de otra cooperativa no existe), y el retiro de la ruta se afirma sobre la tabla de rutas, no sobre la respuesta: un 404 también es lo que contesta una ruta existente sin permiso.
- [X] T020 [P] Actualizar tests/IngenIA365ERP.API.IntegrationTests/Attachments/S3BlobStoreTests.cs, sobre el `IAmazonS3` falso: el 403 en GET y HEAD es «no está», el HEAD, el rango, y la forma del POST y del GET prefirmados
- [X] T021 [P] Crear tests/IngenIA365ERP.API.IntegrationTests/Attachments/AlmacenPrefirmadoTests.cs sobre `MinioFixture`:
  - el POST rechaza otro tamaño, otro tipo y otra clave;
  - el GET trae el nombre con tildes y vence;
  - `DeleteObject` deja la marca y la versión sobrevive.
  - *Hecho (2026-09-23)*: la imagen es `quay.io/minio/minio` —MinIO ya no publica en Docker Hub— y destapó un defecto: el enlace de descarga se firmaba con https aunque `ServiceUrl` fuera http; `S3BlobStore` ahora toma el esquema del cliente.

**Checkpoint**: modelo, almacén y reglas listos. Compila, y las suites de Application y Architecture
están verdes.

---

## Phase 3: User Story 3 - Nada se borra solo; borrar es explícito y recuperable (Priority: P1) 🎯 MVP (E1)

**Goal**: borrar es siempre un acto de una persona, auditado, que retira el objeto a una papelera de
90 días. Los soportes de un comprobante contabilizado y los adjuntos de módulo no se pueden borrar por
ninguna vía. Descartar un borrador con soportes exige confirmarlo.

**Independent Test**:
- borrar el soporte de un borrador: desaparece y queda auditado;
- recuperarlo con la receta de soporte;
- intentar borrar por la API el soporte de un comprobante contabilizado: 422 `Attachments.OwnerLocked`;
- descartar un borrador con soportes: sin confirmar, 422 `Accounting.Document.HasAttachments`
  (quickstart §5).

### Tests for User Story 3 ⚠️

- [X] T022 [P] [US3] Crear tests/IngenIA365ERP.Application.Tests/Attachments/DeleteAttachmentCommandHandlerTests.cs:
  - orden objeto → fila: si falla el guardado, el objeto ya está en la papelera, y reintentar completa;
  - `OwnerLocked` con un comprobante contabilizado;
  - `OwnedByModule` sin cambios;
  - el comando queda auditado.
- [X] T023 [P] [US3] Crear tests/IngenIA365ERP.Application.Tests/Accounting/Documents/DiscardDraftWithAttachmentsTests.cs:
  - 422 `HasAttachments` con `count`;
  - con la marca, borra los soportes y descarta;
  - vale también para borradores `AP`.
- [X] T024 [P] [US3] Crear tests/IngenIA365ERP.Architecture.Tests/Principles/NadieBorraAdjuntosPorSuCuenta.cs: sólo `DeleteAttachment`, `DiscardDraft`, el rechazo de `ConfirmarSubida` (FR-001, opción A) y la reversión de `UploadAttachment` llaman a `IBlobStore.DeleteAsync`. Comprobar que la prueba falla al agregar otro llamador

### Implementation for User Story 3

- [X] T025 [US3] En src/Core/IngenIA365ERP.Application/Attachments/DeleteAttachment/DeleteAttachmentCommand.cs:
  - aplicar `PuedeBorrarAsync` (`OwnerLocked`);
  - retirar el objeto (`DeleteAsync`) **antes** de la baja lógica (R8);
  - registrar con Serilog cooperativa y adjunto;
  - quitar el comentario que promete un «GC programado».
- [X] T026 [US3] En src/Core/IngenIA365ERP.Application/Accounting/Documents/DocumentCommands.cs, pasar a `DiscardDraftCommand(Guid PublicId, bool DeleteAttachments = false)`, con su validador:
  - con soportes y sin la marca: 422 `Accounting.Document.HasAttachments`;
  - con la marca: retirar los objetos y después dar de baja las filas y el borrador en una sola unidad de trabajo (R10).
- [X] T027 [US3] Aceptar `?deleteAttachments=` en `DELETE /drafts/{id}` de src/Presentation/IngenIA365ERP.API/Endpoints/Accounting/DocumentsEndpoints.cs y registrar el código nuevo en specs/009-contabilidad-niif/contracts/api.md
- [X] T028 [US3] Agregar `canDelete` (según `PuedeBorrarAsync`) a src/Core/IngenIA365ERP.Application/Attachments/ListAttachments/ListAttachmentsByOwnerQuery.cs. Sin permiso de lectura del módulo, la consulta devuelve una lista vacía
- [X] T029 [US3] En src/Presentation/IngenIA365ERP.Shared/Components/Shared/AttachmentList.razor, mostrar Borrar según el `canDelete` del servidor —no según `PermitirEliminar`—, con confirmación y el indicador «Borrando…» (`IndicadorDeCarga` + `EstadoDeCarga`)
- [X] T030 [US3] En src/Presentation/IngenIA365ERP.Shared/Pages/Contabilidad/Comprobante.razor, al descartar un borrador con soportes, pedir confirmación («Tiene N soportes; se borrarán y quedarán 90 días en la papelera») y reintentar con `deleteAttachments=true`
- [X] T031 [US3] Agregar a tools/scripts/crear-bucket-adjuntos.ps1 el ciclo de vida con `ExpiredObjectDeleteMarker` y la política de bucket «sólo TLS» (contracts/almacen.md §1). Se puede aplicar con el perfil `ingenia365`, porque no necesita permisos de IAM
- [X] T032 [US3] Reescribir la sección de borrado de docs/operaciones/adjuntos-en-s3.md con lo que pasa de verdad, las recetas de **recuperación** (quitar la marca y reactivar la fila) y de **supresión definitiva** (todas las versiones y la constancia) con la credencial administrativa, y sin ninguna promesa de purga automática (FR-007, FR-008). Corregir además las afirmaciones falsas sobre el borrado en:
  - src/Infrastructure/IngenIA365ERP.Storage/Configuration/AttachmentStorageSettings.cs (línea 33);
  - src/Core/IngenIA365ERP.Application/Attachments/UploadAttachment/UploadAttachmentCommand.cs (línea 149, «GC programado»);
  - docs/operaciones/adjuntos-en-s3.md (línea 30).
- [X] T033 [P] [US3] Corregir el párrafo **Adjuntos** de CLAUDE.md: retirar «un adjunto se borra cuando su dueño lo borra» y describir la papelera de 90 días y las reglas de conservación

**Checkpoint**: E1 entregable. Borrar funciona y es recuperable, y los dos huecos de seguridad están
cerrados. Se puede promover sin tocar nada de AWS.

---

## Phase 4: User Story 1 - Subir un soporte sin pasar por el servidor (Priority: P1)

**Goal**:
- la persona sube un soporte a un comprobante;
- el archivo va directo al almacén;
- el ERP sólo autoriza y confirma, y lo marca «disponible» o «rechazado».

**Independent Test**:
- en un borrador, subir un PDF de 20 MB: queda «disponible» y el POST va al almacén, no a la API;
- un `.exe` renombrado a `.pdf` queda «rechazado»;
- subir a la PILA da 422 (quickstart §1–§3).

### Tests for User Story 1 ⚠️

- [X] T034 [P] [US1] Crear tests/IngenIA365ERP.Application.Tests/Attachments/FirmaDeContenidoTests.cs con los casos de R6: cada tipo, un ejecutable renombrado, una imagen declarada como documento, un binario declarado como texto y un archivo vacío
- [X] T035 [P] [US1] Crear tests/IngenIA365ERP.Application.Tests/Attachments/SolicitarSubidaDeAdjuntoCommandHandlerTests.cs:
  - tamaño, tipo y archivo vacío;
  - `OwnerNotAllowed`;
  - 404 sin permiso o con un documento inexistente;
  - la fila queda `Uploading`, con `UploadExpiresAt`;
  - la firma lleva tamaño, tipo y huella exactos.
- [X] T036 [P] [US1] Crear tests/IngenIA365ERP.Application.Tests/Attachments/ConfirmarSubidaDeAdjuntoCommandHandlerTests.cs:
  - `Available`;
  - `Rejected` por firma y por tamaño, con el objeto retirado a la papelera (FR-001, opción A);
  - `Incomplete`;
  - `Uploading` con la autorización vigente;
  - confirmar dos veces da el mismo resultado.
- [X] T037 [P] [US1] Crear tests/IngenIA365ERP.Application.Tests/Attachments/RenovarSubidaDeAdjuntoCommandHandlerTests.cs: sólo con `Incomplete` o `Uploading` vencido, sobre la misma fila; `NotAvailable` en cualquier otro estado
- [X] T038 [P] [US1] Crear tests/IngenIA365ERP.API.IntegrationTests/Attachments/SubidaDirectaTests.cs, por HTTP y sobre MinIO:
  - solicitar → POST al almacén → confirmar → `Available`;
  - un ejecutable renombrado → `Rejected`;
  - un tipo de módulo → 422;
  - otra cooperativa → 404.
  - *Hecho (2026-09-23)*: sobre `ApiConAlmacenS3Fixture`, el host de siempre con `Provider = S3` apuntando a MinIO (la fixture central ganó dos ganchos para eso). Suma además el 400 con `data.maxBytes`.
- [X] T039 [P] [US1] Crear tests/IngenIA365ERP.Architecture.Tests/Principles/LosAdjuntosNoPasanPorElServidor.cs: ninguna ruta de AttachmentsModule recibe `IFormFile` ni `Stream`, salvo LocalBlobEndpoints

### Implementation for User Story 1

- [X] T040 [US1] Crear el detector puro src/Core/IngenIA365ERP.Application/Attachments/Common/FirmaDeContenido.cs, con la tabla de firmas de R6
- [X] T041 [US1] Crear src/Core/IngenIA365ERP.Application/Attachments/SolicitarSubida/SolicitarSubidaDeAdjuntoCommand.cs, con su validador: `PuedeSubirAsync`, la fila `Uploading` y `FirmarSubidaAsync` (contracts/api.md §1)
  - *Hecho (2026-09-23)*: tamaño, tipo y vacío los responde el handler con los códigos del contrato; el validador sólo mira la forma (un fallo del validador sale siempre como `Validation.Invalid`).
- [X] T042 [US1] Crear src/Core/IngenIA365ERP.Application/Attachments/RenovarSubida/RenovarSubidaDeAdjuntoCommand.cs, con su validador (§2)
  - *Hecho (2026-09-23)*: re-firma la **misma clave** (`SolicitudDeSubida.Referencia`), y si el objeto ya llegó responde `NotAvailable` pidiendo confirmar en vez de volver a subir.
- [X] T043 [US1] Crear src/Core/IngenIA365ERP.Application/Attachments/ConfirmarSubida/ConfirmarSubidaDeAdjuntoCommand.cs, con su validador (§3): `ConsultarAsync`, `LeerInicioAsync` de 8 KiB y `FirmaDeContenido`. Un rechazo retira el objeto a la papelera (R7, FR-001 opción A)
- [X] T044 [US1] Agregar `status`, `format`, `rejectionReason` y `needsConfirmation` (`Uploading` con la autorización vencida) a src/Core/IngenIA365ERP.Application/Attachments/ListAttachments/ListAttachmentsByOwnerQuery.cs (§4)
- [X] T045 [US1] Agregar las rutas §1–§3 (`uploads`, `{id}/upload-url` y `{id}/confirm`) a src/Presentation/IngenIA365ERP.API/Endpoints/AttachmentsModule.cs. Sólo reenvían al `ISender`
- [X] T046 [US1] Crear src/Presentation/IngenIA365ERP.Shared/wwwroot/js/adjuntos.js:
  - lee el `File` del input;
  - calcula el SHA-256 en base64 con WebCrypto;
  - arma el POST `FormData` con los `fields` en orden y el archivo al final;
  - le devuelve a .NET sólo el resultado: **los bytes nunca cruzan a .NET**.
- [X] T047 [US1] Registrar adjuntos.js con `@Assets[...]` en src/Presentation/IngenIA365ERP.Web/Components/App.razor y en src/Presentation/IngenIA365ERP.App/wwwroot/index.html
- [X] T048 [US1] Crear src/Presentation/IngenIA365ERP.Shared/Services/Adjuntos/AdjuntosClient.cs, con los DTOs de solicitar, renovar, confirmar y listar, sin poner la cabecera `Authorization` a mano (`ElTokenDeSesionLoPoneElHandler`), y registrarlo donde se registran los demás clientes tipados
- [X] T049 [US1] Crear src/Presentation/IngenIA365ERP.Shared/Components/Shared/SubirSoporte.razor:
  - input de archivo con validación previa de tipo y tamaño (Principio VIII);
  - interop con adjuntos.js y confirmación;
  - indicador «Subiendo…» (`IndicadorDeCarga`);
  - en MAUI, mientras T003 no confirme los orígenes, el aviso «súbalo desde la web». Ese aviso es **transitorio** y se quita en T081.
  - *Hecho (2026-09-23)*: la app se reconoce por `IFormFactor` (en MAUI no es «Web» ni «WebAssembly»). El tipo se deduce de la extensión en `adjuntos.js`: Windows informa un .csv como `application/vnd.ms-excel` y el servidor lo rechazaría.
- [X] T050 [US1] Borrar el componente huérfano src/Presentation/IngenIA365ERP.Web.Client/Components/AttachmentUploader.razor
  - *Adelantada a E1 (2026-09-23)*: con T018 llamaba a una ruta que ya no existe; nadie la usaba.
- [X] T051 [US1] En src/Presentation/IngenIA365ERP.Shared/Components/Shared/AttachmentList.razor:
  - mostrar el estado: subiendo, disponible, rechazado con su motivo, subida incompleta;
  - confirmar solo los `needsConfirmation`;
  - ofrecer Reintentar;
  - pasar a `AdjuntosClient`, retirando `Peticion` y el comentario desfasado sobre el token.
- [X] T052 [US1] Poner `SubirSoporte` junto a la lista de soportes, en borradores y en contabilizados (FR-017), en src/Presentation/IngenIA365ERP.Shared/Pages/Contabilidad/Comprobante.razor

**Checkpoint**: se suben soportes a comprobantes directo al almacén, con validación de contenido.

---

## Phase 5: User Story 2 - Bajar un adjunto directo del almacén (Priority: P1)

**Goal**: «Descargar» entrega un enlace de 60 s, después de verificar cooperativa y permiso. Fuerza la
descarga con el nombre y el tipo originales. Lo escrito con el formato anterior se sigue bajando por la
API.

**Independent Test**:
- descargar un adjunto disponible: la huella coincide;
- el mismo enlace, 61 s después, falla;
- sin permiso contable, 404;
- un adjunto viejo baja por la API (quickstart §4).

### Tests for User Story 2 ⚠️

- [X] T053 [P] [US2] Crear tests/IngenIA365ERP.Application.Tests/Attachments/EmitirEnlaceDeDescargaCommandHandlerTests.cs:
  - enlace directo con `Available`;
  - `direct: false` con `AppEncrypted`;
  - 409 `NotAvailable`;
  - 404 sin lectura contable;
  - el comando queda auditado.
- [X] T054 [P] [US2] Crear tests/IngenIA365ERP.API.IntegrationTests/Attachments/DescargaDirectaTests.cs, sobre MinIO:
  - el enlace baja el archivo idéntico, con el nombre con tildes;
  - con `DescargaSegundos = 2`, falla a los 3 s;
  - un `AppEncrypted`, sembrado con `SembradorDeAdjuntosAnteriores`, baja por `GET /api/attachments/{id}`;
  - un `Direct` por esa misma ruta da 409.
  - *Hecho (2026-09-23), con un cambio*: el vencimiento no se prueba por HTTP con 2 s porque `DescargaSegundos` admite de 10 a 300 (FR-021) y el host no arrancaría; lo prueban `AlmacenPrefirmadoTests` contra MinIO con 1 s y `EmitirEnlaceDeDescargaCommandHandlerTests` (el comando firma a los segundos de la configuración).

### Implementation for User Story 2

- [X] T055 [US2] Crear src/Core/IngenIA365ERP.Application/Attachments/EmitirEnlaceDeDescarga/EmitirEnlaceDeDescargaCommand.cs, con su validador (§5)
- [X] T056 [US2] Limitar src/Core/IngenIA365ERP.Application/Attachments/DownloadAttachment/DownloadAttachmentQuery.cs a `AppEncrypted`: con `Direct` responde `Attachments.UseDownloadLink` (§6)
- [X] T057 [US2] Agregar las rutas §5 (`{id}/download-link`) y §6 a src/Presentation/IngenIA365ERP.API/Endpoints/AttachmentsModule.cs
- [X] T058 [US2] Agregar a src/Presentation/IngenIA365ERP.Shared/wwwroot/js/adjuntos.js la descarga por navegación a la URL, con un ancla y `download`, sin `fetch`
- [X] T059 [US2] En src/Presentation/IngenIA365ERP.Shared/Services/Adjuntos/AdjuntosClient.cs y src/Presentation/IngenIA365ERP.Shared/Components/Shared/AttachmentList.razor, Descargar pide el enlace: si es directo, navega; si es del formato anterior, baja por la API como hoy (flujo a descargas.js)

**Checkpoint**: subir y bajar soportes no pasa por el servidor.

---

## Phase 6: User Story 4 - Los archivos que genera el ERP no cargan la memoria del servidor (Priority: P2)

**Goal**:
- PILA, dispersión y definitiva se guardan tal como se generaron, sin cifrado propio;
- PILA y dispersión se bajan con un enlace directo, y la PILA conserva su regla de reconocer el
  descuadre y su `charset`;
- la definitiva se sigue bajando por su ruta de regeneración (FR-030).

**Independent Test**: en QA, generar una PILA y una dispersión, y bajarlas por enlace directo,
idénticas. La PILA sigue pidiendo reconocer el descuadre (quickstart §4.5).

### Tests for User Story 4 ⚠️

- [X] T060 [P] [US4] Actualizar tests/IngenIA365ERP.Application.Tests/Attachments/UploadAttachmentCommandHandlerTests.cs: `Direct`, `Available`, sin cifrar, SHA-256 del contenido, y si falla el guardado la reversión retira el objeto
- [X] T061 [P] [US4] Crear tests/IngenIA365ERP.Application.Tests/Payroll/Pila/EnlaceDePilaTests.cs y tests/IngenIA365ERP.Application.Tests/Payroll/Dispersion/EnlaceDeDispersionTests.cs: la PILA exige el reconocimiento y lleva `charset`; la dispersión sale igual

### Implementation for User Story 4

- [X] T062 [US4] Reescribir src/Core/IngenIA365ERP.Application/Attachments/UploadAttachment/UploadAttachmentCommand.cs según R11: `PutObject` del contenido ya generado, sin `IAttachmentCipher`, con `Status = Available` y `Format = Direct`
- [X] T063 [US4] Agregar el comando de enlace, auditado, en src/Core/IngenIA365ERP.Application/Payroll/Pila/PilaQueries.cs y en src/Core/IngenIA365ERP.Application/Payroll/Dispersion/DisbursementQueries.cs: aplica la regla del módulo y firma con el `Content-Type` y el `charset` de hoy
- [X] T064 [US4] Agregar `POST /{id}/download-link` (§9, con el limitador `adjuntos`) a src/Presentation/IngenIA365ERP.API/Endpoints/Payroll/PilaEndpoints.cs y src/Presentation/IngenIA365ERP.API/Endpoints/Payroll/DisbursementsEndpoints.cs, y dejar `GET /{id}/file` sólo para `AppEncrypted`
- [X] T065 [US4] Reescribir las descargas de tests/IngenIA365ERP.API.IntegrationTests/Payroll/PilaTests.cs (líneas 120–143) y tests/IngenIA365ERP.API.IntegrationTests/Payroll/DispersionTests.cs (línea 104) sobre `POST …/download-link`:
  - sin reconocer el descuadre, se mantiene el error de hoy;
  - con el reconocimiento, se sigue la URL devuelta (en la fixture, la ruta `local-blob`) y se compara el contenido;
  - la versión reemplazada conserva su archivo.
- [X] T066 [US4] En src/Presentation/IngenIA365ERP.Shared/Services/Nomina/NominaClient.Pila.cs y NominaClient.Dispersion.cs, pedir el enlace y navegar a él; en src/Presentation/IngenIA365ERP.Shared/Pages/Nomina/Pila.razor y Dispersion.razor, el indicador de carga
  - *Hecho (2026-09-23)*: las pantallas ya tenían su indicador (`_accion`); con `direct: false` siguen bajando por `/file`.
- [X] T067 [US4] Registrar las rutas y los códigos de §9 en specs/010-nomina-prestaciones-pila-dian/contracts/api.md (lo exige `LosCodigosDeNominaEstanEnElContrato`)

**Checkpoint**: todo adjunto nuevo es `Direct`; PILA y dispersión se bajan por enlace.

---

## Phase 7: User Story 5 - La credencial del ERP es de vida corta y alcance mínimo (Priority: P2)

**Goal**:
- sin llave permanente: credenciales de una hora por Roles Anywhere;
- un rol por ambiente, limitado a su prefijo;
- sin permiso de listar ni de borrar versiones;
- revocable por CRL.

**Independent Test**: desde dentro del pod de la API, listar el bucket y leer `qa/` o `pdn/` da
AccessDenied. La credencial vence a la hora sin cortar la API, y revocar el certificado la corta
(quickstart §6).

- [X] T068 [US5] Crear tools/scripts/crear-certificados-adjuntos.ps1:
  - con openssl crea la CA y los tres certificados en una carpeta **fuera del repositorio** que elige el dueño;
  - instala cada certificado como Secret `erp-adjuntos-certificado` en su namespace, por STDIN sobre SSH;
  - nunca imprime una llave ni la escribe en el repo.
  - *Hecho (2026-09-23)*: CA RSA 3072 de diez años con la llave cifrada por una frase que se pide y no se guarda; certificados EC P-256 de un año (`clientAuth`, sólo firma); se niega a escribir dentro del repositorio; producción sólo con `-Ambientes pdn` y escribiendo PRODUCCION. La secuencia de openssl se probó en una carpeta temporal: la cadena verifica. **Lo corre el dueño.**
- [X] T069 [US5] Crear la plantilla CloudFormation docs/operaciones/plantillas/adjuntos-roles-anywhere.yaml (contracts/almacen.md §2): *trust anchor* con el bundle de la CA, perfil de 3600 s, y tres roles con confianza por CN y política acotada a su prefijo
  - *Hecho (2026-09-23)*: validada localmente (el usuario del perfil `ingenia365` no tiene `cloudformation:ValidateTemplate`). Al crear la pila hay que reconocer `CAPABILITY_NAMED_IAM`.
- [X] T070 [P] [US5] Crear tools/credential-helper/Dockerfile y un job en .github/workflows/ci.yml que descarga `aws_signing_helper` en la versión fijada, verifica la SHA-256 que publica AWS y publica la imagen en ghcr
  - *Hecho (2026-09-23)*: `aws_signing_helper` 1.8.5 (2026-08-24) sobre `distroless/base-debian12:nonroot`; el Dockerfile verifica la SHA-256 y ejecuta el binario antes de empaquetarlo (probado con Docker local). Job `credential-helper` propio —la matriz `docker` descarga artefactos de publicación que el sidecar no tiene— y el job `gitops` fija su digest en los overlays que declaran la imagen.
- [ ] T071 [US5] En tools/scripts/crear-bucket-adjuntos.ps1, quitar la creación del usuario IAM y `-SoloSecreto` (después de T001, que todavía los usa), y agregar el CORS con los orígenes que confirmó T003 (contracts/almacen.md §1). Retirar docs/operaciones/politica-iam-adjuntos.json, que reemplaza la plantilla
  - *Avance (2026-09-23, después)*: los dos orígenes de la app ya están en el CORS del guion (T003).
  - *Adelantado en E3 (2026-09-23)*: el CORS con los tres orígenes **web** ya está en el guion, porque sin él la subida directa no funciona en DEV ni QA. Faltan los orígenes de la app (T003) y lo demás de esta tarea.
  - *Avance (2026-09-23)*: la política transitoria (`politica-iam-adjuntos.json`) ya no permite listar ni leer versiones; hay que pegarla en la consola. Quitar el usuario IAM, `-SoloSecreto` y ese JSON espera a T075, y los orígenes de la app a T003.
- [ ] T072 [US5] Tarea del dueño o de un administrador de AWS, guiada por la sección «Puesta en marcha» de docs/operaciones/adjuntos-en-s3.md: aplicar T069 y T071 en la cuenta 058264424927 y guardar la llave de la CA fuera del clúster. **No** desactiva llaves: eso ya lo hizo T001 con la filtrada, y la transitoria se retira en T075
  - *Avance (2026-09-23)*: el dueño creó la CA y los certificados de DEV y QA (hasta 2027-09-24), el guion instaló el Secret `erp-adjuntos-certificado` en `erp-dev` y `erp-qa`, y la pila `ingenia365-erp-adjuntos-roles-anywhere` quedó creada desde CloudShell con sus cinco salidas. Falta confirmar que la política transitoria sin listar se pegó en la consola.
- [ ] T073 [US5] En GitOps (`ingenia365-gitops`), en workloads/erp/base/api.yaml y en los overlays:
  - el sidecar `aws_signing_helper serve` con el Secret del ambiente;
  - los ARN en cada overlay;
  - `AWS_EC2_METADATA_SERVICE_ENDPOINT`;
  - quitar `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` y el Secret `erp-adjuntos-s3`;
  - corregir el comentario de workloads/erp/base/kustomization.yaml que dice que un adjunto se borra cuando su dueño lo borra.
  - *Preparado (2026-09-23), sin commit ni push*: en el clon de GitOps, rama `roles-anywhere-adjuntos`, el componente `workloads/erp/components/adjuntos-roles-anywhere` (sidecar nativo, ARN desde el ConfigMap `erp-roles-anywhere`, borra las `AWS_*` de la API, certificado montado sólo en el sidecar) y el comentario de `base/kustomization.yaml` corregido. Renderiza con un overlay de prueba. Activarlo en los overlays espera los ARN de T072.
  - *DEV y QA activos (2026-09-23)*: GitOps `2e3f5cb` activa el componente con los ARN y el digest del sidecar, y `aa718f6` le quita la `startupProbe`, que no podía pasar (research R3). Los dos ambientes están Ready con la credencial temporal. Faltan: producción (autorización expresa), quitar de base/api.yaml las `AWS_*` y el Secret `erp-adjuntos-s3` (después de T075), y anotar en el componente que el sidecar depende del `fsGroup` del pod.

  Primero DEV y QA; producción **con autorización expresa** del dueño.
- [ ] T074 [US5] Espiga S2 en DEV:
  - confirmar que el SDK toma la credencial del sidecar y la renueva;
  - medir cuánto vive de verdad una URL firmada cerca del vencimiento de la sesión. Si hace falta, que src/Infrastructure/IngenIA365ERP.Storage/Services/S3BlobStore.cs pida una credencial fresca cuando le queden menos de 10 min;
  - comprobar que `/health/ready` sigue sano con esa credencial y sin permiso de listar: la sonda escribe y borra bajo `{ambiente}/.healthcheck/`, que queda dentro del prefijo del rol (FR-051).

  Anotarlo en specs/011-adjuntos-s3-prefirmadas/research.md (R3)
  - *Avance (2026-09-23)*: el SDK toma la credencial y `/health/ready` sigue sano. Los márgenes de renovación salen del código del sidecar y del SDK: en el peor caso firma con 8 minutos por delante, así que `S3BlobStore` no cambia (research R3). Falta ver con los propios ojos la primera renovación, pasada la hora (03:28 UTC).
- [ ] T075 [US5] Verificar la quickstart §6 —sin listar, sin ver otro prefijo, vencimiento a la hora, revocación por CRL en QA— y que los nombres de los objetos no revelan el archivo (FR-043) ni se agregó ningún servicio con costo fijo (FR-047). **Recién entonces**, borrar en la consola el usuario IAM `ingenia365-erp-adjuntos` con su clave transitoria, y comprobar que ninguna llave permanente accede al bucket de adjuntos (FR-048). Registrarlo en docs/operaciones/estado-y-pendientes.md
  - *Herramientas (2026-09-23)*: `probar-credencial-adjuntos.ps1` (permisos, `ObtieneCredencial`, `SinCredencial`), `crear-certificados-adjuntos.ps1 -Prueba` (certificado desechable de 7 dias) y `revocar-certificado-adjuntos.ps1` (CRL acumulada); receta del ensayo en adjuntos-en-s3.md. En QA pasaron las 14 comprobaciones de permisos. El ensayo de revocacion necesita la frase de la CA y un administrador de AWS.
  - *Avance (2026-09-23)*: en DEV, desde un pod de prueba con el mismo sidecar y el mismo Secret, pasaron las 13 comprobaciones de permisos: sin listar, sin versiones, sin otro prefijo, sin respaldos y sin el rol de otro ambiente (research R3). Faltan: el vencimiento a la hora, la CRL en QA, FR-043, FR-047 y borrar el usuario IAM.

**Checkpoint**: ninguna llave permanente accede al almacén de adjuntos. Es condición para ir a
producción.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T076 [P] Crear tests/IngenIA365ERP.Architecture.Tests/Principles/LasRutasDeAdjuntosEstanAcotadas.cs: las rutas `local-blob` sólo existen con `Provider = Local` y fuera de Production, y el grupo de adjuntos y las rutas de §9 llevan el limitador `adjuntos`
  - *Hecho (2026-09-23)*: 5 pruebas. `local-blob` se registra solo detras de `DebenExistir` (proveedor local, no Production) y nadie mas mapea ese prefijo; los grupos de adjuntos y toda ruta `…/download-link` o `/file` de PILA y dispersion llevan el limitador. Se probo que muerden quitando el limitador de PILA y adelantando un Map.
- [X] T077 [P] Crear tests/IngenIA365ERP.Application.Tests/Attachments/ConfirmarSubidaAsignacionesTests.cs (SC-001, R17): validar un objeto de 1 MB y uno de 25 MB asigna lo mismo, con tolerancia
  - *Hecho (2026-09-23)*: el manejador real sobre el `S3BlobStore` real, con un S3 falso que ignora el rango y manda el objeto entero; validar 1 MB y 25 MB asigna lo mismo (tolerancia 1 MB), el rango pedido es 0–8191, y una prueba hermana comprueba que el instrumento ve 25 MB cuando se lee entero. Leyendo el objeto completo la prueba falla (52 MB contra 2 MB).
- [ ] T078 [P] Actualizar docs/INDICE-DOCUMENTACION.md y docs/operaciones/estado-y-pendientes.md: P2b cerrado con el flujo directo, y P1 con las llaves desactivadas
  - *Avance (2026-09-23)*: indice (adjuntos-en-s3, politica transitoria, plantilla de Roles Anywhere) y estado-y-pendientes (P2b con el flujo directo; P1b nuevo para la llave de adjuntos). P1 sigue abierto: cerrarlo es desactivar las llaves (T001, del dueño).
- [X] T079 Remedir con los comandos de la tabla y actualizar los totales de CLAUDE.md (rutas y pruebas)
  - *Hecho (2026-09-23)*: 772 rutas, 182 paginas, 13 reportes (sin cambios) y 1.778 pruebas sin contenedores (223 + 1.318 + 134 + 101 + 2).
- [X] T080 Crear el guion tools/scripts/carga-adjuntos.ps1 para SC-002: veinte subidas de 25 MB en paralelo contra DEV, registrando la memoria de la API y el tiempo de otra pantalla
  - *Hecho (2026-09-23)*: entra a la API por un tunel SSH al Service (DEV y QA estan detras de Cloudflare Access), inicia sesion con el usuario de quien lo corre (correo, contrasena y codigo TOTP pedidos en el momento), mide la lista de comprobantes y `kubectl top` antes y durante, sube en paralelo directo al bucket, confirma y borra lo subido salvo `-Conservar`. Probado hasta el ingreso (tunel y API); la parte con sesion la corre el dueño en T082.
- [ ] T081 Verificar en MAUI, en Windows y en Android, la subida y la descarga de la quickstart §4b (FR-018). Si T003 dejó la subida deshabilitada, habilitarla quitando el aviso de src/Presentation/IngenIA365ERP.Shared/Components/Shared/SubirSoporte.razor, una vez que los orígenes estén en el CORS (T071)
- [ ] T082 Recorrer specs/011-adjuntos-s3-prefirmadas/quickstart.md completa en DEV y en QA, incluida la §4b, y anotar los resultados en ese mismo archivo
- [ ] T083 Promover a producción (E4) con autorización del dueño, `pg_dump` y diagnóstico: aplicar la migración `AdjuntosDirectos` con el DbMigrator y verificar `/health/ready` y la quickstart §1–§5 en producción, según docs/operaciones/adjuntos-en-s3.md
  - *Receta (2026-09-23)*: docs/operaciones/despliegue-adjuntos-directos.md. Producción entra directo con Roles Anywhere (certificado `-Ambientes pdn` y componente en `overlays/pdn` antes de sincronizar). El 2026-09-23 el disco de adjuntos de producción tenía 0 archivos: nada que trasladar, pero hay que volver a mirarlo ese día.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**:
  - **T001 va cuanto antes** y no depende de nada. Es la que corta la exposición de la llave filtrada.
  - T004 y T005 bloquean lo Fundacional.
  - T002 bloquea T011.
  - T003 bloquea T049 (MAUI), T071 (CORS) y T081.
- **Foundational (Phase 2)**: bloquea todas las historias.
- **US3 (Phase 3)**: sólo depende de lo Fundacional. Con él se completa la E1.
- **US1 (Phase 4)** y **US2 (Phase 5)**: dependen de lo Fundacional. US2 no necesita a US1 para
  probarse: siembra sus adjuntos por `UploadAttachmentCommand` y `SembradorDeAdjuntosAnteriores`. En la
  pantalla, T059 toca el mismo `AttachmentList.razor` que T051, así que van en ese orden.
- **US4 (Phase 6)**: depende de lo Fundacional; es independiente de US1 y US2. T065 depende de T064.
- **US5 (Phase 7)**:
  - su parte de código (T068 a T071) puede avanzar en paralelo desde el principio;
  - T071 va después de T001, porque borra `-SoloSecreto`, que T001 usa;
  - T072 depende del dueño y de T069 y T071;
  - **T073 depende de T068 (Secret), T069 y T072 (roles) y T070 (imagen)**;
  - T074 depende de T072 y T073;
  - T075 depende de T073 y T074;
  - **todo US5 bloquea a T083 (producción)**.
- **Polish (Phase 8)**: T081 depende de T071 y T049; T082 necesita US1 a US5 en DEV y QA, y T081;
  T083 necesita T082.

### User Story Dependencies

- **US3 (P1)**: independiente. Primera entrega.
- **US1 (P1)**: independiente de US3 en lógica. Comparte `AttachmentList.razor` con US3 y US2.
- **US2 (P1)**: independiente en lógica. Comparte `AttachmentList.razor` y `adjuntos.js` con US1.
- **US4 (P2)**: independiente.
- **US5 (P2)**: independiente del código. Es condición de producción.

### Within Each User Story

- Primero las pruebas, y tienen que fallar. Después la lógica de Application, las rutas y el cliente.
- Una tarea que toca el mismo archivo que otra no se paraleliza: T025 con T022; T051 con T029 y T059;
  T011 con T074.

### Parallel Opportunities

- **Setup:** T002 y T003. T001 es del dueño y corre en cualquier momento.
- **Fundacional:** T006 y T014 al principio; T016, T020 y T021 una vez hechos T010 a T015.
- **US3:** T022, T023, T024 y T033.
- **US1:** T034 a T039, las seis pruebas.
- **US2:** T053 y T054.
- **US4:** T060 y T061.
- **US5:** T070 en paralelo con T068 y T069.
- **Polish:** T076, T077 y T078.

---

## Parallel Example: User Story 1

```bash
# Las seis pruebas de US1 a la vez (fallan hasta implementar):
Task: "FirmaDeContenidoTests en tests/IngenIA365ERP.Application.Tests/Attachments/FirmaDeContenidoTests.cs"
Task: "SolicitarSubidaDeAdjuntoCommandHandlerTests en tests/IngenIA365ERP.Application.Tests/Attachments/"
Task: "ConfirmarSubidaDeAdjuntoCommandHandlerTests en tests/IngenIA365ERP.Application.Tests/Attachments/"
Task: "RenovarSubidaDeAdjuntoCommandHandlerTests en tests/IngenIA365ERP.Application.Tests/Attachments/"
Task: "SubidaDirectaTests (MinIO) en tests/IngenIA365ERP.API.IntegrationTests/Attachments/"
Task: "LosAdjuntosNoPasanPorElServidor en tests/IngenIA365ERP.Architecture.Tests/Principles/"
```

## Parallel Example: User Story 3

```bash
Task: "DeleteAttachmentCommandHandlerTests en tests/IngenIA365ERP.Application.Tests/Attachments/"
Task: "DiscardDraftWithAttachmentsTests en tests/IngenIA365ERP.Application.Tests/Accounting/Documents/"
Task: "NadieBorraAdjuntosPorSuCuenta en tests/IngenIA365ERP.Architecture.Tests/Principles/"
Task: "Párrafo Adjuntos de CLAUDE.md"
```

---

## Implementation Strategy

### Antes que nada

**T001, rotar la llave filtrada.** No depende de ninguna otra tarea, y cada día que pasa la llave
expuesta sigue siendo la que usan DEV y QA.

### MVP (E1): Fundacional + US3

1. Phase 1: T004 y T005. Las espigas T002 y T003 pueden correr en paralelo, pero T002 tiene que
   terminar antes de T011.
2. Phase 2, completa.
3. Phase 3 (US3).
4. **Parar y validar** la quickstart §5 en DEV y QA.
5. Promover. Cierra los dos huecos de seguridad y hace real el borrado. **No toca AWS** salvo la regla
   de ciclo de vida (T031), que se aplica con el perfil `ingenia365`.

### Entregas siguientes

- **E3:** US1 → US2 → US4. Cada una se prueba en DEV con el almacén actual. La llave transitoria de
  T001 alcanza para DEV y QA mientras US5 no esté lista.
- **E2 (US5):** en paralelo, a medida que el dueño aplique lo de AWS (T072).
- **E4:** T081, T082 y T083, recién con US5 terminada (FR-048).

### Notas

- Commits por tarea o por grupo lógico, **sólo cuando el dueño lo pida**.
- En producción, GitOps se empuja con autorización expresa, `pg_dump` y diagnóstico.
- Nada de este plan escribe secretos en el repositorio. Las llaves viajan por STDIN y nunca se
  imprimen.
