using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Regulatory.Captaciones;

// Feature 014, US17 (T221; api.md §18.2; contracts/plantillas-y-archivos.md §8.3; FR-111, FR-112): validar y generar un formato regulatorio de captaciones
// con la mecánica de la 013 (GeneracionDeArchivoRegulatorio): validar no guarda; generar con bloqueantes deja una versión Validated sin archivo; con
// advertencias (la primera, casi siempre «formato sin cotejar») exige reconocerlas; regenerar deja la anterior Superseded; listar, descargar y marcar
// cargado son las rutas de /files. Los formatos son de toda la cooperativa: exigen alcance total. (nuevo)

/// <summary><c>POST /api/lending/regulatory/deposit-formats/{formatCode}/validate</c>: el formato del período sin guardar nada. (nuevo)</summary>
public sealed record ValidateDepositRegulatoryFileQuery(string FormatCode, int Year, int? Month) : IRequest<Result<RegulatoryValidationDto>>;

public sealed class ValidateDepositRegulatoryFileQueryValidator : AbstractValidator<ValidateDepositRegulatoryFileQuery>
{
    public ValidateDepositRegulatoryFileQueryValidator()
    {
        RuleFor(x => x.FormatCode).NotEmpty().WithMessage("Indique el formato.").MaximumLength(Domain.Entities.Lending.Documents.RegulatoryFile.LargoDelFormato);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999).WithMessage("Indique el año del período.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).When(x => x.Month is not null).WithMessage("El mes del período va de 1 a 12.");
    }
}

public sealed class ValidateDepositRegulatoryFileQueryHandler(PreparacionDeArchivoRegulatorio preparacion, IAlcanceDeCartera alcance)
    : IRequestHandler<ValidateDepositRegulatoryFileQuery, Result<RegulatoryValidationDto>>
{
    public async Task<Result<RegulatoryValidationDto>> Handle(ValidateDepositRegulatoryFileQuery request, CancellationToken ct)
    {
        if (await AlcanceDeFormatosDeCaptaciones.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<RegulatoryValidationDto>(fuera);
        var p = await preparacion.CaptacionesAsync(request.FormatCode, request.Year, request.Month, ct);
        return p.IsFailure ? Result.Failure<RegulatoryValidationDto>(p.Error) : Result.Success(ArchivosRegulatorios.Validacion(p.Value));
    }
}

/// <summary><c>POST /api/lending/regulatory/deposit-formats/{formatCode}/generate</c> (con clave): la versión siguiente del formato del período. (nuevo)</summary>
public sealed record GenerateDepositRegulatoryFileCommand(string FormatCode, int Year, int? Month, bool AcknowledgeWarnings = false)
    : IRequest<Result<RegulatoryFileDetailDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class GenerateDepositRegulatoryFileCommandValidator : AbstractValidator<GenerateDepositRegulatoryFileCommand>
{
    public GenerateDepositRegulatoryFileCommandValidator()
    {
        RuleFor(x => x.FormatCode).NotEmpty().WithMessage("Indique el formato.").MaximumLength(Domain.Entities.Lending.Documents.RegulatoryFile.LargoDelFormato);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999).WithMessage("Indique el año del período.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).When(x => x.Month is not null).WithMessage("El mes del período va de 1 a 12.");
    }
}

public sealed class GenerateDepositRegulatoryFileCommandHandler(GeneracionDeArchivoRegulatorio generacion, IAlcanceDeCartera alcance)
    : IRequestHandler<GenerateDepositRegulatoryFileCommand, Result<RegulatoryFileDetailDto>>
{
    public async Task<Result<RegulatoryFileDetailDto>> Handle(GenerateDepositRegulatoryFileCommand request, CancellationToken ct)
    {
        if (await AlcanceDeFormatosDeCaptaciones.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<RegulatoryFileDetailDto>(fuera);
        var formato = FormatosDeCaptaciones.Buscar(request.FormatCode);
        if (formato is null) return Result.Failure<RegulatoryFileDetailDto>(ErroresDeFormatosDeCaptaciones.FormatoDesconocido(request.FormatCode));
        if (!formato.Anual && request.Month is null) return Result.Failure<RegulatoryFileDetailDto>(ErroresDeFormatosDeCaptaciones.MesRequerido(formato));
        return await generacion.GenerarAsync(formato.Clase, null, request.Year, formato.MesDelPeriodo(request.Month), request.AcknowledgeWarnings, ct,
            formato.Codigo);
    }
}

/// <summary>Los formatos regulatorios de captaciones son de toda la cooperativa: sólo con alcance total. (nuevo)</summary>
public static class AlcanceDeFormatosDeCaptaciones
{
    /// <summary>Nulo si quien pregunta tiene alcance total; si no, el error.</summary>
    public static async Task<Error?> TotalAsync(IAlcanceDeCartera alcance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(alcance);
        var vigente = await alcance.ObtenerAsync(ct);
        return vigente.TodasLasOficinas && vigente.TodasLasLineas ? null : ErroresDeFormatosDeCaptaciones.SinAlcanceTotal();
    }
}
