using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Scoring;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Configuration;

// Feature 013, US1 (T0354; api.md §3.3; FR-037): el bloque de puntaje de una línea vive en su versión. Cambiarlo registra una versión
// nueva que copia todo lo demás de la que rige en esa fecha, con su permiso propio (Lending.Scoring.Manage). Un puntaje bajo el mínimo
// nunca rechaza: sube la solicitud al nivel de excepción (US2). (nuevo)

/// <summary>
/// Registra una versión de la línea con un bloque de puntaje nuevo (api.md §3.3, <c>POST /credit-lines/{id}/scoring</c>). Sin variables
/// la versión queda sin puntaje. (nuevo)
/// </summary>
public sealed record SetCreditLineScoringCommand(
    Guid CreditLinePublicId,
    DateOnly ValidFrom,
    string Reason,
    decimal? MinimumScore,
    string? ExceptionLevelCode,
    IReadOnlyList<ScoringVariableDto> Variables) : IRequest<Result<CreditLineVersionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetCreditLineScoringCommandValidator : ValidadorConMotivo<SetCreditLineScoringCommand>
{
    public SetCreditLineScoringCommandValidator()
    {
        RuleFor(x => x.CreditLinePublicId).NotEmpty();
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige el puntaje.");
        RuleFor(x => x.Variables).NotNull();
        RuleFor(x => x.MinimumScore!.Value).PrecisionScale(12, ModeloDePuntaje.DecimalesDelPuntaje, true)
            .WithMessage("El puntaje mínimo admite hasta 4 decimales.").When(x => x.MinimumScore is not null);
        RuleFor(x => x.ExceptionLevelCode).MaximumLength(10);
        RuleForEach(x => x.Variables).ChildRules(v =>
        {
            v.RuleFor(y => y.Code).NotEmpty().MaximumLength(20).WithMessage("El código de la variable lleva hasta 20 caracteres.");
            v.RuleFor(y => y.Name).NotEmpty().MaximumLength(120);
            v.RuleFor(y => y.Source).NotEmpty().WithMessage("Elegí la fuente de la variable.");
            v.RuleFor(y => y.Ranges).NotEmpty().WithMessage("Cada variable lleva al menos un rango con sus puntos.");
        });
    }
}

/// <summary>
/// Línea inexistente o fuera del alcance → 404; cerrada → <c>Lending.CreditLine.Closed</c>; pesos fuera de 0..1 o que no suman 1 →
/// <c>Lending.Scoring.WeightsInvalid</c> (<c>data.reason</c>); una fuente fuera de <see cref="FuentesDePuntaje"/> →
/// <c>Lending.Scoring.VariableUnknown</c>; un nivel de excepción que la matriz vigente no tiene → <c>Lending.Attribution.LevelUnknown</c>;
/// empieza antes de la última versión → <c>VersionOverlaps</c>. La versión nueva pasa las mismas reglas que cualquier otra (usura incluida).
/// </summary>
public sealed class SetCreditLineScoringCommandHandler(
    IApplicationDbContext db,
    ReglasDeLineaDeCredito reglas,
    AltaDeVersionDeLinea alta,
    MatrizVigenteDeAtribuciones matrices,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<SetCreditLineScoringCommand, Result<CreditLineVersionDto>>
{
    public async Task<Result<CreditLineVersionDto>> Handle(SetCreditLineScoringCommand request, CancellationToken ct)
    {
        var encontrada = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcance, auditoria, request.CreditLinePublicId, seguir: false, ct);
        if (encontrada.IsFailure) return Result.Failure<CreditLineVersionDto>(encontrada.Error);
        var linea = encontrada.Value;
        if (linea.Status == CreditLineStatus.Closed) return Result.Failure<CreditLineVersionDto>(ErroresDeLineasDeCredito.De(linea.ValidarActivacion()!));

        string? puntaje = null;
        if (request.Variables.Count > 0)
        {
            var modelo = new ModeloDePuntaje(request.ExceptionLevelCode, request.Variables.Select(v => v.Modelo()).ToList());
            if (modelo.Validar() is { } problema) return Result.Failure<CreditLineVersionDto>(ErroresDeLineasDeCredito.De(problema));
            if (!string.IsNullOrWhiteSpace(request.ExceptionLevelCode))
            {
                var nivel = request.ExceptionLevelCode.Trim().ToUpperInvariant();
                var matriz = await matrices.LeerAsync(request.ValidFrom, reloj.UtcNow, ct);
                if (matriz is null || matriz.Niveles.All(n => !string.Equals(n.Code, nivel, StringComparison.OrdinalIgnoreCase)))
                    return Result.Failure<CreditLineVersionDto>(ErroresDeLineasDeCredito.NivelDesconocido(nivel));
            }
            puntaje = modelo.Escribir();
        }
        else if (request.MinimumScore is not null)
        {
            return Result.Failure<CreditLineVersionDto>(ErroresDeLineasDeCredito.Invalido("minimumScore", "Un puntaje mínimo necesita las variables que lo calculan."));
        }

        var previa = await new VersionVigenteDeLinea(db).LeerVigenteOProximaAsync(linea.Id, request.ValidFrom, ct);
        if (previa is null) return Result.Failure<CreditLineVersionDto>(ErroresDeLineasDeCredito.LineaInexistente());
        // Copia todo lo demás de la versión que rige en esa fecha y cambia sólo el puntaje (con su fecha y su motivo).
        var borrador = previa.Borrador() with
        {
            ValidFrom = request.ValidFrom,
            Reason = request.Reason.Trim(),
            ScoringModel = puntaje,
            MinimumScore = puntaje is null ? null : request.MinimumScore,
        };

        var contraLoRegistrado = await alta.ValidarContraLoRegistradoAsync(linea.Id, borrador, ct);
        if (contraLoRegistrado.IsFailure) return Result.Failure<CreditLineVersionDto>(contraLoRegistrado.Error);
        var verificada = await reglas.VerificarAsync(borrador, reloj.HoyLocal, ct);
        if (verificada.IsFailure) return Result.Failure<CreditLineVersionDto>(verificada.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var seguida = await db.CreditLines.FirstAsync(l => l.Id == linea.Id, ct);
            var version = await alta.RegistrarAsync(seguida, borrador, ct);
            return Result.Success(await AddCreditLineVersionCommandHandler.VersionDtoAsync(db, version.Id, ct));
        }, ct);
    }
}

/// <summary>El bloque de puntaje de la versión vigente en <c>date</c> (api.md §3.3, <c>GET /credit-lines/{id}/scoring</c>). (nuevo)</summary>
public sealed record GetCreditLineScoringQuery(Guid CreditLinePublicId, DateOnly? Date = null) : IRequest<Result<CreditLineScoringDto>>;

public sealed class GetCreditLineScoringQueryValidator : AbstractValidator<GetCreditLineScoringQuery>
{
    public GetCreditLineScoringQueryValidator() => RuleFor(x => x.CreditLinePublicId).NotEmpty();
}

/// <summary>Las líneas son el eje del alcance: una fuera del <c>AlcanceDeCartera</c> de quien pregunta responde el 404 de la línea.</summary>
public sealed class GetCreditLineScoringQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetCreditLineScoringQuery, Result<CreditLineScoringDto>>
{
    public async Task<Result<CreditLineScoringDto>> Handle(GetCreditLineScoringQuery request, CancellationToken ct)
    {
        var linea = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcanceDeLaPeticion, auditoria, request.CreditLinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<CreditLineScoringDto>(linea.Error);
        var version = await new VersionVigenteDeLinea(db).LeerVigenteOProximaAsync(linea.Value.Id, request.Date ?? reloj.HoyLocal, ct);
        if (version is null) return Result.Failure<CreditLineScoringDto>(ErroresDeLineasDeCredito.LineaInexistente());
        var modelo = ModeloDePuntaje.Leer(version.Version.ScoringModel);
        return Result.Success(new CreditLineScoringDto(version.Version.PublicId, version.Version.ValidFrom, version.Version.MinimumScore,
            modelo?.ExceptionLevelCode, modelo?.Variables.Select(ScoringVariableDto.De).ToList() ?? [], FuentesDePuntaje.Todas));
    }
}
