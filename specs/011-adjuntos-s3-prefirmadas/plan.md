# Implementation Plan: Adjuntos con subida y descarga directas al almacén

**Branch**: `011-adjuntos-s3-prefirmadas` | **Date**: 2026-09-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/011-adjuntos-s3-prefirmadas/spec.md`

## Summary

Los adjuntos dejan de pasar por el servidor del ERP.

- **Subir:** el ERP registra el adjunto como «subiendo», comprueba el documento dueño y el permiso, y
  entrega una **autorización de subida** (POST prefirmado de S3) que fija clave, tipo, tamaño exacto y
  huella, y vence en 5 minutos. El navegador sube directo. Después el ERP confirma con un HEAD y leyendo
  los primeros 8 KiB, y marca el adjunto «disponible» o «rechazado».
- **Bajar:** el ERP verifica cooperativa y permiso del módulo y entrega un **GET prefirmado de 60 s**
  que fuerza la descarga con el nombre y el tipo originales.
- **Archivos generados** por el ERP (PILA, dispersión, definitiva): se guardan sin cifrado propio ni
  copias extra.
- **Credencial:** el ERP deja de usar una llave permanente. Obtiene credenciales de una hora por **IAM
  Roles Anywhere**, a través de un sidecar que emula IMDS, con un rol por ambiente limitado a su prefijo
  y sin permiso de listar ni de borrar versiones.
- **Borrado:** siempre es un acto de una persona. Retira el objeto a una papelera de 90 días, y los
  soportes de comprobantes contabilizados no se pueden borrar.
- **Compatibilidad:** lo escrito con el formato anterior se sigue leyendo por la API, sin migración.
- **Costo:** ningún servicio con cargo fijo.

Decisiones y alternativas en [research.md](research.md) (R1–R19); esquema en
[data-model.md](data-model.md); contratos en [contracts/api.md](contracts/api.md) y
[contracts/almacen.md](contracts/almacen.md); validación en [quickstart.md](quickstart.md).

## Technical Context

**Language/Version**: C# / .NET 10.0.5; JavaScript de navegador (ES2020, WebCrypto) para la subida
directa.

**Primary Dependencies**:
- Ya presentes: Carter, MediatR, FluentValidation, EF Core (PostgreSQL y SQL Server), **AWSSDK.S3
  4.0.103.3** (`CreatePresignedPostAsync`, `GetPreSignedURLAsync`), ASP.NET Core DataProtection
  (tokens firmados para el almacén local) y `Microsoft.AspNetCore.RateLimiting`, que viene
  integrado.
- **Nuevas:** ninguna en el ERP. Fuera del código: `aws_signing_helper`, en una imagen de sidecar que
  construye el CI; y `Testcontainers.Minio`, sólo para pruebas.

**Storage**: `COR_Attachments` en la base de cada cooperativa, con seis columnas nuevas (par de
migraciones aditivas `AdjuntosDirectos`). Los objetos van a S3 `ingenia365-erp-attachments`
(us-east-1, versionado, SSE-S3), con prefijo por ambiente.

**Testing**: xUnit + FluentAssertions + NSubstitute:
- Application, con un almacén falso;
- Architecture: tres reglas nuevas;
- integración con MinIO por Testcontainers, que necesita Docker;
- manual en DEV y QA según la quickstart. No hay pruebas de navegador en el repositorio.

**Target Platform**: API en k3s (Linux), en tres ambientes; cliente Blazor Web/WASM y MAUI
(BlazorWebView).

**Project Type**: aplicación web multi-tenant, con Clean Architecture de cuatro capas.

**Performance Goals**:
- Memoria del servidor por transferencia independiente del tamaño del archivo (SC-001).
- Veinte transferencias simultáneas de 25 MB sin afectar al resto del ERP (SC-002).
- El 95 % de las descargas de hasta 5 MB empiezan en menos de 2 s (SC-003).

**Constraints**:
- Costo sin cargos fijos (≈ USD 0,30 al mes por cooperativa).
- Nada se borra solo, salvo la papelera de 90 días.
- La credencial del ERP no lista, no borra versiones y no ve otro ambiente.
- Autorización de subida de 5 min como máximo y enlace de descarga de 60 s (configurables).
- Tamaño máximo de 25 MB, configurable.

**Scale/Scope**:
- Decenas de cooperativas; miles de adjuntos al mes por cooperativa.
- Cambios en un solo destino habilitado para personas (comprobante contable) y en dos rutas de módulo
  (PILA y dispersión).
- Seis columnas, seis comandos, tres rutas nuevas y dos rutas de módulo nuevas.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | Constitution → spec (con Clarifications del dueño) → este plan → tasks |
| II   | Clean Architecture | PASS | `IBlobStore` (Application) gana firmar, consultar y leer el inicio. S3, el almacén local y la firma viven en `IngenIA365ERP.Storage` (Infrastructure). `FirmaDeContenido` es puro, en Application. Domain sólo suma dos enums y seis propiedades |
| III  | CQRS + MediatR | PASS | Seis comandos nuevos o cambiados, con su validador: Solicitar, Renovar, Confirmar, EmitirEnlace y Delete, más `DiscardDraft` ampliado. **La lista no escribe**: la confirmación perezosa la dispara el cliente como comando (R5). Las rutas sólo reenvían al `ISender` |
| IV   | Multi-tenancy | PASS | La fila vive en la base de la cooperativa. La clave del objeto lleva la cooperativa, y el prefijo del ambiente sale de la configuración. Cada rol ve sólo `bucket/{ambiente}/*`. Sin barridos entre cooperativas: no hay trabajos de fondo |
| V    | Person centralizada | N/A | No toca personas |
| VI   | PublicId / Id | PASS | Rutas y DTO sólo con `PublicId`. La clave del objeto es un GUID nuevo, no el `Id` |
| VII  | Soft-delete + auditoría | PASS | La fila se da de baja lógica, como siempre. El objeto pasa a una papelera recuperable por 90 días. La supresión definitiva es una receta de soporte, como el mantenimiento técnico que admite el XI |
| VIII | Validación dual | PASS | El cliente verifica tipo y tamaño antes de pedir la autorización. El servidor lo hace con FluentValidation y reglas del dueño. El almacén vuelve a exigirlo en la política del POST |
| IX   | Errores visibles | PASS | Cada falla del almacén se registra con Serilog (cooperativa, adjunto y operación, sin el contenido) y llega a la pantalla con un código. Sin `catch` silenciosos: lo vigila `PrincipioIX_NoEmptyCatch` |
| X    | Trazabilidad | PASS | Solicitar, confirmar, rechazar, emitir un enlace y borrar son `*Command` y los audita `AuditBehavior`. Emitir el enlace de descarga es un comando justamente para eso (FR-046) |
| XI   | Inmutabilidad contable | PASS | Los soportes de un comprobante contabilizado no se pueden borrar por ninguna vía (R9). Agregarlos no altera el comprobante |
| XII  | Migraciones | PASS | Par de migraciones EF aditivas, con valores por defecto, sin DML y reversibles (`Down` quita las columnas). No es destructiva |
| UI   | Indicador de carga | PASS | La zona de soportes de `Comprobante.razor` y las acciones de subir, confirmar, bajar y borrar van con `IndicadorDeCarga` y su `EstadoDeCarga`, sin colores literales. Contabilidad no está en `ModulosMigrados`, pero la regla del proyecto vale para toda pantalla modificada |

**Re-evaluación tras la Fase 1:** sin cambios; todas las compuertas siguen en PASS. El diseño no
introduce infraestructura en Domain, ni lógica en las rutas, ni lecturas que escriban.

## Project Structure

### Documentation (this feature)

```text
specs/011-adjuntos-s3-prefirmadas/
├── spec.md
├── plan.md              # este archivo
├── research.md          # R1–R19, con las espigas S1–S3
├── data-model.md        # COR_Attachments + estados + reglas por dueño
├── quickstart.md        # validación en DEV y QA
├── contracts/
│   ├── api.md           # rutas, cuerpos y códigos
│   └── almacen.md       # bucket, CORS, ciclo de vida, Roles Anywhere, políticas
├── checklists/requirements.md
└── tasks.md             # lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/
├── Entities/Core/Attachment.cs                       # + Format, Status, UploadExpiresAt, ConfirmedAt/By, RejectionReason
└── Enums/Core/FormatoDeAdjunto.cs, EstadoDeAdjunto.cs   # nuevos

src/Core/IngenIA365ERP.Application/
├── Common/Interfaces/Storage/IBlobStore.cs          # + FirmarSubidaAsync, FirmarDescargaAsync, ConsultarAsync, LeerInicioAsync; + IAlmacenLocal
├── Attachments/
│   ├── Common/AdjuntosDeModulo.cs                   # reglas con consulta al dueño (R9)
│   ├── Common/FirmaDeContenido.cs                   # nuevo: detector de firmas (R6)
│   ├── Common/AttachmentErrorCodes.cs               # + OwnerNotAllowed, OwnerLocked, NotAvailable, UseDownloadLink, Busy
│   ├── SolicitarSubida/                             # nuevo
│   ├── RenovarSubida/                               # nuevo
│   ├── ConfirmarSubida/                             # nuevo
│   ├── EmitirEnlaceDeDescarga/                      # nuevo
│   ├── Local/                                       # nuevo: RecibirSubidaLocalCommand, LeerBlobLocalQuery (sólo fuera de Production, R16)
│   ├── UploadAttachment/                            # interno de módulos: Direct, sin cifrar (R11)
│   ├── DeleteAttachment/                            # objeto primero, reglas de conservación (R8, R9)
│   ├── DownloadAttachment/                          # sólo para AppEncrypted
│   └── ListAttachments/                             # + status, format, needsConfirmation, canDelete
├── Accounting/Documents/DocumentCommands.cs         # DiscardDraftCommand + DeleteAttachments (R10)
└── Payroll/Pila/PilaQueries.cs, Payroll/Dispersion/DisbursementQueries.cs   # enlaces de descarga (R12)

src/Infrastructure/
├── IngenIA365ERP.Storage/
│   ├── Services/S3BlobStore.cs                      # POST/GET prefirmados, HEAD, rango; 403 = no está (R4)
│   ├── Services/LocalEncryptedFileStore.cs          # firma hacia rutas locales con token temporal (R16)
│   └── Configuration/AttachmentStorageSettings.cs   # + MaxBytes, SubidaMinutos, DescargaSegundos
├── IngenIA365ERP.Persistence/Configurations/Core/AttachmentConfiguration.cs
├── IngenIA365ERP.Persistence.Migrations.PostgreSql/Application/<ts>_AdjuntosDirectos.cs
└── IngenIA365ERP.Persistence.Migrations.SqlServer/Application/<ts>_AdjuntosDirectos.cs

src/Presentation/
├── IngenIA365ERP.API/
│   ├── Endpoints/AttachmentsModule.cs               # §1–§7; retira POST /api/attachments; limitador
│   ├── Endpoints/Attachments/LocalBlobEndpoints.cs  # §10: sólo reenvían al ISender (C1)
│   ├── Endpoints/Payroll/PilaEndpoints.cs, DisbursementsEndpoints.cs   # §9
│   ├── Endpoints/Accounting/DocumentsEndpoints.cs   # §8
│   └── Program.cs                                   # AddRateLimiter("adjuntos"), UseRateLimiter
├── IngenIA365ERP.Shared/
│   ├── wwwroot/js/adjuntos.js                       # nuevo: huella, POST directo, navegación de descarga (R14)
│   ├── Components/Shared/SubirSoporte.razor         # nuevo; reemplaza al huérfano AttachmentUploader
│   ├── Components/Shared/AttachmentList.razor       # estados, confirmar, reintentar, enlace directo
│   ├── Pages/Contabilidad/Comprobante.razor         # subir soportes; descartar con soportes
│   ├── Pages/Nomina/Pila.razor, Dispersion.razor + Services/Nomina/NominaClient*.cs   # enlaces
│   └── Components/App.razor (o equivalente)         # adjuntos.js con @Assets
└── IngenIA365ERP.Web.Client/Components/AttachmentUploader.razor   # se borra

tests/
├── IngenIA365ERP.Application.Tests/Attachments/     # manejadores, estados, reglas, FirmaDeContenido
├── IngenIA365ERP.API.IntegrationTests/Attachments/  # MinIO: POST y GET prefirmados, versiones; se reescriben las del multipart
└── IngenIA365ERP.Architecture.Tests/Principles/
    ├── NadieBorraAdjuntosPorSuCuenta.cs             # sólo Delete, Discard y el rechazo llaman a IBlobStore.DeleteAsync
    ├── LosAdjuntosNoPasanPorElServidor.cs           # ninguna ruta de adjuntos recibe el archivo (salvo las locales)
    └── (ampliar) una prueba que fija que el grupo lleva el limitador y que las rutas locales sólo existen con Local

tools/
├── scripts/crear-certificados-adjuntos.ps1          # nuevo: CA y certificados por ambiente con openssl, local
├── scripts/crear-bucket-adjuntos.ps1                # + CORS, política TLS, ciclo de vida; sin usuario IAM
└── credential-helper/Dockerfile                     # nuevo: imagen del sidecar con aws_signing_helper verificado

docs/operaciones/adjuntos-en-s3.md                   # se reescribe: flujo directo, recetas de recuperación y supresión
CLAUDE.md                                            # párrafo de Adjuntos corregido (incluye la afirmación falsa sobre el borrado)
```

Fuera de este repositorio, en GitOps (`ingenia365-gitops`), en `workloads/erp/base/api.yaml` y los
overlays:
- el sidecar;
- el Secret `erp-adjuntos-certificado` por ambiente;
- `AWS_EC2_METADATA_SERVICE_ENDPOINT`;
- los ARN por ambiente;
- y el retiro de `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` y el Secret `erp-adjuntos-s3`.

Es configuración de producción: se empuja con autorización expresa del dueño.

**Structure Decision**: la Clean Architecture existente, sin proyectos nuevos. El único artefacto
nuevo fuera de `src/` es la imagen del sidecar, que el CI construye junto a las demás.

## Entregas

Cada entrega deja el sistema andando por sí sola:

1. **E1 — Reglas y borrado real**, sin tocar AWS. Incluye:
   - FR-004 en el servidor y las reglas de dueño de R9, con lectura, subida y borrado;
   - el borrado con retiro del objeto (R8) y descartar con soportes (R10);
   - el límite de concurrencia (R13);
   - la documentación corregida;
   - y el trato del 403 como «no está» (R4).

   Cierra ya los dos huecos de seguridad del Contexto (puntos 8 y 9).
2. **E2 — Almacén y credenciales**, operación más GitOps:
   - la receta de `contracts/almacen.md`, el sidecar y los certificados;
   - retirar la llave permanente;
   - y las espigas S1, S2 y S3.
3. **E3 — Flujo directo:** las columnas nuevas, los comandos de §1–§5, los enlaces de módulo (§9), el
   almacén local firmado (R16), `adjuntos.js`, `SubirSoporte` y `AttachmentList`, y los generados en
   `Direct` (R11).
4. **E4 — Validación:** la quickstart en DEV y QA, y después producción con autorización, respaldo y
   diagnóstico.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| Una URL prefirmada vive menos que su vencimiento porque la credencial que la firmó expira antes (S2) | Firmar sólo con una credencial a la que le queden 10 minutos o más; si no, pedir una fresca |
| S3 no verifica `x-amz-checksum-sha256` en el POST (S1) | El tamaño exacto sigue atado por la política; la huella queda como dato y la integridad la cubre la suma por defecto del almacén |
| Orígenes de BlazorWebView distintos de los previstos (S3) | Confirmarlos en cada plataforma antes de fijar el CORS; hasta confirmarlos, la subida desde MAUI queda deshabilitada con un aviso («súbalo desde la web») y la descarga funciona igual, porque no usa CORS |
| El sidecar no arranca, o su imagen tiene un binario alterado | La imagen se construye en CI verificando la SHA-256 que publica AWS. Sin credenciales, `/health/ready` falla y el pod no recibe tráfico; nunca se cae a una llave permanente |
| Un adjunto `Uploading` queda para siempre porque nadie vuelve a abrir la lista | No es basura en el almacén: si el objeto no llegó, no existe. La fila sólo se ve como pendiente en su documento y se resuelve al abrirlo |
| Confundir `.docx` con `.xlsx` | Aceptado (R6): no es un riesgo de seguridad |

## Complexity Tracking

Sin violaciones de la constitución. El sidecar es un contenedor más en el pod de la API, no un
proyecto nuevo, y se justifica en R3: es lo que mantiene la llave privada fuera del proceso de la API.
