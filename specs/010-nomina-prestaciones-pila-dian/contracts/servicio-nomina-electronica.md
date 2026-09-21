# Contrato: servicio central de nómina electrónica (ERP → `IngenIA365.NominaElectronica` → DIAN)

**Feature**: 010 | **Date**: 2026-09-20 | **Base**: research R10 | **Hermano**: `api.md` §8

## 0. Qué es y qué no es

Un proyecto .NET 10 aparte (`src/Servicios/IngenIA365.NominaElectronica`, Minimal API), desplegado
en el clúster **sin acceso público** (ClusterIP; NetworkPolicy: entra sólo desde los pods de la API
del ERP, sale sólo a `vpfe-hab.dian.gov.co` y `vpfe.dian.gov.co` por 443). Hace lo que el ERP **no
puede** hacer sin conocer secretos: completar el CUNE y el SoftwareSC (necesitan el PIN), firmar
(necesita el certificado) y hablar con el servicio web de la DIAN (WS-Security con ese mismo
certificado).

**No tiene base de datos ni disco de estado.** La decisión de la R10 de darle una PostgreSQL propia
con `Habilitacion`, `Certificado`, `Documento` y `Envio` de todas las cooperativas **se descarta**:
sería un espacio compartido entre cooperativas, y el Principio IV no lo admite. Todo lo de cada
cooperativa —habilitación, rangos, documentos con su XML sin firmar y firmado, ZIP,
`ApplicationResponse`, CUNE, estados, intentos y errores traducidos, notas— vive en la base de
**esa** cooperativa (`PAY_ElectronicPayroll*`), escrito por el ERP. El servicio recibe en cada
petición todo lo que necesita, responde con todo lo que produjo y **olvida**.

Lo que el servicio **no guarda ni escribe**: XML (en ningún estado), ZIP, respuestas de la DIAN,
CUNE, NIT, datos del trabajador, certificados descifrados, PIN, contraseñas. Sus logs (Serilog)
llevan sólo `tenantId`, tipo y número del documento, operación, `StatusCode`, duración y el
`traceId` del ERP; una prueba de arquitectura vigila que ningún `Log*` reciba el XML ni el cuerpo de
la petición, y otra que sólo este proyecto referencie criptografía de firma y que ningún proyecto del
ERP conozca PIN ni certificado (R10, R14).

## 1. Autenticación por cooperativa

- **Token**: JWT RS256 emitido por la API del ERP (la misma llave con que firma la identidad
  central; el servicio valida contra su JWKS interno), vida **5 minutos**, `aud = "nomina-electronica"`,
  `sub = "erp-api"`, claim **`tenant`** = `PublicId` de la cooperativa (`ADM_Tenants`), claim
  `scope = "nomina-electronica"`. Lo acuña `ServicioDeNominaElectronicaClient` en la API por cada
  petición, con la cooperativa que `TenantResolutionMiddleware` ya resolvió; nunca desde el
  cliente Blazor.
- **Cabeceras**: `Authorization: Bearer …`, `X-Tenant-Id` (debe coincidir con el claim `tenant`; si
  no, 403 `Servicio.TenantNoCoincide` y se registra), `X-Trace-Id` (el `traceId` del ERP, para
  cruzar logs), `X-Idempotency-Key` (lo pone el ERP: `{tenant}:{tipo}:{numero}:{ambiente}`; el
  servicio **no** lo usa para deduplicar —no tiene dónde— sólo lo devuelve y lo loguea).
- **Identidad de la habilitación**: cada cuerpo lleva `habilitacion` (NIT, DV, modo, ambiente,
  `softwareId`) y `secretos` (nombres). El servicio sólo resuelve secretos bajo la carpeta del
  tenant del token (`/secrets/tenants/{tenant}/…`); un nombre fuera de ella no existe para esa
  petición → 403 `Servicio.SecretoAjeno`. Así una cooperativa no puede firmar con el certificado
  de otra aunque adivine el nombre. El NIT del `Empleador` en el XML debe coincidir con
  `habilitacion.nit` → 422 `Servicio.NitNoCoincide`.
- Sin token, token vencido o `aud` distinto → 401 `Servicio.NoAutenticado`. Una `NetworkPolicy`
  es la primera puerta; el token, la segunda; la carpeta del tenant, la tercera.

## 2. Secretos

Un Secret de Kubernetes **por cooperativa y ambiente**, creado por el dueño con
`tools/scripts/crear-secreto-nomina-electronica.ps1 -Ambiente pdn -Tenant <publicId> -Certificado
<ruta.p12>` (patrón de `crear-secreto-smtp.ps1`: contraseña y PIN por teclado, viajan por STDIN del
canal SSH, nada queda en disco local ni en el historial):

| Secret | Claves | Montado en |
|---|---|---|
| `erp-ne-<tenant-publicid>` | `certificado.p12`, `certificado.clave`, `pin` | `/secrets/tenants/<tenant>/` |
| `erp-ne-ingenia365` (modo PT, futuro) | `certificado.p12`, `certificado.clave` | `/secrets/plataforma/` (sólo se lee si `Modos:ProveedorTecnologicoHabilitado = true`) |

Rotación: se reemplaza el Secret; el servicio lee el archivo en cada petición (caché en memoria
≤ 5 min por huella), así que no exige reinicio. El ERP guarda en `PAY_ElectronicPayrollSettings`
**sólo el nombre** (`SecretName`) y nunca lo muestra en el diff de auditoría. Ni el `.p12`, ni la
contraseña, ni el PIN entran al ERP, al repositorio, a `appsettings*.json` ni al GitOps en claro.
El certificado es de una **ECD acreditada por ONAC** (Certicámara, GSE, Andes SCD, Camerfirma), de
persona jurídica, con Digital Signature + Non Repudiation, `sha256WithRSA` (R10).

## 3. Rutas — `/v1` (JSON; binarios en base64)

Convención de errores: mismo envelope `{ code, message, traceId, data? }`; 400 cuerpo o XML
inválido, 401/403 identidad, 422 negocio, 502 la DIAN respondió algo que no se pudo interpretar
(`Servicio.DianRespuestaIlegible`, con el cuerpo crudo en `data.raw`). **Un timeout de la DIAN no es
error HTTP**: se responde 200 con `resultado = EnProceso` y todos los artefactos ya producidos
(XML firmado, ZIP, CUNE), para que el ERP los guarde y consulte después; si respondiera 504 el ERP
no tendría el CUNE con que preguntar.

Objetos comunes:

```jsonc
// habilitacion
{ "nit": "890300001", "dv": "3", "razonSocial": "COOFLOPAL",
  "modo": "SoftwarePropio" | "ProveedorTecnologico",
  "ambiente": 1 | 2,                       // 1 producción, 2 pruebas (DIAN)
  "softwareId": "uuid-del-catalogo-dian", "testSetId": "uuid" | null }
// secretos
{ "certificado": "erp-ne-<tenant>", "pin": "erp-ne-<tenant>" }   // nombres, nunca valores
// errorDian
{ "regla": "NIE022", "tipo": "Rechazo" | "Notificacion", "mensaje": "<texto DIAN>",
  "traduccion": "<lenguaje llano del diccionario del servicio>" }
// dian (bloque de respuesta)
{ "operacion": "SendNominaSync", "url": "https://vpfe-hab.dian.gov.co/…", "statusCode": "00" | "99" | "…",
  "statusDescription": "…", "statusMessage": "…", "isValid": true, "errores": [errorDian],
  "xmlDocumentKey": "<CUNE>", "zipKey": null, "applicationResponse": "<base64>", "duracionMs": 1830,
  "respondidoEn": "2026-12-05T09:12:44-05:00" }
```

| Ruta | Cuerpo | Respuesta | Qué hace |
|---|---|---|---|
| `GET /salud` · `GET /listo` | — | 200 | liveness / readiness (sin token); `listo` comprueba que la raíz de secretos está montada y que hay salida a la URL de la DIAN del ambiente configurado |
| `POST /documentos/validar` | `{ tipo: 102 \| 103, numero, habilitacion, secretos, xmlSinFirmar }` | `{ valido, errores: [{ linea, columna, mensaje }], cune, softwareSc, nombreArchivo, nombreZip }` | valida contra el XSD v1.0.6 embebido, calcula CUNE y SoftwareSC (para eso pide el PIN) y devuelve nombres de archivo; **no firma ni transmite**. El ERP lo usa al «revisar» antes de transmitir |
| `POST /documentos/firmar-y-transmitir` | `{ tipo, numero, habilitacion, secretos, xmlSinFirmar, soloFirmar?: false }` | `{ cune, softwareSc, xmlFirmado, zip, nombreArchivo, nombreZip, firmadoEn, resultado: Aceptado \| Rechazado \| EnProceso, dian }` | completa CUNE/SoftwareSC en el XML, valida XSD (400 si falla: **nunca firma un XML inválido**), firma XAdES-EPES, empaqueta el ZIP, `SendNominaSync` (timeout 60 s). `soloFirmar = true` devuelve `dian = null` y `resultado = Firmado` (para el set de pruebas por lotes o para inspección) |
| `POST /documentos/consultar-estado` | `{ habilitacion, secretos, trackId }` | `{ resultado, dian }` | `GetStatus(trackId = CUNE)`; `resultado` según §5. Es la única forma segura de saber si un envío en timeout llegó (R10) |
| `POST /set-de-pruebas/enviar` | `{ habilitacion (ambiente 2, testSetId obligatorio), secretos, documentos: [{ tipo, numero, xmlSinFirmar }] }` | `{ documentos: [{ numero, cune, xmlFirmado, zip, nombreZip, zipKey, errores[] }] }` | por cada documento firma y llama `SendTestSetAsync(fileName, contentFile, testSetId)` → `UploadDocumentResponse { ZipKey, ErrorMessageList }`; el estado del set se consulta después |
| `POST /set-de-pruebas/estado` | `{ habilitacion, secretos, zipKey }` | `{ resultado, dian }` | `GetStatusZip(trackId = zipKey)` |
| `POST /certificados/inspeccionar` | `{ habilitacion, secretos }` | `{ sujeto, emisor, huella, validoDesde, validoHasta, diasParaVencer, algoritmo, usos[], esPersonaJuridica, nitEnSujeto? }` | abre el `.p12` con su clave y describe el certificado; 422 `Servicio.CertificadoVencido`, `Servicio.CertificadoSinUsoDeFirma`; el ERP lo llama al guardar la habilitación y en la alerta de 30 días |
| `GET /reglas` | — | `[{ regla, tipo, traduccion, accion }]` | el diccionario NIExxx → lenguaje llano y qué corregir; el ERP lo cachea por día y traduce con él lo que la DIAN devuelve |
| `GET /version` | — | `{ servicio, anexo: "v1.0", xsd: "1.0.6", politicaFirma: "v2", hashPolitica, modos: { softwarePropio: true, proveedorTecnologico: false } }` | lo que el ERP muestra en la pantalla de habilitación |

Errores propios: 400 `Servicio.XmlInvalido` (`data: { errores[] }`), `Servicio.XmlConCune` (el
ERP mandó CUNE o SoftwareSC no vacíos: los llena el servicio), `Servicio.TipoNoCoincide` (la raíz
no es la del `tipo`), `Servicio.NumeroNoCoincide` (`NumeroSecuenciaXML` ≠ `numero`); 401
`Servicio.NoAutenticado`; 403 `Servicio.TenantNoCoincide`, `Servicio.SecretoAjeno`; 422
`Servicio.NitNoCoincide`, `Servicio.SecretoNoEncontrado` (`data: { nombre, clave }`),
`Servicio.CertificadoVencido`, `Servicio.CertificadoClaveIncorrecta`, `Servicio.PinAusente`,
`Servicio.ModoNoDisponible` (PT pedido con `ProveedorTecnologicoHabilitado = false`),
`Servicio.AmbienteNoConfigurado`; 502 `Servicio.DianRespuestaIlegible`.

## 4. Idempotencia y reintentos (los lleva el ERP)

- La unicidad de `(cooperativa, tipo, número, ambiente)` es del ERP: el rango interno de
  `PAY_ElectronicPayrollRanges` entrega el número dentro de la transacción que crea el documento;
  el servicio no deduplica porque no tiene memoria.
- El servicio **nunca reintenta** `SendNominaSync` por su cuenta (riesgo de duplicado, R10). Ante
  timeout, 5xx o red devuelve `EnProceso` con los artefactos; el ERP guarda XML firmado, ZIP y CUNE,
  y consulta con `consultar-estado` (backoff 1, 5, 15, 60 min, máx. 24 h). Reenvía el **mismo ZIP**
  (`POST firmar-y-transmitir` con el mismo `numero`; el XML firmado se regenera con la misma
  `FechaGen/HoraGen`, así el CUNE no cambia) sólo cuando la consulta responde «no encontrado».
- `StatusCode`: `00` procesado correctamente; `99` con reglas de rechazo; la comunidad reporta `66`
  (documento ya procesado) y `90` (trackId no encontrado) —**no verificados**: se observan en
  habilitación y se registran en `PAY_ElectronicPayrollAttempts.StatusCode` tal cual, y en el
  diccionario cuando se confirmen.

## 5. Máquina de estados que el servicio informa y el ERP guarda

| Respuesta del servicio | `resultado` | Estado ERP (`PAY_ElectronicPayrollDocuments.Status`) | Condición |
|---|---|---|---|
| `dian.isValid = true` y `xmlDocumentKey` presente y `applicationResponse` presente | `Aceptado` | `Accepted` (3) con `Cune`, `AcceptedAt`, `ApplicationResponse` | **las tres**; si falta una, `EnProceso` y se consulta |
| `statusCode = "99"` o `isValid = false` con reglas `Rechazo` | `Rechazado` | `Rejected` (4) con `errores` traducidos | reglas `Notificacion` solas no rechazan: quedan como avisos del documento aceptado |
| timeout / red / 5xx DIAN | `EnProceso` | `InProcess` (5) | el ERP programa `consultar-estado` |
| `consultar-estado` → aceptado / rechazado | idem | idem | |
| `consultar-estado` → «no encontrado» | `NoEncontrado` | sigue `InProcess`; el ERP decide reenviar | |
| `soloFirmar` | `Firmado` | `Signed` (1) | |

## 6. Mapeo al servicio web de la DIAN (research R10)

| Asunto | Valor |
|---|---|
| Norma | Res. 013/2021 compilada en la **Res. Única 000227 del 23-09-2025** (arts. 1.5.3.1.1–1.5.3.9.x); anexo técnico **Documento Soporte de Pago de Nómina Electrónica v1.0**; Caja de Herramientas V1-0 con XSD **v1.0.6** (`NominaIndividualElectronicaXSDV1.0.6.xsd`, `NominaIndividualDeAjusteElectronicaXSDV1.0.6.xsd`), embebidos como recursos del servicio |
| Endpoints SOAP | habilitación `https://vpfe-hab.dian.gov.co/WcfDianCustomerServices.svc`; producción `https://vpfe.dian.gov.co/WcfDianCustomerServices.svc`; SOAP 1.2 document/literal sobre TLS 1.2; `Content-Type: application/soap+xml` |
| WS-Addressing | `wsa:Action = http://wcf.dian.colombia/IWcfDianCustomerServices/<Operación>`, `wsa:To` = URL del servicio |
| WS-Security (X.509 Token Profile) | `wsu:Timestamp` (Created/Expires ≈ 1 min), `wsse:BinarySecurityToken` con el certificado, `ds:Signature` exc-c14n + `rsa-sha256` sobre el header `wsa:To` con `SecurityTokenReference`. Cliente SOAP **a mano** (XDocument + `SignedXml` + `HttpClient`): el binding del WSDL (TransportBinding + EndorsingSupportingTokens X.509) no lo reproduce el cliente WCF de .NET (dotnet/wcf #4828) |
| Operaciones | `SendNominaSync(contentFile: zip base64)` → `DianResponse`; `SendTestSetAsync(fileName, contentFile, testSetId)` → `UploadDocumentResponse { ZipKey, ErrorMessageList }`; `GetStatus(trackId = CUNE)` → `DianResponse`; `GetStatusZip(trackId = ZipKey)` → `DianResponse[]` |
| `DianResponse` | `IsValid`, `StatusCode` (`00`/`99`/…), `StatusDescription`, `StatusMessage`, `ErrorMessage[]` con la forma «Regla: NIExxx, Rechazo\|Notificación: …», `XmlBase64Bytes` = `ApplicationResponse` firmado (ResponseCode 02 validado / 04 rechazado), `XmlDocumentKey` = CUNE |
| XML | raíces `NominaIndividual` (**TipoXML 102**) y `NominaIndividualDeAjuste` (**TipoXML 103**, `TipoNota` 1 Reemplazar / 2 Eliminar); namespaces `dian:gov:co:facturaelectronica:NominaIndividual[DeAjuste]`; `InformacionGeneral/@Version` = «V1.0: Documento Soporte de Pago de Nómina Electrónica» (regla NIE022); `Ambiente` 1/2; valores con punto y dos decimales, sin negativos; `NumeroSecuenciaXML` = Prefijo + Consecutivo sin espacios ni guiones |
| CUNE (anexo 8.1) | `SHA-384(Numero + FechaGen + HoraGen + DevengadosTotal + DeduccionesTotal + ComprobanteTotal + NitEmpleador sin DV + DocumentoTrabajador + TipoXML + PIN + Ambiente)`, hexadecimal minúscula de 96; en nota **Eliminar** los tres totales van «0.00» y el documento del trabajador «0». `HoraGen` con zona (`-05:00`). El ejemplo del anexo (págs. 245-249) **no reproduce su propio hash**: el caso dorado es un documento **aceptado en habilitación** (`XmlDocumentKey` = CUNE) que aún **falta** obtener |
| SoftwareSC | `SHA-384(SoftwareID + PIN + Numero)` |
| Firma | XAdES-EPES *enveloped* en `ext:UBLExtensions/ext:UBLExtension/ext:ExtensionContent/ds:Signature`; C14N inclusiva `http://www.w3.org/TR/2001/REC-xml-c14n-20010315`; `rsa-sha256`; tres `ds:Reference` (documento con `enveloped-signature`, `KeyInfo`, `SignedProperties`); `SigningTime` con `-05:00`; `SignaturePolicyIdentifier` = `https://facturaelectronica.dian.gov.co/politicadefirma/v2/politicadefirmav2.pdf`, descripción «Política de firma para nóminas electrónicas de la República de Colombia», `SigPolicyHash` SHA-256 base64 **`dMoMvtcG5aIzgYo0tIsSQeVJBDnUnfSOfBpxXrmor0Y=`** (recalculado sobre el PDF de 1.272.898 bytes); `SignerRole` **«supplier»** en modo software propio (firma el empleador con su certificado) y **«third party»** en modo PT. Implementación propia sobre `System.Security.Cryptography.Xml.SignedXml` (`ds:Object` con `xades:QualifyingProperties` a mano, `GetIdElement` sobrescrito) o `FirmaXadesNetCore` si se acepta LGPL |
| Modo por cooperativa | **SoftwarePropio** (arranque): la cooperativa registra el ERP como su software (fabricante Ingenia365), obtiene `SoftwareID`, fija PIN, pasa su set (4 nóminas + 4 notas), aporta **su** certificado; `ProveedorXML` lleva NIT/DV de la cooperativa. **ProveedorTecnologico** (después, por configuración `Modos:ProveedorTecnologicoHabilitado`): certificado y software de Ingenia365, `ProveedorXML` con NIT de Ingenia365, `SignerRole` third party. Exige que Ingenia365 sea PT de factura electrónica, patrimonio ≥ 20.000 UVT con PPyE ≥ 10.000, ISO 27001 o compromiso a 18 meses, visita DIAN (art. 1.5.1.8.1.1). El contrato ERP↔servicio **no cambia** entre modos: sólo `habilitacion.modo` |
| Nombres de archivo | `nie` (102) / `niae` (103) + NIT 10 dígitos con ceros + `aa` + 8 hex del consecutivo; ZIP `z…` con un solo documento (anexo 3.3-3.5). Los calcula el servicio y los devuelve; el ERP los guarda con el documento |
| QR | lo arma el **ERP** en la representación gráfica: `https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=<CUNE>` (`catalogo-vpfe-hab` en pruebas) |
| Códigos de regla | prefijo `NIE`; el diccionario (`GET /reglas`) se carga de la tabla de reglas de validación del anexo v1.0 y se corrige con lo observado en habilitación. Único código verificado en la investigación: `NIE022` (literal de `Version`). **No se inventan códigos**: una regla que no esté en el diccionario se muestra con el texto de la DIAN y `traduccion = null` |

## 7. Configuración del servicio (no secreta, en el overlay de Kustomize)

`Dian:UrlHabilitacion`, `Dian:UrlProduccion`, `Dian:TimeoutSegundos` (60), `Secretos:Raiz`
(`/secrets/tenants`), `Secretos:Plataforma` (`/secrets/plataforma`),
`Modos:ProveedorTecnologicoHabilitado` (`false`), `Auth:Jwks` (URL interna de la API),
`Auth:Audience` (`nomina-electronica`), `Auth:VidaMaximaMinutos` (5). Sin valor por defecto para
las URL de la DIAN ni para `Secretos:Raiz`: el proceso no arranca si faltan (mismo criterio que
`RelyingPartyId`). Imagen construida por el mismo pipeline (`construir-imagenes.ps1`), réplicas 1
(no hay estado que coordinar; escalar es sólo poner más), recursos pequeños.

## 8. Lo que el ERP guarda de cada llamada (en la base de la cooperativa)

`PAY_ElectronicPayrollAttempts`: documento, operación, `X-Idempotency-Key`, hash SHA-256 del ZIP
enviado, `resultado`, `statusCode`, `isValid`, `errores` (JSON), `xmlDocumentKey`, `zipKey`,
`duracionMs`, `respondidoEn`, `traceId`, quién y cuándo. Los blobs (XML sin firmar, XML firmado,
ZIP, `ApplicationResponse`, PDF) van como adjuntos cifrados del documento (`IngenIA365ERP.Storage`,
`ownerEntityType = "ElectronicPayrollDocument"`), retención ≥ 5 años (ET art. 632 vía art.
1.5.3.8.2). Una prueba de Application comprueba que un `resultado = Aceptado` sin `cune` o sin
`applicationResponse` **no** deja el documento en `Accepted`.

## 9. Pendientes del dueño que este contrato no resuelve

Cuenta de cada cooperativa en `catalogo-vpfe-hab` con `SoftwareID`, PIN y `TestSetId`;
certificado `.p12` de cada cooperativa y su ECD; un documento aceptado en habilitación como caso
dorado de CUNE/firma; observar los `StatusCode` distintos de `00`/`99`; namespace/NetworkPolicy del
servicio y **segundo revisor** de la primera transmisión en producción (Principio XII). Ver
research «Lo que falta del dueño» 2-5 y 10.
