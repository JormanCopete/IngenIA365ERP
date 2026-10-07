using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>Lo que responde aplicar una carga (api.md §19.1). (nuevo)</summary>
public sealed record AppliedMigrationLoadDto(
    Guid PublicId,
    MigrationLoadStatus Status,
    DateOnly CutoffDate,
    DateOnly ActivationDeadline,
    BalanceCheckSummaryDto BalanceCheck,
    SalesReconciliationSummaryDto SalesReconciliation,
    Guid? ReplacedLoadPublicId,
    Guid AppliedMessagePublicId);

/// <summary>El resumen del cuadre de la carga (FR-141). (nuevo)</summary>
public sealed record BalanceCheckSummaryDto(int CheckVersion, bool IsBalanced, int Differences);

/// <summary>El resumen de la conciliación de ventas de la carga (FR-143). (nuevo)</summary>
public sealed record SalesReconciliationSummaryDto(bool SalesReconciled, int Differences);

/// <summary>
/// Aplica una carga de migración (feature 013, T0232, C107; FR-142; api.md §19.1, <c>POST /api/lending/migration/loads/{id}/apply</c>;
/// data-model §29.1). <b>Aplicar sólo guarda la carga</b>: nada nace en el libro hasta la activación. En esta forma mínima aplica una
/// carga <b>sin partes</b> (la de una cooperativa sin cartera que migrar): queda <c>Applied</c> con conteos en cero,
/// <c>IsBalanced</c> y <c>SalesReconciled</c> verdaderos (no hay qué cuadrar ni conciliar), <c>ActivationDeadline</c> = corte +
/// <c>Pagos.DiasMaximosFechaValor</c>, la aplicada anterior <c>Replaced</c> y el informativo <c>CargaDeCarteraAplicada</c> por
/// <see cref="EmisionDeCartera"/>, todo en el mismo guardado. US8 la completa con las partes, la validación entre partes, el cuadre
/// contra Contabilidad y la conciliación de ventas. (nuevo)
/// </summary>
public sealed record ApplyMigrationLoadCommand(Guid MigrationLoadPublicId, string Reason)
    : IRequest<Result<AppliedMigrationLoadDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyMigrationLoadCommandValidator : ValidadorConMotivo<ApplyMigrationLoadCommand>
{
    public ApplyMigrationLoadCommandValidator() => RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
}

public sealed class ApplyMigrationLoadCommandHandler(
    IApplicationDbContext db,
    EmisionDeCartera emision,
    ILectorDeParametros parametros,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ApplyMigrationLoadCommand, Result<AppliedMigrationLoadDto>>
{
    /// <summary>La huella de una instantánea vacía (<c>[]</c>): el cuadre y la conciliación de una carga sin partes.</summary>
    public static readonly string HuellaVacia = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("[]")));

    public async Task<Result<AppliedMigrationLoadDto>> Handle(ApplyMigrationLoadCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AppliedMigrationLoadDto>(ErroresDeActivacion.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var setup = await db.LendingSetups.AsNoTracking().FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct);
            if (setup?.ActivationDate is { } activada) return Result.Failure<AppliedMigrationLoadDto>(ErroresDeActivacion.YaActivada(activada));

            var carga = await db.MigrationLoads.FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
            if (carga is null) return Result.Failure<AppliedMigrationLoadDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
            if (carga.Status != MigrationLoadStatus.Draft) return Result.Failure<AppliedMigrationLoadDto>(ErroresDeActivacion.CargaNoBorrador());
            if (await db.MigrationLoadParts.AnyAsync(p => p.MigrationLoadId == carga.Id && !p.IsDeleted, ct))
                return Result.Failure<AppliedMigrationLoadDto>(ErroresDeActivacion.PartesNoSoportadas());

            var hoy = reloj.HoyLocal;
            var dias = await parametros.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.PagosDiasMaximosFechaValor, hoy, ct: ct);
            if (dias.IsFailure) return Result.Failure<AppliedMigrationLoadDto>(dias.Error);
            var plazo = carga.CutoffDate.AddDays(int.Parse(dias.Value.Texto, System.Globalization.CultureInfo.InvariantCulture));
            var ahora = reloj.UtcNow;

            // Hay a lo sumo una aplicada (índice único filtrado): la anterior queda reemplazada (sirve de ensayo) y se guarda antes.
            var anterior = await db.MigrationLoads.FirstOrDefaultAsync(l => l.Status == MigrationLoadStatus.Applied && !l.IsDeleted, ct);
            if (anterior is not null)
            {
                anterior.Status = MigrationLoadStatus.Replaced;
                anterior.ReplacedAt = ahora;
                anterior.ReplacedByLoadId = carga.Id;
                await db.SaveChangesAsync(ct);
            }

            carga.Status = MigrationLoadStatus.Applied;
            carga.AppliedByUserId = usuario;
            carga.AppliedAt = ahora;
            carga.ActivationDeadline = plazo;
            carga.CurrentCheckVersion = 1;
            carga.BalanceCheckJson = "[]";
            carga.BalanceCheckSha256 = HuellaVacia;
            carga.SalesReconciliationSha256 = HuellaVacia;
            carga.IsBalanced = true;
            carga.SalesReconciled = true;
            carga.ValidationSummaryJson = "{\"errors\":[],\"warnings\":[]}";
            carga.LoanCount = 0;
            carga.ChargedOffCount = 0;
            carga.PersonCount = 0;
            carga.TotalsJson = "{}";

            var mensaje = await emision.CargaAplicadaAsync(carga, anterior?.PublicId, hoy, diferencias: 0, ct);
            if (mensaje.IsFailure) return Result.Failure<AppliedMigrationLoadDto>(mensaje.Error);

            await db.SaveChangesAsync(ct);
            return Result.Success(new AppliedMigrationLoadDto(carga.PublicId, carga.Status, carga.CutoffDate, plazo,
                new BalanceCheckSummaryDto(carga.CurrentCheckVersion, carga.IsBalanced, 0),
                new SalesReconciliationSummaryDto(carga.SalesReconciled, 0),
                anterior?.PublicId, mensaje.Value.PublicId));
        }, ct);
    }
}
