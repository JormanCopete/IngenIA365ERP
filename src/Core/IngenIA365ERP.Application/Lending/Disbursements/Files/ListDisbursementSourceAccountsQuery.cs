using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.BankFiles;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.Files;

/// <summary>
/// Las cuentas de origen posibles del archivo de desembolsos (feature 013, US3, T0454; api.md §9.3,
/// <c>GET /api/lending/disbursement-files/source-accounts?formatPublicId=</c>, <c>Lending.DisbursementFiles.Generate</c>): las cuentas bancarias del
/// plan que informa <see cref="IContabilidadParaCartera.CuentasBancariasAsync"/> —Cartera no lee tablas <c>ACC_</c>—, del banco del formato o todas
/// si el formato es genérico (o no se da). No trae datos de deudores. (nuevo)
/// </summary>
public sealed record ListDisbursementSourceAccountsQuery(Guid? FormatPublicId = null) : IRequest<Result<IReadOnlyList<CuentaBancariaDeOrigenDto>>>;

public sealed class ListDisbursementSourceAccountsQueryValidator : AbstractValidator<ListDisbursementSourceAccountsQuery>;

public sealed class ListDisbursementSourceAccountsQueryHandler(IApplicationDbContext db, IContabilidadParaCartera contabilidad)
    : IRequestHandler<ListDisbursementSourceAccountsQuery, Result<IReadOnlyList<CuentaBancariaDeOrigenDto>>>
{
    public async Task<Result<IReadOnlyList<CuentaBancariaDeOrigenDto>>> Handle(ListDisbursementSourceAccountsQuery request, CancellationToken ct)
    {
        Guid? banco = null;
        if (request.FormatPublicId is { } id)
        {
            var formato = await db.BankFileFormats.AsNoTracking().Include(f => f.Bank).FirstOrDefaultAsync(f => f.PublicId == id && !f.IsDeleted, ct);
            if (formato is null) return Result.Failure<IReadOnlyList<CuentaBancariaDeOrigenDto>>(BankFileFormatErrors.NotFound);
            banco = formato.Bank?.PublicId;
        }
        return await contabilidad.CuentasBancariasAsync(banco, ct);
    }
}
