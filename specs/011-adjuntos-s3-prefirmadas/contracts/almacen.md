# Contrato con el almacén (011)

**Fecha**: 2026-09-23 · Lo que la cuenta de AWS `058264424927` tiene que tener para que la feature
funcione. Lo aplica **una vez** un administrador de la cuenta (FR-050): el usuario del perfil
`ingenia365` no tiene permisos de IAM.

Nada de esto tiene costo mensual fijo (FR-047).

## 1. Bucket `ingenia365-erp-attachments` (us-east-1)

Ya existe desde el 2026-09-22, con acceso público bloqueado, versionado y SSE-S3 (AES256) con Bucket
Key.

**Ciclo de vida.** Se le agrega la purga de la marca huérfana:

```json
{
  "Rules": [{
    "ID": "papelera-90-dias",
    "Status": "Enabled",
    "Filter": {},
    "NoncurrentVersionExpiration": { "NoncurrentDays": 90 },
    "Expiration": { "ExpiredObjectDeleteMarker": true },
    "AbortIncompleteMultipartUpload": { "DaysAfterInitiation": 7 }
  }]
}
```

**Política del bucket.** Sólo TLS:

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Sid": "SoloTls",
    "Effect": "Deny",
    "Principal": "*",
    "Action": "s3:*",
    "Resource": ["arn:aws:s3:::ingenia365-erp-attachments", "arn:aws:s3:::ingenia365-erp-attachments/*"],
    "Condition": { "Bool": { "aws:SecureTransport": "false" } }
  }]
}
```

**CORS.** Sólo subidas desde los orígenes del ERP. Descargar no necesita CORS, porque es navegación:

```json
[{
  "AllowedMethods": ["POST"],
  "AllowedOrigins": [
    "https://app-dev.ingenia365.com",
    "https://app-qa.ingenia365.com",
    "https://app.ingenia365.com",
    "https://0.0.0.1",
    "app://0.0.0.1"
  ],
  "AllowedHeaders": ["*"],
  "ExposeHeaders": ["ETag"],
  "MaxAgeSeconds": 3000
}]
```

Los dos últimos orígenes son los de BlazorWebView (MAUI, .NET 8+). **Confirmados en el código**
(2026-09-23, `dotnet/maui` rama `net10.0`, la versión del proyecto):
- `HostAddressHelper.GetAppHostAddress()` devuelve `0.0.0.1`, salvo con el interruptor
  `BlazorWebView.AppHostAddressAlways0000`, que la app no usa;
- `WebView2WebViewManager` (Windows) y `AndroidWebKitWebViewManager` sirven la página desde
  `https://0.0.0.1/`;
- `BlazorWebViewHandler.iOS` (iOS y Mac Catalyst) la sirve desde `app://0.0.0.1/`.

Falta verlo en un dispositivo (T081). El guion del bucket ya los incluye.

## 2. Credenciales temporales (IAM Roles Anywhere)

| Pieza | Valor |
|---|---|
| CA | propia, RSA 3072 o EC P-384, diez años; **la llave privada nunca entra al clúster** (custodia del dueño) |
| Certificados | uno por ambiente, CN `erp-api-dev` / `erp-api-qa` / `erp-api-pdn`, un año, `digitalSignature` |
| *Trust anchor* | `ingenia365-erp`, tipo `CERTIFICATE_BUNDLE` con el certificado de la CA; CRL importada cuando haga falta revocar |
| Perfil | `ingenia365-erp-adjuntos`, `durationSeconds = 3600` |
| Roles | `ingenia365-erp-adjuntos-dev`, `-qa`, `-pdn`. Confianza en `rolesanywhere.amazonaws.com`, con condición sobre el CN del certificado |

**Política de confianza de cada rol** (el ejemplo es de `pdn`):

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": { "Service": "rolesanywhere.amazonaws.com" },
    "Action": ["sts:AssumeRole", "sts:TagSession", "sts:SetSourceIdentity"],
    "Condition": {
      "StringEquals": { "aws:PrincipalTag/x509Subject/CN": "erp-api-pdn" },
      "ArnEquals": { "aws:SourceArn": "arn:aws:rolesanywhere:us-east-1:058264424927:trust-anchor/<id>" }
    }
  }]
}
```

**Política de permisos de cada rol** (el ejemplo es de `pdn`: cada ambiente sólo ve su prefijo):

```json
{
  "Version": "2012-10-17",
  "Statement": [
    { "Sid": "ObjetosDelAmbiente", "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
      "Resource": "arn:aws:s3:::ingenia365-erp-attachments/pdn/*" },
    { "Sid": "NiListarNiVersiones", "Effect": "Deny",
      "Action": ["s3:ListBucket", "s3:ListBucketVersions", "s3:ListBucketMultipartUploads",
                 "s3:DeleteObjectVersion", "s3:GetObjectVersion",
                 "s3:PutBucketVersioning", "s3:PutLifecycleConfiguration", "s3:PutBucketPolicy",
                 "s3:DeleteBucketPolicy", "s3:PutBucketPublicAccessBlock", "s3:PutEncryptionConfiguration",
                 "s3:PutBucketCORS", "s3:DeleteBucket"],
      "Resource": "*" },
    { "Sid": "NoTocarElBucketDeRespaldos", "Effect": "Deny", "Action": "s3:*",
      "Resource": ["arn:aws:s3:::ingenia365-erp-backups", "arn:aws:s3:::ingenia365-erp-backups/*"] }
  ]
}
```

## 3. En el clúster, por ambiente

| Pieza | Valor |
|---|---|
| Secret | `erp-adjuntos-certificado` con `tls.crt` y `tls.key` del ambiente. Se monta **sólo** en el sidecar |
| Sidecar | `aws_signing_helper serve --port 9911` con el certificado, la llave, el ARN del *trust anchor*, el del perfil y el del rol del ambiente |
| API | `AWS_EC2_METADATA_SERVICE_ENDPOINT=http://127.0.0.1:9911`; **sin** `AWS_ACCESS_KEY_ID` ni `AWS_SECRET_ACCESS_KEY` |
| Se retira | el Secret `erp-adjuntos-s3` y las dos variables que lo leen |

## 4. Lo que el ERP **no** puede hacer, y es a propósito

Listar el bucket, borrar versiones, recuperar un borrado, cambiar la configuración del bucket, tocar
el bucket de respaldos o ver el prefijo de otro ambiente. La recuperación y la supresión son recetas
de soporte con la credencial administrativa (R8).
