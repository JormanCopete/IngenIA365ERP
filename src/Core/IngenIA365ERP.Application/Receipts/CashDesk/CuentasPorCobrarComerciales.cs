using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

// Feature 015, R1, US23 (T0397; FR-049, FR-166, FR-167, FR-179; contabilidad.md §3, §5): las cuentas por cobrar comerciales —lo vendido a crédito en Comercial que no
// pasa a Cartera— se cobran en el recibo como destino CommercialReceivable, con tercero y factura de cruce, nunca como concepto vario ni por más que el saldo. El saldo es
// lo que Contabilidad informa por tercero y documento cruce (sin lo de Recaudo) menos lo que Recaudo ya aplicó en recibos vigentes, contabilizados o no.

/// <summary>Una cuenta por cobrar comercial pendiente de la persona. (nuevo)</summary>
public sealed record CommercialReceivableDto(Guid? AccountingDocumentPublicId, string InvoiceNumber, DateOnly Date, decimal AccountingPending, decimal AppliedByReceipts,
    decimal Pending);

/// <summary>Una aplicación de recibo a una cuenta por cobrar comercial (el informe que Cartera incorpora). (nuevo)</summary>
public sealed record CommercialReceivableApplicationDto(string ReceiptNumber, DateOnly ValueDate, Guid PersonPublicId, string PersonName, string InvoiceNumber,
    decimal ConsultedBalance, decimal Amount, bool ReceiptVoided);

/// <summary>Los errores de las cuentas por cobrar comerciales. (nuevo)</summary>
public static class ErroresDeCuentasPorCobrar
{
    public const string CodigoNoExiste = "Receipts.CommercialReceivable.NotFound";
    public const string CodigoSobreElSaldo = "Receipts.CommercialReceivable.OverBalance";
    public const string CodigoSinFactura = "Receipts.CommercialReceivable.InvoiceRequired";

    public static Error NoExiste(string factura) => new ErrorConDatos(CodigoNoExiste,
        $"La factura {factura} no tiene saldo pendiente a nombre de la persona.", new { invoice = factura });

    public static Error SobreElSaldo(string factura, decimal saldo) => new ErrorConDatos(CodigoSobreElSaldo,
        string.Create(CultureInfo.InvariantCulture, $"La factura {factura} tiene un saldo de {saldo:N2}: no se cobra más que eso."), new { invoice = factura, pending = saldo });

    public static Error SinFactura() => new(CodigoSinFactura, "Una cuenta por cobrar comercial se cobra con su factura (documento cruce).");
}

/// <summary>Lo pendiente de las cuentas por cobrar comerciales de una persona, neto de lo que ya aplicó Recaudo. (nuevo)</summary>
public static class CuentasPorCobrarDeRecaudo
{
    public static async Task<IReadOnlyList<CommercialReceivableDto>> PendientesAsync(IApplicationDbContext db, IContabilidadParaRecaudo contabilidad, Guid persona,
        CancellationToken ct)
    {
        var deContabilidad = await contabilidad.CuentasPorCobrarComercialesAsync(persona, ct);
        if (deContabilidad.Count == 0) return [];
        var id = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        var facturas = deContabilidad.Select(c => c.Number).ToList();
        var aplicadas = await (from a in db.CommercialReceivableApplications.AsNoTracking()
                               join r in db.IssuedReceipts.AsNoTracking() on a.ReceiptId equals r.Id
                               where a.PersonId == id && facturas.Contains(a.InvoiceNumber) && r.Status != ReceiptState.Voided
                               group a by a.InvoiceNumber into g
                               select new { Factura = g.Key, Total = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Factura, x => x.Total, ct);
        return deContabilidad.Select(c =>
            {
                var ya = aplicadas.GetValueOrDefault(c.Number);
                return new CommercialReceivableDto(c.DocumentPublicId, c.Number, c.Date, c.Pending, ya, Math.Max(0m, c.Pending - ya));
            })
            .Where(c => c.Pending > 0m).ToList();
    }

    /// <summary>La regla del ítem: la factura existe, tiene saldo y el valor no lo supera; devuelve la cuenta o el error.</summary>
    public static async Task<Result<CommercialReceivableDto>> ValidarAsync(IApplicationDbContext db, IContabilidadParaRecaudo contabilidad, Guid persona, string? factura,
        decimal valor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(factura)) return Result.Failure<CommercialReceivableDto>(ErroresDeCuentasPorCobrar.SinFactura());
        var cuenta = (await PendientesAsync(db, contabilidad, persona, ct)).FirstOrDefault(c => string.Equals(c.InvoiceNumber, factura.Trim(), StringComparison.Ordinal));
        if (cuenta is null) return Result.Failure<CommercialReceivableDto>(ErroresDeCuentasPorCobrar.NoExiste(factura.Trim()));
        if (valor > cuenta.Pending) return Result.Failure<CommercialReceivableDto>(ErroresDeCuentasPorCobrar.SobreElSaldo(cuenta.InvoiceNumber, cuenta.Pending));
        return Result.Success(cuenta);
    }
}

/// <summary><c>GET /api/receipts/commercial-receivables?person=</c> (<c>Receipts.Receipt.Register</c>) (nuevo): lo pendiente por factura de la persona.</summary>
public sealed record GetCommercialReceivablesQuery(Guid PersonPublicId) : IRequest<Result<IReadOnlyList<CommercialReceivableDto>>>;

public sealed class GetCommercialReceivablesQueryHandler(IApplicationDbContext db, IContabilidadParaRecaudo contabilidad)
    : IRequestHandler<GetCommercialReceivablesQuery, Result<IReadOnlyList<CommercialReceivableDto>>>
{
    public async Task<Result<IReadOnlyList<CommercialReceivableDto>>> Handle(GetCommercialReceivablesQuery request, CancellationToken ct) =>
        Result.Success(await CuentasPorCobrarDeRecaudo.PendientesAsync(db, contabilidad, request.PersonPublicId, ct));
}

/// <summary>
/// <c>GET /api/receipts/commercial-receivables/applications?from=&amp;to=</c> (<c>Receipts.Reports.View</c>) (nuevo; FR-166): lo cobrado en recibos a cuentas por cobrar
/// comerciales; antes de activar Cartera, es la referencia que Cartera incorpora.
/// </summary>
public sealed record ListCommercialReceivableApplicationsQuery(DateOnly? From = null, DateOnly? To = null)
    : IRequest<Result<IReadOnlyList<CommercialReceivableApplicationDto>>>;

public sealed class ListCommercialReceivableApplicationsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListCommercialReceivableApplicationsQuery, Result<IReadOnlyList<CommercialReceivableApplicationDto>>>
{
    public async Task<Result<IReadOnlyList<CommercialReceivableApplicationDto>>> Handle(ListCommercialReceivableApplicationsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var filas = await (from a in db.CommercialReceivableApplications.AsNoTracking()
                           join r in db.IssuedReceipts.AsNoTracking() on a.ReceiptId equals r.Id
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.PersonId equals p.Id
                           where (request.From == null || r.ValueDate >= request.From) && (request.To == null || r.ValueDate <= request.To)
                           orderby r.ValueDate, r.Number
                           select new { r.Number, r.ValueDate, r.BranchId, r.Status, Persona = p, a.InvoiceNumber, a.ConsultedBalance, a.Amount }).Take(2000).ToListAsync(ct);
        return Result.Success<IReadOnlyList<CommercialReceivableApplicationDto>>(filas.Where(f => mio.IncluyeOficina(f.BranchId))
            .Select(f => new CommercialReceivableApplicationDto(f.Number, f.ValueDate, f.Persona.PublicId, OperacionesDeCaja.Nombre(f.Persona), f.InvoiceNumber,
                f.ConsultedBalance, f.Amount, f.Status == ReceiptState.Voided)).ToList());
    }
}
