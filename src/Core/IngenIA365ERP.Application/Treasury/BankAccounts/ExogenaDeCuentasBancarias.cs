using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.BankAccounts;

// Feature 015, T2 (fase 41, T0450; FR-222): lo que la información exógena necesita de las cuentas bancarias propias: cada cuenta con su banco como tercero
// (BankThirdPartyPersonId) y su saldo en libros al 31 de diciembre por el vínculo contable (formato 1012). Los pagos y retenciones por tercero de los formatos de pagos ya
// salen de los hechos de Tesorería, que llevan el beneficiario, el banco o la red como tercero. Una cuenta sin su banco como tercero se marca: hay que completarla.

/// <summary>Una cuenta propia para la exógena: su banco como tercero (documento y nombre), el número y el saldo en libros al corte. (nuevo)</summary>
public sealed record CuentaParaExogenaDto(Guid BankAccountPublicId, string Code, string AccountNumber, Guid? BankThirdPartyPersonPublicId, string? BankTaxId,
    string? BankName, decimal YearEndBalance, bool MissingThirdParty);

/// <summary>
/// <c>GET /api/treasury/bank-accounts/exogena?year=</c> (<c>Treasury.BankBalances.View</c>, lectura sensible; FR-222): las cuentas propias con su banco como tercero y su
/// saldo en libros al 31 de diciembre del año. (nuevo)
/// </summary>
public sealed record ListBankAccountsForExogenaQuery(int Year) : IRequest<Result<IReadOnlyList<CuentaParaExogenaDto>>>;

public sealed class ListBankAccountsForExogenaQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IContabilidadParaTesoreria contabilidad,
    AuditoriaDeTesoreria auditoria) : IRequestHandler<ListBankAccountsForExogenaQuery, Result<IReadOnlyList<CuentaParaExogenaDto>>>
{
    public async Task<Result<IReadOnlyList<CuentaParaExogenaDto>>> Handle(ListBankAccountsForExogenaQuery request, CancellationToken ct)
    {
        if (request.Year is < 2000 or > 2999)
            return Result.Failure<IReadOnlyList<CuentaParaExogenaDto>>(new Error("Validation.Invalid", "Indique el año gravable."));
        var corte = new DateOnly(request.Year, 12, 31);
        var mio = await alcance.ObtenerAsync(ct);
        var cuentas = (await db.OwnBankAccounts.AsNoTracking().Where(c => c.ValidFrom <= corte).OrderBy(c => c.Code).ToListAsync(ct))
            .Where(c => mio.IncluyeCuenta(c.Id)).ToList();
        var terceros = cuentas.Where(c => c.BankThirdPartyPersonId is not null).Select(c => c.BankThirdPartyPersonId!.Value).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => terceros.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.PublicId, p.TaxId, Nombre: Receipts.CashDesk.OperacionesDeCaja.Nombre(p)), ct);
        var lista = new List<CuentaParaExogenaDto>(cuentas.Count);
        foreach (var c in cuentas)
        {
            var saldo = await contabilidad.SaldoEnLibrosAsync(c.PublicId, corte, ct);
            var t = c.BankThirdPartyPersonId is { } id && personas.TryGetValue(id, out var p) ? p : default;
            lista.Add(new CuentaParaExogenaDto(c.PublicId, c.Code, c.AccountNumber, c.BankThirdPartyPersonId is null ? null : t.PublicId, t.TaxId, t.Nombre, saldo,
                c.BankThirdPartyPersonId is null));
        }
        await auditoria.SaldosBancariosLeidosAsync(lista.Select(x => x.BankAccountPublicId).ToList(), corte, ct);
        return Result.Success<IReadOnlyList<CuentaParaExogenaDto>>(lista);
    }
}
