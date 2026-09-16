# Contrato interno de contabilización

**Feature**: 009 | **Date**: 2026-09-14

Es el **único** camino por el que un movimiento contable entra al libro (FR-036, FR-039). Vive en
`Application/Accounting/Posting`. Lo usan la digitación manual (al contabilizar un borrador), los
módulos (Nómina, Cartera, Inventario, Tesorería, CDT/Ahorros) y los procesos propios (apertura,
cierre, activos, conciliación → borrador). Una prueba de arquitectura
(`NingunModuloEscribeMovimientosFueraDelContrato`) impide cualquier otro `Add` de documentos o
líneas.

## 1. Tipos

```csharp
namespace IngenIA365ERP.Application.Accounting.Posting;

public sealed record AccountingOrigin(string Module, string SourceType, Guid SourcePublicId);
// Module: "CNT" | "NOM" | "CAR" | "INV" | "TES" | "CDT" | "ACT"

public sealed record PostingLine(
    string AccountCode,                 // o AccountId: uno de los dos (AccountRef)
    decimal Debit, decimal Credit,      // exactamente uno > 0, dos decimales
    string? Detail = null,
    int? PersonId = null,
    int? BranchId = null,               // null → sucursal principal (R7)
    int? CostCenterId = null,
    string? CrossDocumentType = null, string? CrossDocumentNumber = null,
    decimal? TaxBase = null);

public sealed record PostingRequest(
    string VoucherTypeCode,
    DateOnly Date,
    string Description,
    AccountingOrigin Origin,
    IReadOnlyList<PostingLine> Lines,
    DocumentKind Kind = DocumentKind.Regular);

public sealed class AccountingPoster(IApplicationDbContext db, IDateTimeService clock,
                                     ICurrentUserService user, IUserBranchScope scope)
{
    /// Valida, construye documento + líneas contabilizadas, asigna número y los AGREGA al
    /// contexto SIN guardar. El llamador guarda todo en un solo SaveChangesAsync.
    public Task<Result<AccountingDocument>> PrepareAsync(PostingRequest request, CancellationToken ct);

    /// Documento Reversal del mismo tipo, líneas invertidas, referencia cruzada; sin guardar.
    public Task<Result<AccountingDocument>> PrepareReversalAsync(
        AccountingDocument original, DateOnly date, string reason, AccountingOrigin origin, CancellationToken ct);
}
```

Reglas de línea en **una** clase, `Application/Accounting/Rules/AccountLineRules`, que recibe la
cuenta ya cargada (con reglas y tarifa vigente), el módulo origen y la línea, y devuelve la lista
de infracciones. La usan `AccountingPoster` y `ValidateDraftQuery` (la digitación valida campo a
campo con la misma clase a través de `POST /documents/validate`).

## 2. Qué comprueba `PrepareAsync` (en este orden)

| # | Regla | Error |
|---|---|---|
| 1 | contabilidad iniciada | `Accounting.NotInitialized` |
| 2 | tipo de comprobante existe, activo, `Usage`/`ModuleCode` coherentes con `Origin.Module` (`Manual` sólo desde CNT; `Module` sólo desde su módulo; `Opening/Closing/Assets` sólo desde su proceso) | `Accounting.VoucherType.NotFound` · `Accounting.VoucherType.NotAllowedForModule` |
| 3 | fecha ≤ hoy **sólo al digitar (origen CNT)**: un módulo fecha según su operación (la nómina, al último día del período, que se aprueba unos días antes); período de la fecha existe y está abierto (salvo `Kind = Opening`, que exige `Date = primer período − 1 día`) | `Accounting.Period.NotFound` · `Accounting.Period.Closed` · `Accounting.Date.InFuture` |
| 4 | ≥ 2 líneas; cada línea exactamente un importe > 0 con dos decimales | `Accounting.Line.AmountInvalid` |
| 5 | cuenta existe, `IsMovement`, `IsActive`, `EnabledModules` incluye el módulo | `Accounting.Line.AccountNotFound` · `Accounting.Line.AccountNotMovement` · `Accounting.Line.AccountInactive` · `Accounting.Line.AccountNotEnabledForModule` |
| 6 | sucursal: resuelta (dada o principal), activa, en el alcance del usuario **sólo si `Origin.Module = CNT`** (los módulos mandan la sucursal de la operación); si `RequiresBranch` y no vino explícita → error | `Accounting.Line.BranchRequired` · `Accounting.Line.BranchOutOfScope` |
| 7 | tercero: presente si `RequiresThirdParty`; si presente, vigente (no eliminado, `Status` activo) | `Accounting.Line.ThirdPartyRequired` · `Accounting.Line.ThirdPartyInvalid` |
| 8 | documento cruce: presente si `RequiresCrossDocument` (tipo del catálogo, activo) | `Accounting.Line.CrossDocumentRequired` · `Accounting.Line.CrossDocumentTypeInvalid` |
| 9 | centro de costo: presente si `RequiresCostCenter`, ausente si no lo maneja, activo | `Accounting.Line.CostCenterRequired` · `Accounting.Line.CostCenterNotAllowed` |
| 10 | base gravable: presente si `RequiresTaxBase`; diferencia `\|importe − base × tarifa\|` > 0 → **aviso**; > `TaxTolerance` → **error** | `Accounting.Line.TaxBaseRequired` · `Accounting.Line.TaxAmountDiffers` (aviso) · `Accounting.Line.TaxAmountMismatch` (error) |
| 11 | `Σ Debit == Σ Credit` | `Accounting.Document.Unbalanced` (trae la diferencia) |
| 12 | número: `VoucherType.NextNumber` (incremento en memoria; índice único + reintento, R3) | — |

Cada infracción lleva `Severidad` (Error | Aviso): los avisos se muestran y no impiden contabilizar.
Cada error de línea lleva `data: { lineNumber, accountCode, rule, severity }` (FR-041). Al preparar,
`ChartOfAccount.FirstMovementAt` se fija si estaba en null.

## 3. Cómo lo usa un módulo (molde)

```csharp
var posting = await poster.PrepareAsync(new PostingRequest("NM", fecha, detalle,
    new AccountingOrigin("NOM", "PayrollRun", run.PublicId), lineas), ct);
if (posting.IsFailure) return Result.Failure(posting.Error);   // nada tocado, nada guardado
run.Status = PayrollRunStatus.Approved;
run.AccountingDocument = posting.Value;
await db.SaveChangesAsync(ct);                                  // corrida + comprobante, o nada
```

El comando del módulo se marca `IReintentableAnteConcurrencia` para que
`ReintentoPorConcurrenciaBehavior` lo repita entero si la numeración chocó.

## 4. Qué manda cada módulo (parametrización existente → líneas)

| Módulo · operación | Tipo | Líneas (débito / crédito) | Tercero | Documento cruce | Parametrización |
|---|---|---|---|---|---|
| **Nómina** · aprobar corrida | `NM` | por concepto y centro: cuenta débito / cuenta crédito (`PayrollConceptDefinitionAccount`) | empleado en devengos y deducciones; **entidad vinculada** (EPS/ARL/fondo/caja) en aportes y provisiones (FR-088) | — | ya por FK |
| Nómina · reversar | `NM` | `PrepareReversalAsync` | | | |
| **Cartera** · desembolso | `DS` | capital: `CreditLineParameter.AccountCode` / banco: `COR_Banks.AccountingAccountCode` del banco elegido | asociado | `PG` + número de crédito | resolver códigos → `AccountId`; validar al guardar |
| Cartera · recaudo | `RC` | banco o caja / capital, intereses (`AccountInterestIncome`), mora (`AccountInterestDefault`) | asociado | `PG` + crédito | ídem |
| Cartera · causación de intereses | `CA` | `AccountInterestCxC` / `AccountInterestIncome` | asociado | `PG` + crédito | ídem |
| Cartera · clasificación y provisión | `PV` | gasto provisión / provisión (nuevas columnas en `LND_CreditLineParameters`: `ProvisionExpenseAccount`, `ProvisionAccount`) | asociado | `PG` + crédito | **nueva parametrización**; sin ella, `Accounting.Parameterization.Missing` |
| Cartera · descuento de nómina | `DN` | CxC nómina / capital-intereses | asociado | `PG` | `CreditLineParameter` |
| Ahorros · intereses | `AH` | gasto intereses / ahorro (`SavingsParameter.TreasuryAccount` + `InterestExpenseAccount` nueva) | asociado | `CT` + cuenta de ahorro | nueva columna |
| **CDT** · liquidación de intereses | `CD` | gasto / CDT por pagar (`CdtParameter.TreasuryAccount` + `InterestExpenseAccount` nueva) | titular | `CT` + número CDT | nueva columna |
| **Inventario** · factura de venta | `FV` | CxC cliente / ventas gravadas-no gravadas, IVA (`ProductAccount`, `VatAccount`) | cliente | `FV` + número | resolver códigos |
| Inventario · entradas/salidas | `EI` / `SI` | inventario / proveedor o costo | proveedor | `FC` + número | `ProductAccount.NetAccountCode` |
| Inventario · anular | mismo | `PrepareReversalAsync` por `SourcePublicId` | | | corrige el bug de anular «cualquiera» |
| **Tesorería** · cheque | `CH` | `TreasuryConcept.DebitAccountId` / banco | beneficiario | `FP` + factura | **`TRS_Concepts` gana cuentas** |
| Tesorería · factura por pagar / cobrar | `FP` / `CB` | concepto / CxP o CxC | tercero | `FC`/`FV` + número | ídem |
| Tesorería · anular cheque | `CH` | `PrepareReversalAsync` | | | |
| **Activos** · corrida mensual | `DP` | gasto / depreciación acumulada (o diferido) por activo | — | — | ficha del activo |
| Activos · baja | `DP` | acumulada y contrapartida / costo | — | — | ficha |
| **Cierre** de ejercicio | `CI` | resultados (4/5/6/7 por rubro) / cuenta de resultado | — | — | `AccountingSetup.ResultAccountId` |
| **Apertura** | `AP` | del archivo o digitadas | del archivo | del archivo | — |

Toda cuenta de parametrización de módulo debe cumplir FR-016 al guardarse (`AccountEligibility`):
de movimiento, activa, habilitada para ese módulo. `ListInvalidParameterizationsQuery` recorre
todas las tablas de la columna «Parametrización» y lista las que no cumplen o cuyo vínculo
institucional falta.

## 5. Lo que NO hace el contrato

- No abre transacciones ni guarda: la atomicidad es del `SaveChangesAsync` del llamador.
- No decide cuentas: las trae el módulo desde su parametrización.
- No escribe saldos: no existen (R4).
- No permite editar ni borrar un documento contabilizado: sólo `PrepareReversalAsync`.
