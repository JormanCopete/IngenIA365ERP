using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

/// <summary>
/// El detalle de un crédito en el recálculo de la carga desde el corte (2026-11-24, feature 025, fase 3), tal como se guarda en la línea del borrador
/// (<c>LND_AccrualProcessLines.Detail</c>, JSON) y se muestra: el corte, el capital no vencido al corte, la tasa sellada (E.A. y, si la hay, MV) y la MV
/// propuesta, la cuota del corte con el inicio de su período y los días que le faltaron, el interés del período completo, lo ya causado y lo cargado, el
/// ajuste con su cuota destino y lo que cambia la reconstrucción desde el corte (el ajuste de causación y lo redistribuido de los pagos). (nuevo)
/// </summary>
public sealed record DetalleDelCorte(
    DateOnly Corte,
    decimal CapitalAlCorte,
    decimal TasaEA,
    decimal? TasaMV,
    decimal? MvPropuesta,
    int? CuotaDelCorte,
    DateOnly? InicioDelPeriodo,
    int DiasFaltantes,
    decimal InteresDelPeriodo,
    decimal YaCausado,
    decimal Cargado,
    decimal Ajuste,
    int? CuotaDestino,
    decimal AjusteDeCausacion,
    decimal Redistribuido)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public static DetalleDelCorte? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<DetalleDelCorte>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// La clase <c>CutoverRecalculation</c> del proceso de causación (2026-11-24, feature 025, fase 3, HU1 + HU3; FR-001–FR-006, FR-013; D1, D2): el
/// <b>recálculo de la carga desde el corte</b>, sin borrar nada.
/// <list type="bullet">
/// <item><b>Qué toma</b>: los créditos nacidos de una carga de migración (con plan migrado) del alcance y de los filtros. Quedan fuera, con su motivo, los
/// cancelados, castigados, en UVR, con la causación suspendida, de pago único, los ya ajustados (<c>LND_LoanControls.CutoverAdjustedAt</c>), los que no
/// tienen cuota sin vencer donde cobrar y los que no cambian.</item>
/// <item><b>La tasa</b> (HU3, D2): si la E.A. sellada está a menos de 0,005 puntos de una MV redonda (<see cref="AjusteDelCorteDeCarga.MensualRedondaCercana"/>),
/// el crédito pasa a esa MV en 30/360 lineal con vigencia desde el corte; si no, conserva su tasa.</item>
/// <item><b>El interés del período</b> (HU1, D1): lo que la primera cuota posterior al corte dejó por fuera
/// (<see cref="AjusteDelCorteDeCarga.Calcular"/>), si pasa de un peso, se carga en la primera cuota sin vencer a la fecha de firma, sin mora.</item>
/// <item><b>El recálculo</b> (D1): la reconstrucción desde el corte con la tasa nueva, el ajuste y los mismos pagos se compara con el libro hasta lo causado;
/// cada diferencia es una fila nueva —ajuste de causación (<c>AjusteDeCausacionPorFechaValor</c>) o reparto de un pago
/// (<c>PagoRedistribuidoPorValorNacional</c>)—, como la corrección por valor nacional. Nada se edita ni se borra (Principio XI).</item>
/// <item><b>En firme</b>, con los créditos bloqueados y el cálculo igual al del borrador: una corrida <c>CutoverRecalculation</c> (ya terminada) sella por
/// crédito la versión de condiciones nueva (causa <c>CutoverCorrection</c>, vigente desde el corte), escribe la fila <c>CutoverInterestAdjustment</c>
/// (hecho <c>InteresDelCorte</c>) y las diferencias, una versión nueva del plan (el capital no cambia; la cuota destino lleva el ajuste en su interés), las
/// posiciones del día cambiadas, y marca el crédito; los hechos esperan con el alcance de la causación manual y el proceso ordena su lote. La validación
/// previa contable (<c>Contabilidad.FaltaDeRegla = Bloquear</c>) puede impedirlo.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class RecalculoDelCorte(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    PartesDeHechosDeCartera partes,
    RegistroDeCartera registro,
    ICerrojoDeCartera cerrojo,
    FechaContable fechaContable,
    IActorActual actorActual,
    IDateTimeService reloj,
    ValidacionPreviaDeProceso? validacion = null) : IClaseDeProcesoDeCausacion
{
    /// <summary>Créditos que se reconstruyen a la vez.</summary>
    private const int Trozo = 200;

    public AccrualProcessKind Clase => AccrualProcessKind.CutoverRecalculation;

    /// <summary>Un crédito recalculado: su motivo si queda fuera, el detalle y, si entra, la entrada final y las diferencias con el libro.</summary>
    private sealed record Recalculado(
        int CreditoId,
        string Numero,
        int Oficina,
        int Linea,
        AccrualExclusionReason? Motivo,
        DetalleDelCorte? Detalle,
        decimal? MvPropuesta,
        IReadOnlyList<DiferenciaDeLibro> Diferencias,
        CreditoCargado? Cargado,
        EntradaDeReconstruccion? Entrada,
        DateOnly Tope)
    {
        public decimal Ajuste => Detalle?.Ajuste ?? 0m;

        public int? Destino => Detalle?.CuotaDestino;

        public bool Incluido => Motivo is null;
    }

    private sealed record Candidato(int Id, string Numero, int Oficina, int Linea, LoanStatus Estado);

    // ---------------------------------------------------------------------------------------------------------- calcular --

    public async Task<Result<CalculoDelProceso>> CalcularAsync(ContextoDelCalculo contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var p = contexto.Parametros;
        var hoy = contexto.Hoy;
        var avisos = new List<AvisoDelCalculo>();

        var consulta = db.Loans.AsNoTracking().Where(l => !l.IsDeleted && l.Origin == LoanOrigin.Migration
            && (l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff || l.Status == LoanStatus.PaidOff)
            && db.RepaymentScheduleVersions.Any(v => v.LoanId == l.Id && v.IsMigratedPlan));
        if (p.OficinaId is { } oficina) consulta = consulta.Where(l => l.BranchId == oficina);
        if (p.LineaId is { } linea) consulta = consulta.Where(l => l.CreditLineId == linea);
        if (p.PagaduriaId is { } pagaduria)
            consulta = consulta.Where(l => db.LoanPayrollLinks.Any(v => v.LoanId == l.Id && !v.IsDeleted && v.ValidTo == null && v.PaymasterPersonId == pagaduria));
        var candidatos = (await consulta.Select(l => new Candidato(l.Id, l.Number, l.BranchId, l.CreditLineId, l.Status)).ToListAsync(ct))
            .Where(c => contexto.Alcance.IncluyeOficina(c.Oficina) && contexto.Alcance.IncluyeLinea(c.Linea))
            .OrderBy(c => c.Id).ToList();
        if (candidatos.Count == 0)
        {
            avisos.Add(new AvisoDelCalculo("Lending.AccrualProcess.NoMigratedLoans",
                "No hay créditos nacidos de una carga de migración en su alcance y en los filtros.", false));
            return Result.Success(new CalculoDelProceso([], [], avisos));
        }

        var recalculados = new List<Recalculado>();
        foreach (var trozo in candidatos.Chunk(Trozo))
            recalculados.AddRange(await RecalcularAsync(trozo, hoy, reloj.UtcNow, ct));
        var sinReconstruir = candidatos.Count - recalculados.Count;
        if (sinReconstruir > 0)
            avisos.Add(new AvisoDelCalculo("Lending.AccrualProcess.CutoverNotRebuilt",
                $"{sinReconstruir.ToString(CultureInfo.InvariantCulture)} créditos cargados no se pudieron reconstruir (falta la usura o sus condiciones): quedan fuera.",
                false));
        var sueltos = recalculados.Where(r => r.Incluido && r.Diferencias.Any(SaldoAFavorSuelto)).Select(r => r.Numero).ToList();
        if (sueltos.Count > 0)
            avisos.Add(new AvisoDelCalculo("Lending.AccrualProcess.CutoverCreditBalance",
                $"El recálculo deja un saldo a favor que no sale de un pago en {string.Join(", ", sueltos.Take(5))}: revíselos con soporte antes de ponerlo en firme.",
                true));

        // Las líneas: una por crédito con su detalle (el interés del corte) y, si cambian, el ajuste de causación y lo redistribuido.
        var lineas = new List<LineaDelCalculo>();
        foreach (var r in recalculados)
        {
            lineas.Add(new LineaDelCalculo(r.CreditoId, InteresDelCorteV1.Type, LoanConcept.CurrentInterest, r.Ajuste, null,
                r.Incluido ? AccrualProcessItemStatus.Included : AccrualProcessItemStatus.Excluded, r.Motivo, r.Oficina, r.Linea)
            {
                Cuota = r.Incluido ? r.Destino : null, FechaDeReconocimiento = r.Incluido ? hoy : null, Detalle = r.Detalle?.Serializar(),
            });
            if (!r.Incluido) continue;
            if (r.Detalle!.AjusteDeCausacion != 0m)
                lineas.Add(new LineaDelCalculo(r.CreditoId, AjusteDeCausacionPorFechaValorV1.Type, null, r.Detalle.AjusteDeCausacion, null,
                    AccrualProcessItemStatus.Included, null, r.Oficina, r.Linea));
            if (r.Detalle.Redistribuido != 0m)
                lineas.Add(new LineaDelCalculo(r.CreditoId, PagoRedistribuidoPorValorNacionalV1.Type, null, r.Detalle.Redistribuido, null,
                    AccrualProcessItemStatus.Included, null, r.Oficina, r.Linea));
        }

        // Los hechos: por tipo y sucursal, del día de la firma, con la fecha contable que tendrán.
        var hechos = new List<HechoDelCalculo>();
        var incluidos = recalculados.Where(r => r.Incluido).ToList();
        if (incluidos.Count == 0)
        {
            avisos.Add(new AvisoDelCalculo("Lending.AccrualProcess.NothingToAdjust",
                "Ningún crédito cargado de su alcance tiene algo que recalcular desde el corte: todos quedaron fuera con su motivo.", false));
            return Result.Success(new CalculoDelProceso(hechos, Ordenar(lineas), avisos));
        }
        var contable = await fechaContable.CalcularAsync(hoy, ct);
        if (contable.IsFailure)
            avisos.Add(new AvisoDelCalculo(contable.Error.Code, $"No hay fecha contable para el {hoy:yyyy-MM-dd}: {contable.Error.Message}", true));
        var fecha = contable.IsSuccess ? contable.Value : hoy;
        foreach (var g in incluidos.GroupBy(r => r.Oficina).OrderBy(g => g.Key))
        {
            hechos.Add(new HechoDelCalculo(null, InteresDelCorteV1.Type, hoy, fecha, g.Key, g.Count(), g.Sum(r => r.Ajuste),
                AccrualProcessItemStatus.Included, null, false, null));
            var conAjuste = g.Where(r => r.Detalle!.AjusteDeCausacion != 0m).ToList();
            if (conAjuste.Count > 0)
                hechos.Add(new HechoDelCalculo(null, AjusteDeCausacionPorFechaValorV1.Type, hoy, fecha, g.Key, conAjuste.Count,
                    conAjuste.Sum(r => r.Detalle!.AjusteDeCausacion), AccrualProcessItemStatus.Included, null, false, null));
            var conReparto = g.Where(r => r.Detalle!.Redistribuido != 0m).ToList();
            if (conReparto.Count > 0)
                hechos.Add(new HechoDelCalculo(null, PagoRedistribuidoPorValorNacionalV1.Type, hoy, fecha, g.Key, conReparto.Count,
                    conReparto.Sum(r => r.Detalle!.Redistribuido), AccrualProcessItemStatus.Included, null, false, null));
        }
        return Result.Success(new CalculoDelProceso(hechos, Ordenar(lineas), avisos));
    }

    private static List<LineaDelCalculo> Ordenar(IEnumerable<LineaDelCalculo> lineas) =>
        lineas.OrderBy(l => l.CreditoId).ThenBy(l => l.Tipo, StringComparer.Ordinal).ThenBy(l => l.Estado).ToList();

    /// <summary>Un saldo a favor que el recálculo deja fuera de un pago (no tiene con qué contabilizarse en este proceso).</summary>
    private static bool SaldoAFavorSuelto(DiferenciaDeLibro d) => d.Concepto == LoanConcept.CreditBalance && !d.DePago && !d.DeCorreccionMonetaria;

    /// <summary>Lo que va como ajuste de causación: ni el saldo a favor ni el reparto de un pago (van aparte).</summary>
    private static bool EsAjuste(DiferenciaDeLibro d) => d.Concepto != LoanConcept.CreditBalance && !d.DeCorreccionMonetaria && !d.DePago;

    /// <summary>
    /// Recalcula los créditos de un trozo a la fecha <paramref name="hoy"/>: los que no se pudieron reconstruir no vienen; los demás, con su motivo o con su
    /// detalle, su entrada final (tasa nueva y ajuste) y sus diferencias con el libro hasta lo causado.
    /// </summary>
    private async Task<IReadOnlyList<Recalculado>> RecalcularAsync(IReadOnlyCollection<Candidato> trozo, DateOnly hoy, DateTime ahora, CancellationToken ct)
    {
        var ids = trozo.Select(c => c.Id).ToList();
        var controles = await db.LoanControls.AsNoTracking().Where(c => ids.Contains(c.LoanId))
            .Select(c => new { c.LoanId, c.LastAccruedDate, c.CutoverAdjustedAt }).ToDictionaryAsync(c => c.LoanId, ct);
        var cargados = await cargador.CargarAsync(ids, null, hoy, ct);
        var libros = await cargador.LibroAsync(ids, hoy, ct);
        // El desembolso original de cada crédito cargado (el de su fila en la carga): es el inicio del período de la cuota 1. El crédito nace con el corte.
        var cargas = cargados.Values.Select(x => x.Credito.MigrationLoadId).OfType<int>().Distinct().ToList();
        var numeros = trozo.Select(c => c.Numero).ToList();
        var desembolsos = (await (from fila in db.MigrationLoanRows.AsNoTracking()
                                  join parte in db.MigrationLoadParts.AsNoTracking() on fila.MigrationLoadPartId equals parte.Id
                                  where cargas.Contains(parte.MigrationLoadId) && numeros.Contains(fila.SourceLoanNumber)
                                        && parte.Status == MigrationPartStatus.Imported
                                  select new { parte.MigrationLoadId, fila.SourceLoanNumber, fila.DisbursementDate }).ToListAsync(ct))
            .GroupBy(x => (x.MigrationLoadId, x.SourceLoanNumber)).ToDictionary(g => g.Key, g => g.Min(x => x.DisbursementDate));
        var salida = new List<Recalculado>();
        foreach (var c in trozo)
        {
            if (!cargados.TryGetValue(c.Id, out var cargado) || !cargado.Listo || !controles.TryGetValue(c.Id, out var control)) continue;
            var entrada = cargado.Entrada!;
            Recalculado Fuera(AccrualExclusionReason motivo, DetalleDelCorte? detalle = null) =>
                new(c.Id, c.Numero, c.Oficina, c.Linea, motivo, detalle, null, [], cargado, null, control.LastAccruedDate);

            var corte = cargado.Operaciones.Where(o => o.Kind == LoanOperationKind.OpeningBalance).Select(o => (DateOnly?)o.ValueDate).Min();
            if (corte is not { } alCorte) continue;
            if (control.CutoverAdjustedAt is not null) { salida.Add(Fuera(AccrualExclusionReason.AlreadyAdjusted)); continue; }
            if (c.Estado == LoanStatus.WrittenOff) { salida.Add(Fuera(AccrualExclusionReason.WrittenOff)); continue; }
            if (c.Estado != LoanStatus.Active) { salida.Add(Fuera(AccrualExclusionReason.NotActive)); continue; }
            if (entrada.Uvr is not null) { salida.Add(Fuera(AccrualExclusionReason.UvrLoan)); continue; }
            if (entrada.Condiciones.Periodicidad == RepaymentPeriodicity.SinglePayment) { salida.Add(Fuera(AccrualExclusionReason.NotPeriodic)); continue; }
            var tope = control.LastAccruedDate < hoy ? control.LastAccruedDate : hoy;
            if (MotorDeCartera.Reconstruir(entrada, tope).Suspendida) { salida.Add(Fuera(AccrualExclusionReason.AccrualSuspended)); continue; }

            // La tasa desde el corte (HU3) y el interés del período (HU1), con la tasa que regirá.
            var mv = entrada.Condiciones.TasaMensual is null ? AjusteDelCorteDeCarga.MensualRedondaCercana(entrada.Condiciones.TasaEfectivaAnual) : null;
            var nueva = mv is { } m ? AjusteDelCorteDeCarga.ConTasaMensual(entrada, m) : entrada;
            DateOnly? desembolso = cargado.Credito.MigrationLoadId is { } carga && desembolsos.TryGetValue((carga, c.Numero), out var original) ? original : null;
            var propuesta = AjusteDelCorteDeCarga.Calcular(nueva, alCorte, hoy, desembolso);
            if (propuesta is null) { salida.Add(Fuera(AccrualExclusionReason.NotPeriodic)); continue; }
            var detalle = new DetalleDelCorte(alCorte, propuesta.CapitalAlCorte, entrada.Condiciones.TasaEfectivaAnual, entrada.Condiciones.TasaMensual, mv,
                propuesta.CuotaDelCorte, propuesta.InicioDelPeriodo, propuesta.DiasFaltantes, propuesta.InteresDelPeriodo, propuesta.YaCausado, propuesta.Cargado,
                propuesta.Ajuste, propuesta.CuotaDestino, 0m, 0m);
            if (propuesta.Ajuste > 0m && propuesta.CuotaDestino is null) { salida.Add(Fuera(AccrualExclusionReason.NoInstallmentLeft, detalle)); continue; }

            // El recálculo desde el corte: la reconstrucción con la tasa nueva y el ajuste contra el libro (con el ajuste, si cae en lo causado).
            var final = propuesta.Ajuste > 0m
                ? nueva with { AjustesDelCorte = [.. nueva.AjustesDelCorte, new AjusteDelCorte(hoy, propuesta.CuotaDestino!.Value, propuesta.Ajuste, ahora)] }
                : nueva;
            var libro = libros[c.Id].Where(f => f.Dia <= tope).ToList();
            if (propuesta.Ajuste > 0m && hoy <= tope)
                libro.Add(new MovimientoDeLibro(hoy, LoanTransactionKind.CutoverInterestAdjustment, LoanConcept.CurrentInterest, propuesta.CuotaDestino, propuesta.Ajuste));
            var diferencias = tope > alCorte
                ? MotorDeCartera.VerificarSeparandoLaCorreccionMonetaria(final, tope, libro, separarPagos: true).Where(x => x.Dia > alCorte && x.Dia <= tope).ToList()
                : [];
            detalle = detalle with
            {
                AjusteDeCausacion = diferencias.Where(EsAjuste).Sum(x => x.Diferencia),
                Redistribuido = diferencias.Where(x => x.DePago && x.Diferencia > 0m).Sum(x => x.Diferencia),
            };
            if (mv is null && propuesta.Ajuste == 0m && diferencias.Count == 0)
            {
                salida.Add(Fuera(AccrualExclusionReason.NothingToAdjust, detalle));
                continue;
            }
            salida.Add(new Recalculado(c.Id, c.Numero, c.Oficina, c.Linea, null, detalle, mv, diferencias, cargado, final, tope));
        }
        return salida;
    }

    // ----------------------------------------------------------------------------------------------------- poner en firme --

    public async Task<Result<LoteDelProceso>> PonerEnFirmeAsync(AccrualProcess proceso, IReadOnlyList<AccrualProcessFact> incluidos, string motivo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proceso);
        ArgumentNullException.ThrowIfNull(incluidos);
        var lineas = await db.AccrualProcessLines.AsNoTracking()
            .Where(l => l.AccrualProcessId == proceso.Id && !l.IsDeleted && l.Status == AccrualProcessItemStatus.Included && l.FactType == InteresDelCorteV1.Type)
            .ToListAsync(ct);
        if (lineas.Count == 0 || incluidos.Count == 0) return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.NadaQueContabilizar());

        // 1. Los créditos bloqueados y su recálculo igual al del borrador (la tasa, el ajuste y su cuota, las diferencias); si no, el borrador ya no vale.
        var ids = lineas.Select(l => l.LoanId).Distinct().Order().ToList();
        await cerrojo.BloquearCreditosAsync(ids, ModoDeEspera.Esperar, ct);
        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        var candidatos = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.Id))
            .Select(l => new Candidato(l.Id, l.Number, l.BranchId, l.CreditLineId, l.Status)).ToListAsync(ct);
        var recalculados = new List<Recalculado>();
        foreach (var trozo in candidatos.OrderBy(c => c.Id).Chunk(Trozo)) recalculados.AddRange(await RecalcularAsync(trozo, hoy, ahora, ct));
        var porCredito = recalculados.ToDictionary(r => r.CreditoId);
        foreach (var l in lineas)
        {
            if (!porCredito.TryGetValue(l.LoanId, out var r)) return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.Desactualizado());
            if (r.Motivo == AccrualExclusionReason.AlreadyAdjusted) return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.YaAjustado(r.Numero));
            if (!r.Incluido || r.Ajuste != l.Amount || r.Destino != l.InstallmentNumber || r.Detalle?.Serializar() != l.Detail)
                return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.Desactualizado());
        }
        var contable = await fechaContable.CalcularAsync(hoy, ct);
        if (contable.IsFailure) return Result.Failure<LoteDelProceso>(contable.Error);

        // 2. La corrida que escribe, ya terminada.
        var actor = await actorActual.ObtenerAsync(ct);
        var desde = recalculados.Min(r => r.Detalle!.Corte).AddDays(1);
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.CutoverRecalculation, ProcessDate = hoy, Status = LendingProcessRunStatus.Completed,
            RequestedByKind = actor.Kind, RequestedByUserId = actor.UserId, RequestedByCentralUserId = actor.CentralUserId, RequestedByName = actor.Name,
            ExecutionOrigin = $"Proceso de causación {proceso.Number}", Reason = Recortar(motivo), InformationCutoffAt = ahora, RequestedAt = ahora,
            StartedAt = ahora, FinishedAt = ahora, AffectedFrom = desde < hoy ? desde : hoy, AffectedTo = hoy, TotalLoans = ids.Count, ProcessedLoans = ids.Count,
            BatchCount = 1,
        };
        db.LendingProcessRuns.Add(corrida);
        await db.SaveChangesAsync(ct);

        // 3. Los hechos: el interés del corte (el día de la firma), el ajuste de causación y el reparto de los pagos (por día valor), que esperan con el
        // alcance de la causación manual.
        var datos = await partes.DatosAsync(recalculados.Select(r => r.Cargado!), ct);
        var usuario = actor.CentralUserId is { } central ? new UserRefV1 { CentralUserId = central, Name = actor.Name } : null;
        var origen = new OrigenDePartes(AccountingFactPartSource.CutoverRecalculation, corrida, null, new Dictionary<DateOnly, int> { [hoy] = 1 }, usuario)
        {
            FechaDeRegistro = hoy, CausacionManual = true,
        };
        var conAjuste = recalculados.Where(r => r.Ajuste > 0m).ToList();
        var idc = conAjuste.Count == 0
            ? Result.Success(PartesEmitidas.Vacias)
            : await partes.MasivasAsync(origen, conAjuste.Select(r => new LineaDeCausacion(datos[r.CreditoId], hoy,
                [new MovimientoDeLibro(hoy, LoanTransactionKind.CutoverInterestAdjustment, LoanConcept.CurrentInterest, r.Destino, r.Ajuste)])).ToList(),
                InteresDelCorteV1.Type, ct: ct);
        if (idc.IsFailure) return Result.Failure<LoteDelProceso>(idc.Error);
        var lineasDeAjuste = recalculados.SelectMany(r => r.Diferencias.Where(EsAjuste).GroupBy(x => x.Dia)
            .Select(g => new LineaDeCausacion(datos[r.CreditoId], g.Key,
                g.Select(x => new MovimientoDeLibro(x.Dia, LoanTransactionKind.AccrualAdjustment, x.Concepto, x.Cuota, x.Diferencia)).ToList()))).ToList();
        var afv = lineasDeAjuste.Count == 0
            ? Result.Success(PartesEmitidas.Vacias)
            : await partes.MasivasAsync(origen, lineasDeAjuste, AjusteDeCausacionPorFechaValorV1.Type, ct: ct);
        if (afv.IsFailure) return Result.Failure<LoteDelProceso>(afv.Error);
        var lineasDeReparto = recalculados.SelectMany(r => r.Diferencias.Where(x => x.DePago).GroupBy(x => x.Dia)
            .Select(g => new LineaDeCausacion(datos[r.CreditoId], g.Key,
                g.Select(x => new MovimientoDeLibro(x.Dia, LoanTransactionKind.PaymentReallocation, x.Concepto, x.Cuota, x.Diferencia)).ToList()))).ToList();
        var prn = lineasDeReparto.Count == 0
            ? Result.Success(PartesEmitidas.Vacias)
            : await partes.MasivasAsync(origen, lineasDeReparto, PagoRedistribuidoPorValorNacionalV1.Type, ct: ct);
        if (prn.IsFailure) return Result.Failure<LoteDelProceso>(prn.Error);

        // 4. Por crédito: las condiciones corregidas, el interés del corte, las diferencias, el plan nuevo, las posiciones y la marca.
        var controles = await db.LoanControls.Where(c => ids.Contains(c.LoanId)).ToDictionaryAsync(c => c.LoanId, ct);
        var operacionesDeLaCarga = recalculados.Select(r => r.Cargado!.Operaciones.Where(o => o.Kind == LoanOperationKind.OpeningBalance)
            .OrderBy(o => o.ValueDate).ThenBy(o => o.Sequence).First().Id).ToList();
        var cargas = await db.LoanOperations.Where(o => operacionesDeLaCarga.Contains(o.Id)).ToDictionaryAsync(o => o.LoanId, ct);
        var versionesDePlan = await db.RepaymentScheduleVersions.AsNoTracking().Where(v => ids.Contains(v.LoanId))
            .GroupBy(v => v.LoanId).Select(g => new { LoanId = g.Key, Ultima = g.Max(v => v.VersionNumber) }).ToDictionaryAsync(x => x.LoanId, x => x.Ultima, ct);
        var filas = new List<LoanTransaction>();
        foreach (var r in recalculados)
        {
            var cargado = r.Cargado!;
            var loanId = r.CreditoId;
            var condiciones = cargado.Vigentes;
            if (r.MvPropuesta is { } mv)
            {
                condiciones = CondicionesCorregidas(cargado.Vigentes, cargado.Vigentes.VersionNumber + 1, cargas[loanId].Id, r.Detalle!.Corte, ahora, mv);
                registro.RegistrarVersionDeCondiciones(condiciones);
            }
            if (r.Ajuste > 0m)
                registro.RegistrarAjusteDelCorte(loanId, r.Destino!.Value, r.Ajuste, hoy, corrida.Id, contable.Value, ahora,
                    idc.Value.Mensaje(loanId, hoy, LoanConcept.CurrentInterest));
            foreach (var x in r.Diferencias.Where(EsAjuste))
                filas.AddRange(RegistroDeCartera.Diferencias(loanId, [x], null, corrida.Id, contable.Value, ahora, afv.Value.Mensaje(loanId, x.Dia, x.Concepto)));
            foreach (var x in r.Diferencias.Where(x => x.DePago))
                filas.Add(RegistroDeCartera.DiferenciaDeReparto(loanId, x, corrida.Id, contable.Value, ahora, prn.Value.Mensaje(loanId, x.Dia, x.Concepto)));

            // El plan nuevo: el de la reconstrucción con la tasa desde el corte, con el ajuste en el interés de la cuota destino (el capital no cambia).
            var alDia = MotorDeCartera.Reconstruir(r.Entrada!, hoy);
            if (alDia.Plan is { } plan)
                registro.RegistrarPlanRegenerado(loanId, versionesDePlan.GetValueOrDefault(loanId) + 1, condiciones, cargas[loanId], plan, r.Entrada!.Festivos,
                    ahora, r.Ajuste > 0m ? new Dictionary<int, decimal> { [r.Destino!.Value] = r.Ajuste } : null, hoy);
            controles[loanId].CutoverAdjustedAt = ahora;
        }
        registro.RegistrarTransacciones(filas);

        // Una versión nueva de la posición de cada día cambiado (desde la primera diferencia o el día de la firma).
        var cambiados = recalculados.Where(r => r.Diferencias.Count > 0 || r.Ajuste > 0m).ToList();
        if (cambiados.Count > 0)
        {
            var primero = cambiados.Min(r => r.Diferencias.Count > 0 ? r.Diferencias.Min(x => x.Dia) : hoy);
            var ultimas = await PosicionesDeCartera.UltimasAsync(db, cambiados.Select(r => r.CreditoId).ToList(), primero, hoy, ct);
            foreach (var r in cambiados)
            {
                var inicio = r.Diferencias.Count > 0 ? r.Diferencias.Min(x => x.Dia) : hoy;
                for (var dia = inicio; dia <= r.Tope; dia = dia.AddDays(1))
                {
                    var u = ultimas.GetValueOrDefault((r.CreditoId, dia));
                    if (u.Version == 0) continue;
                    registro.PosicionVersionada(controles[r.CreditoId], MotorDeCartera.Reconstruir(r.Entrada!, dia), r.Cargado!.Credito, r.Cargado.Vigentes,
                        u.Version, u.Huella, ahora);
                }
            }
        }
        if (validacion is not null && await validacion.BloqueoAsync(ct) is { } bloqueo)
            return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.Bloqueado(
                [bloqueo.Bloqueo?.Message ?? "Contabilidad no puede registrar el recálculo del corte: complete la matriz de Cartera (InteresDelCorte) y recalcule."]));
        corrida.PartCount = idc.Value.Partes.Count + afv.Value.Partes.Count + prn.Value.Partes.Count;
        await db.SaveChangesAsync(ct);

        // 5. El lote con exactamente esos hechos (los que pasan a Contabilidad).
        var mensajes = idc.Value.Mensajes.Concat(afv.Value.Mensajes).Concat(prn.Value.Mensajes).Select(m => m.Id).ToList();
        if (mensajes.Count == 0) return Result.Success(new LoteDelProceso(null, null, 0));
        var entregas = await db.IntegrationMessageDeliveries.Include(d => d.Message)
            .Where(d => d.Destination == IntegrationDestinations.Accounting && mensajes.Contains(d.MessageId) && d.Status == DeliveryStatus.InBatch
                        && d.BatchId == null)
            .ToListAsync(ct);
        if (entregas.Count == 0) return Result.Success(new LoteDelProceso(null, null, 0));
        var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting, BatchTrigger.Manual,
            actor, $"Proceso de causación {proceso.Number}: {motivo}", ahora);
        lote.CutoffMessageId = entregas.Max(d => d.MessageId);
        lote.DateFrom = entregas.Min(d => d.Message!.OperationDate);
        lote.DateTo = entregas.Max(d => d.Message!.OperationDate);
        lote.MessageCount = entregas.Count;
        lote.DocumentCount = entregas.Select(d => d.Message!.OriginPublicId).Distinct().Count();
        db.IntegrationBatches.Add(lote);
        await db.SaveChangesAsync(ct);
        LotesDeIntegracion.Asignar(lote, entregas);
        return Result.Success(new LoteDelProceso(lote.PublicId, lote.Number, entregas.Count));
    }

    /// <summary>
    /// La versión de condiciones de la corrección de la carga (HU3, D2): la copia de <paramref name="v"/> con la corriente en mensual vencida
    /// <paramref name="mensual"/> (la E.A. derivada a seis decimales), 30/360 y lineal, la causa <c>CutoverCorrection</c> y vigencia desde el corte. Cuelga de
    /// la operación de la carga (la que la hizo regir desde el corte) y se supo al firmar. Hecho de sólo inserción: la anterior no se toca.
    /// </summary>
    public static LoanTermsVersion CondicionesCorregidas(LoanTermsVersion v, int numero, long operacionDeLaCarga, DateOnly corte, DateTime ahora, decimal mensual)
    {
        ArgumentNullException.ThrowIfNull(v);
        return new LoanTermsVersion
        {
            LoanId = v.LoanId, VersionNumber = numero, OperationId = operacionDeLaCarga, EffectiveValueDate = corte, RegisteredAt = ahora,
            Cause = TermsVersionCause.CutoverCorrection, NoveltyId = null, RulesVersion = v.RulesVersion, CreditLineVersionId = v.CreditLineVersionId,
            ModalityCode = v.ModalityCode, SubsegmentCode = v.SubsegmentCode, ForEmployees = v.ForEmployees, InterestTaxTreatment = v.InterestTaxTreatment,
            RateKind = v.RateKind, AnnualRate = EquivalenciaDeTasas.EfectivaInformativaDeMensual(mensual), RateBasis = RateBasis.MonthlyNominal,
            MonthlyRate = mensual, IndexCode = v.IndexCode, IndexKey = v.IndexKey, Margin = v.Margin, RateFloor = v.RateFloor, RateCeiling = v.RateCeiling,
            ResetPeriodicity = v.ResetPeriodicity, NextResetDate = v.NextResetDate, DefaultRateKind = v.DefaultRateKind, DefaultRate = v.DefaultRate,
            DefaultRateFactor = v.DefaultRateFactor, DefaultOnInterestAllowed = v.DefaultOnInterestAllowed, DayCount = DayCountConvention.Thirty360,
            RateEquivalence = RateEquivalence.Linear, AmortizationSystem = v.AmortizationSystem, Periodicity = v.Periodicity, Installments = v.Installments,
            TermMonths = v.TermMonths, FirstDueDate = v.FirstDueDate, PaymentDay = v.PaymentDay, GraceKind = v.GraceKind, GraceInstallments = v.GraceInstallments,
            GraceInterestTreatment = v.GraceInterestTreatment, GraceDistributionInstallments = v.GraceDistributionInstallments,
            ExtraordinaryInstallments = v.ExtraordinaryInstallments, RoundingUnit = v.RoundingUnit, RoundingMode = v.RoundingMode,
            RoundingMoment = v.RoundingMoment, DenominationUnit = v.DenominationUnit, AllocationOrder = v.AllocationOrder, ConceptPriority = v.ConceptPriority,
            SuspenseWithItsConcept = v.SuspenseWithItsConcept, AllowsSpecificInstallmentOutOfOrder = v.AllowsSpecificInstallmentOutOfOrder,
            NoteRecalculation = v.NoteRecalculation, Insurances = v.Insurances, SubsidizedPurchase = v.SubsidizedPurchase,
            ResultingEffectiveRate = v.ResultingEffectiveRate, ChargesSpread = v.ChargesSpread, UsuryValuePublicId = v.UsuryValuePublicId,
            RestructuringCount = v.RestructuringCount, RestructuredOn = v.RestructuredOn, InputsSha256 = v.InputsSha256,
        };
    }

    private static string Recortar(string texto) => texto.Length <= 1000 ? texto : texto[..1000];
}
