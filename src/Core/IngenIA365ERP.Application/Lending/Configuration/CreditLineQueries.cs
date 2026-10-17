using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Configuration;

// Feature 013, US1 (T0353; api.md §3.1): las consultas de líneas. Las líneas son el eje del alcance por línea (T62): la lista filtra en
// silencio por el AlcanceDeCartera de quien pregunta y el detalle, la versión y la consulta de usura de una línea fuera de él responden
// el mismo 404 que una inexistente (y dejan Lending.Access.OutOfScope). (nuevo)

/// <summary>
/// Las líneas de la cooperativa (api.md §3.1, <c>GET /api/lending/credit-lines</c>, <c>Lending.CreditLines.View</c>): por código, sólo las
/// del alcance; sin <c>includeClosed</c>, las cerradas no salen. Cada una trae su versión vigente hoy con la holgura de usura de su plan
/// tipo (nula si falta la usura). (nuevo)
/// </summary>
public sealed record ListCreditLinesQuery(
    CreditLineStatus? Status = null,
    string? Modality = null,
    bool? CommercialOrigin = null,
    bool? IncludeClosed = null) : IRequest<Result<IReadOnlyList<CreditLineListItemDto>>>;

public sealed class ListCreditLinesQueryValidator : AbstractValidator<ListCreditLinesQuery>
{
    public ListCreditLinesQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Modality).MaximumLength(40);
    }
}

public sealed class ListCreditLinesQueryHandler(
    IApplicationDbContext db,
    ReglasDeLineaDeCredito reglas,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IDateTimeService reloj) : IRequestHandler<ListCreditLinesQuery, Result<IReadOnlyList<CreditLineListItemDto>>>
{
    public async Task<Result<IReadOnlyList<CreditLineListItemDto>>> Handle(ListCreditLinesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted).PorLinea(alcance, l => l.Id);
        if (request.Status is { } estado) consulta = consulta.Where(l => l.Status == estado);
        else if (request.IncludeClosed != true) consulta = consulta.Where(l => l.Status != CreditLineStatus.Closed);
        var lineas = await consulta.OrderBy(l => l.Code).ToListAsync(ct);

        var hoy = reloj.HoyLocal;
        var vigentes = await new VersionVigenteDeLinea(db).LeerVariasAsync(lineas.Select(l => l.Id).ToList(), hoy, null, ct);
        var lista = new List<CreditLineListItemDto>();
        foreach (var linea in lineas)
        {
            var vigente = vigentes.GetValueOrDefault(linea.Id);
            var v = vigente?.Version;
            if (request.Modality is { Length: > 0 } modalidad && !string.Equals(v?.ModalityCode, modalidad.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            if (request.CommercialOrigin is { } comercial && (v?.AllowsCommercialOrigin ?? false) != comercial) continue;

            var holgura = vigente is null ? null : await reglas.HolguraAsync(vigente.Borrador(), hoy, ct);
            lista.Add(new CreditLineListItemDto(linea.PublicId, linea.Code, linea.Name, linea.Purpose, linea.Status, linea.ClosedReason,
                linea.UsuryBlockedSince, v?.ModalityCode, v?.SubsegmentCode, v?.AllowsCommercialOrigin ?? false,
                v is null ? null : new CreditLineCurrentVersionDto(v.PublicId, v.ValidFrom, v.RateKind, v.AnnualRate, v.IndexCode, v.Margin, holgura)));
        }
        return Result.Success<IReadOnlyList<CreditLineListItemDto>>(lista);
    }
}

/// <summary>Una línea con la versión vigente en <c>date</c> (por defecto hoy) y su historial (api.md §3.1, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetCreditLineQuery(Guid CreditLinePublicId, DateOnly? Date = null) : IRequest<Result<CreditLineDto>>;

public sealed class GetCreditLineQueryValidator : AbstractValidator<GetCreditLineQuery>
{
    public GetCreditLineQueryValidator() => RuleFor(x => x.CreditLinePublicId).NotEmpty();
}

public sealed class GetCreditLineQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    Housing.LectorDeVivienda? vivienda = null) : IRequestHandler<GetCreditLineQuery, Result<CreditLineDto>>
{
    public async Task<Result<CreditLineDto>> Handle(GetCreditLineQuery request, CancellationToken ct)
    {
        var linea = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcanceDeLaPeticion, auditoria, request.CreditLinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<CreditLineDto>(linea.Error);
        var fecha = request.Date ?? reloj.HoyLocal;
        var dto = await ConsultasDeLineas.DtoAsync(db, linea.Value, fecha, ct);
        // K6b (bloque 2): una línea de vivienda muestra el tope y los límites de la Ley 546 vigentes a la fecha con su fuente.
        if (vivienda is not null && Housing.LectorDeVivienda.Disponible()
            && string.Equals(dto.CurrentVersion?.ModalityCode, ReglasDeLineaDeCredito.ModalidadDeVivienda, StringComparison.OrdinalIgnoreCase))
            dto = dto with { Housing = await Housing.CreditLineHousingDto.LeerAsync(vivienda, fecha, ct) };
        return Result.Success(dto);
    }
}

/// <summary>Una versión completa de una línea (api.md §3.1, <c>GET /{id}/versions/{versionId}</c>). (nuevo)</summary>
public sealed record GetCreditLineVersionQuery(Guid CreditLinePublicId, Guid VersionPublicId) : IRequest<Result<CreditLineVersionDto>>;

public sealed class GetCreditLineVersionQueryValidator : AbstractValidator<GetCreditLineVersionQuery>
{
    public GetCreditLineVersionQueryValidator()
    {
        RuleFor(x => x.CreditLinePublicId).NotEmpty();
        RuleFor(x => x.VersionPublicId).NotEmpty();
    }
}

public sealed class GetCreditLineVersionQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria) : IRequestHandler<GetCreditLineVersionQuery, Result<CreditLineVersionDto>>
{
    public async Task<Result<CreditLineVersionDto>> Handle(GetCreditLineVersionQuery request, CancellationToken ct)
    {
        var linea = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcanceDeLaPeticion, auditoria, request.CreditLinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<CreditLineVersionDto>(linea.Error);
        var id = await db.CreditLineVersions.AsNoTracking()
            .Where(v => v.PublicId == request.VersionPublicId && v.CreditLineId == linea.Value.Id && !v.IsDeleted)
            .Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        return id is { } versionId
            ? Result.Success(await AddCreditLineVersionCommandHandler.VersionDtoAsync(db, versionId, ct))
            : Result.Failure<CreditLineVersionDto>(ErroresDeLineasDeCredito.LineaInexistente());
    }
}

/// <summary>
/// La consulta de usura de una línea (api.md §3.1, <c>POST /{id}/usury-check</c>, <c>Lending.CreditLines.View</c>, sin clave): la versión
/// vigente en <c>date</c>, un plan con el monto, las cuotas y la periodicidad pedidos (o los máximos y la de defecto) y la banda que casa;
/// devuelve la efectiva con y sin cargos, la usura, la holgura, los cargos que computan y la explicación. No escribe. Sin usura del día,
/// <c>NationalValues.Missing</c>. (nuevo)
/// </summary>
public sealed record CheckCreditLineUsuryQuery(
    Guid CreditLinePublicId,
    DateOnly? Date = null,
    decimal? Amount = null,
    int? TermInstallments = null,
    RepaymentPeriodicity? Periodicity = null) : IRequest<Result<CreditLineUsuryCheckDto>>;

public sealed class CheckCreditLineUsuryQueryValidator : AbstractValidator<CheckCreditLineUsuryQuery>
{
    public CheckCreditLineUsuryQueryValidator()
    {
        RuleFor(x => x.CreditLinePublicId).NotEmpty();
        RuleFor(x => x.Amount!.Value).GreaterThan(0m).PrecisionScale(18, 2, true)
            .WithMessage("El monto es mayor que cero y admite hasta 2 decimales.").When(x => x.Amount is not null);
        RuleFor(x => x.TermInstallments!.Value).GreaterThan(0).WithMessage("El número de cuotas es de al menos una.").When(x => x.TermInstallments is not null);
        RuleFor(x => x.Periodicity).IsInEnum().When(x => x.Periodicity is not null);
    }
}

public sealed class CheckCreditLineUsuryQueryHandler(
    IApplicationDbContext db,
    ReglasDeLineaDeCredito reglas,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<CheckCreditLineUsuryQuery, Result<CreditLineUsuryCheckDto>>
{
    public async Task<Result<CreditLineUsuryCheckDto>> Handle(CheckCreditLineUsuryQuery request, CancellationToken ct)
    {
        var linea = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcanceDeLaPeticion, auditoria, request.CreditLinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<CreditLineUsuryCheckDto>(linea.Error);

        var fecha = request.Date ?? reloj.HoyLocal;
        var version = await new VersionVigenteDeLinea(db).LeerVigenteOProximaAsync(linea.Value.Id, fecha, ct);
        if (version is null) return Result.Failure<CreditLineUsuryCheckDto>(ErroresDeLineasDeCredito.LineaInexistente());
        var borrador = version.Borrador();
        if (request.Periodicity is { } p && !borrador.Periodicities.Contains(p))
            return Result.Failure<CreditLineUsuryCheckDto>(ErroresDeLineasDeCredito.Invalido("periodicity", "La línea no admite esa periodicidad."));

        var consulta = await reglas.ConsultarAsync(borrador, fecha, request.Amount, request.TermInstallments, request.Periodicity, ct);
        if (consulta.IsFailure) return Result.Failure<CreditLineUsuryCheckDto>(consulta.Error);
        var (control, tasa, combinacion) = consulta.Value;
        var r = control.Resultado;
        return Result.Success(new CreditLineUsuryCheckDto(fecha, version.Version.PublicId, combinacion.ModalityCode, r.ClaveDeUsura, control.Campo, tasa,
            ErroresDeLineasDeCredito.Tasa(r.EfectivaConCargos), ErroresDeLineasDeCredito.Tasa(r.EfectivaSinCargos),
            ErroresDeLineasDeCredito.Tasa(r.SobretasaDeCargos), r.Usura, r.UsuraPublicId,
            r.Holgura is { } h ? ErroresDeLineasDeCredito.Tasa(h) : null,
            r.Cargos.Select(c => new UsuryChargeDto(c.Codigo, c.Valor)).ToList(), r.Explicacion.Lineas.ToList(), r.Pasa));
    }
}
