# Adjuntos del ERP en S3

Los adjuntos (soportes de comprobantes contables, planillas PILA, archivos de dispersión,
documentos de liquidación) se suben cifrados y se guardan por `IBlobStore`. Hay dos
implementaciones y se elige por configuración:

| `AttachmentStorage:Provider` | Implementación | Dónde se usa |
|---|---|---|
| `Local` | `LocalEncryptedFileStore` | desarrollo (`storage/attachments`) |
| `S3` | `S3BlobStore` | DEV, QA y producción |

**Por qué no el disco del nodo.** Hasta el 2026-09-22 los tres ambientes escribían en un PVC
`local-path` de 10 Gi. Ese volumen no tiene redundancia, **no entra en los respaldos diarios** (que
cubren PostgreSQL y MongoDB) y, siendo `ReadWriteOnce`, no se puede montar desde dos nodos: hoy
funciona sólo porque las dos réplicas de la API caen en el mismo. Se migró estando el volumen
**vacío en producción**, así que no hubo nada que mover.

## Lo que el cambio no toca

El **cifrado sigue siendo nuestro**: `AttachmentEncryptionService` cifra con AES-256-GCM y una clave
por archivo (envuelta con DataProtection, que vive en `ADM_DataProtectionKeys`) **antes** de llamar
al almacén. S3 recibe bytes opacos y los guarda con `Content-Type: application/octet-stream`; el
cifrado del lado del servidor (SSE-S3, AES256) va **encima**, no en lugar del nuestro. Perder el
llavero de DataProtection sigue significando perder los adjuntos, con S3 o sin él
([llavero-dataprotection.md](llavero-dataprotection.md)).

## El bucket

`ingenia365-erp-attachments`, **distinto del de respaldos**: aquel tiene Object Lock GOVERNANCE a 40
días porque un respaldo no debe poder borrarse, y un adjunto sí se puede borrar cuando una persona con
permiso lo borra desde la aplicación. Mezclarlos obligaría a elegir una sola retención para las dos
cosas.

Lleva: acceso público bloqueado, una política que **rechaza toda petición sin TLS**, **versionado**,
cifrado AES256 por defecto y el ciclo de vida de la **papelera** (abajo). La credencial del ERP puede
borrar objetos pero **no versiones**, así que un `DeleteObject` deja una marca y la versión anterior
sobrevive.

Clave de cada objeto: `{prefijo}/{cooperativa}/{aaaa}/{mm}/{guid}.bin`. El **prefijo es del ambiente**
(`pdn`, `qa`, `dev`) y **no** se guarda en la base: `COR_Attachments.StoragePath` guarda la referencia
sin él, igual que el store local guardaba la ruta relativa al root. Así el prefijo —o el bucket— se
puede mover sin invalidar lo ya escrito.

## Borrar, recuperar y suprimir

**Nada se borra solo** (feature 011, FR-001). Ningún proceso del ERP retira archivos por su cuenta —lo
vigila la prueba `NadieBorraAdjuntosPorSuCuenta`— y cada cooperativa conserva sus archivos el tiempo que
necesite. Hasta el 2026-09-23 esta página y el código prometían una purga automática de los adjuntos
borrados que nunca existió, y decían que borrar un adjunto lo eliminaba del almacén: sólo se daba de baja
la fila.

**Qué pasa al borrar.** Borrar es siempre el acto de una persona con `Attachments.Delete`, y queda en la
auditoría. `DeleteAttachmentCommand` hace, en este orden:

1. aplica las reglas de conservación: lo que genera un módulo —PILA, dispersión, documento de la
   definitiva— no se borra (`Attachments.OwnedByModule`), y el soporte de un comprobante contabilizado o
   reversado tampoco (`Attachments.OwnerLocked`); ninguna de las dos se salta por la API;
2. retira el objeto: S3 deja una **marca de borrado** y la versión pasa a la **papelera**;
3. da de baja la fila.

Si el paso 3 falla, el objeto ya está en la papelera y la fila sigue viva: reintentar el borrado lo
completa. Descartar un borrador de comprobante con soportes exige confirmarlo
(`Accounting.Document.HasAttachments`), y con la confirmación sus soportes siguen el mismo camino.

**La papelera** es el ciclo de vida del bucket (regla `papelera-90-dias`):

| Qué | Cuándo se purga |
|---|---|
| Una versión que alguien borró | a los **90 días** de borrada (`NoncurrentVersionExpiration`) |
| La marca de borrado, cuando ya no le queda versión | sola (`ExpiredObjectDeleteMarker`) |
| Una subida multipart a medias | a los 7 días |

Es la **única** purga automática, y sólo alcanza a lo que una persona ya borró.

### Recuperar un adjunto borrado (dentro de los 90 días)

Lo hace soporte con la credencial administrativa (el perfil `ingenia365`, que tiene permisos de S3
aunque no de IAM). El ERP no puede: su credencial no borra versiones, y quitar la marca es borrar una.

1. En la base de la cooperativa, la clave del objeto (`StoragePath`) de la fila dada de baja:

   ```sql
   SELECT "PublicId", "FileName", "StoragePath", "DeletedAt", "DeletedBy"
   FROM "COR_Attachments" WHERE "PublicId" = '<publicId del adjunto>';
   ```

2. Las versiones de esa clave, con el prefijo del ambiente delante:

   ```bash
   aws s3api list-object-versions --profile ingenia365 --bucket ingenia365-erp-attachments --prefix "pdn/<StoragePath>"
   ```

   La marca es la entrada de `DeleteMarkers` con `IsLatest: true`.

3. Quitar la marca. La versión anterior vuelve a ser la vigente:

   ```bash
   aws s3api delete-object --profile ingenia365 --bucket ingenia365-erp-attachments --key "pdn/<StoragePath>" --version-id "<VersionId de la marca>"
   ```

4. Reactivar la fila:

   ```sql
   UPDATE "COR_Attachments" SET "IsDeleted" = false, "DeletedAt" = NULL, "DeletedBy" = NULL
   WHERE "PublicId" = '<publicId del adjunto>';
   ```

   Si el adjunto era soporte de un borrador **descartado**, el borrador sigue descartado: sin él, el
   soporte no se ve en ninguna pantalla.

### Supresión definitiva (Habeas Data)

Para cuando la ley obliga a que el archivo desaparezca de verdad, antes de los 90 días. **No es un botón
de la aplicación**: es este procedimiento, con la misma credencial administrativa.

1. Borrar el adjunto desde el ERP si todavía está vivo (así queda en la auditoría). Si el ERP no lo
   deja —soporte de un comprobante contabilizado, archivo que generó un módulo—, la decisión es de la
   cooperativa con su asesor: un soporte contable tiene su propia obligación de conservarse.
2. Listar las versiones como en la recuperación y borrar **cada** `VersionId`, versiones y marcas:

   ```bash
   aws s3api delete-object --profile ingenia365 --bucket ingenia365-erp-attachments --key "pdn/<StoragePath>" --version-id "<VersionId>"
   ```

3. Comprobar que `list-object-versions` ya no devuelve nada para esa clave.
4. Dejar constancia en el caso de Habeas Data: quién pidió, quién ejecutó, cuándo, qué adjunto
   (`PublicId`) y qué versiones se borraron. La fila queda dada de baja como rastro; si el propio nombre
   del archivo es un dato personal, se reemplaza por `suprimido-<fecha>`.

## Puesta en marcha

1. **Crear el bucket y la credencial** (una vez, desde una máquina con AWS CLI y el perfil correcto):

   ```bash
   powershell -ExecutionPolicy Bypass -File ./tools/scripts/crear-bucket-adjuntos.ps1 -Perfil ingenia365 -SoloVerificar
   ```

   ```bash
   powershell -ExecutionPolicy Bypass -File ./tools/scripts/crear-bucket-adjuntos.ps1 -Perfil ingenia365
   ```

   (`powershell` es el de Windows, 5.1, que es el que hay en la maquina del dueno; con
   PowerShell 7 instalado sirve igual `pwsh`.)

   Es re-ejecutable. Crea el bucket con todo lo de arriba, el usuario IAM
   `ingenia365-erp-adjuntos` con la política mínima de
   [politica-iam-adjuntos.json](politica-iam-adjuntos.json) —que además **niega explícitamente**
   tocar el bucket de respaldos— y deja el Secret `erp-adjuntos-s3` en los tres namespaces. La llave
   secreta no se imprime ni se escribe en ningún archivo: se genera, viaja por STDIN sobre SSH y se
   descarta. Antes de instalarla **escribe y borra un objeto con esa misma llave**, que es lo que el
   health check intentará al arrancar: que el bucket exista no dice nada sobre si la credencial puede
   escribir en él.

   **Si quien ejecuta no tiene permisos de IAM** (el 2026-09-22, `copesan@hotmail.com` en la cuenta
   `058264424927` —la de los dos buckets— no tiene ni `iam:ListUsers`), el bucket se crea igual con
   `-OmitirIam` y la credencial la hace otro desde la consola de AWS de esa cuenta:

   1. IAM › Usuarios › Crear usuario `ingenia365-erp-adjuntos`, **sin acceso a la consola**.
   2. Permisos › Agregar permisos › Incorporar directamente una política › JSON: pegar
      [politica-iam-adjuntos.json](politica-iam-adjuntos.json) tal cual, con el nombre `AdjuntosDelErp`.
   3. Credenciales de seguridad › Crear clave de acceso › «Aplicación que se ejecuta fuera de AWS».
   4. Volver acá con la llave a la vista y correr:

      ```bash
      powershell -ExecutionPolicy Bypass -File ./tools/scripts/crear-bucket-adjuntos.ps1 -Perfil ingenia365 -SoloSecreto
      ```

      Pide el `AccessKeyId` y el `SecretAccessKey` —éste con `Read-Host -AsSecureString`, así que no se
      ve al teclearlo ni queda en el historial—, prueba la llave contra el bucket y la instala en los
      tres namespaces. **Nunca se pasa la llave por la línea de comandos**: quedaría en el historial de
      la consola.

   Reusar la credencial de los respaldos **no** es una opción: la separación entre las dos es
   justamente lo que impide que una aplicación comprometida borre los respaldos.

2. **Configurar el overlay de cada ambiente** (repo GitOps). En `base/api.yaml`, junto a las demás
   variables:

   ```yaml
   - name: AttachmentStorage__Provider
     value: "S3"
   - name: AttachmentStorage__S3__BucketName
     value: "ingenia365-erp-attachments"
   - name: AttachmentStorage__S3__Region
     value: "us-east-1"
   - name: AWS_ACCESS_KEY_ID
     valueFrom: { secretKeyRef: { name: erp-adjuntos-s3, key: accessKeyId, optional: true } }
   - name: AWS_SECRET_ACCESS_KEY
     valueFrom: { secretKeyRef: { name: erp-adjuntos-s3, key: secretAccessKey, optional: true } }
   ```

   y en el overlay de cada ambiente el prefijo que le toca:

   ```yaml
   - name: AttachmentStorage__S3__Prefix
     value: "pdn"    # qa / dev en los otros
   ```

   **`optional: true` es a propósito**: un ambiente sin el Secret arranca igual y el fallo aparece en
   `/health/ready`, en vez de dejar el pod en `CrashLoopBackOff` sin explicación. El Secret se lee
   **al arrancar**: creado después, hay que relevar los pods.

3. **Verificar**. `/health/ready` trae un check `blobstore` que **escribe y borra** un objeto bajo
   `.healthcheck/` —listar el bucket no alcanza: una credencial puede leer y no poder escribir, y eso
   se descubriría al subir el primer soporte—. Con S3 responde `S3: s3://ingenia365-erp-attachments/pdn`;
   con disco, la ruta. Después, subir un adjunto de prueba a un comprobante, descargarlo y borrarlo.

   Esa escritura se hace **como mucho una vez cada cinco minutos**, no en cada sonda: la
   `readinessProbe` pega cada 5 s por pod, y el 2026-09-22, el día que esto salió a DEV y QA, el
   bucket juntó 18 versiones en dos minutos. Entre medio se repite el último veredicto. El primer
   arranque **siempre** prueba, que es cuando importa: un despliegue con la credencial mal puesta
   tiene que quedarse sin pasar a Ready. Lo fija `BlobStoreHealthCheckTests`.

4. **Retirar el volumen** (opcional, cuando lleve días andando): quitar de `base/api.yaml` el
   `volumeMount`, el `volume` y el PVC `erp-attachments`. Hasta entonces el volumen queda montado sin
   usarse, que no molesta. **No borrarlo antes de verificar** el punto 3.

## Qué pasa si S3 no responde

Subir un adjunto falla y la pantalla lo dice; **lo que se estaba guardando no queda a medias**,
porque el handler escribe la fila de `COR_Attachments` después de que el almacén confirma. En el caso
inverso —objeto escrito y fila no guardada— el handler retira el objeto; si eso también falla, el objeto
queda **huérfano** y vigente. No rompe nada y **nada lo limpia solo** (FR-001): lo encuentra un
inventario manual del bucket contra las bases, que hoy no existe.

Borrar con S3 caído falla antes de tocar la fila: el adjunto sigue vivo y se puede reintentar.

El resto del ERP no depende de los adjuntos: una nómina se aprueba y un comprobante se contabiliza
aunque el bucket esté caído.

## Desarrollo

Nada cambia: `appsettings.json` trae `Provider: Local` y los archivos van a `storage/attachments`.
Para probar S3 en local se puede apuntar a MinIO con `AttachmentStorage:S3:ServiceUrl` y
`ForcePathStyle: true`. Las pruebas (`S3BlobStoreTests`) usan un `IAmazonS3` falso y, para la forma de
las autorizaciones firmadas, un cliente real con credenciales ficticias (firmar no toca la red): **el CI
no tiene credenciales de AWS ni las necesita**. `AlmacenPrefirmadoTests` levanta MinIO con Docker
(`quay.io/minio/minio`: MinIO ya no publica en Docker Hub) para lo que sólo un almacén de verdad puede
decir: que la firma ata tamaño, tipo y clave, que el enlace vence y que borrar deja la versión en la
papelera.
