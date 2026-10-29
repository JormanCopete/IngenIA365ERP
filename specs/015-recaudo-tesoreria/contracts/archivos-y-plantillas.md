# Formatos de archivo y plantillas — Recaudo y Tesorería

**Feature**: 015 | **Fecha**: 2026-10-22 | Motor común de la 010 N4 (D-42) y de la 013 K2

Todos los archivos bancarios se leen y escriben con **un solo motor**: `Application/Common/BankFiles/FlatFileWriter` y
`FlatFileReader`, sobre `COR_BankFileFormats` / `COR_BankFileFormatFields` (formato por banco o genérico, ámbito y
vigencia). Recaudo y Tesorería no escriben un parser propio. Las estructuras reales las aporta el dueño o el banco (P1,
P6): son **datos versionados**, nunca código.

## 1. Ámbitos (`BankFileScope`)

| Valor | Nombre | Sentido | Usa | Entrega |
|---|---|---|---|---|
| 1 | `PayrollDisbursement` | escritura | Nómina | existente |
| 2 | `SeveranceDeposit` | escritura | Nómina | existente |
| 3 | `TreasuryPayments` (antes `SupplierPayments`, reservado sin filas; se renombra sin cambiar el número) | escritura | Tesorería: archivo de pago de un lote | **T0** |
| 4 | `LoanDisbursement` | escritura | Cartera (archivo de desembolsos) | existente |
| 5 | `PayrollDeductionRequest` | escritura | Cartera (archivo de descuentos) | existente |
| 6 | `PayrollDeductionResult` | lectura | **Recaudo**: respuesta de la pagaduría | R0 |
| 7 | `BankStatement` | lectura | Tesorería: extracto o archivo de movimientos | **R0/T0** |
| 8 | `CollectionAgreementFile` | lectura | Recaudo: archivo diario del convenio | R2 |
| 9 | `TreasuryPaymentResponse` | lectura | Tesorería: respuesta del banco a un archivo de pago | **T0** |
| 10 | `PaymentGatewayFile` | lectura | Recaudo: reporte de pasarela o PSE | R2 |
| 11 | `AcquirerSettlement` | lectura | Tesorería: abonos de la red de tarjetas | T2 |

Reglas de la 010 que siguen: dos formatos activos del mismo banco y ámbito no se cruzan en el tiempo
(`Core.BankFileFormat.Overlaps`); un formato que ya generó o leyó archivos no cambia de estructura (`InUse`: se cierra la
vigencia y se crea la versión nueva); un archivo ya generado conserva su formato (Edge Cases).

## 2. Orígenes de campo nuevos

Los del contexto genérico (`Payee*`, `Amount`, `Concept`) sirven al archivo de pago. Se agregan, para lectura:

| Ámbito | Campos que el formato puede mapear |
|---|---|
| `BankStatement` | `MovementDate`, `ValueDate`, `Direction` (o signo), `Amount`, `Debit`, `Credit`, `Reference`, `BankSequence`, `DepositorTaxId`, `DepositorName`, `Description`, `Balance`, `OfficeCode` |
| `TreasuryPaymentResponse` | `LineNumber`, `BeneficiaryTaxId`, `BankCode`, `AccountNumber`, `Amount`, `Outcome` (código del banco), `BankCause`, `BankDate`, `BankReference` |
| `PayrollDeductionResult` (ya existe) | sin cambio |

Cada formato de lectura declara su **huella de movimiento** (FR-197): `BankSequence` si viene; si no, la combinación
`(banco, cuenta, MovementDate, Amount, Reference)` y la marca de posibles duplicados.

## 3. Archivo de pago (T0)

- **Contexto** (`BankFileContext`): empresa, cuenta de origen (de `TES_BankAccounts`), lote (número, fecha, total, cantidad).
- **Líneas** (`BankFileLineValues`): por ítem del lote — beneficiario (tipo y número de documento, nombre), banco
  (`TransferCode`), tipo y número de cuenta destino, valor neto, concepto (número de la orden y del origen), referencia.
- El archivo es un adjunto no borrable `TreasuryPaymentFile` con SHA-256, subido **antes** de guardar `TES_PaymentFiles`
  (molde de `PAY_BankDisbursementFiles`); cada línea guarda su huella para el cotejo de la respuesta (FR-212).
- Sin formato del banco: `PAGOS-GENERICO` (CSV con encabezados) o la forma **portal del banco** (sin archivo: el tesorero
  digita en el portal y registra el resultado a mano con soporte, FR-213).

## 4. Formatos y plantillas genéricas sembrados (E-06)

| Código | Ámbito | Contenido | Para qué |
|---|---|---|---|
| `EXTRACTO-GENERICO` | 7 | CSV/XLSX: fecha, valor (con signo) o débito y crédito, referencia, documento y nombre del consignante, descripción, saldo | banco sin formato o extracto en PDF transcrito (FR-196) |
| `PAGOS-GENERICO` | 3 | CSV: documento, nombre, banco (código ACH), tipo y número de cuenta, valor, concepto | FR-213 |
| `RESPUESTA-PAGOS-GENERICO` | 9 | CSV: línea, documento, cuenta, valor, resultado (`Pagado`/`Rechazado`), causal, fecha | FR-118 |
| `RESPUESTA-PAGADURIA-GENERICA` | 6 | XLSX: documento del empleado, código de la pagaduría (v2) o número de obligación (v1), esperado, descontado, fecha, causal | FR-201 |

Todos entran «pendiente de validar» con el dueño; cada cooperativa puede crear los suyos por banco.

## 5. Plantillas de importación de puesta en marcha (FR-157)

Plataforma de la 012 (`EjecutorDeImportacion`): encabezados en la fila 1, instrucciones en otra hoja, revisión previa
(`?mode=preview`), **todo o nada** con error por fila y columna (`data.errors[] { row, column, code }`), auditada con un
evento resumen.

| Código | Módulo | Columnas principales | Reglas |
|---|---|---|---|
| `recaudo.puntos-y-cajas` | REC | oficina, código y nombre del punto, código y nombre de la caja, base, modo, topes, caja principal | códigos únicos; oficina existente |
| `recaudo.cajeros-y-alcances` | REC | documento del usuario, caja, punto, oficina, rol | usuario con persona y segundo factor (aviso si falta: condición del go/no-go) |
| `recaudo.medios-por-punto` | REC | punto, código del medio | clases admitidas en la entrega (§1.5 del modelo) |
| `recaudo.bovedas-al-corte` | REC | oficina, denominación, cantidad | suma = saldo declarado; sin hechos |
| `recaudo.partidas-al-corte` | REC | cuenta propia, fecha (obligatoria), valor, referencia, consignante | fila sin fecha rechazada |
| `recaudo.cheques-en-canje` | REC | banco, número, cuenta, girador, valor, fecha, estado | |
| `tesoreria.cuentas-bancarias` | TES | banco, número, tipo, oficina, propósitos, cuenta contable (código), exención GMF, saldo al corte | vínculo contable por E-04; fondo de liquidez exclusivo |
| `tesoreria.chequeras` | TES | cuenta, rango, siguiente número | |
| `tesoreria.cheques-en-transito` | TES | cuenta, chequera, número, beneficiario, valor, fecha, entregado (sí/no) | chequera registrada antes (Edge Cases) |
| `tesoreria.remesas-y-consignaciones-en-transito` | TES | cuenta, oficina, valor, fecha, tipo | |
| `recaudo.parametros` · `tesoreria.parametros` | REC · TES | clave, valor, vigente desde, motivo, fuente legal | segunda ronda (D-l): las mismas reglas que el alta de una vigencia (`AddParameterVersionCommand`: clave del catálogo del módulo, permiso adicional, valor admitido, fuente legal, reglas del módulo, sin cruces); todo o nada |
| `plataforma.cuentas-bancarias-de-personas` | plataforma | documento, banco, tipo, número, titular | entran `PendingApproval`; las aprueba otra persona |
| `recaudo.convenios` | REC | — | R2 |
| `tesoreria.pendientes-heredados` | TES | cheques sin cobrar, facturas por pagar del módulo heredado | antes del retiro (después de T2) |

Las cifras de referencia (`…/reference-figures`) se cargan con su propia plantilla (concepto, dimensión, valor) y la
puesta en marcha muestra cargado contra referencia contra contabilidad, sin nombrar el sistema de origen (FR-160).

## 6. Reporte de transacciones (R1, FR-096)

Layout JSON embebido con vigencia en `Application/Receipts/Compliance/Layouts/uiaf-trso-*.json`, sobre el motor
genérico `Domain/Common/Layouts` de la 013. **Desde la segunda ronda del 2026-10-27** es el Anexo 2 TRSO de la
Supersolidaria (`UIAF-TRSO-v1`, versión `2022-12`, vigente desde 2026-01-01): ancho fijo de 645 posiciones en
`windows-1252`, registros `Header` (consecutivo 0, código de la organización, fecha de corte, total, X hasta 645),
`Detail` (22 campos, uno por transacción) y `Trailer` (0, código, total, X hasta 645), todos con `verified = true` porque
se cotejaron con los cuadros 1 a 3 del PDF. Si una versión nueva llega sin cotejar, cada generación lleva
`Recaudo.Sarlaft.LayoutSinCotejar` como advertencia, nunca dentro del archivo. Nombre `TRSOSSTTTCCCCCMMAA.txt`. Las
múltiples van operación por operación y ninguna transacción se repite; el reporte de ausencia **no** va en el archivo (se
presenta en SIREL): la generación sin transacciones queda `Validated` con `IsAbsenceReport` y recibe su radicado. Sin
`Sarlaft.Reporte.CodigoDeOrganizacion` o `Sarlaft.Reporte.UsuarioSirel` hay bloqueante (`Recaudo.Sarlaft.CodigoDeOrganizacionFaltante`,
`.UsuarioSirelFaltante`) y no hay archivo. Con bloqueantes queda una versión `Validated` sin archivo; regenerar deja la
anterior `Superseded`; marcar cargado con radicado. Alcance (efectivo, cheque y plataforma virtual por defecto), estado
por período, recordatorio de ausencia y códigos de producto son parámetros (§23 del modelo).

## 7. Documentos (QuestPDF, con código de verificación)

| Documento | Formato | Entrega |
|---|---|---|
| Recibo de caja (tirilla de 80 mm y carta), «copia» al reimprimir; con pagador tercero, producto enmascarado y valor | `DocumentosDeRecaudoReport.Recibo` | R0 |
| Documento de anulación | `…Anulacion` | R0 |
| Informe de cierre de sesión y del día por medio | `…CierreDeSesion`, `…CierreDelDia` | R0 |
| Acta de arqueo sorpresivo, de remesa y de billete retenido | `…Acta` | R0 |
| Egreso de caja firmado | `…Egreso` | R1 |
| Cheque girado (impresión sobre el formato de la chequera) | `DocumentosDeTesoreriaReport.Cheque` | T0 |
| Informe de conciliación mínima (saldo del extracto, partidas, saldo en libros) | `…Conciliacion` | T0 |
| Comprobante de pago al beneficiario | `…ComprobanteDePago` | T1 |
