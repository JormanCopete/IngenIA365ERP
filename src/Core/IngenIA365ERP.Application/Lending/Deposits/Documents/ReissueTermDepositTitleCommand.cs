using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>
/// <c>POST /api/lending/term-deposits/{id}/titles</c> (feature 014, US13, T198; documentos.md §2.5; <c>Lending.Documents.Issue</c>, con clave): emite el
/// título de la versión vigente de un CDAT que no lo tiene —el que no salió al constituir, prorrogar o renovar porque faltaba el firmante o el almacén no
/// respondió (<c>[Cartera.TituloDeCdatSinEmitir]</c>)—. Si ya tiene título vigente de esa versión, lo devuelve sin emitir otro (la emisión normal es
/// automática: <c>POST /documents</c> con la clase responde <c>Lending.Document.IssuedAutomatically</c>). Un CDAT liquidado no recibe título. (nuevo)
/// </summary>
public sealed record ReissueTermDepositTitleCommand(Guid AccountPublicId) : IRequest<Result<TermDepositTitleDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReissueTermDepositTitleCommandValidator : AbstractValidator<ReissueTermDepositTitleCommand>
{
    public ReissueTermDepositTitleCommandValidator() => RuleFor(x => x.AccountPublicId).NotEmpty();
}

public sealed class ReissueTermDepositTitleCommandHandler(
    IApplicationDbContext db,
    TitulosDeCdat titulos,
    ConstructorDeDocumentosDeCartera constructor,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ReissueTermDepositTitleCommand, Result<TermDepositTitleDto>>
{
    public async Task<Result<TermDepositTitleDto>> Handle(ReissueTermDepositTitleCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<TermDepositTitleDto>(ErroresDeGobierno.SinUsuario());
        var leido = await RespuestasDeCdat.CdatEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<TermDepositTitleDto>(leido.Error);
        var cdat = leido.Value;
        if (cdat.Status is MemberAccountStatus.Opening or MemberAccountStatus.Closing or MemberAccountStatus.Closed)
            return Result.Failure<TermDepositTitleDto>(DocumentErrors.NadaQueCertificar($"el CDAT {cdat.Number} no está vigente"));
        var firmante = await constructor.FirmanteAsync(reloj.HoyLocal, ct);
        if (firmante.IsFailure) return Result.Failure<TermDepositTitleDto>(firmante.Error);

        var documento = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync([cdat.Id], ModoDeEspera.Esperar, ct);
            return await titulos.ReemitirVigenteAsync(cdat, usuario, ct);
        }, ct);
        if (documento is null)
            return Result.Failure<TermDepositTitleDto>(DocumentErrors.NadaQueCertificar($"no se pudo emitir el título del CDAT {cdat.Number}; revise el registro"));
        await auditoria.EmitirDocumentoEmitidoAsync(documento.PublicId, documento.Kind.ToString(), documento.Number, documento.DeliveryMode.ToString(),
            documento.ContentSha256, ct);
        var lista = await RespuestasDeCdat.TitulosAsync(db, cdat.Id, ct);
        return Result.Success(lista.First(t => t.DocumentPublicId == documento.PublicId));
    }
}
