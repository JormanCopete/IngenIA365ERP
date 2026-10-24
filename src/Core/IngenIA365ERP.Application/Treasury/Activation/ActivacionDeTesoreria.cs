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
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Activation;

// Feature 015, T0183b (R-40; TR42; FR-147, FR-150; contracts/api.md §14; mensajes.md §7): la activación mínima de Tesorería del bloque 2. Registra sólo el destino
// TES × MessageDestination × Treasury (Tesorería no tiene fila ObligationsCollector, D2); si la cooperativa tiene vigente la fila del doble TES, la cierra antes
// por EscritorDelRegistroDeIntegracion.RetirarAsync (ValidTo, motivo en la auditoría) y registra la real.

/// <summary>Los requisitos básicos de Tesorería (activación mínima). (nuevo)</summary>
public sealed class RequisitosDeActivacionDeTesoreria(IApplicationDbContext db, IContabilidadParaTesoreria contabilidad, IAmbienteDeDespliegue ambiente)
{
    public const string CuentaVinculada = "LinkedPaymentsAccount";
    public const string FormatoOPortal = "PaymentsFormatOrPortal";
    public const string Cuadre = "OpeningSquare";

    public async Task<IReadOnlyList<RequisitoDeActivacionDto>> EvaluarAsync(DateOnly fecha, CancellationToken ct)
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
        if (ambiente.EsProduccion) lista.Add(new(Cuadre, false, "El cuadre definitivo de la puesta en marcha (se habilita con la activación completa)."));
        return lista;
    }
}

/// <summary><c>GET /api/treasury/activation/requirements</c> (<c>Treasury.Activation.View</c>). (nuevo)</summary>
public sealed record GetTreasuryActivationRequirementsQuery : IRequest<Result<EstadoDeActivacionDto>>;

public sealed class GetTreasuryActivationRequirementsQueryHandler(IApplicationDbContext db, RequisitosDeActivacionDeTesoreria requisitos, IDateTimeService reloj)
    : IRequestHandler<GetTreasuryActivationRequirementsQuery, Result<EstadoDeActivacionDto>>
{
    public async Task<Result<EstadoDeActivacionDto>> Handle(GetTreasuryActivationRequirementsQuery request, CancellationToken ct)
    {
        var setup = await db.TreasurySetups.AsNoTracking().FirstAsync(ct);
        var ultima = await db.TreasuryActivations.AsNoTracking().OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        var lista = await requisitos.EvaluarAsync(reloj.HoyLocal, ct);
        return Result.Success(new EstadoDeActivacionDto(setup.Status.ToString(), setup.ActivatedOn, ultima?.IsMinimal ?? false,
            !setup.Activo && lista.All(r => r.Met), lista));
    }
}

/// <summary>
/// <c>POST /api/treasury/activation</c> (<c>Treasury.Activation.Activate</c>): con los requisitos básicos, deja <c>TES_Setup</c> activo (con
/// <c>Lotes.TerceraPersonaParaResultado</c> sellado), guarda <c>TES_Activations</c> con <c>IsMinimal = true</c> y registra el destino <c>TES × Treasury</c> con
/// <c>OrdenDeGiroEmitida</c> v1 y v2 y <c>AnulacionDeOrdenDeGiro</c> v1, cerrando antes la fila del doble <c>TES</c> si está vigente.
/// <c>Treasury.Activation.RequirementsMissing</c> con <c>data.missing[]</c> si falta algo. (nuevo)
/// </summary>
public sealed record ActivateTreasuryCommand(DateOnly? CutoffDate = null) : IRequest<Result<EstadoDeActivacionDto>>, IOperacionIdempotente
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
    IDateTimeService reloj) : IRequestHandler<ActivateTreasuryCommand, Result<EstadoDeActivacionDto>>
{
    public const string CodigoDeDestino = "TES";
    public const string CodigoRequisitos = "Treasury.Activation.RequirementsMissing";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<EstadoDeActivacionDto>> Handle(ActivateTreasuryCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<EstadoDeActivacionDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        await cerrojo.TomarSetupAsync(exclusivo: true, ct: ct);
        var setup = await db.TreasurySetups.FirstAsync(ct);
        if (setup.Activo) return Result.Failure<EstadoDeActivacionDto>(new Error("Treasury.Activation.AlreadyActive", "Tesorería ya está activada."));
        var hoy = reloj.HoyLocal;
        var lista = await requisitos.EvaluarAsync(hoy, ct);
        var faltan = lista.Where(r => !r.Met).Select(r => r.Code).ToList();
        if (faltan.Count > 0)
            return Result.Failure<EstadoDeActivacionDto>(new ErrorConDatos(CodigoRequisitos, "Faltan requisitos para activar Tesorería: complételos y vuelva a intentarlo.",
                new { missing = faltan }));

        var corte = request.CutoffDate ?? hoy.AddDays(-1);
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
            IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury, true, false,
            [new VersionAdoptada(OrdenDeGiroEmitidaV1.Type, 1), new VersionAdoptada(OrdenDeGiroEmitidaV2.Type, 2), new VersionAdoptada(AnulacionDeOrdenDeGiroV1.Type, 1)],
            desde, "Activación mínima de Tesorería (bloque 2)"), ct);
        if (destino.IsFailure) return Result.Failure<EstadoDeActivacionDto>(destino.Error);

        setup.Status = TreasuryModuleStatus.Active;
        setup.ActivatedOn = desde;
        setup.ResultWithThirdPerson = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.LotesTerceraPersonaParaResultado, hoy, ct);
        db.TreasuryActivations.Add(new TreasuryActivation
        {
            ActivatedOn = desde, CutoffDate = corte, ActivatedByUserId = usuario, RequirementsJson = JsonSerializer.Serialize(lista, Json), IsMinimal = true,
        });
        await auditoria.AgregarAsync("Treasury.Activated", "TreasurySetup", setup.PublicId, null, new { activatedOn = desde, minimal = true, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new EstadoDeActivacionDto(setup.Status.ToString(), setup.ActivatedOn, true, false, lista));
    }
}
