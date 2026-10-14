using System.Globalization;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>Una venta de la conciliación de la carga (api.md §19.2). Sin datos de la persona. (nuevo)</summary>
public sealed record MigrationSaleDto(
    Guid MessagePublicId,
    string InvoiceNumber,
    DateOnly SaleDate,
    decimal Amount,
    SaleReconciliationOutcome Classification,
    bool MarkedFromList,
    string? MarkReason,
    int AdjustmentsUpToCutoff,
    int AdjustmentsAfterCutoff,
    Guid? DifferencePublicId,
    bool Accepted);

/// <summary>Lo que dejó conciliar las ventas de una versión: los conteos, las filas y la huella. (nuevo)</summary>
public sealed record ConciliacionRegistrada(ConciliacionDeVentas Conteos, string Huella);

/// <summary>
/// La conciliación de ventas de la migración (feature 013, US8, T0742; FR-143, T83; data-model §21.5): sobre el clasificador
/// <see cref="ConciliacionDeVentasDeLaCarga"/> (lee por páginas, <b>sin consumir</b>, las ventas acumuladas hasta el corte y sus ajustes; clasifica
/// por factura contra <c>sales</c> y <c>loans</c>; copia las marcas de la versión anterior), hace nacer la diferencia de cada venta no migrada con
/// saldo (<c>SaleNotMigrated</c>) y de cada una marcada fuera de Cartera (<c>SaleOutsideLending</c>), y calcula la huella
/// <c>SalesReconciliationSha256</c>. Las diferencias <b>vigentes</b> de una versión son las del cuadre y las de ventas que todavía nombra su fila de
/// conciliación (<see cref="VigentesAsync"/>): al marcar una venta, su diferencia anterior queda sin efecto (los hechos no se borran). (nuevo)
/// </summary>
public sealed class ConciliacionDeVentasDeMigracion(IApplicationDbContext db, ConciliacionDeVentasDeLaCarga clasificador)
{
    /// <summary>Concilia y deja las filas y sus diferencias en el contexto; guarda una vez para enlazar la diferencia a su fila (el que llama está en transacción).</summary>
    public async Task<Result<ConciliacionRegistrada>> ConciliarAsync(MigrationLoad carga, int version, CancellationToken ct)
    {
        var conciliada = await clasificador.ConciliarAsync(carga, version, ct);
        if (conciliada.IsFailure) return Result.Failure<ConciliacionRegistrada>(conciliada.Error);

        var conDiferencia = conciliada.Value.Filas
            .Where(f => f.Outcome is SaleReconciliationOutcome.NotMigratedWithBalance or SaleReconciliationOutcome.OutsideLendingD29)
            .Select(f => (Fila: f, Diferencia: Diferencia(carga, version, f)))
            .ToList();
        db.MigrationLoadDifferences.AddRange(conDiferencia.Select(x => x.Diferencia));
        if (conDiferencia.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            foreach (var (fila, diferencia) in conDiferencia) fila.DifferenceId = diferencia.Id;
        }
        return Result.Success(new ConciliacionRegistrada(conciliada.Value, Huella(conciliada.Value.Filas)));
    }

    /// <summary>La diferencia de una venta: no migrada con saldo o fuera de Cartera, por su pago (una por venta).</summary>
    public static MigrationLoadDifference Diferencia(MigrationLoad carga, int version, MigrationSaleReconciliation fila)
    {
        var fuera = fila.Outcome == SaleReconciliationOutcome.OutsideLendingD29;
        return new MigrationLoadDifference
        {
            MigrationLoadId = carga.Id,
            CheckVersion = version,
            Kind = fuera ? MigrationDifferenceKind.SaleOutsideLending : MigrationDifferenceKind.SaleNotMigrated,
            DimensionKey = $"{(fuera ? "VentaFueraDeCartera" : "VentaNoMigrada")}|{fila.CreditPaymentPublicId:N}",
            CreditPaymentPublicId = fila.CreditPaymentPublicId,
            InvoiceNumber = fila.InvoiceNumber,
            LendingAmount = 0m,
            AccountingAmount = fila.Amount,
            Difference = -fila.Amount,
        };
    }

    /// <summary>La huella de la conciliación: pago, clasificación, monto y ajustes de cada venta, en orden (no depende de la versión).</summary>
    public static string Huella(IEnumerable<MigrationSaleReconciliation> filas)
    {
        var texto = new StringBuilder();
        foreach (var f in filas.OrderBy(f => f.CreditPaymentPublicId))
            texto.Append(CultureInfo.InvariantCulture,
                $"{f.CreditPaymentPublicId:N}|{(int)f.Outcome}|{f.Amount}|{f.AdjustmentsUpToCutoff}|{f.AdjustmentsAfterCutoff};");
        return texto.Length == 0 ? ApplyMigrationLoadCommandHandler.HuellaVacia : CuadreDeMigracion.Huella(texto.ToString());
    }

    /// <summary>
    /// Las diferencias vigentes de la versión: todas las del cuadre y, de las de ventas, las que nombra todavía su fila de conciliación (una venta
    /// marcada cancelada desde la lista ya no tiene diferencia; una marcada fuera de Cartera tiene la suya nueva).
    /// </summary>
    public static async Task<IReadOnlyList<MigrationLoadDifference>> VigentesAsync(IApplicationDbContext db, MigrationLoad carga, int version, CancellationToken ct)
    {
        var deVentas = db.MigrationSaleReconciliations.Where(r => r.MigrationLoadId == carga.Id && r.CheckVersion == version && !r.IsDeleted && r.DifferenceId != null)
            .Select(r => r.DifferenceId!.Value);
        return await db.MigrationLoadDifferences
            .Where(d => d.MigrationLoadId == carga.Id && d.CheckVersion == version && !d.IsDeleted
                        && (d.Kind == MigrationDifferenceKind.BalanceCheck || deVentas.Contains(d.Id)))
            .ToListAsync(ct);
    }

    /// <summary>Recalcula <c>IsBalanced</c> y <c>SalesReconciled</c> de la carga con las diferencias vigentes (sin guardar).</summary>
    public static void Actualizar(MigrationLoad carga, IReadOnlyList<MigrationLoadDifference> vigentes)
    {
        carga.IsBalanced = vigentes.Where(d => d.Kind == MigrationDifferenceKind.BalanceCheck).All(d => d.Aceptada);
        carga.SalesReconciled = vigentes.Where(d => d.Kind != MigrationDifferenceKind.BalanceCheck).All(d => d.Aceptada);
    }
}

/// <summary>
/// Las ventas de la conciliación de la versión vigente de una carga (feature 013, US8, T0742; api.md §19.2,
/// <c>GET /api/lending/migration/loads/{id}/reconciliation?classification=&amp;page=</c>, <c>Lending.Migration.Load</c>). (nuevo)
/// </summary>
public sealed record GetMigrationSalesReconciliationQuery(Guid MigrationLoadPublicId, SaleReconciliationOutcome? Classification = null, int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<MigrationSaleDto>>>;

public sealed class GetMigrationSalesReconciliationQueryValidator : AbstractValidator<GetMigrationSalesReconciliationQuery>
{
    public GetMigrationSalesReconciliationQueryValidator()
    {
        RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}

public sealed class GetMigrationSalesReconciliationQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMigrationSalesReconciliationQuery, Result<PagedResult<MigrationSaleDto>>>
{
    public async Task<Result<PagedResult<MigrationSaleDto>>> Handle(GetMigrationSalesReconciliationQuery request, CancellationToken ct)
    {
        var carga = await db.MigrationLoads.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
        if (carga is null) return Result.Failure<PagedResult<MigrationSaleDto>>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));

        var consulta = db.MigrationSaleReconciliations.AsNoTracking()
            .Where(r => r.MigrationLoadId == carga.Id && r.CheckVersion == carga.CurrentCheckVersion && !r.IsDeleted
                        && (request.Classification == null || r.Outcome == request.Classification));
        var total = await consulta.CountAsync(ct);
        var filas = await (from r in consulta
                           join d in db.MigrationLoadDifferences.AsNoTracking() on r.DifferenceId equals (int?)d.Id into ds
                           from d in ds.DefaultIfEmpty()
                           orderby r.SaleDate, r.InvoiceNumber
                           select new MigrationSaleDto(r.MessagePublicId, r.InvoiceNumber, r.SaleDate, r.Amount, r.Outcome, r.MarkedFromList, r.MarkReason,
                               r.AdjustmentsUpToCutoff, r.AdjustmentsAfterCutoff, d == null ? null : d.PublicId, d != null && d.AcceptedAt != null))
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<MigrationSaleDto>(filas, request.Page, request.PageSize, total));
    }
}

/// <summary>
/// Marca desde la lista una venta pendiente de la conciliación (feature 013, US8, T0742; api.md §19.2,
/// <c>POST /api/lending/migration/loads/{id}/reconciliation/marks</c>, <c>Lending.Migration.Load</c>): <c>CancelledBeforeCutoff</c> (la venta se
/// pagó antes del corte y la parte <c>sales</c> no la trae) u <c>OutsideLendingD29</c> (venta de servicios a asociados confirmada antes de
/// <c>CreditWithoutLending</c>, T83), con motivo. Sólo con la carga <c>Applied</c> y sobre una venta pendiente de la versión vigente (si no,
/// <c>Lending.MigrationLoad.SaleNotPending</c>). La marcada fuera de Cartera tiene su diferencia <c>SaleOutsideLending</c>, que se acepta venta
/// por venta; la marcada cancelada deja de tener diferencia. La huella de la conciliación cambia. (nuevo)
/// </summary>
public sealed record MarkMigrationSaleCommand(Guid MigrationLoadPublicId, Guid MessagePublicId, SaleReconciliationOutcome Classification, string Reason)
    : IRequest<Result<MigrationSaleDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkMigrationSaleCommandValidator : ValidadorConMotivo<MarkMigrationSaleCommand>
{
    public MarkMigrationSaleCommandValidator()
    {
        RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
        RuleFor(x => x.MessagePublicId).NotEmpty();
        RuleFor(x => x.Classification)
            .Must(c => c is SaleReconciliationOutcome.CancelledBeforeCutoff or SaleReconciliationOutcome.OutsideLendingD29)
            .WithMessage("Desde la lista sólo se marca una venta como cancelada antes del corte o fuera de Cartera.");
    }
}

public sealed class MarkMigrationSaleCommandHandler(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<MarkMigrationSaleCommand, Result<MigrationSaleDto>>
{
    public async Task<Result<MigrationSaleDto>> Handle(MarkMigrationSaleCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<MigrationSaleDto>(ErroresDeActivacion.SinUsuario());
        var carga = await db.MigrationLoads.FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
        if (carga is null) return Result.Failure<MigrationSaleDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));

        var fila = carga.Status != MigrationLoadStatus.Applied ? null
            : await db.MigrationSaleReconciliations.FirstOrDefaultAsync(r => r.MigrationLoadId == carga.Id && r.CheckVersion == carga.CurrentCheckVersion
                && r.MessagePublicId == request.MessagePublicId && !r.IsDeleted, ct);
        if (fila is null || !fila.Pendiente || fila.Outcome != SaleReconciliationOutcome.NotMigratedWithBalance)
            return Result.Failure<MigrationSaleDto>(ErroresDeMigracion.VentaNoPendiente(request.MessagePublicId));

        var anterior = fila.DifferenceId is { } id ? await db.MigrationLoadDifferences.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct) : null;
        if (anterior?.Aceptada == true) return Result.Failure<MigrationSaleDto>(ErroresDeMigracion.VentaNoPendiente(request.MessagePublicId));

        fila.Marcar(request.Classification, request.Reason, usuario, reloj.UtcNow, carga.Status);
        MigrationLoadDifference? nueva = null;
        if (request.Classification == SaleReconciliationOutcome.OutsideLendingD29)
        {
            nueva = ConciliacionDeVentasDeMigracion.Diferencia(carga, carga.CurrentCheckVersion, fila);
            db.MigrationLoadDifferences.Add(nueva);
            await db.SaveChangesAsync(ct);
        }
        fila.DifferenceId = nueva?.Id;

        var filas = await db.MigrationSaleReconciliations.Where(r => r.MigrationLoadId == carga.Id && r.CheckVersion == carga.CurrentCheckVersion && !r.IsDeleted)
            .ToListAsync(ct);
        await db.SaveChangesAsync(ct);
        var vigentes = await ConciliacionDeVentasDeMigracion.VigentesAsync(db, carga, carga.CurrentCheckVersion, ct);
        ConciliacionDeVentasDeMigracion.Actualizar(carga, vigentes);
        carga.SalesReconciliationSha256 = ConciliacionDeVentasDeMigracion.Huella(filas);
        await db.SaveChangesAsync(ct);

        return Result.Success(new MigrationSaleDto(fila.MessagePublicId, fila.InvoiceNumber, fila.SaleDate, fila.Amount, fila.Outcome, fila.MarkedFromList,
            fila.MarkReason, fila.AdjustmentsUpToCutoff, fila.AdjustmentsAfterCutoff, nueva?.PublicId, false));
    }
}
