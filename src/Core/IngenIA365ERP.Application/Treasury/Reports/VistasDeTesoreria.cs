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
/// El registro de las vistas de T0 del centro de informes de Tesorería (feature 015, US26 T0169 y US21 T0314; R-36; FR-153 parte T0; api.md §17): cada una es una
/// <see cref="TablaExportable"/> con alcance por cuenta propia (<see cref="IAlcanceDeTesoreria"/>). Las de T1 llegaron en la fase 35; las de T2, con su entrega. (nuevo)
/// </summary>
public static class VistasDeTesoreria
{
    public const string OrdenesPorEstado = "orders-by-state";
    public const string Lotes = "batches";
    public const string RechazadasYRegiro = "rejected-and-reissue";
    public const string CambiosRecientesDeCuenta = "recent-account-changes";
    public const string ChequesGirados = "issued-checks";
    public const string SaldosBancarios = "bank-balances";
    public const string ConciliacionMinima = "minimal-reconciliation";
    // T1 (fase 35, T0423; FR-153 parte T1).
    public const string ComprobantesDePago = "payment-vouchers";
    public const string Traslados = "transfers";
    public const string CierreDeCheques = "month-end-checks";

    public static IReadOnlyList<VistaDeInformeDeRecaudoYTesoreria> Vistas { get; } =
    [
        new(OrdenesPorEstado, "Órdenes de pago por estado", "Las órdenes vivas al corte por estado, con su antigüedad desde que llegaron, su origen y su lote.", "ordenes-por-estado",
            ["asOf", "account"]),
        new(Lotes, "Lotes de pago", "Los lotes con fecha de pago en el rango: cuenta, forma, órdenes, total, GMF estimado y estado.", "lotes-de-pago", ["from", "to", "account"]),
        new(RechazadasYRegiro, "Rechazadas y regiro", "Los giros rechazados, anulados o con cheque no cobrado del rango, con su motivo y la orden que los reemplazó.",
            "rechazadas-y-regiro", ["from", "to", "account"]),
        new(CambiosRecientesDeCuenta, "Órdenes a cuentas destino cambiadas", "Las órdenes de los lotes marcadas por cambio reciente de la cuenta destino, con quién las revisó.",
            "cuentas-destino-cambiadas", ["from", "to"]),
        new(ChequesGirados, "Cheques girados", "Los cheques emitidos en el rango: cuenta, número, beneficiario, valor, estado y días sin cobrar.", "cheques-girados",
            ["from", "to", "account"]),
        new(SaldosBancarios, "Saldos bancarios", "Por cuenta propia al corte: saldo en libros, último saldo del extracto y su diferencia.", "saldos-bancarios", ["asOf", "account"]),
        new(ConciliacionMinima, "Conciliación mínima", "Las conciliaciones de los meses del rango: saldo del extracto, en libros, partidas por clase y diferencia.", "conciliacion-minima",
            ["from", "to", "account"]),
        new(ComprobantesDePago, "Comprobantes de pago", "Los comprobantes de pago del rango: número, orden, beneficiario, forma, bruto, retención, neto y si se envió.",
            "comprobantes-de-pago", ["from", "to", "account"]),
        new(Traslados, "Traslados entre cuentas", "Los traslados pedidos en el rango: de qué cuenta a cuál, valor, estado, envío, confirmación y días en tránsito.",
            "traslados", ["from", "to", "account"]),
        new(CierreDeCheques, "Cierre del mes de cheques", "Los cierres de los meses del rango por cuenta: cheques reclasificados al efectivo y pasados a cuenta por pagar.",
            "cierre-de-cheques", ["from", "to", "account"]),
    ];

    /// <summary>Las claves (compatibilidad con la fase 9, que sólo tenía <c>recent-account-changes</c>).</summary>
    public static IReadOnlyList<string> Todas { get; } = [.. Vistas.Select(v => v.Key)];

    public static VistaDeInformeDeRecaudoYTesoreria? Buscar(string clave) => Vistas.FirstOrDefault(v => v.Key == clave);
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
