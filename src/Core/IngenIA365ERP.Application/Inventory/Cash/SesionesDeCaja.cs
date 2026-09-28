using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Sales.Cash;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Cash;

/// <summary>
/// Lo que comparten los comandos y las consultas de la caja (feature 012, I3, T617–T620; contracts/api.md §21):
/// <list type="bullet">
/// <item>la sesión visible: existe, su punto está en el alcance y es del usuario —o quien pregunta tiene
/// <c>Inventory.CashSessions.ViewAll</c>—; si no, nula (el mismo 404 que la inexistente);</item>
/// <item>el esperado de una sesión por <see cref="CalculadoraDeEsperado"/> (pura): el catálogo de medios, la base del fondo fijo en el
/// efectivo, los pagos de los documentos <b>confirmados</b> de la sesión y los movimientos de caja que la tocan como origen o destino
/// (sólo cuentan los confirmados);</item>
/// <item>el medio de efectivo (el primero activo de clase <c>Cash</c> por orden de presentación) y el <see cref="CashSessionDto"/>.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class SesionesDeCaja(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos)
{
    public const string PermisoVerTodas = "Inventory.CashSessions.ViewAll";

    private bool? _veTodas;

    /// <summary>¿Quien pregunta ve (y cierra) las sesiones de otros cajeros? Memorizado por petición.</summary>
    public async Task<bool> VeTodasAsync(CancellationToken ct) => _veTodas ??= await permisos.HasPermissionAsync(PermisoVerTodas, ct);

    /// <summary>El <c>SEC_Users.Id</c> de quien pregunta (nunca el entero del token), o nulo.</summary>
    public async Task<int?> UsuarioAsync(CancellationToken ct) => (await actorActual.ObtenerAsync(ct)).UserId;

    /// <summary>La sesión <paramref name="publicId"/> si quien pregunta la ve; nula si no existe, está fuera del alcance o es ajena sin <c>ViewAll</c>.</summary>
    public async Task<CashSession?> VisibleAsync(Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.CashSessions : db.CashSessions.AsNoTracking();
        var sesion = await consulta.FirstOrDefaultAsync(s => s.PublicId == publicId, ct);
        if (sesion is null) return null;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (!alcance.IncluyePunto(sesion.PointOfSaleId)) return null;
        if (sesion.CashierUserId == await UsuarioAsync(ct)) return sesion;
        return await VeTodasAsync(ct) ? sesion : null;
    }

    /// <summary>Las sesiones que quien pregunta ve: de su alcance y, sin <c>ViewAll</c>, sólo las suyas.</summary>
    public async Task<IQueryable<CashSession>> VisiblesAsync(CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.CashSessions.AsNoTracking().PorPunto(alcance, s => s.PointOfSaleId);
        if (await VeTodasAsync(ct)) return consulta;
        var usuario = await UsuarioAsync(ct) ?? -1;
        return consulta.Where(s => s.CashierUserId == usuario);
    }

    /// <summary>El medio al que entra la base: el primer medio activo de clase efectivo por orden de presentación.</summary>
    public async Task<PaymentMeans?> EfectivoAsync(CancellationToken ct) =>
        await db.PaymentMeans.AsNoTracking().Where(m => m.Class == PaymentMeansClass.Cash && m.IsActive)
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code).FirstOrDefaultAsync(ct);

    // --------------------------------------------------------------------------------------------- esperado --

    /// <summary>
    /// El esperado de <paramref name="sesion"/> (§21.2). <paramref name="adicionales"/> son medios que se contaron sin tener línea
    /// (efectivo sin movimiento, por ejemplo): entran con esperado cero para que el arqueo los mida.
    /// </summary>
    public async Task<EsperadoDeLaSesion> EsperadoAsync(CashSession sesion, IReadOnlyCollection<int>? adicionales, CancellationToken ct)
    {
        var medios = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters()
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code)
            .Select(m => new MedioDeArqueo(m.Id, m.Code, m.Class, m.CountMethod, m.ToleranceAmount))
            .ToListAsync(ct);

        BaseDeApertura? @base = null;
        if (sesion.BaseMode == CashSession.BaseFondoFijo && sesion.OpeningBase > 0m && await EfectivoAsync(ct) is { } efectivo)
            @base = new BaseDeApertura(efectivo.Id, sesion.OpeningBase);

        var pagos = await (from p in db.DocumentPayments.AsNoTracking()
                           join d in db.InventoryDocuments.AsNoTracking() on p.DocumentId equals d.Id
                           where p.CashSessionId == sesion.Id && !p.IsDeleted && d.Status == DocumentStatus.Confirmed
                           orderby p.Id
                           select new { p.Id, p.PaymentMeansId, p.Direction, p.Amount, p.CardTerminalId, p.Reference, d.Prefix, d.Number })
            .ToListAsync(ct);

        var movimientos = await MovimientosAsync(sesion.Id, ct);
        var calculado = CalculadoraDeEsperado.Calcular(new PedidoDeEsperado(sesion.Id, medios, @base,
            pagos.Select(p => new PagoDeLaSesion(p.Id, p.PaymentMeansId, p.Direction, p.Amount, p.CardTerminalId, p.Reference,
                VistaDeDocumentos.NumeroVisible(p.Prefix, p.Number))).ToList(),
            movimientos));

        if (adicionales is not { Count: > 0 }) return calculado;
        var lineas = calculado.Lines.ToList();
        foreach (var faltante in adicionales.Where(a => lineas.All(l => l.Medio.PaymentMeansId != a)))
        {
            var medio = medios.FirstOrDefault(m => m.PaymentMeansId == faltante);
            if (medio is not null) lineas.Add(new EsperadoPorMedio(medio, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0, [], []));
        }
        var orden = medios.Select((m, i) => (m.PaymentMeansId, i)).ToDictionary(x => x.PaymentMeansId, x => x.i);
        return new EsperadoDeLaSesion(lineas.OrderBy(l => orden[l.Medio.PaymentMeansId]).ToList());
    }

    /// <summary>
    /// Los movimientos de caja que tocan la sesión (origen o destino), con si están confirmados. Una reclasificación sale del datáfono
    /// del pago que corrige y, si no dice otro, entra al mismo: el voucher pasó por ese datáfono aunque se registró con otro medio.
    /// Hasta el 2026-09-27 el datáfono de origen iba nulo y el lote del datáfono seguía esperando lo reclasificado: el cierre pedía
    /// motivo para dos diferencias que no existían (e2e T566).
    /// </summary>
    public async Task<IReadOnlyList<MovimientoDeCaja>> MovimientosAsync(int sesionId, CancellationToken ct) =>
        await (from m in db.CashMovementDetails.AsNoTracking()
               join d in db.InventoryDocuments.AsNoTracking() on m.DocumentId equals d.Id
               join p in db.DocumentPayments.AsNoTracking() on m.ReclassifiedPaymentId equals (int?)p.Id into corregidos
               from p in corregidos.DefaultIfEmpty()
               where !m.IsDeleted && (m.CashSessionId == sesionId || m.DestinationCashSessionId == sesionId)
               orderby m.Id
               select new MovimientoDeCaja(m.DocumentId, m.Kind, d.Status == DocumentStatus.Confirmed, m.CashSessionId, m.DestinationCashSessionId,
                   m.SourcePaymentMeansId, m.TargetPaymentMeansId, m.Amount, p == null ? null : p.CardTerminalId,
                   m.TargetCardTerminalId ?? (p == null ? null : p.CardTerminalId)))
            .ToListAsync(ct);

    /// <summary>
    /// El <see cref="CashSessionExpectedDto"/> (§21.2). Con arqueo ciego y sin <c>ViewAll</c>, <c>blind</c> y las cifras nulas.
    /// </summary>
    public async Task<CashSessionExpectedDto> EsperadoDtoAsync(CashSession sesion, CancellationToken ct)
    {
        var esperado = await EsperadoAsync(sesion, null, ct);
        var ciego = sesion.IsBlindCount && !await VeTodasAsync(ct);
        var ids = esperado.Lines.Select(l => l.Medio.PaymentMeansId).ToList();
        var medios = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => new { m.PublicId, m.Name }, ct);
        var terminalIds = esperado.Lines.SelectMany(l => l.Terminals).Select(t => t.CardTerminalId).Distinct().ToList();
        var terminales = await (from t in db.CardTerminals.AsNoTracking().IgnoreQueryFilters()
                                join a in db.CardAcquirers.AsNoTracking().IgnoreQueryFilters() on t.CardAcquirerId equals a.Id into aa
                                from a in aa.DefaultIfEmpty()
                                where terminalIds.Contains(t.Id)
                                select new { t.Id, t.PublicId, t.Code, Acquirer = a == null ? null : a.Code })
            .ToDictionaryAsync(t => t.Id, ct);
        var pagoIds = esperado.Lines.SelectMany(l => l.References).Select(r => r.DocumentPaymentId).ToList();
        var pagos = await db.DocumentPayments.AsNoTracking().Where(p => pagoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);

        var lineas = esperado.Lines.Select(l =>
        {
            decimal? Cifra(decimal v) => ciego ? null : v;
            var medio = medios.GetValueOrDefault(l.Medio.PaymentMeansId);
            return new CashExpectedLineDto(
                new CashExpectedMeansDto(medio?.PublicId ?? Guid.Empty, l.Medio.Code, medio?.Name ?? l.Medio.Code, l.Medio.Class, l.Medio.CountMethod),
                Cifra(l.OpeningBase), Cifra(l.Sales), Cifra(l.Refunds), Cifra(l.MovementsIn), Cifra(l.MovementsOut),
                Cifra(l.ReclassificationsIn), Cifra(l.ReclassificationsOut), Cifra(l.Expected), l.Tolerance, l.PaymentsCount,
                l.Terminals.Count == 0 ? null : l.Terminals.Select(t =>
                {
                    var term = terminales.GetValueOrDefault(t.CardTerminalId);
                    return new CashExpectedTerminalDto(term?.PublicId ?? Guid.Empty, term?.Code ?? string.Empty, term?.Acquirer, Cifra(t.Expected), t.PaymentsCount);
                }).ToList(),
                l.References.Count == 0 ? null : l.References
                    .Select(r => new CashExpectedReferenceDto(pagos.GetValueOrDefault(r.DocumentPaymentId), r.DocumentNumber, r.Reference, r.Amount)).ToList());
        }).ToList();
        return new CashSessionExpectedDto(ciego, lineas, ciego ? null : esperado.Total);
    }

    // ------------------------------------------------------------------------------------------------ DTO --

    /// <summary>Las clases que cuentan como venta en <c>salesCount</c>/<c>salesTotal</c>: las del grupo de ventas que sacan mercancía.</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesDeVenta = ClasesDeDocumento.Todas
        .Where(d => d.Group == DocumentClassGroup.Sales && d.Effect == InventoryEffect.Exit).Select(d => d.Class).ToList();

    public async Task<CashSessionDto> DtoAsync(CashSession sesion, CancellationToken ct) => (await DtosAsync([sesion], ct))[0];

    /// <summary>Los <see cref="CashSessionDto"/> de varias sesiones, en su orden.</summary>
    public async Task<IReadOnlyList<CashSessionDto>> DtosAsync(IReadOnlyList<CashSession> sesiones, CancellationToken ct)
    {
        if (sesiones.Count == 0) return [];
        var puntoIds = sesiones.Select(s => s.PointOfSaleId).Distinct().ToList();
        var puntos = await db.PointsOfSale.AsNoTracking().IgnoreQueryFilters().Where(p => puntoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var cajaIds = sesiones.Select(s => s.CashRegisterId).Distinct().ToList();
        var cajas = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(c => cajaIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new ReferenciaDto(c.PublicId, c.Code, c.Name), ct);
        var usuarioIds = sesiones.Select(s => s.CashierUserId).Concat(sesiones.Select(s => s.ClosedByUserId).OfType<int>()).Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarioIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.PublicId, u.Username }, ct);
        var personaIds = sesiones.Select(s => s.CashierPersonId).OfType<int>().Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personaIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var sesionIds = sesiones.Select(s => s.Id).ToList();
        var clases = ClasesDeVenta.ToList();
        var ventas = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.CashSessionId != null && sesionIds.Contains(d.CashSessionId.Value) && d.Status == DocumentStatus.Confirmed && clases.Contains(d.Class))
            .GroupBy(d => d.CashSessionId!.Value)
            .Select(g => new { Sesion = g.Key, Cantidad = g.Count(), Total = g.Sum(d => d.AmountDue) })
            .ToDictionaryAsync(g => g.Sesion, ct);
        var diferencias = await (from c in db.CashCounts.AsNoTracking()
                                 join d in db.InventoryDocuments.AsNoTracking() on c.DifferenceDocumentId equals d.Id
                                 where sesionIds.Contains(c.CashSessionId) && !c.IsDeleted
                                 select new { c.CashSessionId, d.PublicId, d.Prefix, d.Number, d.Status })
            .ToDictionaryAsync(x => x.CashSessionId, ct);

        return sesiones.Select(s =>
        {
            var punto = puntos[s.PointOfSaleId];
            var cajero = usuarios.GetValueOrDefault(s.CashierUserId);
            var venta = ventas.GetValueOrDefault(s.Id);
            var diferencia = diferencias.GetValueOrDefault(s.Id);
            return new CashSessionDto(s.PublicId, CashSessionPointOfSaleDto.De(punto), cajas[s.CashRegisterId],
                new CashSessionCashierDto(cajero?.PublicId, s.CashierName, s.CashierPersonId is int per ? personas.GetValueOrDefault(per) : null),
                s.OperatingDate, s.OpenedAt, s.OpeningBase, s.BaseMode, s.Status, s.ClosedAt,
                s.ClosedByUserId is int cerro ? usuarios.GetValueOrDefault(cerro)?.Username : null,
                venta?.Cantidad ?? 0, venta?.Total ?? 0m,
                diferencia is null ? null : new CashDocumentRefDto(diferencia.PublicId, VistaDeDocumentos.NumeroVisible(diferencia.Prefix, diferencia.Number), diferencia.Status));
        }).ToList();
    }

    /// <summary>La referencia de un documento de caja (número visible y estado).</summary>
    public static CashDocumentRefDto Referencia(InventoryDocument documento, Guid? solicitud = null) =>
        new(documento.PublicId, VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number), documento.Status, solicitud);
}
