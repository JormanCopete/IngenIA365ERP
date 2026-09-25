# Desplegar los adjuntos directos a producción (feature 011)

> **Hecho el 2026-09-24** (`release e0c97b9`, GitOps `f1ba464` + `01d5bb6`, respaldos
> `/root/respaldos/*-20260924-pre-f011.dump`). Tropiezo que conviene no repetir: el primer certificado
> de producción se emitió con la carpeta de custodia **sin** la CA original, el guion creó una CA nueva y
> AWS respondió «Untrusted signing certificate»; el despliegue quedó detenido **sin corte** (los pods viejos
> siguieron atendiendo) hasta reemitirlo con la CA original. La limpieza de abajo se hizo el mismo día.

**El documento a tener delante el día que develop pase a release.** Producción (`release 8d3f9a1`)
todavía no tiene ni `S3BlobStore`: guarda los adjuntos en el disco del nodo aunque su overlay ya diga
`Provider=S3`. Esta promoción le lleva de una vez el almacén S3, el flujo directo (el archivo no pasa por
la API) y las credenciales temporales. Lo que no es de la feature 011 va en el mismo paso porque ya está
en develop y en QA.

## Qué sube

| Commit en develop | Qué es |
|---|---|
| `80d6964`, `4afea06`, `a9d150f` | `S3BlobStore` detrás de `IBlobStore`, el guion del bucket y la sonda del almacén cada 5 minutos |
| `d35e91f` (E1) | nada se borra solo, reglas por dueño, `POST /api/attachments` multipart retirado, limitador `adjuntos` |
| `b7bd207` (E3) | subida y descarga directas con autorizaciones firmadas; la pantalla de soportes en Comprobantes |
| `af84c24`, `b4d8e6b`, `d9519b2` (E2) | credenciales temporales (Roles Anywhere) y sus herramientas; pruebas de rutas y de memoria |
| `2ae7022` | editar un rol respondía 500 |
| `19a650d` | nómina: aprobar la propia liquidación era imposible desde la pantalla |
| `e6395f6` | plan de implementación de COOFLOPAL (sólo documentos) |

**Una migración**, `AdjuntosDirectos`, **aditiva**: seis columnas en `COR_Attachments`, con valores por
defecto `Format = 1` (el formato anterior) y `Status = 2` (disponible). La aplica el Job PreSync del
migrador al sincronizar `erp-pdn`.

## La decisión: producción entra directo con credenciales temporales

**Recomendado.** Activar Roles Anywhere en `erp-pdn` en la misma sincronización que trae el código:
- producción **nunca** usa la llave permanente del usuario IAM `ingenia365-erp-adjuntos`, la que se filtró;
- esa llave no la usa hoy **ningún** ambiente (DEV y QA van con el sidecar desde el 2026-09-23 y el código
  de producción no toca S3), así que puede **desactivarse ya**: consola › IAM › Usuarios ›
  `ingenia365-erp-adjuntos` › Credenciales de seguridad › Desactivar. Antes, ver ahí mismo su
  «Último uso»: tiene que ser anterior al 2026-09-24 02:28 UTC, la hora en que DEV y QA pasaron al sidecar.
  Desactivar es reversible; borrar el usuario viene después de verificar (§ Después). Con esto T001
  —rotar la llave— deja de hacer falta: no se rota lo que se retira.

La alternativa —promover con la llave fija y pasar a Roles Anywhere después— obliga a rotar la llave
primero y a tocar producción dos veces.

## Antes: lo que hay que tener y lo que hay que mirar

1. **Autorización expresa** del dueño para promover y para el cambio de GitOps de producción.
2. **Certificado de producción** (el dueño, con la carpeta de custodia y la frase de la CA):

   ```bash
   powershell -ExecutionPolicy Bypass -File ./tools/scripts/crear-certificados-adjuntos.ps1 -Carpeta D:/Custodia/erp-adjuntos -Ambientes pdn
   ```

   Pide escribir PRODUCCION e instala el Secret `erp-adjuntos-certificado` en `erp-pdn`. Todavía no
   reinicia nada: el sidecar no está activo allí.
3. **Respaldo** con `pg_dump` de la base administrativa y de cada cooperativa, como en la promoción de la
   feature 010 (`*-<fecha>-pre-f011.dump`).
4. **¿Hay adjuntos en el disco de producción?** Si los hay, quedarían inalcanzables al pasar a S3. El
   2026-09-23 había **0 archivos**; hay que volver a mirarlo el mismo día, porque PILA, dispersión y la
   liquidación definitiva generan adjuntos:

   ```bash
   ssh -i ~/.ssh/ingenia365_deploy root@100.104.190.76 'find /var/lib/rancher/k3s/storage/*erp-pdn_erp-attachments* -type f | wc -l'
   ```

   Si no da 0, **parar**. Hay que copiarlos al bucket bajo `pdn/` con la misma ruta relativa antes de
   sincronizar; es otro procedimiento y lleva su propia autorización.
5. **El CORS del bucket** ya incluye `https://app.ingenia365.com`:

   ```bash
   aws --profile ingenia365 s3api get-bucket-cors --bucket ingenia365-erp-attachments
   ```

## Orden

1. **GitOps: activar el componente en `overlays/pdn`**, igual que en DEV y QA (commit `2e3f5cb` del repo
   GitOps). Agregar el componente, el ConfigMap `erp-roles-anywhere` con `roleArn=…:role/ingenia365-erp-adjuntos-pdn`
   y la imagen `credential-helper` por digest (el de develop sirve de partida; el CI de release lo
   reemplaza). Producción queda `OutOfSync` hasta el paso 4: no cambia nada todavía.
2. **Merge de develop a release** y push. El CI construye las imágenes y su job `gitops` escribe en
   `overlays/pdn` los digests de api, web, migrator y credential-helper.
3. **Refrescar** la aplicación `erp-pdn` de Argo (`argocd.argoproj.io/refresh=normal`) y comprobar que el
   render trae la migración, las imágenes nuevas y el sidecar.
4. **Sincronizar** `erp-pdn`: el `patch` del campo `operation` con la revisión del paso 2. El Job del
   migrador aplica `AdjuntosDirectos` (~2 min) y después relevan las dos réplicas de la API
   (`maxUnavailable: 0`: los pods viejos atienden hasta que los nuevos pasan `/health/ready`, que escribe
   y borra en el bucket con la credencial temporal).

## Verificar

- Las dos réplicas Ready, sidecar arrancado y sin reinicios; `/health/ready` con
  `blobstore: S3: s3://ingenia365-erp-attachments/pdn`.
- Las 14 comprobaciones de permisos, con el rol de producción:

  ```bash
  powershell -ExecutionPolicy Bypass -File ./tools/scripts/probar-credencial-adjuntos.ps1 -Ambiente pdn
  ```

- La quickstart §1 a §5 de la feature 011 en `app.ingenia365.com`: subir un soporte a un comprobante en
  borrador, verlo disponible, bajarlo, borrarlo; un ejecutable renombrado a `.pdf` queda rechazado.
- Las otras dos correcciones: editar un rol no responde 500; aprobar la propia liquidación responde el
  código de segregación, no un error genérico.

## Rollback

Volver los digests de `overlays/pdn` a los del `release 8d3f9a1` y sincronizar. La migración **no se
deshace**: es aditiva, y el código anterior ignora las columnas nuevas; sus inserciones toman los valores
por defecto (formato anterior, disponible), que es lo que ese código espera. Lo que se haya subido con el
flujo nuevo queda en el bucket y vuelve a verse al avanzar otra vez. Al revés no: lo que el código
anterior guarde mientras dure el rollback va al disco del nodo, y antes de volver a avanzar hay que
copiarlo al bucket (el punto 4 de «Antes»). El sidecar puede quedarse: el código anterior no usa S3.

## Después (cierra T075 y T071) — hecho el 2026-09-24 salvo lo marcado

1. ✅ Borrado el Secret `erp-adjuntos-s3` de `erp-dev`, `erp-qa` y `erp-pdn` (ningún pod lo referenciaba).
2. ✅ GitOps `1fdc852`: `base/api.yaml` sin `AWS_ACCESS_KEY_ID` ni `AWS_SECRET_ACCESS_KEY` (el manifiesto
   renderizado no cambió: el componente ya las quitaba). El volumen `erp-attachments` se retiró
   después, el mismo día, tras comprobar que estaba vacío en los tres ambientes: GitOps `ee8c324`
   (DEV y QA) y `431092b` (`base`, producción sincronizada, respaldo `*-20260924c-pre-volumen.dump`).
3. ⏳ El dueño **elimina** en la consola el usuario IAM `ingenia365-erp-adjuntos` (su llave ya está desactivada).
4. ✅ Retirado `docs/operaciones/politica-iam-adjuntos.json`; el guion del bucket ya no crea el usuario ni
   tiene `-OmitirIam`/`-SoloSecreto`.
