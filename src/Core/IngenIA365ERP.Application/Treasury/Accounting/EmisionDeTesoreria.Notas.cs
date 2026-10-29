using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Accounting;

// Feature 015, T2 (fase 41, T0448; FR-130, FR-218; contracts/contabilidad.md §4): la nota bancaria aprobada, en línea y con el comprobante sugerido TB. Sin guardar.
public sealed partial class EmisionDeTesoreria
{
    public const string ClaseNotaBancaria = "BankNote";

    /// <summary>
    /// <c>NotaBancariaRegistrada</c> (NBR): según la clase, GMF (<c>GmfGasto</c> → <c>Banco</c>), comisión (<c>ComisionesBancarias</c> + <c>IvaDescontable</c> →
    /// <c>Banco</c>), intereses en bruto (<c>Banco</c> + <c>RetencionesAFavor</c> → <c>InteresesBancarios</c>) o impuesto u otra (<c>OtrosCargosBancarios</c> →
    /// <c>Banco</c>, o al revés si es un abono). El banco como tercero.
    /// </summary>
    public async Task<IntegrationMessage> NotaBancariaAsync(OwnBankAccount cuenta, BankNote nota, CancellationToken ct)
    {
        var dim = await DimensionesDeCuentaAsync(cuenta, nota.Kind.ToString(), ct);
        var tercero = nota.ThirdPartyPersonId is { } t
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == t).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var cruce = nota.Number;
        var lineas = new List<TreasuryFactLineV1>();
        switch (nota.Kind)
        {
            case BankNoteKind.Gmf:
                lineas.Add(Linea(RolesDeTesoreria.GmfGasto, RolesDeTesoreria.Debito, nota.Amount, dim, tercero, cruce));
                lineas.Add(Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Credito, nota.Amount, dim, cruce: cruce));
                break;
            case BankNoteKind.Commission:
                lineas.Add(Linea(RolesDeTesoreriaT2.ComisionesBancarias, RolesDeTesoreria.Debito, nota.BaseAmount, dim, tercero, cruce));
                if (nota.VatAmount > 0m) lineas.Add(Linea(RolesDeTesoreriaT2.IvaDescontable, RolesDeTesoreria.Debito, nota.VatAmount, dim, tercero, cruce));
                lineas.Add(Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Credito, nota.Amount, dim, cruce: cruce));
                break;
            case BankNoteKind.Interest:
                lineas.Add(Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Debito, nota.Amount, dim, cruce: cruce));
                if (nota.WithholdingAmount > 0m) lineas.Add(Linea(RolesDeTesoreriaT2.RetencionesAFavor, RolesDeTesoreria.Debito, nota.WithholdingAmount, dim, tercero, cruce));
                lineas.Add(Linea(RolesDeTesoreriaT2.InteresesBancarios, RolesDeTesoreria.Credito, nota.BaseAmount, dim, tercero, cruce));
                break;
            default:
                var cargo = nota.Direction == StatementLineDirection.Debit;
                lineas.Add(Linea(RolesDeTesoreriaT2.OtrosCargosBancarios, cargo ? RolesDeTesoreria.Debito : RolesDeTesoreria.Credito, nota.Amount, dim, tercero, cruce));
                lineas.Add(Linea(RolesDeTesoreria.Banco, cargo ? RolesDeTesoreria.Credito : RolesDeTesoreria.Debito, nota.Amount, dim, cruce: cruce));
                break;
        }
        var contenido = new NotaBancariaRegistradaV1
        {
            BankNotePublicId = nota.PublicId, Number = nota.Number, BankAccountPublicId = cuenta.PublicId, Kind = nota.Kind.ToString(), NoteDate = nota.NoteDate,
            Amount = nota.Amount, BaseAmount = nota.BaseAmount, VatAmount = nota.VatAmount, WithholdingAmount = nota.WithholdingAmount,
            StatementLinePublicId = nota.StatementLinePublicId, ThirdPartyPersonPublicId = tercero, RegisteredBy = await UsuarioAsync(ct), Lines = lineas,
        };
        return await EmitirAsync(contenido, nota.PublicId, ClaseNotaBancaria, TipoDeTraslado, nota.Number, nota.NoteDate, cuenta.BranchId, tercero,
            ClavesDeEvento.Confirmacion, ct);
    }
}
