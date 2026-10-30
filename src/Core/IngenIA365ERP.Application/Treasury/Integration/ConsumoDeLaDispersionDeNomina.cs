using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Payroll;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Integration;

/// <summary>
/// Consume en Tesorería <c>DispersionDeNominaGenerada</c> v1 (feature 015, T2, fase 41, T0448; E-05; FR-168): guarda <c>TES_PayrollDispersions</c> con la cuenta de origen
/// casada con la cuenta propia por su número; Tesorería no paga la nómina (D27): la toma como compromiso de la posición y como registro de los libros para conciliar el
/// cargo del banco. Sin ruta; deja su recibo en <c>TES_InboundMessages</c> en el mismo guardado. (nuevo)
/// </summary>
public sealed record ConsumePayrollDispersionCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumePayrollDispersionCommandValidator : AbstractValidator<ConsumePayrollDispersionCommand>
{
    public ConsumePayrollDispersionCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumePayrollDispersionCommandHandler(IApplicationDbContext db, IMensajesEntrantes entrantes, IDateTimeService reloj)
    : IRequestHandler<ConsumePayrollDispersionCommand, Result<ResultadoDeConsumo>>
{
    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumePayrollDispersionCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Treasury, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (await RecibosDeTesoreria.PrevioAsync(db, mensaje.MessageId, ct) is { } previo) return Result.Success(RecibosDeTesoreria.Repetido(previo));
        if (mensaje.Payload is not DispersionDeNominaGeneradaV1 d || mensaje.Version != 1)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Tesorería no acepta {mensaje.Type} v{mensaje.Version}."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ahora = reloj.UtcNow;
            if (!await db.TreasuryPayrollDispersions.AnyAsync(x => x.FilePublicId == d.FilePublicId, ct))
            {
                var cuenta = d.BankAccount.AccountNumber is { Length: > 0 } numero
                    ? await db.OwnBankAccounts.AsNoTracking().Where(c => c.AccountNumber == numero).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct)
                    : null;
                db.TreasuryPayrollDispersions.Add(new PayrollDispersion
                {
                    MessagePublicId = mensaje.MessageId, FilePublicId = d.FilePublicId, RunPublicId = d.RunPublicId, BankAccountId = cuenta, SourceBankCode = d.BankAccount.BankCode,
                    SourceAccountNumber = d.BankAccount.AccountNumber, Total = d.Total, LineCount = d.LineCount, PayDate = d.PayDate, ReceivedAt = ahora,
                    CreatedAt = ahora, CreatedBy = RecibosDeTesoreria.Actor,
                });
            }
            db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora, InboundMessageOutcome.Processed, null, null,
                $"Dispersión de Nómina de {d.LineCount} pagos por {d.Total:N2} para el {d.PayDate:dd/MM/yyyy}."));
            await db.SaveChangesAsync(ct);
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
        }, ct);
    }
}
