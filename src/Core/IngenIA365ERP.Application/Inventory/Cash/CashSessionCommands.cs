using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Sales.Cash;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Cash;

/// <summary>
/// Abre la sesión del usuario en una caja (<c>POST /api/inventory/cash-sessions</c>, <c>Inventory.CashSessions.Open</c>; feature 012, I3,
/// T617; contracts/api.md §21.1; T50). (nuevo)
/// </summary>
public sealed record OpenCashSessionCommand(Guid CashRegisterPublicId, decimal? OpeningBase = null, IReadOnlyList<DenominationCountInput>? Denominations = null)
    : IRequest<Result<OpenCashSessionResultDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }

    /// <summary>Se hace desde la caja (canal <c>pos</c>, T36, T562), pero la sesión todavía no existe: vacía.</summary>
    Guid IOperacionDePuntoDeVenta.CashSessionPublicId => Guid.Empty;
}

public sealed class OpenCashSessionCommandValidator : AbstractValidator<OpenCashSessionCommand>
{
    public OpenCashSessionCommandValidator()
    {
        RuleFor(x => x.CashRegisterPublicId).NotEmpty();
        RuleFor(x => x.OpeningBase).GreaterThanOrEqualTo(0).When(x => x.OpeningBase is not null);
        RuleForEach(x => x.Denominations).ChildRules(d => d.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0));
    }
}

/// <summary>
/// En orden, en una transacción:
/// <list type="number">
/// <item>la caja existe y está activa, y su punto en el alcance (si no, 404); el <b>candado de la fila del punto</b>
/// (<see cref="ClavesDeCerrojo.PuntoDeVenta"/>) serializa las aperturas del punto con su cierre del día;</item>
/// <item>la fecha operativa es <c>HoyLocal</c>; el día ya cerrado → <c>Inventory.CashSession.DayClosed</c>; la caja ocupada →
/// <c>RegisterBusy</c>; con <c>Caja.UnaSesionPorCajero</c>, el cajero ocupado → <c>CashierBusy</c> (una carrera que llega al índice se
/// traduce al mismo error, <see cref="ColisionesDeVenta"/>);</item>
/// <item>se <b>sellan</b> en la sesión <c>Caja.BaseModo</c> (por caja), <c>Caja.TratamientoFaltante</c> y <c>Caja.ArqueoCiego</c> (por
/// punto) y <c>Caja.UnaSesionPorCajero</c>; con el faltante a cargo del cajero, su usuario tiene persona
/// (<c>CashierWithoutPerson</c>); la persona es copia de <c>SEC_Users.PersonId</c>;</item>
/// <item>la base: con fondo fijo hereda el de la caja (el de su sesión anterior; en la primera, lo indicado) y avisa
/// <c>BaseDiffers</c> si lo indicado difiere; con base del día la base es obligatoria y se registra como un movimiento
/// <c>BaseIncome</c> que sigue la política de su tipo (cuenta en el esperado al confirmarse);</item>
/// <item>el aviso temprano de <see cref="GuardiaDeEmisionFiscal"/> con los tipos de venta de la caja: bloqueada, la sesión abre con
/// <c>ElectronicInvoicing.NotReady</c> (<c>data.missing[]</c>) y la venta fiscal se detendrá al cobrar.</item>
/// </list>
/// Deja el evento <c>Inventory.CashSession.Opened</c>. (nuevo)
/// </summary>
public sealed class OpenCashSessionCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IAlcanceDeInventario alcanceDeLaPeticion,
    ILectorDeParametros parametros,
    ICerrojoPorClave cerrojo,
    GuardiaDeEmisionFiscal guardia,
    SesionesDeCaja sesiones,
    ConfirmacionDeDocumento confirmacion,
    IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<OpenCashSessionCommand, Result<OpenCashSessionResultDto>>
{
    public Task<Result<OpenCashSessionResultDto>> Handle(OpenCashSessionCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var r = await AbrirAsync(request, ct);
            if (r.IsFailure) db.DescartarCambios();
            return r;
        }, ct);

    private async Task<Result<OpenCashSessionResultDto>> AbrirAsync(OpenCashSessionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        // (1) La caja, su punto y el candado del punto.
        var caja = await db.CashRegisters.AsNoTracking().Include(c => c.DocumentTypes.Where(t => !t.IsDeleted)).ThenInclude(t => t.DocumentType)
            .FirstOrDefaultAsync(c => c.PublicId == request.CashRegisterPublicId, ct);
        if (caja is null || !caja.IsActive) return Falla(ErroresDePuntoDeVenta.CashRegisterNotFound());
        var punto = await db.PointsOfSale.AsNoTracking().FirstAsync(p => p.Id == caja.PointOfSaleId, ct);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (!alcance.IncluyePunto(punto.Id) || !punto.IsActive) return Falla(ErroresDePuntoDeVenta.PointOfSaleNotFound());
        await cerrojo.BloquearAsync(ClavesDeCerrojo.PuntoDeVenta(punto.Id), ct);

        // (2) El día, la caja y el cajero.
        var fecha = reloj.HoyLocal;
        if (await ColisionesDeVenta.DiaCerradoAsync(db, punto.Id, fecha, ct) is not null) return Falla(ErroresDeCaja.DayClosed(fecha));
        if (await ColisionesDeVenta.CajaOcupadaAsync(db, caja.Id, ct) is { } ocupada) return Falla(ocupada);
        var exclusivo = await SiNoAsync(ParametrosDeInventario.CajaUnaSesionPorCajero, true, fecha, ParameterScopeKind.None, 0, ct);
        if (exclusivo && await ColisionesDeVenta.CajeroOcupadoAsync(db, usuario, ct) is { } cajero) return Falla(cajero);

        // (3) Lo que se sella.
        var modo = await TextoAsync(ParametrosDeInventario.CajaBaseModo, CashSession.BaseFondoFijo, fecha, ParameterScopeKind.CashRegister, caja.Id, ct);
        var faltante = await TextoAsync(ParametrosDeInventario.CajaTratamientoFaltante, EvaluadorDeArqueo.FaltanteAlGasto, fecha,
            ParameterScopeKind.PointOfSale, punto.Id, ct);
        var ciego = await SiNoAsync(ParametrosDeInventario.CajaArqueoCiego, false, fecha, ParameterScopeKind.PointOfSale, punto.Id, ct);
        var persona = await db.Users.AsNoTracking().Where(u => u.Id == usuario).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        if (faltante == EvaluadorDeArqueo.FaltanteACargoDelCajero && persona is null) return Falla(ErroresDeCaja.CashierWithoutPerson());

        // (4) La base.
        var avisos = new List<AvisoDto>();
        decimal baseDeApertura = 0m;
        var efectivo = await sesiones.EfectivoAsync(ct);
        if (modo == CashSession.BaseDelDia)
        {
            if (request.OpeningBase is not { } delDia || delDia <= 0m) return Falla(ErroresDeCaja.BaseRequired());
            if (efectivo is null) return Falla(ErroresDeCaja.CashMeansMissing());
        }
        else
        {
            var anterior = await db.CashSessions.AsNoTracking().Where(s => s.CashRegisterId == caja.Id)
                .OrderByDescending(s => s.Id).Select(s => (decimal?)s.OpeningBase).FirstOrDefaultAsync(ct);
            baseDeApertura = anterior ?? request.OpeningBase ?? 0m;
            if (request.OpeningBase is { } indicada && indicada != baseDeApertura) avisos.Add(ErroresDeCaja.BaseDiffers(indicada, baseDeApertura));
        }

        // (5) El aviso fiscal temprano.
        var tiposDeVenta = caja.DocumentTypes.Select(t => t.DocumentType).OfType<InventoryDocumentType>()
            .Where(t => ClasesDeDocumento.De(t.Class).Group == DocumentClassGroup.Sales).ToList();
        foreach (var tipo in tiposDeVenta)
        {
            var evaluacion = await guardia.EvaluarAsync(fecha, tipo, caja, ct);
            if (evaluacion.Veredicto != VeredictoFiscal.Blocked) continue;
            avisos.Add(ReglasDelDocumento.ComoAviso(ErroresDeVentas.NotReady(evaluacion)));
            break;
        }

        var sesion = new CashSession
        {
            CashRegisterId = caja.Id,
            PointOfSaleId = punto.Id,
            CashierUserId = usuario,
            CashierPersonId = persona,
            CashierName = actor.Name,
            OperatingDate = fecha,
            OpenedAt = reloj.UtcNow,
            LastActivityAt = reloj.UtcNow,
            BaseMode = modo,
            OpeningBase = baseDeApertura,
            ShortageTreatment = faltante,
            IsBlindCount = ciego,
            ExclusiveCashier = exclusivo,
        };
        db.CashSessions.Add(sesion);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ColisionesDeVenta.Es(ex))
        {
            var error = await ColisionesDeVenta.TraducirAsync(db, ex, new DatosDeLaColision(CashRegisterId: caja.Id, CashierUserId: usuario,
                PointOfSaleId: punto.Id, OperatingDate: fecha), ct);
            return Falla(error ?? throw ex);
        }

        // (6) La base del día como movimiento BaseIncome, por su política.
        CashDocumentRefDto? ingreso = null;
        if (modo == CashSession.BaseDelDia)
        {
            var tipo = await TiposDeCaja.PorDefectoAsync(db, DocumentClass.CashMovement, ct);
            if (tipo is null) return Falla(InventoryErrors.DocumentClassNotAvailable(DocumentClass.CashMovement));
            var documento = await MovimientosDelSistema.CrearAsync(db, tipo, sesion, punto.BranchId, usuario, fecha, CashMovementKind.BaseIncome,
                efectivo!.Id, CashMovementDestination.Safe, request.OpeningBase!.Value,
                await MovimientosDelSistema.DenominacionesAsync(db, request.Denominations, ct), "Base del día de la sesión", ct);
            sesion.BaseIncomeDocumentId = documento.Id;
            var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(documento.PublicId, DocumentClassGroup.Cash), ct);
            if (confirmada.IsFailure) return Falla(confirmada.Error);
            ingreso = SesionesDeCaja.Referencia(documento, confirmada.Value.Approval?.RequestPublicId);
            avisos.AddRange(confirmada.Value.Warnings);
        }

        await auditoria.AnotarAsync(AuditEventTypes.InventoryCashSessionOpened, request, sesion.PublicId,
            new { cashRegister = caja.Code, pointOfSale = punto.Code, sesion.OperatingDate, sesion.BaseMode, sesion.OpeningBase, sesion.ShortageTreatment,
                sesion.IsBlindCount, sesion.ExclusiveCashier }, ct, entidad: AuditoriaDelPuntoDeVenta.EntidadSesionDeCaja);
        await db.SaveChangesAsync(ct);
        return Result.Success(new OpenCashSessionResultDto(await sesiones.DtoAsync(sesion, ct), ingreso, avisos));
    }

    private async Task<string> TextoAsync(string clave, string defecto, DateOnly fecha, ParameterScopeKind ambito, int ambitoId, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, fecha, ambito, ambitoId, ct);
        return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto : defecto;
    }

    private async Task<bool> SiNoAsync(string clave, bool defecto, DateOnly fecha, ParameterScopeKind ambito, int ambitoId, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, fecha, ambito, ambitoId, ct);
        return leido.IsSuccess && leido.Value.Valor is bool b ? b : defecto;
    }

    private static Result<OpenCashSessionResultDto> Falla(Error error) => Result.Failure<OpenCashSessionResultDto>(error);
}

/// <summary>
/// Los movimientos que la caja crea por sí misma (feature 012, I3, T617, T618): el ingreso de la base del día al abrir y el retiro de
/// cierre. Son documentos del grupo <c>Cash</c> como cualquier otro —su tipo, su consecutivo y su política— y se confirman por el flujo
/// canónico. (nuevo)
/// </summary>
public static class MovimientosDelSistema
{
    /// <summary>Crea y guarda el borrador del movimiento con su satélite; lo confirma quien llama.</summary>
    public static async Task<InventoryDocument> CrearAsync(IApplicationDbContext db, InventoryDocumentType tipo, CashSession sesion, int sucursal,
        int usuario, DateOnly fecha, CashMovementKind clase, int medio, CashMovementDestination? destino, decimal valor, string? denominaciones,
        string motivo, CancellationToken ct)
    {
        var documento = new InventoryDocument
        {
            Class = DocumentClass.CashMovement,
            DocumentTypeId = tipo.Id,
            DocumentType = tipo,
            OperationDate = fecha,
            CreatedByUserId = usuario,
            BranchId = sucursal,
            PointOfSaleId = sesion.PointOfSaleId,
            CashRegisterId = sesion.CashRegisterId,
            CashSessionId = sesion.Id,
            Currency = InventoryDocument.MonedaPorDefecto,
            ExchangeRate = 1m,
            Reason = motivo,
            Subtotal = valor,
            Total = valor,
            AmountDue = valor,
        };
        db.InventoryDocuments.Add(documento);
        db.CashMovementDetails.Add(new CashMovementDetail
        {
            Document = documento,
            CashSessionId = sesion.Id,
            Kind = clase,
            SourcePaymentMeansId = medio,
            Destination = destino,
            Amount = valor,
            DenominationsJson = denominaciones,
        });
        await db.SaveChangesAsync(ct);
        return documento;
    }

    /// <summary>Las denominaciones como dato (<c>[{ denominationPublicId, quantity, amount }]</c>), o nulo.</summary>
    public static async Task<string?> DenominacionesAsync(IApplicationDbContext db, IReadOnlyList<DenominationCountInput>? pedidas, CancellationToken ct)
    {
        if (pedidas is not { Count: > 0 }) return null;
        var ids = pedidas.Select(d => d.CashDenominationPublicId).Distinct().ToList();
        var valores = await db.CashDenominations.AsNoTracking().Where(d => ids.Contains(d.PublicId)).ToDictionaryAsync(d => d.PublicId, d => d.Value, ct);
        return JsonSerializer.Serialize(pedidas.Where(d => valores.ContainsKey(d.CashDenominationPublicId)).Select(d => new
        {
            denominationPublicId = d.CashDenominationPublicId, quantity = d.Quantity, amount = valores[d.CashDenominationPublicId] * d.Quantity,
        }));
    }
}

// ------------------------------------------------------------------------------------------------ consultas --

/// <summary>
/// <c>GET /api/inventory/cash-sessions</c> (§21.1): paginado de las sesiones que quien pregunta ve —de su alcance de puntos y, sin
/// <c>Inventory.CashSessions.ViewAll</c>, sólo las suyas—. <c>mine=true&amp;status=Open</c> es la sesión abierta del usuario, con la que
/// arranca el POS. (nuevo)
/// </summary>
public sealed record ListCashSessionsQuery(
    bool? Mine = null,
    Guid? PointOfSalePublicId = null,
    Guid? CashRegisterPublicId = null,
    CashSessionStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? CashierUserPublicId = null,
    PageRequest? Pagina = null) : IRequest<Result<PagedResult<CashSessionDto>>>;

public sealed class ListCashSessionsQueryHandler(IApplicationDbContext db, SesionesDeCaja sesiones)
    : IRequestHandler<ListCashSessionsQuery, Result<PagedResult<CashSessionDto>>>
{
    public async Task<Result<PagedResult<CashSessionDto>>> Handle(ListCashSessionsQuery request, CancellationToken ct)
    {
        var q = await sesiones.VisiblesAsync(ct);
        if (request.Mine == true)
        {
            var yo = await sesiones.UsuarioAsync(ct) ?? -1;
            q = q.Where(s => s.CashierUserId == yo);
        }
        if (request.PointOfSalePublicId is { } p)
        {
            var id = await db.PointsOfSale.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
            q = q.Where(s => s.PointOfSaleId == id);
        }
        if (request.CashRegisterPublicId is { } c)
        {
            var id = await db.CashRegisters.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
            q = q.Where(s => s.CashRegisterId == id);
        }
        if (request.CashierUserPublicId is { } u)
        {
            var id = await db.Users.AsNoTracking().Where(x => x.PublicId == u).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
            q = q.Where(s => s.CashierUserId == id);
        }
        if (request.Status is { } st) q = q.Where(s => s.Status == st);
        if (request.From is { } desde) q = q.Where(s => s.OperatingDate >= desde);
        if (request.To is { } hasta) q = q.Where(s => s.OperatingDate <= hasta);

        var pagina = request.Pagina ?? new PageRequest();
        var total = await q.LongCountAsync(ct);
        var filas = await q.OrderByDescending(s => s.OpenedAt).ThenByDescending(s => s.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<CashSessionDto>(await sesiones.DtosAsync(filas, ct), pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>
/// <c>GET /api/inventory/cash-sessions/{id}</c> (§21.1): la sesión con sus movimientos, las ventas suspendidas y los borradores abiertos, y
/// su arqueo si ya se cerró. Ajena sin <c>ViewAll</c> o fuera del alcance: el mismo 404 que la inexistente. (nuevo)
/// </summary>
public sealed record GetCashSessionQuery(Guid CashSessionPublicId) : IRequest<Result<CashSessionDetailDto>>;

public sealed class GetCashSessionQueryHandler(IApplicationDbContext db, SesionesDeCaja sesiones)
    : IRequestHandler<GetCashSessionQuery, Result<CashSessionDetailDto>>
{
    public async Task<Result<CashSessionDetailDto>> Handle(GetCashSessionQuery request, CancellationToken ct)
    {
        var sesion = await sesiones.VisibleAsync(request.CashSessionPublicId, seguir: false, ct);
        if (sesion is null) return Result.Failure<CashSessionDetailDto>(ErroresDeCaja.SessionNotFound());

        var movimientos = await (from m in db.CashMovementDetails.AsNoTracking()
                                 join d in db.InventoryDocuments.AsNoTracking() on m.DocumentId equals d.Id
                                 where !m.IsDeleted && (m.CashSessionId == sesion.Id || m.DestinationCashSessionId == sesion.Id)
                                 orderby d.Id
                                 select new { m, d }).ToListAsync(ct);
        var borradores = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.CashSessionId == sesion.Id && d.Status == DocumentStatus.Draft && d.Class != DocumentClass.CashMovement
                        && d.Class != DocumentClass.CashCountDifference)
            .Select(d => d.IsSuspended).ToListAsync(ct);
        var arqueo = await (from l in db.CashCountLines.AsNoTracking()
                            join c in db.CashCounts.AsNoTracking() on l.CashCountId equals c.Id
                            join m in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters() on l.PaymentMeansId equals m.Id
                            where c.CashSessionId == sesion.Id && !c.IsDeleted && !l.IsDeleted
                            orderby m.DisplayOrder, m.Code
                            select new CashCountLineDto(m.Code, l.ExpectedAmount, l.CountedAmount, l.DifferenceAmount, l.ToleranceAmount, l.WithinTolerance, l.Treatment))
            .ToListAsync(ct);

        // pendingDeliveries: la entrega pendiente del documento electrónico llega con I4 (US8); en I3 no hay ninguna.
        return Result.Success(new CashSessionDetailDto(await sesiones.DtoAsync(sesion, ct),
            await VistaDeMovimientosDeCaja.DtosAsync(db, movimientos.Select(x => (x.m, x.d)).ToList(), ct),
            borradores.Count(s => s), borradores.Count(s => !s), 0, arqueo.Count == 0 ? null : arqueo));
    }
}
