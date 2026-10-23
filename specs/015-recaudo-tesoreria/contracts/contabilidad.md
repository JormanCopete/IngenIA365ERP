# Hechos a Contabilidad y matriz — Recaudo (`REC`) y Tesorería (`TES`)

**Feature**: 015 | **Fecha**: 2026-10-22 | Enmienda **E-04** a la 009 | Molde: `013/contracts/contabilidad.md`

Recaudo y Tesorería **no escriben asientos ni conocen cuentas** (FR-004). Emiten hechos a la bandeja con destino
`Accounting`, nacidos en la misma transacción que su operación, por el único escritor `EmisorDeMensajes`; Contabilidad
los contabiliza por la matriz de cada emisor y siempre por `AccountingPoster` (un solo camino al libro). Ningún hecho
lleva una cuenta contable (`LosHechosDeRecaudoYTesoreriaNoLlevanCuentas`); llevan **dimensiones por código** y la cuenta
bancaria propia por su `PublicId`, que Contabilidad resuelve por el vínculo (`ACC_OwnBankAccountLinks`).

## 1. Qué cambia en Contabilidad (E-04)

| Pieza | Cambio | Entrega |
|---|---|---|
| `DestinoContabilidad` | Dos ramas nuevas en el enrutador: `REC` → `PostReceiptsFactsCommand` / `PostReceiptsSummaryGroupCommand`; `TES` → `PostTreasuryFactsCommand`. `VersionesAceptadas` suma los tipos de §3 y §4 | R0, T0 |
| `ModuloContable` · `AccountingModules` | `Recaudo = "REC"` · `Receipts = 128` (cuentas «habilitadas para el módulo») | R0 |
| Matrices | `ACC_ReceiptsPostingRules`, `ACC_TreasuryPostingRules` con plantilla de filas sugeridas sin cuenta y completitud | R0, T0 |
| Recibos y comprobantes | `ACC_ReceiptsPostings`, `ACC_TreasuryPostings`; `ACC_ReceiptsVoucherMappings`, `ACC_TreasuryVoucherMappings` | R0, T0 |
| Vínculo | `ACC_OwnBankAccountLinks` (cuenta propia ↔ cuenta contable bancaria, único en los dos sentidos) | T0 |
| Validación previa | `IContabilidadParaRecaudo.EvaluarAsync`, `IContabilidadParaTesoreria.EvaluarAsync` | R0, T0 |
| Consultas | saldo contable por cuenta a una fecha (cuadres), movimientos contables de una cuenta bancaria (conciliación mínima, FR-209), cuentas por cobrar comerciales pendientes (R1), cuentas por pagar pendientes (T2) | R0, T0, R1, T2 |
| Comprobantes | `Accounting.Document.ModuleOwned` para `REC` y `TES` con mensaje «se corrige desde Recaudo / Tesorería» (FR-145) | R0, T0 |
| Guarda de cierre | `Accounting.Period.OpeningSquarePending` (FR-228); en T1, aviso de conciliaciones abiertas y sesiones sin cerrar (FR-132) | R0, T0, T1 |
| Conciliación de puentes | la de la 013 suma el lado de `REC` y `TES` en los puentes `RecaudoPorAplicar`, `DesembolsoPorGirar`, `RefundPayable` | R0, T0 |
| Cambio de política | «bloquear» → «advertir» con permiso propio, motivo, auditoría y alerta diaria (FR-221) | R0, T0 |

## 2. Forma común de un hecho

Sobre de la bandeja (`IntegrationEnvelopeV1` de la 012/013): `originModule` (`REC` o `TES`), origen = el documento de
Recaudo o Tesorería, `originEventKey` `Confirmation` (o `Voiding`, `Reversal`), `personPublicId` = tercero principal,
`branchPublicId` = sucursal contable de la oficina, `operationDate` = fecha contable propuesta. Cuerpo común:

```csharp
public sealed record ReceiptsFactDimensionsV1          // y TreasuryFactDimensionsV1, con sus campos
{
    public string BranchCode { get; init; } = "";       // oficina
    public string? CollectionPointCode { get; init; }    // punto
    public string? CashBoxCode { get; init; }            // caja
    public Guid? OwnBankAccountPublicId { get; init; }   // cuenta bancaria propia (rol Banco por el vínculo)
    public string? OwnBankAccountCode { get; init; }
    public string? MeansCode { get; init; }              // medio de la cooperativa
    public int? MeansClass { get; init; }                // ClaseDeMedioDeRecaudo
    public string? MiscConceptCode { get; init; }
    public string? TargetModule { get; init; }           // CAR (créditos o captaciones), INV, …
    public string? TargetNature { get; init; }           // Loan | Deposit | Contribution | Commercial
    public string? CollectorClass { get; init; }         // Receipts
    public string? Channel { get; init; }                // CashDesk | Bank | Paymaster | Contingency …
}

public sealed record FactLineV1                          // una línea por rol y combinación
{
    public string Role { get; init; } = "";              // §5
    public string Side { get; init; } = "";              // Debit | Credit (lo propone el hecho; la matriz lo respeta)
    public decimal Amount { get; init; }
    public Guid? ThirdPartyPersonPublicId { get; init; } // donde la cuenta lo exija
    public string? CrossDocument { get; init; }          // número del recibo, factura o documento cruce
    public ReceiptsFactDimensionsV1 Dimensions { get; init; } = new();
}
```

Los hechos que corrigen llevan `Related` = el hecho original (`OriginalFactRefV1`) y se fechan en el período abierto;
el original no se toca (FR-064, US8-3).

## 3. Hechos de Recaudo (`REC → Accounting`)

| Tipo v1 | Origen | Débito → Crédito (roles) | Modo de paso | Entrega |
|---|---|---|---|---|
| `ReciboEmitido` | recibo (caja, contingencia) | por porción: `Caja` (efectivo) · `BancoRecaudo` (transferencia, consignación) · `ChequesEnCanje` · `MedioPorCobrar` (tarjetas, R1) → `PuenteDelModulo` (créditos y captaciones: el `RecaudoPorAplicar` de Cartera, por `TargetModule`, `TargetNature`, `CollectorClass`) · `ConceptoVario` · `CuentasPorCobrarComerciales` (R1) | **por lotes resumidos** por día, punto y medio (D25); línea por tercero o cruce sólo donde la cuenta lo exija | R0 |
| `ReciboDeBancoEmitido` | recibo de canal banco | `PartidasPorIdentificar` (si venía de partida) o `BancoRecaudo` → `PuenteDelModulo` · `ConceptoVario` | en línea | R0 |
| `ReciboDePagaduriaEmitido` | recibo de canal pagaduría | `SaldoDePagadurias` (el giro recibido) → `PuenteDelModulo` | en línea | R0 |
| `GiroDePagaduriaRecibido` | giro | `BancoRecaudo` · `Caja` → `SaldoDePagadurias` | en línea | R0 |
| `ReciboAnulado` | anulación | espejo del recibo; si la sesión ya cerró, `PuenteDelModulo` → `SaldoAFavorDePagadores` | en línea | R0 |
| `MovimientoDeCajaRegistrado` | movimiento de caja o de bóveda | `Caja` ↔ `Boveda` ↔ `Caja` (otra caja) | en línea | R0 |
| `MedioReclasificado` | reclasificación | rol del medio origen ↔ rol del medio destino | en línea | R0 |
| `DiferenciaDeArqueoAprobada` | línea de arqueo | faltante: `CuentaPorCobrarCajero` (tercero = el cajero) o `GastoFaltante` → `Caja`; sobrante: `Caja` → `SobrantesDeCaja` | en línea | R0 |
| `AjusteDeSesionRegistrado` | ajuste | según el ajuste | en línea | R0 |
| `DineroRetenidoEnCustodia` · `CustodiaLiberada` | operación retenida | `Caja` → `CustodiaPorLista` · espejo | en línea | R0 |
| `RemesaDespachada` | movimiento de bóveda | `EfectivoEnTransito` → `Boveda` | en línea | R0/T0 |
| `PartidaPorIdentificarReconocida` | identificación `UnidentifiedItem` | `BancoRecaudo` → `PartidasPorIdentificar` | en línea | R0 |
| `PartidaClasificada` | resolución | `PartidasPorIdentificar` o `BancoRecaudo` → `ConceptoVario` (reintegro, rendimiento, giro devuelto) | en línea | R0 |
| `AplicacionReversadaPorModulo` | aviso del módulo | dinero recibido: `PuenteDelModulo` → `SaldoAFavorDePagadores`; no recibido: `PuenteDelModulo` → rol del medio (baja) | en línea | R0 |
| `ChequeRecibidoDevuelto` | devolución | `PuenteDelModulo` (por porción reversada) → `ChequesEnCanje`; con cuenta por cobrar al pagador, `CuentaPorCobrarPagador` | en línea | R0 |
| `SaldoAFavorDePagadorResuelto` | resolución | `SaldoAFavorDePagadores` → `PuenteDeDevolucion` (orden de pago a Tesorería) · `PuenteDelModulo` (aplicado con recibo nuevo) | en línea | R0 |
| `SaldoAFavorDeCarteraDevuelto` | resolución `RefundThroughCollector` | `PuenteDelModulo` → `PuenteDeDevolucion` | en línea | R0 |
| `SobranteLlevadoAIngreso` | decisión aprobada | `SobrantesDeCaja` → `IngresoSobrantes` | en línea | R0 |
| `EgresoDeCajaEntregado` · `EgresoDeCajaAnulado` | egreso | `PuenteDelModulo` / `SaldoAFavorDePagadores` / `ConceptoVario` → `Caja` · espejo | en línea | R1 |
| `GmfDeEgresoCausado` | egreso | `GmfGasto` → `GmfPorPagar` | en línea | R1 |
| `RecaudoPendienteReconocido` · `RecaudoPendienteAplicado` | pendiente de aplicar | `Caja`/`BancoRecaudo` → `RecaudoPendienteDeAplicar` · → `PuenteDelModulo` | en línea | R1 |
| `ComisionDeConvenioRegistrada` | conciliación del convenio | `ComisionesDeConvenio`, `IvaDescontable`, `RetencionesAFavor` → `BancoRecaudo` | en línea | R2 |

## 4. Hechos de Tesorería (`TES → Accounting`)

| Tipo v1 | Origen | Débito → Crédito (roles) | Entrega |
|---|---|---|---|
| `PagoDeOrdenGirado` | resultado «girada» de una transferencia o pago a tercero | `PuenteDeGiroDelOrigen` (Cartera: `DesembolsoPorGirar`; Captaciones: `RefundPayable` por `OrderKind`; Recaudo: `PuenteDeDevolucion`) → `Banco` | T0 |
| `ChequeGiradoEmitido` | cheque emitido | `PuenteDeGiroDelOrigen` → `Banco` | T0 |
| `ChequeGiradoAnulado` | cheque anulado | espejo del emitido (causa `SpoiledBeforeDelivery` o `NotCashed`) | T0 |
| `GmfDePagoCausado` | pago que dispone de una cuenta contable a favor de un tercero (FR-205) | `GmfGasto` → `GmfPorPagar` (o `Banco` si el banco lo cobra en el mismo débito, según parámetro) | T0 |
| `RemesaConfirmada` | remesa cruzada o confirmada | `Banco` → `EfectivoEnTransito` (o al revés, banco a bóveda) | T0 |
| `TrasladoEntreCuentasEnviado` · `TrasladoConfirmado` | traslado | `FondosEnTransito` → `Banco` origen · `Banco` destino → `FondosEnTransito` | T1 |
| `ChequesReclasificadosAlCierre` · `ChequeNoCobradoACuentaPorPagar` | cierre del mes de cheques | `Banco` → `ChequesPorEntregar` (y reverso al inicio del mes) · `Banco` → `CuentaPorPagarChequesNoCobrados` | T1 |
| `NotaBancariaRegistrada` | nota | GMF: `GmfGasto` → `Banco`; comisión: `ComisionesBancarias` + `IvaDescontable` → `Banco`; intereses: `Banco` + `RetencionesAFavor` → `InteresesBancarios` (en bruto) | T2 |
| `ConciliacionDeAdquirenteRegistrada` | conciliación de adquirentes | `Banco` + `ComisionesDeRed` + `IvaDescontable` + `RetencionesAFavor` → `MedioPorCobrar` | T2 |
| `PagoDeProveedorGirado` · `PagoDeCuentaPorPagarGirado` | pago T2 | `CuentasPorPagarProveedores` / `CuentaPorPagarContable` (tercero y cruce) → `Banco` | T2 |
| `UsoDelFondoDeLiquidez` | uso aprobado | informativo (el movimiento lo hace el pago o traslado que lo usa) | T0 |

El rol `PuenteDeGiroDelOrigen` se resuelve por `OrderOrigin` y `OrderKind`, para que la contadora lo mapee a la **misma**
cuenta que el puente del origen: así `DesembolsoPorGirar` y `RefundPayable` quedan en cero (FR-144) y la conciliación de
puentes explica cada diferencia hecho por hecho.

## 5. Roles

| Rol | Emisor | Dimensiones que pesan | Tercero o cruce |
|---|---|---|---|
| `Caja` | REC | caja, punto, oficina, medio | — |
| `Boveda` | REC | oficina | — |
| `EfectivoEnTransito` | REC, TES | oficina, cuenta propia | — |
| `BancoRecaudo` · `Banco` | REC, TES | **por el vínculo** de la cuenta propia (no admite regla) | el banco como tercero si la cuenta lo exige (FR-222) |
| `ChequesEnCanje` | REC | oficina | girador si la cuenta lo exige |
| `MedioPorCobrar` | REC | medio, red | la red |
| `PuenteDelModulo` | REC | módulo, naturaleza, clase de recaudador, oficina | — (los puentes no llevan tercero en el lado de Recaudo salvo que la cuenta lo exija) |
| `ConceptoVario` | REC | concepto | tercero y cruce si el concepto lo exige |
| `PartidasPorIdentificar` | REC | cuenta propia | — |
| `SaldoAFavorDePagadores` · `SaldoDePagadurias` | REC | — | el pagador o la pagaduría |
| `CuentaPorCobrarCajero` · `CuentaPorCobrarPagador` | REC | — | el cajero o el pagador |
| `GastoFaltante` · `SobrantesDeCaja` · `IngresoSobrantes` | REC | oficina | — |
| `CustodiaPorLista` | REC | oficina | la persona |
| `PuenteDeDevolucion` | REC, TES | clase de orden | el beneficiario si la cuenta lo exige |
| `PuenteDeGiroDelOrigen` | TES | origen y clase de orden | el beneficiario si la cuenta lo exige |
| `GmfGasto` · `GmfPorPagar` | REC, TES | clase de egreso | — |
| `CuentasPorCobrarComerciales` | REC (R1) | — | tercero y factura (cruce) |
| `RecaudoPendienteDeAplicar` | REC (R1) | módulo | la persona |
| `FondosEnTransito` · `ChequesPorEntregar` · `CuentaPorPagarChequesNoCobrados` | TES (T1) | cuenta propia | beneficiario en la de no cobrados |
| `ComisionesBancarias` · `InteresesBancarios` · `IvaDescontable` · `RetencionesAFavor` · `ComisionesDeRed` · `ComisionesDeConvenio` | TES (T2), REC (R2) | cuenta propia, red, convenio | el banco, la red o el convenio |
| `CuentasPorPagarProveedores` · `CuentaPorPagarContable` | TES (T2) | — | tercero y cruce |

## 6. Matriz

- **Resolución**: hecho × rol × dimensiones por código; una dimensión exigida por igualdad o `*`; con varias reglas, gana
  la más específica por pesos: caja 256, punto 128, medio 64, concepto 64, clase de orden 64, módulo y naturaleza 32,
  oficina 4, hecho 2 (sólo en roles de resultado: gastos e ingresos). El centro de costo se asigna y no pesa (como C77 de
  la 013). Empate de especificidad: error de configuración que la completitud muestra.
- **Vigencia**: una regla rige desde su fecha; nunca retroactiva sobre lo contabilizado.
- **Plantilla**: `GET …/template` lista las filas sugeridas sin cuenta (hecho × rol × combinación esperada por la
  configuración real de la cooperativa: medios por punto, conceptos, cuentas propias, clases de orden); no se siembra
  ninguna regla con cuenta (P12).
- **Completitud**: por hecho que no esté en «no pasa»; condición del go/no-go al 100 % (FR-227).
- **Validación previa**: una pregunta por combinación con el tiempo `Contabilidad.ValidacionPreviaSegundos`; con
  «Bloquear» la pantalla no confirma (`Receipts.Prevalidation.NotPostable` / `Treasury.Prevalidation.NotPostable`, con
  `data.errors[] { factType, role, combination, whoFixes }`); en procesos no interactivos, aviso y reproceso; sin respuesta,
  bloquea en pantalla (como Cartera) y avisa en procesos (Edge Cases).

## 7. Lotes resumidos de recibos de caja

`PostReceiptsSummaryGroupCommand` toma los `ReciboEmitido` de un día y punto con modo `PorLotes` y genera **un**
comprobante por día, punto y medio, con tercero o cruce sólo donde la cuenta lo exija. El lote corre después de la hora de
corte en el orden fijo de FR-185 (lote de Recaudo → cuadre diario → ciclo diario de Cartera); un punto con el día abierto
no detiene el de los demás: los recibos de sus sesiones cerradas entran y los de las abiertas entran al siguiente
(FR-184). Los anulados del día anterior van como espejo en línea.

## 8. Tipos de comprobante sugeridos (pendientes de validar con la contadora)

| Código propuesto | Nombre | Hechos |
|---|---|---|
| `RR` | Recaudo resumido de caja | `ReciboEmitido` por lote |
| `RB` | Recibo de banco y pagaduría | `ReciboDeBancoEmitido`, `ReciboDePagaduriaEmitido`, `GiroDePagaduriaRecibido`, `PartidaPorIdentificarReconocida`, `PartidaClasificada` |
| `MC` | Movimientos y diferencias de caja | `MovimientoDeCajaRegistrado`, `MedioReclasificado`, `DiferenciaDeArqueoAprobada`, `AjusteDeSesionRegistrado`, custodia, `RemesaDespachada`, `SobranteLlevadoAIngreso` |
| `AR` | Anulaciones y reversos de Recaudo | `ReciboAnulado`, `AplicacionReversadaPorModulo`, `ChequeRecibidoDevuelto`, `SaldoAFavor…` |
| `EG` | Egreso de caja | R1 |
| `TP` | Pago de Tesorería | `PagoDeOrdenGirado`, `ChequeGirado*`, `GmfDePagoCausado`, `RemesaConfirmada` |
| `TB` | Notas y traslados bancarios | T1, T2 |

Antes de sembrarlos se coteja que no choquen con los 24 tipos vigentes; un choque se resuelve con otro código, no
reutilizando un tipo de otro módulo.

## 9. Reglas que se fijan

- Lo de Recaudo y Tesorería no se reversa desde Comprobantes (`LoDeRecaudoYTesoreriaNoSeReversaDesdeComprobantes`); se
  corrige con su documento (anulación, ajuste, reverso) y su hecho espejo.
- `RecaudoNoConoceContabilidad` y `TesoreriaNoConoceContabilidad`: ningún tipo de `Receipts` o `Treasury` nombra un tipo
  de `Accounting` salvo los puertos `IContabilidadParaRecaudo` e `IContabilidadParaTesoreria`.
- La conciliación con Contabilidad (FR-146) compara por conjuntos de cuentas: caja por oficina, bancos, partidas, saldos a
  favor y puentes, con cero diferencias sin explicar al cierre (SC-014).
- Sin contabilidad iniciada, el modo de paso por defecto es `NoPasa` y nada se bloquea (como la 012 y la 013).
