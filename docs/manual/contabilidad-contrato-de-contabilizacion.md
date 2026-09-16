# Cómo contabiliza un módulo: el contrato de contabilización

> Feature 009 (contabilidad NIIF), entrega E1. Complementa
> [specs/009-contabilidad-niif/contracts/contabilizacion.md](../../specs/009-contabilidad-niif/contracts/contabilizacion.md),
> que es el contrato formal; este documento es la receta para el equipo.

Hasta la feature 009 había dieciséis sitios que escribían en las tablas contables, doce de
ellos con cabecera y sin líneas, cada uno con su numeración y sin ninguna regla de cuenta.
Desde la 009 **hay un solo camino al libro**: `AccountingPoster`
(`Application/Accounting/Posting`). Nadie más instancia un `AccountingDocument` ni una
`JournalEntry`, ni toca `VoucherType.NextNumber`. Lo vigilan dos pruebas de arquitectura:
`NingunModuloEscribeMovimientosFueraDelContrato` y `PrincipioXI_ContableImmutable`.

## 1. La regla en una frase

**El módulo decide qué contabilizar (cuentas, valores, terceros); el contrato decide si se
puede y cómo queda escrito.** El módulo no abre transacciones, no numera, no escribe saldos
(no existen: todo saldo es una suma sobre `ACC_JournalEntries`) y no edita ni borra nada
contabilizado.

## 2. El molde (lo que hace Nómina)

```csharp
public sealed class MiModuloAccountingPoster(IApplicationDbContext db, AccountingPoster poster)
{
    public async Task<Result<AccountingDocument>> PostAsync(MiDocumento doc, DateOnly fecha, CancellationToken ct)
    {
        // 1. Cuentas: de la parametrización del módulo, resueltas por Id o por código.
        //    Si falta una, se devuelve un error del módulo y no se toca el contexto.
        // 2. Líneas: una por (cuenta, tercero, centro, documento cruce); débito o crédito, nunca ambos.
        var lineas = new List<PostingLine>
        {
            new() { AccountId = cuentaDebito.Id, Debit = valor, Detail = "…", PersonId = tercero, CostCenterId = centro },
            new() { AccountId = cuentaCredito.Id, Credit = valor, Detail = "…", PersonId = tercero },
        };
        // 3. Al contrato: tipo de comprobante DEL MÓDULO, fecha de la operación, origen (módulo, tipo y PublicId del documento).
        return await poster.PrepareAsync(
            new PostingRequest("XX", fecha, "Descripción", new AccountingOrigin(ModuloContable.MiModulo, "MiDocumento", doc.PublicId), lineas), ct);
    }

    public Task<Result<AccountingDocument>> ReverseAsync(AccountingDocument original, DateOnly fecha, string motivo, CancellationToken ct) =>
        poster.PrepareReversalAsync(original, fecha, motivo, new AccountingOrigin(ModuloContable.MiModulo, "MiDocumento", original.SourcePublicId ?? Guid.Empty), ct);
}
```

Y en el comando del módulo:

```csharp
var posting = await contabilizador.PostAsync(documento, fecha, ct);
if (posting.IsFailure) return Result.Failure(posting.Error);   // nada se agregó al contexto
documento.Estado = Aprobado;
await db.SaveChangesAsync(ct);                                  // operación + comprobante, o nada
```

El comando se marca `IReintentableAnteConcurrencia`: el consecutivo se toma en memoria y su
`RowVersion` convierte una carrera de numeración en un reintento del comando completo.

## 3. Qué comprueba el contrato (y qué error da)

En este orden; las de encabezado cortan, las de línea se acumulan y llegan juntas.

| # | Comprobación | Error |
|---|---|---|
| 1 | Contabilidad iniciada | `Accounting.NotInitialized` |
| 2 | Tipo de comprobante activo y del uso correcto (manual sólo desde Contabilidad; de módulo sólo desde su módulo) | `Accounting.VoucherType.NotFound` / `.Inactive` / `.NotAllowedForModule` |
| 3 | Fecha en un período existente y abierto; «posterior a hoy» sólo se rechaza al digitar (un módulo fecha según su operación) | `Accounting.Period.NotFound` / `.Closed` / `Accounting.Date.InFuture` |
| 4 | Al menos dos líneas | `Accounting.Document.TooFewLines` |
| 5 | Cuenta existe, es de movimiento, activa y habilitada para el módulo | `Accounting.Line.AccountNotFound` / `.AccountNotMovement` / `.AccountInactive` / `.AccountNotEnabledForModule` |
| 6 | Importe: débito o crédito, mayor que cero, nunca ambos | `Accounting.Line.AmountInvalid` |
| 7 | Sucursal: toda línea lleva una (la propuesta si no viene); «exige sucursal» pide una explícita; con alcance restringido (sólo al digitar) tiene que ser permitida | `Accounting.Line.BranchRequired` / `.BranchInvalid` / `.BranchOutOfScope` |
| 8 | Tercero si la cuenta lo exige; vigente si viene | `Accounting.Line.ThirdPartyRequired` / `.ThirdPartyInvalid` |
| 9 | Documento cruce si la cuenta lo exige; tipo activo | `Accounting.Line.CrossDocumentRequired` / `.CrossDocumentTypeInvalid` |
| 10 | Centro de costo: un módulo lo manda siempre y la cuenta decide si lo conserva; al digitar, ponerlo en una cuenta que no lo maneja es error | `Accounting.Line.CostCenterRequired` / `.CostCenterNotAllowed` / `.CostCenterInvalid` |
| 10b | Base gravable y valor ≈ base × tarifa vigente (con la tolerancia de la empresa); la diferencia pequeña es aviso, la grande error | `Accounting.Line.TaxBaseRequired` / `.TaxAmountDiffers` (aviso) / `.TaxAmountMismatch` / `.TaxRateMissing` |
| 11 | Cuadre | `Accounting.Document.Unbalanced` |
| 12 | Número y construcción: líneas con fecha y `IsPosted`; `FirstMovementAt` de cada cuenta | — |

Un solo error de línea llega tal cual; varios llegan como `Accounting.Document.Invalid` con
`data.errors[]` (`lineNumber`, `field`, `code`, `message`, `severity`). Los avisos no
bloquean. El `ErrorEnvelopeFilter` serializa `data` para que la pantalla señale la celda.

## 4. Terceros (FR-088)

El tercero de una línea es una **persona** (`COR_People`). En Nómina: el empleado en
devengos y deducciones; en aportes y provisiones, la persona vinculada a la EPS, ARL, fondo
o caja del empleado (`TercerosDeNomina`). Las entidades institucionales se vinculan a su
persona en su propia pantalla («Persona que la representa como tercero»); si una cuenta
exige tercero y la entidad no tiene vínculo, la aprobación falla nombrándola
(`Accounting.Line.ThirdPartyRequired` con `data.entity`) y la parametrización de cuentas por
concepto lo rechaza antes (`Accounting.Parameterization.InstitutionalLinkMissing`).
`Contabilidad › Parametrizaciones inválidas` lista lo que va a fallar.

## 5. Reversión

Lo contabilizado no se edita ni se borra (Principio XI). La corrección es
`PrepareReversalAsync`: un documento `Reversal` del mismo tipo con las líneas invertidas y la
referencia en ambos sentidos; el original queda `Reversed`. **Sólo el módulo dueño reversa lo
suyo**: desde Contabilidad, un `NM` responde `Accounting.Document.ModuleOwned` con
`data.origin`, y la pantalla ofrece «Ver en Nómina». Si la fecha cae en un mes cerrado, la
reversión se fecha en el primer período abierto y lo dice en la descripción.

## 6. Qué NO hacer

- No hacer `db.AccountingDocuments.Add(...)` ni `new JournalEntry` fuera de `Accounting/Posting`.
  La prueba de arquitectura lo rechaza; el borrador manual también nace en el contrato
  (`NuevoBorrador`, `NuevaLineaDeBorrador`).
- No tocar `VoucherType.NextNumber` (ni en pruebas de módulo): sólo el contrato numera.
- No usar `Remove`/`RemoveRange` sobre documentos o líneas: las sobrantes de un borrador se
  marcan `IsDeleted`.
- No guardar saldos: se calculan sobre `ACC_JournalEntries` con `IsPosted = true`.
- No inventar un tipo de comprobante en código: el módulo pide el suyo por código (`NM`,
  `DS`, `RC`…) y falla claro si no existe o está inactivo.
- No hacer `SaveChangesAsync` dentro del contabilizador del módulo: quien llama guarda
  todo junto.

## 7. Dónde está cada cosa

| | |
|---|---|
| Contrato | `Application/Accounting/Posting/AccountingPoster.cs`, `PostingRequest.cs`, `AccountingErrors.cs` |
| Reglas puras | `Application/Accounting/Rules/AccountLineRules.cs` |
| Elegibilidad de una cuenta para un módulo | `Application/Accounting/Accounts/AccountEligibility.cs` |
| Molde de módulo | `Application/Payroll/Services/PayrollAccountingPoster.cs` |
| Digitación manual | `Application/Accounting/Documents/` |
| Pruebas del contrato | `tests/…Application.Tests/Accounting/Posting/AccountingPosterTests.cs` |
| Pruebas de arquitectura | `tests/…Architecture.Tests/Principles/NingunModuloEscribeMovimientosFueraDelContrato.cs`, `PrincipioXI_ContableImmutable.cs` |
