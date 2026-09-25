# Contrato: documentos electrónicos DIAN (puerto del canal, modelo canónico y ciclo fiscal)

**Feature**: 012 | **Date**: 2026-09-24 | **Entrega**: I4 (eventos RADIAN emitidos: I5; nota débito
electrónica: I6) | **Base**: investigación `dian` (decisiones 1 a 20), decisiones transversales T16,
T40, T41 y T42 (`decisiones-transversales.md`, en la carpeta de la feature) | **Hermanos**: `api.md` (rutas y cuerpos), `data-model.md` (tablas `COR_Electronic*`
y `COR_Dian*`), `plantillas.md`, y el contrato de la 010
`specs/010-nomina-prestaciones-pila-dian/contracts/servicio-nomina-electronica.md`

Requisitos que cubre: FR-011, FR-012, FR-016, FR-038, FR-050, FR-058, FR-063 a FR-068, FR-083,
FR-095; US8 (escenarios 1 a 9); SC-002, SC-004, SC-013, SC-015, SC-019.

## 0. Qué es y qué no es

La facturación electrónica es un **módulo de plataforma**, `ElectronicInvoicing` (T3): rutas
`/api/electronic-invoicing/*`, permisos `ElectronicInvoicing.*`, tablas `COR_` en la base de cada
cooperativa (T2). Hoy le sirve a Inventario (ventas y documento soporte); mañana puede servirle a
Cartera o a servicios sin leer tablas `INV_`, porque el módulo fuente le entrega sus datos por un
puerto (§4.1).

Reparto de responsabilidades:

- **El ERP numera.** El número fiscal se asigna al confirmar el documento comercial, dentro de su
  transacción y dentro de una resolución vigente (§9). Así el documento tiene número aunque el canal
  no conteste, y eso es lo que permite reintentar sin duplicar y facturar en contingencia.
- **El canal emite.** En modo proveedor tecnológico, el proveedor arma su XML, calcula el CUFE con la
  clave técnica, firma, transmite a la DIAN y devuelve artefactos. En modo software propio, el mismo
  papel lo cumple el servicio central sin estado de la 010 (§15). El ERP **no habla con la DIAN** en
  modo proveedor.
- **El ERP guarda todo lo de la cooperativa** en su base: configuración, resoluciones, documentos,
  versiones, transmisiones y contingencias (Principio IV). Los archivos van por `IBlobStore` como
  adjuntos del módulo que no se borran (§12).
- **El ERP representa y entrega.** La representación gráfica es siempre la del ERP, sea cual sea el
  canal (§13).

Lo que **no** es: Ingenia365 como proveedor tecnológico (C6); POS fuera de línea; los eventos RADIAN
distintos de 030 y 032; la recepción de facturas de proveedores (el XML del proveedor se lee para
prellenar y no se guarda, E10); la nómina electrónica (N3 de la 010, que comparte el servicio central
pero no estas tablas).

> **Diferencia con la 010 que nadie debe copiar al revés.** En la nómina electrónica un documento
> rechazado genera un documento nuevo con número nuevo. Aquí **un número rechazado no se consume**:
> se reenvía con el mismo número (caso a) o lo toma el documento de reemplazo (caso b). Por eso la
> identidad del documento electrónico es el número, y las versiones van en otra tabla (§8, T40).

## 1. Norma y fuentes

Toda cifra legal (plazos, porcentajes de aviso, esperas del POS) es un parámetro con vigencia y norma
(§10.3). Ningún valor legal está escrito en el código: `ElComercioNoTieneValoresLegalesFijos` cubre
`Domain/ElectronicInvoicing` y `Application/ElectronicInvoicing`.

| Tema | Fuente |
|---|---|
| Resoluciones 165/2023 (factura, notas, documento equivalente) y 167/2021 (documento soporte), **compiladas en la Resolución Única 000227 del 23-09-2025, Título 5, anexo T5.1** | https://www.dian.gov.co/normatividad/Paginas/Resolucion-000227-del-23092025.aspx |
| Resolución 165/2023 en el normograma (cita el art. 616-1 ET sobre contingencias) | https://normograma.dian.gov.co/dian/compilacion/docs/resolucion_dian_0165_2023.htm |
| Resolución 167/2021, documento soporte: por operación o acumulado semanal (art. 2, art. 10 par. 2), numeración autorizada (art. 4.5), nota de ajuste con referencia al CUDS (art. 5), contingencias con 48 h desde el día siguiente (arts. 11-12), rechazado se corrige hasta validar (art. 13) | https://normograma.dian.gov.co/dian//compilacion/docs/resolucion_dian_0167_2021.htm |
| Anexo técnico de la factura electrónica de venta, versión 1.9 | https://www.dian.gov.co/impuestos/factura-electronica/Documents/Anexo-Tecnico-Factura-Electronica-de-Venta-vr-1-9.pdf |
| Anexo técnico del documento equivalente electrónico, versión 1.0 | https://www.dian.gov.co/impuestos/factura-electronica/Documents/Anexo-Tecnico-Documento-Equivalente-Electronico-V1-0-final.pdf |
| Tabla de códigos de tipo de documento del anexo 1.9 (01, 02, 03, 04, 05, 91, 92, 95, 96) | https://felcowiki.thefactoryhka.com.co/index.php/Tablas_de_c%C3%B3digos_de_propiedades_para_emisi%C3%B3n_de_documentos_V1.9-Cambios_Integraci%C3%B3n_Anexo_V_1.9 |
| Contingencias tipo 03 y 04 (numeración, CUFE, 48 h) | https://felcowiki.thefactoryhka.com.co/index.php/Contingencia_de_factura_electr%C3%B3nica_un_aliado_para_su_negocio y https://micrositios.dian.gov.co/sistema-de-facturacion-electronica/inconvenientes-tecnologicos/ (esta última todavía dice «30 días» para el tipo 03: desactualizada o por cotejar) |
| Servicio web de la DIAN (`WcfDianCustomerServices`, SOAP 1.2 con WS-Security): `SendBillSync`, `SendBillAsync`, `SendTestSetAsync`, `GetStatus`, `GetStatusZip`, `GetNumberingRange`, `SendEventUpdateStatus`, `GetStatusEvent`, `GetAcquirer`, `GetXmlByDocumentKey`… | https://github.com/movaltech/cofacture-php |
| Documento equivalente POS: se valida antes de entregarse; sin tope de 5 UVT (ése es del tiquete POS tradicional, par. 2 art. 616-1 ET) | https://sovos.com/es/iva/como-emitir-documentos-equivalentes-electronicos-en-colombia/ |
| El documento equivalente identificado da derecho a costos e impuestos descontables | https://incp.org.co/publicaciones/boletin-virtual/contenido-de-interes-profesional-boletin-virtual/tributario/2024/12/el-documento-equivalente-electronico-otorga-derecho-a-costos-deducciones-e-impuestos-descontables/ |
| Datos de la caja, del cajero y del software en el DEE; cambio a factura si el comprador la pide | https://actualicese.com/elementos-clave-que-debes-conocer-del-nuevo-pos-electronico/ |
| Resolución 202/2025: consulta de autollenado del adquirente, datos mínimos, «consumidor final» | https://incp.org.co/sin-categoria/2025/04/nuevas-herramientas-y-mayor-flexibilidad-para-la-facturacion-electronica/ |
| RADIAN, Resolución 85/2022: eventos 030 a 034 como `ApplicationResponse` firmados por el adquirente | https://www.dian.gov.co/normatividad/Normatividad/Resoluci%C3%B3n%20000085%20de%2008-04-2022.pdf |
| The Factory HKA: API y DLL (`Enviar`, `EstadoDocumento`, `DescargarXml`, `GenerarContenedor`, códigos 200/201/208, numeración manual, DEE) | https://felcowiki.thefactoryhka.com.co/index.php/Manual_de_usuario_API_IntTfhkaFel21 y https://felcowiki.thefactoryhka.com.co/index.php/Manual_DLL_hkafact21_-_Emisi%C3%B3n_V4 |
| Dataico: REST JSON, el número va en el cuerpo, `dian_status`, `cufe`, `qrcode`; DS, eventos y POS | https://portaldelcliente.dataico.com/es/knowledge/documentaci%C3%B3n-t%C3%A9cnica-de-la-api-de-dataico-factura-electr%C3%B3nica |
| Factus: `reference_code` como clave de idempotencia; el número lo asigna el proveedor desde su rango | https://factusapi-v2.halltec.co/facturas/descripcion-de-campos/ |
| Facturatech: SOAP ASMX (`uploadInvoiceFile`, `documentStatusFile`, `downloadXMLFile`…) | https://webservice.facturatech.co/v2/BETA/WSV2DEMO.asmx |
| Siigo: API de su propio ERP (tercero y comprobante deben existir en Siigo) | https://developers.siigo.com/docs/siigoapi/invoice/1-create-invoice/ |

**Por cotejar** (con el contador y con el proveedor elegido, antes del ensayo; ver §19): el
tratamiento de contingencia del DEE POS; los códigos 20 y 94 del DEE y su nota de ajuste; la
generación semanal del documento soporte; la numeración de las notas de ajuste del DS; el formato de
asunto y ZIP del correo; el contenido exacto del QR del anexo 1.9; y que el DEE pueda emitirse por
proveedor tecnológico (una fuente secundaria, Actualícese, sugiere que sólo con software propio;
Sovos y la oferta de los proveedores lo contradicen).

## 2. Piezas y dónde viven

| Pieza | Ubicación | Qué hace |
|---|---|---|
| `ICanalDeEmisionElectronica`, `ICanalesDeEmision`, `ICredencialesDeCanal`, `ResultadoDeCanal`, `CapacidadesDelCanal` | `Application/ElectronicInvoicing/Channels` | El puerto y su registro (§3) |
| `DocumentoElectronicoCanonico` (v1), `ConstructorDelCanonico`, `IFuenteDeDocumentoElectronico` | `Application/ElectronicInvoicing/Canonical` | Modelo neutral y su único constructor (§4) |
| `FuenteDeEmisionDeInventario` | `Application/Inventory/Integration` | Inventario implementa la fuente (§4.1) |
| `CatalogoDian` + JSON embebidos versionados | `Application/ElectronicInvoicing/Catalogs` | Tipos de documento, operación, identificación, responsabilidades, tributos, unidades Rec. 20, medios y formas de pago, conceptos de corrección, tipos de caja DEE (§4.4) |
| `NumeradorFiscal` | `Application/ElectronicInvoicing/Numeracion` | Único escritor de `LastIssuedNumber` (§9) |
| `GuardiaDeEmisionFiscal`, `IRepresentacionGraficaRenderer` | `Application/ElectronicInvoicing` | Decisión única de emitir (§10.4) y puerto del PDF (§13) |
| `TransicionesDelDocumentoElectronico`, `ReglaDeCorreccionFiscal`, `PlazoDeContingencia` | `Domain/ElectronicInvoicing` (puros) | Máquina de estados (§5), huella económica (§8.2), plazo (§7.3) |
| Comandos y consultas | `Application/ElectronicInvoicing/{Documents,Contingencies,Settings,Numeracion}` | §17 |
| Proyecto `IngenIA365ERP.ElectronicInvoicing` | `src/Infrastructure` | `Channels/Simulado/CanalSimulado`, `Channels/<Proveedor>/…`, `Channels/ServicioCentral/CanalServicioCentral` (cuando exista), `Credentials/CredencialesEnArchivo`, `Processor/ProcesadorDeDocumentosElectronicos`, `Circuit/CircuitoDeCanal` |
| `RepresentacionGraficaReport` | `API/Reports` | QuestPDF + QRCoder (se agrega a `IngenIA365ERP.API.csproj`) |

`ElProveedorTecnologicoSoloLoConoceSuAdaptador` impide que Domain y Application referencien el
proyecto `IngenIA365ERP.ElectronicInvoicing` o un SDK de proveedor (molde de
`LaCriptografiaDeWebAuthnNoSeFiltra`); la API lo referencia sólo como raíz de composición: únicamente
`Program.cs` llama su extensión `AddElectronicInvoicing()`, igual que con los demás trabajos de fondo
(T47), y ningún otro archivo de la API usa tipos de `IngenIA365ERP.ElectronicInvoicing.*`.

## 3. El puerto del canal

### 3.1 Interfaz

```csharp
namespace IngenIA365ERP.Application.ElectronicInvoicing.Channels;

public interface ICanalDeEmisionElectronica
{
    /// Código estable del adaptador; es el que se sella en cada documento ("SIMULADO", "<PROVEEDOR>", "SERVICIO-CENTRAL").
    string ChannelCode { get; }
    CapacidadesDelCanal Capacidades { get; }

    /// Emisión normal y transmisión diferida de las contingencias 03 y 04: el canónico dice cuál es.
    Task<ResultadoDeCanal> EmitirAsync(DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, CancellationToken ct);

    /// Por número y ambiente, o por código único o referencia externa (trackId).
    Task<ResultadoDeCanal> ConsultarEstadoAsync(ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct);

    /// Eventos RADIAN 030 y 032 (I5).
    Task<ResultadoDeCanal> EmitirEventoAsync(EventoRadianCanonico evento, ContextoDeCanal contexto, CancellationToken ct);

    /// XmlFirmado | ApplicationResponse | AttachedDocument. Idempotente.
    Task<ResultadoDeCanal> DescargarArtefactoAsync(ReferenciaDeEnvio referencia, TipoDeArtefacto tipo, ContextoDeCanal contexto, CancellationToken ct);

    /// Credenciales y salida. Al guardar la configuración (§10.2) y en el sondeo del circuito (§7.2).
    Task<ResultadoDeCanal> ProbarAsync(ContextoDeCanal contexto, CancellationToken ct);

    /// Opcionales según Capacidades: GetNumberingRange y GetAcquirer (Res. 202/2025).
    Task<ResultadoDeCanal> ConsultarRangosAsync(ContextoDeCanal contexto, CancellationToken ct);
    Task<ResultadoDeCanal> ConsultarAdquirenteAsync(string tipoDeIdentificacionDian, string numero, ContextoDeCanal contexto, CancellationToken ct);
}

public interface ICanalesDeEmision
{
    /// Por el canal SELLADO en el documento, nunca por la configuración de hoy.
    ICanalDeEmisionElectronica Resolver(string channelCode);
}
```

`ContextoDeCanal` lleva: el `PublicId` de la cooperativa resuelta, NIT y DV del emisor, `Mode`,
`Environment`, `SoftwareId` y `TestSetId` (modo propio), la clave técnica de la resolución cuando la
operación la necesita (sólo en modo propio, §15), la clave de idempotencia (§6.2) y las credenciales ya
resueltas por `ICredencialesDeCanal` (§11). Nunca lleva un valor leído de la base que sirva para
armar la ruta de las credenciales.

`ReferenciaDeEnvio`: número completo (prefijo + consecutivo), ambiente, código único si se conoce y
referencia externa del canal si la hubo.

### 3.2 Capacidades

```csharp
public sealed record CapacidadesDelCanal(
    IReadOnlySet<ElectronicDocumentKind> Tipos,      // Invoice, CreditNote, DebitNote, PosEquivalent, PosAdjustmentNote, SupportDocument, SupportDocumentAdjustmentNote, RadianEvent030, RadianEvent032
    bool AceptaNumeroDelErp,                          // obligatorio: un canal sin esto no se admite
    bool ContingenciaDelFacturador,                   // transmite tipo 03 referido al número de papel
    bool InformaContingenciaDian,                     // distingue "DIAN no disponible" de un error
    bool EsAsincrono,                                 // la respuesta definitiva puede llegar por consulta
    bool DevuelvePdf, bool PuedeEnviarCorreo,
    bool ConsultaRangos, bool ConsultaAdquirente,
    bool AdmiteClaveDeIdempotencia);                  // reference_code o equivalente
```

`GuardiaDeEmisionFiscal` responde `Blocked` para un tipo que el canal sellado no declara (§10.4), y
`ConfigureEmissionCommand` rechaza un canal sin `AceptaNumeroDelErp`.

### 3.3 Resultado uniforme

```csharp
public sealed record ResultadoDeCanal(
    ChannelOutcome Outcome,                 // Validated | ValidatedWithNotices | Rejected | InProcess | NotFound | DianUnavailable | ChannelUnavailable | InvalidData
    string? UniqueCode, string? UniqueCodeKind,   // CUFE | CUDE | CUDS
    string? QrContent, DateTimeOffset? ValidatedAt,
    string? DianDocumentTypeCode,           // el que el canal realmente emitió (04 en contingencia de la DIAN)
    IReadOnlyList<MensajeDelCanal> Mensajes,      // { Regla, Tipo: Rechazo|Notificacion, Texto, Traduccion? }
    IReadOnlyList<ArtefactoDelCanal> Artefactos,  // { Tipo, NombreDeArchivo, ContentType, Bytes }
    string? ExternalReference, string? ProviderCode, string? DianStatusCode,
    long DurationMs);
```

Reglas para todo adaptador (las verifica su propia batería de pruebas contra `CanalSimulado`):

1. **Nunca lanza por un rechazo de negocio.** Un rechazo es `Rejected` con sus mensajes; un error de
   validación del propio proveedor que nunca llegó a la DIAN es `InvalidData` (se guarda como rechazo
   con `RejectedBy = Channel`, §5).
2. **Ninguna excepción de transporte sube.** Si la petición **no salió** (DNS, conexión rehusada,
   credencial inválida antes de enviar) es `ChannelUnavailable`. Si **pudo haber llegado** (tiempo
   agotado después de enviar, 5xx, conexión cortada con el cuerpo ya enviado) es `InProcess`.
3. **«Ya existe» o «ya procesado» no es un error**: el adaptador lo traduce a una consulta de estado y
   devuelve lo que ésta diga.
4. **Traduce los códigos del proveedor** (p. ej. TFHKA 201 → `InProcess`, 208 → `DianUnavailable`) a
   `ChannelOutcome`, y conserva el crudo en `ProviderCode`/`DianStatusCode`.
5. **`Validated` exige** código único **y** respuesta de validación (`ApplicationResponse` o su
   equivalente del canal). Si falta cualquiera, el resultado es `InProcess` y se consulta.
6. **Las notificaciones solas no rechazan**: `ValidatedWithNotices`.
7. **Sin reintentos HTTP automáticos en `EmitirAsync`**. Sólo `ConsultarEstadoAsync` y
   `DescargarArtefactoAsync`, que son idempotentes, admiten la resiliencia de
   `Microsoft.Extensions.Http.Resilience`. Los reintentos de la emisión los decide la máquina de
   estados (§6).
8. Tiempo de conexión corto y tiempo total configurables en `appsettings`, sección
   `ElectronicInvoicing:Channels:{channelCode}` (valores técnicos, no parámetros de la cooperativa).

### 3.4 Canales

| `ChannelCode` | Entrega | Qué es |
|---|---|---|
| `SIMULADO` | I4 (primero) | Obligatorio para CI, e2e y el ensayo de COOFLOPAL. **Sólo admite `Environment = Testing`**: `ConfigureEmissionCommand` rechaza configurarlo en producción y la guardia responde `Blocked` si lo encontrara. Decide el resultado por el último dígito de la identificación de la contraparte: 1 `Rejected` (con una regla de ejemplo), 2 `ValidatedWithNotices`, 3 `InProcess` y luego `NotFound` en la primera consulta, 4 `DianUnavailable`, 5 `ChannelUnavailable`, cualquier otro `Validated`. Produce código único, QR y artefactos de ejemplo marcados «SIMULADO». |
| `<PROVEEDOR>` | I4 | El adaptador del proveedor que contrate la cooperativa (§16); su código lo fija su clase. Hasta el contrato no hay adaptador real. |
| `SERVICIO-CENTRAL` | cuando exista el servicio de la 010 | Modo software propio (§15). |

## 4. Modelo canónico `DocumentoElectronicoCanonico` v1

Un solo modelo, independiente del proveedor, con los códigos DIAN ya resueltos. Lo arma **un solo**
`ConstructorDelCanonico`; cada adaptador lo traduce a su formato (JSON del proveedor, SOAP, o UBL 2.1
en modo propio). Es la evidencia de qué se mandó: se serializa en JSON canónico (claves en orden,
decimales con su escala fija y punto decimal, fechas ISO con `-05:00`) y su SHA-256 queda en la
versión del documento (§8.1) y en cada transmisión.

### 4.1 De dónde salen los datos

```csharp
public interface IFuenteDeDocumentoElectronico
{
    string SourceModule { get; }   // "INV"
    // Entrada neutral del documento comercial ya confirmado: líneas, foto fiscal de la contraparte,
    // foto tributaria (INV_DocumentTaxLines), pagos, referencias, bloque POS o DS.
    // (Nombre del método y del tipo de entrada: de trabajo; los fija el plan.)
    Task<Result<EntradaDeDocumentoElectronico>> LeerAsync(Guid sourceDocumentPublicId, CancellationToken ct);
}
```

La plataforma nunca lee tablas `INV_`: Inventario implementa la fuente con
`FuenteDeEmisionDeInventario`. El constructor completa con lo que es de plataforma: el emisor (datos de
la cooperativa y sus parámetros `TAX` vigentes a la fecha), la resolución, el ambiente y los códigos
de `CatalogoDian`. La contraparte sale **siempre** de la copia inmutable vigente
(`INV_DocumentPartySnapshots`, la de mayor `Version`, FR-011, T52), nunca del maestro de hoy.

El constructor es determinista a partir de datos inmutables (documento confirmado, sus fotos,
catálogos versionados por fecha). Eso permite subir el artefacto después del commit (§6.1): si hubiera
que reconstruirlo y su SHA-256 no coincidiera con el de la versión, no se emite, se registra en
Critical y sale la alerta `Dian.DocumentoSinValidar` con la causa.

### 4.2 Forma

```jsonc
{
  "schemaVersion": 1,
  "kind": "Invoice",                        // ElectronicDocumentKind
  "dianDocumentTypeCode": "01",             // CatalogoDian por kind y contingencia (tabla 4.3)
  "operationTypeCode": "10",                // tipo de operación del anexo; por kind
  "environment": "Testing",                 // DianEnvironment
  "number": { "prefix": "SETP", "consecutive": 990000123, "full": "SETP990000123" },
  "resolution": {                           // null en notas (no tienen resolución)
    "number": "18760000001", "date": "2026-10-01",
    "rangeFrom": 990000000, "rangeTo": 995000000,
    "validFrom": "2026-10-01", "validTo": "2027-10-01"
  },
  "issuedAt": "2026-12-05T10:14:22-05:00",  // fecha y hora locales de la operación
  "dueDate": null,                          // sólo con forma de pago crédito
  "currency": "COP", "exchangeRate": 1,
  "paymentForm": "Cash",                    // Cash | Credit: se deriva de la clase de los medios
  "payments": [
    { "dianPaymentMeansCode": "10", "amount": 26000.00, "reference": null },
    { "dianPaymentMeansCode": "48", "amount": 20000.00, "reference": "054321" }
  ],
  "issuer": {
    "taxId": "890300001", "checkDigit": "3", "idTypeCode": "31", "personTypeCode": "1",
    "name": "COOPERATIVA …", "responsibilities": ["O-13"], "taxSchemeCode": "01",
    "address": { "line": "Cra 1 # 2-3", "cityDaneCode": "76001", "countryCode": "CO" },
    "ciiu": "4711", "email": "facturacion@…"
  },
  "counterparty": {                         // role: Buyer (ventas) | Supplier (documento soporte)
    "role": "Buyer", "isFinalConsumer": false,
    "taxId": "16000111", "checkDigit": null, "idTypeCode": "13", "personTypeCode": "2",
    "name": "…", "responsibilities": ["R-99-PN"], "taxSchemeCode": "ZZ",
    "address": { "line": "…", "cityDaneCode": "76001", "countryCode": "CO" },
    "receptionEmail": "…",
    "partySnapshotVersion": 1               // qué versión de la copia fiscal se usó
  },
  "lines": [
    {
      "lineNumber": 1, "productCode": "ARZ-001", "description": "ARROZ 500 G",
      "quantity": 2.0000, "unitCode": "94", "unitPrice": 2100.000000, "lineExtension": 4200.00,
      "allowances": [ { "reasonCode": null, "percent": 0.050000, "base": 4200.00, "amount": 210.00 } ],
      "taxes": [ { "dianTaxCode": "01", "rate": 0.190000, "amountPerUnit": null, "taxableUnits": null, "base": 3990.00, "amount": 758.10 } ]
    }
  ],
  "withholdings": [                         // informativas: las que practica el comprador agente retenedor (T26)
    { "dianTaxCode": "06", "rate": 0.025000, "base": 3990.00, "amount": 99.75 }
  ],
  "totals": {
    "lineExtension": 4200.00, "allowances": 210.00, "taxExclusive": 3990.00,
    "taxes": 758.10, "taxInclusive": 4748.10, "charges": 0.00, "rounding": 0.00,
    "payable": 4748.10, "withholdings": 99.75, "amountDue": 4648.35
  },
  "references": {
    "corrected": null,                      // notas: { number, uniqueCode, issueDate, correctionConceptCode }
    "order": null, "despatches": []
  },
  "contingency": null,                      // { type: "Issuer03", paperNumber, paperIssuedAt } en la transmisión 03
  "pos": null,                              // DEE: { cashRegisterPlate, location, cashierName, cashRegisterTypeCode, software: { name, manufacturerTaxId, manufacturerName } }
  "supportDocument": null,                  // DS: { generation: "PerOperation" | "Weekly", periodFrom?, periodTo? }
  "notes": [],
  "source": { "module": "INV", "documentPublicId": "…", "documentClass": "SalesInvoice", "documentNumber": "SETP990000123" }
}
```

Reglas del constructor:

- Montos (18,2), cantidades (18,4), precio unitario (18,6), tarifas como **fracción** (9,6) (T19).
  Impuestos por línea redondeados por línea; el total es la suma exacta de las líneas (FR-017).
- `payable` es `Total` y `amountDue` es `AmountDue` del documento (T26): la retención que practica el
  comprador viaja como retención, nunca como medio de pago.
- `paymentForm = Credit` si algún pago es de clase `AssociateCredit` o `CustomerCredit`; entonces
  `dueDate` es obligatoria.
- Consumidor final: la persona genérica sembrada (`ConsumidorFinalSeeder`), con la identificación que
  indique `CatalogoDian` (Res. 202/2025).
- El QR no lo arma el constructor en modo proveedor: se usa el `QrContent` que devuelve el canal tal
  cual (§13).

### 4.3 Tipos y códigos

| `ElectronicDocumentKind` | Clase(s) de `DocumentClass` | Código DIAN | Código único | Numeración | Entrega |
|---|---|---|---|---|---|
| `Invoice` | `SalesInvoice`, `SalesInvoiceFromShipments` | 01; 04 si la DIAN no está (§7.1); 03 al transmitir la de papel (§7.2) | CUFE | resolución `Invoice` (o `Contingency` que la respalda) | I4 |
| `CreditNote` | `CreditNote` | 91 | CUDE | consecutivo propio (`INV_DocumentSequences`) | I4 |
| `DebitNote` | `DebitNote` | 92 | CUDE | consecutivo propio | I6 |
| `PosEquivalent` | `PosEquivalentDocument` | 20 (**por cotejar**, anexo DEE 1.0) | CUDE | resolución `PosEquivalent` | I4 |
| `PosAdjustmentNote` | `PosAdjustmentNote` | 94 (**por cotejar**) | CUDE | consecutivo propio | I4 |
| `SupportDocument` | `SupportDocument` | 05 | CUDS | resolución `SupportDocument` | I4 |
| `SupportDocumentAdjustmentNote` | `SupportDocumentAdjustmentNote` | 95 | CUDS | consecutivo propio (la Res. 167 art. 5 habla de numeración: **por cotejar**) | I4 |
| `RadianEvent030` · `RadianEvent032` | registro de factura del proveedor (`INV_SupplierInvoiceEvents`) | 96 (`ApplicationResponse`) | CUDE | propia del evento (la define I5) | I5 |

Los códigos no están en el código: los da `CatalogoDian` desde su JSON vigente a la fecha.

### 4.4 Correspondencias y datos faltantes

`CatalogoDian` traduce al armar el canónico:

- tipo de identificación heredado de `COR_People.IdType` → código DIAN (por tabla, como hace la
  dispersión con «C» → «CC»);
- marcas tributarias de la persona (`IsLargeContributor`, `IsSelfWithholder`,
  `IsVatWithholdingAgent`, `IsSimpleTaxRegime`, `IsVatResponsible`…, T24) → responsabilidades
  (O-13, O-15, O-23, O-47, R-99-PN) y tributo (01 / ZZ);
- `INV_UnitsOfMeasure.DianUnitCode` → unidad Rec. 20;
- `COR_PaymentMeans.DianPaymentMeansCode` → medio de pago; la clase → forma de pago;
- `COR_TaxDefinitions.DianTaxCode` → tributo de cada impuesto y retención;
- concepto de corrección de la nota (lo elige quien la emite, de la tabla vigente).

Si falta un dato, **la confirmación se bloquea** (Principio VIII) con
`ElectronicInvoicing.Document.MissingData` (422) y `data.missing[] { field, where, permission }`,
p. ej. «La unidad KILO no tiene código DIAN: complételo en Inventario → Unidades
(`Inventory.Catalog.Manage`)». No se descubre en un rechazo de la DIAN.

## 5. Máquina de estados

`COR_ElectronicDocuments.Status` (`ElectronicDocumentStatus`). La máquina es pura
(`Domain/ElectronicInvoicing/TransicionesDelDocumentoElectronico`) y tiene una prueba por cada
transición prohibida.

| Estado | Valor | Significado | ¿Final? |
|---|---|---|---|
| `Pending` | 0 | Numerado; no hay constancia de que el canal lo haya recibido. **Se puede reenviar.** | no |
| `Sent` | 1 | El canal lo recibió, o pudo recibirlo; falta la respuesta definitiva. **Sólo se consulta.** | no |
| `Validated` | 2 | Validado por la DIAN, con código único y respuesta de validación. | sí |
| `ValidatedWithNotices` | 3 | Validado con notificaciones. | sí |
| `Rejected` | 4 | Rechazo con motivos; `RejectedBy = Dian` o `Channel`. | hasta aplicar a, b o c |
| `IssuerContingency` | 5 | Contingencia 03: documento de papel con numeración de contingencia, pendiente de transmitir. | no |
| `DianContingency` | 6 | Contingencia 04: entregado sin validar, pendiente de transmitir. | no |
| `CancelledWithoutReplacement` | 7 | Caso c: el número queda ligado a su documento anulado, con motivo y responsable. | sí |

`ContingencyType` (`Issuer03` / `Dian04`) se conserva en el documento aunque después se valide: el
historial muestra que fue de contingencia.

### 5.1 Transiciones

| Desde | Evento | Hacia | Condición |
|---|---|---|---|
| — | confirmar el documento comercial | `Pending` | operación normal |
| — | confirmar el documento comercial | `IssuerContingency` | hay contingencia 03 abierta para el canal sellado y el tipo se numeró con la resolución de contingencia (§7.2) |
| `Pending` | `Emit` → `Validated` / `ValidatedWithNotices` | `Validated` / `ValidatedWithNotices` | código único y respuesta de validación presentes |
| `Pending` | `Emit` → `Rejected` / `InvalidData` | `Rejected` | `RejectedBy = Dian` / `Channel` |
| `Pending` | `Emit` → `InProcess` | `Sent` | se consulta en la espera siguiente |
| `Pending` | `Emit` → `DianUnavailable` | `DianContingency` | el canal lo declara y entrega documento firmado y código único (§7.1) |
| `Pending` | `Emit` → `ChannelUnavailable` | `Pending` | espera siguiente; cuenta para el circuito (§7.2) |
| `Sent` | `QueryStatus` → final | `Validated` / `ValidatedWithNotices` / `Rejected` | |
| `Sent` | `QueryStatus` → `NotFound` | `Pending` | se reenvía **la misma versión con el mismo número** |
| `Sent` | `QueryStatus` → `InProcess` / `ChannelUnavailable` | `Sent` | espera siguiente |
| `IssuerContingency` | `TransmitContingency` | `Sent` / `Validated` / `ValidatedWithNotices` / `Rejected` | sólo con el evento 03 cerrado |
| `DianContingency` | `TransmitContingency` | `Sent` / `Validated` / `ValidatedWithNotices` / `Rejected` | |
| `Rejected` | caso a (`CorrectRejectedDocumentCommand`) | `Pending` | versión n+1, mismo número, mismo canal |
| `Rejected` | caso b (`ReplaceRejectedDocumentCommand`) | `Pending` | versión n+1 con el documento de reemplazo, mismo número |
| `Rejected` | caso c (`CancelRejectedDocumentCommand`) | `CancelledWithoutReplacement` | |

Invariantes:

- Nunca `Validated`/`ValidatedWithNotices` sin código único **y** sin respuesta de validación.
- Un `Sent` no se corrige ni se anula hasta tener respuesta (`ElectronicInvoicing.Document.AwaitingResponse`, 422; el 409 queda para `Concurrency.*`). Un `Pending` sin respuesta tampoco se corrige: los casos a, b y c sólo aplican a `Rejected`.
- Nada sale de un estado final salvo `Rejected` por a, b o c.
- **Nunca se renumera** un documento ya numerado: ni a contingencia 03 cuando su resultado es ambiguo,
  ni a otra resolución.
- La DIAN que valida tarde un `Pending` o `Sent` lo deja validado; queda en auditoría (edge case «La
  DIAN valida tarde»).

Los eventos RADIAN (I5) usan la misma tabla y la misma máquina simplificada: `Pending`, `Sent`,
`Validated`, `Rejected`.

### 5.2 Relación con el documento comercial

El documento comercial (`INV_Documents`) queda `Confirmed` al numerarse y no espera a la DIAN. Su
estado fiscal es el del documento electrónico. «Anular» un documento fiscal **validado** o expedido en
contingencia emite su documento de corrección (nota crédito total, nota de ajuste del DEE, nota de
ajuste del DS; FR-066), que es un documento electrónico nuevo con número, código único y fecha
propios, y sale por el canal vigente al confirmarse (FR-064). Un documento fiscal validado **nunca**
emite `DocumentoAnulado`.

## 6. Emisión, idempotencia y reintentos

### 6.1 Secuencia

1. **Confirmación** (transacción de `ConfirmInventoryDocumentCommand`, pasos 4, 8 y 10 de §1.3 de
   `decisiones-transversales.md`): `GuardiaDeEmisionFiscal.Evaluar` responde `Electronic(canal,
   resolución)`; `NumeradorFiscal` asigna el número con la fila de numeración bloqueada al final del
   orden canónico (T15, T16); se agrega la fila de `COR_ElectronicDocuments` en `Pending` (o
   `IssuerContingency`) con el canal sellado (`Mode`, `ChannelCode`, `SoftwareId`) y la versión 1 con
   el SHA-256 del canónico. **La llamada al canal nunca va dentro de esta transacción**: mantendría
   bloqueada la resolución mientras responde el proveedor y serializaría las 30 cajas (SC-019).
2. **Commit.**
3. **Emisión** (`EmitElectronicDocumentCommand`, fuera de la transacción de confirmación): toma el
   arrendamiento de la fila (§6.4), sube el canónico como adjunto si no está (§12), llama
   `EmitirAsync` y, en **otra** transacción, agrega la transmisión (inmutable), los artefactos que
   llegaron y el nuevo estado.
4. **POS**: la petición del cobro espera la emisión hasta `Dian.EsperaMaximaPosSegundos` (por defecto
   15, ámbito general o punto de venta). Si se cumple la espera sin respuesta definitiva, la venta ya
   está confirmada, la caja queda libre y el documento pasa a «pendientes de entrega» de la sesión
   (§13.2). No se imprime un comprobante provisional sin validar. Esa espera vencida **cuenta como
   falla** para `CircuitoDeCanal` (§7.2): si se repite, las ventas siguientes entran en contingencia
   03 y reciben su representación de papel en el acto.
5. **Procesador de fondo** (§6.4): reintentos, consultas y transmisiones de contingencia.

### 6.2 Clave de idempotencia

`{tenantPublicId:N}:{Environment}:{Prefix}{Consecutive}:v{VersionNumber}`, p. ej.
`3f2a…c91:Testing:SETP990000123:v1`. Viaja al canal si éste la admite
(`AdmiteClaveDeIdempotencia`, p. ej. `reference_code`); si no, el número mismo es la clave, porque el
proveedor y la DIAN rechazan un número repetido y el adaptador lo traduce a consulta (§3.3, regla 3).
Queda en cada `COR_ElectronicDocumentTransmissions.IdempotencyKey`. La unicidad
`(Environment, Prefix, Consecutive)` **sin filtro** de `COR_ElectronicDocuments` hace imposible un
segundo documento con el mismo número (el rechazado y su reemplazo comparten la fila).

Las operaciones de pantalla (reintentar, consultar, corregir, reemplazar, cancelar, abrir o cerrar
contingencia, configurar, registrar resolución) llevan además su cabecera `Idempotency-Key` de T13.

### 6.3 Regla del resultado ambiguo y esperas

- Ante `InProcess` (tiempo agotado, 5xx, corte después de enviar) **siempre se consulta primero**.
  Sólo si la consulta responde `NotFound` se reenvía la misma versión con el mismo número.
- Esperas entre intentos: 15 s, 1, 2, 5, 15, 30 y 60 minutos, y después cada hora. Son valores
  técnicos en `appsettings` (`ElectronicInvoicing:Retries`), no parámetros de la cooperativa (T10).
  En contingencia el tope es `TransmissionDeadline` (§7.3); fuera de ella no hay tope: se sigue
  intentando y alertando.
- `Rejected` no se reintenta solo: espera a una persona (casos a, b, c) y levanta
  `Dian.DocumentoRechazado`.
- Un documento `Pending` o `Sent` que pasa `Dian.MinutosAlertaSinValidar` (por defecto 10) sin
  validar levanta `Dian.DocumentoSinValidar` (una por documento, `DedupKey`).
- «Reintentar ahora» (`POST /documents/{id}/retry`, `ElectronicInvoicing.Documents.Transmit`) sólo
  adelanta `NextAttemptAt`; respeta la regla del ambiguo (un `Sent` consulta, no reenvía).

### 6.4 Procesador y arrendamientos

`ProcesadorDeDocumentosElectronicos` (`BackgroundService` registrado sólo en la API, T47) recorre
`ITenantDirectory.ListActiveAsync`, toma el arrendamiento `einvoicing.process` de cada cooperativa
(`COR_BackgroundLeases`, T10) y trabaja por `IEjecutorEnCooperativa` con el actor «Proceso de
integración», canal `Process`, origen `Tarea:einvoicing.process` (FR-083; T5, T6). Así guarda
artefactos y audita en la base y en la auditoría de **esa** cooperativa.

Elige documentos con `NextAttemptAt ≤ ahora` en `Pending`, `Sent`, `DianContingency`, e
`IssuerContingency` con su evento cerrado, en orden de consecutivo. Cada fila se toma con un
arrendamiento propio, porque el intento en línea del POS y el procesador pueden coincidir:

```sql
UPDATE COR_ElectronicDocuments
   SET LeaseUntil = @ahora + @ttl, LeaseOwner = @instancia
 WHERE Id = @id AND (LeaseUntil IS NULL OR LeaseUntil < @ahora) AND NextAttemptAt <= @ahora
```

Si no afecta filas, otro ya lo tiene. La exactitud no depende del arrendamiento: la unicidad del
número y la regla del ambiguo impiden el duplicado aunque el arrendamiento venza a mitad de una
llamada. `AttemptCount` y `NextAttemptAt` se actualizan en la misma transacción que agrega la
transmisión.

### 6.5 Transmisión (bitácora inmutable)

Cada llamada al canal agrega una fila `COR_ElectronicDocumentTransmissions` (`IHechoInmutable`):
documento y versión, `TransmissionOperation` (`Emit`, `TransmitContingency`, `QueryStatus`, `Event`,
`Download`), `ChannelCode`, solicitante (proceso o persona), inicio, fin, duración, clave de
idempotencia, SHA-256 de lo enviado, `ChannelOutcome`, códigos del proveedor y de la DIAN, mensajes
crudos y traducidos, referencia externa y la referencia al `ApplicationResponse` recibido. Nunca se
edita.

## 7. Contingencias

Dos, según quién falla (FR-067, supuesto 9). En las dos **la venta no se detiene**: la operación se
confirma y el documento queda en cola con aviso visible. `COR_DianContingencyEvents` guarda tipo,
canal, inicio, fin, quién la detectó (el proceso o una persona), motivo, estado y plazo; la evidencia
automática es la bitácora de transmisiones fallidas y de sondeos del evento, exportable desde su
pantalla (`/ventas/contingencias-dian`).

### 7.1 Tipo 04: falla la DIAN

- **Sólo la declara el canal**: `EmitirAsync` devuelve `DianUnavailable` junto con el documento
  generado y firmado, su código único y su XML. El ERP **no** la supone por un tiempo agotado propio:
  arriesgaría declarar 04 un documento que sí llegó.
- El documento conserva su **numeración normal** (la norma y los proveedores coinciden: el tipo 04 usa
  los consecutivos normales y lleva CUFE), queda `DianContingency` con `ContingencyType = Dian04` y
  `DianDocumentTypeCode` el que informó el canal. Se une al evento 04 abierto del canal o abre uno
  (alerta `Dian.ContingenciaAbierta`).
- Se entrega al comprador en el acto, con la leyenda de §13.1.
- El evento 04 se cierra solo cuando un documento de ese canal recibe una respuesta definitiva de la
  DIAN después del inicio del evento (`EndedAt` = ese instante), o a mano con motivo
  (`CloseContingencyCommand`, `ElectronicInvoicing.Contingencies.Declare`). Al cerrarse se fija el
  plazo de sus documentos (§7.3) y el procesador los transmite en orden de consecutivo.
- Si al transmitirlo la DIAN lo rechaza, se corrige como un rechazado (§8). Su documento de corrección
  en cola se transmite después, referido al documento que quede validado con ese número (FR-066).

### 7.2 Tipo 03: falla el facturador, su conexión o su proveedor

- **Apertura automática** por `CircuitoDeCanal` (uno por cooperativa y canal): cuenta los
  `ChannelUnavailable` definitivos, los sondeos `ProbarAsync` fallidos y **las esperas del POS
  vencidas** (`Dian.EsperaMaximaPosSegundos` cumplida sin respuesta definitiva, §6.1 paso 4); al
  llegar a `Dian.UmbralFallasCircuito` (por defecto 3) seguidos abre un evento `Issuer03` con
  `DetectedBy = Process`. Desde ese momento las ventas **nuevas** del POS se numeran en contingencia
  03 y reciben la representación de papel en el acto, así SC-004 se cumple por su rama «o el
  documento de contingencia si no»: la venta que ya tenía número normal con resultado ambiguo no se
  renumera (abajo) y se entrega al validarse. **Apertura manual** con motivo: `OpenContingencyCommand`
  (`POST /api/electronic-invoicing/contingencies`, `ElectronicInvoicing.Contingencies.Declare`).
- Mientras esté abierto, **las ventas fiscales nuevas** se numeran con la resolución de contingencia
  del tipo que respaldan (`ResolutionKind.Contingency`, `BacksUpKind`, FR-058, FR-065):
  - en el POS, automáticamente, con el tipo del rol de contingencia que respalda al rol de la venta
    (`PosSaleContingency` para `PosSale`, `InvoiceContingency` para `InvoiceOnRequest`;
    `CashRegisterDocumentRole`): el primero exige una resolución con `BacksUpKind = PosEquivalent` y el
    segundo una con `BacksUpKind = Invoice`;
  - en oficina, el tipo normal responde `Blocked` y el mensaje nombra el tipo de contingencia que
    debe usarse (el que la cooperativa haya configurado para esa clase, con el prefijo de la
    resolución de contingencia).
  Sin resolución de contingencia vigente asociada al canal, la guardia responde `Blocked` con ese
  motivo.
- El ERP imprime la representación de papel con su propia plantilla, **sin CUFE** y con la leyenda de
  la norma (§13.1). El documento queda `IssuerContingency`.
- Lo que ya estaba numerado con la numeración normal y con resultado ambiguo **no se renumera**: sigue
  en `Sent` o `Pending` y se resuelve consultando.
- **Cierre**: automático cuando el sondeo del circuito vuelve a responder (`ProbarAsync` exitoso), o
  manual con motivo. Al cerrarse, `EndedAt` fija el plazo (§7.3) y el procesador transmite en orden
  de consecutivo como `TransmitContingency`: el canónico lleva `dianDocumentTypeCode = "03"` y el
  bloque `contingency` con el número y la fecha del documento de papel (anexo 1.9).
- Quién presenta la constancia ante la DIAN y con qué soporte es de la cooperativa (pregunta I2 al
  dueño; propuesta: la cooperativa, con el informe del evento).
- **Por cotejar**: si el DEE POS usa numeración de contingencia o transmisión diferida; hasta
  confirmarlo, un DEE en contingencia 03 exige que la cooperativa haya registrado una resolución de
  contingencia con `BacksUpKind = PosEquivalent` o la guardia lo bloquea.

### 7.3 Plazo de transmisión

`PlazoDeContingencia` (puro) calcula `TransmissionDeadline` de cada documento del evento:

- cuántas horas: `Dian.PlazoContingenciaHoras` vigente a la fecha del documento (por defecto 48, con
  `LegalSource` «ET art. 616-1; Res. 165/2023 y 167/2021 compiladas en la Res. 000227/2025»);
- desde cuándo: en factura, DEE y sus notas, desde el fin del evento (`EndedAt`, «desde que se supera
  el inconveniente»); en documento soporte y su nota, desde las 00:00 del día siguiente al fin del
  evento (Res. 167, arts. 11-12).

La alerta `Dian.PlazoDeContingencia` sale `Dian.AlertaHorasAntesDelPlazo` horas antes (por defecto
6) por cada evento con documentos sin transmitir, y otra vez al vencer. SC-015 exige que ninguno
supere el plazo sin alerta previa.

## 8. Documentos rechazados: casos a, b y c

Sólo sobre `Status = Rejected`. El caso a basta con la respuesta definitiva de rechazo. Los casos b y c,
que anulan el documento comercial, exigen además el rechazo **confirmado por la consulta de estado**
(FR-066): una transmisión `QueryStatus` posterior al rechazo que respondió `Rejected`
(`RejectedBy = Dian`) o `NotFound` (`RejectedBy = Channel`: nunca llegó a la DIAN); la pantalla ofrece
«Consultar a la DIAN» antes de habilitarlos. Si no, `ElectronicInvoicing.Document.NotRejected` (422).
Ninguno aplica a `Sent`, `Validated` ni a un `Pending` sin respuesta. Un rechazado no está
expedido: su número no se consume (Res. 167 art. 13; Res. 165) y ningún caso deja un número sin
explicación (FR-038, US8 escenario 3).

### 8.1 Versiones

`COR_ElectronicDocumentVersions` (`IHechoInmutable`): `VersionNumber`, `SourceDocumentPublicId` (el
documento comercial que la originó), `Reason` (`Initial`, `CaseA`, `CaseB`), SHA-256 y referencia del
canónico, y referencias del XML firmado, el `AttachedDocument` y el PDF. Como esos artefactos llegan
**después** de insertar la versión, sus referencias nulas se llenan **una sola vez** (nulo → valor) y
no cambian más; la guarda de `SaveChangesAsync` admite exactamente ese cambio (patrón de
`IInmutableTrasConfirmar`) y rechaza cualquier otro. `CurrentVersionId` del documento apunta a la
vigente.

### 8.2 Caso a: corregir sin cambio económico

`CorrectRejectedDocumentCommand` (`POST /documents/{id}/correct`,
`ElectronicInvoicing.Documents.Correct`, motivo obligatorio).

- `ReglaDeCorreccionFiscal` (pura) compara la **huella económica** del canónico vigente y del nuevo:
  identificación de la contraparte (tipo, número y DV); por línea, producto, cantidad, base,
  descuentos, impuestos y retenciones; y los totales.
- Si coinciden: crea la versión n+1 con el canónico nuevo (formato, códigos, datos de contacto o
  responsabilidades corregidos en el maestro o en los catálogos). Si cambió la identificación fiscal
  **no económica** de la contraparte (nombre, dirección, contacto, responsabilidades), agrega una
  versión nueva de `INV_DocumentPartySnapshots` con antes y después auditados: es la única excepción
  de FR-005 (SC-013). Vuelve a `Pending` y reemite **por el mismo canal y con el mismo número**. **No
  emite mensajes de negocio.**
- Si difieren: 422 `ElectronicInvoicing.Document.EconomicFootprintChanged` con `data.fields[]` (qué campo
  económico cambió) y la indicación de usar el caso b. Quien opera no elige entre a y b: lo decide la
  huella.

### 8.3 Caso b: reemplazar

`ReplaceRejectedDocumentCommand` (`POST /documents/{id}/replace`, `ElectronicInvoicing.Documents.Correct`
más el permiso de confirmar de la clase —`Inventory.Sales.Confirm` en ventas,
`Inventory.Purchases.Confirm` en DS—, motivo obligatorio).

1. Anula el documento comercial con un documento `Voiding` **sin efecto fiscal**, que emite sus
   mensajes de anulación (`DocumentoAnulado`, `AjusteDeVentaACredito` con
   `AdjustmentClass = VoidingByDianRejection` si tenía crédito) y revierte existencias.
2. Marca `FiscalNumberReleased = true` en el documento anulado, lo que libera el número frente al
   índice único filtrado de `INV_Documents` (T16).
3. Crea el documento de reemplazo precargado, enlazado (`DocumentLinkKind.ReplacementOf`), y lo
   confirma tomando **el mismo número fiscal** por una vía de `NumeradorFiscal` exclusiva de este
   comando, que no consume consecutivo. El reemplazo emite sus propios mensajes
   (`AjusteDeVentaACredito` con `AdjustmentClass = Replacement` si hay crédito).
4. Crea la versión n+1 con `SourceDocumentPublicId` del reemplazo y `Reason = CaseB`; vuelve a
   `Pending` y reemite por el canal sellado.

Todo en una transacción; la emisión, después del commit (§6.1).

### 8.4 Caso c: cancelar sin reemplazo

`CancelRejectedDocumentCommand` (`POST /documents/{id}/cancel`, mismos permisos que b). Igual que b en
la anulación (pasos 1 y 2), sin reemplazo. El documento electrónico queda
`CancelledWithoutReplacement` con motivo y responsable; el número queda ligado a su documento anulado y
no cuenta como hueco.

## 9. Resoluciones de numeración y clave técnica

`COR_DianNumberingResolutions` (FR-065): `Kind` (`Invoice`, `PosEquivalent`, `SupportDocument`,
`Contingency`), `BacksUpKind` (en las de contingencia, el tipo que respaldan), número y fecha de la
resolución, `Prefix` (hasta 4 alfanuméricos, Res. 165 art. 11 num. 4), `RangeFrom`, `RangeTo`,
vigencia, `Environment`, `LastIssuedNumber` y `RowVersion`. El numerador se llama
`LastIssuedNumber`, nunca `NextNumber` (lo vigila `NingunModuloEscribeMovimientosFueraDelContrato`).

`COR_DianResolutionChannels`: asocia el prefijo de una resolución a un canal o `SoftwareId` **desde
una fecha**. Es la condición de FR-064 para cambiar de canal. En las resoluciones de factura lleva
`TechnicalKey`, con vigencia, ligada a la resolución y al software con que se obtuvo: marcada
`[NoAuditar]` (fuera del diff), **enmascarada** en toda respuesta (sólo los 4 últimos caracteres) y
guardada en la base de la cooperativa (propuesta I4 aceptada por defecto; en modo proveedor la usa el
proveedor, en modo propio la envía el ERP al servicio central, §15).

Numeración (`NumeradorFiscal`, T16):

- Se asigna **al confirmar**; el borrador no consume. La fila de la resolución es la **última** del
  orden canónico del cerrojo y se incrementa por EF dentro de la transacción de confirmación.
- Busca la resolución por (`Kind`, `Prefix` del tipo de documento, `Environment` del canal sellado)
  vigente a la fecha de operación **y** asociada al canal sellado en esa fecha.
- Rechaza: vencida → `ElectronicInvoicing.Resolution.Expired`; agotada →
  `ElectronicInvoicing.Resolution.Exhausted`; sin resolución, de otro ambiente o no asociada al canal →
  `Inventory.Numbering.ResolutionUnavailable`. Nunca numera fuera de una resolución vigente; la venta se
  detiene con mensaje claro y alerta a quien administra resoluciones (edge case «Resolución agotada»).
- **Las notas** (91, 92, 94, 95) llevan su propio consecutivo por tipo y prefijo en
  `INV_DocumentSequences` (`NextValue`), sin resolución, y pueden referirse a una factura cuya
  resolución ya venció o se agotó.
- Avisos: `Dian.ResolucionPorAgotar` al consumir `Dian.AvisoResolucionPorcentaje` (0,90) del rango;
  `Dian.ResolucionPorVencer` a `Dian.AvisoResolucionDias` (30) de vencer. Los revisa
  `ProgramadorDeTareas` (I4).
- `ConsultarRangosAsync`, donde el canal lo ofrezca, sólo verifica una resolución registrada y
  **propone** su clave técnica; nunca crea resoluciones sola.

Comandos: `RegisterNumberingResolutionCommand` (`POST /resolutions`,
`ElectronicInvoicing.Resolutions.Manage`) y `LinkResolutionToChannelCommand`
(`POST /resolutions/{id}/channels`). Una resolución con documentos no cambia de prefijo, rango ni
ambiente; se registra otra.

## 10. Configuración de emisión por cooperativa

### 10.1 Configuración con vigencia

`COR_ElectronicEmissionSettings`, una fila por vigencia, sin cruces (FR-064, supuesto 10: por
cooperativa y con vigencia, no por tipo de documento): `Mode` (`TechnologyProvider` |
`OwnSoftware`), `ChannelCode`, `Environment` (`DianEnvironment`, trasladado de `Enums/Payroll` a
`Domain/Enums/Dian` con los mismos valores `Production = 1`, `Testing = 2`), `SoftwareId` y `TestSetId`
(modo propio), `CredentialKey` (**sólo la referencia**, §11), `CredentialVerifiedAt`,
`EmailDeliveryBy` (`Erp` | `Channel`), `IsEnabled`, `Reason`, `ValidFrom`, `ValidTo`.

`ConfigureEmissionCommand` (`POST /api/electronic-invoicing/settings`,
`ElectronicInvoicing.Settings.Manage`, motivo obligatorio):

- crea una fila nueva y cierra la anterior la víspera de `ValidFrom`;
- exige, para el canal nuevo, al menos una resolución vigente de cada tipo que la cooperativa usa con
  su prefijo asociado a ese canal o software (§9);
- rechaza un canal sin `AceptaNumeroDelErp` y `SIMULADO` en `Production`;
- se audita sin `CredentialKey` en el diff; `CredentialVerifiedAt` queda nulo hasta verificar.

### 10.2 Verificación de la credencial

`VerifyChannelCredentialCommand` (`POST /settings/verify-credential`) resuelve la credencial (§11),
llama `ProbarAsync` y, si responde, fija `CredentialVerifiedAt`. Si la clave del Secret no coincide
con la ruta derivada, `ElectronicInvoicing.CredentialMismatch` (422).

### 10.3 Parámetros con vigencia (`COR_ParameterVersions`, módulo `EINV`)

| Clave | Valores | Defecto | Ámbitos | Uso |
|---|---|---|---|---|
| `Dian.ObligadaAFacturar` | bool | **true** (apagarlo exige permiso y motivo) | None | Sólo gobierna la venta (FR-063). Lo lee la guardia desde I3. |
| `Dian.EsperaMaximaPosSegundos` | int | 15 | None, PointOfSale | §6.1 paso 4 |
| `Dian.PlazoContingenciaHoras` | int, con `LegalSource` | 48 | None | §7.3 |
| `Dian.AlertaHorasAntesDelPlazo` | int | 6 | None | §7.3 |
| `Dian.MinutosAlertaSinValidar` | int | 10 | None | §6.3 |
| `Dian.UmbralFallasCircuito` | int | 3 | None | §7.2 |
| `Dian.AvisoResolucionPorcentaje` · `Dian.AvisoResolucionDias` | decimal · int | 0.90 · 30 | None | §9 |
| `Dian.EntregaCorreo` | `Erp`, `Canal` | Erp | None | Valor propuesto para `EmailDeliveryBy` al crear una configuración; manda el de la configuración vigente (§13.2) |
| `DocumentoSoporte.Generacion` | `PorOperacion`, `Semanal` | PorOperacion | None | §14.2 |

El modo de emisión y la numeración **se sellan al confirmar** (FR-012): un cambio posterior no altera
un documento ya numerado.

### 10.4 La decisión única: `GuardiaDeEmisionFiscal`

`GuardiaDeEmisionFiscal.Evaluar(fecha, tipoDeDocumento, caja?)` (patrón de `GuardiaDeMetodos.Evaluar`)
responde uno de tres veredictos:

- `Electronic(canal, resolución)`: se emite por ese canal y se numera con esa resolución (o con el
  consecutivo propio, si es nota);
- `NonElectronic`: la cooperativa no está obligada a esa fecha y la clase es de venta: se usa el
  comprobante no electrónico y su nota (FR-036, supuesto 20);
- `Blocked(motivos[])`, nunca sin salida: cada motivo dice qué falta, dónde completarlo y con qué
  permiso.

Motivos de `Blocked`: la cooperativa está obligada y la entrega I4 no está activa; no hay
configuración vigente o `IsEnabled = false`; credencial no verificada; el canal no declara el tipo;
no hay resolución vigente asociada al canal (o está vencida o agotada); en producción con el set de
pruebas pendiente (modo propio); contingencia 03 abierta sin resolución de contingencia para el tipo;
el tipo pide una clase no electrónica en una cooperativa obligada (o al revés); `SIMULADO` en
producción. El documento soporte **no** depende de `Dian.ObligadaAFacturar`: sigue su propia norma
(Res. 167) y exige canal configurado y resolución `SupportDocument`.

La consultan: la confirmación en el servidor (paso 4 de §1.3 de `decisiones-transversales.md`), la apertura de la sesión de
caja (aviso temprano, no bloquea la apertura) y `GET /api/electronic-invoicing/readiness`
(`GetDianReadinessQuery`, `ElectronicInvoicing.Settings.View`), que devuelve el veredicto por tipo de
documento y por caja con la lista de lo que falta, al estilo de
`GET /api/payroll/legal-parameters/missing`. Una cooperativa obligada no confirma ninguna venta fiscal
hasta que la guardia responda `Electronic` (US8 escenario 9).

### 10.5 Cambio de canal

Cada documento sella al numerarse `Mode`, `ChannelCode` y `SoftwareId` (FR-064). Sus reenvíos, sus
casos a y b (que conservan el número) y su transmisión desde contingencia salen **por ese canal**,
aunque la configuración cambie (US8 escenario 8; edge case «Cambia la configuración…»). Las notas y los
eventos son documentos nuevos: salen por el canal vigente al confirmarse. El modo nuevo aplica a lo
numerado desde su vigencia y sólo con resoluciones cuyo prefijo esté asociado a ese software.

Si el canal sellado se retira, `TransmitByCurrentChannelCommand`
(`POST /documents/{id}/transmit-by-current-channel`,
`ElectronicInvoicing.Documents.TransmitByCurrentChannel`, motivo) lo transmite por el canal vigente
**sólo** si la resolución con que se numeró está asociada al canal vigente; queda auditado. Nunca se
reenvía lo pendiente por el canal nuevo de forma automática.

## 11. Credenciales: fuera de la base y del repositorio

- **Un Secret de Kubernetes por ambiente**, `erp-fe-credenciales`, con **una clave por cooperativa y
  canal**: `{tenantPublicId}.{channelCode}.json`. Se monta como volumen de **directorio, sin
  `subPath`**, en `/secrets/facturacion-electronica/`, así el kubelet refresca los cambios sin
  reiniciar la API (a diferencia de SMTP, que se lee al arrancar). Agregar una cooperativa no toca el
  Deployment. Límite del Secret: 1 MiB (cientos de JSON pequeños).
- Contenido: lo que pida el proveedor, p. ej.
  `{ "tokenEmpresa": "…", "tokenPassword": "…" }` o `{ "usuario": "…", "clave": "…", "cuenta": "…" }`.
  El esquema lo declara cada adaptador y `CredencialesEnArchivo` lo valida al leer.
- `CredencialesEnArchivo` (`ICredencialesDeCanal`) arma la ruta **sólo** con el `PublicId` de la
  cooperativa resuelta (`ICurrentTenantService`, o la entrada del directorio dentro de
  `IEjecutorEnCooperativa`) y el `ChannelCode` sellado en el documento; nunca con un texto leído de la
  base. Caché en memoria de 5 minutos por huella del archivo. Así una cooperativa no puede usar
  credenciales de otra aunque alguien altere su base.
- La base guarda sólo `CredentialKey` (para mostrar «configurada / verificada el…»); si no coincide con
  la ruta derivada, `ElectronicInvoicing.CredentialMismatch`.
- Guion `tools/scripts/crear-secreto-facturacion-electronica.ps1 -Ambiente -Tenant -Canal`, con el
  patrón de `crear-secreto-smtp.ps1`: valores por teclado, por STDIN sobre SSH, y
  `kubectl patch secret` que agrega o reemplaza **sólo esa clave**. En producción exige escribir
  `PRODUCCION`. Nada queda en disco local, en el historial, en `appsettings*` ni en GitOps en claro. El
  overlay de GitOps monta el volumen.
- Modo software propio: el certificado `.p12`, su clave y el PIN siguen en los Secrets del servicio
  central de la 010 (`erp-ne-<tenant>`, ampliado); la API del ERP no los ve (§15).
- Se exigen **credenciales por empresa**, no una llave maestra de socio que pueda emitir a nombre de
  cualquier NIT (criterio eliminatorio, §16).
- Pruebas: `LasCredencialesDeFacturacionNoTocanLaBase` (ninguna propiedad `Password`, `Token` o
  `Secret` en entidades de `ElectronicInvoicing`, y ruta derivada del inquilino) y una prueba
  unitaria de `CredencialesEnArchivo` con dos cooperativas.

Evolución posible, fuera de esta feature: AWS Secrets Manager con el rol de IAM Roles Anywhere de la
011 (el rol es por ambiente, no por cooperativa).

## 12. Artefactos: adjuntos del módulo que no se borran

`AdjuntosDeModulo.DeModulo` suma tres dueños (T41, D-04 punto 2):

| Dueño | Lectura | Borrable | Subidas | Qué guarda |
|---|---|---|---|---|
| `ElectronicSalesDocument` | `Inventory.Sales.View` | no | no | factura, notas, DEE y su nota |
| `ElectronicPurchaseDocument` | `Inventory.Purchases.View` | no | no | documento soporte y su nota; eventos RADIAN (I5) |
| `DianContingencyEvent` | `ElectronicInvoicing.Contingencies.View` | no | sí, con `ElectronicInvoicing.Contingencies.Declare` | constancias y evidencias del evento 03/04 |

- `OwnerEntityPublicId` = `PublicId` del documento electrónico (en `DianContingencyEvent`, el del
  evento de contingencia).
- Por versión: el canónico (`application/json`), el XML firmado y el `AttachedDocument`
  (`application/xml` o `application/zip`) y el PDF de la representación gráfica. Por transmisión: el
  `ApplicationResponse`. Nombre: `{Number}-v{n}-{artefacto}.{ext}`.
- Se guardan desde el servidor con `UploadAttachmentCommand` (formato `Direct`, estado `Available`,
  SHA-256), por `IBlobStore`, dentro del contexto de la cooperativa (`IEjecutorEnCooperativa` en
  segundo plano). El canónico se sube **después** del commit de la confirmación (T40) y su referencia
  llena la versión una sola vez (§8.1).
- `AttachmentPolicy.ModuleGeneratedMimeTypes` (+ `application/xml`, `application/zip`,
  `application/json`) vale **sólo** para lo que genera un módulo; las subidas de personas conservan la
  lista de la 011.
- Conservación: el plazo legal, 5 años (ET art. 632). Como no se borran, la papelera de 90 días no les
  aplica. En S3 se recomienda la transición a almacenamiento frío (Glacier Instant Retrieval o
  Intelligent-Tiering) a los 90 días: es una transición, no un borrado.
- Volumen de referencia: ~150 KB por documento; con 5.000 documentos al día, ~0,75 GB diarios y
  ~20.000 filas de `COR_Attachments` por cooperativa.
- No se guardan: el PDF del proveedor (I3 del dueño, propuesta «no») ni el XML de la factura recibida
  del proveedor (E10).
- Descarga: `POST /api/electronic-invoicing/documents/{id}/download-link?artifact=&version=` (como
  las demás rutas de enlace: adjuntos, PILA, dispersión) devuelve un
  enlace firmado de 60 s (el de la 011), auditado; lo decide la regla del dueño del adjunto (FR-068).

## 13. Representación gráfica y entrega al comprador

### 13.1 Representación

`IRepresentacionGraficaRenderer` (Application) → `RepresentacionGraficaReport` (API, QuestPDF +
QRCoder). **Una sola plantilla del ERP** para todos los canales (FR-064: cambiar de proveedor no
cambia pantallas ni documentos) y para el modo propio, que no tiene PDF de proveedor. Dos formatos:
carta, para factura, notas y documento soporte, y tirilla de 80 mm para el POS, impresa desde el
navegador o la app (`IImpresionDeDocumentos`).

- Usa la copia fiscal vigente de la contraparte o el archivo firmado (FR-011), nunca el maestro de hoy.
- QR: el `QrContent` que devuelve el canal, tal cual. En modo propio lo arma el constructor según el
  anexo 1.9 con `https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey=<código>`
  (`catalogo-vpfe-hab` en pruebas). El contenido exacto está **por cotejar**.
- Leyendas por estado: tipo 04 «Factura electrónica de venta – tipo 04, pendiente de validación de la
  DIAN»; papel 03 sin CUFE, con el texto de la norma; `Environment = Testing` «SIN VALIDEZ FISCAL».
- Se genera al validarse (o al expedirse en contingencia) y se guarda como artefacto (§12). Las
  reimpresiones (`POST /api/inventory/documents/{id}/reprint`, auditada) usan el PDF guardado.

### 13.2 Entrega

- En operación normal, **sólo después** de `Validated` o `ValidatedWithNotices` (FR-063, SC-015). En
  contingencia, en el acto (FR-067).
- POS: imprime al validarse. Si se cumple `Dian.EsperaMaximaPosSegundos`, el documento queda en
  «pendientes de entrega» de la sesión, para reimprimir o enviar por correo cuando se valide; la
  espera vencida cuenta para el circuito (§7.2) y, pasado el umbral, las ventas siguientes salen en
  contingencia 03 con su papel en el acto.
- Correo: con `EmailDeliveryBy = Erp` (por defecto) lo envía el ERP por `IEmailSender`, con el ZIP del
  `AttachedDocument` y el PDF (asunto y nombre del adjunto según el anexo: **por cotejar**), a la
  dirección de recepción de la copia fiscal. Con `Channel`, si el proveedor lo ofrece
  (`PuedeEnviarCorreo`); nunca los dos. El envío se registra en el documento (fecha y canal de
  entrega). Sin autorización de datos (FR-011) el correo sólo se usa para entregar el documento que la
  ley exige.

## 14. Documento soporte y eventos RADIAN

### 14.1 Documento soporte

Clases `SupportDocument` y `SupportDocumentAdjustmentNote` (I4, ruta
`/api/inventory/purchases/support-documents`). Acompaña la recepción de una compra a un proveedor no
obligado a facturar (`IsObligatedToInvoice = false`); en el canónico, `counterparty.role = Supplier`.
Emite `FacturaProveedorRegistrada` (con signo en la nota). Sus contingencias cuentan el plazo desde el
día siguiente (§7.3).

### 14.2 Generación

`DocumentoSoporte.Generacion = PorOperacion` (defecto): un DS por compra. `Semanal` (Res. 167 art. 10
par. 2) acumula las operaciones de la semana y se genera el último día hábil; queda **por cotejar** con
el contador y el proveedor, y su generación la dispararía `ProgramadorDeTareas`.

### 14.3 Eventos 030 y 032

- **Hasta I5** (desde I1): la cooperativa emite el acuse (030) y el recibo del bien (032) en el portal
  de su proveedor o de la DIAN, y el ERP lo registra con `RegisterExternalRadianEventCommand` en
  `INV_SupplierInvoiceEvents` (fecha, código único si lo tiene, quién). El registro de la factura del
  proveedor a crédito muestra el estado de los dos eventos y levanta `Compras.EventosRadianFaltantes`
  a los `Compras.DiasAlertaEventosRadian` (FR-050).
- **Desde I5**: `EmitRadianEventCommand` crea un `COR_ElectronicDocuments` de
  `Kind = RadianEvent030/032`, emite por `EmitirEventoAsync` del canal vigente (el evento es un
  documento nuevo), con la misma tabla de transmisiones y la máquina simplificada, y lo enlaza en
  `INV_SupplierInvoiceEvents.ElectronicDocumentPublicId` (T42). La numeración propia del evento la
  define I5. Criterio de contratación: que el canal reciba facturas y emita eventos para el mismo NIT.

## 15. Modo software propio: el servicio central de la 010

Cuando exista el servicio (hoy no existe `src/Servicios`; N3 de la 010, T108 a T115, sin empezar), se
suma **un adaptador más**, `CanalServicioCentral : ICanalDeEmisionElectronica`, **sin cambiar tablas,
estados, pantallas ni comandos**:

- Convierte el canónico en **UBL 2.1 sin firmar** con constructores puros en Domain
  (`UblInvoiceBuilder`, `UblCreditNoteBuilder` y los de DEE, DS y `ApplicationResponse` de eventos),
  con casos dorados byte a byte en `tests/IngenIA365ERP.Domain.Tests/ElectronicInvoicing/Casos/`,
  regenerables sólo a propósito (patrón `PILA_ESCRIBIR_ESPERADO`).
- Llama al servicio con el **mismo sobre** del contrato de la 010: JWT RS256 de 5 minutos acuñado por
  la API con claim `tenant`, audiencia propia de facturación, `habilitacion` (NIT, DV, modo,
  ambiente, `softwareId`, `testSetId`), `secretos` **por nombre** bajo la carpeta del tenant y
  `X-Idempotency-Key` (§6.2).
- Rutas nuevas del servicio, en un contrato hermano (`servicio-documentos-electronicos.md`, a escribir
  cuando exista el servicio): `/v1/facturacion/documentos/{validar | firmar-y-transmitir |
  consultar-estado}`, `/v1/facturacion/eventos`, `/v1/facturacion/rangos` (`GetNumberingRange`) y
  `/v1/facturacion/adquirente` (`GetAcquirer`), con los XSD de los anexos 1.9, DEE 1.0 y DS embebidos;
  operaciones SOAP `SendBillSync` o `SendBillAsync` + `GetStatusZip`, `SendTestSetAsync` y
  `SendEventUpdateStatus`.
- El servicio completa CUFE, CUDE o CUDS: el CUFE con la clave técnica que el ERP envía desde
  `COR_DianResolutionChannels`; el CUDE y el CUDS con el PIN, que es secreto del servicio. Firma
  XAdES-EPES, transmite, devuelve los artefactos y **olvida**. Nunca reintenta; ante un tiempo agotado
  responde 200 con `EnProceso` y los artefactos ya producidos, igual que en la 010.
- Correspondencia de resultados: `Aceptado` → `Validated` (o `ValidatedWithNotices` si trae
  notificaciones), `Rechazado` → `Rejected`, `EnProceso` → `InProcess`, `NoEncontrado` → `NotFound`.
  En modo propio **el canal es el servicio**: el contrato hermano debe fijar cuándo responde «DIAN no
  disponible» (con el documento firmado y su código) para que siga valiendo que la contingencia 04
  sólo la declara el canal.
- Aplica a lo numerado desde su vigencia y sólo con resoluciones cuyo prefijo esté asociado al
  `SoftwareId` (FR-064). La habilitación del software, el set de pruebas (`TestSetId`) y la asociación
  de prefijos son trámites de la cooperativa.

Firmar dentro de la API del ERP queda descartado (la 010 lo descartó en R10: certificado y PIN en el
mismo proceso).

## 16. Selección del proveedor tecnológico

Es decisión del dueño y de COOFLOPAL (A3, D-05, D-07). Mientras tanto se desarrolla y se ensaya con
`CanalSimulado`, para no bloquear la entrega ni el ensayo por el contrato.

**Criterios eliminatorios** (salen de FR-038, FR-064, FR-067 y FR-068):

1. Autorizado por la DIAN, con API documentada y ambiente de habilitación (sandbox).
2. **Acepta el número que asigna el ERP** (prefijo y consecutivo) y varias resoluciones por NIT; un
   número repetido devuelve el documento existente o un código reconocible.
3. Cubre 01, 91 (92 en I6), el DEE POS y su nota de ajuste, el DS y su nota de ajuste, las
   contingencias 03 y 04 con código explícito de contingencia de la DIAN, y recepción con eventos
   030 y 032 para I5.
4. Respuesta síncrona con código único y QR; consulta de estado por número o código único; descarga
   por API del XML firmado, el `ApplicationResponse` y el `AttachedDocument`.
5. Credenciales por empresa, no sólo una llave maestra de socio.

**Criterios de puntuación**: latencia p95 ≤ 5 s medida en sandbox con 30 cajas a la vez (condición de
SC-004 y SC-019); acuerdo de servicio y precio por documento a ~150.000 documentos al mes por
cooperativa; ISO 27001; que el ERP pueda desactivar el PDF y el correo del proveedor; salida de datos
al terminar el contrato.

**Lista corta para una prueba pagada en sandbox**: The Factory HKA (numeración manual, códigos 201
asíncrono y 208 contingencia, métodos de descarga y contenedor, manuales de DEE) y Dataico (REST JSON,
número en el cuerpo, DS, eventos y POS). Factus sólo si admite el número del ERP (hoy lo asigna desde
su rango). Carvajal y Facturatech son viables, con SOAP o layout propio y costo corporativo. Siigo,
Alegra y Loggro se descartan como canal: son ERP o POS completos que exigen tener clientes y productos
en su sistema. Ninguno de los revisados ofrece webhooks como vía principal de estado: el estado se
consulta; un webhook podría sumarse después como aviso para consultar antes, nunca como fuente de
verdad.

## 17. Permisos, rutas y comandos (resumen; el detalle en `api.md`)

| Ruta (`/api/electronic-invoicing/…`) | Comando / consulta | Permiso |
|---|---|---|
| `GET settings` · `POST settings` | — · `ConfigureEmissionCommand` | `Settings.View` · `Settings.Manage` |
| `POST settings/verify-credential` | `VerifyChannelCredentialCommand` | `Settings.Manage` |
| `GET/POST resolutions`, `POST resolutions/{id}/channels` | `RegisterNumberingResolutionCommand`, `LinkResolutionToChannelCommand` | `Resolutions.View` / `Resolutions.Manage` |
| `GET documents`, `GET documents/{id}` | `ListElectronicDocumentsQuery` | `Documents.View` (con alcance de bodega y punto) |
| `POST documents/{id}/retry` · `POST documents/{id}/query-status` | `EmitElectronicDocumentCommand` · `QueryElectronicDocumentStatusCommand` | `Documents.Transmit` |
| `POST documents/{id}/correct` · `/replace` · `/cancel` | casos a, b, c | `Documents.Correct` (+ confirmar de la clase en b y c) |
| `POST documents/{id}/transmit-by-current-channel` | `TransmitByCurrentChannelCommand` | `Documents.TransmitByCurrentChannel` |
| `POST documents/{id}/download-link` | — | regla del dueño del adjunto: `Inventory.Sales.View` o `Inventory.Purchases.View`; sin ella, 404 |
| `GET/POST contingencies`, `POST contingencies/{id}/close` | `OpenContingencyCommand`, `CloseContingencyCommand` | `Contingencies.View` / `Contingencies.Declare` |
| `GET readiness` | `GetDianReadinessQuery` | `Settings.View` |

Todos con prefijo `ElectronicInvoicing.`; sin permiso o fuera de alcance, el 404 genérico. Informe
`/api/reports/inventory/dian-documents`. Pantallas: `/admin/facturacion-electronica`,
`/maestros/resoluciones-dian`, `/ventas/documentos-electronicos`, `/ventas/contingencias-dian`,
`/compras/documentos-soporte`.

Errores (todos 422; el 409 queda para `Concurrency.*`): `ElectronicInvoicing.NotReady`
(`data.missing[]` de la guardia), `ElectronicInvoicing.CredentialMismatch`,
`ElectronicInvoicing.Document.NotRejected`, `ElectronicInvoicing.Document.AwaitingResponse`,
`ElectronicInvoicing.Resolution.Exhausted`, `ElectronicInvoicing.Resolution.Expired`,
`Inventory.Numbering.ResolutionUnavailable`; nuevos de este contrato:
`ElectronicInvoicing.Document.MissingData` (§4.4, `data.missing[] { field, where, permission }`) y
`ElectronicInvoicing.Document.EconomicFootprintChanged` (§8.2, `data.fields[]`).

Alertas: `Dian.DocumentoSinValidar`, `Dian.DocumentoRechazado`, `Dian.PlazoDeContingencia`,
`Dian.ContingenciaAbierta`, `Dian.ResolucionPorAgotar`, `Dian.ResolucionPorVencer` (I4), con
destinatarios por permiso (`ElectronicInvoicing.Documents.View` por defecto).

## 18. Pruebas

- **Arquitectura**: `ElProveedorTecnologicoSoloLoConoceSuAdaptador`,
  `LasCredencialesDeFacturacionNoTocanLaBase`, `ElComercioNoTieneValoresLegalesFijos`,
  `LosHechosInmutablesNoSeModifican` (versiones con su excepción de una sola escritura, transmisiones),
  `SoloElNumeradorNumera`, `PrincipioXI_ContableImmutable` ampliada a
  `Entities/ElectronicInvoicing/Transactions`, `LosEndpointsProtegidosExigenPermiso` ampliada a
  `Endpoints/ElectronicInvoicing/*.cs`.
- **Domain**: todas las transiciones prohibidas de `TransicionesDelDocumentoElectronico`;
  `ReglaDeCorreccionFiscal` (cada campo económico decide b; nombre y dirección deciden a);
  `PlazoDeContingencia` (factura desde el fin, DS desde el día siguiente).
- **Application**: un `Validated` sin código único o sin respuesta de validación **no** deja el
  documento validado; `InProcess` consulta antes de reenviar; `NotFound` reenvía la misma versión.
- **Integración** (`ElectronicInvoicing/DocumentosElectronicosTests`, Testcontainers en los dos
  motores, con `CanalSimulado`): factura validada; nota crédito total que la anula; DEE con su nota de
  ajuste; DS; rechazo y caso a; caso b con el mismo número; caso c; contingencia 04 y su transmisión;
  contingencia 03 por circuito, numeración de contingencia y transmisión al cerrar; cambio de canal con
  documentos pendientes; dos emisiones concurrentes del mismo documento (POS y procesador) producen
  una sola transmisión efectiva; una cooperativa no usa credenciales de otra.
- **Manual en sandbox del proveedor** (no hay DIAN en CI): latencia p95 con 30 cajas; habilitación.

## 19. Pendientes del dueño e incertidumbres

- **A3**: proveedor tecnológico (lista corta y prueba pagada en sandbox). Bloquea el adaptador real y
  la salida, no el desarrollo.
- **A4**: cómo factura COOFLOPAL hoy (SOLIDO no lo hace), sus resoluciones, prefijos, software
  asociado, resolución de contingencia y numeración del DEE POS. Bloquea el ensayo de I4.
- **A5**: buzón y dominio del correo al comprador (SPF/DKIM del relay).
- **I1 a I7** con sus propuestas por defecto (15 s de espera; 03 automática y manual; no guardar el PDF
  del proveedor y almacenamiento frío a 90 días; clave técnica en la base enmascarada; obligada = sí
  por defecto; DS por operación). I7 ya está resuelta: la factura después de un DEE va en I4 (FR-063,
  `contracts/api.md` §18.3.1).
- **Personas** (T24, E3): las responsabilidades, el tributo y el tipo de identificación DIAN se
  derivan de las marcas nuevas de `COR_People`; sin ellas no se factura a clientes identificados.
- **Normativas por cotejar** (§1): contingencia del DEE; códigos 20 y 94; DS semanal; numeración de
  la nota de ajuste del DS; asunto y ZIP del correo; contenido exacto del QR; el plazo del tipo 03 que
  la página de la DIAN todavía da en 30 días.
- **Evidencia de contingencia subida por personas**: la evidencia automática es la bitácora; los
  soportes propios de la cooperativa se adjuntan al evento con el dueño `DianContingencyEvent` (§12),
  que T41 de `decisiones-transversales.md` también lista.
