using System.Text.Json;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Common.Cuadres;
using IngenIA365ERP.Domain.Entities.Receipts.Activation;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Activation;

// Feature 015, T0183b (R-40; TR42) y fase 19, T0303 (R-26, R-27; FR-147, FR-150, FR-159, FR-176, FR-185; contracts/api.md §14; mensajes.md §7): la activación de
// Recaudo. La mínima del bloque 2 (sólo los requisitos básicos, sin cuadre, IsMinimal = true) sigue disponible fuera de producción para el paralelo de QA; la
// completa exige además el cuadre de la puesta en marcha (definitivo o provisional aprobado), el mes contable abierto, la matriz contable completa con
// Contabilidad iniciada, un cajero y un supervisor con los permisos de FR-176 y la hora de corte antes del ciclo de Cartera. Las dos dejan RCD_Setup activo,
// guardan RCD_Activations y registran en el mismo guardado al recaudador REC y su destino Receipts.

/// <summary>Un requisito de la activación. (nuevo)</summary>
public sealed record RequisitoDeActivacionDto(string Code, bool Met, string Description);

/// <summary>
/// Los requisitos de la activación de un módulo y si se puede activar. Desde la fase 19 lleva el cuadre que la activación tomaría (o tomó): su
/// <c>PublicId</c>, su clase (<c>Definitive</c> / <c>Provisional</c>) y su fecha de corte. (nuevo)
/// </summary>
public sealed record EstadoDeActivacionDto(
    string Status,
    DateOnly? ActivatedOn,
    bool IsMinimal,
    bool CanActivate,
    IReadOnlyList<RequisitoDeActivacionDto> Requirements,
    Guid? OpeningSquarePublicId = null,
    string? OpeningSquareKind = null,
    DateOnly? CutoffDate = null);

/// <summary>El cuadre de apertura que la activación completa toma: el último aprobado de su fecha de corte. (nuevo)</summary>
public sealed record CuadreParaActivar(int Id, Guid PublicId, OpeningSquareKind Kind, DateOnly CutoffDate);

/// <summary>Los requisitos de Recaudo: los básicos (activación mínima) y, con <c>completa</c>, los de la fase 19. (nuevo)</summary>
public sealed class RequisitosDeActivacionDeRecaudo(
    IApplicationDbContext db,
    IAmbienteDeDespliegue ambiente,
    IContabilidadParaRecaudo contabilidad,
    IAutoridadDeOtroAprobador permisos,
    ParametrosDeCaja parametros,
    IEnumerable<IReglasDeParametrosDeModulo> reglas)
{
    public const string Punto = "CollectionPoint";
    public const string CajaConCajero = "CashBoxWithCashier";
    public const string Efectivo = "CashMeans";
    public const string Transferencia = "TransferMeans";
    public const string TiposDeDocumento = "DocumentTypes";
    public const string Cuadre = "OpeningSquare";
    public const string CajeroConPermisos = "CashierWithPermissions";
    public const string SupervisorConPermisos = "SupervisorWithPermissions";
    public const string MesAbierto = "OpenAccountingMonth";
    public const string MatrizCompleta = "AccountingMatrixComplete";
    public const string HoraDeCorte = "CutoffBeforeLendingCycle";

    /// <summary>Lo que un cajero necesita para registrar recibos (FR-176; plantilla <c>recaudo.cajero</c>).</summary>
    public static readonly IReadOnlyList<string> PermisosDelCajero = ["Receipts.Session.Open", "Receipts.Receipt.Register"];

    /// <summary>Lo que un supervisor necesita para aprobar diferencias y cerrar el día (FR-176; plantilla <c>recaudo.supervisor</c>).</summary>
    public static readonly IReadOnlyList<string> PermisosDelSupervisor = ["Receipts.Count.ApproveDifference", "Receipts.Day.Close", "Receipts.Voiding.Approve"];

    /// <summary>Los estados de un cuadre que permiten activar: cuadrado, aceptado con acta o provisional aprobado (FR-158, FR-228).</summary>
    public static readonly IReadOnlySet<OpeningSquareState> EstadosQueActivan =
        new HashSet<OpeningSquareState> { OpeningSquareState.Squared, OpeningSquareState.AcceptedWithMinutes, OpeningSquareState.ProvisionalApproved };

    /// <summary>Los requisitos básicos (la activación mínima, T0183b).</summary>
    public async Task<IReadOnlyList<RequisitoDeActivacionDto>> EvaluarAsync(DateOnly fecha, CancellationToken ct) =>
        (await EvaluarAsync(fecha, completa: false, corte: null, ct)).Requisitos;

    /// <summary>Los requisitos a la fecha; con <paramref name="completa"/>, también los de la activación completa y el cuadre que tomaría.</summary>
    public async Task<(IReadOnlyList<RequisitoDeActivacionDto> Requisitos, CuadreParaActivar? Cuadre)> EvaluarAsync(DateOnly fecha, bool completa, DateOnly? corte,
        CancellationToken ct)
    {
        var puntos = (await db.CollectionPoints.AsNoTracking().ToListAsync(ct)).Where(p => p.VigenteEn(fecha)).Select(p => p.Id).ToList();
        var cajas = (await db.CashBoxes.AsNoTracking().Where(c => puntos.Contains(c.CollectionPointId)).ToListAsync(ct)).Where(c => c.VigenteEn(fecha)).Select(c => c.Id).ToList();
        var cajeros = (await db.CashBoxCashiers.AsNoTracking().Where(c => cajas.Contains(c.CashBoxId)).ToListAsync(ct)).Where(c => c.VigenteEn(fecha))
            .Select(c => c.UserId).Distinct().ToList();
        var ofrecidos = (await db.PaymentMeansCollectionPoints.AsNoTracking().Where(o => puntos.Contains(o.CollectionPointId)).ToListAsync(ct))
            .Where(o => o.VigenteEn(fecha)).Select(o => o.PaymentMeansId).ToList();
        var clases = await db.PaymentMeans.AsNoTracking().Where(m => ofrecidos.Contains(m.Id) && m.IsActive).Select(m => m.Class).Distinct().ToListAsync(ct);
        var tipos = await db.ReceiptDocumentTypes.AsNoTracking().Where(t => t.Status == ReceiptsCatalogStatus.Active).Select(t => t.Kind).Distinct().ToListAsync(ct);
        var lista = new List<RequisitoDeActivacionDto>
        {
            new(Punto, puntos.Count > 0, "Un punto de recaudo vigente."),
            new(CajaConCajero, cajeros.Count > 0, "Una caja vigente con un cajero asignado."),
            new(Efectivo, clases.Contains(PaymentMeansClass.Cash), "El medio efectivo ofrecido en un punto."),
            new(Transferencia, clases.Contains(PaymentMeansClass.Transfer), "El medio transferencia ofrecido en un punto."),
            new(TiposDeDocumento, tipos.Contains(ReceiptDocumentKind.CashReceipt) && tipos.Contains(ReceiptDocumentKind.Voiding),
                "Los tipos de documento del recibo de caja y de la anulación."),
        };
        if (!completa)
        {
            // En producción no hay activación mínima: la completa exige el cuadre de la puesta en marcha.
            if (ambiente.EsProduccion) lista.Add(new(Cuadre, false, "El cuadre de la puesta en marcha: en producción sólo hay activación completa."));
            return (lista, null);
        }

        // ---- la activación completa (fase 19, T0303) ----
        var cuadre = await CuadreAsync(db, corte, ct);
        lista.Add(new(Cuadre, cuadre is not null, cuadre is null
            ? "El cuadre de apertura de la puesta en marcha, definitivo o provisional aprobado (/recaudo/puesta-en-marcha)."
            : $"Cuadre {(cuadre.Kind == OpeningSquareKind.Definitive ? "definitivo" : "provisional")} al {cuadre.CutoffDate:dd/MM/yyyy}."));
        var desde = (cuadre?.CutoffDate ?? corte ?? fecha.AddDays(-1)).AddDays(1);

        var cajeroConPermisos = false;
        foreach (var usuario in cajeros)
            if (await TodosAsync(usuario, PermisosDelCajero, ct)) { cajeroConPermisos = true; break; }
        lista.Add(new(CajeroConPermisos, cajeroConPermisos, $"Un cajero asignado con {string.Join(", ", PermisosDelCajero)} (FR-176)."));

        var supervisor = false;
        var usuarios = await db.Users.AsNoTracking().Where(u => u.IsActive && !u.IsSaasOperator).Select(u => u.Id).ToListAsync(ct);
        foreach (var usuario in usuarios)
            if (await TodosAsync(usuario, PermisosDelSupervisor, ct)) { supervisor = true; break; }
        lista.Add(new(SupervisorConPermisos, supervisor, $"Un supervisor de caja con {string.Join(", ", PermisosDelSupervisor)} (FR-176)."));

        var periodo = await contabilidad.EstadoDelPeriodoAsync(desde, ct);
        lista.Add(new(MesAbierto, periodo is null || !periodo.AccountingInitialized || periodo.State != EstadoDePeriodoContable.Closed,
            $"El mes contable del {desde:dd/MM/yyyy}, día en que rige la activación, está abierto (FR-159)."));

        var matriz = await contabilidad.CompletitudDeLaMatrizAsync(desde, ct);
        lista.Add(new(MatrizCompleta, matriz is null || matriz.Cumple, matriz is null || !matriz.AccountingInitialized
            ? "Sin Contabilidad iniciada la matriz contable no se exige."
            : matriz.NotPosted
                ? "Los hechos de Recaudo están en «no pasa»: la matriz no se exige."
                : $"La matriz contable de Recaudo al 100 % ({matriz.Percent:0.##} %, faltan {matriz.Missing}); la completa Contabilidad (/contabilidad/recaudo/matriz)."));

        var hora = await parametros.HoraAsync(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, fecha, ct);
        var inicio = reglas.OfType<ReglasDeParametrosDeRecaudo>().Select(r => (TimeOnly?)r.InicioDelCicloDeCartera).FirstOrDefault()
                     ?? ReglasDeParametrosDeRecaudo.InicioPorDefecto;
        lista.Add(new(HoraDeCorte, ReglasDeParametrosDeRecaudo.CorteValido(hora, inicio),
            $"La hora de corte ({hora:HH\\:mm}) cae antes del ciclo diario de Cartera ({inicio:HH\\:mm}) (FR-185)."));
        return (lista, cuadre);
    }

    private async Task<bool> TodosAsync(int usuario, IReadOnlyList<string> requeridos, CancellationToken ct)
    {
        foreach (var p in requeridos)
            if (!await permisos.TienePermisoAsync(usuario, p, ct)) return false;
        return true;
    }

    /// <summary>El último cuadre que permite activar, de la fecha de corte pedida o, sin ella, el más reciente.</summary>
    public static async Task<CuadreParaActivar?> CuadreAsync(IApplicationDbContext db, DateOnly? corte, CancellationToken ct)
    {
        var estados = EstadosQueActivan.ToList();
        var consulta = db.ReceiptsOpeningSquares.AsNoTracking().Where(c => estados.Contains(c.Status));
        if (corte is { } d) consulta = consulta.Where(c => c.CutoffDate == d);
        return await consulta.OrderByDescending(c => c.CutoffDate).ThenByDescending(c => c.Id)
            .Select(c => new CuadreParaActivar(c.Id, c.PublicId, c.Kind, c.CutoffDate)).FirstOrDefaultAsync(ct);
    }
}

/// <summary>
/// <c>GET /api/receipts/activation/requirements?minimal=</c> (<c>Receipts.Activation.View</c>): los requisitos de la activación completa (o, con
/// <c>minimal=true</c>, los de la mínima). (nuevo)
/// </summary>
public sealed record GetReceiptsActivationRequirementsQuery(bool Minimal = false, DateOnly? CutoffDate = null) : IRequest<Result<EstadoDeActivacionDto>>;

public sealed class GetReceiptsActivationRequirementsQueryHandler(IApplicationDbContext db, RequisitosDeActivacionDeRecaudo requisitos, IDateTimeService reloj)
    : IRequestHandler<GetReceiptsActivationRequirementsQuery, Result<EstadoDeActivacionDto>>
{
    public async Task<Result<EstadoDeActivacionDto>> Handle(GetReceiptsActivationRequirementsQuery request, CancellationToken ct)
    {
        var setup = await db.ReceiptsSetups.AsNoTracking().FirstAsync(ct);
        var ultima = await db.ReceiptsActivations.AsNoTracking().OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        var (lista, cuadre) = await requisitos.EvaluarAsync(reloj.HoyLocal, !request.Minimal, request.CutoffDate, ct);
        if (setup.Activo && ultima?.OpeningSquareId is { } usado)
            cuadre = await db.ReceiptsOpeningSquares.AsNoTracking().Where(c => c.Id == usado)
                .Select(c => new CuadreParaActivar(c.Id, c.PublicId, c.Kind, c.CutoffDate)).FirstOrDefaultAsync(ct);
        return Result.Success(new EstadoDeActivacionDto(setup.Status.ToString(), setup.ActivatedOn, ultima?.IsMinimal ?? false,
            !setup.Activo && lista.All(r => r.Met), lista, cuadre?.PublicId, cuadre?.Kind.ToString(), setup.Activo ? ultima?.CutoffDate : cuadre?.CutoffDate));
    }
}

/// <summary>
/// <c>POST /api/receipts/activation</c> (<c>Receipts.Activation.Activate</c>): con los requisitos cumplidos deja <c>RCD_Setup</c> activo (la hora de corte sellada de
/// <c>Recaudo.HoraDeCorte</c>), guarda <c>RCD_Activations</c> con lo verificado y registra por <see cref="EscritorDelRegistroDeIntegracion"/> en el mismo guardado
/// <c>REC × ObligationsCollector</c> (<c>ProveedorDeObligaciones</c> y <c>ProveedorDeCuentas</c> v1) y <c>REC × MessageDestination × Receipts</c>
/// (<c>PagoReversadoPorCartera</c> v1, <c>ConsignacionReversadaPorCartera</c> v1, <c>DescuentosPorPagaduria</c> v1 y v2). Rige desde el día siguiente al corte
/// (FR-159). La <b>completa</b> (por defecto) toma el cuadre (<c>OpeningSquareId</c>, <c>IsMinimal = false</c>); la corta del corte sale del cuadre si no viene. La
/// <b>mínima</b> (<see cref="Minimal"/>) no existe en producción (<c>Receipts.Activation.MinimalNotAllowed</c>). Sin un requisito,
/// <c>Receipts.Activation.RequirementsMissing</c> con <c>data.missing[]</c>. (nuevo)
/// </summary>
public sealed record ActivateReceiptsCommand(DateOnly? CutoffDate = null, bool Minimal = false) : IRequest<Result<EstadoDeActivacionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ActivateReceiptsCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    RequisitosDeActivacionDeRecaudo requisitos,
    ParametrosDeCaja parametros,
    EscritorDelRegistroDeIntegracion registro,
    AuditoriaDeRecaudo auditoria,
    IAmbienteDeDespliegue ambiente,
    IDateTimeService reloj) : IRequestHandler<ActivateReceiptsCommand, Result<EstadoDeActivacionDto>>
{
    public const string CodigoMinimaNoPermitida = "Receipts.Activation.MinimalNotAllowed";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Las versiones que el destino <c>Receipts</c> adopta (FR-147).</summary>
    public static IReadOnlyList<VersionAdoptada> VersionesDelDestino { get; } =
    [
        new(PagoReversadoPorCarteraV1.Type, 1), new(ConsignacionReversadaPorCarteraV1.Type, 1),
        new(DescuentosPorPagaduriaV1.Type, 1), new(DescuentosPorPagaduriaV1.Type, 2),
    ];

    /// <summary>Las versiones que el recaudador <c>REC</c> adopta (FR-147).</summary>
    public static IReadOnlyList<VersionAdoptada> VersionesDelRecaudador { get; } =
        [new(ContratosDeIntegracion.ProveedorDeObligaciones, 1), new(ContratosDeIntegracion.ProveedorDeCuentas, 1)];

    public async Task<Result<EstadoDeActivacionDto>> Handle(ActivateReceiptsCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<EstadoDeActivacionDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        if (request.Minimal && ambiente.EsProduccion)
            return Result.Failure<EstadoDeActivacionDto>(new Error(CodigoMinimaNoPermitida,
                "En producción Recaudo se activa con la activación completa: registre el cuadre de la puesta en marcha y actívelo sin «mínima»."));
        await cerrojo.TomarSetupAsync(exclusivo: true, ct: ct);
        var setup = await db.ReceiptsSetups.FirstAsync(ct);
        if (setup.Activo) return Result.Failure<EstadoDeActivacionDto>(ErroresDeRecaudo.YaActivado());
        var hoy = reloj.HoyLocal;
        var (lista, cuadre) = await requisitos.EvaluarAsync(hoy, !request.Minimal, request.CutoffDate, ct);
        var faltan = lista.Where(r => !r.Met).Select(r => r.Code).ToList();
        if (faltan.Count > 0) return Result.Failure<EstadoDeActivacionDto>(ErroresDeRecaudo.RequisitosDeActivacion(faltan));

        var corte = request.CutoffDate ?? cuadre?.CutoffDate ?? hoy.AddDays(-1);
        var desde = corte.AddDays(1);
        var motivo = request.Minimal ? "Activación mínima de Recaudo (bloque 2)" : "Activación de Recaudo";
        var recaudador = await registro.RegistrarAsync(new NuevoParticipante(ClavesDeRecaudo.Codigo, IntegrationParticipantClass.Receipts,
            IntegrationParticipantRole.ObligationsCollector, null, true, false, VersionesDelRecaudador, desde, motivo), ct);
        if (recaudador.IsFailure) return Result.Failure<EstadoDeActivacionDto>(recaudador.Error);
        var destino = await registro.RegistrarAsync(new NuevoParticipante(ClavesDeRecaudo.Codigo, IntegrationParticipantClass.Receipts,
            IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Receipts, true, false, VersionesDelDestino, desde, motivo), ct);
        if (destino.IsFailure) return Result.Failure<EstadoDeActivacionDto>(destino.Error);

        setup.Status = ReceiptsModuleStatus.Active;
        setup.ActivatedOn = desde;
        setup.CutoffTime = await parametros.HoraAsync(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, hoy, ct);
        db.ReceiptsActivations.Add(new ReceiptsActivation
        {
            ActivatedOn = desde, CutoffDate = corte, ActivatedByUserId = usuario, RequirementsJson = JsonSerializer.Serialize(lista, Json), IsMinimal = request.Minimal,
            OpeningSquareId = request.Minimal ? null : cuadre?.Id,
        });
        await auditoria.AgregarAsync("Receipts.Activated", "ReceiptsSetup", setup.PublicId, null,
            new { activatedOn = desde, cutoff = corte, minimal = request.Minimal, square = cuadre?.PublicId, squareKind = cuadre?.Kind.ToString(), by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new EstadoDeActivacionDto(setup.Status.ToString(), setup.ActivatedOn, request.Minimal, false, lista,
            request.Minimal ? null : cuadre?.PublicId, request.Minimal ? null : cuadre?.Kind.ToString(), corte));
    }
}
