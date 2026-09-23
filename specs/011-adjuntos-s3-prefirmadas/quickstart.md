# Quickstart: validar adjuntos directos (011)

**Fecha**: 2026-09-23 · Se recorre en **DEV y después en QA**, antes de pedir la promoción a producción.
Cada paso dice qué criterio de éxito comprueba.

## 0. Previo

- Las dos llaves permanentes expuestas están **desactivadas** (FR-048). Se comprueba en la consola de
  IAM.
- La receta de [contracts/almacen.md](contracts/almacen.md) está aplicada.
- En el pod de la API hay dos contenedores (`api` y el sidecar) y ninguna variable `AWS_ACCESS_KEY_ID`:

  ```bash
  ssh -i ~/.ssh/ingenia365_deploy root@100.94.218.42 "k3s kubectl -n erp-dev get pod -l app.kubernetes.io/name=erp-api -o jsonpath='{.items[0].spec.containers[*].name}'"
  ```

- `/health/ready` responde `blobstore: S3: s3://ingenia365-erp-attachments/dev`, con la credencial
  temporal.

## 1. Subir un soporte (US1 · SC-001, SC-008, SC-011)

1. Con un usuario que tiene `Attachments.Upload` y `Accounting.Vouchers.Create`, abrir un comprobante
   en borrador › Soportes › subir un PDF de ~20 MB.
2. El soporte pasa por «subiendo» a «disponible». En la pestaña Red del navegador, el POST del archivo
   va a `ingenia365-erp-attachments.s3.amazonaws.com`, **no** a `app-dev…/api`.
3. Renombrar un `.exe` a `.pdf` y subirlo: queda **«rechazado»**, con el motivo, y no ofrece Descargar.
4. Pedir por la API (§1 del contrato) una subida con `ownerEntityType = "PilaGeneration"`: **422
   `Attachments.OwnerNotAllowed`**. Pedirla para un comprobante de otra cooperativa, o con un usuario
   sin `Accounting.Vouchers.Create`: **404**, igual que si no existiera.
5. Pedir una subida de 30 MB: **422 `Validation.Attachments.FileTooLarge`**, antes de subir nada.

## 2. Una autorización no sirve para otra cosa (SC-005)

Con los `fields` de una autorización de 1 MB, intentar subir desde `curl` un archivo de 2 MB, con otro
`Content-Type`, o con otra `key`. El almacén responde **403** en los tres casos.

## 3. Subida incompleta (US1, escenario 5)

Pedir una autorización y no usarla. Pasados 5 minutos, abrir la lista: la pantalla confirma sola y el
soporte figura **«subida incompleta»**, con Reintentar y Borrar. Reintentar entrega otra autorización
sobre la misma fila.

## 4. Bajar (US2 · SC-003, SC-005, SC-010)

1. Descargar el PDF del paso 1. Baja con su nombre original, tildes incluidas. La descarga sale de
   `s3.amazonaws.com` y **empieza en menos de 2 s** (con un archivo de hasta 5 MB). La huella coincide:
   `certutil -hashfile <archivo> SHA256` es igual a `Sha256Hex` de la fila.
2. Copiar el enlace y abrirlo **61 s después**: el almacén responde **AccessDenied (Request has
   expired)**.
3. Con un usuario sin `Accounting.Vouchers.View`, la lista de soportes viene vacía, y pedir el enlace
   por la API da **404**.
4. Bajar un adjunto viejo de DEV (`Format = 1`): llega correcto, a través de la API.
5. En Nómina › PILA y en Nómina › Dispersión, descargar el archivo generado. Baja directo. La PILA sigue
   pidiendo reconocer el descuadre antes y conserva su codificación, lo que se ve abriéndola en el
   validador del operador.

## 4b. Móvil (FR-018)

En la app MAUI, en **Windows** y en **Android**:

1. Abrir un comprobante en borrador › Soportes y subir un PDF. Tiene que quedar «disponible», con la
   subida directa al almacén, igual que en la web.
2. Bajarlo. Tiene que llegar idéntico y con su nombre.

Si la espiga de orígenes dejó la subida deshabilitada con el aviso «súbalo desde la web», este paso
**no está aprobado** hasta quitar ese aviso.

## 5. Borrar y recuperar (US3 · SC-004, SC-009)

1. Borrar el soporte del borrador: desaparece de la lista, y la auditoría registra quién, cuándo y qué.
2. **Recuperar**, con la receta de soporte y el perfil `ingenia365`:

   ```bash
   aws --profile ingenia365 s3api list-object-versions --bucket ingenia365-erp-attachments --prefix dev/<cooperativa>/<aaaa>/<mm>/<guid>.bin
   ```

   Borrar la **marca** (`DeleteObjectVersion` sobre el `VersionId` de la marca) y reactivar la fila. El
   soporte vuelve, idéntico.
3. Contabilizar el comprobante e intentar borrar un soporte, desde la pantalla (no hay botón) y por la
   API: **422 `Attachments.OwnerLocked`**.
4. Descartar un borrador que tiene soportes: la pantalla pregunta. Sin confirmar, **422
   `Accounting.Document.HasAttachments`**; confirmando, se descartan juntos.

## 6. La credencial (US5 · SC-006)

1. Con la credencial que entrega el sidecar (desde dentro del pod, sin salir de él), `aws s3 ls
   s3://ingenia365-erp-attachments/dev/` responde **AccessDenied**. Un `GetObject` sobre `qa/…` o
   `pdn/…`, también.
2. Esa credencial vence sola a la hora (`Expiration` en la respuesta del sidecar), y la API sigue
   funcionando sin reinicio.
3. En QA, revocar el certificado importando la CRL. A más tardar en una hora, el sidecar ya no obtiene
   credenciales, y `/health/ready` lo muestra. Después, restituir el certificado.

## 7. Memoria y carga (SC-001, SC-002)

- Con veinte subidas de 25 MB en paralelo (un guion con `curl` sobre autorizaciones reales), la memoria
  de la API no se mueve (`kubectl top pod`) y otra pantalla, por ejemplo la lista de comprobantes,
  responde en el mismo tiempo que sin la carga.
- La prueba de asignaciones de la validación (R17) está verde en CI.

## 8. Costo (SC-007)

Una semana después, en Billing › Cost Explorer, filtrando por el servicio S3 y el bucket: el cargo es
proporcional a lo subido y no hay ningún cargo fijo nuevo.
