using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Holds;

// Feature 014, US11 (T189; api.md §8; FR-005, FR-055, FR-057, FR-065; D20, R23; research §20): registrar, consultar, responder y levantar embargos. Lo que
// retiene cada cuenta lo calcula y escribe RetencionDeEmbargos; la consignación vive en ConsignGarnishmentCommand. Todo exige el alcance sobre TODAS las cuentas
// de la persona (falla cerrado: un embargo revela lo retenido de cada una) y quien registra no levanta (expediente Garnishment). (nuevo)

/// <summary>Lo que trae el oficio (api.md §8, <c>POST /</c>, con lo de research E161). (nuevo)</summary>
public sealed record RegisterGarnishmentInput
{
    public Guid PersonPublicId { get; init; }
    public GarnishmentAuthority Authority { get; init; } = GarnishmentAuthority.Court;
    public Guid CourtPersonPublicId { get; init; }
    public string? CourtCode { get; init; }
    public string ProcessNumber { get; init; } = string.Empty;
    public string OfficeReference { get; init; } = string.Empty;
    public DateOnly OfficeDate { get; init; }
    public DateOnly? ReceivedOn { get; init; }
    public string ClaimantName { get; init; } = string.Empty;
    public string? ClaimantTaxId { get; init; }
    public Guid? ClaimantPersonPublicId { get; init; }
    public decimal? Amount { get; init; }
    public bool IsAlimony { get; init; }
    public GarnishmentAccountsScope AccountsScope { get; init; } = GarnishmentAccountsScope.AllSavings;
    public IReadOnlyList<Guid>? AccountPublicIds { get; init; }
    /// <summary>El oficio nombra también los aportes (se responden inembargables).</summary>
    public bool IncludesContributions { get; init; }
    public Guid? DestinationBankPublicId { get; init; }
    public string? DestinationAccountNumber { get; init; }
    public Guid SupportAttachmentPublicId { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Registra un embargo (api.md §8, <c>POST /</c>, <c>Lending.Garnishments.Register</c>, con clave). Rige desde que se registra (CGP art. 593 num. 10): retiene
/// en el mismo guardado lo embargable de las cuentas de la persona (<see cref="RetencionDeEmbargos"/>), marca y reclasifica las cuentas, pone el bloqueo de
/// deudor <c>Garnishment</c> de la 013 (la originación lo ve) y fija el plazo informativo de respuesta. Si el oficio nombra aportes, registra la respuesta
/// <c>ContributionsNotGarnishable</c>; si sólo nombra aportes, el embargo nace <c>Rejected</c> con esa respuesta (Ley 79 de 1988 art. 49). Los dos casos y
/// «nada embargable» viajan como avisos, no como errores (research E161). No pasa por la validación previa contable: la orden del juez no se detiene por
/// una regla de la matriz que falte (research E162). (nuevo)
/// </summary>
public sealed record RegisterGarnishmentCommand(RegisterGarnishmentInput Input) : IRequest<Result<GarnishmentDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterGarnishmentCommandValidator : AbstractValidator<RegisterGarnishmentCommand>
{
    public RegisterGarnishmentCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.PersonPublicId).NotEmpty().WithMessage("Elija la persona embargada.");
        RuleFor(x => x.Input.CourtPersonPublicId).NotEmpty().WithMessage("Elija el juzgado o la entidad que embarga (como persona del maestro).");
        RuleFor(x => x.Input.Authority).IsInEnum();
        RuleFor(x => x.Input.AccountsScope).IsInEnum();
        RuleFor(x => x.Input.ProcessNumber).NotEmpty().MaximumLength(Garnishment.LargoDelRadicado).WithMessage("Digite el radicado del proceso.");
        RuleFor(x => x.Input.OfficeReference).NotEmpty().MaximumLength(Garnishment.LargoDelRadicado).WithMessage("Digite el número del oficio.");
        RuleFor(x => x.Input.OfficeDate).NotEmpty();
        RuleFor(x => x.Input.ClaimantName).NotEmpty().MaximumLength(Garnishment.LargoDelNombre).WithMessage("Digite el demandante.");
        RuleFor(x => x.Input.ClaimantTaxId).MaximumLength(Garnishment.LargoDelDocumento);
        RuleFor(x => x.Input.CourtCode).MaximumLength(Garnishment.LargoDelCodigo);
        RuleFor(x => x.Input.Amount).GreaterThan(0m).When(x => x.Input.Amount is not null).WithMessage("El valor de la medida es mayor que cero.");
        RuleFor(x => x.Input.AccountPublicIds).NotEmpty().When(x => x.Input.AccountsScope == GarnishmentAccountsScope.Specific)
            .WithMessage("Con cuentas específicas, elija las cuentas que nombra el oficio.");
        RuleFor(x => x.Input.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el oficio.");
        RuleFor(x => x.Input.DestinationAccountNumber).MaximumLength(Garnishment.LargoDeLaCuentaBancaria);
        RuleFor(x => x.Input.Notes).MaximumLength(Garnishment.LargoDeLasNotas);
    }
}

/// <summary>Lo común de los comandos y consultas de embargos: la persona en el alcance y el expediente de la segregación. (nuevo)</summary>
public static class AlcanceDeEmbargos
{
    public const string Expediente = SegregacionDeCaptaciones.Embargo;

    public static ExpedienteDeCartera ExpedienteDe(Guid embargo) => new(Expediente, SegregacionDeCaptaciones.Raiz(Expediente, embargo));

    /// <summary>
    /// ¿Están en el alcance todas las cuentas de la persona (captación y aportes)? Si alguna no, emite <c>Lending.Access.OutOfScope</c> y devuelve falso: la
    /// respuesta es la de lo inexistente.
    /// </summary>
    public static async Task<bool> PersonaEnAlcanceAsync(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, LendingAuditEmitter auditoria,
        int persona, CancellationToken ct)
    {
        var cuentas = await db.MemberAccounts.AsNoTracking().Where(a => a.HolderPersonId == persona && !a.IsDeleted)
            .Select(a => new { a.PublicId, a.BranchId, a.CreditLineId }).ToListAsync(ct);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (cuentas.FirstOrDefault(c => !alcance.IncluyeOficina(c.BranchId) || !alcance.IncluyeLinea(c.CreditLineId)) is not { } ajena) return true;
        await auditoria.EmitirFueraDeAlcanceAsync(nameof(MemberAccount), ajena.PublicId, ct);
        return false;
    }

    /// <summary>El embargo por su PublicId con su persona en el alcance; fuera, el 404 del embargo.</summary>
    public static async Task<Result<Garnishment>> EmbargoEnAlcanceAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.Garnishments : db.Garnishments.AsNoTracking();
        var g = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted, ct);
        if (g is null || !await PersonaEnAlcanceAsync(db, alcance, auditoria, g.PersonId, ct)) return Result.Failure<Garnishment>(ErroresDeEmbargos.NoEncontrado());
        return Result.Success(g);
    }

    /// <summary>El plazo informativo de la respuesta: los días hábiles del parámetro desde el recibo (sin sábados, domingos ni festivos).</summary>
    public static async Task<DateOnly?> PlazoDeRespuestaAsync(ICalendarioHabil calendario, DateOnly recibido, int dias, CancellationToken ct)
    {
        if (dias <= 0) return null;
        var c = await calendario.ObtenerAsync(recibido, recibido.AddDays(dias * 3 + 15), DiasDeDescanso.SabadoYDomingo, null, ct);
        return c.SumarHabiles(recibido, dias);
    }
}

public sealed class RegisterGarnishmentCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    RetencionDeEmbargos retencion,
    RegistroDeIntervenciones intervenciones,
    ICalendarioHabil calendario,
    IActorActual actorActual,
    IDateTimeService reloj,
    Documents.RespuestasAlJuzgado? respuestas = null) : IRequestHandler<RegisterGarnishmentCommand, Result<GarnishmentDto>>
{
    public async Task<Result<GarnishmentDto>> Handle(RegisterGarnishmentCommand request, CancellationToken ct)
    {
        var e = request.Input;
        var hoy = reloj.HoyLocal;
        var recibido = e.ReceivedOn ?? hoy;
        if (recibido > hoy || e.OfficeDate > recibido)
            return Falla(new ErrorConDatos("Lending.Garnishment.Invalid", "El oficio no se recibe antes de su fecha ni en una fecha futura.",
                new { field = "receivedOn" }));
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == e.PersonPublicId && !p.IsDeleted, ct);
        var inexistente = new Error("Lending.Person.NotFound", "La persona no existe.");
        if (persona is null || !await AlcanceDeEmbargos.PersonaEnAlcanceAsync(db, alcance, auditoria, persona.Id, ct)) return Falla(inexistente);
        var juzgado = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == e.CourtPersonPublicId && !p.IsDeleted, ct);
        if (juzgado is null) return Falla(new ErrorConDatos("Lending.Person.NotFound", "El juzgado o la entidad no existe en el maestro de personas.",
            new { field = "courtPersonPublicId" }));
        int? demandante = null;
        if (e.ClaimantPersonPublicId is { } d)
        {
            demandante = await db.People.AsNoTracking().Where(p => p.PublicId == d && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (demandante is null) return Falla(new ErrorConDatos("Lending.Person.NotFound", "El demandante no existe en el maestro de personas.",
                new { field = "claimantPersonPublicId" }));
        }
        int? banco = null;
        if (e.DestinationBankPublicId is { } b)
        {
            banco = await db.Banks.AsNoTracking().Where(x => x.PublicId == b && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (banco is null) return Falla(new ErrorConDatos("Generic.NotFound", "El banco de la cuenta de depósitos judiciales no existe.",
                new { field = "destinationBankPublicId" }));
        }

        // Las cuentas del oficio: las de captación se embargan; las de aportes se responden inembargables.
        var deLaPersona = await db.MemberAccounts.AsNoTracking().Where(a => a.HolderPersonId == persona.Id && !a.IsDeleted)
            .Select(a => new { a.Id, a.PublicId, a.Nature }).ToListAsync(ct);
        var aportes = e.IncludesContributions;
        List<int>? especificas = null;
        if (e.AccountsScope == GarnishmentAccountsScope.Specific)
        {
            var pedidas = e.AccountPublicIds!.Distinct().ToList();
            var halladas = deLaPersona.Where(a => pedidas.Contains(a.PublicId)).ToList();
            if (halladas.Count != pedidas.Count) return Falla(ErroresDeEmbargos.CuentaAjena());
            aportes |= halladas.Any(a => a.Nature == LineNature.Contribution);
            especificas = halladas.Where(a => a.Nature == LineNature.Deposit).Select(a => a.Id).ToList();
        }
        var soloAportes = especificas is { Count: 0 };
        var cfg = await retencion.ConfiguracionAsync(hoy, ct);
        var plazo = await AlcanceDeEmbargos.PlazoDeRespuestaAsync(calendario, recibido, cfg.DiasHabilesParaResponder, ct);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([persona.Id], ModoDeEspera.Esperar, ct);
            var cuentas = deLaPersona.Where(a => a.Nature == LineNature.Deposit).Select(a => a.Id).ToList();
            if (cuentas.Count > 0) await cerrojo.BloquearCuentasAsync(cuentas, ModoDeEspera.Esperar, ct);
            var actor = await actorActual.ObtenerAsync(ct);
            var ahora = reloj.UtcNow;
            var g = new Garnishment
            {
                PersonId = persona.Id, Authority = e.Authority, CourtPersonId = juzgado.Id, CourtName = ReferenciasDeGobierno.Nombre(juzgado),
                CourtCode = Texto(e.CourtCode), ProcessNumber = e.ProcessNumber.Trim(), OrderNumber = e.OfficeReference.Trim(), OrderDate = e.OfficeDate,
                ReceivedOn = recibido, ClaimantName = e.ClaimantName.Trim(), ClaimantTaxId = Texto(e.ClaimantTaxId), ClaimantPersonId = demandante,
                Amount = e.Amount, IsChildSupport = e.IsAlimony, AccountsScope = e.AccountsScope,
                SpecificAccountIdsJson = especificas is null ? null : JsonSerializer.Serialize(especificas.Order()), IncludesContributions = aportes,
                DestinationBankId = banco, DestinationBankAccountNumber = Texto(e.DestinationAccountNumber), OrderAttachmentPublicId = e.SupportAttachmentPublicId,
                Status = soloAportes ? GarnishmentStatus.Rejected : GarnishmentStatus.Active, ResponseDueOn = plazo,
                RegisteredByUserId = actor.UserId, RegisteredAt = ahora, Notes = Texto(e.Notes),
            };
            var radicada = await intervenciones.VerificarAsync(AlcanceDeEmbargos.ExpedienteDe(g.PublicId), AccionesDeSegregacion.Radicar, hoy, ct);
            if (radicada.IsFailure) return Falla(radicada.Error);
            g.RegisteredByUserId = radicada.Value.UsuarioId;
            db.Garnishments.Add(g);
            intervenciones.Registrar(radicada.Value, AlcanceDeEmbargos.Expediente, g.PublicId);
            await db.SaveChangesAsync(ct);

            var avisos = new List<string>();
            Domain.Lending.Deposits.EmbargoCalculado? calculo = null;
            if (!soloAportes)
            {
                var r = await retencion.RecalcularAsync(persona.Id, hoy, radicada.Value.UsuarioId, $"Embargo {g.OrderNumber}", ct);
                if (r.IsFailure) return Falla(r.Error);
                calculo = r.Value.FirstOrDefault(x => x.Embargo.Id == g.Id)?.Calculo;
                if (calculo is { Retenido: 0m })
                    avisos.Add($"{ErroresDeEmbargos.CodigoNadaEmbargable}: con el límite inembargable vigente no hay nada que retener hoy; los depósitos posteriores se retienen al recalcular.");
                // La 013: la originación ve el embargo de la persona (DebtorBlockReason.Garnishment).
                db.DebtorBlocks.Add(DebtorBlock.Nuevo(persona.Id, DebtorBlockReason.Garnishment, ClaveDelBloqueoDeDeudor(g), recibido,
                    $"Embargo {g.OrderNumber} del proceso {g.ProcessNumber}", radicada.Value.UsuarioId));
            }
            if (aportes)
            {
                var texto = VistaDeEmbargos.Llenar(cfg.Plantillas.GetValueOrDefault(GarnishmentResponseKind.ContributionsNotGarnishable)
                        ?? VistaDeEmbargos.Estandar[GarnishmentResponseKind.ContributionsNotGarnishable],
                    await VistaDeEmbargos.VariablesAsync(db, g, 0m, 0m, hoy, [], ct));
                // US13 (T196; research E176): la respuesta nace con su documento emitido (código de verificación), en la misma transacción.
                int? documento = null;
                if (respuestas is not null)
                {
                    var (id, aviso) = await respuestas.EmitirAsync(g, GarnishmentResponseKind.ContributionsNotGarnishable, hoy, Recortar(texto), null, null,
                        radicada.Value.UsuarioId, ct);
                    documento = id;
                    if (aviso is not null) avisos.Add(aviso);
                }
                db.GarnishmentResponses.Add(new GarnishmentResponse
                {
                    GarnishmentId = g.Id, Kind = GarnishmentResponseKind.ContributionsNotGarnishable, SentOn = hoy, Text = Recortar(texto),
                    RespondedAt = ahora, RespondedByUserId = radicada.Value.UsuarioId, IssuedDocumentId = documento,
                });
                avisos.Add($"{ErroresDeEmbargos.CodigoAportesInembargables}: los aportes sociales no se embargan (Ley 79 de 1988, art. 49); "
                           + "quedó registrada la respuesta al juzgado.");
            }
            await db.SaveChangesAsync(ct);
            return Result.Success(await VistaDeEmbargos.DtoAsync(db, g, calculo, 0m, hoy, avisos, ct));
        }, ct);
    }

    /// <summary>La clave del bloqueo de deudor que deja el embargo (la 013, <c>DebtorBlockReason.Garnishment</c>).</summary>
    public static string ClaveDelBloqueoDeDeudor(Garnishment g) => $"garnishment:{g.PublicId:N}";

    internal static string? Texto(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    internal static string Recortar(string t) => t.Length <= GarnishmentResponse.LargoDelTexto ? t : t[..GarnishmentResponse.LargoDelTexto];

    private static Result<GarnishmentDto> Falla(Error e) => Result.Failure<GarnishmentDto>(e);
}

/// <summary>Los embargos (api.md §8, <c>GET /</c>, <c>Lending.Accounts.Read</c>): de las personas en el alcance; lectura sensible auditada. (nuevo)</summary>
public sealed record ListGarnishmentsQuery(Guid? PersonPublicId = null, GarnishmentStatus? Status = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<GarnishmentDto>>>;

public sealed class ListGarnishmentsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
    RetencionDeEmbargos retencion, IDateTimeService reloj) : IRequestHandler<ListGarnishmentsQuery, Result<PagedResult<GarnishmentDto>>>
{
    public async Task<Result<PagedResult<GarnishmentDto>>> Handle(ListGarnishmentsQuery request, CancellationToken ct)
    {
        var consulta = db.Garnishments.AsNoTracking().Where(g => !g.IsDeleted);
        if (request.PersonPublicId is { } p) consulta = consulta.Where(g => db.People.Any(x => x.Id == g.PersonId && x.PublicId == p));
        if (request.Status is { } s) consulta = consulta.Where(g => g.Status == s);
        var todos = await consulta.OrderByDescending(g => g.ReceivedOn).ThenByDescending(g => g.Id).Take(2_000).ToListAsync(ct);
        var visibles = new List<Garnishment>();
        foreach (var persona in todos.Select(g => g.PersonId).Distinct())
            if (await AlcanceDeEmbargos.PersonaEnAlcanceAsync(db, alcance, auditoria, persona, ct)) visibles.AddRange(todos.Where(g => g.PersonId == persona));
        visibles = visibles.OrderByDescending(g => g.ReceivedOn).ThenByDescending(g => g.Id).ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        var hoy = reloj.HoyLocal;
        var deLaPagina = visibles.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();
        var consignado = await retencion.ConsignadoAsync(deLaPagina.Select(g => g.Id).ToList(), ct);
        var items = new List<GarnishmentDto>();
        foreach (var g in deLaPagina) items.Add(await VistaDeEmbargos.DtoAsync(db, g, null, consignado.GetValueOrDefault(g.Id), hoy, [], ct));
        await auditoria.EmitirEmbargoVistoAsync(null, nameof(ListGarnishmentsQuery),
            new { person = request.PersonPublicId, status = request.Status?.ToString(), rows = items.Count }, ct);
        return Result.Success(new PagedResult<GarnishmentDto>(items, pagina.SafePage, pagina.SafePageSize, visibles.Count));
    }
}

/// <summary>
/// Un embargo con la explicación del límite inembargable (api.md §8, <c>GET /{id}</c>, <c>Lending.Accounts.Read</c>): corre el motor con los saldos de hoy
/// (sin escribir) para mostrar lo protegido y lo embargable de cada cuenta. Lectura sensible auditada. (nuevo)
/// </summary>
public sealed record GetGarnishmentQuery(Guid GarnishmentPublicId) : IRequest<Result<GarnishmentDto>>;

public sealed class GetGarnishmentQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
    RetencionDeEmbargos retencion, IDateTimeService reloj) : IRequestHandler<GetGarnishmentQuery, Result<GarnishmentDto>>
{
    public async Task<Result<GarnishmentDto>> Handle(GetGarnishmentQuery request, CancellationToken ct)
    {
        var leido = await AlcanceDeEmbargos.EmbargoEnAlcanceAsync(db, alcance, auditoria, request.GarnishmentPublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<GarnishmentDto>(leido.Error);
        var g = leido.Value;
        var hoy = reloj.HoyLocal;
        var consignado = (await retencion.ConsignadoAsync([g.Id], ct)).GetValueOrDefault(g.Id);
        var avisos = new List<string>();
        Domain.Lending.Deposits.EmbargoCalculado? calculo = null;
        if (g.Vigente)
        {
            var explicado = await ExplicacionAsync(g, consignado, hoy, ct);
            if (explicado.IsSuccess) calculo = explicado.Value;
            else avisos.Add($"{explicado.Error.Code}: {explicado.Error.Message}");
        }
        await auditoria.EmitirEmbargoVistoAsync(g.PublicId, nameof(GetGarnishmentQuery), new { status = g.Status.ToString() }, ct);
        return Result.Success(await VistaDeEmbargos.DtoAsync(db, g, calculo, consignado, hoy, avisos, ct));
    }

    /// <summary>El motor sobre los saldos de hoy con lo que retienen los embargos anteriores de la persona (sin escribir).</summary>
    private async Task<Result<Domain.Lending.Deposits.EmbargoCalculado>> ExplicacionAsync(Garnishment g, decimal consignado, DateOnly hoy, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == g.PersonId, ct);
        var cuentas = await retencion.CuentasDeLaPersonaAsync(g.PersonId, seguir: false, ct);
        var porConcepto = await Accounts.LectorDeCuentas.SaldosAsync(db, cuentas.Select(c => c.Id).ToList(), ct);
        var saldos = cuentas.ToDictionary(c => c.Id, c => Accounts.LectorDeCuentas.SaldoDelTitular(c.Nature, porConcepto.GetValueOrDefault(c.Id) ?? []));
        var anteriores = await db.Garnishments.AsNoTracking()
            .Where(x => x.PersonId == g.PersonId && x.Status == GarnishmentStatus.Active && !x.IsDeleted
                        && (x.ReceivedOn < g.ReceivedOn || (x.ReceivedOn == g.ReceivedOn && x.Id < g.Id)))
            .Select(x => x.Id).ToListAsync(ct);
        var porOtros = await db.AccountHolds.AsNoTracking()
            .Where(h => h.GarnishmentId != null && anteriores.Contains(h.GarnishmentId.Value) && h.ReleasedAt == null && !h.IsDeleted)
            .GroupBy(h => h.MemberAccountId).Select(x => new { x.Key, Total = x.Sum(h => h.Amount ?? 0m) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return await retencion.CalcularAsync(g, persona, cuentas, saldos, porOtros, consignado, await retencion.ConfiguracionAsync(hoy, ct), hoy, ct);
    }
}

/// <summary>
/// El borrador del texto de una respuesta al juzgado (research E158; <c>Lending.Accounts.Read</c>): la plantilla de la cooperativa
/// (<c>Embargos.PlantillasDeRespuesta</c>) o la estándar, llena con el oficio, la persona, lo retenido y lo consignado. No escribe; lectura auditada. (nuevo)
/// </summary>
public sealed record GetGarnishmentResponseDraftQuery(Guid GarnishmentPublicId, GarnishmentResponseKind Kind) : IRequest<Result<GarnishmentResponseDraftDto>>;

public sealed class GetGarnishmentResponseDraftQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
    RetencionDeEmbargos retencion, IDateTimeService reloj) : IRequestHandler<GetGarnishmentResponseDraftQuery, Result<GarnishmentResponseDraftDto>>
{
    public async Task<Result<GarnishmentResponseDraftDto>> Handle(GetGarnishmentResponseDraftQuery request, CancellationToken ct)
    {
        var leido = await AlcanceDeEmbargos.EmbargoEnAlcanceAsync(db, alcance, auditoria, request.GarnishmentPublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<GarnishmentResponseDraftDto>(leido.Error);
        var g = leido.Value;
        var hoy = reloj.HoyLocal;
        var cfg = await retencion.ConfiguracionAsync(hoy, ct);
        var consignado = (await retencion.ConsignadoAsync([g.Id], ct)).GetValueOrDefault(g.Id);
        var dto = await VistaDeEmbargos.DtoAsync(db, g, null, consignado, hoy, [], ct);
        var propia = cfg.Plantillas.TryGetValue(request.Kind, out var plantilla);
        var texto = VistaDeEmbargos.Llenar(propia ? plantilla! : VistaDeEmbargos.Estandar[request.Kind],
            await VistaDeEmbargos.VariablesAsync(db, g, dto.RetainedTotal, consignado, hoy, dto.Retained, ct));
        await auditoria.EmitirEmbargoVistoAsync(g.PublicId, nameof(GetGarnishmentResponseDraftQuery), new { kind = request.Kind.ToString() }, ct);
        return Result.Success(new GarnishmentResponseDraftDto(request.Kind, texto, propia));
    }
}

/// <summary>
/// Registra una respuesta enviada al juzgado (api.md §8, <c>POST /{id}/responses</c>, <c>Lending.Garnishments.Register</c>, con clave): la clase, el
/// soporte firmado, la fecha de envío y el texto (si no viene, el borrador de la plantilla); deja lo retenido y lo consignado a la fecha. (nuevo)
/// </summary>
public sealed record RegisterGarnishmentResponseCommand(Guid GarnishmentPublicId, GarnishmentResponseKind Kind, Guid? AttachmentPublicId, DateOnly SentOn,
    string? Text) : IRequest<Result<GarnishmentResponseDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterGarnishmentResponseCommandValidator : AbstractValidator<RegisterGarnishmentResponseCommand>
{
    public RegisterGarnishmentResponseCommandValidator()
    {
        RuleFor(x => x.GarnishmentPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.SentOn).NotEmpty();
        RuleFor(x => x.Text).MaximumLength(GarnishmentResponse.LargoDelTexto);
    }
}

public sealed class RegisterGarnishmentResponseCommandHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
    RetencionDeEmbargos retencion, IActorActual actorActual, IDateTimeService reloj, Documents.RespuestasAlJuzgado? respuestas = null)
    : IRequestHandler<RegisterGarnishmentResponseCommand, Result<GarnishmentResponseDto>>
{
    public async Task<Result<GarnishmentResponseDto>> Handle(RegisterGarnishmentResponseCommand request, CancellationToken ct)
    {
        var leido = await AlcanceDeEmbargos.EmbargoEnAlcanceAsync(db, alcance, auditoria, request.GarnishmentPublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<GarnishmentResponseDto>(leido.Error);
        var g = leido.Value;
        var hoy = reloj.HoyLocal;
        if (request.SentOn > hoy || request.SentOn < g.ReceivedOn)
            return Result.Failure<GarnishmentResponseDto>(new ErrorConDatos("Lending.Garnishment.Invalid",
                "La respuesta se envía entre el recibo del oficio y hoy.", new { field = "sentOn" }));
        var consignado = (await retencion.ConsignadoAsync([g.Id], ct)).GetValueOrDefault(g.Id);
        var dto = await VistaDeEmbargos.DtoAsync(db, g, null, consignado, hoy, [], ct);
        var texto = request.Text;
        if (string.IsNullOrWhiteSpace(texto))
        {
            var cfg = await retencion.ConfiguracionAsync(hoy, ct);
            texto = VistaDeEmbargos.Llenar(cfg.Plantillas.GetValueOrDefault(request.Kind) ?? VistaDeEmbargos.Estandar[request.Kind],
                await VistaDeEmbargos.VariablesAsync(db, g, dto.RetainedTotal, consignado, request.SentOn, dto.Retained, ct));
        }
        var actor = await actorActual.ObtenerAsync(ct);
        var textoFinal = RegisterGarnishmentCommandHandler.Recortar(texto.Trim());
        // US13 (T196; research E176): la respuesta y su documento emitido (número, código de verificación y PDF) en una transacción.
        return await IngenIA365ERP.Application.Common.Persistence.TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            int? documento = null;
            string? aviso = null;
            if (respuestas is not null)
                (documento, aviso) = await respuestas.EmitirAsync(g, request.Kind, request.SentOn, textoFinal, dto.RetainedTotal, consignado, actor.UserId, ct);
            var respuesta = new GarnishmentResponse
            {
                GarnishmentId = g.Id, Kind = request.Kind, AttachmentPublicId = request.AttachmentPublicId, SentOn = request.SentOn,
                RetainedAmount = dto.RetainedTotal, ConsignedAmount = consignado, Text = textoFinal,
                RespondedAt = reloj.UtcNow, RespondedByUserId = actor.UserId ?? 0, IssuedDocumentId = documento,
            };
            db.GarnishmentResponses.Add(respuesta);
            await db.SaveChangesAsync(ct);
            var emitido = documento is { } id
                ? await db.IssuedLendingDocuments.AsNoTracking().Where(d => d.Id == id).Select(d => new { d.PublicId, d.Number }).FirstOrDefaultAsync(ct)
                : null;
            return Result.Success(new GarnishmentResponseDto(respuesta.PublicId, respuesta.Kind, respuesta.SentOn, respuesta.RetainedAmount,
                respuesta.ConsignedAmount, respuesta.Text, respuesta.AttachmentPublicId, respuesta.RespondedAt)
            {
                IssuedDocumentPublicId = emitido?.PublicId, IssuedDocumentNumber = emitido?.Number, Warning = aviso,
            });
        }, ct);
    }
}

/// <summary>
/// El desembargo (api.md §8, <c>POST /{id}/release</c>, <c>Lending.Garnishments.Release</c>, con clave): con <c>amount</c> menor que el valor de la medida,
/// la reduce (desembargo parcial); sin él, la levanta toda. Libera lo retenido que sobre (recalcula), quita la marca y reclasifica, levanta el bloqueo de
/// deudor y conserva la historia. Quien registró el embargo no lo levanta (<c>Lending.Segregation.Conflict</c>, FR-005). (nuevo)
/// </summary>
public sealed record ReleaseGarnishmentCommand(Guid GarnishmentPublicId, string ReleaseOfficeReference, DateOnly? ReleaseOfficeDate,
    Guid? SupportAttachmentPublicId, decimal? Amount, string Reason) : IRequest<Result<GarnishmentDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReleaseGarnishmentCommandValidator : ValidadorConMotivo<ReleaseGarnishmentCommand>
{
    public ReleaseGarnishmentCommandValidator()
    {
        RuleFor(x => x.GarnishmentPublicId).NotEmpty();
        RuleFor(x => x.ReleaseOfficeReference).NotEmpty().MaximumLength(Garnishment.LargoDelRadicado).WithMessage("Digite el oficio del desembargo.");
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.Amount is not null);
    }
}

public sealed class ReleaseGarnishmentCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    RetencionDeEmbargos retencion,
    RegistroDeIntervenciones intervenciones,
    IDateTimeService reloj) : IRequestHandler<ReleaseGarnishmentCommand, Result<GarnishmentDto>>
{
    public async Task<Result<GarnishmentDto>> Handle(ReleaseGarnishmentCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leido = await AlcanceDeEmbargos.EmbargoEnAlcanceAsync(db, alcance, auditoria, request.GarnishmentPublicId, seguir: false, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var vista = leido.Value;
        if (vista.Status == GarnishmentStatus.Released) return Falla(ErroresDeEmbargos.YaLevantado(vista.OrderNumber));
        if (vista.Status != GarnishmentStatus.Active) return Falla(ErroresDeEmbargos.NoActivo(vista.OrderNumber, vista.Status.ToString()));
        var parcial = request.Amount is { } valor && vista.Amount is { } medida && valor < medida;
        if (request.Amount is { } pedido && !parcial && vista.Amount is { } m2 && pedido > m2)
            return Falla(ErroresDeEmbargos.DesembargoParcialInvalido(vista.Amount, pedido));
        if (request.Amount is { } sinMedida && vista.Amount is null) return Falla(ErroresDeEmbargos.DesembargoParcialInvalido(null, sinMedida));
        var decidida = await intervenciones.VerificarAsync(AlcanceDeEmbargos.ExpedienteDe(vista.PublicId), AccionesDeSegregacion.Decidir, hoy, ct);
        if (decidida.IsFailure) return Falla(decidida.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([vista.PersonId], ModoDeEspera.Esperar, ct);
            var cuentas = (await retencion.CuentasDeLaPersonaAsync(vista.PersonId, seguir: false, ct)).Select(c => c.Id).ToList();
            if (cuentas.Count > 0) await cerrojo.BloquearCuentasAsync(cuentas, ModoDeEspera.Esperar, ct);
            var g = await db.Garnishments.FirstAsync(x => x.Id == vista.Id, ct);
            var ahora = reloj.UtcNow;
            if (parcial) g.Reducir(request.Amount!.Value);
            else
            {
                g.Levantar(request.ReleaseOfficeReference, request.ReleaseOfficeDate ?? hoy, request.SupportAttachmentPublicId, request.Reason,
                    decidida.Value.UsuarioId, ahora);
                var clave = RegisterGarnishmentCommandHandler.ClaveDelBloqueoDeDeudor(g);
                foreach (var b in await db.DebtorBlocks.Where(b => b.PersonId == g.PersonId && b.SourceKey == clave && b.LiftedAt == null && !b.IsDeleted)
                             .ToListAsync(ct))
                    b.Levantar(hoy, ahora, decidida.Value.UsuarioId, $"Desembargo {request.ReleaseOfficeReference}");
            }
            intervenciones.Registrar(decidida.Value, AlcanceDeEmbargos.Expediente, g.PublicId);
            await db.SaveChangesAsync(ct);
            var r = await retencion.RecalcularAsync(g.PersonId, hoy, decidida.Value.UsuarioId,
                parcial ? $"Desembargo parcial {request.ReleaseOfficeReference}" : $"Desembargo {request.ReleaseOfficeReference}", ct);
            if (r.IsFailure) return Falla(r.Error);
            var consignado = (await retencion.ConsignadoAsync([g.Id], ct)).GetValueOrDefault(g.Id);
            return Result.Success(await VistaDeEmbargos.DtoAsync(db, g, r.Value.FirstOrDefault(x => x.Embargo.Id == g.Id)?.Calculo, consignado, hoy, [], ct));
        }, ct);
    }

    private static Result<GarnishmentDto> Falla(Error e) => Result.Failure<GarnishmentDto>(e);
}
