# Contrato: documentos al titular de captaciones y aportes

**Feature**: 014 | **Fecha**: 2026-10-14 | **Entregas**: K7 (constancia y paz y salvo de aportes, liquidación de
retiro) · K8 (extracto, certificados de saldo, tributario y de retención) · K9 (título del CDAT) | **Base**:
`research.md` §0.3 (`IssuedDocumentKind` 7–14), R24; spec FR-068, FR-101–FR-106, US13, SC-016 | **Molde**:
`specs/013-cartera-financiera/contracts/adaptadores-y-archivos.md` §8 (en adelante **«la 013»**) | **Hermanos**:
`api.md` §17, `mensajes.md`

Los documentos de captaciones y aportes usan **la mecánica de documentos de la Cartera sin cambios**: número por clase
(`NumeradorDeCartera`, al final de la transacción), código de verificación de 16 caracteres Crockford base32 con QR a
`/cartera/verificar?codigo=`, SHA-256, PDF guardado como adjunto no borrable (`LendingIssuedDocument`), copia de la
identificación del titular tal como se emitió (I-1), `InformationCutoff` (R) y huella de la liquidación, estado
**calculado** y **anulación por reverso** en la misma unidad de trabajo. Lo que este contrato fija son las **ocho
clases nuevas**, su contenido mínimo y qué reverso anula cada una.

---

## 1. Clases nuevas (`IssuedDocumentKind`, valores al final)

| Valor | `IssuedDocumentKind` | Qué es | Cómo nace | Prefijo **(nuevo en contracts)** | E |
|---|---|---|---|---|---|
| 7 | `AccountStatement` | Extracto de una cuenta por período (FR-102) | a pedido; lote opcional | `EXT` | K8 (K7 para aportes) |
| 8 | `AccountBalanceCertificate` | Certificado de saldo de una cuenta a una fecha (FR-103) | a pedido | `CSA` | K8 (K7 para aportes) |
| 9 | `DepositTaxCertificate` | Certificado tributario anual por titular (FR-104) | a pedido o en lote anual (`api.md` §17) | `CTR` | K8 |
| 10 | `WithholdingCertificate` | Certificado de retención en la fuente por período (FR-105) | a pedido o en lote | `CRF` | K8 |
| 11 | `TermDepositTitle` | Título del CDAT, **no negociable** (FR-068) | **automático** al constituir, prorrogar y renovar | `TIT` | K9 |
| 12 | `ContributionCertificate` | Constancia de aportes (FR-106) | a pedido | `CNA` | K7 |
| 13 | `ContributionClearance` | Paz y salvo de aportes (FR-106) | a pedido | `PSA` | K7 |
| 14 | `MembershipWithdrawalSettlement` | Liquidación del retiro del asociado (FR-076–FR-080) | **automático** al liquidar el retiro | `LRA` | K7 |
| 15 | `GarnishmentResponse` | Respuesta al juzgado de un embargo, a nombre de la persona embargada (R23; **enmienda E176**) | **automático** en la transacción de la respuesta (la que registra la persona y la de «aportes inembargables» que nace con el oficio) | `RJZ` | K8 |

Las clases de la 013 (`PaymentPlan` 1 … `TaxCertificate` 6) siguen siendo de créditos; el «extracto» y el «certificado
tributario» de captaciones son clases **propias** (7 y 9), con su contenido y su prefijo.

### 1.1 El registro

`LND_IssuedDocuments` sin cambios de columnas salvo que `CutoffDate`, `PeriodFrom`, `PeriodTo` y `Year` se usan según la
clase. Tabla hija nueva **(nuevo en contracts)** `LND_IssuedDocumentAccounts` (`IssuedDocumentAccount`): cuenta
cubierta, rol de la persona en ella (`MemberAccountPartyRole`), proporción del cotitular, última operación considerada
(`MemberAccountOperation`) y saldo mostrado. Molde de `LND_IssuedDocumentLoans`. `LND_IssuedDocumentEvents` suma los
eventos `Superseded` (un título reemplazado por la prórroga o la renovación) y `Settled` (el CDAT se redimió o canceló)
**(nuevos en contracts)**.

### 1.2 Estado calculado

`IssuedDocumentStatus` (calculado, no es columna) suma dos valores al final **(nuevo en contracts)**:

| Estado | Cuándo |
|---|---|
| `Voided` | hay un evento `Voided` (manual o por reverso) |
| `Superseded` | sólo títulos: hay un título posterior de la misma cuenta (prórroga o renovación) |
| `Settled` | sólo títulos: el CDAT se redimió o canceló |
| `SubjectToFinality` | sólo el paz y salvo de aportes: mientras hoy < `FinalityUntil` |
| `Valid` | en otro caso |

---

## 2. Contenido mínimo de cada clase

Todo PDF lleva el membrete de la cooperativa (nombre y NIT), el firmante (`Documentos.Firmante.Nombre`/`.Cargo`),
número, fecha y hora de emisión, código de verificación y QR, la identificación del titular tal como se emitió y, fuera
de producción, la leyenda «AMBIENTE DE PRUEBAS – SIN VALIDEZ». Ningún documento nombra al sistema anterior: lo migrado
dice «saldo migrado al {corte}».

### 2.1 Extracto (`AccountStatement`, FR-102)

| Bloque | Contenido |
|---|---|
| Encabezado | Cuenta (número, línea, clase), titular y cotitulares con su proporción, oficina, período `desde`–`hasta`, `InformationCutoff` |
| Saldo inicial | Por concepto al día anterior a `desde` (capital, interés por pagar; en aportes, por clase) |
| Movimientos | Por **fecha valor**: fecha valor, fecha contable, tipo (`MemberAccountTransactionKind` en español), concepto, valor con signo, número de operación, recaudador o canal; los reversos enlazados al original |
| Intereses | Causado en el período, liquidado, ajuste de redondeo; tasa o tramo aplicado y método de saldo |
| Retención | Base, componente inflacionario descontado, tarifa, retención; o «no se retuvo: ningún día alcanzó el umbral» |
| GMF | GMF cobrado, exención usada en el mes y restante (política vigente) |
| Cargos | Cargos cobrados, pendientes y condonados; estado de la factura si está gravado |
| Saldo final | Por concepto y **disponible desglosado** (saldo − bloqueos − embargos − canje − mínimo, FR-057) |

- **Misma copia** (US13-1): pedir el mismo período de la misma cuenta, **sin operaciones registradas después del
  `InformationCutoff` del emitido que toquen el período** y sin anulación, devuelve el documento ya emitido (no consume
  número); si hay, se emite uno nuevo.
- En aportes, el extracto muestra las cuotas exigibles del período y su estado.

### 2.2 Certificado de saldo (`AccountBalanceCertificate`, FR-103)

Saldo por concepto de una cuenta a una fecha D ≤ hoy, con R = el instante de emisión: estado, fecha de apertura,
titulares y proporciones, manejo, bloqueos y embargos vigentes (sólo si el titular lo pide, `includeRestrictions`), y
la leyenda «certifica el saldo registrado al {D} con la información disponible al {R}». Una D futura responde
`Lending.Document.FutureDateNotAllowed` **(nuevo en contracts)**.

### 2.3 Certificado tributario anual (`DepositTaxCertificate`, FR-104, SC-016)

Por **titular** y **año** terminado (`Lending.Document.TaxYearNotClosed` si no, el de la 013; enmienda E171):

| Bloque | Contenido |
|---|---|
| Identificación | Agente retenedor (cooperativa, NIT), titular, año gravable, fecha de expedición |
| Por cuenta | Número, clase, **saldo al 31 de diciembre**, rendimientos abonados o pagados en el año, retención practicada, componente inflacionario (no constitutivo de renta), GMF retenido |
| Cotitulares | Cada titular recibe su certificado **por su proporción** registrada; sin proporción, por partes iguales (Edge Cases) |
| Totales | Suma por concepto; cuadra al centavo con la suma de liquidaciones, retenciones y GMF del año (`LND_WithholdingAccumulations`, `LND_GmfMonthlyUsage`) incluida la parte migrada (`tax-year-to-date`) |
| Aportes | Saldo de aportes al 31 de diciembre y revalorización del año **sólo** si el parámetro `Documentos.CertificadoTributarioIncluyeAportes` **(nuevo en contracts)** lo dice (P10, pendiente de la contadora) |

El lote anual (`POST /api/lending/documents/batches`) emite uno por titular con productos en el año.

### 2.4 Certificado de retención (`WithholdingCertificate`, FR-105)

Por titular y período (mes, bimestre o año; `period: { from, to }`): agente retenedor, titular, concepto (rendimientos
financieros), **base sometida a retención**, tarifa, valor retenido, componente inflacionario descontado, ciudad y
fecha de la consignación del período cuando Contabilidad la informe; leyenda de expedición sin firma autógrafa según el
reglamento tributario vigente (texto en parámetro, `Documentos.LeyendaCertificadoRetencion` **(nuevo en contracts)**).

### 2.5 Título del CDAT (`TermDepositTitle`, FR-068)

Leyenda destacada **«CDAT — NO NEGOCIABLE»** y «título de legitimación; no es título valor (C. de Co., art. 645)».

| Bloque | Contenido |
|---|---|
| Identificación | Número del título **con sufijo de versión** (R11: `TIT-000123-1`, `-2`…), número de la cuenta, título anterior enlazado (prórroga o renovación) |
| Titulares | Titular, cotitulares, manejo (indistinto o conjunto), representante si es menor; beneficiarios sólo si el titular lo pide |
| Condiciones selladas | Capital, fecha de constitución y de inicio de la versión, plazo en días, fecha de vencimiento (siguiente hábil, D19), tasa **efectiva anual** y su nominal equivalente para la periodicidad, tasa de la tabla y margen negociado si lo hay, periodicidad y modalidad de intereses (vencido o anticipado), destino de los intereses |
| Reglas del reglamento | Instrucción al vencimiento, prórroga automática y su regla de tasa, días de gracia (hábiles), redención anticipada (sí/no y penalidad), tratamiento si no se prorroga |
| Retención y GMF | Leyenda: «los rendimientos están sujetos a retención en la fuente y el GMF según la norma vigente» |
| Seguro de depósitos | Sólo si la cooperativa está inscrita (perfil regulatorio): leyenda del seguro con el tope vigente |

- Nace **automáticamente** en la transacción de la constitución (cuando el CDAT queda `Active`), de la prórroga y de la
  renovación; la emisión anterior recibe `Superseded`. Redimir o cancelar le da `Settled`.
- El reverso de la constitución (fondeo reversado) **anula** el título (`Voided`).
- Un título no se «reimprime para entregar» si está `Superseded`, `Settled` o `Voided`
  (`Lending.Document.VoidedCannotReissue`); sí se descarga para consulta.
- **Enmienda E173**: sin firmante o con el almacén caído la operación sigue sin título
  (`[Cartera.TituloDeCdatSinEmitir]`) y el pendiente se emite con `POST /api/lending/term-deposits/{id}/titles`
  (`Lending.Documents.Issue`, con clave; devuelve el vigente si ya existe). Los CDAT **migrados** no reciben título al
  activar: se emite por esa ruta. La prórroga del proceso diario emite el título sin persona (`IssuedByUserId` nulo,
  evento con `ActorKind = Process`; E168).

### 2.6 Constancia de aportes (`ContributionCertificate`, FR-106)

Asociado, fecha de vinculación, saldo de aportes a una fecha **por clase** (mínimos, ordinarios, extraordinarios,
revalorizados), mínimo no reducible, ahorro permanente (si `includePermanentSavings`), cuotas exigibles pendientes y
la leyenda «los aportes sociales son garantía de las obligaciones del asociado con la cooperativa y son inembargables
(Ley 79 de 1988, art. 49)». Sale del mismo saldo que `IConsultaDeAportes` (SC-010).

### 2.7 Paz y salvo de aportes (`ContributionClearance`, FR-106)

Sólo sin cuotas exigibles pendientes a hoy con la información de ahora; si no, se niega con
`Lending.Document.ClearanceDenied` y `data: { installments: [{ period, dueDate, pending }] }`. **Sujeto a firmeza**:
`FinalityUntil` = registro del último pago que dejó las cuotas al día + los días hábiles de
`Pagos.FirmezaDiasPorClaseDeMedio` de su clase de medio (cruce interno, cero). Leyenda: «certifica sólo las cuotas de
aportes; no certifica obligaciones de crédito ni el estado de asociado».

### 2.8 Liquidación del retiro (`MembershipWithdrawalSettlement`, FR-076–FR-080)

Nace **automáticamente** al liquidar (`SettleMembershipWithdrawalCommand`):

| Bloque | Contenido |
|---|---|
| Trámite | Número, asociado, causa, fecha de radicación, aprobación (órgano, sesión, punto de acta, fecha), fecha efectiva |
| Aportes | Por clase al momento de liquidar |
| Ahorros | Por cuenta: capital, intereses liquidados a la fecha, retención, GMF, neto |
| CDAT | Los vigentes que **continúan** hasta su vencimiento (o se liquidan si la línea lo dice) |
| Cruces | Por crédito: número, saldo antes, valor aplicado y distribución (capital, intereses, otros), saldo después, prelación aplicada (D14) |
| Pérdidas | Proporción descontada con su punto de acta, o «sin descuento por pérdidas» (D15) |
| Devolución | Valor, forma (orden de giro o traslado), plazo estatutario; o «pendiente: perforaría el mínimo irreducible de la entidad» (FR-080) |

---

## 3. Emitir, enviar, reimprimir

Rutas de la 013 §15 sin cambio de forma (`api.md` §17). El cuerpo de `POST /api/lending/documents` admite según la
clase:

| Clase | Campos | Errores propios |
|---|---|---|
| `AccountStatement` | `accountPublicId`, `period: { from, to }` | `Lending.Document.PeriodInvalid`, `Lending.Document.NotAccountHolder` (la persona no es titular ni cotitular de la cuenta; enmienda E170) |
| `AccountBalanceCertificate` | `accountPublicId`, `cutoffDate`, `includeRestrictions?` | `Lending.Document.FutureDateNotAllowed` |
| `DepositTaxCertificate` | `personPublicId`, `year` | `Lending.Document.TaxYearNotClosed` (enmienda E171) |
| `WithholdingCertificate` | `personPublicId`, `period` | `Lending.Document.PeriodInvalid` |
| `TermDepositTitle` | `accountPublicId` (sólo descarga del vigente; la emisión es automática) | `Lending.Document.IssuedAutomatically` **(nuevo en contracts)** |
| `ContributionCertificate` | `personPublicId`, `cutoffDate`, `includePermanentSavings?` | |
| `ContributionClearance` | `personPublicId` | `Lending.Document.ClearanceDenied` |
| `MembershipWithdrawalSettlement` | `membershipWithdrawalPublicId` **(nuevo en contracts)** (sólo descarga; la emisión es automática) | `Lending.Document.IssuedAutomatically` |

- **Entrega**: `Download` o `Email` (mensaje con propósito `DocumentDelivery` por los canales autorizados del titular;
  exento de las ventanas si lo pidió él).
- **Permisos**: `Lending.Documents.Read`, `.Issue`, `.Reprint`, `.Void`, `.Verify` (los de la 013); la emisión cuenta
  como lectura sensible y se audita (FR-003).
- **Antes de activar** la naturaleza: `Lending.Savings.NotActivated` o `Lending.Contributions.NotActivated`.
- **Verificar** (US13-2): `GET /api/lending/documents/verify/{code}` muestra el original y su estado (vigente,
  reemplazado, liquidado o anulado), sólo a usuarios de la cooperativa (D21 de la 013).

---

## 4. Anulación por reverso (FR-101)

`VigenciaDeDocumentos` (puro) se **extiende a las cuentas** y corre dentro del comando que reversa, en la misma unidad
de trabajo: el reverso por el contrato (`ReversarAsync`), el reverso desde la pantalla de la Cartera, el reintegro por
un giro rechazado, el reverso del pago del cruce o del débito automático y el reverso de una distribución de asamblea.
Anula, con el evento `Voided` y la operación causa:

| Clase | Se anula si el reverso… |
|---|---|
| `AccountStatement` | toca una operación con fecha valor hasta el **fin** del período del extracto (una anterior cambia el saldo inicial que certificó; enmienda E179) cuyo registro es anterior o igual a su `InformationCutoff` |
| `AccountBalanceCertificate` | toca una operación con fecha valor ≤ su fecha de corte y registro ≤ su `InformationCutoff` |
| `DepositTaxCertificate` · `WithholdingCertificate` | toca una liquidación, una retención o un GMF del año (o del período) del certificado — US13: reversar una retención de diciembre anula el certificado tributario del año y el nuevo cambia |
| `TermDepositTitle` | reversa la constitución, la prórroga o la renovación que lo emitió |
| `ContributionCertificate` | toca una operación de aportes con fecha valor ≤ su corte |
| `ContributionClearance` | reversa un pago que dejó las cuotas al día (como el paz y salvo de crédito de la 013) |
| `MembershipWithdrawalSettlement` | reversa el pago del cruce o la devolución (el retiro queda `ToRedo`, US10-4); la nueva liquidación emite otro |

La anulación **manual** sigue la 013 (`POST /api/lending/documents/{id}/void`, `Lending.Documents.Void`, con motivo).
Las **respuestas al juzgado** de un embargo **sí** son documentos emitidos (clase `GarnishmentResponse` 15, enmienda E176),
pero no cubren cuentas: el reverso de una operación **no** las anula (es lo que se le dijo al juzgado ese día). Sin
firmante, la respuesta se registra sin documento con el aviso `Lending.Garnishment.ResponseDocumentNotIssued`.

---

## 5. Pruebas

Renderizado con QuestPDF por `IDocumentoDeCarteraRenderer` (una plantilla por clase en `DocumentosDeCarteraReport`):
casos de Application con PDF de texto extraíble para cada clase; «misma copia» del extracto; certificado tributario que
cuadra al centavo con las liquidaciones del año (SC-016) con dos cotitulares al 60/40; reverso de una retención de
diciembre que anula el certificado; título con sufijo de versión tras dos prórrogas y estados `Superseded`/`Settled`;
paz y salvo de aportes negado con cuotas pendientes y sujeto a firmeza; e2e «Captaciones e2e» de US13.

---

## 6. Nombres fijados en este documento (nuevo en contracts)

- Prefijos de número: `EXT`, `CSA`, `CTR`, `CRF`, `TIT` (con sufijo de versión `-n`), `CNA`, `PSA`, `LRA`.
- Tabla `LND_IssuedDocumentAccounts` (`IssuedDocumentAccount`); eventos `IssuedDocumentEventKind.Superseded`,
  `.Settled`; estados calculados `IssuedDocumentStatus.Superseded`, `.Settled`.
- Parámetros `Documentos.CertificadoTributarioIncluyeAportes`, `Documentos.LeyendaCertificadoRetencion`.
- Campos del cuerpo `accountPublicId`, `membershipWithdrawalPublicId`, `includeRestrictions`,
  `includePermanentSavings`.
- Errores `Lending.Document.FutureDateNotAllowed`, `Lending.Document.PeriodInvalid`,
  `Lending.Document.IssuedAutomatically`.

## 7. Conciliación

| Punto | Decisión |
|---|---|
| research §0.3 nombra las clases 7–14 sin prefijos | Prefijos de tres letras que no chocan con los de la 013 |
| Título «nuevo» en cada prórroga (FR-068) frente a «misma cuenta, nueva versión» (R11) | Un documento `TermDepositTitle` por versión de condiciones; el número del título lleva el sufijo de la versión y el anterior queda `Superseded` |
| Respuesta al juzgado «como documento» (R23) | **Enmienda E176**: sí es un documento emitido, clase `GarnishmentResponse` 15 (prefijo `RJZ`), y su Id queda en `LND_GarnishmentResponses.IssuedDocumentId` |
