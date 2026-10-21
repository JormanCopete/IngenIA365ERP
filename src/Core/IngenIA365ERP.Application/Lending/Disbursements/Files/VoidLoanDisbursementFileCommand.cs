using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.Files;

/// <summary>
/// Anula un archivo de desembolsos (feature 013, US3, T0454; api.md §9.3, <c>POST /api/lending/disbursement-files/{id}/void</c>,
/// <c>Lending.DisbursementFiles.Generate</c>, con clave y motivo; T78, C10, C114 —el nombre evita el choque con Nómina—): sólo antes del primer
/// resultado de Tesorería de cualquiera de sus órdenes (<c>Lending.DisbursementFile.HasResults</c> con <c>data.orders[]</c>). El archivo y sus
/// líneas quedan (hecho; la anulación en su escritura única); sus órdenes quedan libres para otro archivo y siguen para pagarse por archivo. No emite
/// mensaje. (nuevo)
/// </summary>
public sealed record VoidLoanDisbursementFileCommand(Guid FilePublicId, string Reason) : IRequest<Result<DisbursementFileDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidLoanDisbursementFileCommandValidator : AbstractValidator<VoidLoanDisbursementFileCommand>
{
    public VoidLoanDisbursementFileCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se anula el archivo.").MaximumLength(500);
    }
}

public sealed class VoidLoanDisbursementFileCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<VoidLoanDisbursementFileCommand, Result<DisbursementFileDto>>
{
    public async Task<Result<DisbursementFileDto>> Handle(VoidLoanDisbursementFileCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leido = await ArchivosDeDesembolsos.LeerAsync(db, alcance, auditoria, request.FilePublicId, seguir: false, ct);
        if (leido.IsFailure) return Falla(leido.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var archivo = await db.DisbursementFiles.FirstAsync(f => f.Id == leido.Value.Id, ct);
            if (archivo.Anulado) return Falla(ErroresDeArchivoDeDesembolsos.YaAnulado(archivo.Number));
            var idsDeOrden = await db.DisbursementFileLines.AsNoTracking().Where(l => l.FileId == archivo.Id).Select(l => l.OrderId).ToListAsync(ct);
            var creditos = await db.DisbursementOrders.AsNoTracking().Where(o => idsDeOrden.Contains(o.Id)).Select(o => o.LoanId).Distinct().ToListAsync(ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCreditosAsync(creditos.Order().ToList(), ModoDeEspera.Esperar, ct);

            var conResultado = await db.DisbursementOrderResults.AsNoTracking().Where(r => idsDeOrden.Contains(r.OrderId)).Select(r => r.OrderId).Distinct()
                .ToListAsync(ct);
            if (conResultado.Count > 0)
            {
                var numeros = await db.DisbursementOrders.AsNoTracking().Where(o => conResultado.Contains(o.Id)).OrderBy(o => o.Number).Select(o => o.Number)
                    .ToListAsync(ct);
                return Falla(ErroresDeArchivoDeDesembolsos.ConResultados(numeros));
            }

            archivo.Anular(usuario, reloj.UtcNow, request.Reason);
            foreach (var orden in await db.DisbursementOrders.Where(o => idsDeOrden.Contains(o.Id)).ToListAsync(ct))
                orden.LiberarArchivo(archivo.Id);
            await db.SaveChangesAsync(ct);
            return Result.Success((await ArchivosDeDesembolsos.DtosAsync(db, [archivo], ct))[0]);
        }, ct);
    }

    private static Result<DisbursementFileDto> Falla(Error e) => Result.Failure<DisbursementFileDto>(e);
}
