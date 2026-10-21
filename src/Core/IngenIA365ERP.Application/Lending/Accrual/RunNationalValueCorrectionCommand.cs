using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Accrual;

/// <summary>
/// Una tanda de la corrida <c>NationalValueCorrection</c> (feature 013, US4, T0500; T28, C02, C03, C05; mensajes.md §10.5, §10.12): para cada
/// crédito con días causados en <c>AffectedFrom..AffectedTo</c>, verifica el libro contra la reconstrucción con la información de ahora (que ya ve la
/// usura corregida) y registra cada diferencia por (día valor, concepto, cuota) como <c>AccrualAdjustment</c> de la corrida (<c>ProcessRunId</c>),
/// con fecha contable del período abierto; sus partes <c>NationalValueCorrectionBatch</c> emiten <c>AjusteDeCausacionPorFechaValor</c> (por lotes,
/// <c>Confirmation:{parte:N}</c>, <c>cause = NationalValueCorrection</c>, <c>correctedNationalValue</c>). Si la reconstrucción deja un saldo a favor
/// (desde US5, un interés ya pagado que la corrección baja), lo reconoce como <c>CreditBalanceRecognized</c> con <c>SaldoAFavorReconocido</c> por
/// crédito. Una liquidación con R anterior a la corrección no cambia (la reconstrucción es bitemporal). Cada día cambiado deja una versión nueva de
/// su posición. (nuevo)
/// <para>K6b (fase 24): la corrección de una <c>UVR</c> recorre sólo los créditos en UVR
/// (<see cref="EfectoDeReplicacionCorreccionDeValorNacional.CreditosAfectados"/>), y la diferencia de su corrección monetaria se registra como
/// <c>MonetaryCorrection</c> con el hecho <c>CorreccionMonetariaUvrCausada</c> (la corrida en <c>runKind</c>), no como ajuste de causación: el
/// ajuste de causación no tiene rol para el capital y la corrección monetaria sí.</para>
/// <para>K6b (decisión del dueño del 2026-10-17): en la corrección de una <c>UVR</c>, el <b>reparto de los pagos</b> se compara aparte
/// (<see cref="DiferenciaDeLibro.DePago"/>): un pago aplicado en el día corregido es el mismo en pesos pero cubre otra mezcla de conceptos. Su
/// diferencia se registra como <c>PaymentReallocation</c> de la corrida con el hecho <c>PagoRedistribuidoPorValorNacional</c> (una parte por
/// sucursal, una línea por crédito y día valor del pago), con la fecha contable del período abierto: el pago y su comprobante no se tocan.</para>
/// <para>Decisión del dueño del 2026-10-18: la corrección de una <c>USURA</c> separa el reparto igual. Antes, el capital que un pago cubre de más
/// o de menos quedaba en el ajuste de causación, que no tiene rol para él. Efecto sobre el saldo a favor de C93: el que deja un pago (filas
/// <c>CreditBalanceRecognized</c> de la operación) es parte de su reparto y viaja en <c>PagoRedistribuidoPorValorNacional</c> —<c>SaldoAFavor</c>
/// al crédito, el interés que el pago deja de cubrir al débito de <c>InteresPorCobrar</c>, que el ajuste baja contra el <c>Ingreso</c>: el mismo
/// asiento neto que hacía <c>SaldoAFavorReconocido</c> con causa <c>NationalValueCorrection</c>—. Ese hecho por crédito queda sólo para un saldo
/// a favor que no nazca de un reparto.</para>
/// </summary>
public sealed class CorreccionDeValorNacional(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    CargadorDeReconstruccion cargador,
    RegistroDeCartera registro,
    PartesDeHechosDeCartera partes,
    FechaContable fechaContable,
    ParametrosDeLaTandaDeCartera tanda,
    IngenIA365ERP.Application.Common.NationalValues.IValoresNacionales valores,
    IDateTimeService reloj,
    ValidacionPreviaDeProceso? validacionDeProceso = null)
{
    public async Task<AvanceDeCorrida> TandaAsync(LendingProcessRun corrida, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (corrida.Kind != LendingProcessKind.NationalValueCorrection || corrida.AffectedFrom is not { } desde || corrida.AffectedTo is not { } hasta)
            throw new InvalidOperationException("La tanda de la corrección es de una corrida NationalValueCorrection con los días que toca.");

        var valor = await ValorCorregidoAsync(corrida, ct);
        // K6b (decisión del dueño del 2026-10-17): en la corrección de una UVR el reparto de los pagos va en su propio hecho; desde la del
        // 2026-10-18, también en la de una usura (un pago en un día con la usura corregida cubre menos interés y más capital).
        const bool separarPagos = true;
        if (corrida.InformationCutoffAt is null)
        {
            corrida.InformationCutoffAt = reloj.UtcNow;
            corrida.NationalValueReplicationId ??= valor.ReplicationId;
            corrida.TotalLoans = await EfectoDeReplicacionCorreccionDeValorNacional.CreditosAfectados(db, valor.Code).CountAsync(c => c.LastAccruedDate >= desde, ct);
        }
        var ids = await EfectoDeReplicacionCorreccionDeValorNacional.CreditosAfectados(db, valor.Code)
            .Where(c => c.LastAccruedDate >= desde && (corrida.CursorLoanId == null || c.LoanId > corrida.CursorLoanId))
            .OrderBy(c => c.LoanId).Select(c => c.LoanId).Take(Math.Max(1, tanda.TamanoDeTanda)).ToListAsync(ct);
        if (ids.Count == 0) return AvanceDeCorrida.Terminada;

        DecisionDeValidacionPrevia? detenida = null;
        await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            var bloqueados = await cerrojo.BloquearCreditosAsync(ids, ModoDeEspera.Saltar, ct);
            corrida.SkippedLoans += ids.Count - bloqueados.Count;
            if (bloqueados.Count == 0) return Result.Success();

            var hoy = reloj.HoyLocal;
            var contable = await fechaContable.CalcularAsync(hoy, ct);
            if (contable.IsFailure) throw new InvalidOperationException($"{contable.Error.Code}: {contable.Error.Message}");
            var ahora = reloj.UtcNow;
            var controles = await db.LoanControls.Where(c => bloqueados.Contains(c.LoanId)).ToDictionaryAsync(c => c.LoanId, ct);
            var cargados = await cargador.CargarAsync(bloqueados, null, hasta, ct);
            var libros = await cargador.LibroAsync(bloqueados, hasta, ct);
            var datos = await partes.DatosAsync(cargados.Values.Where(c => c.Listo), ct);

            var conAjuste = new List<(CreditoCargado Cargado, IReadOnlyList<DiferenciaDeLibro> Diferencias)>();
            foreach (var id in bloqueados.Order())
            {
                if (!cargados.TryGetValue(id, out var cargado) || !cargado.Listo) { corrida.FailedLoans += 1; continue; }
                var tope = controles[id].LastAccruedDate < hasta ? controles[id].LastAccruedDate : hasta;
                var diferencias = MotorDeCartera.VerificarSeparandoLaCorreccionMonetaria(cargado.Entrada!, tope, libros[id], separarPagos: separarPagos)
                    .Where(x => x.Dia >= desde && x.Dia <= tope).ToList();
                if (diferencias.Count > 0) conAjuste.Add((cargado, diferencias));
                corrida.ProcessedLoans += 1;
            }
            if (conAjuste.Count == 0) return Result.Success();

            // El ajuste por partes (sin el saldo a favor, que va por crédito).
            var corregido = new CorrectedNationalValueV1
            {
                Code = valor.Code, Key = valor.Key, ValidFrom = valor.ValidFrom, SourcePublicId = valor.SourcePublicId, SourceSequence = valor.SourceSequence,
            };
            var origen = new OrigenDePartes(AccountingFactPartSource.NationalValueCorrectionBatch, corrida, null,
                new Dictionary<DateOnly, int> { [hasta] = corrida.BatchCount + 1 }, null) { FechaDeRegistro = hoy };
            var lineas = conAjuste.SelectMany(c => c.Diferencias.Where(EsAjuste).GroupBy(x => x.Dia)
                .Select(g => new LineaDeCausacion(datos[c.Cargado.Credito.Id], g.Key,
                    g.Select(x => new MovimientoDeLibro(x.Dia, LoanTransactionKind.AccrualAdjustment, x.Concepto, x.Cuota, x.Diferencia)).ToList()))).ToList();
            var masivas = await partes.MasivasAsync(origen, lineas, AjusteDeCausacionPorFechaValorV1.Type, corregido, ct);
            if (masivas.IsFailure) throw new InvalidOperationException($"{masivas.Error.Code}: {masivas.Error.Message}");
            // K6b (fase 24): la corrección monetaria de los créditos en UVR, en su propio hecho.
            var lineasCmu = conAjuste.SelectMany(c => c.Diferencias.Where(x => x.DeCorreccionMonetaria).GroupBy(x => x.Dia)
                .Select(g => new LineaDeCausacion(datos[c.Cargado.Credito.Id], g.Key,
                    g.Select(x => new MovimientoDeLibro(x.Dia, LoanTransactionKind.MonetaryCorrection, x.Concepto, x.Cuota, x.Diferencia)).ToList()))).ToList();
            var cmu = lineasCmu.Count == 0
                ? Result.Success(PartesEmitidas.Vacias)
                : await partes.MasivasAsync(origen, lineasCmu, CorreccionMonetariaUvrCausadaV1.Type, corregido, ct);
            if (cmu.IsFailure) throw new InvalidOperationException($"{cmu.Error.Code}: {cmu.Error.Message}");
            // K6b (decisión del dueño del 2026-10-17): el reparto de los pagos de los días corregidos, en su propio hecho.
            var lineasDeReparto = conAjuste.SelectMany(c => c.Diferencias.Where(x => x.DePago).GroupBy(x => x.Dia)
                .Select(g => new LineaDeCausacion(datos[c.Cargado.Credito.Id], g.Key,
                    g.Select(x => new MovimientoDeLibro(x.Dia, LoanTransactionKind.PaymentReallocation, x.Concepto, x.Cuota, x.Diferencia)).ToList()))).ToList();
            var reparto = lineasDeReparto.Count == 0
                ? Result.Success(PartesEmitidas.Vacias)
                : await partes.MasivasAsync(origen, lineasDeReparto, PagoRedistribuidoPorValorNacionalV1.Type, corregido, ct);
            if (reparto.IsFailure) throw new InvalidOperationException($"{reparto.Error.Code}: {reparto.Error.Message}");

            var hechos = new List<HechoPorCredito>();
            var filas = new List<LoanTransaction>();
            // US9 (T0702): la categoría contable vigente de cada crédito a la fecha contable del ajuste.
            var categorias = await new CategoriaContableDeCartera(db).VigentesAsync(conAjuste.Select(c => c.Cargado.Credito.Id).ToList(), contable.Value, ct);
            foreach (var (cargado, diferencias) in conAjuste)
            {
                var loanId = cargado.Credito.Id;
                foreach (var x in diferencias.Where(EsAjuste))
                    filas.AddRange(RegistroDeCartera.Diferencias(loanId, [x], null, corrida.Id, contable.Value, ahora, masivas.Value.Mensaje(loanId, x.Dia, x.Concepto)));
                foreach (var x in diferencias.Where(x => x.DeCorreccionMonetaria))
                    filas.Add(RegistroDeCartera.DiferenciaDeCorreccionMonetaria(loanId, x, corrida.Id, contable.Value, ahora, cmu.Value.Mensaje(loanId, x.Dia, x.Concepto)));
                foreach (var x in diferencias.Where(x => x.DePago))
                    filas.Add(RegistroDeCartera.DiferenciaDeReparto(loanId, x, corrida.Id, contable.Value, ahora, reparto.Value.Mensaje(loanId, x.Dia, x.Concepto)));
                var aFavor = diferencias.Where(x => x.Concepto == LoanConcept.CreditBalance && !x.DeCorreccionMonetaria && !x.DePago).ToList();
                if (aFavor.Count > 0)
                {
                    var dd = datos[loanId];
                    hechos.Add(new HechoPorCredito(SaldoAFavorReconocidoV1.Type, dd, hasta, new SaldoAFavorReconocidoV1
                    {
                        Loan = dd.Referencia, DebtorPersonPublicId = dd.DeudorPublicId, ValueDate = aFavor[^1].Dia,
                        RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(ahora, DateTimeKind.Utc)),
                        Dimensions = EmisionDeCartera.Dimensiones(dd.Hecho(contable.Value), categorias[loanId]),
                        Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.CreditBalance), Amount = aFavor.Sum(x => x.Diferencia) }],
                        RulesVersion = dd.Condiciones.RulesVersion, Cause = PartesDeHechosDeCartera.CausaPorCorreccion,
                    }));
                    filas.AddRange(aFavor.Select(x => new LoanTransaction
                    {
                        LoanId = loanId, ProcessRunId = corrida.Id, Kind = LoanTransactionKind.CreditBalanceRecognized, Concept = LoanConcept.CreditBalance,
                        Amount = x.Diferencia, ValueDate = x.Dia, AccountingDate = contable.Value, RegisteredAt = ahora,
                    }));
                }
            }
            var porCredito = await partes.PorCreditoAsync(origen, hechos, ct);
            if (porCredito.IsFailure) throw new InvalidOperationException($"{porCredito.Error.Code}: {porCredito.Error.Message}");
            registro.RegistrarTransacciones(filas);

            // Una versión nueva de la posición de cada día cambiado.
            var primero = conAjuste.Min(c => c.Diferencias[0].Dia);
            var ultimas = await PosicionesDeCartera.UltimasAsync(db, conAjuste.Select(c => c.Cargado.Credito.Id).ToList(), primero, hasta, ct);
            foreach (var (cargado, diferencias) in conAjuste)
            {
                var control = controles[cargado.Credito.Id];
                var tope = control.LastAccruedDate < hasta ? control.LastAccruedDate : hasta;
                for (var dia = diferencias[0].Dia; dia <= tope; dia = dia.AddDays(1))
                {
                    var u = ultimas.GetValueOrDefault((cargado.Credito.Id, dia));
                    if (u.Version == 0) continue;
                    registro.PosicionVersionada(control, MotorDeCartera.Reconstruir(cargado.Entrada!, dia), cargado.Credito, cargado.Vigentes, u.Version, u.Huella, ahora);
                }
            }
            // A8 = «Bloquear todo» (2026-10-14): con la política en Bloquear, una tanda con ajustes que Contabilidad no puede registrar no se guarda.
            if (validacionDeProceso is not null && await validacionDeProceso.BloqueoAsync(ct) is { } bloqueo)
            {
                detenida = bloqueo;
                return Result.Failure(bloqueo.Bloqueo!);
            }
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

        if (detenida is not null)
        {
            db.DescartarCambiosSalvo(corrida);
            throw await validacionDeProceso!.DetenerAsync(detenida, $"la corrección por {valor.Code}", ct);
        }

        db.DescartarCambiosSalvo(corrida);
        corrida.CursorLoanId = ids.Max();
        return AvanceDeCorrida.Pendiente;
    }

    /// <summary>
    /// Lo que va como ajuste de causación: ni el saldo a favor (va por crédito), ni la corrección monetaria ni el reparto de un pago (van en su hecho).
    /// </summary>
    private static bool EsAjuste(DiferenciaDeLibro x) => x.Concepto != LoanConcept.CreditBalance && !x.DeCorreccionMonetaria && !x.DePago;

    private sealed record ValorLeido(string Code, string Key, DateOnly ValidFrom, Guid SourcePublicId, long SourceSequence, int? ReplicationId);

    private async Task<ValorLeido> ValorCorregidoAsync(LendingProcessRun corrida, CancellationToken ct)
    {
        var texto = corrida.ExecutionOrigin.StartsWith(EfectoDeReplicacionCorreccionDeValorNacional.PrefijoDeOrigen, StringComparison.Ordinal)
            ? corrida.ExecutionOrigin[EfectoDeReplicacionCorreccionDeValorNacional.PrefijoDeOrigen.Length..]
            : string.Empty;
        if (!Guid.TryParse(texto, out var fuente))
            throw new InvalidOperationException($"La corrida {corrida.PublicId} no dice qué valor nacional la provocó (ExecutionOrigin «{corrida.ExecutionOrigin}»).");
        var copia = await valores.LeerCopiaAsync(fuente, ct)
                    ?? throw new InvalidOperationException($"El valor nacional {fuente} de la corrida {corrida.PublicId} no está replicado.");
        return new ValorLeido(copia.Codigo, copia.Clave, copia.ValidFrom, copia.SourcePublicId, copia.Sequence, copia.ReplicationId);
    }
}

/// <summary>Una tanda de la corrida <c>NationalValueCorrection</c> (sin ruta; la corre el procesador). (nuevo)</summary>
public sealed record RunNationalValueCorrectionCommand(Guid ProcessRunPublicId) : IRequest<Result<AvanceDeCorrida>>;

public sealed class RunNationalValueCorrectionCommandValidator : AbstractValidator<RunNationalValueCorrectionCommand>
{
    public RunNationalValueCorrectionCommandValidator() => RuleFor(x => x.ProcessRunPublicId).NotEmpty();
}

public sealed class RunNationalValueCorrectionCommandHandler(IApplicationDbContext db, CorreccionDeValorNacional correccion, IDateTimeService reloj)
    : IRequestHandler<RunNationalValueCorrectionCommand, Result<AvanceDeCorrida>>
{
    public async Task<Result<AvanceDeCorrida>> Handle(RunNationalValueCorrectionCommand request, CancellationToken ct)
    {
        var corrida = db.LendingProcessRuns.Local.FirstOrDefault(r => r.PublicId == request.ProcessRunPublicId)
                      ?? await db.LendingProcessRuns.FirstOrDefaultAsync(r => r.PublicId == request.ProcessRunPublicId && !r.IsDeleted, ct);
        if (corrida is null || corrida.Kind != LendingProcessKind.NationalValueCorrection) return Result.Failure<AvanceDeCorrida>(ErroresDeCorridas.Inexistente());
        if (corrida.Status == LendingProcessRunStatus.Requested)
        {
            corrida.Status = LendingProcessRunStatus.Running;
            corrida.StartedAt ??= reloj.UtcNow;
        }
        var avance = await correccion.TandaAsync(corrida, ct);
        corrida.BatchCount += 1;
        corrida.LastHeartbeatAt = reloj.UtcNow;
        if (avance == AvanceDeCorrida.Terminada)
        {
            corrida.Status = corrida.FailedLoans > 0 ? LendingProcessRunStatus.CompletedWithErrors : LendingProcessRunStatus.Completed;
            corrida.FinishedAt = reloj.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(avance);
    }
}

/// <summary>El ejecutor de las corridas <c>NationalValueCorrection</c> que conduce <see cref="ConductorDeCorridasDeCartera"/>. (nuevo)</summary>
public sealed class EjecutorDeLaCorreccionDeValorNacional(CorreccionDeValorNacional correccion) : IEjecutorDeCorridaDeCartera
{
    public LendingProcessKind Clase => LendingProcessKind.NationalValueCorrection;

    public Task<AvanceDeCorrida> EjecutarTandaAsync(LendingProcessRun corrida, TimeSpan presupuesto, CancellationToken ct) => correccion.TandaAsync(corrida, ct);
}
