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
días porque un respaldo no debe poder borrarse, y un adjunto sí se borra cuando su dueño lo borra
desde la aplicación. Mezclarlos obligaría a elegir una sola retención para las dos cosas.

Lleva: acceso público bloqueado, **versionado** (un borrado accidental se recupera: la credencial
puede borrar objetos pero **no versiones**, así que un `DeleteObject` deja marca y la versión
anterior sobrevive), cifrado AES256 por defecto y ciclo de vida que limpia versiones no vigentes a
los 90 días y multipart a medias a los 7.

Clave de cada objeto: `{prefijo}/{cooperativa}/{aaaa}/{mm}/{guid}.bin`. El **prefijo es del ambiente**
(`pdn`, `qa`, `dev`) y **no** se guarda en la base: `COR_Attachments.BlobUri` guarda la referencia sin
él, igual que el store local guardaba la ruta relativa al root. Así el prefijo —o el bucket— se
puede mover sin invalidar lo ya escrito.

## Puesta en marcha

1. **Crear el bucket y la credencial** (una vez, desde una máquina con AWS CLI y el perfil correcto):

   ```bash
   pwsh ./tools/scripts/crear-bucket-adjuntos.ps1 -Perfil ingenia365 -SoloVerificar
   ```

   ```bash
   pwsh ./tools/scripts/crear-bucket-adjuntos.ps1 -Perfil ingenia365
   ```

   Es re-ejecutable. Crea el bucket con todo lo de arriba, el usuario IAM
   `ingenia365-erp-adjuntos` con la política mínima de
   [politica-iam-adjuntos.json](politica-iam-adjuntos.json) —que además **niega explícitamente**
   tocar el bucket de respaldos— y deja el Secret `erp-adjuntos-s3` en los tres namespaces. La llave
   secreta no se imprime ni se escribe en ningún archivo. Con `-OmitirIam` sólo hace el bucket, para
   cuando el usuario de AWS no puede crear usuarios IAM.

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

4. **Retirar el volumen** (opcional, cuando lleve días andando): quitar de `base/api.yaml` el
   `volumeMount`, el `volume` y el PVC `erp-attachments`. Hasta entonces el volumen queda montado sin
   usarse, que no molesta. **No borrarlo antes de verificar** el punto 3.

## Qué pasa si S3 no responde

Subir un adjunto falla y la pantalla lo dice; **lo que se estaba guardando no queda a medias**,
porque el handler escribe la fila de `COR_Attachments` después de que el almacén confirma. El caso
inverso —blob escrito y fila no guardada— deja un objeto huérfano, que no rompe nada y se limpia con
un inventario contra la base (sigue pendiente, como con el disco).

El resto del ERP no depende de los adjuntos: una nómina se aprueba y un comprobante se contabiliza
aunque el bucket esté caído.

## Desarrollo

Nada cambia: `appsettings.json` trae `Provider: Local` y los archivos van a `storage/attachments`.
Para probar S3 en local se puede apuntar a MinIO con `AttachmentStorage:S3:ServiceUrl` y
`ForcePathStyle: true`. Las pruebas (`S3BlobStoreTests`) usan un `IAmazonS3` falso: **el CI no tiene
credenciales de AWS ni las necesita**.
