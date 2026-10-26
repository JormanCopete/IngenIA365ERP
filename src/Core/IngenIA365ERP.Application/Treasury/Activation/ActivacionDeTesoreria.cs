using System.Text.Json;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Activation;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Common.Cuadres;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Activation;

// Feature 015, T0183b (R-40; TR42; FR-147, FR-150; contracts/api.md §14; mensajes.md §7) y fase 19, T0303 (R-26, R-27; FR-150, FR-159): la activación de
// Tesorería, mínima (bloque 2, fuera de producción) o completa (el cuadre de la puesta en marcha, la chequera si gira cheques, el mes abierto y la matriz con
// Contabilidad iniciada). Registra sólo el destino TES × MessageDestination × Treasury (Tesorería no tiene fila ObligationsCollector, D2); si la cooperativa tiene
// vigente la fila del doble TES, la cierra antes por EscritorDelRegistroDeIntegracion.RetirarAsync (ValidTo, motivo en la auditoría) y registra la real.

/// <summary>Los requisitos de Tesorería: los básicos (activación mínima) y, con <c>completa</c>, los de la fase 19 (T0303). (nuevo)</summary>
public sealed class RequisitosDeActivacionDeTesoreria(IApplicationDbContext db, IContabilidadParaTesoreria contabilidad, IAmbienteDeDespliegue ambiente)
{
    public const string CuentaVinculada = "LinkedPaymentsAccount";
    public const string FormatoOPortal = "PaymentsFormatOrPortal";
    public const string Cuadre = "OpeningSquare";
    public const string Chequera = "CheckbookIfChecks";
    public const string MesAbierto = "OpenAccountingMonth";
    public const string MatrizCompleta = "AccountingMatrixComplete";

    /// <summary>Los requisitos básicos (la activación mínima, T0183b).</summary>
    public async Task<IReadOnlyList<RequisitoDeActivacionDto>> EvaluarAsync(DateOnly fecha, CancellationToken ct) =>
        (await EvaluarAsync(fecha, completa: false, corte: null, ofreceCheques: false, ct)).Requisitos;

    /// <summary>
    /// Los requisitos a la fecha; con <paramref name="completa"/>, también los de la activación completa y el cuadre que tomaría. <paramref name="ofreceCheques"/>: la
    /// cooperativa gira cheques (lo dice quien activa; FR-150 «chequera si ofrece cheques»).
    /// </summary>
    public async Task<(IReadOnlyList<RequisitoDeActivacionDto> Requisitos, CuadreParaActivar? Cuadre)> EvaluarAsync(DateOnly fecha, bool completa, DateOnly? corte,
        bool ofreceCheques, CancellationToken ct)
    {
        var cuentas = (await db.OwnBankAccounts.AsNoTracking().ToListAsync(ct))
            .Where(c => c.Activa(fecha) && c.Purposes.HasFlag(BankAccountPurpose.Payments)).ToList();
        var vinculada = false;
        foreach (var c in cuentas)
            if (await contabilidad.VinculoAsync(c.PublicId, ct) is not null) { vinculada = true; break; }
        var formato = await db.BankFileFormats.AsNoTracking()
            .AnyAsync(f => f.Scope == BankFileScope.TreasuryPayments && f.IsActive && f.ValidFrom <= fecha && (f.ValidTo == null || f.ValidTo >= fecha), ct);
        var lista = new List<RequisitoDeActivacionDto>
        {
            new(CuentaVinculada, vinculada, "Una cuenta bancaria propia de pagos, vigente y vinculada a su cuenta contable bancaria."),
            // La forma «portal del banco» siempre está disponible (FR-213): el formato es opcional y sólo se informa.
            new(FormatoOPortal, true, formato ? "Hay un formato de pagos vigente." : "Sin formato de pagos: los lotes van por la forma «portal del banco» (FR-213)."),
        };
        if (!completa)
        {
            if (ambiente.EsProduccion) lista.Add(new(Cuadre, false, "El cuadre de la puesta en marcha: en producción sólo hay activación completa."));
            return (lista, null);
        }

        // ---- la activación completa (fase 19, T0303) ----
        var cuadre = await CuadreAsync(db, corte, ct);
        lista.Add(new(Cuadre, cuadre is not null, cuadre is null
            ? "El cuadre de apertura de la puesta en marcha, definitivo o provisional aprobado (/tesoreria/puesta-en-marcha)."
            : $"Cuadre {(cuadre.Kind == OpeningSquareKind.Definitive ? "definitivo" : "provisional")} al {cuadre.CutoffDate:dd/MM/yyyy}."));
        var desde = (cuadre?.CutoffDate ?? corte ?? fecha.AddDays(-1)).AddDays(1);

        var idsDeCuentas = cuentas.Select(c => c.Id).ToList();
        var conChequera = await db.Checkbooks.AsNoTracking()
            .AnyAsync(k => idsDeCuentas.Contains(k.BankAccountId) && k.Status == CheckbookState.Active && k.NextNumber <= k.ToNumber, ct);
        lista.Add(new(Chequera, !ofreceCheques || conChequera, ofreceCheques
            ? "La cooperativa gira cheques: una chequera activa con cheques disponibles en una cuenta de pagos (/tesoreria/chequeras)."
            : "La cooperativa no gira cheques: la chequera no se exige."));

        var periodo = await contabilidad.EstadoDelPeriodoAsync(desde, ct);
        lista.Add(new(MesAbierto, periodo is null || !periodo.AccountingInitialized || periodo.State != EstadoDePeriodoContable.Closed,
            $"El mes contable del {desde:dd/MM/yyyy}, día en que rige la activación, está abierto (FR-159)."));

        var matriz = await contabilidad.CompletitudDeLaMatrizAsync(desde, ct);
        lista.Add(new(MatrizCompleta, matriz is null || matriz.Cumple, matriz is null || !matriz.AccountingInitialized
            ? "Sin Contabilidad iniciada la matriz contable no se exige."
            : matriz.NotPosted
                ? "Los hechos de Tesorería están en «no pasa»: la matriz no se exige."
                : $"La matriz contable de Tesorería al 100 % ({matriz.Percent:0.##} %, faltan {matriz.Missing}); la completa Contabilidad (/contabilidad/tesoreria/matriz)."));
        return (lista, cuadre);
    }

    /// <summary>El último cuadre de Tesorería que permite activar, de la fecha de corte pedida o, sin ella, el más reciente.</summary>
    public static async Task<CuadreParaActivar?> CuadreAsync(IApplicationDbContext db, DateOnly? corte, CancellationToken ct)
    {
        var estados = new[] { OpeningSquareState.Squared, OpeningSquareState.AcceptedWithMinutes, OpeningSquareState.ProvisionalApproved };
        var consulta = db.TreasuryOpeningSquares.AsNoTracking().Where(c => estados.Contains(c.Status));
        if (corte is { } d) consulta = consulta.Where(c => c.CutoffDate == d);
        return await consulta.OrderByDescending(c => c.CutoffDate).ThenByDescending(c => c.Id)
            .Select(c => new CuadreParaActivar(c.Id, c.PublicId, c.Kind, c.CutoffDate)).FirstOrDefaultAsync(ct);
    }
}

/// <summary><c>GET /api/treasury/activation/requirements?minimal=&amp;offersChecks=</c> (<c>Treasury.Activation.View</c>). (nuevo)</summary>
public sealed record GetTreasuryActivationRequirementsQuery(bool Minimal = false, DateOnly? CutoffDate = null, bool OffersChecks = false)
    : IRequest<Result<EstadoDeActivacionDto>>;

public sealed class GetTreasuryActivationRequirementsQueryHandler(IApplicationDbContext db, RequisitosDeActivacionDeTesoreria requisitos, IDateTimeService reloj)
    : IRequestHandler<GetTreasuryActivationRequirementsQuery, Result<EstadoDeActivacionDto>>
{
    public async Task<Result<EstadoDeActivacionDto>> Handle(GetTreasuryActivationRequirementsQuery request, CancellationToken ct)
    {
        var setup = await db.TreasurySetups.AsNoTracking().FirstAsync(ct);
        var ultima = await db.TreasuryActivations.AsNoTracking().OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        var (lista, cuadre) = await requisitos.EvaluarAsync(reloj.HoyLocal, !request.Minimal, request.CutoffDate, request.OffersChecks, ct);
        if (setup.Activo && ultima?.OpeningSquareId is { } usado)
            cuadre = await db.TreasuryOpeningSquares.AsNoTracking().Where(c => c.Id == usado)
                .Select(c => new CuadreParaActivar(c.Id, c.PublicId, c.Kind, c.CutoffDate)).FirstOrDefaultAsync(ct);
        return Result.Success(new EstadoDeActivacionDto(setup.Status.ToString(), setup.ActivatedOn, ultima?.IsMinimal ?? false,
            !setup.Activo && lista.All(r => r.Met), lista, cuadre?.PublicId, cuadre?.Kind.ToString(), setup.Activo ? ultima?.CutoffDate : cuadre?.CutoffDate));
    }
}

/// <summary>
/// <c>POST /api/treasury/activation</c> (<c>Treasury.Activation.Activate</c>): con los requisitos cumplidos deja <c>TES_Setup</c> activo (con
/// <c>Lotes.TerceraPersonaParaResultado</c> sellado), guarda <c>TES_Activations</c> con lo verificado y el cuadre (la completa, <c>IsMinimal = false</c>) y registra el
/// destino <c>TES × Treasury</c> con <c>OrdenDeGiroEmitida</c> v1 y v2, <c>AnulacionDeOrdenDeGiro</c> v1 y los dos del archivo de desembolsos de Cartera, cerrando
/// antes la fila del doble <c>TES</c> si está vigente. La mínima no existe en producción (<c>Treasury.Activation.MinimalNotAllowed</c>).
/// <c>Treasury.Activation.RequirementsMissing</c> con <c>data.missing[]</c> si falta algo. (nuevo)
/// </summary>
public sealed record ActivateTreasuryCommand(DateOnly? CutoffDate = null, bool Minimal = false, bool OffersChecks = false)
    : IRequest<Result<EstadoDeActivacionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ActivateTreasuryCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    RequisitosDeActivacionDeTesoreria requisitos,
    ParametrosDeTesoreria parametros,
    EscritorDelRegistroDeIntegracion registro,
    AuditoriaDeTesoreria auditoria,
    IAmbienteDeDespliegue ambiente,
    IDateTimeService reloj) : IRequestHandler<ActivateTreasuryCommand, Result<EstadoDeActivacionDto>>
{
    public const string CodigoDeDestino = "TES";
    public const string CodigoRequisitos = "Treasury.Activation.RequirementsMissing";
    public const string CodigoMinimaNoPermitida = "Treasury.Activation.MinimalNotAllowed";

    /// <summary>
    /// Las versiones que el destino <c>Treasury</c> adopta (FR-147): las órdenes de giro en sus dos versiones, su anulación y, con E-01 c, el archivo de desembolsos
    /// de Cartera. Tesorería nunca es recaudador (D2).
    /// </summary>
    public static IReadOnlyList<VersionAdoptada> VersionesDelDestino { get; } =
    [
        new(OrdenDeGiroEmitidaV1.Type, 1), new(OrdenDeGiroEmitidaV2.Type, 2), new(AnulacionDeOrdenDeGiroV1.Type, 1),
        new(ArchivoDeDesembolsosGeneradoV1.Type, 1), new(ArchivoDeDesembolsosAnuladoV1.Type, 1),
        // Feature 015, T2 (fase 41; E-05): la dispersión de Nómina.
        new(IngenIA365ERP.Application.Common.Integration.Contracts.Payroll.DispersionDeNominaGeneradaV1.Type, 1),
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<EstadoDeActivacionDto>> Handle(ActivateTreasuryCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<EstadoDeActivacionDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (request.Minimal && ambiente.EsProduccion)
            return Result.Failure<EstadoDeActivacionDto>(new Error(CodigoMinimaNoPermitida,
                "En producción Tesorería se activa con la activación completa: registre el cuadre de la puesta en marcha y actívela sin «mínima»."));
        await cerrojo.TomarSetupAsync(exclusivo: true, ct: ct);
        var setup = await db.TreasurySetups.FirstAsync(ct);
        if (setup.Activo) return Result.Failure<EstadoDeActivacionDto>(new Error("Treasury.Activation.AlreadyActive", "Tesorería ya está activada."));
        var hoy = reloj.HoyLocal;
        var (lista, cuadre) = await requisitos.EvaluarAsync(hoy, !request.Minimal, request.CutoffDate, request.OffersChecks, ct);
        var faltan = lista.Where(r => !r.Met).Select(r => r.Code).ToList();
        if (faltan.Count > 0)
            return Result.Failure<EstadoDeActivacionDto>(new ErrorConDatos(CodigoRequisitos, "Faltan requisitos para activar Tesorería: complételos y vuelva a intentarlo.",
                new { missing = faltan }));

        var corte = request.CutoffDate ?? cuadre?.CutoffDate ?? hoy.AddDays(-1);
        var desde = corte.AddDays(1);
        // La fila del doble TES vigente se cierra antes (mensajes.md §7): la real ocupa la misma llave lógica con una vigencia nueva.
        var doble = await db.IntegrationParticipants.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Code == CodigoDeDestino && p.Role == IntegrationParticipantRole.MessageDestination && p.Destination == IntegrationDestinations.Treasury
                        && p.ValidTo == null && p.IsTestDouble)
            .Select(p => new { p.PublicId, p.ValidFrom }).FirstOrDefaultAsync(ct);
        if (doble is not null)
        {
            var hasta = desde.AddDays(-1) < doble.ValidFrom ? doble.ValidFrom : desde.AddDays(-1);
            var retirado = await registro.RetirarAsync(doble.PublicId, hasta, ct);
            if (retirado.IsFailure) return Result.Failure<EstadoDeActivacionDto>(retirado.Error);
            await auditoria.AgregarAsync("Treasury.Activation.TestDoubleRetired", "IntegrationParticipant", doble.PublicId, null,
                new { code = CodigoDeDestino, validTo = hasta, reason = "Retirada al activar la Tesorería real" }, ct);
            await db.SaveChangesAsync(ct);
            if (hasta >= desde) desde = hasta.AddDays(1);
        }
        var destino = await registro.RegistrarAsync(new NuevoParticipante(CodigoDeDestino, IntegrationParticipantClass.Treasury,
            IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury, true, false, VersionesDelDestino, desde,
            request.Minimal ? "Activación mínima de Tesorería (bloque 2)" : "Activación de Tesorería"), ct);
        if (destino.IsFailure) return Result.Failure<EstadoDeActivacionDto>(destino.Error);

        setup.Status = TreasuryModuleStatus.Active;
        setup.ActivatedOn = desde;
        setup.ResultWithThirdPerson = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.LotesTerceraPersonaParaResultado, hoy, ct);
        db.TreasuryActivations.Add(new TreasuryActivation
        {
            ActivatedOn = desde, CutoffDate = corte, ActivatedByUserId = usuario, RequirementsJson = JsonSerializer.Serialize(lista, Json), IsMinimal = request.Minimal,
            OpeningSquareId = request.Minimal ? null : cuadre?.Id,
        });
        await auditoria.AgregarAsync("Treasury.Activated", "TreasurySetup", setup.PublicId, null,
            new
            {
                activatedOn = desde, cutoff = corte, minimal = request.Minimal, square = cuadre?.PublicId, squareKind = cuadre?.Kind.ToString(),
                offersChecks = request.OffersChecks, by = actor.Name,
            }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new EstadoDeActivacionDto(setup.Status.ToString(), setup.ActivatedOn, request.Minimal, false, lista,
            request.Minimal ? null : cuadre?.PublicId, request.Minimal ? null : cuadre?.Kind.ToString(), corte));
    }
}
