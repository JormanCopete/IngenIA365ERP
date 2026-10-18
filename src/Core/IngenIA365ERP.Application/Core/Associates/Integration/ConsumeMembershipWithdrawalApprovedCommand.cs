using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Associates.Integration;

/// <summary>
/// Consume en Asociados un <c>RetiroDeAsociadoAprobado</c> v1 de la Cartera (feature 014, US10, T098; contracts/mensajes.md §5.1; FR-124, enmienda E-04).
/// Sin ruta (<c>LosComandosDeConsumoNoTienenRuta</c>): lo manda <see cref="DestinoAsociados"/>; es <see cref="IReintentableAnteConcurrencia"/>. Responde
/// siempre un <see cref="ResultadoDeConsumo"/>; la falla del <see cref="Result"/> queda para lo que no se pudo leer. (nuevo)
/// </summary>
public sealed record ConsumeMembershipWithdrawalApprovedCommand(IReadOnlyList<Guid> MessagePublicIds)
    : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeMembershipWithdrawalApprovedCommandValidator : AbstractValidator<ConsumeMembershipWithdrawalApprovedCommand>
{
    public ConsumeMembershipWithdrawalApprovedCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

/// <summary>
/// El efecto de §5.1 en un solo <c>SaveChanges</c> con el recibo <c>COR_AssociateInboundMessages</c> (único por <c>MessagePublicId</c>):
/// <list type="bullet">
/// <item>el asociado de la persona pasa a retirado (<c>Status = "R"</c>) con la fecha efectiva; repetir el mismo mensaje responde
/// <c>AlreadyProcessed</c> sin un segundo efecto, también ante la colisión del índice por concurrencia;</item>
/// <item>ya retirado con la misma fecha: deja el recibo sin cambiar nada; con otra fecha, <c>Rejected</c> con
/// <c>Associate.WithdrawalDateMismatch</c> para revisión en la bandeja;</item>
/// <item>sin asociado para la persona: <c>Rejected</c> con <c>Associate.NotFound</c>.</item>
/// </list>
/// Asociados ya no retira por su cuenta (<c>Associate.WithdrawalOwnedByLending</c>): este consumo es la única vía. (nuevo)
/// </summary>
public sealed class ConsumeMembershipWithdrawalApprovedCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    IDateTimeService reloj) : IRequestHandler<ConsumeMembershipWithdrawalApprovedCommand, Result<ResultadoDeConsumo>>
{
    public const string CodigoAsociadoInexistente = "Associate.NotFound";
    public const string CodigoFechaDistinta = "Associate.WithdrawalDateMismatch";

    /// <summary>El estado de un asociado retirado (<c>COR_Associates.Status</c>).</summary>
    public const string EstadoRetirado = "R";

    /// <summary>El usuario que firma el cambio en el asociado.</summary>
    public const string Actor = "Proceso de integración";

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeMembershipWithdrawalApprovedCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Associates, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);

        Guid? referencia = null;
        var nuevos = new List<AssociateInboundMessage>();
        foreach (var m in leidos.Value)
        {
            if (await YaRecibidoAsync(m.MessageId, ct) is { } ya)
            {
                referencia ??= ya;
                continue;
            }
            if (m.Payload is not RetiroDeAsociadoAprobadoV1 retiro || m.Version != 1)
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted",
                    $"Asociados no acepta {m.Type} v{m.Version}."));

            var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == retiro.PersonPublicId)
                .Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            var asociado = persona is { } pid
                ? await db.Associates.Where(a => a.PersonId == pid && !a.IsDeleted).OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct)
                : null;
            if (asociado is null)
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(CodigoAsociadoInexistente,
                    $"La persona del retiro {retiro.WithdrawalNumber} no es asociado en Asociados: revisá el retiro en la bandeja de mensajes."));

            var yaRetirado = Retirado(asociado);
            if (yaRetirado && asociado.WithdrawalDate != retiro.EffectiveDate)
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(CodigoFechaDistinta,
                    $"El asociado ya está retirado desde el {asociado.WithdrawalDate:yyyy-MM-dd} y la Cartera aprobó el retiro {retiro.WithdrawalNumber} "
                    + $"con fecha {retiro.EffectiveDate:yyyy-MM-dd}: revisá cuál es la fecha correcta."));
            if (!yaRetirado)
            {
                asociado.Status = EstadoRetirado;
                asociado.WithdrawalDate = retiro.EffectiveDate;
                asociado.UpdatedAt = reloj.UtcNow;
                asociado.UpdatedBy = Actor;
            }
            nuevos.Add(new AssociateInboundMessage
            {
                MessagePublicId = m.MessageId,
                MessageType = RetiroDeAsociadoAprobadoV1.Type,
                MessageVersion = (short)m.Version,
                AssociateId = asociado.Id,
                WithdrawalPublicId = retiro.WithdrawalPublicId,
                WithdrawalNumber = Recortar(retiro.WithdrawalNumber, 20),
                EffectiveDate = retiro.EffectiveDate,
                Cause = Recortar(retiro.Cause, 30),
                AlreadyWithdrawn = yaRetirado,
                ReceivedAt = reloj.UtcNow,
            });
        }

        if (nuevos.Count == 0)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.AlreadyProcessed(referencia, SinComprobante: MotivoSinComprobante.Informational));

        db.AssociateInboundMessages.AddRange(nuevos);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dos réplicas con el mismo mensaje: la otra ya guardó el recibo (UK MessagePublicId). Si no fue eso, sube.
            db.DescartarCambios();
            if (await YaRecibidoAsync(nuevos[0].MessagePublicId, ct) is { } otro)
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.AlreadyProcessed(otro, SinComprobante: MotivoSinComprobante.Informational));
            throw;
        }
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(nuevos[0].PublicId, null, null, MotivoSinComprobante.Informational));
    }

    /// <summary>¿Está retirado? Estado <c>R</c> con fecha de retiro y sin un reingreso posterior.</summary>
    public static bool Retirado(Associate a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return string.Equals(a.Status?.Trim(), EstadoRetirado, StringComparison.OrdinalIgnoreCase) && a.WithdrawalDate is { } w
               && (a.RejoinDate is null || a.RejoinDate < w);
    }

    private async Task<Guid?> YaRecibidoAsync(Guid mensaje, CancellationToken ct) =>
        await db.AssociateInboundMessages.AsNoTracking().Where(r => r.MessagePublicId == mensaje).Select(r => (Guid?)r.PublicId).FirstOrDefaultAsync(ct);

    private static string Recortar(string texto, int largo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}
