# Adjuntos del ERP en S3

Los adjuntos (soportes de comprobantes contables, planillas PILA, archivos de dispersión,
documentos de liquidación) se guardan por `IBlobStore`. Hay dos implementaciones y se elige por
configuración:

| `AttachmentStorage:Provider` | Implementación | Dónde se usa |
|---|---|---|
| `Local` | `LocalEncryptedFileStore` | desarrollo (`storage/attachments`); imita a S3 con rutas `local-blob` firmadas, sólo fuera de Production |
| `S3` | `S3BlobStore` | DEV, QA y producción |

**Por qué no el disco del nodo.** Hasta el 2026-09-22 los tres ambientes escribían en un PVC
`local-path` de 10 Gi. Ese volumen no tiene redundancia, **no entra en los respaldos diarios** (que
cubren PostgreSQL y MongoDB) y, siendo `ReadWriteOnce`, no se puede montar desde dos nodos: hoy
funciona sólo porque las dos réplicas de la API caen en el mismo. Se migró estando el volumen
**vacío en producción**, así que no hubo nada que mover.

## Dos formatos: el anterior y el directo

Desde la feature 011 (entrega E3, 2026-09-23) los archivos **no pasan por el servidor**:

- **Subir** (soportes de comprobantes): la pantalla pide una autorización firmada
  (`POST /api/attachments/uploads`), el navegador sube el archivo **directo al bucket** y la API sólo
  confirma lo que llegó (`/confirm`): tamaño, huella y la firma del contenido sobre los primeros 8 KiB.
  Un ejecutable con nombre de PDF queda **rechazado**, con el motivo, y su objeto va a la papelera.
- **Bajar**: la API verifica cooperativa y permiso y devuelve un **enlace firmado de 60 s**
  (`POST /api/attachments/{id}/download-link`) que obliga a guardar el archivo con su nombre y su tipo.
  PILA y dispersión tienen el suyo (`POST …/{id}/download-link`), con su `charset`.
- **Lo que generan los módulos** (PILA, dispersión, definitiva) se guarda tal como se generó.

| Formato | Cifrado en reposo | Cómo se baja |
|---|---|---|
| `Direct` (todo lo nuevo) | el del bucket (SSE-S3, AES256) | enlace firmado, directo del bucket |
| `AppEncrypted` (lo escrito antes del 2026-09-23 en DEV y QA) | el nuestro, AES-256-GCM con clave por archivo envuelta con DataProtection, **y** el del bucket encima | por la API (`GET /api/attachments/{id}`, `GET …/file`), que lo descifra |

Lo del formato anterior **no se migra**: se sigue leyendo por la API. Para él vale lo de siempre: perder
el llavero de DataProtection es perder esos archivos ([llavero-dataprotection.md](llavero-dataprotection.md)).
Lo directo ya no depende del llavero.

**El bucket necesita CORS** para que el navegador pueda subir (bajar no lo necesita: es navegación). Lo
pone `tools/scripts/crear-bucket-adjuntos.ps1` con los orígenes web del ERP (`app-dev`, `app-qa`, `app`);
**sin él, la subida falla en el navegador** con «No se pudo contactar el almacén de archivos», aunque la
API autorice. Los orígenes de la app (MAUI) se agregan cuando se confirmen en cada plataforma (T003);
mientras tanto la app muestra «Por ahora los soportes se agregan desde la versión web del ERP».

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

## Credenciales temporales (IAM Roles Anywhere)

Feature 011, entrega E2. **Reemplaza la llave permanente** del usuario IAM `ingenia365-erp-adjuntos`,
que hoy está instalada en los tres ambientes y ya se filtró. Con esto:

- cada pod de la API recibe una credencial que **vence en una hora** y se renueva sola;
- **cada ambiente sólo ve su prefijo**: la credencial de DEV no puede leer `pdn/`;
- nadie puede **listar** el bucket ni **borrar versiones** (la papelera es de soporte);
- si se filtra un certificado, se **revoca** con una CRL y a más tardar en una hora deja de servir.

**Costo: cero.** El *trust anchor* es de tipo «certificate bundle» con una CA propia; no usa AWS Private
CA (~USD 400 al mes).

**Estado:** activo en **DEV y QA** desde el 2026-09-23 (GitOps `2e3f5cb` y `aa718f6`, pila
`ingenia365-erp-adjuntos-roles-anywhere`); producción pendiente, con autorización expresa del dueño.

**Cómo funciona.** En el pod de la API corre un segundo contenedor, el **sidecar** con el binario oficial
de AWS (`aws_signing_helper serve`, imagen `ghcr.io/jormancopete/ingenia365erp/credential-helper` que
construye el CI verificando la SHA-256 que publica AWS). El sidecar presenta el certificado del ambiente y
expone la credencial en `127.0.0.1:9911`, imitando al IMDSv2 de EC2; la API la toma por la cadena
estándar del SDK con `AWS_EC2_METADATA_SERVICE_ENDPOINT`, **sin cambiar su código**. El certificado y
su llave se montan **sólo en el sidecar**.

Tres cosas del sidecar que no se ven en el manifiesto:
- **No tiene sondas.** `serve` escucha sólo en 127.0.0.1 (está fijo en su código) y el kubelet sondea la
  IP del pod, así que una `startupProbe` TCP no pasa nunca y mata al sidecar en bucle; le pasó al primer
  despliegue. Lo que prueba la credencial es `/health/ready` de la API, que escribe y borra en el bucket
  cada 5 minutos.
- **Lee el certificado una sola vez, al arrancar.** Después de instalar un certificado nuevo hay que
  reiniciar la API; `crear-certificados-adjuntos.ps1` lo hace solo.
- **Lo puede leer gracias al `fsGroup` del pod.** El Secret se monta 0400 y la imagen corre sin
  privilegios; sin el `fsGroup: 10001` de `base/api.yaml`, falla con «could not parse PEM data».

**Márgenes.** El sidecar pide sesión nueva cuando a la suya le quedan menos de 10 minutos, y el SDK se la
vuelve a pedir cada 2. Lo que firma la API tiene siempre al menos 8 minutos por delante, más que los 5 de
una autorización de subida (research R3).

### Puesta en marcha, en este orden

1. **La imagen del sidecar** la publica el CI solo (job `credential-helper`) en cada push a develop o
   release. No hay nada que hacer.

2. **Certificados** (el dueño, una vez; se renuevan cada año). Crea una CA propia y un certificado por
   ambiente en una carpeta **fuera del repositorio**, y los instala como Secret
   `erp-adjuntos-certificado` en cada namespace. La llave de la CA queda cifrada con una frase que el
   guion pide y no guarda; nunca entra al clúster.

   ```bash
   powershell -ExecutionPolicy Bypass -File ./tools/scripts/crear-certificados-adjuntos.ps1 -Carpeta D:/Custodia/erp-adjuntos
   ```

   Sin `-Ambientes` emite DEV y QA. Producción, cuando toque, con `-Ambientes pdn` (pide escribir
   PRODUCCION). **Guarde la carpeta fuera de la máquina**: sin la llave de la CA y su frase no se emite ni
   se renueva ningún certificado.

3. **La pila de AWS** (un administrador de la cuenta 058264424927; el usuario del perfil `ingenia365` no
   tiene permisos de IAM ni de CloudFormation). Consola › CloudFormation › Crear pila › Cargar
   [plantillas/adjuntos-roles-anywhere.yaml](plantillas/adjuntos-roles-anywhere.yaml):
   - parámetro `CertificadoDeLaCa`: el contenido de `ca.crt` de la carpeta del paso 2 (es público);
   - marcar **«Reconozco que CloudFormation puede crear recursos de IAM con nombres personalizados»**
     (`CAPABILITY_NAMED_IAM`): los roles llevan nombre;
   - al terminar, la pestaña **Salidas** trae `TrustAnchorArn`, `ProfileArn` y un ARN de rol por
     ambiente. Son los que necesita el paso 4.

4. **GitOps, primero DEV y QA** (con los ARN del paso 3). En el overlay de cada ambiente se activa el
   componente `workloads/erp/components/adjuntos-roles-anywhere` —su cabecera dice exactamente qué
   agregar— con los tres ARN en el ConfigMap `erp-roles-anywhere` y la imagen del sidecar. El componente
   quita de la API `AWS_ACCESS_KEY_ID` y `AWS_SECRET_ACCESS_KEY`: el SDK las preferiría al sidecar.
   **Producción, sólo con autorización expresa del dueño** y después del paso 6 en QA.

5. **Espiga en DEV** (T074): que el SDK tome la credencial del sidecar y la renueve, cuánto vive de
   verdad una autorización firmada cerca del vencimiento de la sesión, y que `/health/ready` siga sano
   sin permiso de listar (la sonda escribe y borra bajo `dev/.healthcheck/`, dentro del prefijo).

6. **Verificar** (T075, quickstart §6 de la feature 011):
   - los permisos, con un pod de prueba que usa el mismo sidecar y el mismo certificado de la API y se
     borra solo (14 comprobaciones; en DEV y QA pasaron el 2026-09-23):

     ```bash
     powershell -ExecutionPolicy Bypass -File ./tools/scripts/probar-credencial-adjuntos.ps1 -Ambiente qa
     ```

   - la credencial vence a la hora sin cortar la API;
   - en QA, el ensayo de revocación de abajo.

   **Recién entonces**: borrar el Secret `erp-adjuntos-s3` de cada namespace, borrar en la consola el
   usuario IAM `ingenia365-erp-adjuntos` con su llave, y retirar
   [politica-iam-adjuntos.json](politica-iam-adjuntos.json).

### Renovar los certificados (cada año)

Los certificados de los ambientes duran un año; los de DEV y QA vencen el **2027-09-24**. En el último mes
de vigencia se vuelve a correr el mismo guion con la carpeta de custodia: reutiliza la CA (pide su frase),
emite los certificados que vencen en menos de 30 días, los instala y **reinicia la API** del ambiente para
que el sidecar tome el nuevo. El reinicio no corta el servicio (`maxUnavailable: 0`).

```bash
powershell -ExecutionPolicy Bypass -File ./tools/scripts/crear-certificados-adjuntos.ps1 -Carpeta D:/Custodia/erp-adjuntos
```

Si no se renueva, el día del vencimiento el sidecar ya no obtiene sesión: los adjuntos fallan y la API sale
de Ready.

### Si se filtra un certificado: revocarlo

Roles Anywhere no consulta ningún servidor de revocación: sólo conoce la **CRL** (lista de revocados) que se
le importa. En este orden, para no dejar a la API sin credencial:

1. Emitir el reemplazo, que se instala y reinicia la API:
   `crear-certificados-adjuntos.ps1 -Carpeta … -Ambientes <amb> -Renovar`. El certificado viejo queda
   en `anteriores/erp-api-<amb>-<serie>.crt` de la carpeta de custodia, y el guion dice cuál es.
2. Revocar el viejo. El guion lleva la base de la CA en `revocacion/` de la carpeta de custodia, suma la
   revocación a las anteriores, firma `revocados.crl.pem` y muestra los comandos para importarla:

   ```bash
   powershell -ExecutionPolicy Bypass -File ./tools/scripts/revocar-certificado-adjuntos.ps1 -Carpeta D:/Custodia/erp-adjuntos -Certificado anteriores/erp-api-qa-<serie>.crt
   ```

3. Un administrador la importa en CloudShell: `aws rolesanywhere import-crl … --enabled` la primera vez,
   y `update-crl` las siguientes, con la lista completa.

Desde ese momento el certificado no obtiene sesiones nuevas; las ya emitidas vencen en una hora como
mucho.

### Ensayo de revocación (T075, en QA)

Se ensaya con un certificado **desechable** del mismo CN, así que la API de QA no se entera:

1. `crear-certificados-adjuntos.ps1 -Carpeta … -Ambientes qa -Prueba`: emite `erp-api-qa-prueba.crt`
   (7 días) y lo instala como Secret `erp-adjuntos-certificado-prueba`.
2. `probar-credencial-adjuntos.ps1 -Ambiente qa -Secret erp-adjuntos-certificado-prueba -Prueba ObtieneCredencial`.
3. `revocar-certificado-adjuntos.ps1 -Carpeta … -Certificado erp-api-qa-prueba.crt`, e importar la CRL.
4. `probar-credencial-adjuntos.ps1 -Ambiente qa -Secret erp-adjuntos-certificado-prueba -Prueba SinCredencial`:
   el sidecar informa el rechazo.
5. `probar-credencial-adjuntos.ps1 -Ambiente qa`: la API sigue con los mismos permisos.
6. Borrar el Secret de prueba: `k3s kubectl delete secret erp-adjuntos-certificado-prueba -n erp-qa`.

**Mientras tanto**, la llave transitoria sigue en uso. Su política
([politica-iam-adjuntos.json](politica-iam-adjuntos.json)) ya **no permite listar** el bucket (desde el
2026-09-23): el ERP no lo necesita y trata el 403 de una clave inexistente como «no está». Hay que
pegarla de nuevo en la consola (IAM › Usuarios › `ingenia365-erp-adjuntos` › Permisos › `AdjuntosDelErp`
› Editar › JSON).

## Qué pasa si S3 no responde

**Una persona que sube un soporte**: el envío al bucket falla y la pantalla lo dice. La fila quedó
`Uploading`; cuando vence la autorización (5 minutos), la próxima vez que alguien mire la lista pasa a
**«Subida incompleta»** y se puede reintentar con el mismo archivo o borrar. Nada se borra solo.

**Un módulo que guarda lo que generó** (PILA, dispersión, definitiva): falla la generación y la pantalla
lo dice; la fila de `COR_Attachments` se escribe después de que el almacén confirma, así que **no queda a
medias**. En el caso inverso —objeto escrito y fila no guardada— el handler retira el objeto; si eso
también falla, el objeto queda **huérfano** y vigente. No rompe nada y **nada lo limpia solo** (FR-001):
lo encuentra un inventario manual del bucket contra las bases, que hoy no existe.

**Bajar**: el enlace se firma sin tocar el bucket, así que se entrega igual; es el navegador el que no
podrá bajar el archivo mientras S3 no responda.

Borrar con S3 caído falla antes de tocar la fila: el adjunto sigue vivo y se puede reintentar.

El resto del ERP no depende de los adjuntos: una nómina se aprueba y un comprobante se contabiliza
aunque el bucket esté caído.

## Desarrollo

`appsettings.json` trae `Provider: Local` y los archivos van a `storage/attachments`. El almacén local
**imita a S3**: firma tokens de DataProtection hacia rutas anónimas de la propia API
(`/api/attachments/local-blob/{token}`) que rechazan lo mismo que la política de S3, así que la pantalla
usa exactamente el mismo flujo, sin Docker. Esas rutas no existen con `Provider = S3` ni en Production.
Para probar S3 en local se puede apuntar a MinIO con `AttachmentStorage:S3:ServiceUrl` y
`ForcePathStyle: true`. Las pruebas (`S3BlobStoreTests`) usan un `IAmazonS3` falso y, para la forma de
las autorizaciones firmadas, un cliente real con credenciales ficticias (firmar no toca la red): **el CI
no tiene credenciales de AWS ni las necesita**. `AlmacenPrefirmadoTests` levanta MinIO con Docker
(`quay.io/minio/minio`: MinIO ya no publica en Docker Hub) para lo que sólo un almacén de verdad puede
decir: que la firma ata tamaño, tipo y clave, que el enlace vence y que borrar deja la versión en la
papelera.
