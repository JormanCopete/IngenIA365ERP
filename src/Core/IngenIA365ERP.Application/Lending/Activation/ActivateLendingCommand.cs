using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Contrato;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>Lo que responde la activación (api.md §19.3). (nuevo)</summary>
public sealed record LendingActivationDto(
    Guid ActivationPublicId,
    DateOnly ActivationDate,
    int Loans,
    int OpeningTransactions,
    Guid? BaseClosePublicId,
    int CreditLimits,
    int RevalidatedLimits,
    int NonDeductionsMarked,
    int ProvisionalCollectionsStored,
    IReadOnlyList<MensajeEmitidoDto> Messages);

/// <summary>Un mensaje que nació con la activación. (nuevo)</summary>
public sealed record MensajeEmitidoDto(string Type, Guid MessagePublicId);

/// <summary>
/// Activa Cartera en la cooperativa (feature 013, T0233, C60, C61, C107; US8, T0752; FR-089, FR-140–FR-143, T84; api.md §19.3,
/// <c>POST /api/lending/activation</c>; data-model §30.1, §32.3). La activación sin cartera previa (carga sin partes) sigue igual; con cartera
/// previa hace nacer todo lo de la carga. <see cref="ProvisionalCollectionsAttachmentPublicId"/> es el adjunto de la revisión previa de los cobros
/// provisionales: un adjunto no cambia, así que su <c>PublicId</c> fija el SHA-256 del archivo en la huella de idempotencia (que además se sella en
/// <c>LND_Activations.ProvisionalCollectionsFileSha256</c>). (nuevo)
/// </summary>
public sealed record ActivateLendingCommand(
    Guid MigrationLoadPublicId,
    DateOnly ActivationDate,
    Guid? ProvisionalCollectionsAttachmentPublicId,
    Guid SessionItemPublicId,
    bool AcknowledgeWarnings,
    string Reason,
    bool PayrollWarningAcknowledged = false) : IRequest<Result<LendingActivationDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ActivateLendingCommandValidator : ValidadorConMotivo<ActivateLendingCommand>
{
    public ActivateLendingCommandValidator()
    {
        RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
        RuleFor(x => x.ActivationDate).NotEqual(default(DateOnly)).WithMessage("Indicá la fecha de activación.");
    }
}

/// <summary>
/// <list type="number">
/// <item><b>Antes de la transacción</b>: Cartera sin activar (<c>Lending.Activation.AlreadyActivated</c>) y la carga existe; fecha = corte + 1
/// (<c>.CutoffInvalid</c>, <c>data.expected</c>); registrada a más tardar en el plazo de la carga (<c>.DeadlinePassed</c>); el acta obligatoria
/// (C61): un punto <c>Decision</c> aprobado de una sesión cerrada del sujeto <c>ActivationDifferences</c> de esa carga (<see cref="PuntoDeActaCerrado"/>,
/// <c>Lending.Session.ItemMismatch</c>); el <b>recálculo</b> del cuadre y la conciliación (<see cref="RecalculoDelCuadreDeActivacion"/>: con otra
/// huella nace <c>CheckVersion + 1</c>, guardada fuera de esta transacción, y responde <c>.DifferencesNotAccepted</c>) y sus diferencias aceptadas
/// (<c>.DifferencesNotAccepted</c>); y los requisitos de <see cref="RequisitosDeActivacion"/> (<c>.RequirementsMissing</c>, <c>data.missing[]</c>).
/// El mes contable cerrado sigue saliendo como el requisito <c>AccountingMonthOpen</c> (lo fijó la activación sin cartera previa).</item>
/// <item><b>En una <see cref="TransaccionExplicita"/></b>, con <c>LND_Setup</c> exclusivo (<see cref="ICerrojoDeCartera"/>), se repiten esas
/// validaciones (sin recalcular) y: se revalidan los cobros provisionales del adjunto (<see cref="ValidacionDeCobrosProvisionales"/>: <c>Import.Invalid</c>
/// o <c>Lending.ProvisionalCollection.ExceedsSale</c>); nacen los créditos por tandas de 1.000 (<see cref="NacimientoDeCreditosMigrados"/>), guardando y
/// soltando el contexto entre tandas (C60); nace lo del deudor (<see cref="NacimientoDeLoDelDeudorMigrado"/>); las ventas de la conciliación quedan
/// enlazadas a su obligación (<c>ResultingLoanId</c>: la migrada a su crédito, la cancelada antes del corte a una obligación en cero, pagada, con número
/// del numerador en la línea de la carga o en la de origen comercial por defecto del tipo de deudor); el numerador de créditos sube por encima del mayor
/// número numérico migrado; las cuotas de nómina propia vencidas entre el día siguiente al corte y el registro quedan como no aplicadas «Cartera sin
/// activar» con su alerta; la carga queda <c>Activated</c> y los cupos otorgados a mano se revalidan con lo que nació (<see cref="RevalidacionDeCuposMigrados"/>);
/// <c>CarteraActivada</c>; <c>LND_Activations</c> con requisitos, conteos y huellas; el destino <c>Lending</c> habilitado por
/// <see cref="EscritorDelRegistroDeIntegracion"/>; <c>LND_Setup</c>; el cierre base (<see cref="Risk.CierreBase"/>); los no aplicados; los cobros
/// provisionales guardados sin aplicar (los aplica el consumidor de Comercial al nacer la obligación de su venta, C62); y la auditoría
/// <c>Lending.Activation.Registered</c>. Si algo falla, no queda nada.</item>
/// </list>
/// Cartera no escribe asientos: nada de lo que nace emite un hecho contable (FR-140). Las dependencias que suma US8 son opcionales para la
/// activación sin cartera previa; la DI las inyecta todas. (nuevo)
/// </summary>
public sealed class ActivateLendingCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    RequisitosDeActivacion requisitos,
    EscritorDelRegistroDeIntegracion escritorDelRegistro,
    EmisionDeCartera emision,
    PuntoDeActaCerrado puntoDeActa,
    IActorActual actorActual,
    IDateTimeService reloj,
    Risk.CierreBase? cierreBase = null,
    RecalculoDelCuadreDeActivacion? recalculo = null,
    ValidacionDeCobrosProvisionales? cobrosProvisionales = null,
    NacimientoDeCreditosMigrados? creditos = null,
    NacimientoDeLoDelDeudorMigrado? deudores = null,
    NumeradorDeCartera? numerador = null,
    RegistroDeCartera? registro = null,
    RevalidacionDeCuposMigrados? revalidacion = null,
    IAlertas? alertas = null,
    LendingAuditEmitter? auditoria = null) : IRequestHandler<ActivateLendingCommand, Result<LendingActivationDto>>
{
    /// <summary>El código del destino de Cartera que siembra <c>PlataformaParaCartera</c> inhabilitado (C35).</summary>
    public const string CodigoDelDestinoDeCartera = "CAR";

    /// <summary>El motivo de los no aplicados de la activación (data-model §24.9).</summary>
    public const string MotivoCarteraSinActivar = "Cartera sin activar";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Validacion(MigrationLoad Carga, int Punto, EvaluacionDeActivacion Requisitos);

    public async Task<Result<LendingActivationDto>> Handle(ActivateLendingCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<LendingActivationDto>(ErroresDeActivacion.SinUsuario());

        // Lo que se puede rechazar sin cerrojo, con el recálculo del cuadre (las lecturas se repiten adentro).
        var previa = await ValidarAsync(request, recalcular: true, ct);
        if (previa.IsFailure) return Result.Failure<LendingActivationDto>(previa.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: true, ct);
            var setup = await db.LendingSetups.FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct)
                ?? throw new InvalidOperationException("Falta la fila única de LND_Setup: la migración CarteraNucleo no se aplicó.");
            var validado = await ValidarAsync(request, recalcular: false, ct);
            if (validado.IsFailure) return Result.Failure<LendingActivationDto>(validado.Error);
            var (carga, punto, evaluacion) = validado.Value;
            var hoy = reloj.HoyLocal;

            // 1. Los cobros provisionales revisados se vuelven a validar aquí: todo o nada con la activación (C62).
            IReadOnlyList<CobroProvisionalValidado> cobros = [];
            string? huellaDeCobros = null;
            if (request.ProvisionalCollectionsAttachmentPublicId is { } adjunto)
            {
                if (cobrosProvisionales is null) throw new InvalidOperationException("La validación de cobros provisionales no está registrada.");
                var archivo = await cobrosProvisionales.LeerAdjuntoAsync(adjunto, ct);
                if (archivo.IsFailure) return Result.Failure<LendingActivationDto>(archivo.Error);
                var revisados = await cobrosProvisionales.ValidarAsync(archivo.Value, carga.CutoffDate, hoy, ct);
                if (revisados.IsFailure) return Result.Failure<LendingActivationDto>(revisados.Error);
                if (revisados.Value.HayErrores) return Result.Failure<LendingActivationDto>(ValidacionDeCobrosProvisionales.Error(revisados.Value));
                cobros = revisados.Value.Cobros;
                huellaDeCobros = revisados.Value.Sha256;
            }

            // 2. Los créditos, por tandas de 1.000: cada una se guarda y se suelta del contexto (C60).
            var conteo = new ConteoDeActivacion();
            if (creditos is not null)
            {
                foreach (var tanda in await creditos.TandasAsync(carga, ct))
                {
                    await creditos.NacerTandaAsync(carga, tanda, actor, conteo, ct);
                    db.DescartarCambiosSalvo([setup, carga, .. db.OperationKeys.Local]);
                }
            }

            // 3. Lo del deudor: cupos, bloqueos, autorizaciones, avisos, reclamos y vinculados (lo registrado a mano prevalece).
            if (deudores is not null)
            {
                var deudor = await deudores.NacerAsync(carga, actor, conteo, ct);
                if (deudor.IsFailure) return Result.Failure<LendingActivationDto>(deudor.Error);
                await db.SaveChangesAsync(ct);
            }

            // 4. El numerador de créditos por encima de lo migrado; después, las ventas conciliadas con su obligación.
            if (numerador is not null && conteo.Creditos + conteo.Castigados > 0)
                await numerador.LlevarCreditosPorEncimaDeAsync(
                    await db.Loans.AsNoTracking().Where(l => l.MigrationLoadId == carga.Id).Select(l => l.Number).ToListAsync(ct),
                    TramoDeDesembolso.PrefijoDeCredito, ct);
            var ventas = await VentasAsync(carga, conteo, ct);
            if (ventas.IsFailure) return Result.Failure<LendingActivationDto>(ventas.Error);

            // 5. Las cuotas de nómina propia vencidas entre el día siguiente al corte y el registro (las liquidaciones no las descontaron).
            var noAplicadas = await NoAplicadasAsync(carga, hoy, ct);

            // 6. La carga activada (desde aquí nadie lee la preparación) y los cupos otorgados a mano revalidados con lo que nació.
            var ahora = reloj.UtcNow;
            carga.Status = MigrationLoadStatus.Activated;
            carga.ActivatedAt = ahora;
            await db.SaveChangesAsync(ct);
            var revalidados = revalidacion is null ? 0 : await revalidacion.RevalidarAsync(carga, ct);

            // 7. CarteraActivada y la activación.
            var activacionPublicId = Guid.NewGuid();
            Guid? basePublicId = cierreBase is null ? null : Guid.NewGuid();
            var mensaje = await emision.ActivacionAsync(activacionPublicId, request.ActivationDate, carga, cierreBase: basePublicId, capitalTotal: conteo.Capital, ct);
            if (mensaje.IsFailure) return Result.Failure<LendingActivationDto>(mensaje.Error);

            var activacion = new LendingActivation
            {
                PublicId = activacionPublicId,
                MigrationLoadId = carga.Id,
                CutoffDate = carga.CutoffDate,
                ActivationDate = request.ActivationDate,
                RegisteredByUserId = usuario,
                RegisteredAt = ahora,
                SessionItemId = punto,
                CheckVersion = carga.CurrentCheckVersion,
                BalanceCheckSha256 = carga.BalanceCheckSha256 ?? ApplyMigrationLoadCommandHandler.HuellaVacia,
                SalesReconciliationSha256 = carga.SalesReconciliationSha256 ?? ApplyMigrationLoadCommandHandler.HuellaVacia,
                IsProductionEnvironment = requisitos.EsProduccion,
                RequirementsJson = JsonSerializer.Serialize(evaluacion.Requisitos.Select(r => new { r.Code, r.Status, r.Met, r.Detail }), Json),
                ObligatoryModelUnavailable = evaluacion.ObligatoryModelUnavailable,
                CountsJson = JsonSerializer.Serialize(conteo.ParaJson(), Json),
                NonDeductionsMarked = noAplicadas.Count,
                CreditLimitsToRevalidate = revalidados,
                ProvisionalCollectionsFileSha256 = huellaDeCobros,
                ProvisionalCollectionsCount = cobros.Count,
                ProvisionalCollectionsReviewAttachmentPublicId = request.ProvisionalCollectionsAttachmentPublicId,
                CommercialAdjustmentsJson = JsonSerializer.Serialize(new
                {
                    lendingEnabledFrom = request.ActivationDate,
                    adjust = new[] { "LendingEnabledFrom", "NoResponsePolicy", "AccountsReceivableRecordedBy" },
                    warnings = evaluacion.Avisos.Select(a => a.Code),
                    acknowledged = request.AcknowledgeWarnings,
                }, Json),
                PayrollWarningAcknowledged = request.PayrollWarningAcknowledged,
                ActivatedMessagePublicId = mensaje.Value.PublicId,
            };
            db.LendingActivations.Add(activacion);

            var destino = await escritorDelRegistro.CambiarHabilitacionAsync(CodigoDelDestinoDeCartera, IntegrationParticipantRole.MessageDestination,
                IntegrationDestinations.Lending, habilitado: true, request.ActivationDate, $"Activación de Cartera: {request.Reason.Trim()}", ct);
            if (destino.IsFailure) return Result.Failure<LendingActivationDto>(destino.Error);

            await db.SaveChangesAsync(ct);

            setup.ActivationDate = request.ActivationDate;
            setup.ActivationId = activacion.Id;
            setup.ActivatedAt = ahora;
            await db.SaveChangesAsync(ct);

            // 8. El cierre base (con la carga vacía nace sin líneas: el primer cierre mensual tiene así su PreviousClose).
            if (cierreBase is not null)
            {
                var creado = await cierreBase.CrearAsync(activacion, carga, basePublicId!.Value, usuario, ct);
                if (creado.IsFailure) return Result.Failure<LendingActivationDto>(creado.Error);
            }

            // 9. Los no aplicados «Cartera sin activar» y los cobros provisionales pendientes de aplicar.
            await RegistrarNoAplicadasAsync(activacion, noAplicadas, ct);
            for (var i = 0; i < cobros.Count; i++)
            {
                var c = cobros[i];
                db.ProvisionalCollections.Add(new ProvisionalCollection
                {
                    ActivationId = activacion.Id, RowNumber = c.Fila, InvoiceNumber = c.Factura, Amount = c.Valor, ValueDate = c.FechaValor,
                    AccountingReference = c.ReferenciaContable,
                });
            }
            await db.SaveChangesAsync(ct);

            if (auditoria is not null)
                await auditoria.EmitirActivacionRegistradaAsync(activacion.PublicId, new
                {
                    load = carga.PublicId, cutoff = carga.CutoffDate, activationDate = request.ActivationDate, checkVersion = activacion.CheckVersion,
                    balanceCheckSha256 = activacion.BalanceCheckSha256, salesReconciliationSha256 = activacion.SalesReconciliationSha256,
                    provisionalCollectionsSha256 = huellaDeCobros, isProduction = activacion.IsProductionEnvironment,
                    requirements = evaluacion.Requisitos.Select(r => new { r.Code, r.Status }), counts = conteo.ParaJson(),
                    nonDeductions = noAplicadas.Count, revalidatedLimits = revalidados, provisionalCollections = cobros.Count,
                }, ct);

            return Result.Success(new LendingActivationDto(activacion.PublicId, activacion.ActivationDate, conteo.Creditos + conteo.Castigados,
                conteo.SaldosIniciales, basePublicId, conteo.Cupos, revalidados, noAplicadas.Count, cobros.Count,
                [new MensajeEmitidoDto(mensaje.Value.Type, mensaje.Value.PublicId)]));
        }, ct);
    }

    /// <summary>
    /// Activada, carga, fecha, plazo, acta, recálculo y diferencias, y requisitos, en ese orden. Devuelve la carga con seguimiento, el Id del punto y la
    /// evaluación de los requisitos.
    /// </summary>
    private async Task<Result<Validacion>> ValidarAsync(ActivateLendingCommand request, bool recalcular, CancellationToken ct)
    {
        var setup = await db.LendingSetups.AsNoTracking().FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct);
        if (setup?.ActivationDate is { } activada) return Result.Failure<Validacion>(ErroresDeActivacion.YaActivada(activada));

        var carga = await db.MigrationLoads.FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
        if (carga is null) return Result.Failure<Validacion>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));

        if (request.ActivationDate != carga.CutoffDate.AddDays(1)) return Result.Failure<Validacion>(ErroresDeActivacion.CorteInvalido(carga.CutoffDate));
        var hoy = reloj.HoyLocal;
        if (carga is { Status: MigrationLoadStatus.Applied, ActivationDeadline: { } plazo } && hoy > plazo)
            return Result.Failure<Validacion>(ErroresDeMigracion.PlazoVencido(plazo));

        // El acta (C61) por la validación única de las historias (T0311): punto Decision aprobado de una sesión cerrada sobre esta carga.
        var punto = await puntoDeActa.ValidarAsync(request.SessionItemPublicId, LendingDecisionSubjects.ActivationDifferences, carga.PublicId, ct);
        if (punto.IsFailure) return Result.Failure<Validacion>(punto.Error);

        // El cuadre y la conciliación con los saldos y las ventas de este momento: si cambiaron, hay que volver a aceptar (FR-142).
        if (recalcular && recalculo is not null && carga.Status == MigrationLoadStatus.Applied)
        {
            var r = await recalculo.RecalcularAsync(carga.PublicId, ct);
            if (r.IsFailure) return Result.Failure<Validacion>(r.Error);
            if (r.Value.Cambio) return Result.Failure<Validacion>(ErroresDeMigracion.DiferenciasSinAceptar(r.Value.Pendientes));
        }
        if (carga is { Status: MigrationLoadStatus.Applied } && (!carga.IsBalanced || !carga.SalesReconciled))
        {
            var vigentes = await ConciliacionDeVentasDeMigracion.VigentesAsync(db, carga, carga.CurrentCheckVersion, ct);
            return Result.Failure<Validacion>(ErroresDeMigracion.DiferenciasSinAceptar(vigentes.Count(d => !d.Aceptada)));
        }

        var evaluacion = await requisitos.EvaluarConAvisosAsync(carga, request.ActivationDate, hoy, ct);
        var faltantes = evaluacion.Faltantes.Where(f => !RequisitosDeActivacion.ConCodigoPropio.Contains(f.Code)).ToList();
        if (faltantes.Count > 0) return Result.Failure<Validacion>(ErroresDeActivacion.FaltanRequisitos(faltantes));

        return Result.Success(new Validacion(carga, punto.Value.ItemId, evaluacion));
    }

    // ----------------------------------------------------------------------------------------------------------- ventas --

    /// <summary>
    /// Las ventas de la conciliación vigente quedan enlazadas a su obligación (<c>ResultingLoanId</c>, data-model §21.5): la migrada, al crédito de la
    /// carga que la conserva; la cancelada antes del corte, a una obligación <b>en cero</b> que nace aquí —pagada al corte, con número del numerador, en
    /// la línea que la parte <c>sales</c> nombra o, si no la nombra o se marcó desde la lista, en la de origen comercial por defecto del tipo de deudor
    /// (<c>CommercialDefaultFor</c>), con su control y sin operaciones—. Las respuestas a Comercial salen cuando el consumidor recibe el mensaje
    /// acumulado (notas de coordinación, contradicción 11).
    /// </summary>
    private async Task<Result> VentasAsync(MigrationLoad carga, ConteoDeActivacion conteo, CancellationToken ct)
    {
        var filas = await db.MigrationSaleReconciliations
            .Where(r => r.MigrationLoadId == carga.Id && r.CheckVersion == carga.CurrentCheckVersion && !r.IsDeleted && r.ResultingLoanId == null
                        && (r.Outcome == SaleReconciliationOutcome.Migrated || r.Outcome == SaleReconciliationOutcome.CancelledBeforeCutoff))
            .ToListAsync(ct);
        if (filas.Count == 0) return Result.Success();

        var idsDeFila = filas.Where(f => f.MigrationLoanRowId is not null).Select(f => f.MigrationLoanRowId!.Value).ToList();
        var numeros = await db.MigrationLoanRows.AsNoTracking().Where(r => idsDeFila.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.SourceLoanNumber, ct);
        var nacidos = await db.Loans.AsNoTracking().Where(l => l.MigrationLoadId == carga.Id && numeros.Values.Contains(l.Number))
            .ToDictionaryAsync(l => l.Number, l => l.Id, StringComparer.Ordinal, ct);
        foreach (var f in filas.Where(f => f.Outcome == SaleReconciliationOutcome.Migrated && f.MigrationLoanRowId is not null))
            if (numeros.TryGetValue(f.MigrationLoanRowId!.Value, out var numero) && nacidos.TryGetValue(numero, out var id))
                f.ResultingLoanId = id;

        var canceladas = filas.Where(f => f.Outcome == SaleReconciliationOutcome.CancelledBeforeCutoff).ToList();
        if (canceladas.Count > 0)
        {
            if (numerador is null || registro is null) throw new InvalidOperationException("El numerador y el registro de Cartera no están registrados.");
            var idsDeVenta = canceladas.Where(f => f.MigrationSaleRowId is not null).Select(f => f.MigrationSaleRowId!.Value).ToList();
            var deLaCarga = await db.MigrationSaleRows.AsNoTracking().Where(r => idsDeVenta.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
            var oficina = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => !l.IsDeleted).Select(l => new { l.Id, l.Code }).ToListAsync(ct);
            var versiones = await new VersionVigenteDeLinea(db).LeerVariasAsync(lineas.Select(l => l.Id).ToList(), carga.CutoffDate, ct: ct);
            var ahora = reloj.UtcNow;
            foreach (var f in canceladas)
            {
                var fila = f.MigrationSaleRowId is { } idDeVenta ? deLaCarga.GetValueOrDefault(idDeVenta) : null;
                var persona = f.PersonId ?? fila?.PersonId;
                if (persona is not { } deudorId)
                    return Result.Failure(ErroresDeActivacion.ObligacionEnCeroImposible(f.InvoiceNumber, "la venta no nombra a una persona del maestro"));
                var asociado = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == deudorId).Select(p => p.IsAssociate).FirstOrDefaultAsync(ct);
                var tipo = asociado ? DebtorKind.Associate : DebtorKind.Customer;
                var linea = fila?.CreditLineCode is { } codigo ? lineas.FirstOrDefault(l => string.Equals(l.Code, codigo, StringComparison.OrdinalIgnoreCase))?.Id : null;
                linea ??= versiones.Values.Where(v => v.Version.CommercialDefaultFor == tipo).Select(v => (int?)v.Version.CreditLineId).OrderBy(i => i).FirstOrDefault();
                if (linea is null || oficina is null)
                    return Result.Failure(ErroresDeActivacion.ObligacionEnCeroImposible(f.InvoiceNumber,
                        "no hay una línea de origen comercial por defecto para el tipo de deudor ni la carga nombra una"));

                var obligacion = new Loan
                {
                    DebtorPersonId = deudorId,
                    DebtorKind = tipo,
                    BranchId = oficina.Value,
                    CreditLineId = linea.Value,
                    Origin = LoanOrigin.Migration,
                    Status = LoanStatus.PaidOff,
                    ApprovedAmount = f.Amount,
                    OpenedOn = f.SaleDate,
                    PaidOffOn = carga.CutoffDate,
                    SourceSalesDocumentPublicId = f.SaleDocumentPublicId,
                    SourcePaymentPublicId = f.CreditPaymentPublicId,
                    SourceInvoiceNumber = f.InvoiceNumber,
                    SourceMessagePublicId = f.MessagePublicId,
                    SaleObligationStatus = SaleObligationStatus.CancelledBeforeCutoff,
                    MigrationLoadId = carga.Id,
                };
                await numerador.NumerarCreditoAsync(obligacion, TramoDeDesembolso.PrefijoDeCredito, ct);
                db.Loans.Add(obligacion);
                registro.IniciarControl(obligacion, carga.CutoffDate, ahora);
                await db.SaveChangesAsync(ct);
                f.ResultingLoanId = obligacion.Id;
                conteo.ObligacionesEnCero++;
            }
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ------------------------------------------------------------------------------------------------------ no aplicadas --

    private sealed record CuotaNoAplicada(int LoanId, Guid LoanPublicId, string Numero, int BranchId, long CuotaId, int Cuota, DateOnly Vence, decimal Valor);

    /// <summary>
    /// Las cuotas de los créditos migrados con forma de pago <c>OwnPayroll</c> que vencen entre el día siguiente al corte y el registro de la
    /// activación (impagas: antes de activar no se aplicó nada). Las liquidaciones de nómina de esos días no descontaron Cartera.
    /// </summary>
    private async Task<IReadOnlyList<CuotaNoAplicada>> NoAplicadasAsync(MigrationLoad carga, DateOnly hoy, CancellationToken ct)
    {
        var desde = carga.CutoffDate.AddDays(1);
        if (hoy < desde) return [];
        return await (from l in db.Loans.AsNoTracking()
                      join v in db.LoanPayrollLinks.AsNoTracking() on l.Id equals v.LoanId
                      join p in db.RepaymentScheduleVersions.AsNoTracking() on l.Id equals p.LoanId
                      join i in db.RepaymentInstallments.AsNoTracking() on p.Id equals i.ScheduleVersionId
                      where l.MigrationLoadId == carga.Id && l.Origin == LoanOrigin.Migration && l.Status == LoanStatus.Active
                            && v.RepaymentChannel == RepaymentChannel.OwnPayroll && v.ValidTo == null && !v.IsDeleted
                            && p.VersionNumber == 1 && i.DueDate >= desde && i.DueDate <= hoy
                      orderby l.Id, i.InstallmentNumber
                      select new CuotaNoAplicada(l.Id, l.PublicId, l.Number, l.BranchId, i.Id, i.InstallmentNumber, i.DueDate, i.Total))
            .ToListAsync(ct);
    }

    private async Task RegistrarNoAplicadasAsync(LendingActivation activacion, IReadOnlyList<CuotaNoAplicada> cuotas, CancellationToken ct)
    {
        if (cuotas.Count == 0) return;
        var oficinas = cuotas.Select(c => c.BranchId).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        foreach (var c in cuotas)
        {
            Guid? alerta = null;
            if (alertas is not null)
            {
                var levantada = await alertas.LevantarAsync(new AlertaALevantar(
                    TiposDeAlerta.CuotaNoDescontadaPorNomina,
                    $"Cuota {c.Cuota} del crédito {c.Numero} no descontada por nómina",
                    string.Create(CultureInfo.InvariantCulture,
                        $"La cuota {c.Cuota} del crédito {c.Numero} venció el {c.Vence:yyyy-MM-dd}, después del corte de la migración y antes de activar Cartera: la nómina no la descontó. La cuota sigue exigible: cóbrela por caja o acuerde el pago con el deudor."),
                    EntityType: "Loan", EntityPublicId: c.LoanPublicId,
                    DedupKey: $"{TiposDeAlerta.CuotaNoDescontadaPorNomina}:{c.LoanPublicId:D}:{c.Cuota}",
                    RecipientPermissions: [NoDescontadosDelLote.PermisoDeLaAlerta], ModuloDeOrigen: "Lending",
                    ScopeBranchPublicId: sucursales.TryGetValue(c.BranchId, out var s) ? s : null), ct);
                alerta = levantada.IsSuccess ? levantada.Value.AlertPublicId : null;
            }
            db.InstallmentNonDeductions.Add(new InstallmentNonDeduction
            {
                LoanId = c.LoanId,
                RepaymentInstallmentId = c.CuotaId,
                InstallmentNumber = c.Cuota,
                DueDate = c.Vence,
                Origin = NonDeductionOrigin.Activation,
                Reason = NonDeductionReason.Other,
                ReasonText = MotivoCarteraSinActivar,
                ExpectedAmount = c.Valor,
                NotDeductedAmount = c.Valor,
                ActivationId = activacion.Id,
                AlertPublicId = alerta,
            });
        }
    }
}
