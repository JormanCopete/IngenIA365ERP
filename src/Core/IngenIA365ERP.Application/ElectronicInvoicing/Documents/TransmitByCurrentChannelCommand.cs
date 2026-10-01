using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>El documento con su canal nuevo: antes y después. (nuevo)</summary>
public sealed record CambioDeCanalDto(Guid ElectronicDocumentPublicId, string PreviousChannelCode, string ChannelCode, ElectronicDocumentStatus Status);

/// <summary>
/// Transmitir por el canal vigente un documento cuyo canal sellado se retiró (feature 012, I4, T714; FR-064; contracts/dian.md §10.5; api.md
/// §24.4 <c>POST /documents/{id}/transmit-by-current-channel</c>). Cada documento sale por el canal con que se numeró; esto es la excepción y
/// <b>nunca es automática</b>: la pide una persona con motivo. Sólo procede si
/// <list type="bullet">
/// <item>el canal sellado está <b>retirado</b>: su configuración ya no está vigente y la vigente es de otro canal (o su adaptador ya no está);</item>
/// <item>la configuración vigente es del mismo ambiente (el número es único por ambiente);</item>
/// <item>la resolución con que se numeró está <b>asociada</b> al canal vigente a la fecha del documento (las notas, con su consecutivo propio,
/// no tienen resolución que asociar);</item>
/// <item>el documento no es final ni está <c>Sent</c> (lo enviado se consulta por donde se envió).</item>
/// </list>
/// Si no, <c>ElectronicInvoicing.Document.ChannelNotLinked</c>. Cambia la configuración, el modo, el canal, el software y quién entrega el correo
/// de la fila; la auditoría de la fila guarda antes y después, con el motivo de la operación. (nuevo)
/// </summary>
public sealed record TransmitByCurrentChannelCommand(Guid ElectronicDocumentPublicId, string Reason)
    : IRequest<Result<CambioDeCanalDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class TransmitByCurrentChannelCommandValidator : ValidadorConMotivo<TransmitByCurrentChannelCommand>
{
    public TransmitByCurrentChannelCommandValidator() => RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
}

public sealed class TransmitByCurrentChannelCommandHandler(IApplicationDbContext db, ICanalesDeEmision canales, IDateTimeService reloj)
    : IRequestHandler<TransmitByCurrentChannelCommand, Result<CambioDeCanalDto>>
{
    public async Task<Result<CambioDeCanalDto>> Handle(TransmitByCurrentChannelCommand request, CancellationToken ct)
    {
        var documento = await db.ElectronicDocuments
            .Include(d => d.EmissionSetting)
            .Include(d => d.Resolution!).ThenInclude(r => r.Channels)
            .FirstOrDefaultAsync(d => d.PublicId == request.ElectronicDocumentPublicId, ct);
        if (documento is null) return Falla(ErroresDeDocumentosElectronicos.NotFound());
        if (documento.EsFinal)
            return Falla(new Error(TransicionesDelDocumentoElectronico.CodigoFinal, $"El documento está {documento.Status}: no cambia más."));
        if (documento.Status == ElectronicDocumentStatus.Sent)
            return Falla(new Error(TransicionesDelDocumentoElectronico.CodigoEsperaRespuesta,
                "El documento se envió y no tiene respuesta: se consulta por el canal con que se envió."));

        var hoy = reloj.HoyLocal;
        var vigente = (await db.ElectronicEmissionSettings.Where(s => s.IsEnabled).ToListAsync(ct))
            .Where(s => s.VigenteEn(hoy)).OrderByDescending(s => s.ValidFrom).FirstOrDefault();
        var sellado = ReglasDeResolucion.Canal(documento.ChannelCode);
        if (vigente is null)
            return Falla(ErroresDeDocumentosElectronicos.ChannelNotLinked("No hay una configuración de emisión vigente a la cual pasar el documento."));

        var nuevo = ReglasDeResolucion.Canal(vigente.ChannelCode);
        var adaptadorRetirado = !canales.Codigos.Contains(sellado, StringComparer.OrdinalIgnoreCase);
        var configuracionRetirada = documento.EmissionSetting is { } s0 && !s0.VigenteEn(hoy);
        if (string.Equals(nuevo, sellado, StringComparison.OrdinalIgnoreCase) || !(adaptadorRetirado || configuracionRetirada))
            return Falla(ErroresDeDocumentosElectronicos.ChannelNotLinked(
                $"El canal {sellado} con que se numeró el documento sigue vigente: el documento sale por ese canal."));
        if (vigente.Environment != documento.Environment)
            return Falla(ErroresDeDocumentosElectronicos.ChannelNotLinked(
                "La configuración vigente es de otro ambiente: el número del documento no es válido allí."));
        if (documento.Resolution is { } resolucion
            && ReglasDeResolucion.AsociacionVigente(resolucion, nuevo, vigente.SoftwareId, documento.IssueDate) is null)
            return Falla(ErroresDeDocumentosElectronicos.ChannelNotLinked(
                $"La resolución {resolucion.ResolutionNumber} (prefijo {resolucion.Prefix}) con que se numeró no está asociada al canal vigente {nuevo}: " +
                "asóciela en Resoluciones de la DIAN o transmita por el canal anterior."));

        documento.EmissionSettingId = vigente.Id;
        documento.EmissionSetting = vigente;
        documento.Mode = vigente.Mode;
        documento.ChannelCode = nuevo;
        documento.SoftwareId = vigente.SoftwareId;
        documento.EmailDeliveryBy = vigente.EmailDeliveryBy;
        documento.NextAttemptAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(new CambioDeCanalDto(documento.PublicId, sellado, nuevo, documento.Status));
    }

    private static Result<CambioDeCanalDto> Falla(Error error) => Result.Failure<CambioDeCanalDto>(error);
}
