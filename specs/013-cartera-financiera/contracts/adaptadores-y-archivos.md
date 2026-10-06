# Contrato: adaptadores externos, mensajes al deudor, archivos planos, layouts regulatorios y documentos emitidos

**Feature**: 013 | **Date**: 2026-10-05 | **Entregas**: K1 (puertos con canal `SIMULADO`, correo e
informativos, archivo de desembolsos, documentos si se aprueba I5), K2 (archivos de pagaduría), K4
(cobranza, aviso previo con constancia, SMS y WhatsApp reales con contrato), K5 (layouts del SICSES y de
centrales, extracto y certificado tributario) | **Base**: decisiones transversales T71–T81, con T7, T13,
T20, T22, T38, T42, T62, T67, T69, T70, T102 (`decisiones-transversales.md`) | **Hermanos**: `api.md`
(rutas y cuerpos), `mensajes.md` (catálogo y sobre), `interfaces.md` (orden de giro y contrato de
obligaciones), `contabilidad.md`, `plantillas.md`, `data-model.md`

Requisitos que cubre: FR-003, FR-006, FR-013, FR-014, FR-016, FR-034, FR-036, FR-043, FR-044, FR-045,
FR-047, FR-082, FR-097, FR-098, FR-108–FR-124, FR-131, FR-132, FR-136, FR-137, FR-149 (E-05); D1, D14,
D21, D23, D24, D28; I-1, I-5; US10, US12, US13, US16; SC-011, SC-012, SC-016, SC-019.

## 0. Qué es y qué no es

Este contrato fija cómo Cartera habla con **terceros fuera del ERP** y qué **archivos y documentos**
produce. Todo lo que sale del ERP pasa por uno de cinco caminos:

| Camino | Qué sale | Dónde se decide | Sección |
|---|---|---|---|
| Adaptador externo | Pagaré desmaterializado, consulta a centrales, consulta de listas, SMS, WhatsApp, correo | Un puerto por clase, con canal simulado y guardia de efecto legal | §3 |
| Cola de mensajes al deudor | Informativos, cobranza, aviso previo, gasto de cobranza informado, envío de documentos | `LND_DebtorMessages` y el motor `VentanaLegalDeContacto` | §4, §5 |
| Archivo plano bancario | Transferencia de desembolsos; descuentos por pagaduría (ida) y su respuesta (vuelta) | El motor común de la 010 (`COR_BankFileFormats`, `FlatFileWriter`) | §6 |
| Layout regulatorio | Formato de cartera del SICSES y archivos para centrales de riesgo | JSON embebido con cotejo por campo (`Domain/Common/Layouts`) | §7 |
| Documento emitido | Plan de pagos, extracto, paz y salvo, certificados | `LND_IssuedDocuments` con código de verificación | §8 |

**Tres conceptos que no se mezclan** (CH20):

- **Canal simulado** (`SIMULADO`): el adaptador de prueba de un puerto externo. Existe en todo ambiente,
  también en producción antes y después de activar, y **nunca tiene efecto legal en producción** (§3.6).
- **Doble de prueba** (`TesoreriaDePrueba`, `RecaudadorDePrueba`): reemplaza a un **módulo** que no existe
  (FR-089). Nunca existe en producción: si la configuración lo pide allí, la API no arranca (T20). No es tema
  de este contrato.
- **Doble unitario**: implementación falsa de un puerto, sólo en `tests/`.

**Lo que no es**:

- No hay **webhooks** entrantes. El estado de entrega de un mensaje y el de firma de un pagaré se consultan por
  sondeo (T75). Un webhook sería una ruta anónima sin cooperativa resuelta, y choca con el Principio IV.
- No hay **verificación pública** de documentos (D21): la verificación es de usuarios de la cooperativa.
- No hay **portal del asociado** (D1).
- Cartera **no procesa el archivo de respuesta** de una pagaduría: lo procesa Recaudo, que le envía
  `ResultadoDeDescuentos` (FR-115, T78).
- Cartera **no transmite** archivos a centrales ni a la Superintendencia: los genera, y una persona los carga en
  el portal y marca la carga con su radicado (FR-136, US16).
- No se gestiona SARLAFT (segmentación, monitoreo, UIAF): sólo la consulta de listas y su carga provisional
  (Alcance).
- La PILA de la 010 no se toca: el motor de layouts regulatorios es otro (T79).

## 1. Norma y fuentes

Ningún valor legal está escrito en el código (`LaCarteraNoTieneValoresLegalesFijos`, FR-014). Cada cifra es un
parámetro de la cooperativa con su piso o techo nacional (`LIMITE`, T42) o un valor nacional replicado (T37).

| Tema | Fuente | Dónde vive el valor |
|---|---|---|
| Horarios de cobranza (L–V 7:00–19:00, sábado 8:00–15:00, nunca domingos ni festivos), un contacto por día, un canal por semana tras un contacto directo, nunca referencias personales | Ley 2300 de 2023 | Límites `LIMITE` del catálogo `CatalogoDeLimitesLegales` (techo): `COBRANZA_HORA_INICIO_LV`, `COBRANZA_HORA_FIN_LV`, `COBRANZA_HORA_INICIO_SAB`, `COBRANZA_HORA_FIN_SAB`, `COBRANZA_CONTACTOS_DIA` (C71); parámetros `Cobranza.HorarioLunesAViernes`, `Cobranza.HorarioSabado`, `Cobranza.ContactosPorDia` (la cooperativa sólo restringe) |
| Aviso previo al reporte negativo (20 días calendario desde el envío; dos comunicaciones bajo el umbral) | Leyes 1266 de 2008 y 2157 de 2021 | Límite `AVISO_PREVIO_DIAS`; parámetros `AvisoPrevio.DiasMinimos`, `AvisoPrevio.UmbralDosComunicaciones` |
| Autorización previa para consultar y reportar, permanencia del dato negativo, reclamos (15 + 8 días hábiles) | Leyes 1266 de 2008, 1581 de 2012 y 2157 de 2021 | Autorizaciones por finalidad (T69); plazos del titular como límites (F11) |
| Capacidad de descuento por libranza, ventana de giro (3 días) | Ley 1527 de 2012 | Límite `LIBRANZA_CAPACIDAD_DESCUENTO`; parámetro `Libranza.VentanaDeGiroDias` |
| Formato de cartera del SICSES | Instructivo vigente de la Superintendencia de la Economía Solidaria | Layout JSON con vigencia y `verified` por campo (§7); lo aporta el dueño (I7) |
| Layouts de cada central | Contrato de la cooperativa con la central | Layout JSON por central (§7); lo aporta el dueño (I7) |
| Plazo de firmeza tras una cancelación | D23 | Parámetro `Pagos.FirmezaDiasPorClaseDeMedio` (10 hábiles; cruce interno 0) |

Todo valor sembrado nace «pendiente de validar» (T41). La lectura del abogado sobre «un solo canal por semana»
(I2) y sobre el documento pedido por el titular (I3) está pendiente; las reglas de §4.5 siguen la propuesta por
defecto.

## 2. Piezas y dónde viven

| Pieza | Ubicación | Qué hace |
|---|---|---|
| `ResultadoExterno<T>`, `ContextoDeCanalExterno`, `ArtefactoExterno`, `ICredencialesDeCanalExterno` (fijados aquí) | `Application/Common/ExternalChannels` (fijado aquí) | Lo común de los cuatro puertos (§3.1) |
| `ICanalDeMensajeria` · `ICanalesDeMensajeria` | `Application/Common/Messaging` | Mensajería saliente a personas (plataforma, E-05) |
| `IListasRestrictivas` · `IProveedoresDeListas` | `Application/Common/Compliance` | Consulta de listas (plataforma) |
| `IDepositoDeValores` · `IDepositosDeValores` | `Application/Lending/Adapters` | Pagaré desmaterializado |
| `ICentralDeRiesgo` · `ICentralesDeRiesgo` | `Application/Lending/Adapters` | Consulta a centrales de riesgo |
| `GuardiaDeCanalesExternos` · `CanalesExternosOptions` | `Application/Lending/Adapters` | Única decisión del efecto legal de un resultado simulado (§3.6) |
| `VentanaLegalDeContacto` · `RepartoDeGestores` | `Domain/Lending/Collections` (puros) | Ley 2300 (§4.5); reparto de gestores (K4) |
| `CoincidenciaDeNombres`, `ReglaDeRevocacion`, `PlazosDelTitular` | `Domain/Common/Compliance` (puros) | Coincidencia por nombre normalizado; revocación; plazos |
| `RenderizadorDePlantillasDeMensaje` | `Application/Lending/Collections` | Sustitución `{{Variable}}` con lista blanca (§4.7) |
| `NovedadesDeLibranza` | `Domain/Lending/Payroll` (puro) | Altas, cambios y cancelaciones del archivo de descuentos (K2) |
| `FlatFileReader` | `Application/Common/BankFiles` (puro) | Inverso de `FlatFileWriter` (K2) |
| `LoanDisbursementLines` | `Application/Lending/Disbursements` | Líneas del archivo de desembolsos (§6.2) |
| `LayoutDeArchivo`, `EscritorPorLayout`, `ValidadorDeLayout` | `Domain/Common/Layouts` (puros) | Layouts regulatorios genéricos (§7) |
| `CatalogoDeLayoutsDeCartera` · `ProyeccionSicsesCartera` · `ProyeccionCentralDeRiesgo` | `Application/Lending/Regulatory` | Layouts embebidos y las filas de cada archivo (K5) |
| `VigenciaDeDocumentos` | `Domain/Lending/Documents` (puro) | Qué documentos anula un reverso (§8.7) |
| `IDocumentoDeCarteraRenderer` | `Application/Lending/Documents` | Puerto del PDF de documentos |
| `NumeradorDeCartera` | `Application/Lending/Common` | Único que toca `LND_NumberSequences` (`NextValue`) |
| Proyecto `IngenIA365ERP.ExternalChannels` | `src/Infrastructure` | `Messaging` (SMS, WhatsApp, `MensajeriaSimulada`), `Securities` (`DepositoDeValoresSimulado` y el proveedor), `CreditBureaus` (`CentralDeRiesgoSimulada` y una por central), `RestrictiveLists` (`CanalDeListasCargadas`, `ListasSimuladas`), `Credentials` (`CredencialesDeCanalExternoEnArchivo`), `Circuit` (`CircuitoDeCanalExterno`) |
| `CanalDeCorreo` | `Application/Common/Messaging` | Adaptador de correo sobre `IEmailSender`; no necesita proyecto externo |
| `DocumentosDeCarteraReport` | `API/Reports` | QuestPDF + QRCoder |

Composición (T71): `AddApplication` registra los defectos «sin canal» de cada registro; **sólo `Program.cs`**
llama `AddExternalChannels`, después de `AddApplication`, y ningún otro archivo de la API usa tipos de
`IngenIA365ERP.ExternalChannels.*`. Lo fija `LosProveedoresExternosSoloLosConoceSuAdaptador` (molde de
`ElProveedorTecnologicoSoloLoConoceSuAdaptador` de la 012).

## 3. Adaptadores externos por puerto (T71–T73)

### 3.1 Lo común a los cuatro puertos

Cada puerto declara `ChannelCode` (estable, es lo que se **sella** en la operación), `Capacidades` y
`ProbarAsync`. Cada registro resuelve el adaptador por el código sellado, nunca por la configuración de hoy:

```csharp
namespace IngenIA365ERP.Application.Common.ExternalChannels;

public sealed record ResultadoExterno<T>(
    ExternalOutcome Outcome,            // Ok | Rejected | InProcess | NotFound | ChannelUnavailable | InvalidData
    T? Valor,                           // sólo con Ok (y con InProcess cuando hay datos parciales)
    string ChannelCode,                 // el que respondió; se sella en la operación
    bool Simulado,                      // true en todo canal SIMULADO; lo lee la guardia (§3.6)
    string? ReferenciaExterna,          // id del proveedor: mensaje, pagaré, consulta
    string? CodigoDelProveedor,         // código crudo, para soporte
    string? Detalle,                    // texto del proveedor, nunca se muestra tal cual al usuario
    IReadOnlyList<ArtefactoExterno> Artefactos,   // { Tipo, NombreDeArchivo, ContentType, Bytes }
    long DurationMs);

public sealed record ContextoDeCanalExterno(
    Guid TenantPublicId,                // la cooperativa resuelta; arma la ruta de la credencial
    string ChannelCode,                 // el sellado (o el de la configuración vigente al crear la operación)
    string? BureauCode,                 // sólo centrales
    CredencialDeCanalExterno? Credencial,         // ya resuelta por ICredencialesDeCanalExterno (§3.4)
    string ClaveDeIdempotencia,         // PublicId de la operación de Cartera que llama
    bool AmbienteDePruebas);            // CanalesExternosOptions.AmbienteDePruebas
```

Los tipos de los puertos externos siguen el precedente de `ResultadoDeCanal` de la 012: nombres en español y
**sin** el sufijo `Dto`, que T1 reserva para los puertos entre módulos.

Reglas para todo adaptador (las verifica su batería de conformidad contra el canal `SIMULADO`):

1. **Nunca lanza por un rechazo de negocio**: un rechazo es `Rejected`; un dato mal formado que el proveedor
   rechaza antes de procesar es `InvalidData`.
2. **Ninguna excepción de transporte sube**. Si la petición **no salió** (DNS, conexión rehusada, credencial
   inválida antes de enviar) es `ChannelUnavailable`. Si **pudo haber llegado** (tiempo agotado después de
   enviar, 5xx, conexión cortada con el cuerpo enviado) es `InProcess`, y se resuelve consultando.
3. **«Ya existe» no es un error**: el adaptador consulta por la clave de idempotencia y devuelve lo que diga la
   consulta.
4. **Sin reintentos HTTP automáticos en las escrituras** (enviar, crear pagaré, endosar, anular). Sólo las
   consultas, que son idempotentes, admiten `Microsoft.Extensions.Http.Resilience`. Los reintentos de las
   escrituras los decide quien llama (la cola, §4.6; la tarea `cartera.pagares`).
5. Tiempos de conexión y totales en `appsettings`, sección `ExternalChannels:{channelCode}` (técnicos, no son
   parámetros de la cooperativa).
6. Un registro con dos adaptadores del mismo código hace fallar el arranque.
7. Un código sellado sin adaptador en el despliegue responde `ChannelUnavailable` con el detalle «el canal
   {código} no está instalado»; nunca lanza.

### 3.2 Los cuatro puertos

| Puerto | Métodos | Capacidades | Canales | E |
|---|---|---|---|---|
| `ICanalDeMensajeria` | `EnviarAsync(MensajeSaliente)` → `EnvioDeMensaje`; `ConsultarEntregaAsync(referenciaExterna)` → `EstadoDeEntrega`; `ProbarAsync` | canal (`MessageChannel`), admite adjuntos, exige plantilla del proveedor (WhatsApp), largo máximo, consulta de entrega | `CanalDeCorreo` (sobre `IEmailSender`), uno por proveedor de SMS y de WhatsApp, `MensajeriaSimulada` | K1 (correo e informativos); SMS y WhatsApp reales con contrato (K4) |
| `IDepositoDeValores` | `CrearPagareAsync(SolicitudDePagare)`, `ConsultarPagareAsync(idExterno)`, `EndosarPagareAsync(idExterno, endosatario)`, `AnularPagareAsync(idExterno, motivo)`, `ProbarAsync` → `EstadoDePagare` | endoso, anulación, certificado de custodia | uno por depósito, `DepositoDeValoresSimulado` | K1 |
| `ICentralDeRiesgo` | `ConsultarAsync(ConsultaACentral)` → `InformeDeCentral`; `ProbarAsync` | puntaje, marcas negativas, artefacto crudo | uno por central (`BureauCode`), `CentralDeRiesgoSimulada` | K1 |
| `IListasRestrictivas` | `ConsultarAsync(ConsultaDeListas)` → `ResultadoDeListas`; `ProbarAsync` | coincidencia por documento, por nombre, versión consultada | `CanalDeListasCargadas` (`CARGA-PROPIA`), `ListasSimuladas`; un proveedor comercial después | K1 |

Ningún puerto **reporta** a centrales: el reporte es el archivo de §7, que una persona carga en el portal de la
central. Si una central ofreciera transmisión directa, sería una capacidad nueva del puerto por contrato, fuera de
esta feature.

Formas de entrada y salida (nombres fijados aquí):

```csharp
// Mensajería (plataforma)
public sealed record MensajeSaliente(Guid MensajePublicId, string Destino, string? Asunto, string Cuerpo,
    string? ProviderTemplateId, IReadOnlyDictionary<string, string>? VariablesDelProveedor,
    IReadOnlyList<ArtefactoExterno> Adjuntos);           // MensajePublicId = referencia ante el proveedor
public sealed record EnvioDeMensaje(string ReferenciaExterna, DateTimeOffset AceptadoEn);
public sealed record EstadoDeEntrega(DeliveryState Estado, DateTimeOffset? En, string? Motivo);
// DeliveryState (fijado aquí): Accepted | Delivered | Failed | Unknown

// Depósito de valores (Cartera)
public sealed record SolicitudDePagare(Guid PromissoryNotePublicId, string NumeroDelCredito, decimal Valor,
    DateOnly FechaDeCreacion, IReadOnlyList<FirmanteDePagare> Firmantes);   // deudor y codeudores (copia I-1)
public sealed record EstadoDePagare(string IdExterno, PromissoryNoteStatus Estado,
    IReadOnlyList<FirmaDePagare> Firmas, DateTimeOffset? CustodiaDesde);

// Centrales (Cartera)
public sealed record ConsultaACentral(Guid PersonaPublicId, string TipoDeDocumento, string NumeroDeDocumento,
    string Proposito, Guid AutorizacionPublicId);
public sealed record InformeDeCentral(int? Puntaje, bool SinHistoria, IReadOnlyList<string> MarcasNegativas,
    DateTimeOffset ConsultadoEn);           // el informe crudo viaja en Artefactos

// Listas (plataforma)
public sealed record ConsultaDeListas(Guid PersonaPublicId, string TipoDeDocumento, string NumeroDeDocumento,
    string NombreCompleto);
public sealed record ResultadoDeListas(ScreeningOutcome Resultado, IReadOnlyList<CoincidenciaEnLista> Coincidencias,
    IReadOnlyList<VersionDeListaConsultada> Versiones);
// ScreeningOutcome (fijado aquí): NoMatch | PossibleMatch | Match
```

### 3.3 Configuración por cooperativa (T72)

`COR_ExternalChannelSettings` (`ExternalChannelSetting`), una fila por vigencia y clase, sin cruces:

| Columna | Tipo | Regla |
|---|---|---|
| `Kind` | `ExternalChannelKind` (`Sms`, `WhatsApp`, `SecuritiesDepository`, `CreditBureau`, `RestrictiveLists`) | El correo no se configura aquí: usa el envío de la plataforma |
| `BureauCode` | texto 20 | Obligatorio con `CreditBureau` (una fila por central); nulo en las demás |
| `ChannelCode` | texto 30 | Debe existir un adaptador con ese código en el despliegue |
| `ValidFrom`, `ValidTo` | fecha | Dos filas de la misma clase (y central) no se cruzan |
| `CredentialKey` | texto 120 | **Sólo la referencia** (§3.4); nulo en `SIMULADO` y `CARGA-PROPIA` |
| `VerifiedAt`, `VerifiedBy` | instante, persona | Los llena `VerifyExternalChannelCredentialCommand` |
| `Reason` | texto 400 | Obligatorio al crear o cerrar una vigencia |

- `ConfigureExternalChannelCommand` (`POST /api/core/external-channels`, `Core.ExternalChannels.Manage`) crea una
  fila y cierra la anterior la víspera. Se audita sin `CredentialKey` en el diff.
- `VerifyExternalChannelCredentialCommand` (`POST /api/core/external-channels/{id}/verify`) resuelve la credencial,
  llama `ProbarAsync` y, si responde `Ok`, fija `VerifiedAt`.
- Pantalla `/admin/canales-externos` (`Core.ExternalChannels.View`).
- **Configurar `SIMULADO` en producción se admite antes y después de activar** (T73): la guardia le quita el efecto
  legal. La pantalla lo muestra con un aviso permanente: «Canal simulado: no tiene validez legal».
- Una operación sella el `ChannelCode` vigente al crearse; los pasos siguientes de esa operación (consultar el
  estado de un pagaré, la entrega de un mensaje) usan el sellado aunque la configuración cambie.

Errores (fijados aquí): `Core.ExternalChannel.Overlaps` (vigencias cruzadas), `Core.ExternalChannel.UnknownChannel`
(código sin adaptador en el despliegue), `Core.ExternalChannel.CredentialMismatch` (la clave guardada no coincide
con la ruta derivada).

### 3.4 Credenciales en un Secret montado (T72)

- Un Secret de Kubernetes por ambiente, **`erp-canales-externos`**, con una clave por cooperativa y canal:
  `{tenantPublicId}.{channelCode}.json`. Se monta como directorio, **sin `subPath`**, en
  `/secrets/canales-externos/`, así el kubelet refresca los cambios sin reiniciar la API. Límite del Secret:
  1 MiB.
- `CredencialesDeCanalExternoEnArchivo` (`ICredencialesDeCanalExterno`) arma la ruta **sólo** con el `PublicId` de
  la cooperativa resuelta (`ICurrentTenantService` o la entrada de `IEjecutorEnCooperativa`) y un `ChannelCode`
  validado con `^[A-Z0-9-]{2,30}$`; nunca con un texto leído de la base. Caché en memoria de 5 minutos por huella
  del archivo. Si `CredentialKey` no coincide con la ruta derivada, `Core.ExternalChannel.CredentialMismatch`.
- El esquema del JSON lo declara cada adaptador y se valida al leer. Ejemplo de un proveedor de SMS:

```json
{ "usuario": "cooflopal-sms", "clave": "…", "remitente": "COOFLOPAL", "urlBase": "https://api.proveedor.example" }
```

- `SIMULADO` y `CARGA-PROPIA` no piden archivo.
- Guion `tools/scripts/crear-secreto-canales-externos.ps1 -Ambiente -Tenant -Canal`, con el patrón de
  `crear-secreto-facturacion-electronica.ps1`: valores por teclado o por STDIN sobre SSH, `kubectl patch secret`
  que agrega o reemplaza sólo esa clave, y en producción exige escribir `PRODUCCION`. Nada queda en disco local,
  en el historial, en `appsettings*` ni en GitOps en claro.
- No se reutiliza `CredencialesEnArchivo` de la DIAN (está acoplada a `ElectronicEmissionSettings` y su caché es
  `internal`). Unificar los dos lectores es un refactor posterior, con sus pruebas.
- Lo fija `LasCredencialesDeCanalesExternosNoTocanLaBase`: ninguna propiedad `Password`, `Token`, `Secret` o
  `ApiKey` en entidades `COR_ExternalChannel*` ni `LND_*`, y ruta derivada del inquilino.

### 3.5 Circuito por cooperativa y canal

`CircuitoDeCanalExterno` cuenta en memoria las fallas seguidas (`ChannelUnavailable` o `InProcess` sin resolver)
por cooperativa y `ChannelCode`. Al llegar al umbral (`Canales.UmbralDeFallas`) abre el circuito, levanta
`Cartera.CanalExternoNoDisponible` (`DedupKey` = cooperativa + canal) y las operaciones siguientes reciben
`ChannelUnavailable` sin llamar al proveedor hasta el sondeo de prueba (`ProbarAsync` cada 5 minutos). Cuenta por
réplica: con dos réplicas, cada una abre su circuito. Nunca lanza hacia quien avisa.

### 3.6 Guardia única del efecto legal (T73)

`GuardiaDeCanalesExternos.Evaluar(efecto, resultado)` es el **único** sitio que decide si un resultado externo
cuenta. `CanalesExternosOptions.AmbienteDePruebas` se fija en `Program.cs` con `!builder.Environment.IsProduction()`;
ninguna cooperativa lo cambia.

```csharp
public enum EfectoLegalDeCanal   // fijado aquí
{
    PromissoryNotePerfection,    // el pagaré desmaterializado queda perfeccionado para desembolsar
    PriorNoticeEvidence,         // el envío cuenta como constancia de aviso previo
    BureauEvidenceForApproval,   // la consulta a centrales sirve de evidencia para aprobar
    RealDelivery,                // el mensaje salió de verdad hacia el titular
}
// VeredictoDeEfectoLegal (fijado aquí): ConEfecto | SinEfecto(Motivo, QueConfigurar)
```

| Efecto | En producción con `SIMULADO` | Fuera de producción con `SIMULADO` | Qué hace la operación |
|---|---|---|---|
| `PromissoryNotePerfection` | Sin efecto | Con efecto | El desembolso que exige pagaré desmaterializado se niega con `Lending.Adapter.SimulatedInProduction` («Configure el depósito de valores en Administración › Canales externos o use pagaré físico») |
| `PriorNoticeEvidence` | Sin efecto | Con efecto | El aviso queda sin constancia; el dato negativo no entra al archivo de centrales (§5) |
| `BureauEvidenceForApproval` | Sin efecto | Con efecto | En una línea con «exige consulta a centrales», la decisión de aprobar se niega con `Lending.Adapter.SimulatedInProduction`; las líneas sin esa marca aprueban sin consulta |
| `RealDelivery` | Sin efecto: el mensaje queda con el evento `Simulated` y no sale | Con efecto (el simulado «entrega» según el dígito) | La cola no reintenta un `Simulated`; el registro de envíos lo muestra como «simulado, no enviado» |

- `CARGA-PROPIA` **no es simulado**: vale en producción.
- Una cooperativa activada sin contratos (el caso de COOFLOPAL) opera así: pagaré físico; aprobación en las líneas
  sin «exige consulta a centrales»; mensajes al deudor por correo (los de SMS y WhatsApp quedan `Simulated`); aviso
  previo con constancia manual (§5.3).
- Fuera de producción **todo** documento y todo artefacto llevan la leyenda «AMBIENTE DE PRUEBAS – SIN VALIDEZ».
- El canal simulado marca «SIMULADO» en sus referencias (`SIM-…`) y en sus artefactos, recuerda por cooperativa lo
  que respondió (para que la consulta siguiente sea coherente) y tiene un retardo configurable.

### 3.7 El comportamiento del canal `SIMULADO`

Decide por el **último dígito del documento** de la persona (T71, §2.22):

| Puerto | 1 | 2 | 3 | 5 | 8 | 9 | Otro |
|---|---|---|---|---|---|---|---|
| Mensajería | `InvalidData` (número o correo inválido) | `Ok` y `Delivered` en la consulta siguiente | `Ok` y `Failed` en la consulta siguiente | `ChannelUnavailable` | — | — | `Ok` y `Delivered` |
| Depósito de valores | `Rejected` (firma rechazada) | — | `InProcess` (firma pendiente; `Signed` a la tercera consulta) | `ChannelUnavailable` | — | — | `Ok`, `Signed` en la consulta siguiente |
| Central | `Ok` con reporte negativo | `Ok` sin historia | `Ok` con puntaje bajo | `ChannelUnavailable` | — | — | `Ok` sin marcas |
| Listas (`ListasSimuladas`) | — | — | — | — | `PossibleMatch` | `Match` | `NoMatch` |

### 3.8 Cada puerto en su operación

**Pagaré desmaterializado (`IDepositoDeValores`, FR-044).**

- El pagaré es una garantía de la solicitud y del crédito (`LND_PromissoryNotes`, `Kind` `Physical` o
  `Dematerialized`). El desmaterializado sella `CustodianChannelCode` y `ExternalNoteId`.
- Estados que informa el adaptador: `PromissoryNoteStatus` (fijado aquí) `Created`, `PendingSignature`, `Signed`,
  `InCustody`, `Endorsed`, `Voided`. El depósito notifica a los firmantes; el ERP no les escribe.
- La tarea `cartera.pagares` consulta cada pagaré en `Created` o `PendingSignature` y registra el cambio;
  pasados los días de la vigencia de la aprobación sin firma, alerta `Cartera.PagarePendienteDeFirma`.
- El físico pasa a `Signed` cuando el asesor adjunta el pagaré firmado (`LendingGuaranteeDocument`).
- Perfeccionado = `Signed` o `InCustody`, y con efecto legal (§3.6). El desembolso lo exige si la línea lo pide
  (FR-045).
- Endoso y anulación por el adaptador; la anulación sólo cuando la cancelación queda en firme (FR-082).
- `Lending.PromissoryNotes.Manage`.

**Consulta a centrales (`ICentralDeRiesgo`, FR-036, FR-006).**

- `QueryCreditBureauCommand` (`POST /api/lending/bureau-queries`, `Lending.CreditBureau.Query`) exige una autorización
  vigente con finalidad `CreditBureauQuery` y con evidencia (`Lending.Authorization.Missing`,
  `Lending.Authorization.WithoutEvidence`); sin ella, la operación se bloquea y explica cómo obtenerla.
- Guarda `LND_BureauQueries`: persona, propósito, central, canal sellado, autorización usada, resumen (puntaje y
  marcas negativas), bandera de simulado, y el informe crudo como adjunto `LendingBureauReport` (no borrable, se lee
  con `Lending.CreditBureau.ReadReports`).
- La consulta vale `Centrales.DiasDeVigenciaDeConsulta` días: dentro de ese plazo, la solicitud reutiliza la última en
  lugar de consultar otra vez.
- Es una **lectura sensible**: `LendingAuditEmitter` la audita (T67).

**Listas restrictivas (`IListasRestrictivas`, FR-036, FR-049, T70).**

- `CanalDeListasCargadas` (`CARGA-PROPIA`) consulta la carga vigente de `COR_RestrictiveListLoads` y
  `COR_RestrictiveListEntries` (plantilla `compliance.restrictive-lists`, `plantillas.md` §4):
  - coincidencia exacta por documento normalizado → `Match`;
  - posible por nombre con `CoincidenciaDeNombres` (puro, en memoria, porque SQL Server no tiene `pg_trgm`), sobre
    el umbral `Listas.UmbralDeCoincidenciaDeNombre` → `PossibleMatch`.
- Cartera consulta al radicar, antes de aprobar, al otorgar cupo y al consumir cupo con una venta. Cada consulta
  queda en `LND_ScreeningResults`: persona, solicitud, cupo o venta, versión de las listas consultadas, resultado y
  liberación.
- Toda coincidencia (también la posible) **bloquea** con `Compliance.RestrictiveList.Match` y alerta
  `Cartera.CoincidenciaEnListas` al oficial de cumplimiento.
- Sólo libera quien tenga `Compliance.RestrictiveLists.Release`, con motivo (`ReleaseScreeningMatchCommand`,
  `POST /api/lending/screening-results/{id}/release`), y nunca quien radicó (`Lending.Segregation.Conflict`). Una
  coincidencia en listas nunca se aprueba por excepción (`NoExceptuables`).
- Al aplicar una carga nueva de listas, la tarea `cartera.re-escaneo-de-listas` (diaria, C90) vuelve a
  consultar a deudores y codeudores vivos. Cada hallazgo deja su `ScreeningResult`, el bloqueo automático de FR-060
  (que impide originar y vender, no toca el cobro de lo vivo) y la alerta al oficial (I12).

**Mensajería (`ICanalDeMensajeria`)**: §4.

### 3.9 Baterías de conformidad

Una por puerto, corrida contra el simulado y contra cada adaptador real en su ambiente de pruebas:
`ConformidadDeMensajeria`, `ConformidadDelDepositoDeValores`, `ConformidadDeCentral` y
`ConformidadDeListasRestrictivas` (fijada aquí). Cada una comprueba las siete reglas de §3.1, que el resultado
traiga el `ChannelCode` y que `Simulado` sea `true` sólo en el simulado.

## 4. Mensajes al deudor: la cola y la Ley 2300 (T74–T76)

### 4.1 Quién recibe y a dónde

- **Sólo** el deudor y los codeudores obligados del crédito (FR-122). Otro destinatario es
  `Lending.DebtorMessage.RecipientNotObligated`. **Nunca** una referencia personal: ningún código de
  `Application/Lending/Collections` ni de la cola lee `COR_References` (`LaCobranzaNuncaContactaReferencias`).
- El destino (correo, celular, dirección) se lee del maestro de personas **al despachar**, no al programar, y su copia
  queda en el evento `Sent` (I-1): así un cambio de contacto antes del envío se respeta y el registro dice a dónde
  salió de verdad.
- `COR_Notifications` no se usa para deudores: su destinatario es un usuario y es mutable (T74).

### 4.2 El registro: `LND_DebtorMessages` y `LND_DebtorMessageEvents`

El mensaje es **inmutable desde que nace** (I-5): lo que cambia es su historia, que es de sólo inserción.

`LND_DebtorMessages` (`DebtorMessage`):

| Columna | Qué guarda |
|---|---|
| `PublicId` | También es la referencia ante el proveedor |
| `LoanId` (nulo en `DocumentDelivery` general) · `PersonId` | Crédito y persona destinataria (persona del maestro) |
| `Purpose` · `Channel` | `DebtorMessagePurpose` · `MessageChannel` (`Email`, `Sms`, `WhatsApp`, `Letter`) |
| `TemplatePublicId` · `TemplateVersion` | La versión exacta de la plantilla usada (§4.7) |
| `Subject` · `Body` | Copia renderizada al nacer; nunca se vuelve a renderizar |
| `RequestedFor` | Instante local pedido (inmediato, programado o de campaña) |
| `Origin` · `OriginPublicId` | `DebtorMessageOrigin` (fijado aquí): `Operation`, `Process`, `PriorNotice`, `Campaign`, `Manual`, `IssuedDocument` |
| `CampaignId` | Campaña (K4), si la hay |
| `AuthorizationPublicId` | La autorización de canal usada (nula en `DocumentDelivery` y `Letter`) |
| `ChannelCode` | Adaptador sellado al nacer |
| `IdempotencyKey` | `{origen}:{entidad}:{propósito}:{fecha}`, único (T74) |
| `AttachmentPublicIds` | Adjuntos (el PDF de un documento emitido) |

`LND_DebtorMessageEvents` (`DebtorMessageEvent`, H):

| Columna | Qué guarda |
|---|---|
| `Kind` | `DebtorMessageEventKind`: `Queued`, `Deferred`, `Excluded`, `Sent`, `Delivered`, `Failed`, `Simulated`, `Cancelled` |
| `OccurredAt` · `Actor` | Instante UTC y actor (persona o el proceso) |
| `Reasons` | Lista de `ContactWindowReason` (fijado aquí, §4.5) en `Deferred` y `Excluded` |
| `NextAttemptAt` | En `Deferred`: el instante siguiente permitido |
| `DestinationAddress` | En `Sent` y `Simulated`: el teléfono, correo o dirección al que salió. La copia va en el evento, no en el mensaje, que no cambia tras nacer (C57) |
| `ExternalReference` · `ProviderCode` · `Detail` | Respuesta del adaptador |
| `Attempt` | Número de intento de envío |

El estado que muestra la pantalla es el del último evento. Transiciones:

| Desde | Evento | Quién | Cuándo |
|---|---|---|---|
| (nace) | `Queued`, `Deferred` o `Excluded` | quien lo origina | Según el motor al programar (§4.5) |
| `Queued`, `Deferred` | `Deferred` | `cartera.mensajes` | El motor difiere al despachar, o el canal no está disponible |
| `Queued`, `Deferred` | `Excluded` | `cartera.mensajes` | El motor excluye al despachar (autorización revocada, fallecimiento, insolvencia, sin destino) |
| `Queued`, `Deferred` | `Sent` | `cartera.mensajes` | El adaptador respondió `Ok` o `InProcess` |
| `Queued`, `Deferred` | `Simulated` | `cartera.mensajes` | El resultado vino de `SIMULADO` (§3.6) |
| `Queued`, `Deferred` | `Failed` | `cartera.mensajes` | `Rejected`, `InvalidData` o reintentos agotados |
| `Queued`, `Deferred` | `Cancelled` | una persona o `cartera.mensajes` | Cancelación con motivo, o la causa desapareció (el crédito se puso al día antes de enviar un mensaje de cobranza) |
| `Sent`, `Simulated` (fuera de producción) | `Delivered` o `Failed` | `cartera.entregas` | Consulta de entrega |

`Excluded`, `Failed`, `Cancelled` y `Delivered` son finales; `Simulated` es final en producción. Un `Failed` no se
reenvía solo: el gestor programa otro mensaje.

### 4.3 Propósitos

| `DebtorMessagePurpose` | Ejemplos | Ventana horaria y tope diario | Canal semanal | Autorización | E |
|---|---|---|---|---|---|
| `Collection` | Recordatorio preventivo, mora administrativa, prejurídica | Sí | Sujeto al canal fijado | Canal autorizado **con evidencia** | K4 |
| `PriorNotice` | Aviso previo de la Ley 1266 (§5) | Sí | Sujeto al canal fijado | Canal autorizado con evidencia (en `Letter`, dirección registrada) | K1 datos / K4 envío |
| `CollectionFeeNotice` | Gasto de cobranza informado (FR-121) | Sí | Sujeto al canal fijado | Canal autorizado con evidencia | K4 |
| `Informative` | Abono a capital aplicado (D24), cuota no descontada por nómina (FR-112), no aplicado de pagaduría y paso a caja (FR-115, FR-116) | Sí | **No fija ni respeta** el canal semanal (I2) | Canal autorizado (sin evidencia basta) | K1 |
| `DocumentDelivery` | Envío de un documento emitido pedido por el titular (§8) | **Exento** (I3) | Exento | Correo registrado y autorización de tratamiento general | K1 |

Una autorización migrada sin evidencia deja el crédito vivo y cobrable, y bloquea **sólo** la consulta y el reporte a
centrales y los mensajes de cobranza (`Collection`, `PriorNotice`, `CollectionFeeNotice`) hasta obtenerla (FR-006).

### 4.4 Quién encola

Un mensaje nace **en la misma transacción** que lo que lo motiva (regla de T5): el abono aplicado por el contrato
(dentro de la unidad de trabajo del recaudador), el no descontado de la nómina propia, la tanda del ciclo diario que
mueve una etapa (K4), el aviso previo, la campaña (K4), el envío manual o la emisión de un documento. Si la causa se
reversa, el mensaje que todavía no salió se cancela en la misma transacción del reverso.

Las cuotas vencidas entre el corte de la migración y el registro de la activación **no** generan mensajes con fecha
anterior al registro (FR-142): la cola sólo programa desde la activación.

### 4.5 El motor `VentanaLegalDeContacto` (puro, `Domain/Lending/Collections`)

```csharp
public static class VentanaLegalDeContacto
{
    public static DecisionDeContacto Evaluar(
        SolicitudDeContacto solicitud,     // instante local pedido, canal, propósito, inmediato o programado
        ReglasDeContacto reglas,           // horario efectivo por día, contactos por día, días no hábiles
        HistorialDeContactos historial,    // la semana calendario del titular: envíos y gestiones con contacto directo
        EstadoDelTitular titular);         // autorizaciones por canal y evidencia, fallecido, insolvencia, cobro suspendido, obligado
}
// DecisionDeContacto: Permitido(En) | Diferido(Siguiente, Motivos) | Excluido(Motivo)   (nombres fijados aquí)
```

**Entradas.**

- **Horario efectivo** por día = techo nacional ∩ restricción de la cooperativa. El techo son los valores `LIMITE`
  de `CatalogoDeLimitesLegales` que fija `data-model.md` §12.5 (C71), con su semilla «pendiente de validar»:

  | Código `LIMITE` | Unidad · estricto | Semilla | Acota |
  |---|---|---|---|
  | `COBRANZA_HORA_INICIO_LV` | `Horas` · `Mayor` | 7 | `Cobranza.HorarioLunesAViernes` |
  | `COBRANZA_HORA_FIN_LV` | `Horas` · `Menor` | 19 | `Cobranza.HorarioLunesAViernes` |
  | `COBRANZA_HORA_INICIO_SAB` | `Horas` · `Mayor` | 8 | `Cobranza.HorarioSabado` |
  | `COBRANZA_HORA_FIN_SAB` | `Horas` · `Menor` | 15 | `Cobranza.HorarioSabado` |
  | `COBRANZA_CONTACTOS_DIA` | `Veces` · `Menor` | 1 | `Cobranza.ContactosPorDia` |

  La cooperativa sólo restringe con los parámetros `Cobranza.HorarioLunesAViernes` (`HH:mm-HH:mm`, defecto
  `07:00-19:00`), `Cobranza.HorarioSabado` (`08:00-15:00`) y `Cobranza.ContactosPorDia` (1). Un valor de la
  cooperativa menos estricto lo rechaza el escritor de parámetros (`Lending.Parameter.BelowLegalFloor`, T42).
- **Días no hábiles**: domingos y festivos **nacionales** por `ICalendarioHabil` (T27); los festivos «Manual» de
  Nómina no cuentan (E5).
- **Semana calendario**: lunes a domingo, hora de Colombia (`HoyLocal`, T9).
- **Historial**: los eventos `Sent` y `Delivered` (y `Simulated` fuera de producción) de la semana, más las gestiones
  con `IsDirectContact = true` (K4, §4.8). Un `Simulated` en producción no contactó a nadie y no cuenta.

**Reglas, en este orden** (la primera exclusión gana; después, la exención; después, el aplazamiento):

| # | Regla | Resultado |
|---|---|---|
| 1 | El destinatario no es deudor ni codeudor obligado | `Excluido(RecipientNotObligated)` |
| 2 | Fallecido con reclamación al seguro `InProcess` (FR-097, T102): ningún mensaje al fallecido; a los codeudores, nada de cobranza | `Excluido(DebtorDeceased)` |
| 3 | Insolvencia en `Negotiating` o cobro suspendido (FR-098, FR-124): nada de cobranza (`Collection`, `PriorNotice`, `CollectionFeeNotice`) | `Excluido(Insolvency \| CollectionSuspended)` |
| 4 | El canal no está autorizado para el titular (salvo `DocumentDelivery` y `Letter`: la carta no exige autorización de canal, sólo dirección registrada y autorización de tratamiento general; `Call` y `Visit` no son canales de mensaje sino gestiones de K4, C90) | `Excluido(ChannelNotAuthorized)` |
| 5 | Propósito de cobranza con autorización sin evidencia | `Excluido(AuthorizationWithoutEvidence)` |
| 6 | El titular no tiene destino para ese canal en el maestro | `Excluido(NoDestination)` |
| 7 | `DocumentDelivery` pedido por el titular | `Permitido(ahora)` (exento de 8 a 10) |
| 8 | Domingo, festivo nacional o fuera del horario efectivo | `Diferido` a la apertura siguiente (`Sunday`, `Holiday`, `OutsideHours`) |
| 9 | Ya hubo `ContactosPorDia` contactos ese día (los informativos cuentan) | `Diferido` a la apertura del día hábil siguiente (`DailyLimitReached`) |
| 10 | Hubo un contacto directo esa semana por otro canal, y el propósito es de cobranza | `Diferido` a la apertura del lunes siguiente (`WeeklyChannelLocked`) |
| 11 | Ninguna de las anteriores | `Permitido(máx(pedido, ahora))` |

El aplazamiento busca el primer instante que cumple 8, 9 y 10 a la vez, día por día, hasta 14 días; si no lo
encuentra, `Excluido(NoWindow)`. `ContactWindowReason` (fijado aquí, `Domain/Enums/Lending`): `RecipientNotObligated`,
`DebtorDeceased`, `Insolvency`, `CollectionSuspended`, `ChannelNotAuthorized`, `AuthorizationWithoutEvidence`,
`NoDestination`, `Sunday`, `Holiday`, `OutsideHours`, `DailyLimitReached`, `WeeklyChannelLocked`, `NoWindow` y el
técnico `ChannelUnavailable` (lo pone el despachador, no el motor).

**Cuándo se evalúa**: al programar y otra vez al despachar, porque un contacto directo, una revocación o un pago
registrados después cambian la respuesta.

- **Envío manual inmediato** fuera de ventana: se rechaza con `Lending.DebtorMessage.OutsideLegalWindow` y
  `nextAllowedAt`; por canal semanal, `Lending.DebtorMessage.WeeklyChannelUsed`; por canal no autorizado,
  `Lending.DebtorMessage.ChannelNotAuthorized`.
- **Programado o de campaña**: se difiere con el evento `Deferred` (o se excluye con `Excluded`) y queda la
  constancia del motivo (US12-4).

Casos dorados en `Domain.Tests/Lending/Collections/Casos/*.json` (molde de los de Nómina). Ejemplo, el de US12-2:

```jsonc
{
  "caso": "contacto-directo-por-telefono-bloquea-whatsapp-esa-semana",
  "reglas": {
    "horario": { "lunesAViernes": ["07:00", "19:00"], "sabado": ["08:00", "15:00"] },
    "contactosPorDia": 1,
    "noHabiles": ["2026-11-16"]                      // festivo nacional: Independencia de Cartagena, trasladado al lunes
  },
  "titular": { "obligado": true, "canalesAutorizados": ["Sms", "WhatsApp", "Email"], "conEvidencia": true },
  "historial": [
    { "en": "2026-11-09T10:15", "canal": "Phone", "directo": true }   // lunes, gestión del gestor (K4)
  ],
  "solicitud": { "pedido": "2026-11-11T09:00", "canal": "WhatsApp", "proposito": "Collection", "inmediato": true },
  "esperado": { "decision": "Diferido", "siguiente": "2026-11-17T07:00", "motivos": ["WeeklyChannelLocked"] }
}
```

(La semana siguiente empieza el lunes 16, que es festivo, así que la apertura es el martes 17. Como la solicitud
es inmediata, la ruta responde 422 `Lending.DebtorMessage.WeeklyChannelUsed` con `nextAllowedAt = 2026-11-17T07:00`.)

### 4.6 Programar, despachar y consultar la entrega (T75)

**`cartera.mensajes`** (`ITareaProgramada`, cada minuto, por cooperativa; sale si Cartera no está activada):

1. Toma hasta 200 mensajes vencidos: último evento `Queued` con `RequestedFor ≤ ahora`, o `Deferred` con
   `NextAttemptAt ≤ ahora`, en orden de `RequestedFor`.
2. Vuelve a evaluar con el motor. Un mensaje de cobranza cuyo crédito ya no tiene saldo vencido se cancela con motivo
   «el crédito se puso al día».
3. Lee el destino del maestro, pregunta a la guardia por `RealDelivery` y llama `EnviarAsync` del adaptador sellado.
4. Registra el resultado con `RegisterDebtorMessageAttemptCommand` (un comando por mensaje e intento, idempotente por
   `PublicId` + `Attempt`):

| `ExternalOutcome` | Evento | Sigue |
|---|---|---|
| `Ok` | `Sent` con `DestinationAddress` y `ExternalReference` | `cartera.entregas` consulta si el canal lo admite |
| `InProcess` | `Sent` sin referencia confirmada | `cartera.entregas` lo resuelve |
| `Ok` de `SIMULADO` | `Simulated` | En producción, final; fuera, `cartera.entregas` según el dígito |
| `ChannelUnavailable` | `Deferred(ChannelUnavailable)` a la ventana permitida siguiente | Tras `Mensajes.Reintentos` intentos, `Failed` |
| `Rejected`, `InvalidData` | `Failed` | Alerta `Cartera.MensajeAlDeudorFallido` al jefe de cartera (K1) o al gestor asignado (K4) |

**`cartera.entregas`** (periódica): para los `Sent` sin desenlace de las últimas 72 horas cuyo canal declara consulta
de entrega, llama `ConsultarEntregaAsync` y registra `Delivered` o `Failed`. El correo no tiene consulta: su `Sent` es
el último evento.

**Rutas** (`api.md` las detalla):

| Ruta | Permiso | Qué hace |
|---|---|---|
| `GET /api/lending/debtor-messages?loan=&person=&purpose=&status=&from=&to=` | `Lending.DebtorMessages.Read` | Registro de envíos, filtrado por el alcance de oficina y línea |
| `GET /api/lending/debtor-messages/{id}` | `Lending.DebtorMessages.Read` | Mensaje con su historia |
| `POST /api/lending/debtor-messages` | `Lending.DebtorMessages.Send` | `SendDebtorMessageCommand` (fijado aquí): manual, inmediato o programado |
| `POST /api/lending/debtor-messages/{id}/cancel` | `Lending.DebtorMessages.Cancel` | `CancelDebtorMessageCommand` (fijado aquí), con motivo; sólo en `Queued` o `Deferred` |
| `GET /api/lending/debtor-messages/contact-window?person=&channel=&purpose=&at=` | `Lending.DebtorMessages.Read` | `GetContactWindowQuery` (fijado aquí): la decisión del motor, para la agenda y antes de contactar |

```jsonc
// POST /api/lending/debtor-messages   (Idempotency-Key obligatoria)
{ "loanPublicId": "1b8e…", "personPublicId": "77c0…", "purpose": "Collection",
  "channel": "Sms", "templateCode": "MORAADMIN", "scheduledFor": null }

// 422 si es inmediato y el motor no lo permite
{ "code": "Lending.DebtorMessage.OutsideLegalWindow",
  "message": "La Ley 2300 no permite contactar a esta persona ahora. Puede programarlo para el 17/11/2026 a las 7:00 a. m.",
  "data": { "nextAllowedAt": "2026-11-17T07:00:00-05:00", "reasons": ["Holiday"] } }
```

Pantalla `/cartera/mensajes` (K1): registro de envíos con su historia, envío manual y cancelación. Vista de informe
`debtor-messages` (K1).

### 4.7 Plantillas de mensaje (T76)

Columnas aditivas en `COR_NotificationTemplates` (§2.6 de las transversales): `Code`, `Module` (`"Lending"`),
`Purpose`, `Version`, `ValidFrom`, `ValidTo`, `ProviderTemplateId`, `Variables`, `IsSeeded`. Reglas:

- Una plantilla es (`Code`, `Channel`, `Version`). Cambiarla **crea una fila nueva** con `Version + 1` y cierra la
  anterior la víspera; el mensaje sella `PublicId` y versión. Lo que ya salió nunca cambia.
- `RenderizadorDePlantillasDeMensaje` sustituye `{{Variable}}` **sólo** desde la lista blanca del propósito, sin
  expresiones; una variable fuera de la lista es error al guardar la plantilla. Escapa el HTML del correo.
- SMS: avisa el largo (160 caracteres en GSM-7, 70 en UCS-2) y el número de segmentos.
- WhatsApp: sin `ProviderTemplateId` (la plantilla aprobada por Meta que la cooperativa registró ante su proveedor) no
  se usa ese canal (I15); el texto de la plantilla debe coincidir con la aprobada.
- Rutas `GET/POST /api/lending/message-templates` (`Lending.MessageTemplates.View` / `.Manage`;
  `CreateMessageTemplateVersionCommand`, fijado aquí); pantalla `/cartera/plantillas-de-mensaje`.

Lista blanca por propósito (variables fijadas aquí; la hoja de ayuda de la pantalla sale de esta lista):

| Propósito | Variables |
|---|---|
| Todas | `NombreDelTitular`, `NombreDeLaCooperativa`, `TelefonoDeLaCooperativa`, `NumeroDelCredito`, `FechaDeCorte` |
| `Collection` | `ValorVencido`, `DiasDeMora`, `CuotasVencidas`, `ValorParaPonerseAlDia` |
| `PriorNotice` | `ValorDeLaObligacion`, `ValorVencido`, `FechaMasTempranaDeReporte`, `Centrales` |
| `CollectionFeeNotice` | `ValorDelGasto`, `Etapa`, `FechaDeCausacion` |
| `Informative` | `ValorAplicado`, `ModalidadAplicada`, `NuevaCuota`, `NuevoPlazo`, `MotivoDeNoDescuento`, `FormaDePagoNueva` |
| `DocumentDelivery` | `ClaseDeDocumento`, `NumeroDelDocumento`, `CodigoDeVerificacion` |

Los valores de un mensaje de cobranza se escriben «a la fecha {{FechaDeCorte}}», porque el cuerpo se sella al nacer y
el envío puede diferirse.

Semillas «pendiente de validar» (código de catálogo de hasta 10 caracteres según `CodigoDeCatalogo`, C90; una fila
por canal): `AVISOPREV` (aviso previo),
`RECORDPREV` (recordatorio preventivo), `MORAADMIN` (mora administrativa), `PREJURID` (prejurídica), `NOAPLICPAG`
(no aplicado de pagaduría y de la nómina propia), `PASOACAJA` (paso a caja), `ABONOCAP` (abono a capital aplicado,
D24), `GASTOCOBR` (gasto de cobranza informado), `ENVIODOC` (envío de documento).

### 4.8 Registro de contactos directos (K4)

La Ley 2300 cuenta los contactos directos, que no son mensajes: son las **gestiones** del gestor.

- `LND_CollectionActions` (`CollectionAction`), inmutable: fecha y hora, canal (`CollectionActionChannel`, fijado aquí:
  `Phone`, `InPerson`, `Email`, `Sms`, `WhatsApp`, `Letter`, `Visit`), persona contactada (deudor o codeudor; nunca
  una referencia, `Lending.DebtorMessage.RecipientNotObligated`), `IsDirectContact`, resultado (catálogo de la
  cooperativa), promesa opcional y la decisión del motor vigente al registrar.
- Una corrección es una gestión nueva que anula la anterior con motivo; la anulada deja de contar en el historial.
- `RecordCollectionActionCommand` (fijado aquí), `POST /api/lending/collection-actions`,
  `Lending.Collections.RecordActivity`. Registrar no pasa por la ventana (es un hecho que ya ocurrió); la pantalla de
  gestión muestra antes de contactar lo que dice `contact-window`.
- Desde K4, el motor suma estas gestiones a su historial: un contacto directo por teléfono fija el canal «teléfono»
  para la semana y la cola difiere los mensajes de cobranza por otros canales.

### 4.9 Cómo se comprueba SC-011

Sobre el registro, nunca sobre los logs: para cada evento `Sent` o `Delivered` de un propósito sujeto a la ley se
reconstruye la entrada del motor con la información conocida en ese instante (historial, autorizaciones, horario
vigente) y se exige `Permitido`. Lo hace la e2e `NingunMensajeSaleFueraDeLaVentanaTests` (fijada aquí), que programa
mensajes para un domingo, un festivo, después de las 19:00, después del tope diario, después de un contacto directo y
por un canal revocado.

## 5. Aviso previo al reporte negativo, por episodio y con constancia (T77)

### 5.1 Episodio

Un **episodio de mora** de un crédito empieza el día en que vence sin pagarse una cuota estando el crédito al día, y
termina el día en que el crédito queda sin saldo vencido. El siguiente atraso abre un episodio nuevo, que exige un
aviso nuevo (US12-5). Se identifica por (crédito, fecha de vencimiento de la primera cuota impaga del episodio); lo
calcula la reconstrucción (T24), no una marca guardada.

### 5.2 `LND_PriorNotices` (K1 datos, K4 proceso)

| Columna | Qué guarda |
|---|---|
| `LoanId` · `EpisodeStartDate` | Crédito y episodio; único por los dos |
| `RequiredCommunications` | 1, o 2 en días distintos si el saldo de la obligación está bajo `AvisoPrevio.UmbralDosComunicaciones` |
| `Origin` | `Process` (la etapa de cobranza que lo dispara, K4), `Manual` o `Migration` |
| `FirstEvidenceAt` · `LastEvidenceAt` | Primera y última constancia válida |
| `EarliestReportDate` | `LastEvidenceAt` (fecha local) + `AvisoPrevio.DiasMinimos` días calendario; nunca menos que el límite `AVISO_PREVIO_DIAS` (20) |
| `MigratedReference` | En lo migrado: la referencia del envío que trae la carga (FR-139) |

Cada comunicación es un `DebtorMessage` con propósito `PriorNotice` y `Origin = PriorNotice`. El aviso nace en K1
para recibir lo migrado; desde K4 lo crea la acción «aviso previo» de la etapa de cobranza configurada (FR-117), y
también se envía a mano (`Lending.PriorNotices.Send`).

### 5.3 Constancia

Cuenta como constancia de una comunicación, si la guardia responde `ConEfecto` para `PriorNoticeEvidence` (§3.6):

1. El evento `Sent` del mensaje (correo, SMS o WhatsApp), con su referencia del proveedor. Para el correo, la
   aceptación del servidor de envío.
2. En correo físico (`Letter`), el registro manual con la guía de envío adjunta: `RegisterPriorNoticeEvidenceCommand`
   (fijado aquí), `POST /api/lending/prior-notices/{id}/evidence`, `Lending.PriorNotices.RegisterEvidence`, adjunto
   `LendingPriorNoticeEvidence` (no se borra una vez registrado).
3. En lo migrado, la referencia de envío que trae la carga (`plantillas.md` §5, parte `prior-notices`).

Una constancia de `SIMULADO` no vale en producción. Dos comunicaciones el mismo día cuentan como una.

### 5.4 Estado (calculado, no es columna)

`PriorNoticeStatus` (fijado aquí): `AwaitingEvidence` (sin constancia), `Partial` (una de dos), `Complete`
(constancias completas; reportable desde `EarliestReportDate`), `EpisodeClosed` (el crédito se puso al día: el aviso
ya no sirve para otro episodio).

### 5.5 Efecto en el archivo de centrales (K5, FR-137, SC-012)

- Un dato negativo entra al archivo sólo si su episodio tiene un aviso `Complete` y el período del archivo es igual o
  posterior a `EarliestReportDate`.
- Lo que no cumple queda **fuera del archivo y listado** con su motivo en la generación (`ExcludedJson`) y en la
  vista `bureau-exclusions`.
- Un dato negativo reportado sin aviso (lo trae la migración en la parte `bureau-reports`, o lo reportó un archivo
  anterior) se **retira** en el archivo siguiente.
- Si un episodio llega al punto de reporte sin constancia, se alerta `Cartera.AvisoPrevioSinConstancia` al jefe de
  cartera.

```jsonc
// GET /api/lending/prior-notices?loan=1b8e…   (Lending.PriorNotices.Read)
{ "publicId": "5d2a…", "loanNumber": "CR-004512", "episodeStartDate": "2026-11-06",
  "requiredCommunications": 2, "status": "Partial",
  "communications": [ { "messagePublicId": "c41f…", "channel": "Email", "event": "Sent",
                        "at": "2026-11-12T08:02:10-05:00", "evidence": true } ],
  "earliestReportDate": null }
```

## 6. Archivos planos con el motor común (T78)

### 6.1 El motor y lo que se le agrega

Un solo motor para todos los módulos (D-42 de la 010): el formato es **dato** en `COR_BankFileFormats` y sus campos
(`BankId` nulo = genérico, `Scope`, `Kind` de ancho fijo o delimitado, codificación, formato de monto, patrón de
nombre, vigencia), `FlatFileWriter` es puro, escribe y devuelve el SHA-256, y un formato que ya generó archivos no
cambia de estructura (`InUse`: se cierra su vigencia y se crea otro). Cartera agrega:

| Pieza | Cambio | E |
|---|---|---|
| `BankFileScope` | `LoanDisbursement = 4` | K1 |
| `BankFileScope` | `PayrollDeductionRequest = 5`, `PayrollDeductionResult = 6`, marcados `SeleccionPorReferencia` | K2 |
| `SolapadoAsync` | No aplica el cruce de vigencias a los ámbitos `SeleccionPorReferencia`: varios formatos genéricos de ida y de vuelta conviven, uno por pagaduría, y cada convenio elige el suyo por referencia. Lo fija `LosFormatosElegidosPorReferenciaNoCompitenPorVigencia` | K2 |
| `BankFieldSource` | `PayerEmployeeId`, `LoanNumber`, `InstallmentNumber`, `DueDate`, `NoveltyKind`, `PastDueAmount`, `PeriodYear`, `PeriodMonth`, `LibranzaDate` (ida); `ResultCode`, `DeductedAmount`, `DeductionDate` (vuelta). Los números del enum los fija `data-model.md` | K2 |
| `BankFileFormatValidator` | Gana la lista de orígenes admitidos **por ámbito** (tabla de abajo): un origen fuera de su ámbito es error al guardar el formato | K1 |
| `FlatFileReader` | Puro, en `Application/Common/BankFiles`, inverso del escritor: `Leer(formato, bytes)` → `FlatFileReadResult` (fijado aquí) con una fila por registro de detalle (valores por origen) y los errores por línea y campo. Sirve la vista previa del formato de vuelta y lo reutilizará Recaudo | K2 |
| Pantalla `/maestros/formatos-bancarios` | Filtra por ámbito; en los ámbitos de pagaduría ofrece la vista previa con un archivo de prueba | K1/K2 |

Orígenes admitidos por ámbito (además de los de todo registro: `Constant`, `Blank`, `CompanyNit`, `CompanyNitDv`,
`CompanyName`, `SourceAccount*`, `PaymentDate`, `GenerationDate`, `GenerationTime`, `Sequence`, `BatchReference`,
`LineNumber`, `LineCount`, `TotalAmount`):

| Ámbito | Orígenes del detalle |
|---|---|
| `LoanDisbursement` | `Payee*`, `Amount`, `Concept`, `LoanNumber` |
| `PayrollDeductionRequest` | `PayeeDocumentType`, `PayeeDocument`, `PayeeFullName`, `PayeeFirstNames`, `PayeeLastNames`, `PayerEmployeeId`, `LoanNumber`, `InstallmentNumber`, `DueDate`, `Amount`, `PastDueAmount`, `NoveltyKind`, `PeriodYear`, `PeriodMonth`, `LibranzaDate` |
| `PayrollDeductionResult` | `PayeeDocument`, `PayerEmployeeId`, `LoanNumber`, `InstallmentNumber`, `ResultCode`, `DeductedAmount`, `DeductionDate`, `PeriodYear`, `PeriodMonth` |

### 6.2 Archivo de transferencia de desembolsos (K1, FR-047)

**Qué entra.** Órdenes de giro (`LND_DisbursementOrders`) en estado `Issued`, de forma transferencia (a la cuenta del
deudor o de un tercero), marcadas al desembolsar para pagarse por archivo (`PaidByFile = true`, C10) y que no estén en
otro archivo no anulado.

**Generar.** `GenerateDisbursementFileCommand` (fijado aquí), `POST /api/lending/disbursement-files`,
`Lending.DisbursementFiles.Generate`, con `Idempotency-Key`:

```jsonc
{ "formatPublicId": "a3f0…",               // ámbito LoanDisbursement, vigente a paymentDate
  "sourceAccountPublicId": "6c21…",        // una de IContabilidadParaCartera.CuentasBancariasAsync
  "paymentDate": "2026-12-02",
  "orderPublicIds": ["0d5e…", "91aa…"] }   // o vacío: todas las elegibles dentro del alcance de oficina
```

Reglas:

- La cuenta de origen es del **banco del formato** (o el formato es genérico); si no,
  `Lending.DisbursementFile.SourceAccountBankMismatch` (fijado aquí). Cartera no lee tablas `ACC_`: la cuenta llega
  por el puerto y se guarda su copia (código, número y tipo).
- Una orden va a lo sumo a un archivo no anulado: `Lending.DisbursementFile.OrderAlreadyInFile` (fijado aquí).
- Cada línea sale de la **copia sellada del beneficiario en la orden** (I-1), no del maestro de hoy:

| Origen | Valor |
|---|---|
| `PayeeDocumentType`, `PayeeDocument`, `PayeeFullName`, `PayeeFirstNames`, `PayeeLastNames`, `PayeeEmail` | Copia del beneficiario en la orden |
| `PayeeBankCode` | `COR_Banks.TransferCode` (código ACH) del banco de la cuenta destino |
| `PayeeAccountType`, `PayeeAccountNumber` | Cuenta destino sellada en la orden |
| `Amount` | Valor neto a girar, después de la recogida de créditos propios |
| `Concept`, `LoanNumber` | Número del crédito |
| `BatchReference` | Número del archivo (`NumeradorDeCartera`) |

- Una orden a la que le falta un campo obligatorio del formato no entra: queda en «pendientes» con su motivo
  (`BankCodeMissing`, `NoBankAccount`, como la dispersión de la 010, o el nombre del campo que falta).
- El adjunto (`LendingDisbursementFile`, no borrable, SHA-256) se sube **antes** de guardar la fila; el guardado
  escribe `LND_DisbursementFiles` (número, formato y su versión, copia de la cuenta de origen, fecha de pago,
  conteo, total, SHA-256, adjunto, `Status` `DisbursementFileStatus` fijado aquí: `Generated`, `Voided`) y sus
  `LND_DisbursementFileLines` (H: orden, número de línea, copia del beneficiario, valor, concepto).

**Descargar.** `POST /api/lending/disbursement-files/{id}/download-link`: enlace firmado de 60 s, auditado.

**Anular.** `VoidDisbursementFileCommand` (fijado aquí), `POST /api/lending/disbursement-files/{id}/void`, con
motivo y `Lending.DisbursementFiles.Generate`, **sólo** si ninguna de sus órdenes tiene ya un `ResultadoDelGiro`
(`Lending.DisbursementFile.HasTreasuryResults`, fijado aquí). El archivo y sus líneas quedan; sus órdenes vuelven a
estar disponibles y van a otro archivo (T78): siguen con `PaidByFile = true` y no se emite mensaje nuevo.

**Relación con Tesorería (C10).** La orden de giro **nace al desembolsar** (T12) con `PaidByFile = true` en el
contenido de `OrdenDeGiroEmitidaV1`, así Tesorería no la paga por otra vía (FR-045). La referencia al archivo queda
**en la orden de Cartera** (`LND_DisbursementOrders`) al generarlo, y el archivo no viaja en ningún mensaje: el
contenido de la orden no lleva `DisbursementFilePublicId`. Tesorería responde `ResultadoDelGiro` por orden, referido
a la clave de la orden (`interfaces.md`, que fija los campos de giro, C13).

Ejemplo de formato (ilustrativo; el layout real de cada banco lo aporta la cooperativa):

```jsonc
{ "code": "CSV-DESEMBOLSOS", "name": "Desembolsos de crédito, CSV genérico", "bankPublicId": null,
  "scope": "LoanDisbursement", "validFrom": "2026-12-01", "kind": "Delimited", "delimiter": ";",
  "encoding": "utf-8", "amountFormat": "Point2", "fileName": "DESEMB{PaymentDate:yyyyMMdd}-{BatchReference}.csv",
  "records": {
    "header":  { "enabled": false, "fields": [] },
    "detail":  { "enabled": true, "fields": [
      { "order": 1, "name": "Tipo doc",   "source": "PayeeDocumentType", "required": true },
      { "order": 2, "name": "Documento",  "source": "PayeeDocument",     "required": true },
      { "order": 3, "name": "Nombre",     "source": "PayeeFullName",     "required": true },
      { "order": 4, "name": "Banco ACH",  "source": "PayeeBankCode",     "required": true },
      { "order": 5, "name": "Tipo cta",   "source": "PayeeAccountType",  "required": true },
      { "order": 6, "name": "Cuenta",     "source": "PayeeAccountNumber","required": true },
      { "order": 7, "name": "Valor",      "source": "Amount",            "required": true },
      { "order": 8, "name": "Referencia", "source": "LoanNumber",        "required": true } ] },
    "trailer": { "enabled": false, "fields": [] } } }
```

### 6.3 Archivos de pagaduría, de ida y de vuelta (K2, FR-108–FR-116)

**Formatos.** Siempre genéricos (`BankId` nulo), uno de ida (`PayrollDeductionRequest`) y uno de vuelta
(`PayrollDeductionResult`) por pagaduría, elegidos **por referencia** desde la versión del convenio
(`LND_PayrollAgreementVersions.OutboundFormatId`, `.ResultFormatId`). El layout real de cada pagaduría es dato que
aporta el dueño (I8). Los códigos de respuesta de cada pagaduría se traducen al motivo de Cartera por versión del
convenio en `LND_PayrollAgreementResultCodes` (código de la pagaduría → `MotivoDeNoDescuento` o «descontado»).

**Qué va en el archivo.** `NovedadesDeLibranza` (puro) compara lo exigible del período de cada obligación del
convenio (y lo vencido, si el convenio lo admite) con el último archivo `Sent` del mismo convenio, y da por línea
`PayrollNoveltyKind`: `New`, `Change`, `Cancellation` o `Unchanged`. Lo exigible lo calcula la reconstrucción en
bloque (`CargadorDeReconstruccion`), nunca una consulta por crédito: 5.000 obligaciones en menos de un minuto
(SC-016).

**Generar.** `GeneratePayrollDeductionFileCommand` (fijado aquí), `POST /api/lending/payroll-deduction-files`
(`agreementPublicId`, período), `Lending.PayrollDeductionFiles.Generate`. Escribe `LND_PayrollDeductionFiles`
(convenio, versión del convenio, período, `Version`, `Status` `PayrollFileStatus`, conteos y totales por novedad,
SHA-256, adjunto `LendingPayrollDeductionFile`) y sus líneas (H). Emite `DescuentosPorPagaduria` (CAR → `Receipts`)
con el `PublicId` del adjunto, el SHA-256 y los totales, **sin bytes** en el sobre.

| Situación | Resultado |
|---|---|
| Regenerar antes del envío | Versión `n + 1`; la anterior queda `Superseded` con su archivo |
| Regenerar después de `Sent` | `Lending.PayrollFile.AlreadySent`; la corrección viaja como novedad en el archivo del período siguiente (I9) |
| Llega `EnvioDeDescuentosConfirmado` | El archivo queda `Sent` |
| Anular un archivo no enviado | `Voided`, con motivo |

**La respuesta.** Cartera **no lee** el archivo de vuelta: lo procesa Recaudo, que envía `ResultadoDeDescuentos`.
`ConsumePayrollDeductionResultCommand` (sin ruta) marca cada línea como:

- **descontado**: «pendiente de giro» hasta que Recaudo aplica el pago por el contrato con la fecha valor del
  descuento (FR-114). Dentro de `Libranza.VentanaDeGiroDias` y hasta `Libranza.UmbralDeMoraDePagaduria`, al deudor no
  se le imputa mora, calificación, gastos ni reporte negativo;
- **no aplicado**: una fila en `LND_InstallmentNonDeductions` con origen `PaymasterResult` y su motivo, un mensaje
  informativo al deudor (`NOAPLICPAG`) y la acción del convenio (pasar a caja o a cobranza). Alerta
  `Cartera.NoAplicadoDePagaduria`.

**Tareas.** `cartera.archivos-de-descuento` (diaria): convenios cuyo archivo debió generarse según su antelación →
`Cartera.ArchivoDeDescuentosPendiente`. `cartera.giros-de-pagaduria` (diaria): ventana de giro vencida →
`Cartera.GiroDePagaduriaAtrasado` y la pagaduría en mora; superado el umbral, aviso al deudor y calificación
individual.

**La nómina propia** es una pagaduría más en sus reglas (FR-112), pero **sin archivo**: Nómina consulta las cuotas por
`IConsultaDeObligaciones` y aplica por el contrato (`interfaces.md`). Su convenio sólo fija la regla de lo no
descontado (FR-115).

Rutas: `/api/lending/payroll-deduction-files` (+ `{id}`, `{id}/download-link`), `/api/lending/payroll-not-applied`
(`Lending.PayrollDeductionFiles.Read`, `Lending.PayrollNotApplied.Manage`); pantallas `/cartera/archivos-de-descuento`,
`/cartera/archivos-de-descuento/{id:guid}`, `/cartera/no-aplicados`; vista `payroll-deduction-reconciliation`
(enviado, aplicado, pendiente de giro, no aplicado).

## 7. Layouts regulatorios: SICSES y centrales (K5, T79)

### 7.1 El motor genérico (puro, `Domain/Common/Layouts`)

| Pieza | Qué hace |
|---|---|
| `LayoutDeArchivo` | La definición leída del JSON: registros por código (cabecera, detalle, totales u otros), campos, ancho fijo o delimitado |
| `ValidadorDeLayout` | Valida las filas contra el layout y devuelve inconsistencias: severidad (`Blocking`, `Warning`), registro, campo, crédito, ruta y mensaje |
| `EscritorPorLayout` | Escribe los bytes y el SHA-256; nunca escribe una fila con bloqueantes |

La PILA de la 010 no se toca: `PilaLayout` y `PilaWriter` siguen siendo suyos.

### 7.2 El JSON embebido

Archivos en `Application/Lending/Regulatory/Layouts/`: `sicses-cartera-{versión}-{aaaa-mm-dd}.json` y
`central-{CODIGO}-{versión}-{aaaa-mm-dd}.json`, recursos embebidos del ensamblado. Forma (los campos son
**ilustrativos**: el instructivo vigente y los layouts de cada central los aporta el dueño, I7):

```jsonc
{
  "code": "SICSES-CARTERA-v1",
  "kind": "Sicses",                       // RegulatoryFileKind: Sicses | CreditBureau
  "bureauCode": null,                     // en las centrales, el código de la central
  "version": "1",
  "source": "Instructivo del formato de cartera de créditos, SICSES (por cotejar)",
  "validFrom": "2026-12-01", "validTo": null,
  "fileKind": "Delimited", "delimiter": "|", "encoding": "utf-8", "lineEnding": "CRLF",
  "records": [
    { "code": "Detail", "repeat": "PorCredito", "fields": [
      { "number": 1, "name": "Tipo de identificación", "order": 1, "kind": "A", "length": 2,
        "format": "Text", "required": true, "path": "Deudor.TipoDeDocumento",
        "allowedValues": ["CC", "CE", "NI", "PA", "TI"], "verified": false },
      { "number": 2, "name": "Número de identificación", "order": 2, "kind": "A", "length": 15,
        "format": "Text", "required": true, "path": "Deudor.NumeroDeDocumento", "verified": false },
      { "number": 3, "name": "Saldo de capital", "order": 3, "kind": "N", "length": 18,
        "format": "Amount2", "required": true, "path": "Saldos.Capital", "verified": false },
      { "number": 4, "name": "Calificación", "order": 4, "kind": "A", "length": 1,
        "format": "Text", "required": true, "path": "Riesgo.Categoria",
        "allowedValues": ["A", "B", "C", "D", "E"], "verified": false } ] }
  ],
  "notes": ["Campos reconstruidos; cada uno lleva verified=false hasta cotejarlo con el instructivo y un archivo aceptado por el validador."]
}
```

Formatos de campo: `Text`, `Integer`, `Amount2` (dos decimales implícitos), `AmountPoint2` (con punto), `Rate`
(fracción con los decimales del campo), `Date` (con `pattern`, por defecto `yyyyMMdd`). En ancho fijo, `start` y
`length` en vez de `order`; `pad` y `align` como en los formatos bancarios. `path` resuelve contra el objeto de la
proyección (§7.4); `constant` reemplaza a `path`.

`CatalogoDeLayoutsDeCartera` carga y valida el esquema de todos los JSON **al arrancar** (un JSON mal formado impide
arrancar, igual que `PilaLayoutCatalog`) y elige el vigente por clase, central y fecha de fin del período.
`GET /api/lending/regulatory/layouts` (`Lending.RegulatoryFiles.Read`, C85: no hay un `.View` regulatorio sin datos
personales; lo tienen también contador, auditor y revisor fiscal) los lista con su estado de cotejo (campos cotejados
sobre el total).

### 7.3 Cotejo por campo

- Cada campo lleva `verified`. Un layout con algún campo sin cotejar **es vigente** (precedente D-43 de la PILA),
  pero generar deja la advertencia `Lending.Regulatory.LayoutSinCotejar`, que se guarda con la generación y la
  pantalla muestra junto al archivo (FR-136).
- Cotejar es publicar una versión nueva del JSON con `verified: true` en los campos cotejados, más `verifiedBy`,
  `verifiedOn` y `verifiedAgainst` (el instructivo y el archivo aceptado por el validador de la Superintendencia o
  por la central, SC-019). Exige despliegue: una versión nueva del instructivo es un archivo nuevo con su `validFrom`,
  no lógica.

### 7.4 Proyecciones

- **`ProyeccionSicsesCartera`** sale del **cierre mensual vigente** del período (`Kind = Monthly`, sin reapertura
  posterior) y de sus líneas: una fila por crédito, con deudor (copia del maestro a la fecha de corte), saldos por
  concepto, días de mora, calificación y subcategoría, deterioro individual y general, garantías, reestructuración y
  modalidad. La periodicidad del reporte la da el nivel de supervisión del perfil regulatorio (FR-018). Pasado el día
  límite que fije el perfil, se alerta `Cartera.PlazoRegulatorioPorVencer`.
- **`ProyeccionCentralDeRiesgo`**, por central y período: las novedades desde el último archivo `Uploaded` de esa
  central (desembolsos, pagos, mora, cancelaciones, castigos), los reclamos en trámite con su marca
  (`LND_CreditReportClaims`) y los retiros de reportes hechos sin aviso. Incluye **sólo** a quienes tienen autorización
  `CreditBureauReport` con evidencia (FR-006), y un dato negativo sólo con aviso previo `Complete` de su episodio
  (§5.5). Lo que se excluye queda en `ExcludedJson` con su motivo.
- Si el cierre lleva `ObligatoryModelUnavailable` (D28), la generación del SICSES guarda la advertencia
  `ModeloObligatorioNoDisponible` y la muestra como advertencia, igual que la de layout sin cotejar; **nunca** va
  dentro del archivo, que sigue
  el layout vigente con los datos del modelo efectivamente aplicado.

### 7.5 Validar, generar, regenerar y marcar la carga

`LND_RegulatoryFiles` (`RegulatoryFile`): `Kind`, `BureauCode`, `PeriodYear`, `PeriodMonth`, `Version`, `Status`
(`Validated`, `Generated`, `Superseded`, `Uploaded`), `LayoutCode` y su SHA-256, `MonthlyClosePublicId` (SICSES),
advertencias (`LayoutSinCotejar`, `ModeloObligatorioNoDisponible`), conteos de filas, bloqueantes y advertencias,
`IssuesJson`, `ExcludedJson`, `FileSha256`, adjunto `LendingRegulatoryFile`, `UploadedAt`, `UploadedBy` y
`FilingReference` (radicado).

| Paso | Comando | Qué hace |
|---|---|---|
| Validar | `ValidateSicsesFileQuery`, `ValidateCreditBureauFileQuery` (fijado aquí) | Corre proyección y validador; **no guarda nada** |
| Generar | `GenerateSicsesFileCommand`, `GenerateCreditBureauFileCommand` | Con bloqueantes: fila `Validated` **sin archivo**, con las inconsistencias guardadas (`Lending.Regulatory.BlockingIssues`). Sin bloqueantes y con advertencias: exige `acknowledgeWarnings = true`; fila `Generated` con archivo |
| Regenerar | los mismos | Versión `n + 1`; la anterior queda `Superseded` con su archivo |
| Marcar cargado | `MarkRegulatoryFileUploadedCommand` | `Uploaded` con radicado y fecha; el período de esa clase (y central) ya no se regenera: `Lending.Regulatory.AlreadyUploaded` (fijado aquí) |

| Ruta | Permiso |
|---|---|
| `POST /api/lending/regulatory/sicses/{year}/{month}/validate` · `/generate` | `Lending.RegulatoryReports.Generate` |
| `POST /api/lending/regulatory/bureaus/{bureau}/{year}/{month}/validate` · `/generate` | `Lending.CreditBureauFiles.Generate` |
| `GET /api/lending/regulatory/files` · `POST …/files/{id}/download-link` | `Lending.RegulatoryFiles.Read` (las filas traen deudores) |
| `POST /api/lending/regulatory/files/{id}/mark-uploaded` | `Lending.RegulatoryFiles.MarkUploaded` |

```jsonc
// POST /api/lending/regulatory/sicses/2027/1/generate   { "acknowledgeWarnings": true }
{ "publicId": "e81b…", "kind": "Sicses", "period": "2027-01", "version": 1, "status": "Generated",
  "layoutCode": "SICSES-CARTERA-v1", "rows": 48210, "blocking": 0,
  "warnings": [ { "code": "Lending.Regulatory.LayoutSinCotejar", "message": "El formato tiene 4 de 4 campos sin cotejar con el instructivo." } ],
  "fileSha256": "4be0…", "downloadAvailable": true }
```

Pantallas `/cartera/sicses` y `/cartera/centrales` (K5). El contador genera; auditor y revisor fiscal leen.

### 7.6 Pruebas

Casos del motor en `Domain.Tests/Common/Layouts/Casos/`; archivos dorados en
`Application.Tests/Lending/Regulatory/Casos/*.esperado.txt`, comparados **byte a byte** y regenerables sólo a
propósito con `CARTERA_ESCRIBIR_ESPERADO=1` (fijada aquí; molde `PILA_ESCRIBIR_ESPERADO`). Lo que verifica el dueño:
que el validador de la Superintendencia acepte el archivo de un cierre de prueba sin errores de estructura (SC-019).

## 8. Documentos emitidos con código de verificación (T80)

### 8.1 Clases y entrega

| `IssuedDocumentKind` | Qué certifica | Condición para emitir | E |
|---|---|---|---|
| `PaymentPlan` | Plan vigente de un crédito, cuota a cuota, con tasas efectiva y nominal | Crédito vivo | K1 (I5, C88) |
| `LoanClearance` | Paz y salvo de una obligación | Saldo cero; sujeto a firmeza | K1 (I5, C88) |
| `GeneralClearance` | Paz y salvo general de la persona | Ver §8.6 | K1 (I5, C88) |
| `BalanceCertificate` | Saldo por concepto a una fecha | Crédito con historia a esa fecha | K1 (I5, C88) |
| `Statement` | Extracto de un período | Período con movimientos o saldo | K5 |
| `TaxCertificate` | Intereses pagados en el año por crédito y saldo al 31 de diciembre | Año terminado | K5 (vivienda separada desde K6b) |

**Entregas (C88)**: las tablas `LND_IssuedDocument*` nacen en la migración `CarteraNucleo` (K1). Hasta K5, pedir un
extracto o un certificado tributario responde `Lending.Document.KindNotAvailable`. Antes de activar Cartera no se
emite ningún documento (`Lending.NotActivated`). El PDF del simulador (FR-034) **no**
es un documento emitido: se arma a pedido, sin número ni código, con la leyenda «Simulación – no constituye
aprobación»; enviarlo por correo deja un mensaje `Informative` y nada más.

### 8.2 El registro

`LND_IssuedDocuments` (`IssuedLendingDocument`):

| Columna | Qué guarda |
|---|---|
| `Kind` · `Number` | Clase y número con el prefijo de su clase (`NumeradorDeCartera`, numerado al final de la transacción) |
| `IssuedAt` · `IssuedBy` | Instante UTC y persona |
| `PersonId` · `HolderDocumentType` · `HolderDocumentNumber` · `HolderName` | Titular y **copia de su identificación tal como se emitió** (I-1) |
| `CutoffDate` · `PeriodFrom` · `PeriodTo` · `Year` | Según la clase |
| `InformationCutoff` | R: el instante de la información usada (T24) |
| `SettlementFingerprint` · `RulesVersion` | Huella de la liquidación y versión de reglas (T24) |
| `VerificationCode` | Único en la cooperativa (§8.4) |
| `ContentSha256` · `AttachmentPublicId` | Huella y PDF guardado (adjunto `LendingIssuedDocument`, no borrable) |
| `EssentialDataJson` | Lo que devuelve la verificación (§8.9) |
| `FinalityUntil` | Fin de la firmeza (D23), en los paz y salvo |
| `CoversCoDebts` | Si el paz y salvo general incluyó codeudas (parámetro aplicado) |
| `OutsideLendingNote` | Ventas aceptadas sin migrar que el documento menciona (FR-143) |

`LND_IssuedDocumentLoans` (`IssuedDocumentLoan`): crédito cubierto, rol de la persona (deudor o codeudor), última
operación considerada y saldo mostrado. `LND_IssuedDocumentEvents` (`IssuedDocumentEvent`, H):
`IssuedDocumentEventKind` (fijado aquí) `Issued`, `Delivered`, `Voided`, con motivo, operación causa y actor.

### 8.3 Estado calculado (no es columna)

`IssuedDocumentStatus`: `Voided` si hay un evento `Voided`; `SubjectToFinality` mientras hoy < `FinalityUntil`;
`Valid` en otro caso. El paso de «sujeto a firmeza» a «vigente» no lo marca ninguna tarea: lo da la fecha.

### 8.4 Código de verificación

- 16 caracteres **Crockford base32** de 80 bits aleatorios (`RandomNumberGenerator`), mostrados
  `XXXX-XXXX-XXXX-XXXX`. Al verificar se normaliza: mayúsculas, sin guiones ni espacios, `I` y `L` → `1`, `O` → `0`.
- Único por cooperativa (índice único); una colisión se resuelve generando otro antes de guardar.
- El PDF lleva el código y un **QR** a `{urlDeLaAplicacion}/cartera/verificar?codigo=XXXX-XXXX-XXXX-XXXX`. La página
  exige sesión de un usuario de la cooperativa: no hay verificación pública ni anónima (D21).

### 8.5 Firmeza

`FinalityUntil` = fecha de registro de la operación que dejó el saldo en cero + los días hábiles de
`Pagos.FirmezaDiasPorClaseDeMedio` para la **clase de medio** de ese pago (diez por defecto; cruce interno, cero),
contados con `ICalendarioHabil`. Hasta esa fecha el paz y salvo dice «La cancelación está sujeta a firmeza hasta el
{fecha}», y pagaré y garantías no se liberan (FR-082).

### 8.6 Reglas por clase

- **Paz y salvo de una obligación**: saldo cero a hoy con la información de ahora; si no, se niega con
  `Lending.Document.ClearanceDenied` y el saldo pendiente.
- **Paz y salvo general**: todas las obligaciones de la persona como deudor (y como codeudor si
  `Documentos.PazYSalvoIncluyeCodeudas`) en cero. Se niega con `Lending.Document.ClearanceDenied`, listando:
  - las obligaciones con saldo;
  - las ventas de la persona con mensaje en reproceso (FR-053), con su factura.

  Las ventas **aceptadas sin migrar** no lo niegan: el documento las menciona como fuera de Cartera. Siempre lleva la
  leyenda «Certifica sólo las obligaciones de crédito de la cooperativa» y dice si incluyó las codeudas.
- **Certificado de saldo**: liquidación a la fecha D pedida con R = el instante de emisión (T24); una D futura se
  proyecta sin pagos y el documento dice «válido hasta {ValidaHasta}».
- **Plan de pagos**: la versión del plan vigente en R, con las condiciones selladas.
- **Extracto** (K5): saldo inicial, operaciones del período por fecha valor y saldo final, con R = emisión.
- **Certificado tributario** (K5): intereses efectivamente pagados en el año, por crédito (los de vivienda aparte
  desde K6b), y saldo de la deuda al 31 de diciembre.

### 8.7 Emitir en un paso, enviar y reimprimir

```jsonc
// POST /api/lending/documents   (Lending.Documents.Issue; Idempotency-Key)
{ "kind": "LoanClearance", "personPublicId": "77c0…", "loanPublicId": "1b8e…",
  "cutoffDate": null, "periodFrom": null, "periodTo": null, "year": null,
  "delivery": "Email" }                       // Download | Email

// 201
{ "publicId": "f20c…", "number": "PS-000318", "verificationCode": "7K2M-Q9XD-4HTP-B1RW",
  "status": "SubjectToFinality", "finalityUntil": "2026-12-15",
  "downloadLink": "https://…(60 s)", "debtorMessagePublicId": "3e77…" }
```

`IssueLendingDocumentCommand` valida, reconstruye, numera, renderiza por `IDocumentoDeCarteraRenderer`
(`DocumentosDeCarteraReport`, QuestPDF + QRCoder), sube el PDF **antes** de guardar, y en un `SaveChanges` escribe el
documento, sus créditos, el evento `Issued` y, con `Email`, el `DebtorMessage` `DocumentDelivery` con el PDF adjunto.
Es un diálogo con una confirmación (FR-013). La emisión se audita y además cuenta como lectura sensible (T67).

- **Enviar después**: `SendLendingDocumentCommand`, `POST /api/lending/documents/{id}/send`. El envío pedido por el
  titular está exento de las ventanas (I3) y exige correo registrado y autorización de tratamiento general. Cuando
  el mensaje queda `Sent`, el documento recibe el evento `Delivered`.
- **Reimprimir** es descargar el PDF guardado, idéntico al original: `POST /api/lending/documents/{id}/download-link`,
  `Lending.Documents.Reprint`, auditado. Un documento anulado no se reimprime para entregarlo
  (`Lending.Document.VoidedCannotReissue`): se emite uno nuevo.

### 8.8 Anulación

- **Automática**: `VigenciaDeDocumentos` (puro) corre **dentro** del comando que reversa (reverso de pago por pantalla,
  `ReversarAsync` del contrato, reverso de desembolso), en la misma unidad de trabajo. Anula todo documento que cubra
  el crédito y cuyo `InformationCutoff` sea igual o posterior al registro de la operación reversada, con el evento
  `Voided` y la operación causa. Así el paz y salvo emitido después del pago que canceló el crédito se verifica como
  anulado (Edge Case).
- **Manual**: `VoidLendingDocumentCommand`, `POST /api/lending/documents/{id}/void`, con motivo,
  `Lending.Documents.Void`.

### 8.9 Verificar

`VerifyLendingDocumentQuery`, `GET /api/lending/documents/verify/{code}`, `Lending.Documents.Verify`, auditada.
Como cada cooperativa tiene su base, nunca muestra documentos de otra. Un código desconocido responde el 404 genérico
con «No hay un documento con ese código en esta cooperativa».

```jsonc
{ "authentic": true, "status": "Voided", "kind": "GeneralClearance", "number": "PSG-000044",
  "issuedAt": "2026-12-03T10:41:00-05:00", "holder": { "name": "…", "documentType": "CC", "documentNumber": "…" },
  "essentialData": { "coversCoDebts": false, "loans": [ { "number": "CR-004512", "balance": 0.00 } ] },
  "voided": { "at": "2026-12-05T08:00:00-05:00", "reason": "Se reversó el pago que canceló el crédito CR-004512." } }
```

Opcional en K5: comparar el SHA-256 de un PDF que el usuario sube con el guardado.

### 8.10 El PDF

- Membrete con nombre y NIT de la cooperativa (`COR_Companies`); firmante por `Documentos.Firmante.Nombre` y
  `Documentos.Firmante.Cargo`; número, fecha, código y QR; copia de la identificación del titular.
- Leyendas: fuera de producción, «AMBIENTE DE PRUEBAS – SIN VALIDEZ»; firmeza (§8.5); alcance del paz y salvo
  general; ventas fuera de Cartera.
- Logotipo e imagen de firma: adjuntos opcionales de la cooperativa, dueños `LendingLogo` y
  `LendingSignatureImage` (§9), sólo si el dueño acepta I6 (C90); la firma digital certificada queda fuera.

### 8.11 Rutas y pantallas

| Ruta | Permiso |
|---|---|
| `GET /api/lending/documents?person=&loan=&kind=&from=&to=` · `GET …/{id}` | `Lending.Documents.Read` |
| `POST /api/lending/documents` | `Lending.Documents.Issue` |
| `POST /api/lending/documents/{id}/download-link` | `Lending.Documents.Reprint` |
| `POST /api/lending/documents/{id}/send` | `Lending.Documents.Issue` |
| `POST /api/lending/documents/{id}/void` | `Lending.Documents.Void` |
| `GET /api/lending/documents/verify/{code}` | `Lending.Documents.Verify` |

Pantallas `/cartera/documentos` y `/cartera/verificar`; vista de informe `issued-documents` (K5).

## 9. Adjuntos de Cartera (T81)

`AdjuntosDeModulo` es el único sitio que decide lectura, subida y borrado.

| Dueño | Lectura | Subida | Borrable |
|---|---|---|---|
| `LendingIssuedDocument` | `Lending.Documents.Read` | no (lo genera Cartera) | no |
| `LendingDisbursementFile` | `Lending.DisbursementFiles.Generate` | no | no |
| `LendingPayrollDeductionFile` | `Lending.PayrollDeductionFiles.Read` | no | no |
| `LendingRegulatoryFile` | `Lending.RegulatoryFiles.Read` | no | no |
| `LendingBureauReport` | `Lending.CreditBureau.ReadReports` | no | no |
| `LoanApplicationDocument` | `Lending.Applications.Read` | `Lending.Applications.Create`, mientras la solicitud no esté decidida | no, una vez decidida (`Attachments.OwnerLocked`) |
| `LendingGuaranteeDocument` | `Lending.Guarantees.Read` | `Lending.Guarantees.Manage` | no, una vez constituida |
| `LendingPayrollCertification` | `Lending.Applications.Read` | `Lending.Applications.Create` (FR-109) | no, una vez decidida |
| `LendingPriorNoticeEvidence` | `Lending.PriorNotices.Read` | `Lending.PriorNotices.RegisterEvidence` | no, una vez registrada |
| `LendingLegalProcessDocument` | `Lending.Collections.Read` | `Lending.LegalProcesses.Manage` | no |
| `LendingNoveltyConsent` | `Lending.Novelties.Read` | `Lending.Novelties.Request` | no, una vez decidida la novedad |
| `DataAuthorization` (plataforma) | `Compliance.DataSubjectRequests.Read` | quien registra la autorización (radicación o Cumplimiento) | no |
| `LendingLogo` · `LendingSignatureImage` (sólo si el dueño acepta I6, C90) | `Lending.Documents.Read` | `Lending.Parameters.Manage` (como el firmante, que es parámetro) | sí, al reemplazarlo por otro; un documento ya emitido conserva su PDF |

Lo que genera un módulo se guarda en formato `Direct` y `Available` desde el servidor; la descarga es siempre un
enlace firmado de 60 s, auditado. Las evidencias regulatorias no se borran (Principio XI, FR-003): la papelera de 90
días de la 011 no les aplica.

## 10. Pruebas

| Tipo | Pruebas |
|---|---|
| Arquitectura | `LosProveedoresExternosSoloLosConoceSuAdaptador`, `LasCredencialesDeCanalesExternosNoTocanLaBase`, `LaCobranzaNuncaContactaReferencias`, `LosFormatosElegidosPorReferenciaNoCompitenPorVigencia`, `LaCarteraNoTieneValoresLegalesFijos` (sobre `Domain/Lending/Collections` y `Domain/Common/Layouts`) |
| Dominio | Casos dorados de `VentanaLegalDeContacto` (`Domain.Tests/Lending/Collections/Casos`), de `NovedadesDeLibranza`, de `VigenciaDeDocumentos` y del motor de layouts; `CoincidenciaDeNombres` con homónimos y tildes |
| Conformidad | `ConformidadDeMensajeria`, `ConformidadDelDepositoDeValores`, `ConformidadDeCentral`, `ConformidadDeListasRestrictivas` |
| Application | Archivos dorados `.esperado.txt` del archivo de desembolsos, del de descuentos y de los layouts regulatorios; guardia de efecto legal con `AmbienteDePruebas` en los dos valores; `CredencialesDeCanalExternoEnArchivo` con dos cooperativas |
| e2e («Cartera e2e») | `NingunMensajeSaleFueraDeLaVentanaTests` (SC-011); `ElReversoAnulaLosDocumentosTests` (fijada aquí: reverso del pago que canceló → el paz y salvo verifica `Voided`); aviso previo sin constancia excluido del archivo de centrales (SC-012); archivo de descuentos con tres no aplicados (US10) |
| Rendimiento (`RUN_PERF_TESTS=1`) | Archivo de descuentos de 5.000 obligaciones en menos de un minuto (SC-016) |

## 11. Pendientes del dueño (propuesta por defecto de §5 de las transversales; rige si no responde)

| # | Tema | Propuesta | Bloquea |
|---|---|---|---|
| I1 | ¿Cada cooperativa contrata SMS y WhatsApp? | Sí: una credencial por cooperativa y canal | Canal real (K4) |
| I2 | «Un canal por semana» y los informativos | El canal del contacto directo; los informativos respetan horario y tope, no fijan canal | Casos dorados del motor |
| I3 | Documento pedido por el titular, por correo | Exento de ventanas; exige correo y autorización de tratamiento | Envío de documentos |
| I4 | Simulado en producción y aprobación sin centrales | Sí, sin efecto legal; cada línea dice si exige consulta; pagaré físico | Originación en producción |
| I5 | Plan de pagos, paz y salvo y certificado de saldo en K1 | Sí; extracto y certificado tributario en K5 | Orden de entregas y migración `CarteraNucleo` |
| I6 | Firma y logotipo | Nombre y cargo por parámetro más código y QR; imagen y logotipo como adjuntos opcionales | PDF |
| I7 | Formato del SICSES y centrales de COOFLOPAL | El dueño aporta los layouts; se cargan con `verified = false` | K5 |
| I8, I9 | Layouts de pagaduría; regenerar lo enviado | Formatos de ámbitos 5 y 6; no se regenera lo enviado | K2 |
| I10 | Cuenta del beneficiario del desembolso | Se propone desde la persona, se corrige al desembolsar y se sella en la orden | Archivo de desembolsos |
| I12 | Listas por cooperativa y re-escaneo | Cada cooperativa; re-escaneo diario con alerta | Listas |
| I13 | La cooperativa como persona jurídica para su convenio | Sí | K2 |
| I15 | Plantillas de WhatsApp ante Meta | Las registra la cooperativa; sin `ProviderTemplateId` no hay WhatsApp | WhatsApp real |

## 12. Nombres fijados en este documento

Aplican T1–T4; `data-model.md`, `api.md` y `tasks.md` los copian o este contrato se corrige antes de publicarse.

- **Carpeta y tipos comunes**: `Application/Common/ExternalChannels`; `ResultadoExterno<T>`, `ContextoDeCanalExterno`,
  `ArtefactoExterno`, `ICredencialesDeCanalExterno`, `CredencialDeCanalExterno`.
- **Formas de los puertos**: `MensajeSaliente`, `EnvioDeMensaje`, `EstadoDeEntrega`, `SolicitudDePagare`,
  `FirmanteDePagare`, `FirmaDePagare`, `EstadoDePagare`, `ConsultaACentral`, `InformeDeCentral`, `ConsultaDeListas`,
  `ResultadoDeListas`, `CoincidenciaEnLista`, `VersionDeListaConsultada`; motor: `SolicitudDeContacto`,
  `ReglasDeContacto`, `HistorialDeContactos`, `EstadoDelTitular`, `DecisionDeContacto` (`Permitido`, `Diferido`,
  `Excluido`); guardia: `VeredictoDeEfectoLegal` (`ConEfecto`, `SinEfecto`); lector: `FlatFileReadResult`.
- **Enums**: `DeliveryState {Accepted, Delivered, Failed, Unknown}`; `ScreeningOutcome {NoMatch, PossibleMatch,
  Match}`; `PromissoryNoteKind {Physical, Dematerialized}`; `PromissoryNoteStatus {Created, PendingSignature, Signed,
  InCustody, Endorsed, Voided}`; `EfectoLegalDeCanal {PromissoryNotePerfection, PriorNoticeEvidence,
  BureauEvidenceForApproval, RealDelivery}`; `ContactWindowReason {RecipientNotObligated, DebtorDeceased, Insolvency,
  CollectionSuspended, ChannelNotAuthorized, AuthorizationWithoutEvidence, NoDestination, Sunday, Holiday,
  OutsideHours, DailyLimitReached, WeeklyChannelLocked, NoWindow, ChannelUnavailable}`; `DebtorMessageOrigin
  {Operation, Process, PriorNotice, Campaign, Manual, IssuedDocument}`; `CollectionActionChannel {Phone, InPerson,
  Email, Sms, WhatsApp, Letter, Visit}`; `PriorNoticeStatus {AwaitingEvidence, Partial, Complete, EpisodeClosed}`
  (calculado); `DisbursementFileStatus {Generated, Voided}`; `IssuedDocumentEventKind {Issued, Delivered, Voided}`.
- **Columnas**: `DisbursementOrder.PaidByFile`; las de `LND_DebtorMessages`, `LND_DebtorMessageEvents`,
  `LND_PriorNotices`, `LND_IssuedDocuments` y `LND_RegulatoryFiles` de §4.2, §5.2, §8.2 y §7.5
  (`EpisodeStartDate`, `RequiredCommunications`, `EarliestReportDate`, `MigratedReference`, `DestinationAddress`,
  `NextAttemptAt`, `Attempt`, `HolderDocumentType`, `HolderDocumentNumber`, `HolderName`, `SettlementFingerprint`,
  `OutsideLendingNote`, `FilingReference`, `MonthlyClosePublicId`, `IssuesJson`, `ExcludedJson`).
- **Comandos y consultas**: `SendDebtorMessageCommand`, `CancelDebtorMessageCommand`, `GetContactWindowQuery`,
  `CreateMessageTemplateVersionCommand`, `RecordCollectionActionCommand` (K4), `RegisterPriorNoticeEvidenceCommand`,
  `GenerateDisbursementFileCommand`, `VoidDisbursementFileCommand`, `GeneratePayrollDeductionFileCommand` (K2),
  `ValidateCreditBureauFileQuery` (K5).
- **Rutas**: `GET /api/lending/debtor-messages/contact-window`, `POST /api/lending/debtor-messages/{id}/cancel`,
  `POST /api/core/external-channels/{id}/verify`, `POST /api/lending/disbursement-files/{id}/void`,
  `POST /api/lending/prior-notices/{id}/evidence`.
- **Errores**: `Core.ExternalChannel.Overlaps`, `.UnknownChannel`, `.CredentialMismatch`;
  `Lending.DisbursementFile.SourceAccountBankMismatch`, `.OrderAlreadyInFile`, `.HasTreasuryResults`;
  `Lending.Regulatory.AlreadyUploaded`.
- **Tarea**: `cartera.re-escaneo-de-listas` (diaria).
- **Plantillas de mensaje sembradas**: `AVISOPREV`, `RECORDPREV`, `MORAADMIN`, `PREJURID`, `NOAPLICPAG`, `PASOACAJA`,
  `ABONOCAP`, `GASTOCOBR`, `ENVIODOC`; y las variables de la lista blanca de §4.7.
- **Pruebas y entorno**: `ConformidadDeListasRestrictivas`, `NingunMensajeSaleFueraDeLaVentanaTests`,
  `ElReversoAnulaLosDocumentosTests`, variable `CARTERA_ESCRIBIR_ESPERADO`.
- **Layouts**: nombres de archivo `sicses-cartera-{versión}-{aaaa-mm-dd}.json` y `central-{CODIGO}-{versión}-{aaaa-mm-dd}.json`;
  formatos de campo `Text`, `Integer`, `Amount2`, `AmountPoint2`, `Rate`, `Date`; propiedades `verifiedBy`,
  `verifiedOn`, `verifiedAgainst`.
- **Adjuntos (sujetos a I6, C90)**: `LendingLogo`, `LendingSignatureImage`.

## 13. Conciliación

Los puntos que este contrato dejó para conciliar se decidieron en `decisiones-transversales.md` §7; el cuerpo ya
aplica cada decisión.

| Punto | Decisión |
|---|---|
| ADA-1 · orden pagada por archivo | C10: `PaidByFile = true` en `OrdenDeGiroEmitidaV1` al desembolsar; la referencia al archivo queda en la orden de Cartera; sin `DisbursementFilePublicId` en el contenido; anular un archivo lleva sus órdenes a otro (§6.2) |
| ADA-2 · re-escaneo de listas | C90: tarea `cartera.re-escaneo-de-listas`, con alerta y bloqueo de originar y vender, no del cobro (§3.8) |
| ADA-3 · destino copiado | C57: `LND_DebtorMessageEvents.DestinationAddress` en el evento `Sent` (§4.2) |
| ADA-4 · límites de cobranza | C71: claves de `data-model.md` §12.5 (`COBRANZA_HORA_INICIO_LV`, `COBRANZA_HORA_FIN_LV`, `COBRANZA_HORA_INICIO_SAB`, `COBRANZA_HORA_FIN_SAB`, `COBRANZA_CONTACTOS_DIA`) y parámetros `Cobranza.*` (§1, §4.5) |
| ADA-5 · canales de autorización y de mensaje | C90: `Letter` no exige autorización de canal; `Call` y `Visit` sólo son gestiones de K4 (§4.5) |
| ADA-6 · plantillas de 10 caracteres | C90: `CodigoDeCatalogo` (§4.7) |
| ADA-7 · logotipo e imagen de firma | C90: adjuntos `LendingLogo` y `LendingSignatureImage` si el dueño acepta I6 (§8.10, §9) |
| ADA-8 · permiso de los layouts | C85: `Lending.RegulatoryFiles.Read` (§7.2) |
| ADA-9 · documentos en K1 | C88: plan de pagos, paz y salvo y certificado de saldo en K1, `LND_IssuedDocument*` en `CarteraNucleo`; extracto y certificado tributario en K5 con `Lending.Document.KindNotAvailable` (§8.1) |

