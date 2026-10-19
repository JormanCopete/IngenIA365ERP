using FluentValidation;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>Lo que responde la revisión de los cobros provisionales (api.md §19.3). (nuevo)</summary>
public sealed record ProvisionalCollectionsReviewDto(
    Guid StagedAttachmentPublicId,
    string Sha256,
    int Rows,
    decimal Total,
    IReadOnlyList<ErrorDeFila> Errors,
    IReadOnlyList<ErrorDeFila> Warnings);

/// <summary>
/// La revisión previa de los cobros provisionales antes de activar Cartera (feature 013, US8, T0748; C62; plantillas.md §6; api.md §19.3,
/// <c>POST /api/lending/activation/provisional-collections/review</c>, <c>Lending.Activation.Execute</c>). Lleva clave porque escribe el adjunto. (nuevo)
/// </summary>
public sealed record ReviewProvisionalCollectionsCommand(ArchivoDeImportacion File)
    : IRequest<Result<ProvisionalCollectionsReviewDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReviewProvisionalCollectionsCommandValidator : AbstractValidator<ReviewProvisionalCollectionsCommand>
{
    public ReviewProvisionalCollectionsCommandValidator() => RuleFor(x => x.File).NotNull().WithMessage("Adjunte el archivo de cobros provisionales.");
}

/// <summary>
/// Corre <see cref="ValidacionDeCobrosProvisionales"/> contra las ventas acumuladas confirmadas después del corte de la carga aplicada; con
/// errores responde 422 <c>Import.Invalid</c> (o <c>Lending.ProvisionalCollection.ExceedsSale</c> si sólo hay cobros que superan su venta) y no
/// guarda nada; sin errores guarda el archivo como adjunto <c>Direct</c> de dueño de módulo, no borrable, cuyo dueño es la carga, y devuelve su
/// <c>PublicId</c>, el SHA-256, las filas y el total. <b>No guarda filas</b>: las guarda la activación, que vuelve a validar el adjunto en su
/// transacción. Con Cartera activada, <c>Lending.Activation.AlreadyActivated</c>; sin carga aplicada, <c>Lending.Activation.RequirementsMissing</c>
/// con <c>LoadApplied</c>. (nuevo)
/// </summary>
public sealed class ReviewProvisionalCollectionsCommandHandler(
    IApplicationDbContext db,
    ValidacionDeCobrosProvisionales validacion,
    ISender sender,
    IDateTimeService reloj) : IRequestHandler<ReviewProvisionalCollectionsCommand, Result<ProvisionalCollectionsReviewDto>>
{
    public const string TipoCsv = "text/csv";
    public const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<Result<ProvisionalCollectionsReviewDto>> Handle(ReviewProvisionalCollectionsCommand request, CancellationToken ct)
    {
        var setup = await db.LendingSetups.AsNoTracking().FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct);
        if (setup?.ActivationDate is { } activada) return Result.Failure<ProvisionalCollectionsReviewDto>(ErroresDeActivacion.YaActivada(activada));
        var carga = await db.MigrationLoads.AsNoTracking().Where(l => l.Status == MigrationLoadStatus.Applied && !l.IsDeleted && l.Nature == LineNature.Placement).FirstOrDefaultAsync(ct);
        if (carga is null)
            return Result.Failure<ProvisionalCollectionsReviewDto>(ErroresDeActivacion.FaltanRequisitos(
            [
                new RequisitoDeActivacionDto(RequisitosDeActivacion.CargaAplicada, false, "aplicar una carga de migración (aunque no traiga créditos)",
                    new IngenIA365ERP.Application.Common.Integration.Accounting.QuienCorrigeDto("Cartera", "/cartera/migracion", "Lending.Migration.Load"), EstadosDeRequisito.Missing),
            ]));

        var revisados = await validacion.ValidarAsync(request.File, carga.CutoffDate, reloj.HoyLocal, ct);
        if (revisados.IsFailure) return Result.Failure<ProvisionalCollectionsReviewDto>(revisados.Error);
        if (revisados.Value.HayErrores) return Result.Failure<ProvisionalCollectionsReviewDto>(ValidacionDeCobrosProvisionales.Error(revisados.Value));

        var tipo = request.File.NombreArchivo.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? TipoCsv : TipoXlsx;
        var subida = await sender.Send(new UploadAttachmentCommand(ValidacionDeCobrosProvisionales.AdjuntosDeCobros, carga.PublicId,
            request.File.NombreArchivo, tipo, request.File.Contenido), ct);
        if (subida.IsFailure) return Result.Failure<ProvisionalCollectionsReviewDto>(subida.Error);

        var cobros = revisados.Value.Cobros;
        return Result.Success(new ProvisionalCollectionsReviewDto(subida.Value, revisados.Value.Sha256, cobros.Count, cobros.Sum(c => c.Valor),
            revisados.Value.Resultado.Errors, revisados.Value.Resultado.Warnings));
    }
}
