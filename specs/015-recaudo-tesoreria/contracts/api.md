# API REST — Recaudo y Tesorería

**Feature**: 015 | **Fecha**: 2026-10-22 | Convenciones de la 012 y la 013.

Rutas Carter en `API/Endpoints/Receipts/*` (`/api/receipts`) y `API/Endpoints/Treasury/*` (`/api/treasury`), con
grupos `ReceiptsGroup.Grupo(app, …)` y `TreasuryGroup.Grupo(app, …)` que aplican sesión, cooperativa resuelta y el
limitador de la cooperativa. Cada ruta declara su permiso (`LosEndpointsProtegidosExigenPermiso`, SC-018). Las rutas
heredadas `/api/treasury/{checks,concepts,invoices,cash-flow}` **no cambian** hasta su retiro después de T2 (R-04).

## 1. Convenciones

### 1.1 Identificadores y cuerpos

- Sólo `PublicId` en rutas, cuerpos y respuestas (Principio VI); los números visibles son datos de búsqueda.
- Enums: entran por nombre o número y salen como número (`EnumPorNombreONumero`).
- Montos en pesos con dos decimales; fechas `yyyy-MM-dd` en hora de Colombia; instantes UTC.

### 1.2 Idempotencia

Toda escritura lleva `Idempotency-Key` (Guid) (`LosComandosDeRecaudoYTesoreriaLlevanClave`). Misma clave y misma
huella → la respuesta original (`200` con el mismo documento); misma clave y otra huella → `409`
`Operation.KeyReused`. La caja usa la **misma** clave al reconfirmar tras `DistributionChanged` o tras una
aprobación (FR-172).

### 1.3 Respuestas de error

Sobre de la API (`code`, `message`, `data`). `400 Validation.Invalid` con `data.errors[] { field, code }`; `404
Generic.NotFound` también ante lo que está fuera de alcance; `409` en conflictos de estado; `422` en reglas de negocio
(incluidos los rechazos por ítem de un contrato, con `data.items[] { applicationPublicId, portionKey, code, message }`).
Las pantallas ramifican por `code`, nunca por el texto.

### 1.4 Aprobación presente (caja)

Una operación que exige aprobador presente responde `428` `Approvals.PresenceRequired` **(nuevo)** con
`data.approvalRequestPublicId` y el monto; la pantalla pide el desafío (`/api/approvals/presence-challenges`, de la 012), y
la operación se repite con la misma `Idempotency-Key` y el encabezado `X-Approval-Request` **(nuevo)**. El código `428`, el
código y el encabezado son nuevos (decisiones-transversales.md §2.7); las fallas del desafío no lo son: responden `422`
con los `Approvals.Presence.*` de la 012 (`NotRequester`, `Invalid`, `Expired`, `TotpReused`), nunca `401`.

### 1.5 Permisos

Se siembran completos con R0/T0 (FR-011); los de entregas futuras existen desde el primer día y sus rutas responden
`404` hasta su entrega. Plantillas de rol sembradas (FR-176): `recaudo.cajero`, `recaudo.supervisor`,
`recaudo.analista`, `tesoreria.tesorero`, `tesoreria.aprobador`, `tesoreria.conciliador`, `cumplimiento.oficial`,
con los permisos de Cartera y Captaciones que los contratos verifican sobre quien actúa (`Lending.Payments.BackdateBeforeClose`
con monto, `Lending.Accounts.Deposit`, `Lending.Accounts.Withdraw` con monto). Activar Recaudo exige al menos un cajero
y un supervisor con esos permisos.

---

## 2. Recaudo · configuración (R0)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET/POST /api/receipts/collection-points` · `PUT /{id}` · `POST /{id}/inactivate` | `Receipts.Setup.View` / `.Manage` | puntos de recaudo |
| `GET/POST /api/receipts/cash-boxes` · `PUT /{id}` · `POST /{id}/inactivate` | ídem | cajas (base, topes, caja principal) |
| `GET/POST /api/receipts/cash-boxes/{id}/cashiers` · `POST /{id}/cashiers/{userId}/retire` | ídem | cajeros |
| `GET/POST /api/receipts/collection-points/{id}/payment-means` · `POST …/{meansId}/retire` | ídem | medios por punto (FR-023) |
| `GET/POST /api/receipts/transfer-accounts?collectionPoint=` · `POST /{id}/disable` | ídem | segunda ronda (D-f, FR-231): cuentas propias de recaudo habilitadas por punto (caja opcional) y medio de transferencia o consignación; con clave; errores `Receipts.TransferAccount.{MeansNotTransfer, MeansNotOffered, AccountNotForCollection, AlreadyEnabled}` |
| `GET /api/receipts/sessions/{id}/transfer-accounts` | `Receipts.Receipt.Register` | segunda ronda (D-f): lo que el cajero elige en su sesión; `PUT …/cash-operations/{id}/means` admite `ownBankAccountPublicId` por medio (con una habilitada se propone; si no, `Receipts.Receipt.OwnBankAccountRequired` con `data.options`, o `.OwnBankAccountNotEnabled`) |
| `GET/POST /api/receipts/document-types` · `PUT /{id}` | ídem | tipos de recibo, egreso, anulación, movimiento |
| `GET/POST /api/receipts/misc-concepts` · `PUT /{id}` · `POST /{id}/inactivate` | ídem | conceptos varios (FR-027, FR-179) |
| `GET/POST /api/receipts/contingency-books` · `POST /{id}/void` | ídem | talonarios |
| `GET/POST /api/receipts/vaults` · `PUT /{id}` · `GET/POST /{id}/custodians` | ídem | bóvedas (FR-035, FR-180) |
| `GET/POST /api/receipts/supervisor-delegations` · `POST /{id}/end` | `Receipts.Setup.Manage` | suplencias (FR-184) |
| `GET/PUT /api/receipts/channel-modality-rules` | ídem | modalidad por canal (FR-198) |
| `GET/PUT /api/receipts/users/{userId}/scope` | `Receipts.Scope.Manage` | alcance (FR-011) |
| `GET /api/catalogos/{catalogo}/codigo/{codigo}` | (existente) | duplicado de código antes de guardar (US1-4) |

## 3. Recaudo · sesión, arqueo y bóveda (R0)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `POST /api/receipts/sessions` | `Receipts.Session.Open` | abrir con la base por denominaciones |
| `GET /api/receipts/sessions?state=&point=&date=` · `GET /{id}` | `Receipts.Session.View` | con alcance |
| `GET /api/receipts/sessions/current` | `Receipts.Session.Open` | la sesión del cajero |
| `GET /api/receipts/sessions/{id}/expected` | `Receipts.Session.ViewExpected` | lo esperado (oculto con arqueo ciego hasta registrar lo contado) |
| `POST /api/receipts/sessions/{id}/movements` | `Receipts.Session.Move` | abastecer, retirar a bóveda, trasladar entre cajas (doble custodia) |
| `POST /api/receipts/sessions/{id}/reclassifications` | `Receipts.Session.Reclassify` | reclasificar el medio de un recibo (soporte + aprobador presente) |
| `POST /api/receipts/sessions/{id}/count` | `Receipts.Session.Close` | registrar lo contado por medio |
| `POST /api/receipts/sessions/{id}/differences/{lineId}/approve` | `Receipts.Count.ApproveDifference` | otra persona, motivo y tratamiento |
| `POST /api/receipts/sessions/{id}/close-forgotten` | `Receipts.Session.CloseForgotten` | supervisor con testigo |
| `POST /api/receipts/sessions/{id}/adjustments` | `Receipts.Session.Adjust` | ajuste de una sesión cerrada |
| `POST /api/receipts/surprise-counts` | `Receipts.Session.SurpriseCount` | arqueo sorpresivo de sesión o bóveda con acta |
| `POST /api/receipts/collection-points/{id}/days/{date}/close` | `Receipts.Day.Close` | cerrar el día del punto |
| `GET /api/receipts/vaults/{id}` · `POST /{id}/movements` | `Receipts.Vault.View` / `.Move` | movimientos de bóveda |
| `POST /api/receipts/vaults/{id}/remittances` | `Receipts.Vault.Remit` | remesa a banco (llama a `IRemesasDeTesoreria`) |
| `GET /api/receipts/daily-squares?branch=&date=` | `Receipts.Reports.View` | cuadre diario (FR-036) |

## 4. Recaudo · caja y recibo (R0)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET /api/receipts/payables?person=&document=&obligationNumber=&includeCoDebtor=` | `Receipts.Receipt.Register` | lo que puede pagar una persona (FR-037, lectura sensible auditada) |
| `POST /api/receipts/cash-operations` | `Receipts.Receipt.Register` | crear la operación de caja (persona, pagador, quien efectúa); consulta listas |
| `PUT /api/receipts/cash-operations/{id}/items` | ídem | aplicaciones elegidas; simula cada una con su módulo y devuelve distribución, avisos, excedente, alternativas del abono extraordinario |
| `PUT /api/receipts/cash-operations/{id}/means` | ídem | medios e imputación propuesta (o la fijada por el cajero) |
| `POST /api/receipts/cash-operations/{id}/declaration` | ídem | declaración de origen de fondos con su adjunto (FR-094) |
| `POST /api/receipts/cash-operations/{id}/confirm` | ídem | **confirma**: recibo, aplicaciones por los contratos, hechos; `422` por ítem o `DistributionChanged`; `428` si falta aprobador |
| `POST /api/receipts/cash-operations/{id}/abandon` | ídem | |
| `POST /api/receipts/cash-operations/{id}/release` | `Receipts.Compliance.Release` | el oficial libera una operación retenida |
| `POST /api/receipts/contingency-receipts` | `Receipts.Receipt.RegisterContingency` | digitar un recibo del talonario (fecha valor del talonario, imagen) |
| `GET /api/receipts/receipts?…` · `GET /{id}` | `Receipts.Receipt.View` | con alcance; el de un pagador tercero, enmascarado |
| `GET /api/receipts/receipts/{id}/document?format=thermal\|letter&copy=` | `Receipts.Receipt.Print` | PDF con código de verificación; «copia» al reimprimir |
| `POST /api/receipts/receipts/{id}/voiding` | `Receipts.Voiding.Request` | pedir la anulación con motivo |
| `POST /api/receipts/voidings/{id}/approve` · `/reject` | `Receipts.Voiding.Approve` (con monto) | otra persona; aplica la anulación por los contratos |
| `GET /api/receipts/module-reversals?state=` · `POST /{id}/confirm-cause` | `Receipts.Reversals.View` / `.Resolve` | aplicaciones reversadas por el módulo (FR-195) |
| `GET /api/receipts/payer-credit-balances?…` · `POST /{id}/refund-by-order` · `POST /{id}/apply` | `Receipts.CreditBalances.View` / `.Resolve` | saldos a favor de pagadores (FR-054); por orden de pago en R0 |
| `POST /api/receipts/lending-credit-balances/refund-by-order` | `Receipts.CreditBalances.Resolve` | saldo a favor de Cartera devuelto por el recaudador vía orden de pago (FR-053) |
| `GET /api/receipts/received-checks?…` · `POST /{id}/deposit` · `/clearing` · `/paid` · `/return` | `Receipts.Checks.View` / `.Manage` / `.Return` | cheques recibidos; la devolución reversa por los contratos (FR-060) |

## 5. Recaudo · bancos y partidas (R0)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET /api/receipts/bank-items?account=&from=&to=&status=` | `Receipts.BankItems.View` | abonos por identificar (de los extractos de Tesorería, con alcance) |
| `POST /api/receipts/bank-items/{lineId}/identify` | `Receipts.BankItems.Identify` | identificar y aplicar: divide entre personas y productos con suma exacta; recibo de canal banco en forma no interactiva |
| `POST /api/receipts/bank-items/{lineId}/unidentified` | ídem | dejar como partida por identificar |
| `POST /api/receipts/bank-items/{lineId}/classify` | `Receipts.BankItems.Classify` | a concepto vario con aprobación (reintegro, rendimiento, giro devuelto) |
| `POST /api/receipts/bank-items/{lineId}/duplicate-decision` | `Receipts.BankItems.Identify` | posibles duplicados (FR-197) |
| `GET /api/receipts/unidentified-items?aging=` · `POST /{id}/identify` · `/classify` · `/refund-by-order` · `/move-to-income` | `Receipts.UnidentifiedItems.View` / `.Resolve` / `.MoveToIncome` | partidas (FR-070 a FR-073) |
| `GET /api/receipts/processing-runs/{id}` · `POST /{id}/lines/{lineId}/retry` | `Receipts.BankItems.Identify` | corridas no interactivas y reproceso con la misma clave |

## 6. Recaudo · pagadurías (R0)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET /api/receipts/payroll-files?paymaster=&period=&state=` · `GET /{id}` · `GET /{id}/file` | `Receipts.Paymasters.View` | archivos recibidos de Cartera (descarga auditada) |
| `POST /api/receipts/payroll-files/{id}/sent` | `Receipts.Paymasters.ConfirmSent` | confirmar envío (canal, fecha, radicado) → `EnvioDeDescuentosConfirmado` |
| `POST /api/receipts/payroll-files/{id}/responses` (multipart o captura) · `GET /{id}/responses/{rid}/preview` | `Receipts.Paymasters.LoadResponse` | cargar respuesta por formato, plantilla genérica o a mano |
| `POST /api/receipts/payroll-files/{id}/responses/{rid}/emit` | ídem | devolver el resultado → `ResultadoDeDescuentos` v1 o v2 |
| `POST /api/receipts/paymaster-remittances` · `GET /{id}` | `Receipts.Paymasters.RegisterRemittance` | registrar el giro (línea de extracto, recibo de caja o cheque) |
| `POST /api/receipts/paymaster-remittances/{id}/apply` | `Receipts.Paymasters.ApplyRemittance` | aplicar las líneas por los dos contratos, cada una su unidad (FR-171) |

## 7. Recaudo · cumplimiento (R0; reporte R1)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET /api/receipts/compliance/cash-transactions?person=&month=` | `Receipts.Compliance.View` | registro de efectivo (lectura sensible) |
| `GET /api/receipts/compliance/signals?…` | `Receipts.Compliance.View` | señales y alertas |
| `GET /api/receipts/compliance/blocked` | `Receipts.Compliance.View` o `.ReleaseBindingMatch` | segunda ronda (D-e, FR-230): operaciones bloqueadas por lista vinculante con las personas que coincidieron |
| `POST /api/receipts/cash-operations/{id}/release-binding-match` | `Receipts.Compliance.ReleaseBindingMatch` | segunda ronda (D-e, FR-230): el oficial libera el falso positivo (`supportAttachmentPublicId` del tipo `ReceiptsScreeningReleaseSupport` de la operación, `reason`); con clave; errores `Receipts.Compliance.NotBlocked`, `.ReleaseByCashier`, `.ReleaseSupportInvalid` |
| `GET/POST /api/receipts/compliance/cash-transaction-reports` · `POST /{id}/regenerate` · `/uploaded` · `GET /{id}/file` | `Receipts.Sarlaft.Report` (R1) | reporte de transacciones (FR-096) |

## 8. Recaudo · R1 y R2 (las rutas existen, responden `404` hasta su entrega)

| Ruta | Entrega |
|---|---|
| `POST /api/receipts/cash-disbursements` (retiro, saldo a favor, egreso vario) · `POST /{id}/void` | R1 |
| `GET/POST /api/receipts/pending-applications` · `POST /{id}/apply` | R1 |
| `GET/POST /api/receipts/reported-deposits` | R1 |
| `GET/PUT /api/receipts/identification-rules` | R1 |
| `GET /api/receipts/commercial-receivables?person=` | R1 |
| `GET/POST /api/receipts/collection-agreements` · `…/files` · `…/reconciliations` · `GET /api/receipts/payment-references` · `…/gateway-files` | R2 |

## 9. Tesorería · cuentas bancarias y fondo de liquidez (T0)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET/POST /api/treasury/bank-accounts` · `PUT /{id}` · `POST /{id}/inactivate` | `Treasury.BankAccounts.View` / `.Manage` | cuenta propia; el alta vincula la cuenta contable por E-04 |
| `GET /api/treasury/bank-accounts/{id}/balances` | `Treasury.BankBalances.View` | libros, extracto y disponible proyectado (lectura sensible) |
| `GET/PUT /api/treasury/users/{userId}/scope` | `Treasury.Scope.Manage` | alcance por cuenta |
| `GET/POST /api/treasury/liquidity-fund-uses` · `POST /{id}/approve` · `/reject` | `Treasury.LiquidityFund.Request` / `.Approve` | uso del fondo (FR-207) |

## 10. Tesorería · órdenes, lotes, archivos y resultados (T0)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET /api/treasury/payment-orders?state=&origin=&age=` · `GET /{id}` · `GET /{id}/chain` | `Treasury.Orders.View` | bandeja única y cadena de regiros |
| `POST /api/treasury/payment-orders/{id}/revalidate` | `Treasury.Orders.Manage` | volver a validar tras corregir el maestro |
| `POST /api/treasury/payment-orders/{id}/destination` | `Treasury.Orders.ResolveDestination` | D-a (2026-10-27, FR-229): completar la cuenta destino de una orden `DestinationMissing` con una cuenta registrada y activa del beneficiario (`personBankAccountPublicId`, `reason`); con clave; errores `Treasury.Order.DestinationNotResolvable`, `.DestinationNotMissing`, `.DestinationAccountInvalid` |
| `POST /api/treasury/payment-orders/{id}/pay-by-check` | `Treasury.Orders.ResolveDestination` | D-a: pasarla a cheque (`reason`); queda lista para un lote de forma «cheques»; con clave; mismos errores |
| `POST /api/treasury/payment-batches` · `PUT /{id}/items` · `GET /{id}/preview` | `Treasury.Batches.Prepare` | armar lote por cuenta de origen y forma; vista previa con el formato y GMF estimado |
| `POST /api/treasury/payment-batches/{id}/submit` | ídem | enviar a aprobación |
| `POST /api/treasury/payment-batches/{id}/items/{itemId}/review` | `Treasury.Batches.Approve` | revisión expresa de una cuenta cambiada (US26-1) |
| `POST /api/treasury/payment-batches/{id}/approve` · `/reject` | `Treasury.Batches.Approve` (con monto) | nivel por nivel; quien prepara no aprueba |
| `POST /api/treasury/payment-batches/{id}/file` | `Treasury.PaymentFiles.Generate` | generar el archivo (inmutable, SHA-256) |
| `GET /api/treasury/payment-files/{id}/download` | `Treasury.PaymentFiles.Download` | otra persona que la que preparó; auditado |
| `POST /api/treasury/payment-files/{id}/sent` | `Treasury.PaymentFiles.MarkSent` | quién lo cargó y quién lo autorizó en el banco |
| `POST /api/treasury/payment-files/{id}/void` · `/rejected-by-bank` | `Treasury.PaymentFiles.Void` | no enviado / rechazo técnico (las órdenes vuelven sin resultado) |
| `POST /api/treasury/payment-files/{id}/responses` · `GET …/{rid}` · `POST …/{rid}/lines/{lineId}/review` | `Treasury.Results.Register` | respuesta del banco con cotejo contra el archivo (FR-212) |
| `POST /api/treasury/payment-orders/{id}/results` | `Treasury.Results.Register` | registro manual con soporte (girada, rechazada); emite `ResultadoDelGiro` |
| `POST /api/treasury/payment-orders/results/bulk` | ídem | en bloque, con soporte (FR-213) |

## 11. Tesorería · chequeras y cheques (T0)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET/POST /api/treasury/checkbooks` · `POST /{id}/void` | `Treasury.Checkbooks.View` / `.Manage` | rango y custodia |
| `POST /api/treasury/checkbooks/{id}/spoiled` | `Treasury.Checks.Void` | anular un cheque en blanco dañado por su número |
| `GET /api/treasury/issued-checks?state=` · `GET /{id}` | `Treasury.Checks.View` (o los demás permisos de cheques) | `GET /{id}` existe desde la segunda ronda (D-j); fuera del alcance de la cuenta, 404 |
| `POST /api/treasury/issued-checks/{id}/deliver` | `Treasury.Checks.Deliver` | entrega con constancia → «girada» |
| `POST /api/treasury/issued-checks/{id}/void` | `Treasury.Checks.Void` | antes de entregar (sin resultado) o entregado no cobrado (→ «cheque no cobrado», con permiso y motivo) |
| `GET /api/treasury/issued-checks/{id}/document` | `Treasury.Checks.Print` | impresión del cheque en el formato de la chequera |

## 12. Tesorería · remesas, extractos y conciliación mínima (T0)

Es la única conciliación bancaria de la cooperativa: reemplaza a la de Contabilidad (E-04 a la 009, FR-055 a FR-060; TR41).
Reabrir un período contable marca `Outdated` las conciliaciones cerradas de ese mes, por `IConciliacionBancariaParaContabilidad`.

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET /api/treasury/remittances?state=` · `POST /{id}/confirm` | `Treasury.Remittances.View` / `.Confirm` | confirmar con soporte si no cruza |
| `POST /api/treasury/statements` (multipart o captura) · `GET /{id}` · `GET /{id}/lines` | `Treasury.Statements.Load` / `.View` | carga única del extracto (R-06) con huella y revisión previa |
| `GET/POST /api/treasury/reconciliations` · `GET /{id}` | `Treasury.Reconciliation.View` / `.Work` | abrir por cuenta y mes |
| `POST /api/treasury/reconciliations/{id}/auto-match` | `Treasury.Reconciliation.Work` | cruce automático por valor, fecha con holgura y referencia |
| `POST /api/treasury/reconciliations/{id}/matches` · `DELETE /{id}/matches/{mid}` (sólo abierta) | ídem | cruce manual uno a varios; deshacer un cruce de una conciliación abierta |
| `POST /api/treasury/reconciliations/{id}/items` | ídem | partidas conciliatorias; un abono sin registro pasa a Recaudo como partida |
| `POST /api/treasury/reconciliations/{id}/close` · `/reopen` | `Treasury.Reconciliation.Close` / `.Reopen` | cierre con informe; reapertura con motivo |

## 13. Tesorería · T1 y T2 (existen, `404` hasta su entrega)

| Ruta | Entrega |
|---|---|
| `GET/POST /api/treasury/manual-orders` · `/manual-order-concepts` | T1 |
| `GET/POST /api/treasury/transfers` · `POST /{id}/approve` | T1 |
| `GET/POST /api/treasury/bank-accounts/{id}/signers` · `/tokens` | T1 |
| `GET /api/treasury/payment-vouchers/{id}/document` · `POST /{id}/send` | T1 |
| `POST /api/treasury/month-end/checks` | T1 |
| `GET/POST /api/treasury/bank-notes` · `/reconciliation-rules` · `/acquirer-settlements` · `/liquidity` · `/cash-flow` · `/payables` | T2 |

## 14. Activación, cuadre, puesta en marcha y go/no-go (R0, T0)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET /api/receipts/activation/requirements` · `POST /api/receipts/activation` | `Receipts.Activation.View` / `.Activate` | requisitos y activación (abre las filas del registro); existen desde el bloque 2 con la activación mínima (TR42) y la fase 19 les suma el cuadre y los demás requisitos |
| `GET /api/treasury/activation/requirements` · `POST /api/treasury/activation` | `Treasury.Activation.View` / `.Activate` | ídem |
| `GET /api/receipts/opening-squares/current` · `POST …/provisional` · `POST …/definitive` | `Receipts.Activation.ProvisionalSquare` / `.Square` | cuadre provisional o definitivo |
| `GET /api/treasury/opening-squares/current` · `POST …/provisional` · `POST …/definitive` | `Treasury.Activation.ProvisionalSquare` / `.Square` | ídem |
| `GET /api/receipts/imports/templates/{code}.xlsx` · `POST /api/receipts/imports/{code}` (`?mode=preview\|apply`) | `Receipts.Imports.Run` | plantillas de puesta en marcha |
| `GET /api/treasury/imports/templates/{code}.xlsx` · `POST /api/treasury/imports/{code}` | `Treasury.Imports.Run` | ídem |
| `GET/POST /api/receipts/reference-figures` · `GET /api/treasury/reference-figures` | `…Imports.Run` | cifras de referencia (FR-160) |
| `GET/POST /api/core/go-live-checklists` · `POST /{id}/items/{itemId}/check` · `POST /{id}/items/{itemId}/override` · `POST /{id}/decision` | `Platform.GoLive.View` / `.Manage` / `.Override` | lista de go/no-go (FR-227) |

## 15. Plataforma (E-06)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET /api/core/people/{id}/bank-accounts` | `Core.PersonBankAccounts.View` | lectura sensible auditada |
| `POST /api/core/people/{id}/bank-accounts` | `Core.PersonBankAccounts.Request` | alta o cambio con certificación |
| `POST /api/core/person-bank-accounts/{id}/approve` · `/reject` · `/retire` | `Core.PersonBankAccounts.Approve` | otra persona; abre el período de espera |

## 16. Contabilidad (E-04)

| Método y ruta | Permiso | Qué |
|---|---|---|
| `GET/POST /api/accounting/receipts/rules` · `GET /template` · `GET /completeness` | `Accounting.ReceiptsMatrix.View` / `.Manage` | matriz de Recaudo |
| `GET/POST /api/accounting/treasury/rules` · `GET /template` · `GET /completeness` | `Accounting.TreasuryMatrix.View` / `.Manage` | matriz de Tesorería |
| `GET/PUT /api/accounting/receipts/voucher-types` · `/api/accounting/treasury/voucher-types` | ídem | tipo de comprobante por hecho |
| `GET /api/accounting/receipts/batches` · `/treasury/batches` | `Accounting.Batches.View` | lotes resumidos |
| `GET /api/accounting/own-bank-account-links` | `Accounting.Accounts.View` | vínculos cuenta propia ↔ contable |
| `GET /api/accounting/bridges-reconciliation?module=REC\|TES` | `Accounting.Reports.View` | conciliación de puentes con Cartera y Captaciones (FR-144) |
| `PUT /api/receipts/accounting-policy` · `/api/treasury/accounting-policy` | `Receipts.Accounting.PolicyChange` · `Treasury.Accounting.PolicyChange` | «bloquear» ↔ «advertir» con motivo (FR-221) |

## 17. Informes (centro de informes, `TablaExportable`)

`GET /api/reports/receipts/{vista}?format=json|xlsx|pdf` (`Receipts.Reports.View`; exportar `Receipts.Reports.Export`,
auditado) y `GET /api/reports/treasury/{vista}` (`Treasury.Reports.View` / `.Export`):

| Vista | Entrega |
|---|---|
| `session-close`, `day-close`, `daily-collection` (punto, caja, cajero, medio, canal, módulo, producto o línea), `receipts-by-cashier`, `voided-receipts`, `exceptions` (fecha valor, reclasificación, contingencia), `unidentified-items` (antigüedad), `payer-credit-balances`, `paymasters`, `returned-checks`, `contingency-pending`, `parallel-comparison` (lo registrado contra las cifras de referencia, FR-226) | R0 |
| `consolidated-collection` (con Nómina y débito automático de sólo lectura), `voidings-with-reissue`, `delinquent-paymasters`, `received-checks`, `cash-transactions`, `pending-application-load` | R1 |
| `channels`, `agreements` | R2 |
| `orders-by-state`, `batches`, `rejected-and-reissue`, `recent-account-changes`, `issued-checks`, `bank-balances`, `minimal-reconciliation` | T0 |
| `payment-vouchers`, `transfers`, `month-end-checks` | T1 |
| `reconciliations`, `liquidity`, `cash-flow` | T2 |

Tableros (`/api/receipts/dashboard` R1, `/api/treasury/dashboard` T2) sin datos personales (FR-155).

## 18. Pantallas (`Shared/Pages/Recaudo`, `Shared/Pages/Tesoreria`)

R0: `/recaudo/caja` (la caja de tres pasos con atajos), `/recaudo/sesiones`, `/recaudo/cierre-del-dia`, `/recaudo/boveda`,
`/recaudo/recibos`, `/recaudo/anulaciones`, `/recaudo/reversos-de-modulos`, `/recaudo/saldos-a-favor`,
`/recaudo/cheques-recibidos`, `/recaudo/extractos`, `/recaudo/partidas`, `/recaudo/pagadurias`, `/recaudo/cumplimiento`,
`/recaudo/configuracion/{puntos, cajas, medios, tipos-de-documento, conceptos, talonarios, suplencias, alcance}`,
`/recaudo/puesta-en-marcha`, `/recaudo/activacion`.
T0: `/tesoreria/cuentas-bancarias`, `/tesoreria/fondo-de-liquidez`, `/tesoreria/ordenes`, `/tesoreria/lotes`,
`/tesoreria/archivos-de-pago`, `/tesoreria/resultados`, `/tesoreria/chequeras`, `/tesoreria/cheques-girados`,
`/tesoreria/remesas`, `/tesoreria/extractos`, `/tesoreria/conciliacion`, `/tesoreria/puesta-en-marcha`,
`/tesoreria/activacion`.
Plataforma: `/maestros/personas/{id}/cuentas-bancarias` (o pestaña del diálogo de persona), `/admin/go-no-go`.
Contabilidad: `/contabilidad/recaudo/{matriz, tipos-de-comprobante, completitud, lotes}`,
`/contabilidad/tesoreria/{matriz, tipos-de-comprobante, completitud}`.
Las de R1/R2/T1/T2 llegan con su entrega; el menú sólo enlaza páginas con `@page` (`TodoEnlaceDelMenuTieneSuPagina`).
