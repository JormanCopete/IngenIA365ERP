using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Configuration;

// Feature 013, US1 (T0353; api.md §3.1; data-model §1.2–§1.7; FR-019–FR-033): crear una línea con su primera versión, registrar
// versiones, cerrarla, reabrirla y borrarla. Todo pasa por ReglasDeLineaDeCredito (las mismas reglas que la plantilla) y por
// AltaDeVersionDeLinea (el único que registra una versión con sus hijas). (nuevo)

/// <summary>
/// El único que registra una versión de línea con sus cuatro hijas (feature 013, US1, T0353): número siguiente, versión, hijas, y en la
/// línea la activación (<c>Draft → Active</c>) y la limpieza del bloqueo por usura (una versión que pasa el control lo limpia). Lo usan el
/// alta, la versión nueva, el puntaje y la plantilla. No abre transacción ni verifica: quien llama ya pasó
/// <see cref="ReglasDeLineaDeCredito.VerificarAsync"/> y corre dentro de una. Los créditos vivos no se tocan: sellaron sus condiciones
/// (FR-032). (nuevo)
/// </summary>
public sealed class AltaDeVersionDeLinea(IApplicationDbContext db, IDateTimeService reloj)
{
    /// <summary>
    /// Las reglas de la versión que dependen de las demás versiones y líneas: no empieza antes de la última registrada
    /// (<c>VersionOverlaps</c>) y, si es la de defecto de Comercial para un tipo de deudor, ninguna otra línea activa lo es.
    /// </summary>
    public async Task<Result> ValidarContraLoRegistradoAsync(int? lineaId, BorradorDeVersionDeLinea b, CancellationToken ct)
    {
        if (lineaId is { } id)
        {
            // Feature 014 (T011, T013): una versión de crédito sólo va sobre una línea de colocación.
            var linea = await db.CreditLines.AsNoTracking().FirstAsync(l => l.Id == id, ct);
            if (linea.ValidarNaturaleza(LineNature.Placement) is { } distinta) return Result.Failure(ErroresDeLineasDeCredito.De(distinta));
            var ultima = await db.CreditLineVersions.AsNoTracking().Where(v => v.CreditLineId == id && !v.IsDeleted)
                .Select(v => (DateOnly?)v.ValidFrom).MaxAsync(ct);
            if (ultima is { } u && b.ValidFrom < u) return Result.Failure(ErroresDeLineasDeCredito.VersionCruzada(u));
        }
        if (b.CommercialDefaultFor is { } tipo)
        {
            var otras = await db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted && l.Status != CreditLineStatus.Closed && l.Id != (lineaId ?? 0) && l.Nature == LineNature.Placement)
                .Select(l => new { l.Id, l.Code }).ToListAsync(ct);
            var ids = otras.Select(o => o.Id).ToArray();
            var versiones = await db.CreditLineVersions.AsNoTracking().Where(v => ids.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct);
            foreach (var otra in otras)
            {
                var vigente = VigenciaBitemporal.VigenteDeCartera(versiones.Where(v => v.CreditLineId == otra.Id), b.ValidFrom, DateTime.MaxValue)
                    ?? versiones.Where(v => v.CreditLineId == otra.Id).OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.VersionNumber).FirstOrDefault();
                if (vigente?.CommercialDefaultFor == tipo) return Result.Failure(ErroresDeLineasDeCredito.DefectoComercialRepetido(otra.Code));
            }
        }
        return Result.Success();
    }

    /// <summary>
    /// Registra la versión de <paramref name="linea"/> (ya guardada) y sus hijas, y activa o desbloquea la línea. Guarda dos veces (la
    /// versión para tener su Id, y las hijas con la línea), dentro de la transacción de quien llama.
    /// </summary>
    /// <param name="admitirDesde">Sólo la plantilla, en una línea sin uso: admite una versión que empieza antes de hoy (plantillas.md §1).</param>
    public async Task<CreditLineVersion> RegistrarAsync(CreditLine linea, BorradorDeVersionDeLinea b, CancellationToken ct, bool admitirDesde = false)
    {
        // Feature 014 (T011): CreditLineVersion es sólo de colocación (data-model §1.1).
        IngenIA365ERP.Domain.Lending.ReglaDeCarteraException.SiHay(linea.ValidarNaturaleza(LineNature.Placement));
        var ultimo = await db.CreditLineVersions.IgnoreQueryFilters().Where(v => v.CreditLineId == linea.Id)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        var hoy = reloj.HoyLocal;
        var registrada = CreditLineVersion.Registrar(linea.Id, b, ultimo + 1, admitirDesde && b.ValidFrom < hoy ? b.ValidFrom : hoy, reloj.UtcNow);
        db.CreditLineVersions.Add(registrada.Version);
        await db.SaveChangesAsync(ct);

        db.CreditLineRateBands.AddRange(registrada.Bandas(registrada.Version.Id));
        db.CreditLineDocumentRequirements.AddRange(registrada.Documentos(registrada.Version.Id));
        db.CreditLineInsurances.AddRange(registrada.Seguros(registrada.Version.Id));
        db.CreditLineCollateralRules.AddRange(registrada.Garantias(registrada.Version.Id));
        if (linea.Status == CreditLineStatus.Draft) linea.Activar();
        linea.DesbloquearPorUsura();
        await db.SaveChangesAsync(ct);
        return registrada.Version;
    }
}

/// <summary>Lo que comparten las consultas y los comandos de líneas: el DTO de una línea y las aseguradoras por Id. (nuevo)</summary>
public static class ConsultasDeLineas
{
    /// <summary>La línea con la versión vigente en <paramref name="fecha"/> y su historial.</summary>
    public static async Task<CreditLineDto> DtoAsync(IApplicationDbContext db, CreditLine linea, DateOnly fecha, CancellationToken ct)
    {
        var versiones = await db.CreditLineVersions.AsNoTracking().Where(v => v.CreditLineId == linea.Id && !v.IsDeleted).ToListAsync(ct);
        var vigente = VigenciaBitemporal.VigenteDeCartera(versiones, fecha, DateTime.MaxValue);
        CreditLineVersionDto? actual = null;
        if (vigente is not null)
        {
            var leida = await new VersionVigenteDeLinea(db).LeerVersionAsync(vigente.Id, ct);
            actual = CreditLineVersionDto.De(leida!, VersionVigenteDeLinea.VigenteHasta(vigente, versiones), await AseguradorasAsync(db, leida!.Seguros, ct));
        }
        var historial = versiones.OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.VersionNumber)
            .Select(v => new CreditLineVersionSummaryDto(v.PublicId, v.VersionNumber, v.ValidFrom, VersionVigenteDeLinea.VigenteHasta(v, versiones),
                v.RegisteredAt, v.CreatedBy, v.Reason, v.Id == vigente?.Id))
            .ToList();
        return new CreditLineDto(linea.PublicId, linea.Code, linea.Name, linea.Purpose, linea.Status, linea.ClosedReason, linea.ClosedAt,
            linea.CloseNotes, linea.UsuryBlockedSince, linea.UsuryBlockedByValuePublicId, linea.AdmiteCreditosNuevos, actual, historial);
    }

    /// <summary>Las aseguradoras de unos seguros: PublicId y nombre por Id interno.</summary>
    public static async Task<IReadOnlyDictionary<int, (Guid PublicId, string? Nombre)>> AseguradorasAsync(IApplicationDbContext db,
        IEnumerable<CreditLineInsurance> seguros, CancellationToken ct)
    {
        var ids = seguros.Select(s => s.InsurerPersonId).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, (Guid, string?)>();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        return personas.ToDictionary(p => p.Id, p => (p.PublicId, (string?)(string.IsNullOrWhiteSpace(p.BusinessName) ? Domain.Entities.Core.NombreDePersona.Completo(p) : p.BusinessName)));
    }

    /// <summary>Las aseguradoras citadas por <c>PublicId</c>: Id interno de las vivas; nulo si alguna no existe.</summary>
    public static async Task<IReadOnlyDictionary<Guid, int>?> ResolverAseguradorasAsync(IApplicationDbContext db, CreditLineVersionInput input, CancellationToken ct)
    {
        var pedidas = (input.Insurances ?? []).Select(s => s.InsurerPersonPublicId).Distinct().ToArray();
        if (pedidas.Length == 0) return new Dictionary<Guid, int>();
        var vivas = await db.People.AsNoTracking().Where(p => pedidas.Contains(p.PublicId) && !p.IsDeleted)
            .ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        return pedidas.All(vivas.ContainsKey) ? vivas : null;
    }

    /// <summary>
    /// La línea viva por su PublicId y dentro del alcance de líneas; fuera, el mismo 404 que si no existiera. Feature 014 (T011, T013): por
    /// defecto sólo de colocación —las rutas de líneas de crédito no operan una de captación o de aportes
    /// (<c>Lending.CreditLine.NatureMismatch</c>)—; con <paramref name="naturaleza"/> nula, cualquiera (catálogo común).
    /// </summary>
    public static async Task<Result<CreditLine>> LineaEnAlcanceAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid publicId, bool seguir, CancellationToken ct, LineNature? naturaleza = LineNature.Placement)
    {
        var consulta = seguir ? db.CreditLines : db.CreditLines.AsNoTracking();
        var linea = await consulta.FirstOrDefaultAsync(l => l.PublicId == publicId && !l.IsDeleted, ct);
        if (linea is null) return Result.Failure<CreditLine>(ErroresDeLineasDeCredito.LineaInexistente());
        if (naturaleza is { } esperada && linea.ValidarNaturaleza(esperada) is { } distinta)
            return Result.Failure<CreditLine>(ErroresDeLineasDeCredito.De(distinta));
        var dentro = await alcance.AsegurarAsync(null, linea.Id, ErroresDeLineasDeCredito.LineaInexistente(), auditoria, nameof(CreditLine), linea.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<CreditLine>(dentro.Error) : Result.Success(linea);
    }
}

/// <summary>La forma de una versión (Principio VIII): lo que no necesita la base. Las reglas de negocio las decide el handler.</summary>
public sealed class CreditLineVersionInputValidator : AbstractValidator<CreditLineVersionInput>
{
    public CreditLineVersionInputValidator()
    {
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige la versión.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indicá el motivo de la versión.").MaximumLength(300);
        RuleFor(x => x.ModalityCode).NotEmpty().WithMessage("Elegí la modalidad de la línea.").MaximumLength(40);
        RuleFor(x => x.SubsegmentCode).MaximumLength(30);
        RuleFor(x => x.Rates).NotNull();
        RuleFor(x => x.Amounts).NotNull();
        RuleFor(x => x.Terms).NotNull();
        RuleFor(x => x.Periodicities).NotEmpty().WithMessage("Elegí al menos una periodicidad.");
        RuleFor(x => x.PaymentForms).NotEmpty().WithMessage("Elegí al menos una forma de pago.");
        RuleFor(x => x.Amounts.Min).PrecisionScale(18, 2, true).WithMessage("Los montos admiten hasta 2 decimales.").When(x => x.Amounts is not null);
        RuleFor(x => x.Amounts.Max).PrecisionScale(18, 2, true).WithMessage("Los montos admiten hasta 2 decimales.").When(x => x.Amounts is not null);
        RuleForEach(x => x.Documents).ChildRules(d =>
        {
            d.RuleFor(y => y.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
            d.RuleFor(y => y.Name).NotEmpty().MaximumLength(120);
        });
        RuleForEach(x => x.Insurances).ChildRules(s =>
        {
            s.RuleFor(y => y.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
            s.RuleFor(y => y.Name).NotEmpty().MaximumLength(120);
            s.RuleFor(y => y.InsurerPersonPublicId).NotEmpty().WithMessage("Elegí la aseguradora.");
            s.RuleFor(y => y.Rate).GreaterThanOrEqualTo(0m);
        });
    }
}

// ------------------------------------------------------------------------------------------------------------------ crear --

/// <summary>
/// Crea una línea con su primera versión (api.md §3.1, <c>POST /api/lending/credit-lines</c>, <c>Lending.CreditLines.Manage</c>). El
/// código es la nomenclatura de la cooperativa (<see cref="CodigoDeCatalogo"/>) y no cambia. (nuevo)
/// </summary>
public sealed record CreateCreditLineCommand(string Code, string Name, string? Purpose, CreditLineVersionInput FirstVersion)
    : IRequest<Result<CreditLineDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public string Reason => FirstVersion?.Reason ?? string.Empty;
}

public sealed class CreateCreditLineCommandValidator : AbstractValidator<CreateCreditLineCommand>
{
    public CreateCreditLineCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indicá el código de la línea.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage("El código admite hasta 10 caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indicá el nombre de la línea.").MaximumLength(120);
        RuleFor(x => x.Purpose).MaximumLength(300);
        RuleFor(x => x.FirstVersion).NotNull().WithMessage("La línea nace con su primera versión.")
            .SetValidator(new CreditLineVersionInputValidator()!);
    }
}

/// <summary>
/// Código vivo repetido → <c>Catalogo.CodigoDuplicado</c> con el nombre del existente; aseguradora inexistente → <c>Core.Person.NotFound</c>;
/// la versión pasa <see cref="ReglasDeLineaDeCredito"/> (modalidad, vivienda, no asociados, D20, precisión, bandas, usura a
/// <c>ValidFrom</c> y a hoy). Si algo falla no se guarda nada; si pasa, la línea nace <c>Active</c> con su versión 1 y sus hijas.
/// </summary>
public sealed class CreateCreditLineCommandHandler(IApplicationDbContext db, ReglasDeLineaDeCredito reglas, AltaDeVersionDeLinea alta, IDateTimeService reloj)
    : IRequestHandler<CreateCreditLineCommand, Result<CreditLineDto>>
{
    public async Task<Result<CreditLineDto>> Handle(CreateCreditLineCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var repetida = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.Code == codigo && !l.IsDeleted, ct);
        if (repetida is not null)
            return Result.Failure<CreditLineDto>(CodigoDeCatalogo.Duplicado("una línea de crédito", codigo, repetida.Name, repetida.PublicId));

        var aseguradoras = await ConsultasDeLineas.ResolverAseguradorasAsync(db, request.FirstVersion, ct);
        if (aseguradoras is null) return Result.Failure<CreditLineDto>(ErroresDeGobierno.PersonaInexistente());
        var borrador = request.FirstVersion.Borrador(aseguradoras);

        var verificada = await reglas.VerificarAsync(borrador, reloj.HoyLocal, ct);
        if (verificada.IsFailure) return Result.Failure<CreditLineDto>(verificada.Error);
        var contraLoRegistrado = await alta.ValidarContraLoRegistradoAsync(null, borrador, ct);
        if (contraLoRegistrado.IsFailure) return Result.Failure<CreditLineDto>(contraLoRegistrado.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var linea = CreditLine.Nueva(codigo, request.Name, request.Purpose);
            db.CreditLines.Add(linea);
            await db.SaveChangesAsync(ct);
            await alta.RegistrarAsync(linea, borrador, ct);
            return Result.Success(await ConsultasDeLineas.DtoAsync(db, linea, Max(reloj.HoyLocal, borrador.ValidFrom), ct));
        }, ct);
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}

// -------------------------------------------------------------------------------------------------------------- versionar --

/// <summary>
/// Registra una versión nueva de una línea (api.md §3.1, <c>POST /{id}/versions</c>): la anterior rige hasta la víspera (lectura
/// bitemporal); si la nueva pasa la usura, limpia el bloqueo por usura. Conserva el puntaje de la versión de la que parte (lo cambia
/// <c>SetCreditLineScoringCommand</c>). (nuevo)
/// </summary>
public sealed record AddCreditLineVersionCommand(Guid CreditLinePublicId, CreditLineVersionInput Version)
    : IRequest<Result<CreditLineVersionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public string Reason => Version?.Reason ?? string.Empty;
}

public sealed class AddCreditLineVersionCommandValidator : AbstractValidator<AddCreditLineVersionCommand>
{
    public AddCreditLineVersionCommandValidator()
    {
        RuleFor(x => x.CreditLinePublicId).NotEmpty();
        RuleFor(x => x.Version).NotNull().SetValidator(new CreditLineVersionInputValidator()!);
    }
}

/// <summary>
/// Línea inexistente o fuera del alcance → 404; cerrada → <c>Lending.CreditLine.Closed</c>; empieza antes de la última registrada →
/// <c>VersionOverlaps</c>; las demás reglas, las del alta. Los créditos vivos no se tocan.
/// </summary>
public sealed class AddCreditLineVersionCommandHandler(
    IApplicationDbContext db,
    ReglasDeLineaDeCredito reglas,
    AltaDeVersionDeLinea alta,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<AddCreditLineVersionCommand, Result<CreditLineVersionDto>>
{
    public async Task<Result<CreditLineVersionDto>> Handle(AddCreditLineVersionCommand request, CancellationToken ct)
    {
        var encontrada = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcance, auditoria, request.CreditLinePublicId, seguir: false, ct);
        if (encontrada.IsFailure) return Result.Failure<CreditLineVersionDto>(encontrada.Error);
        var linea = encontrada.Value;
        if (linea.Status == CreditLineStatus.Closed) return Result.Failure<CreditLineVersionDto>(ErroresDeLineasDeCredito.De(linea.ValidarActivacion()!));

        var aseguradoras = await ConsultasDeLineas.ResolverAseguradorasAsync(db, request.Version, ct);
        if (aseguradoras is null) return Result.Failure<CreditLineVersionDto>(ErroresDeGobierno.PersonaInexistente());
        var previa = await new VersionVigenteDeLinea(db).LeerVigenteOProximaAsync(linea.Id, request.Version.ValidFrom, ct);
        var borrador = request.Version.Borrador(aseguradoras, previa?.Version.ScoringModel, previa?.Version.MinimumScore);

        var contraLoRegistrado = await alta.ValidarContraLoRegistradoAsync(linea.Id, borrador, ct);
        if (contraLoRegistrado.IsFailure) return Result.Failure<CreditLineVersionDto>(contraLoRegistrado.Error);
        var verificada = await reglas.VerificarAsync(borrador, reloj.HoyLocal, ct);
        if (verificada.IsFailure) return Result.Failure<CreditLineVersionDto>(verificada.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var seguida = await db.CreditLines.FirstAsync(l => l.Id == linea.Id, ct);
            var version = await alta.RegistrarAsync(seguida, borrador, ct);
            return Result.Success(await VersionDtoAsync(db, version.Id, ct));
        }, ct);
    }

    /// <summary>La versión recién registrada con su fin de vigencia implícito.</summary>
    public static async Task<CreditLineVersionDto> VersionDtoAsync(IApplicationDbContext db, int versionId, CancellationToken ct)
    {
        var leida = (await new VersionVigenteDeLinea(db).LeerVersionAsync(versionId, ct))!;
        var versiones = await db.CreditLineVersions.AsNoTracking().Where(v => v.CreditLineId == leida.Version.CreditLineId && !v.IsDeleted).ToListAsync(ct);
        return CreditLineVersionDto.De(leida, VersionVigenteDeLinea.VigenteHasta(leida.Version, versiones),
            await ConsultasDeLineas.AseguradorasAsync(db, leida.Seguros, ct));
    }
}

// -------------------------------------------------------------------------------------------------------- cerrar y reabrir --

/// <summary>Cierra la línea a créditos nuevos con motivo (api.md §3.1, <c>POST /{id}/close</c>; FR-032). Los créditos vivos siguen. (nuevo)</summary>
public sealed record CloseCreditLineCommand(Guid CreditLinePublicId, string Reason) : IRequest<Result<CreditLineDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseCreditLineCommandValidator : ValidadorConMotivo<CloseCreditLineCommand>
{
    public CloseCreditLineCommandValidator() => RuleFor(x => x.CreditLinePublicId).NotEmpty();
}

/// <summary>Ya cerrada → <c>Lending.CreditLine.AlreadyClosed</c>. Quien cierra queda en <c>ClosedByUserId</c>.</summary>
public sealed class CloseCreditLineCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<CloseCreditLineCommand, Result<CreditLineDto>>
{
    public async Task<Result<CreditLineDto>> Handle(CloseCreditLineCommand request, CancellationToken ct)
    {
        var encontrada = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcance, auditoria, request.CreditLinePublicId, seguir: false, ct);
        if (encontrada.IsFailure) return Result.Failure<CreditLineDto>(encontrada.Error);
        if (encontrada.Value.ValidarCierre() is { } problema) return Result.Failure<CreditLineDto>(ErroresDeLineasDeCredito.De(problema));
        var actor = await actorActual.ObtenerAsync(ct);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var linea = await db.CreditLines.FirstAsync(l => l.Id == encontrada.Value.Id, ct);
            linea.Cerrar(CreditLineClosedReason.Manual, request.Reason, actor.UserId, reloj.UtcNow);
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeLineas.DtoAsync(db, linea, reloj.HoyLocal, ct));
        }, ct);
    }
}

/// <summary>Reabre una línea cerrada revalidando la usura del día (api.md §3.1, <c>POST /{id}/reopen</c>). (nuevo)</summary>
public sealed record ReopenCreditLineCommand(Guid CreditLinePublicId, string Reason) : IRequest<Result<CreditLineDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReopenCreditLineCommandValidator : ValidadorConMotivo<ReopenCreditLineCommand>
{
    public ReopenCreditLineCommandValidator() => RuleFor(x => x.CreditLinePublicId).NotEmpty();
}

/// <summary>
/// No cerrada → <c>Lending.CreditLine.NotClosed</c>. La versión vigente hoy (o la próxima, si la línea todavía no tiene una) pasa otra vez
/// la usura del día y la modalidad contra el catálogo vigente (una cerrada por modalidad retirada no se reabre si sigue retirada); si
/// pasa, la línea vuelve a <c>Active</c> y queda sin bloqueo por usura.
/// </summary>
public sealed class ReopenCreditLineCommandHandler(
    IApplicationDbContext db,
    ReglasDeLineaDeCredito reglas,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ReopenCreditLineCommand, Result<CreditLineDto>>
{
    public async Task<Result<CreditLineDto>> Handle(ReopenCreditLineCommand request, CancellationToken ct)
    {
        var encontrada = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcance, auditoria, request.CreditLinePublicId, seguir: false, ct);
        if (encontrada.IsFailure) return Result.Failure<CreditLineDto>(encontrada.Error);
        if (encontrada.Value.ValidarReapertura() is { } problema) return Result.Failure<CreditLineDto>(ErroresDeLineasDeCredito.De(problema));

        var hoy = reloj.HoyLocal;
        var version = await new VersionVigenteDeLinea(db).LeerVigenteOProximaAsync(encontrada.Value.Id, hoy, ct);
        if (version is not null)
        {
            var control = await reglas.ControlarUsuraAsync(version.Borrador(), hoy, ct);
            if (control.IsFailure) return Result.Failure<CreditLineDto>(control.Error);
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var linea = await db.CreditLines.FirstAsync(l => l.Id == encontrada.Value.Id, ct);
            linea.Reabrir();
            linea.DesbloquearPorUsura();
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeLineas.DtoAsync(db, linea, hoy, ct));
        }, ct);
    }
}

// ----------------------------------------------------------------------------------------------------------------- borrar --

/// <summary>
/// Borra (lógicamente) una línea que nunca se usó (api.md §3.1, <c>DELETE /{id}</c>): sin solicitudes, créditos, cupos, reservas de
/// número ni ventas de Comercial que la nombren. Si no, <c>Lending.CreditLine.HasLoans</c> con <c>data.alternatives = ["Close"]</c>. Sus
/// versiones son hechos y quedan. (nuevo)
/// </summary>
public sealed record DeleteCreditLineCommand(Guid CreditLinePublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteCreditLineCommandValidator : ValidadorConMotivo<DeleteCreditLineCommand>
{
    public DeleteCreditLineCommandValidator() => RuleFor(x => x.CreditLinePublicId).NotEmpty();
}

public sealed class DeleteCreditLineCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<DeleteCreditLineCommand, Result>
{
    public async Task<Result> Handle(DeleteCreditLineCommand request, CancellationToken ct)
    {
        var encontrada = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcance, auditoria, request.CreditLinePublicId, seguir: false, ct);
        if (encontrada.IsFailure) return Result.Failure(encontrada.Error);
        var id = encontrada.Value.Id;
        var codigo = encontrada.Value.Code;

        var usada = await db.CreditApplications.IgnoreQueryFilters().AnyAsync(a => a.CreditLineId == id, ct)
            || await db.Loans.IgnoreQueryFilters().AnyAsync(l => l.CreditLineId == id, ct)
            || await db.CreditLimits.IgnoreQueryFilters().AnyAsync(c => c.CreditLineId == id, ct)
            || await db.LoanNumberReservations.IgnoreQueryFilters().AnyAsync(r => r.CreditLineId == id, ct)
            || await db.LendingInboundMessageEvaluations.IgnoreQueryFilters().AnyAsync(e => e.CreditLineCode == codigo, ct);
        if (usada) return Result.Failure(ErroresDeLineasDeCredito.ConCreditos());

        var actor = await actorActual.ObtenerAsync(ct);
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var linea = await db.CreditLines.FirstAsync(l => l.Id == id, ct);
            linea.IsDeleted = true;
            linea.DeletedAt = reloj.UtcNow;
            linea.DeletedBy = actor.Name;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);
    }
}
