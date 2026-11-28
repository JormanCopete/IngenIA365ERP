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
/// <para>
/// 2026-11-27 (feature 028, E4, T086; R14): siembra también <c>RECOGIDA</c>, el medio del cruce interno (clase <c>InternalOffset</c>) con que el
/// desembolso de un crédito nuevo recoge facturas a crédito de Comercial en un recibo sin caja de Recaudo. No mueve dinero, no se arquea y no se ofrece
/// en ningún punto, canal ni tipo de documento (la caja y el punto de venta nunca lo muestran). Es del sistema: no se crea otro ni se cambia su clase.
/// Idempotente por código, como el efectivo.
/// </para>
/// </summary>
public sealed class DefaultPaymentMeansSeeder : IDataSeeder
{
    public const string CodigoEfectivo = "EFECTIVO";

    /// <summary>Medio de pago DIAN «Efectivo» sugerido; lo valida la contadora.</summary>
    public const string CodigoDianSugerido = "10";

    public const string Nota = "Sembrado por el sistema. Código DIAN 10 (efectivo) pendiente de validar por la contadora.";

    /// <summary>2026-11-27 (feature 028, E4, T086): el medio del cruce interno de la recogida de facturas en el desembolso.</summary>
    public const string CodigoRecogida = "RECOGIDA";

    /// <summary>El código DIAN «acuerdo mutuo» (ZZZ): el cruce interno nunca va en una factura electrónica; queda por completitud del catálogo.</summary>
    public const string CodigoDianRecogida = "ZZZ";

    public const string NotaRecogida = "Sembrado por el sistema. Lo usa el recibo sin caja con que el desembolso de un crédito recoge facturas a crédito de "
        + "Comercial: no entra dinero y no se ofrece en la caja ni en el punto de venta.";

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
        var sembrados = await RecogidaAsync(db, ct);
        if (await db.PaymentMeans.IgnoreQueryFilters().AnyAsync(m => m.Code == CodigoEfectivo, ct)) return sembrados;

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
        return sembrados + 1;
    }

    /// <summary>
    /// 2026-11-27 (feature 028, E4, T086): el medio <see cref="CodigoRecogida"/> de la clase <c>InternalOffset</c>, si no hay ninguno de esa clase ni otro medio con
    /// ese código (también de baja): no se ofrece en ningún lado, sin arqueo, sin vueltas, activo. Devuelve cuántos sembró.
    /// </summary>
    public static async Task<int> RecogidaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        if (await db.PaymentMeans.IgnoreQueryFilters().AnyAsync(m => m.Code == CodigoRecogida || m.Class == PaymentMeansClass.InternalOffset, ct)) return 0;
        db.PaymentMeans.Add(new PaymentMeans
        {
            Code = CodigoRecogida,
            Name = "Recogida en el desembolso",
            DisplayOrder = 999,
            Class = PaymentMeansClass.InternalOffset,
            RequiresReference = false,
            AllowsChange = false,
            AllowsPartial = true,
            UniqueReference = false,
            CountMethod = CashCountMethod.None,
            RequiresTerminalBatchAtClose = false,
            ToleranceAmount = 0m,
            DianPaymentMeansCode = CodigoDianRecogida,
            OfferedAtAllPointsOfSale = false,
            OfferedInAllChannels = false,
            OfferedForAllDocumentTypes = false,
            IsActive = true,
            ValidFrom = CashDenominationsSeeder.VigenciaDeLaSemilla,
            Notes = NotaRecogida,
            CreatedBy = SeedContext.ParametricCreatedBy,
        });
        await db.SaveChangesAsync(ct);
        return 1;
    }
}
