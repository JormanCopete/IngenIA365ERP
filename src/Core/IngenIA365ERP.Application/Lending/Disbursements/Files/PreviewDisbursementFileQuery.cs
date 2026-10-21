using FluentValidation;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Disbursements.Files;

/// <summary>
/// La vista previa del archivo de desembolsos (feature 013, US3, T0454; api.md §9.3, <c>POST /api/lending/disbursement-files/preview</c>,
/// <c>Lending.DisbursementFiles.Generate</c>, sin clave: no escribe): las líneas como quedarían con el formato y la cuenta elegidos —el lote todavía
/// no tiene número—, las órdenes pendientes con su motivo (<c>BankCodeMissing</c>, <c>NoBankAccount</c>, <c>AlreadyInFile</c>, <c>NotEligible</c> o
/// el campo obligatorio vacío) y el total. Sin órdenes pedidas, todas las elegibles del alcance. (nuevo)
/// </summary>
public sealed record PreviewDisbursementFileQuery(Guid FormatPublicId, Guid? SourceAccountPublicId, IReadOnlyList<Guid>? OrderPublicIds = null,
    DateOnly? PaymentDate = null) : IRequest<Result<DisbursementFilePreviewDto>>;

public sealed class PreviewDisbursementFileQueryValidator : AbstractValidator<PreviewDisbursementFileQuery>
{
    public PreviewDisbursementFileQueryValidator() => RuleFor(x => x.FormatPublicId).NotEmpty().WithMessage("Elija el formato del archivo.");
}

public sealed class PreviewDisbursementFileQueryHandler(PreparacionDeArchivoDeDesembolsos preparacion)
    : IRequestHandler<PreviewDisbursementFileQuery, Result<DisbursementFilePreviewDto>>
{
    public async Task<Result<DisbursementFilePreviewDto>> Handle(PreviewDisbursementFileQuery request, CancellationToken ct)
    {
        var fecha = request.PaymentDate ?? preparacion.Hoy;
        var prep = await preparacion.PrepararAsync(request.FormatPublicId, request.SourceAccountPublicId, request.OrderPublicIds, fecha,
            PreparacionDeArchivoDeDesembolsos.LoteDeVistaPrevia, 1, ct);
        if (prep.IsFailure) return Result.Failure<DisbursementFilePreviewDto>(prep.Error);
        var p = prep.Value;
        var incluidos = p.Incluidos;
        try
        {
            var salida = FlatFileWriter.Escribir(p.Formato, p.Contexto, incluidos.Select(c => c.Valores).ToList());
            var lineas = salida.Text.Split(p.Formato.LineEnding == BankFileLineEnding.Lf ? "\n" : "\r\n", StringSplitOptions.RemoveEmptyEntries);
            return Result.Success(new DisbursementFilePreviewDto(salida.FileName, lineas, p.Pendientes, salida.TotalAmount));
        }
        catch (BankFileWriteException ex)
        {
            var orden = ex.LineNumber >= 1 && ex.LineNumber <= incluidos.Count ? incluidos[ex.LineNumber - 1].Orden.Number : "?";
            return Result.Failure<DisbursementFilePreviewDto>(ErroresDeArchivoDeDesembolsos.NoCabe(ex.LineNumber, ex.Field, orden));
        }
    }
}
