using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>Lo que responde aplicar una carga de aportes o captaciones (api.md §16 de la 014). (nuevo)</summary>
public sealed record AppliedDepositMigrationLoadDto(
    Guid PublicId,
    LineNature Nature,
    MigrationLoadStatus Status,
    DateOnly CutoffDate,
    DateOnly ActivationDeadline,
    BalanceCheckSummaryDto BalanceCheck,
    Guid? ReplacedLoadPublicId,
    Guid AppliedMessagePublicId,
    int Accounts,
    IReadOnlyList<TotalDeCargaDto> Totals,
    IReadOnlyList<ErrorEntrePartes> Warnings);

/// <summary>
/// Aplica una carga de aportes o captaciones (feature 014, US9, T153; api.md §16, <c>POST /api/lending/deposits/migration/loads/{id}/apply</c>,
/// <c>Lending.Deposits.Migration</c>; plantillas-y-archivos.md §4.9; data-model §7). <b>Aplicar sólo guarda la carga</b>: las cuentas nacen al activar.
/// La carga es de toda la cooperativa (sin alcance de oficina ni de línea; las filas ya pasaron por el alcance de quien las importó). (nuevo)
/// </summary>
public sealed record ApplyDepositMigrationLoadCommand(Guid MigrationLoadPublicId, string Reason)
    : IRequest<Result<AppliedDepositMigrationLoadDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyDepositMigrationLoadCommandValidator : ValidadorConMotivo<ApplyDepositMigrationLoadCommand>
{
    public ApplyDepositMigrationLoadCommandValidator() => RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
}

/// <summary>
/// Todo o nada en una <see cref="TransaccionExplicita"/>:
/// <list type="number">
/// <item>la carga es de aportes o captaciones (si no, <c>Lending.MigrationLoad.NotFound</c>), su naturaleza no está activada
/// (<c>Lending.Activation.AlreadyActivated</c>) y es un borrador (<c>Lending.Migration.LoadNotDraft</c>);</item>
/// <item>las validaciones entre partes (<see cref="ValidacionEntrePartesDeCaptaciones"/>); con un error, 422 <c>Lending.Migration.CrossPartErrors</c>
/// (<c>data.errors[] { part, batch, row, column, code, message }</c>) y la carga sigue <c>Draft</c>;</item>
/// <item>la <c>Applied</c> anterior <b>de la misma naturaleza</b> pasa a <c>Replaced</c> (no se borra: sirve de ensayo, US9-3), la carga queda
/// <c>Applied</c> con quién y cuándo, el plazo de la activación = corte + <c>Captaciones.DiasMaximosFechaValor</c>, las cuentas, los totales por línea y
/// concepto y los avisos;</item>
/// <item>el cuadre de la versión 1 (<see cref="CuadreDeCaptaciones"/>) con sus diferencias y su huella;</item>
/// <item><c>CargaDeCaptacionesAplicada</c> (informativo) en el mismo guardado.</item>
/// </list>
/// </summary>
public sealed class ApplyDepositMigrationLoadCommandHandler(
    IApplicationDbContext db,
    ValidacionEntrePartesDeCaptaciones validacion,
    CuadreDeCaptaciones cuadre,
    EmisionDeCartera emision,
    ReglasDeParametrosDeCartera parametros,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ApplyDepositMigrationLoadCommand, Result<AppliedDepositMigrationLoadDto>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<AppliedDepositMigrationLoadDto>> Handle(ApplyDepositMigrationLoadCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AppliedDepositMigrationLoadDto>(ErroresDeActivacion.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var carga = await ConsultasDeCargasDeCaptaciones.BuscarAsync(db, request.MigrationLoadPublicId, seguimiento: true, ct);
            if (carga is null) return Result.Failure<AppliedDepositMigrationLoadDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
            var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
            if (fechas.De(carga.Nature) is { } activada)
                return Result.Failure<AppliedDepositMigrationLoadDto>(ErroresDeActivacionDeCaptaciones.YaActivada(carga.Nature, activada));
            if (!carga.EsBorrador) return Result.Failure<AppliedDepositMigrationLoadDto>(ErroresDeActivacion.CargaNoBorrador());

            var filas = await FilasDeLaCarga.LeerAsync(db, carga, ct);
            var validada = await validacion.ValidarAsync(filas, ct);
            if (validada.Errores.Count > 0)
                return Result.Failure<AppliedDepositMigrationLoadDto>(ErroresDeMigracion.ErroresEntrePartes(validada.Errores));

            var hoy = reloj.HoyLocal;
            var plazo = carga.CutoffDate.AddDays(await DiasDeFechaValorAsync(hoy, ct));
            var ahora = reloj.UtcNow;

            var anterior = await db.MigrationLoads.FirstOrDefaultAsync(l => l.Nature == carga.Nature && l.Status == MigrationLoadStatus.Applied && !l.IsDeleted, ct);
            if (anterior is not null)
            {
                anterior.Reemplazar(carga.Id, ahora);
                await db.SaveChangesAsync(ct);
            }

            var totales = filas.Totales();
            var resumen = JsonSerializer.Serialize(new { errors = Array.Empty<object>(), warnings = validada.Avisos }, Json);
            carga.Aplicar(usuario, ahora, plazo, 0, 0, validada.Personas, JsonSerializer.Serialize(totales, Json), resumen);
            carga.AccountCount = validada.Cuentas;

            const int version = 1;
            var calculado = await cuadre.CalcularAsync(filas, ct);
            if (calculado.IsFailure) return Result.Failure<AppliedDepositMigrationLoadDto>(calculado.Error);
            var diferencias = cuadre.Registrar(carga, version, calculado.Value);
            carga.SellarCuadre(version, calculado.Value.InstantaneaJson, calculado.Value.Huella, diferencias.Count == 0);
            // Sin ventas en una carga de captaciones: la conciliación de ventas no aplica (su huella es la vacía y queda conciliada).
            carga.SellarConciliacion(ApplyMigrationLoadCommandHandler.HuellaVacia, conciliada: true);

            var mensaje = await emision.CargaDeCaptacionesAplicadaAsync(carga, filas.Tandas.OrderBy(t => t.PartKey).ThenBy(t => t.BatchNumber).Select(t => t.PublicId).ToList(),
                totales.Select(t => new DepositLoadTotalV1 { CreditLineCode = t.LineCode, Concept = t.Concept, Amount = t.Amount, AccountCount = t.Accounts }).ToList(),
                hoy, ct);
            if (mensaje.IsFailure) return Result.Failure<AppliedDepositMigrationLoadDto>(mensaje.Error);

            await db.SaveChangesAsync(ct);
            return Result.Success(new AppliedDepositMigrationLoadDto(carga.PublicId, carga.Nature, carga.Status, carga.CutoffDate, plazo,
                new BalanceCheckSummaryDto(carga.CurrentCheckVersion, carga.IsBalanced, diferencias.Count), anterior?.PublicId, mensaje.Value.PublicId,
                validada.Cuentas, totales, validada.Avisos));
        }, ct);
    }

    /// <summary>Los días de fecha valor de captaciones (FR-116): el parámetro vigente o, si no se lee, el defecto del catálogo.</summary>
    private async Task<int> DiasDeFechaValorAsync(DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CaptacionesDiasMaximosFechaValor, hoy, ct: ct);
        if (leido.IsSuccess && int.TryParse(leido.Value.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dias) && dias >= 0) return dias;
        return int.Parse(ParametrosDeCartera.Definiciones.First(d => d.Clave == ParametrosDeCartera.CaptacionesDiasMaximosFechaValor).DefectoSeguro,
            CultureInfo.InvariantCulture);
    }
}
