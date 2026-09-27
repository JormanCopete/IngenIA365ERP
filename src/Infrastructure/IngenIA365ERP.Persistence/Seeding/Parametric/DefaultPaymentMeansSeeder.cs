using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// El único medio de pago sembrado, <c>EFECTIVO</c> (<c>COR_PaymentMeans</c>; feature 012, I3, T587; FR-096; data-model §16;
/// decisiones-transversales §2.14, Order 86): clase <c>Cash</c>, arqueo físico, admite vueltas y pago parcial, ofrecido en todos
/// los puntos, canales y tipos, tolerancia cero y código de medio de pago DIAN <c>10</c> sugerido —«pendiente de validar por la
/// contadora» (A8)—. Tarjetas, bonos, créditos y consignaciones son datos de cada cooperativa: se crean en la pantalla o con la
/// plantilla de medios. Idempotente por <c>Code</c> <b>incluidos los de baja</b> y sin pisar: lo que la cooperativa cambió del
/// efectivo (nombre, tolerancia, código DIAN) se queda.
///
/// <para>
/// La tabla llega con el par <c>VentasYPuntoDeVenta</c> (T586): hasta que esa migración esté aplicada en la base, la semilla no
/// hace nada.
/// </para>
/// </summary>
public sealed class DefaultPaymentMeansSeeder : IDataSeeder
{
    public const string CodigoEfectivo = "EFECTIVO";

    /// <summary>Medio de pago DIAN «Efectivo» sugerido; lo valida la contadora.</summary>
    public const string CodigoDianSugerido = "10";

    public const string Nota = "Sembrado por el sistema. Código DIAN 10 (efectivo) pendiente de validar por la contadora.";

    public int Order => 86;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        if (!await CashDenominationsSeeder.TieneLaMigracionAsync(db, context.Logger, "[Ventas.MediosSinTablas]", ct)) return 0;
        return await AplicarAsync(db, ct);
    }

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        if (await db.PaymentMeans.IgnoreQueryFilters().AnyAsync(m => m.Code == CodigoEfectivo, ct)) return 0;

        db.PaymentMeans.Add(new PaymentMeans
        {
            Code = CodigoEfectivo,
            Name = "Efectivo",
            DisplayOrder = 1,
            Class = PaymentMeansClass.Cash,
            RequiresReference = false,
            AllowsChange = true,
            AllowsPartial = true,
            UniqueReference = false,
            CountMethod = CashCountMethod.PhysicalCount,
            RequiresTerminalBatchAtClose = false,
            ToleranceAmount = 0m,
            DianPaymentMeansCode = CodigoDianSugerido,
            OfferedAtAllPointsOfSale = true,
            OfferedInAllChannels = true,
            OfferedForAllDocumentTypes = true,
            IsActive = true,
            ValidFrom = CashDenominationsSeeder.VigenciaDeLaSemilla,
            Notes = Nota,
            CreatedBy = SeedContext.ParametricCreatedBy,
        });
        await db.SaveChangesAsync(ct);
        return 1;
    }
}
