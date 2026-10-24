using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Arqueo;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Sessions;

/// <summary>Un medio tal como quedó sellado al abrir la sesión (R-14). (nuevo)</summary>
public sealed record MedioSellado(int PaymentMeansId, Guid PublicId, string Code, string Name, PaymentMeansClass Class, CashCountMethod CountMethod, decimal Tolerance);

/// <summary>
/// Lo que la sesión sella al abrir (feature 015, T0092; R-14; data-model §2.2): el modo de base, los topes, el arqueo ciego, la doble custodia, el
/// tratamiento del faltante, los días del sobrante y los medios del punto con su método y su tolerancia. No cambia aunque cambien la caja, el punto o los
/// parámetros (US1-3). (nuevo)
/// </summary>
public sealed record ValoresSelladosDeLaSesion(
    CashBoxBaseMode BaseMode,
    decimal FixedFund,
    decimal? CashWarnCap,
    decimal? CashBlockCap,
    decimal? DailyCashOutCap,
    bool BlindCount,
    bool DoubleCustody,
    bool OneSessionPerCashier,
    CountTreatment ShortageTreatment,
    int? SurplusDaysBeforeIncome,
    int? CashMeansId,
    IReadOnlyList<MedioSellado> Means)
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public string Json() => JsonSerializer.Serialize(this, Opciones);

    public static ValoresSelladosDeLaSesion De(CashSession sesion) =>
        JsonSerializer.Deserialize<ValoresSelladosDeLaSesion>(sesion.SealedSettingsJson, Opciones)
        ?? throw new InvalidOperationException($"La sesión {sesion.PublicId} no tiene valores sellados.");

    public MedioSellado? Medio(int paymentMeansId) => Means.FirstOrDefault(m => m.PaymentMeansId == paymentMeansId);

    public IReadOnlyList<MedioDeArqueo> MediosDeArqueo() =>
        Means.Select(m => new MedioDeArqueo(m.PaymentMeansId, m.Code, m.Class, m.CountMethod, m.Tolerance)).ToList();
}

/// <summary>
/// Lo común de la sesión de caja (feature 015, T0092, T0093): la sesión visible por alcance, lo esperado por medio con el motor <see cref="ArqueoDeCaja"/>
/// (recibos de la sesión desde la fase 6, movimientos propios y los traslados que la tienen de contraparte), su proyección en
/// <see cref="CashSession.ExpectedByMeansJson"/>, las denominaciones y el número de los documentos de la caja. (nuevo)
/// </summary>
public static class SesionesDeCaja
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    /// <summary>La sesión, si su caja está al alcance.</summary>
    public static async Task<CashSession?> VisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.ReceiptsCashSessions : db.ReceiptsCashSessions.AsNoTracking();
        var sesion = await consulta.FirstOrDefaultAsync(s => s.PublicId == publicId, ct);
        if (sesion is null) return null;
        var caja = await db.CashBoxes.AsNoTracking().Where(c => c.Id == sesion.CashBoxId).Select(c => new { c.Id, c.CollectionPointId }).FirstAsync(ct);
        var oficina = await db.CollectionPoints.AsNoTracking().Where(p => p.Id == caja.CollectionPointId).Select(p => p.BranchId).FirstAsync(ct);
        var mio = await alcance.ObtenerAsync(ct);
        return mio.IncluyeCaja(caja.Id, caja.CollectionPointId) || mio.IncluyeOficina(oficina) ? sesion : null;
    }

    /// <summary>La sesión recibe operaciones: abierta; la olvidada y la que se cierra no.</summary>
    public static Result Operable(CashSession sesion) => sesion.Status switch
    {
        CashSessionState.Open => Result.Success(),
        CashSessionState.ForgottenOpen => Result.Failure(ErroresDeRecaudo.SesionOlvidada()),
        _ => Result.Failure(ErroresDeRecaudo.SesionNoAbierta()),
    };

    /// <summary>
    /// Los cobros de la sesión por medio (fase 6): lo que cada medio de los recibos de la sesión dejó en la caja —su valor menos las vueltas, que salen del
    /// efectivo— y si el recibo se anuló (la anulación llega con US5).
    /// </summary>
    public static async Task<IReadOnlyList<CobroDeLaSesion>> CobrosAsync(IApplicationDbContext db, CashSession sesion, CancellationToken ct)
    {
        var filas = await (from m in db.ReceiptMeans.AsNoTracking()
                           join r in db.IssuedReceipts.AsNoTracking() on m.ReceiptId equals r.Id
                           where r.CashSessionId == sesion.Id
                           orderby r.Id, m.Ordinal
                           select new { m.ReceiptId, m.PaymentMeansId, m.MeansClass, m.Amount, m.Reference, r.ChangeGiven, Anulado = r.Status == ReceiptState.Voided })
            .ToListAsync(ct);
        var cobros = new List<CobroDeLaSesion>();
        foreach (var recibo in filas.GroupBy(f => f.ReceiptId))
        {
            var vueltas = recibo.First().ChangeGiven;
            // Las vueltas salen del último efectivo del recibo.
            var ultimoEfectivo = recibo.LastOrDefault(f => f.MeansClass == PaymentMeansClass.Cash);
            foreach (var f in recibo)
            {
                var valor = ReferenceEquals(f, ultimoEfectivo) ? f.Amount - vueltas : f.Amount;
                cobros.Add(new CobroDeLaSesion(f.PaymentMeansId, valor, f.Anulado, f.Reference));
            }
        }
        return cobros;
    }

    /// <summary>Los movimientos que tocan la sesión: los suyos y los traslados que la tienen de contraparte (entradas).</summary>
    public static async Task<IReadOnlyList<MovimientoDeLaSesion>> MovimientosAsync(IApplicationDbContext db, CashSession sesion, int? medioDelEfectivo,
        CancellationToken ct)
    {
        var propios = await db.CashMovements.AsNoTracking().Where(m => m.CashSessionId == sesion.Id).ToListAsync(ct);
        var entrantes = await db.CashMovements.AsNoTracking()
            .Where(m => m.CounterpartSessionId == sesion.Id && m.Kind == CashMovementKindR.TransferBetweenBoxes).ToListAsync(ct);
        var lista = new List<MovimientoDeLaSesion>();
        foreach (var m in propios)
        {
            var medio = m.FromMeansId ?? medioDelEfectivo;
            if (m.Kind == CashMovementKindR.Opening || medio is null) continue;
            lista.Add(new MovimientoDeLaSesion(m.Kind, medio.Value, m.ToMeansId, m.Amount, EsOrigen: true));
        }
        foreach (var m in entrantes)
        {
            var medio = m.FromMeansId ?? medioDelEfectivo;
            if (medio is null) continue;
            lista.Add(new MovimientoDeLaSesion(m.Kind, medio.Value, null, m.Amount, EsOrigen: false));
        }
        // Feature 015, R1 (T0372; R-14): los egresos de caja de la sesión salen del efectivo —también el que después se anuló: la sesión que lo entregó no
        // cambia— y el dinero de un egreso anulado vuelve como entrada de la sesión donde se anula.
        if (medioDelEfectivo is { } efectivo)
        {
            var egresos = await db.CashDisbursements.AsNoTracking().Where(e => e.CashSessionId == sesion.Id).Select(e => e.Amount).ToListAsync(ct);
            lista.AddRange(egresos.Select(v => new MovimientoDeLaSesion(CashMovementKindR.CashDisbursement, efectivo, null, v)));
            var vueltos = await (from v in db.CashDisbursementVoidings.AsNoTracking()
                                 join e in db.CashDisbursements.AsNoTracking() on v.CashDisbursementId equals e.Id
                                 where v.CashSessionId == sesion.Id
                                 select e.Amount).ToListAsync(ct);
            lista.AddRange(vueltos.Select(v => new MovimientoDeLaSesion(CashMovementKindR.CashDisbursementReturn, efectivo, null, v, EsOrigen: false)));
        }
        return lista;
    }

    /// <summary>Lo esperado por medio a este momento.</summary>
    public static async Task<IReadOnlyList<EsperadoDeMedio>> EsperadoAsync(IApplicationDbContext db, CashSession sesion, CancellationToken ct)
    {
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        var pedido = new PedidoDeArqueo(
            sellados.MediosDeArqueo(),
            sellados.CashMeansId,
            sesion.OpeningBase,
            await CobrosAsync(db, sesion, ct),
            await MovimientosAsync(db, sesion, sellados.CashMeansId, ct));
        return ArqueoDeCaja.Esperado(pedido);
    }

    /// <summary>El efectivo esperado de la sesión (el medio de clase efectivo sellado), con lo que todavía no se guardó.</summary>
    public static async Task<decimal> EfectivoAsync(IApplicationDbContext db, CashSession sesion, CancellationToken ct)
    {
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        if (sellados.CashMeansId is not { } efectivo) return 0m;
        return (await EsperadoAsync(db, sesion, ct)).Where(e => e.Medio.PaymentMeansId == efectivo).Sum(e => e.Expected);
    }

    /// <summary>Recalcula la proyección de lo esperado. Se llama después de guardar el documento que la cambia (lee lo guardado).</summary>
    public static async Task ProyectarAsync(IApplicationDbContext db, CashSession sesion, CancellationToken ct)
    {
        var esperado = await EsperadoAsync(db, sesion, ct);
        sesion.ExpectedByMeansJson = JsonSerializer.Serialize(
            esperado.Select(e => new { e.Medio.PaymentMeansId, e.Medio.Code, e.Expected }), Opciones);
    }

    /// <summary>Las denominaciones contadas resueltas contra el catálogo, con su valor, o el error.</summary>
    public static async Task<Result<IReadOnlyList<(CashDenomination Denominacion, int Cantidad, decimal Valor)>>> DenominacionesAsync(
        IApplicationDbContext db, IReadOnlyList<DenominacionContadaDto>? contadas, CancellationToken ct)
    {
        var lista = new List<(CashDenomination, int, decimal)>();
        if (contadas is null || contadas.Count == 0) return Result.Success<IReadOnlyList<(CashDenomination, int, decimal)>>(lista);
        var ids = contadas.Select(c => c.DenominationPublicId).Distinct().ToList();
        var catalogo = await db.CashDenominations.AsNoTracking().Where(d => ids.Contains(d.PublicId)).ToDictionaryAsync(d => d.PublicId, ct);
        foreach (var c in contadas.GroupBy(c => c.DenominationPublicId))
        {
            if (!catalogo.TryGetValue(c.Key, out var d) || !d.IsActive)
                return Result.Failure<IReadOnlyList<(CashDenomination, int, decimal)>>(ErroresDeRecaudo.NoEncontrado("La denominación"));
            var cantidad = c.Sum(x => x.Quantity);
            if (cantidad < 0) return Result.Failure<IReadOnlyList<(CashDenomination, int, decimal)>>(ErroresDeRecaudo.MovimientoInvalido("Una cantidad no puede ser negativa."));
            if (cantidad > 0) lista.Add((d, cantidad, d.Value * cantidad));
        }
        return Result.Success<IReadOnlyList<(CashDenomination, int, decimal)>>(lista);
    }

    /// <summary>El tipo de documento de la clase y el prefijo del punto (si el tipo lo usa).</summary>
    public static async Task<(ReceiptDocumentType Tipo, string Prefijo)> TipoYPrefijoAsync(IApplicationDbContext db, ReceiptDocumentKind clase, int? puntoId,
        CancellationToken ct)
    {
        var tipo = await db.ReceiptDocumentTypes.AsNoTracking()
            .Where(t => t.Kind == clase && t.Status == ReceiptsCatalogStatus.Active).OrderBy(t => t.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"No hay un tipo de documento activo de la clase {clase}: la semilla de RecaudoNucleo lo deja.");
        var prefijo = string.Empty;
        if (tipo.UsesPointPrefix && puntoId is { } p)
            prefijo = await db.CollectionPoints.AsNoTracking().Where(x => x.Id == p).Select(x => x.DocumentPrefix).FirstOrDefaultAsync(ct) ?? string.Empty;
        return (tipo, prefijo);
    }

    /// <summary>Los medios vigentes del punto, activos, con su método y su tolerancia.</summary>
    public static async Task<IReadOnlyList<MedioSellado>> MediosDelPuntoAsync(IApplicationDbContext db, int puntoId, DateOnly hoy, CancellationToken ct)
    {
        var filas = await (from o in db.PaymentMeansCollectionPoints.AsNoTracking()
                           join m in db.PaymentMeans.AsNoTracking() on o.PaymentMeansId equals m.Id
                           where o.CollectionPointId == puntoId && m.IsActive
                           select new { o.ValidFrom, o.ValidTo, Medio = m }).ToListAsync(ct);
        return filas.Where(f => VigenciaDeRecaudo.Incluye(f.ValidFrom, f.ValidTo, hoy))
            .Select(f => f.Medio).DistinctBy(m => m.Id).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code)
            .Select(m => new MedioSellado(m.Id, m.PublicId, m.Code, m.Name, m.Class, m.CountMethod, m.ToleranceAmount)).ToList();
    }

    /// <summary>El DTO de la sesión.</summary>
    public static async Task<CashSessionDto> DtoAsync(IApplicationDbContext db, CashSession s, CancellationToken ct)
    {
        var caja = await (from c in db.CashBoxes.AsNoTracking()
                          join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                          where c.Id == s.CashBoxId
                          select new { c.PublicId, c.Code, PuntoPublicId = p.PublicId, Punto = p.Code }).FirstAsync(ct);
        var cajero = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == s.CashierUserId).Select(u => new { u.PublicId, u.Username }).FirstAsync(ct);
        var dia = await db.CollectionPointDays.AsNoTracking().Where(d => d.Id == s.CollectionPointDayId).Select(d => d.BusinessDate).FirstAsync(ct);
        var sellados = ValoresSelladosDeLaSesion.De(s);
        return new CashSessionDto(s.PublicId, caja.PublicId, caja.Code, caja.PuntoPublicId, caja.Punto, cajero.PublicId, cajero.Username, dia, s.Status,
            s.OpenedAt, s.ClosedAt, s.OpeningBase, s.IsForgotten, sellados.BlindCount, sellados.DoubleCustody, sellados.CashWarnCap, sellados.CashBlockCap,
            sellados.Means.Select(m => new MedioDeSesionDto(m.PublicId, m.Code, m.Name, m.Class, m.CountMethod, m.Tolerance)).ToList());
    }

    /// <summary>El nombre de un usuario (para los mensajes).</summary>
    public static async Task<string> NombreAsync(IApplicationDbContext db, int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.Username).FirstOrDefaultAsync(ct) ?? "otro usuario";

    /// <summary>La oficina (<c>COR_Branches.Id</c>) y el punto de la caja de una sesión.</summary>
    public static async Task<(int Oficina, int Punto)> OficinaYPuntoAsync(IApplicationDbContext db, CashSession sesion, CancellationToken ct)
    {
        var r = await (from c in db.CashBoxes.AsNoTracking()
                       join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                       where c.Id == sesion.CashBoxId
                       select new { p.BranchId, PuntoId = p.Id }).FirstAsync(ct);
        return (r.BranchId, r.PuntoId);
    }

    /// <summary>El arqueo con sus líneas.</summary>
    public static async Task<CashCountDto> ArqueoDtoAsync(IApplicationDbContext db, CashCount arqueo, CashSessionState? estado, CancellationToken ct)
    {
        var lineas = await (from l in db.ReceiptsCashCountLines.AsNoTracking()
                            join m in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters() on l.PaymentMeansId equals m.Id
                            where l.CashCountId == arqueo.Id
                            orderby l.Id
                            select new { l, m.PublicId, m.Code }).ToListAsync(ct);
        var dtos = lineas.Select(x => new CashCountLineDto(x.l.PublicId, x.PublicId, x.Code, x.l.Method, x.l.Expected, x.l.Counted, x.l.Difference,
            x.l.Tolerance, x.l.WithinTolerance, x.l.Treatment, x.l.Reason, x.l.PorAprobar, x.l.ApprovedAt)).ToList();
        return new CashCountDto(arqueo.PublicId, arqueo.Kind, arqueo.CountedAt, dtos.Sum(d => d.Expected), dtos.Sum(d => d.Counted), dtos.Sum(d => d.Difference),
            dtos.Where(d => d.PendingApproval).Sum(d => Math.Abs(d.Difference)), estado, dtos);
    }
}
