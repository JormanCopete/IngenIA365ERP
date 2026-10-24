using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Reports;

/// <summary>
/// El registro de vistas de Tesorería (feature 015, US26, T0169; FR-153): por ahora sólo <c>recent-account-changes</c>; la fase 20 (US21) suma las demás vistas, la
/// exportación y la ruta del centro de reportes sobre este mismo registro. Toda vista es una <see cref="TablaExportable"/> con alcance por cuenta propia. (nuevo)
/// </summary>
public static class VistasDeTesoreria
{
    public const string CambiosRecientesDeCuenta = "recent-account-changes";

    public static IReadOnlyList<string> Todas { get; } = [CambiosRecientesDeCuenta];
}

/// <summary>
/// La vista <c>recent-account-changes</c> (<c>Treasury.Orders.View</c>): las órdenes de los lotes marcadas por cambio reciente de la cuenta destino (FR-210), con quién
/// las revisó y su nota, en un rango de fechas de pago, sólo de las cuentas propias al alcance. (nuevo)
/// </summary>
public sealed record RecentAccountChangesQuery(DateOnly Desde, DateOnly Hasta) : IRequest<Result<TablaExportable>>;

public sealed class RecentAccountChangesQueryValidator : AbstractValidator<RecentAccountChangesQuery>
{
    public RecentAccountChangesQueryValidator() =>
        RuleFor(x => x).Must(x => x.Desde <= x.Hasta && x.Hasta.DayNumber - x.Desde.DayNumber <= 366).WithMessage("El rango va de «desde» a «hasta», hasta un año.");
}

public sealed class RecentAccountChangesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance) : IRequestHandler<RecentAccountChangesQuery, Result<TablaExportable>>
{
    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Lote"), new("Fecha de pago", TipoDeColumna.Fecha), new("Cuenta de origen"), new("Orden"), new("Beneficiario"), new("Documento"),
        new("Cuenta destino"), new("Valor", TipoDeColumna.Moneda), new("Marcas"), new("Revisó"), new("Nota de la revisión"), new("Estado del lote"),
        new("_orden", Clave: "_orden"),
    ];

    public async Task<Result<TablaExportable>> Handle(RecentAccountChangesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var filas = await (from i in db.PaymentBatchItems.AsNoTracking()
                           join b in db.PaymentBatches.AsNoTracking() on i.PaymentBatchId equals b.Id
                           join o in db.PaymentOrders.AsNoTracking() on i.PaymentOrderId equals o.Id
                           join c in db.OwnBankAccounts.AsNoTracking() on b.BankAccountId equals c.Id
                           join u in db.Users.AsNoTracking().IgnoreQueryFilters() on i.ExplicitlyReviewedByUserId equals u.Id into us
                           from u in us.DefaultIfEmpty()
                           where (i.Flags & PaymentBatchItemFlags.RecentAccountChange) != 0 && b.PayDate >= request.Desde && b.PayDate <= request.Hasta
                           orderby b.PayDate, b.Number, o.Number
                           select new
                           {
                               b.BankAccountId, Lote = b.Number, b.PayDate, Cuenta = c.Code, Orden = o.Number, o.PublicId, o.BeneficiaryName, o.BeneficiaryTaxId,
                               o.DestinationAccountNumber, i.Amount, i.Flags, Reviso = u != null ? u.Username : null, i.ReviewNote, b.Status,
                           }).ToListAsync(ct);
        var visibles = filas.Where(f => mio.IncluyeCuenta(f.BankAccountId)).ToList();
        var tabla = new TablaExportable("Órdenes a cuentas destino cambiadas", $"Fecha de pago del {request.Desde:dd/MM/yyyy} al {request.Hasta:dd/MM/yyyy}", Columnas,
            visibles.Select(f => new FilaExportable(
            [
                f.Lote, f.PayDate, f.Cuenta, f.Orden, f.BeneficiaryName, f.BeneficiaryTaxId, CuentasBancariasPropias.Enmascarar(f.DestinationAccountNumber ?? string.Empty), f.Amount,
                f.Flags.ToString(), f.Reviso ?? "Sin revisar", f.ReviewNote, f.Status.ToString(), f.PublicId,
            ], Resaltada: f.Reviso is null)).ToList(),
            null, ["La cuenta cambiada dentro de CuentasDestino.DiasDeMarca exige la revisión expresa de otra persona antes de aprobar el lote (FR-210)."]);
        return Result.Success(tabla);
    }
}
