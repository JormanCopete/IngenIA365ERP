using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Migration;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Activation;

/// <summary>
/// La activación <b>con carga</b> de aportes o de captaciones (feature 014, US9, T155; FR-114 a FR-116, R21; api.md §16; data-model §7.2), con la misma regla
/// de la activación de créditos (la 013 §30):
/// <list type="number">
/// <item><b>Antes de la transacción</b>, en este orden: la entrega (<c>Lending.Savings.DeliveryNotAvailable</c>), ya activada
/// (<c>Lending.Activation.AlreadyActivated</c>), los créditos primero (<c>Lending.Activation.LoansFirst</c>), la carga de la naturaleza
/// (<c>Lending.MigrationLoad.NotFound</c>), la fecha = corte + 1 (<c>Lending.Activation.CutoffInvalid</c>), no futura, dentro del plazo de fecha valor
/// (<c>Lending.Activation.DeadlinePassed</c>), el acta de la activación (un punto cerrado y aprobado de una sesión sobre esta carga,
/// <c>Lending.Session.ItemMismatch</c>), el <b>cuadre recalculado</b> con los saldos contables de este momento (si cambió, nace la versión siguiente y se
/// guarda: <c>Lending.Activation.DifferencesNotAccepted</c>, hay que volver a aceptar), las diferencias aceptadas y los requisitos
/// (<see cref="RequisitosDeActivacionDeCaptaciones"/> con la carga).</item>
/// <item><b>En una <see cref="TransaccionExplicita"/></b> con <c>LND_Setup</c> exclusivo: lo repite, hace nacer las cuentas
/// (<see cref="NacimientoDeCuentasMigradas"/>: saldo inicial por concepto al corte <b>sin hechos contables</b>), deja la carga <c>Activated</c>, escribe la
/// fila de <c>LND_Activations</c> con su <c>Nature</c>, la carga, el acta, la versión y la huella del cuadre, emite <c>CaptacionesActivadas</c> con las cuentas
/// y las transacciones iniciales y pone la fecha de la naturaleza en <c>LND_Setup</c> (lo que cambia el contrato de cuentas y la consulta de aportes a
/// habilitados). Audita <c>Lending.Activation.Registered</c>.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class ActivacionDeCaptacionesConCarga(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    RequisitosDeActivacionDeCaptaciones requisitos,
    PuntoDeActaCerrado puntoDeActa,
    CuadreDeCaptaciones cuadre,
    NacimientoDeCuentasMigradas nacimiento,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj,
    LendingAuditEmitter? auditoria = null,
    Documents.TitulosDeCdat? titulos = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Validacion(MigrationLoad Carga, int PuntoDeActa, EvaluacionDeActivacion Evaluacion);

    public async Task<Result<DepositsActivationDto>> ActivarAsync(LineNature naturaleza, Guid cargaPublicId, DateOnly fecha, Guid? puntoPublicId,
        bool reconoceAvisos, string motivo, CancellationToken ct)
    {
        if (naturaleza is not (LineNature.Deposit or LineNature.Contribution))
            throw new ArgumentOutOfRangeException(nameof(naturaleza), naturaleza, "Sólo se activan aportes o captaciones.");
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DepositsActivationDto>(ErroresDeActivacion.SinUsuario());

        var previa = await ValidarAsync(naturaleza, cargaPublicId, fecha, puntoPublicId, recalcular: true, ct);
        if (previa.IsFailure) return Result.Failure<DepositsActivationDto>(previa.Error);

        var activada = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: true, ct);
            var setup = await db.LendingSetups.FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct)
                ?? throw new InvalidOperationException("Falta la fila única de LND_Setup: la migración CarteraNucleo no se aplicó.");
            var validado = await ValidarAsync(naturaleza, cargaPublicId, fecha, puntoPublicId, recalcular: false, ct);
            if (validado.IsFailure) return Result.Failure<DepositsActivationDto>(validado.Error);
            var (carga, punto, evaluacion) = validado.Value;

            var filas = await FilasDeLaCarga.LeerAsync(db, carga, ct);
            var nacidas = await nacimiento.NacerAsync(carga, filas, fecha, motivo.Trim(), ct);

            var ahora = reloj.UtcNow;
            var activacionPublicId = Guid.NewGuid();
            var mensaje = await emision.ActivacionDeCaptacionesAsync(activacionPublicId, naturaleza, fecha, carga.PublicId, nacidas.Cuentas,
                nacidas.TransaccionesIniciales, ct);
            if (mensaje.IsFailure) return Result.Failure<DepositsActivationDto>(mensaje.Error);

            carga.Status = MigrationLoadStatus.Activated;
            carga.ActivatedAt = ahora;
            carga.AccountCount = nacidas.Cuentas;

            var activacion = new LendingActivation
            {
                PublicId = activacionPublicId,
                Nature = naturaleza,
                MigrationLoadId = carga.Id,
                CutoffDate = carga.CutoffDate,
                ActivationDate = fecha,
                RegisteredByUserId = usuario,
                RegisteredAt = ahora,
                SessionItemId = punto,
                CheckVersion = carga.CurrentCheckVersion,
                BalanceCheckSha256 = carga.BalanceCheckSha256 ?? ApplyMigrationLoadCommandHandler.HuellaVacia,
                SalesReconciliationSha256 = ApplyMigrationLoadCommandHandler.HuellaVacia,
                IsProductionEnvironment = requisitos.EsProduccion,
                RequirementsJson = JsonSerializer.Serialize(evaluacion.Requisitos.Select(r => new { r.Code, r.Status, r.Met, r.Detail }), Json),
                CountsJson = JsonSerializer.Serialize(nacidas.ParaJson(), Json),
                CommercialAdjustmentsJson = JsonSerializer.Serialize(new
                {
                    warnings = evaluacion.Avisos.Select(a => a.Code), acknowledged = reconoceAvisos, reason = motivo.Trim(),
                }, Json),
                ActivatedMessagePublicId = mensaje.Value.PublicId,
            };
            db.LendingActivations.Add(activacion);
            await db.SaveChangesAsync(ct);

            if (naturaleza == LineNature.Deposit)
            {
                setup.DepositsActivationDate = fecha;
                setup.DepositsActivationId = activacion.Id;
                setup.DepositsActivatedAt = ahora;
            }
            else
            {
                setup.ContributionsActivationDate = fecha;
                setup.ContributionsActivationId = activacion.Id;
                setup.ContributionsActivatedAt = ahora;
            }
            await db.SaveChangesAsync(ct);

            if (auditoria is not null)
                await auditoria.EmitirActivacionRegistradaAsync(activacion.PublicId, new
                {
                    nature = naturaleza.ToString(), activationDate = fecha, load = carga.PublicId, cutoffDate = carga.CutoffDate,
                    checkVersion = carga.CurrentCheckVersion, isProduction = activacion.IsProductionEnvironment, counts = nacidas.ParaJson(),
                    requirements = evaluacion.Requisitos.Select(r => new { r.Code, r.Status }), reason = motivo.Trim(),
                }, ct);

            return Result.Success(new DepositsActivationDto(activacion.PublicId, naturaleza.ToString(), fecha, nacidas.Cuentas, nacidas.TransaccionesIniciales,
                [new MensajeEmitidoDto(mensaje.Value.Type, mensaje.Value.PublicId)], evaluacion.Requisitos, evaluacion.Avisos));
        }, ct);
        // Respuesta 87 B del dueño (E250): el título de cada CDAT migrado se emite al activar las captaciones, a nombre de su titular. Después de confirmar la
        // activación y uno por uno: un título que no sale (sin firmante, almacén caído) queda en el registro y se emite desde la ficha, como cualquier otro.
        if (activada.IsSuccess && naturaleza == LineNature.Deposit && titulos is not null)
            await EmitirTitulosDeLosCdatMigradosAsync(cargaPublicId, usuario, ct);
        return activada;
    }

    /// <summary>Emite el título vigente de cada CDAT que nació con la carga (los que ya lo tienen no se repiten). Nunca falla la activación.</summary>
    private async Task EmitirTitulosDeLosCdatMigradosAsync(Guid cargaPublicId, int usuario, CancellationToken ct)
    {
        var carga = await db.MigrationLoads.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == cargaPublicId, ct);
        if (carga is null) return;
        var tandas = await db.MigrationLoadParts.AsNoTracking().Where(p => p.MigrationLoadId == carga.Id && !p.IsDeleted).Select(p => p.Id).ToListAsync(ct);
        var numeros = await db.MigrationTermDepositRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId)).Select(r => r.SourceAccountNumber).ToListAsync(ct);
        if (numeros.Count == 0) return;
        var cuentas = await db.MemberAccounts.Where(a => numeros.Contains(a.Number) && a.DepositClass == DepositClass.TermDeposit && !a.IsDeleted).ToListAsync(ct);
        foreach (var cuenta in cuentas)
        {
            try
            {
                await titulos!.ReemitirVigenteAsync(cuenta, usuario, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Se emite después desde la ficha del CDAT (POST /term-deposits/{id}/titles).
            }
        }
    }

    /// <summary>Entrega, activada, créditos primero, carga, fecha, plazo, acta, recálculo, diferencias y requisitos, en ese orden.</summary>
    private async Task<Result<Validacion>> ValidarAsync(LineNature naturaleza, Guid cargaPublicId, DateOnly fecha, Guid? puntoPublicId, bool recalcular,
        CancellationToken ct)
    {
        if (requisitos.EntregaNoDisponible(naturaleza) is { } entrega) return Result.Failure<Validacion>(entrega);
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        if (fechas.De(naturaleza) is { } activada) return Result.Failure<Validacion>(ErroresDeActivacionDeCaptaciones.YaActivada(naturaleza, activada));
        if (fechas.Creditos is not { } creditos || creditos > fecha)
            return Result.Failure<Validacion>(ErroresDeActivacionDeCaptaciones.CreditosPrimero(fechas.Creditos));

        var carga = await ConsultasDeCargasDeCaptaciones.BuscarAsync(db, cargaPublicId, seguimiento: true, ct);
        if (carga is null || carga.Nature != naturaleza) return Result.Failure<Validacion>(ErroresDeActivacion.CargaNoEncontrada(cargaPublicId));
        if (fecha != carga.CutoffDate.AddDays(1)) return Result.Failure<Validacion>(ErroresDeActivacion.CorteInvalido(carga.CutoffDate));
        var hoy = reloj.HoyLocal;
        if (fecha > hoy) return Result.Failure<Validacion>(ErroresDeActivacionDeCaptaciones.FechaFutura(fecha, hoy));
        if (carga is { Status: MigrationLoadStatus.Applied, ActivationDeadline: { } plazo } && hoy > plazo)
            return Result.Failure<Validacion>(ErroresDeMigracion.PlazoVencido(plazo));

        var punto = await puntoDeActa.ValidarAsync(puntoPublicId ?? Guid.Empty, LendingDecisionSubjects.ActivationDifferences, carga.PublicId, ct);
        if (punto.IsFailure) return Result.Failure<Validacion>(punto.Error);

        if (recalcular && carga.Status == MigrationLoadStatus.Applied)
        {
            var r = await cuadre.RecalcularAsync(carga, ct);
            if (r.IsFailure) return Result.Failure<Validacion>(r.Error);
            if (r.Value.Cambio)
            {
                // La versión nueva del cuadre se guarda aunque la activación se niegue: es la que hay que aceptar (FR-115, como la 013).
                await db.SaveChangesAsync(ct);
                return Result.Failure<Validacion>(ErroresDeMigracion.DiferenciasSinAceptar(r.Value.Pendientes));
            }
        }
        if (carga is { Status: MigrationLoadStatus.Applied, IsBalanced: false })
        {
            var pendientes = (await ConciliacionDeVentasDeMigracion.VigentesAsync(db, carga, carga.CurrentCheckVersion, ct)).Count(d => !d.Aceptada);
            return Result.Failure<Validacion>(ErroresDeMigracion.DiferenciasSinAceptar(pendientes));
        }

        var evaluacion = await requisitos.EvaluarAsync(naturaleza, fecha, hoy, carga, ct);
        var faltantes = evaluacion.Faltantes;
        return faltantes.Count > 0
            ? Result.Failure<Validacion>(ErroresDeActivacion.FaltanRequisitos(faltantes))
            : Result.Success(new Validacion(carga, punto.Value.ItemId, evaluacion));
    }
}
