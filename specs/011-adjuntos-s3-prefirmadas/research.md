# Research: Adjuntos con subida y descarga directas al almacén (011)

**Fecha**: 2026-09-23 · **Spec**: [spec.md](spec.md)

Cada decisión lleva lo elegido, por qué y lo que se descartó. Lo marcado **Espiga** se confirma
con una prueba corta contra el almacén real al empezar la implementación, antes de construir encima;
si la espiga falla, el plan dice qué hacer en su lugar.

Base verificada del código (2026-09-23): `COR_Attachments` (`Attachment : AuditableEntityLong`, con
`StoragePath`, `StorageProvider`, `EncryptedDek` obligatorio, `Sha256Hex`, `SizeBytes`); `IBlobStore`
con `PutAsync/GetAsync/DeleteAsync/ProbarAsync` y dos implementaciones (`LocalEncryptedFileStore`,
`S3BlobStore`); `UploadAttachmentCommand` sin comprobación del dueño; `AdjuntosDeModulo` con reglas
para `EmploymentTermination`, `BankDisbursementFile` y `PilaGeneration` (y ninguna para
`AccountingDocument`); rutas de módulo que sirven bytes en `PilaEndpoints` y `DisbursementsEndpoints`;
`AuditBehavior` audita todo `*Command`; el límite de tasa actual es `AspNetCoreRateLimit` por IP;
AWSSDK.S3 resuelto a 4.0.103.3.

---

## R1 — Subida: POST prefirmado, no PUT

**Decisión.** La autorización de subida es un **POST prefirmado** (`CreatePresignedPostAsync`, presente en
AWSSDK.S3 4.0.103), con una política que fija:
- la clave exacta;
- el `Content-Type` exacto (`ExactMatchCondition`);
- el tamaño exacto, como rango `[n, n]` (`ContentLengthRangeCondition`);
- la suma SHA-256 que el navegador calculó antes de subir;
- y el vencimiento: 5 minutos, configurable.

S3 rechaza en la entrada todo lo que no coincida.

**Por qué.** En un PUT prefirmado el tamaño no queda atado a la firma. Con la suma firmada el contenido
queda fijo, pero quien tiene permiso de subir podría declarar 1 MB y subir 5 GB bajo esa misma
autorización. El costo quedaría en el almacén, y como nada se borra solo, quedaría para siempre. La
condición de rango del POST lo corta en la puerta, sin que el ERP intervenga.

**Descartado.**
- **PUT prefirmado.** El tamaño no se firma.
- **Subida *multipart*.** Es innecesaria por debajo de 5 GB, y cada parte exige su propia firma.

**Espiga S1 — hecha el 2026-09-23 contra el bucket real** (prefijo `dev/.espiga/`, perfil
`ingenia365`, con las versiones borradas al terminar). Resultado con AWSSDK.S3 4.0.103:

| Caso | Respuesta de S3 |
|---|---|
| a) correcto, con la huella en los campos | **204**; guarda el SHA-256 y SSE AES256 |
| b) un byte más de lo declarado | **400 `EntityTooLarge`** |
| c) otro `Content-Type` | **403** |
| d) mismo tamaño, contenido alterado, con la huella firmada | **400 `BadDigest`** |
| e) mismo tamaño, contenido alterado, **sin** huella | 204: sin el campo no hay protección |
| f) otra clave | **403** |
| g) contenido alterado **con la huella del alterado** en lugar de la firmada | **403** |

**Decisión final.** La huella va **siempre**, en dos campos:
- `x-amz-checksum-algorithm = SHA256`;
- `x-amz-checksum-sha256 = <base64>`.

El SDK convierte cada entrada de `Fields` en una condición exacta de la política —por eso *g* da 403—.
Eso vuelve el archivo **inmutable desde que se autoriza**: el cliente no puede cambiar ni el contenido
ni la huella declarada. No hace falta el plan B.

**Lo que la espiga no midió:** una subida que empieza antes de vencer la autorización y termina
después. Queda para la espiga S2, junto con el vencimiento de la credencial.

## R2 — Descarga: GET prefirmado de 60 s con cabeceras de respuesta firmadas

**Decisión.** `GetPreSignedURLAsync` (verbo GET, `Expires` = ahora + 60 s, configurable con un techo de
5 min). En `ResponseHeaderOverrides` van:
- `Content-Disposition: attachment; filename*=UTF-8''<nombre codificado según RFC 5987>`;
- `Content-Type` con el tipo original, y en la PILA con su `charset`;
- `Cache-Control: no-store`.

El navegador baja por navegación directa (un `<a>` generado), así que no hace falta CORS para bajar.

**Por qué.** Las cabeceras firmadas obligan a descargar como archivo con el nombre y la codificación
correctos. Sin eso, un PDF se abriría dentro de la página y la PILA perdería el `charset` que exige el
operador.

**Descartado.**
- **Poner `Content-Disposition` como metadato del objeto al subir.** Queda fijo para siempre y no admite
  el `charset` variable de la PILA.
- **Pedir el archivo con fetch y guardarlo desde JavaScript.** Exige CORS en GET y vuelve a meter el
  archivo en la memoria de la pestaña.

## R3 — Credenciales temporales: IAM Roles Anywhere con un sidecar que emula IMDS

**Decisión.**
- **Una CA propia**, cuya llave privada queda fuera del clúster, en custodia del dueño, emite un
  **certificado por ambiente** (dev, qa, pdn) con un año de vigencia.
- En AWS: un *trust anchor* de tipo «certificate bundle» (CA externa, **sin costo**; nada de AWS Private
  CA, que cuesta ~USD 400 al mes), un perfil con sesión de 1 hora y **un rol por ambiente**.
- En el pod de la API, un **sidecar** con `aws_signing_helper serve` expone credenciales en
  `127.0.0.1:9911` emulando IMDSv2. La API las toma con `AWS_EC2_METADATA_SERVICE_ENDPOINT`, por la
  cadena estándar del SDK, sin cambiar el código de la API.
- El certificado y su llave se montan **sólo en el sidecar**: el proceso de la API nunca los ve.
- **Revocación:** se importa una CRL en el *trust anchor* y, a más tardar en una hora, deja de haber
  credenciales.
- La imagen del sidecar la construye el CI a partir del binario oficial, verificado contra la SHA-256
  que publica AWS.

**Por qué.**
- Es la única opción sin costo que elimina la llave permanente, que se filtró dos veces.
- Si se filtra una credencial de sesión (por un log, un volcado o un `printenv`), vence en una hora.
- Si se filtra el certificado, se revoca.
- Con un rol por ambiente, la credencial de DEV no puede leer producción. Hoy la **misma** llave está
  instalada en los tres.

**Descartado.**
- **Llave permanente rotada cada 90 días.** Sigue siendo permanente entre rotaciones.
- **`credential_process` dentro de la imagen de la API.** La llave privada viviría en el contenedor de
  la API y habría que tocar su Dockerfile.
- **Secrets Manager.** No resuelve el primer secreto y cuesta; decidido con el dueño.

**Espiga S2.** Confirmar dos cosas:
- que el SDK de .NET respeta `AWS_EC2_METADATA_SERVICE_ENDPOINT` y renueva la credencial con margen
  suficiente;
- y, sobre todo, que **una URL prefirmada no vive más que la credencial que la firmó**: una autorización
  de 5 minutos firmada con una sesión a la que le quedan 2 dejaría de servir a los 2.

Si el margen de renovación del SDK es menor que la vida de la URL más larga, el firmador pide
credenciales frescas antes de firmar cuando a la sesión le quedan menos de 10 minutos.

## R4 — Política de acceso por ambiente y del bucket

**Decisión.** El rol de cada ambiente tiene una política en línea que:

- **Permite** `s3:GetObject`, `s3:PutObject` y `s3:DeleteObject` sobre `arn:aws:s3:::ingenia365-erp-attachments/{ambiente}/*`.
- **Niega explícitamente**:
  - `s3:ListBucket` y `s3:ListBucketVersions`;
  - `s3:DeleteObjectVersion`;
  - todo cambio de configuración del bucket (versionado, ciclo de vida, política, cifrado, acceso
    público, `DeleteBucket`);
  - y **todo** `s3:*` sobre el bucket de respaldos.

La política del bucket suma una negación a toda petición sin TLS (`aws:SecureTransport = false`). Al
ciclo de vida se le agrega `ExpiredObjectDeleteMarker: true`, para que al purgar una versión no quede
la marca sola.

**Consecuencia en el código.** Sin permiso de listar, un GET o un HEAD de una clave inexistente
responde **403**, no 404. `S3BlobStore` tiene que tratar el 403 en esas dos operaciones como «no está»
(`FileNotFoundException` / `BlobMissing`). Hoy lo trata como un error.

**Descartado.**
- **La política actual de un usuario para los tres ambientes.** Sin aislamiento entre ambientes y con
  permiso de listar.
- **Condicionar por IP de origen.** Las URLs prefirmadas las ejecuta el navegador, desde la IP del
  usuario, así que la condición las rompería.

## R5 — Estados del adjunto y formato

**Decisión.**
- **Estados** (en `COR_Attachments.Status`): `Uploading` → `Available` | `Rejected`. `Uploading`, con la
  autorización vencida y sin objeto, pasa a `Incomplete`. El borrado es la baja lógica de siempre
  (`IsDeleted`).
- **Formato** (`Format`): `AppEncrypted`, el formato actual, que se lee por la API y se descifra; y
  `Direct`, el nuevo, con cifrado del almacén y descarga prefirmada.
- **Columnas nuevas:** `UploadExpiresAt`, `ConfirmedAt/By` y `RejectionReason`.

**Las filas existentes** quedan en `Available` y `AppEncrypted` por el valor por defecto de la
migración. `EncryptedDek` sigue siendo obligatorio y vale cadena vacía en `Direct`, así que no hace
falta alterar la columna.

**Confirmación perezosa sin romper CQRS.** La consulta de la lista **no escribe**. Devuelve cada
adjunto con su estado y, para los `Uploading` con la autorización vencida, una marca
`needsConfirmation`. La pantalla llama entonces a `confirm` para esos adjuntos: es un comando, auditado.
Así se cumple FR-015 («la próxima vez que se consulte la lista») sin que una consulta cambie datos
(Principio III).

## R6 — Validación del contenido real

**Decisión.** Al confirmar, se lee **un rango de los primeros 8 KiB** del objeto (un GET con `Range`) y
un detector puro en Application, `FirmaDeContenido`, contrasta ese inicio con el tipo declarado:

| Tipo declarado | Firma exigida |
|---|---|
| `application/pdf` | `%PDF-` |
| `image/png` | `89 50 4E 47 0D 0A 1A 0A` |
| `image/jpeg` | `FF D8 FF` |
| `image/gif` | `GIF87a` / `GIF89a` |
| `image/webp` | `RIFF` …4 bytes… `WEBP` |
| `application/msword`, `application/vnd.ms-excel` | OLE2 `D0 CF 11 E0 A1 B1 1A E1` |
| `…wordprocessingml.document`, `…spreadsheetml.sheet` | ZIP `50 4B 03 04` con `[Content_Types].xml` entre las primeras entradas |
| `text/plain`, `text/csv` | sin bytes NUL ni de control binarios (salvo tab, CR, LF); UTF-8 válido o Latin-1 |

Además, el tamaño que informa el HEAD tiene que coincidir con el declarado.

**Por qué 8 KiB y no el archivo entero.** La firma está al principio, y leer más mete el archivo en la
memoria del ERP, que es justamente lo que se quiere evitar.

**Límites que se aceptan.**
- Entre `.docx` y `.xlsx` no se distingue con certeza en 8 KiB, porque comparten contenedor. Confundirlos
  no es un problema de seguridad; aceptar un ejecutable como PDF sí lo sería, y eso se atrapa.
- Esto **no** es un antivirus: un PDF con contenido malicioso pasa. El antivirus quedó fuera por
  decisión del dueño (ClamAV en el clúster cuando suban personas externas).

**Los archivos que genera el ERP no se validan** (FR-030): el origen es el propio programa.

## R7 — Qué pasa con un archivo rechazado

**Decisión.** Al rechazar, el objeto se retira del almacén con `DeleteObject`. Eso deja una marca de
borrado, y la versión queda en la papelera de 90 días para poder examinarla. La fila sigue visible como
`Rejected`, con el motivo.

**Por qué.** Un archivo rechazado nunca fue aceptado: no es un archivo de la cooperativa. Dejarlo
vigente en el almacén acumularía, sin límite, basura que nadie puede bajar ni borrar desde la pantalla.
Retirarlo **no** contradice el requisito 1: la eliminación definitiva sigue siendo la de la papelera, y
sólo alcanza a lo que ya se retiró.

**Confirmado por el dueño** tras el análisis de consistencia (opción A). FR-001 se amplió para decir
explícitamente que ésta es la segunda y última eliminación automática permitida.

## R8 — Borrado: primero el objeto, después la fila

**Decisión.** `DeleteAttachmentCommand` hace tres cosas, en este orden:
1. Aplica las reglas de conservación (R9).
2. Hace `DeleteObject`: el almacén deja una marca de borrado, y la versión queda en la papelera.
3. Da de baja la fila (baja lógica y auditoría).

**Por qué ese orden.** Si falla el paso 3, el objeto está en la papelera y la fila sigue viva. La
descarga responde «no está» y reintentar el borrado lo completa, porque `DeleteObject` es idempotente.
En el orden inverso, si falla el almacén, la fila queda borrada y el objeto **vigente**: ninguna regla
lo purgaría y quedaría para siempre, sin fila que lo nombre.

**Recuperación (FR-003) y supresión (FR-007).** Son recetas de soporte con la credencial administrativa
del perfil `ingenia365`, que sí tiene permisos de S3 aunque no de IAM:
- *Recuperar*: borrar la marca de borrado (`DeleteObjectVersion` sobre el `VersionId` de la marca) y
  reactivar la fila de esa cooperativa.
- *Suprimir*: borrar todas las versiones del objeto y dejar constancia.

El ERP no puede hacer ninguna de las dos, por diseño (R4).

## R9 — Reglas de conservación y de destino

**Decisión.** `AdjuntosDeModulo` pasa de reglas estáticas a reglas que también pueden consultar el
documento. Para `AccountingDocument`:
- **Leer** exige `Accounting.Vouchers.View` (FR-025).
- **Subir** exige `Accounting.Vouchers.Create`, y que el comprobante exista en la cooperativa (FR-019).
- **Borrar** sólo mientras el comprobante esté en borrador (FR-004): la regla lee su estado.

Los tipos que genera un módulo (`EmploymentTermination`, `BankDisbursementFile`, `PilaGeneration`)
quedan **prohibidos como destino de subidas de personas**. La lista de destinos habilitados arranca con
`AccountingDocument` y nada más; cada módulo nuevo se agrega cuando exista su pantalla.

**Por qué.** Hoy `UploadAttachmentCommand` no comprueba el dueño (Contexto, punto 8): con el permiso
genérico de subir se puede colgar un archivo de la PILA de otro, y ese archivo queda inmutable.

**Descartado.** Una tabla de reglas en la base: son pocas, cambian con el código y viven mejor junto a
él.

## R10 — Descartar un borrador con soportes

**Decisión.** `DiscardDraftCommand(Guid PublicId, bool DeleteAttachments = false)`.
- **Con soportes y sin la marca** → 422 `Accounting.Document.HasAttachments`, con `data: { count }`. La
  pantalla pregunta y reintenta con la marca.
- **Con la marca** → cada soporte se retira del almacén y después, en una sola unidad de trabajo, se dan
  de baja las filas y se descarta el borrador.

Aplica también a los borradores de apertura (`AP`), que se descartan por la misma ruta.

## R11 — Archivos que genera el ERP

**Decisión.** `UploadAttachmentCommand` queda como camino **interno** para los módulos (PILA, dispersión,
definitiva) y cambia su implementación:
- deja de cifrar;
- sube con `PutObject` desde el contenido ya generado;
- calcula el SHA-256 de ese mismo contenido;
- y crea la fila `Available` y `Direct`.

Las firmas que usan los módulos no cambian.

**La ruta multipart** `POST /api/attachments`, la de las subidas de personas a través de la API, **se
retira**: su único cliente era el componente huérfano `AttachmentUploader`. Las pruebas de integración
que la usan se reescriben sobre el flujo nuevo.

## R12 — Rutas de descarga de módulo (PILA y dispersión)

**Decisión.** Siguen aplicando sus reglas. La PILA sigue exigiendo el reconocimiento del cuadre. Pero en
vez de devolver bytes devuelven `{ url, expiresAt }` (R2), con el `Content-Type` que hoy calculan,
`charset` incluido. Para un adjunto `AppEncrypted` siguen sirviendo bytes como hoy. En Shared,
`NominaClient` y las dos pantallas navegan al enlace.

## R13 — Límite de concurrencia

**Decisión.** Un `ConcurrencyLimiter`, del paquete integrado `Microsoft.AspNetCore.RateLimiting`, con la
política `adjuntos`:
- 32 permisos y cola de 64 **por réplica**, configurables;
- aplicado al grupo `/api/attachments` y a las dos rutas de descarga de módulo;
- lo que lo excede recibe 429 con el sobre `Attachments.Busy`.

Convive con `AspNetCoreRateLimit`, que limita por IP y sigue igual.

**Por qué.** Con las transferencias fuera del servidor, lo que queda en estas rutas es firmar, validar
8 KiB y servir el formato anterior. El límite protege sobre todo esto último, que sigue armando el
archivo en memoria.

## R14 — Cliente web: el archivo nunca entra a .NET

**Decisión.** Un módulo JS, `Shared/wwwroot/js/adjuntos.js`, que:
- lee el `File` del `<input type=file>` en el navegador;
- calcula su SHA-256 con WebCrypto;
- le devuelve a .NET **sólo** nombre, tipo, tamaño y huella;
- recibe la autorización;
- sube con `fetch` + `FormData` directo al almacén;
- y avisa el resultado.

La descarga se hace navegando al enlace.

CORS del bucket: `POST` desde `https://app-dev.ingenia365.com`, `https://app-qa.ingenia365.com`,
`https://app.ingenia365.com` y los orígenes de BlazorWebView (R15). Cabeceras permitidas `*`, expone
`ETag`, `MaxAgeSeconds` 3000.

**Por qué.** Pasar el archivo por `InputFile` lo copia a la memoria de .NET/WASM en la pestaña, y
después habría que devolverlo a JS para subirlo. Por JS directo, el archivo sale del disco del usuario
al almacén.

## R15 — Cliente móvil (MAUI, BlazorWebView)

**Decisión.** El mismo JS corre dentro de BlazorWebView. Desde .NET 8, su origen es `https://0.0.0.1`
en Windows y Android y `app://0.0.0.1` en iOS y macOS, y esos orígenes se agregan al CORS.

**Espiga S3.** Confirmar los orígenes reales en cada plataforma. No hay pruebas de navegador en el
repositorio, así que la verificación es manual.

## R16 — Desarrollo local (`Provider = Local`)

**Decisión.** El almacén en disco también «firma», pero hacia rutas de la propia API que **sólo existen
con `Provider = Local`**: `POST /api/attachments/local-blob/{token}` para subir y `GET` para bajar.
- Los tokens se firman con DataProtection y llevan operación, clave, tipo, tamaño, huella y vencimiento. El vencimiento va dentro del token: así no hace falta el paquete de `ITimeLimitedDataProtector`.
- El cliente usa exactamente el mismo flujo que con S3.
- En disco local, el formato `Direct` va sin cifrar.

**Por qué.** Mantiene un solo flujo de cliente y no obliga a levantar Docker ni MinIO para trabajar en
local (Docker Desktop está apagado por defecto en esta máquina).

**Cómo se respeta la arquitectura.** Las rutas no validan ni escriben: reenvían a un comando
(`RecibirSubidaLocalCommand`) y a una consulta (`LeerBlobLocalQuery`) de Application, y el almacén
local implementa la abstracción `IAlmacenLocal` (Principios II y III). Además de `Provider = Local`,
se registran sólo **fuera de Production**: el token ya lo acuña sólo el servidor, pero una ruta
anónima no tiene por qué existir en producción. Se usa «no Production» en lugar de «sólo
Development» para que las pruebas de integración, que no corren en Development, las sigan teniendo.

## R17 — Pruebas

- **Application:** los manejadores sobre un almacén falso (autorizaciones deterministas), las
  transiciones de estado, R7 a R10, y la lectura del formato anterior intacta.
- **Detector de firmas:** casos por tipo, incluidos un ejecutable renombrado, una imagen declarada como
  documento, un binario declarado como texto y un archivo vacío.
- **Integración con MinIO (Testcontainers; necesita Docker):**
  - POST prefirmado real que rechaza otro tamaño, otro tipo y otra clave;
  - GET prefirmado con `Content-Disposition` y vencido a los 61 s;
  - versionado y marcas de borrado;
  - la espiga S1.
- **Architecture:**
  - (a) Sólo `DeleteAttachment`, `DiscardDraft` y el rechazo llaman a `IBlobStore.DeleteAsync` (lo
    fija el requisito 1, y la prueba falla al agregar un llamador).
  - (b) Ninguna ruta de `/api/attachments` recibe el cuerpo del archivo (`IFormFile`, `Stream`), salvo
    las locales de R16.
  - (c) El grupo lleva el limitador `adjuntos`.

**SC-001** (memoria independiente del tamaño) queda cubierto por (b): el servidor no recibe los bytes.
Para lo que sí pasa por él —los 8 KiB de la validación—, una prueba mide las asignaciones de validar un
objeto de 1 MB y uno de 25 MB y exige que sean iguales, con tolerancia.

## R18 — Costo

No se agrega ningún servicio con cargo fijo (FR-047). Lo nuevo es gratis: Roles Anywhere con CA
propia, CORS, políticas y la regla de ciclo de vida. Los pedidos por archivo pasan a ser un POST, un
HEAD y un GET de rango de 8 KiB al subir, y un GET al bajar. A precios de lista de us-east-1 son
fracciones de centavo por cada mil operaciones. La estimación de SC-007 (≈ USD 0,30 al mes por
cooperativa típica) no cambia.

## R19 — Orden de despliegue

1. **Ya, sin esperar a lo demás:** rotar la llave filtrada de adjuntos (clave nueva instalada con
   `-SoloSecreto` y pods relevados, y **después** desactivar la filtrada) y desactivar la de P1. Esa
   llave transitoria sostiene DEV y QA hasta el paso 3. Desactivar la vieja **antes** de instalar la
   nueva dejaría sin adjuntos a los dos ambientes.
2. Un administrador de la cuenta aplica la receta de operación (FR-050):
   - la CA y los tres certificados;
   - el *trust anchor*, el perfil y los tres roles;
   - la política del bucket, el CORS y el ciclo de vida.
3. GitOps: el sidecar en los tres ambientes, el Secret con el certificado de cada uno, la variable del
   endpoint y el retiro del Secret `erp-adjuntos-s3`.
4. El código, primero en DEV y QA. Las pruebas de la quickstart pasan ahí.
5. Con el sidecar andando en los tres ambientes, borrar el usuario IAM `ingenia365-erp-adjuntos` con su
   clave transitoria. Desde ahí ninguna llave permanente accede al bucket (FR-048).
6. Producción, con autorización, respaldo y diagnóstico. Producción no tiene adjuntos en el almacén, así
   que no hay nada que convertir.
