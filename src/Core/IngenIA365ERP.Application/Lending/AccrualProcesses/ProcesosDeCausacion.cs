using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

/// <summary>
/// Lo común de los procesos de causación (2026-11-16, pedido del dueño; feature 024, fase 3), sea cual sea su clase (<see cref="IClaseDeProcesoDeCausacion"/>):
/// <list type="bullet">
/// <item><b>Leer</b> con el alcance: un proceso se ve y se opera si su oficina y su línea están en el alcance de quien opera (sin alcance total de
/// oficinas, el proceso tiene que ser de una oficina suya; igual con las líneas). Fuera, el mismo 404 y <c>Lending.Access.OutOfScope</c>.</item>
/// <item><b>Calcular</b>: la clase arma hechos y líneas, y aquí se suman los bloqueos de todas las clases —la causación del motor al día hasta el fin del
/// rango y la usura de los días que faltan— y, en un reproceso, los créditos con abonos posteriores y lo que contabilizó el reprocesado.</item>
/// <item><b>Cruces</b>: un solo proceso abierto por concepto y ninguno en firme del mismo concepto que se cruce en rango y filtros (salvo el que se
/// reprocesa).</item>
/// <item><b>Poner en firme</b> desde la aprobación (<see cref="FuenteDeAprobacionDeCausacion"/>): recalcula y compara la huella —si algo cambió, la
/// aprobación no queda—, toma el cerrojo y deja que la clase ordene su lote.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class ProcesosDeCausacion(
    IApplicationDbContext db,
    IEnumerable<IClaseDeProcesoDeCausacion> clases,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ISeriesRegulatorias series,
    ICerrojoDeCartera cerrojo,
    NumeradorDeCartera numerador,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    TareasTrasElCommit? tareasTrasElCommit = null,
    ISenalDeMensajes? senal = null)
{
    /// <summary>El prefijo del número de un proceso de causación.</summary>
    public const string Prefijo = "PCA-";

    private const int Trozo = 500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Lo que se guarda de los bloqueos y avisos de un cálculo.</summary>
    private sealed record ProblemasGuardados(IReadOnlyList<AccrualProcessIssueDto> Issues);

    public IClaseDeProcesoDeCausacion Clase(AccrualProcessKind clase) =>
        clases.FirstOrDefault(c => c.Clase == clase) ?? throw new InvalidOperationException($"No hay clase de proceso de causación registrada para «{clase}».");

    // ------------------------------------------------------------------------------------------------------------ leer --

    /// <summary>¿Ve y opera el proceso quien tiene <paramref name="alcance"/>? (oficina y línea del proceso dentro, o alcance total).</summary>
    public static bool EnAlcance(AccrualProcess proceso, AlcanceDeCartera alcance)
    {
        ArgumentNullException.ThrowIfNull(proceso);
        ArgumentNullException.ThrowIfNull(alcance);
        return (alcance.TodasLasOficinas || proceso.BranchId is { } o && alcance.Oficinas.Contains(o))
               && (alcance.TodasLasLineas || proceso.CreditLineId is { } l && alcance.Lineas.Contains(l));
    }

    /// <summary>Los procesos que ve quien tiene <paramref name="alcance"/>.</summary>
    public static IQueryable<AccrualProcess> Visibles(IQueryable<AccrualProcess> procesos, AlcanceDeCartera alcance)
    {
        ArgumentNullException.ThrowIfNull(alcance);
        var todasLasOficinas = alcance.TodasLasOficinas;
        var todasLasLineas = alcance.TodasLasLineas;
        var oficinas = alcance.Oficinas.ToArray();
        var lineas = alcance.Lineas.ToArray();
        return procesos.Where(p => !p.IsDeleted
            && (todasLasOficinas || (p.BranchId != null && oficinas.Contains(p.BranchId.Value)))
            && (todasLasLineas || (p.CreditLineId != null && lineas.Contains(p.CreditLineId.Value))));
    }

    /// <summary>El proceso de <paramref name="publicId"/> en el alcance (seguido para escribirlo si <paramref name="seguir"/>); si no, el 404.</summary>
    public async Task<Result<AccrualProcess>> LeerAsync(Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.AccrualProcesses : db.AccrualProcesses.AsNoTracking();
        var proceso = await consulta.FirstOrDefaultAsync(p => p.PublicId == publicId && !p.IsDeleted, ct);
        if (proceso is null) return Result.Failure<AccrualProcess>(ErroresDeProcesosDeCausacion.Inexistente());
        if (EnAlcance(proceso, await alcanceDeLaPeticion.ObtenerAsync(ct))) return Result.Success(proceso);
        await auditoria.EmitirFueraDeAlcanceAsync("AccrualProcess", proceso.PublicId, ct);
        return Result.Failure<AccrualProcess>(ErroresDeProcesosDeCausacion.Inexistente());
    }

    /// <summary>
    /// Los filtros que pide quien opera, dentro de su alcance: sin alcance total de oficinas el proceso lleva una oficina suya, y sin el de líneas, una
    /// línea suya (si no, <c>Lending.AccrualProcess.ScopeFilterRequired</c>); una oficina o una línea fuera de su alcance es el mismo 404.
    /// </summary>
    public async Task<Result> FiltrosEnAlcanceAsync(int? oficina, int? linea, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (!alcance.TodasLasOficinas && oficina is null) return Result.Failure(ErroresDeProcesosDeCausacion.FiltroDeAlcance(oficina: true));
        if (!alcance.TodasLasLineas && linea is null) return Result.Failure(ErroresDeProcesosDeCausacion.FiltroDeAlcance(oficina: false));
        return await alcanceDeLaPeticion.AsegurarAsync(oficina, linea, ErroresDeProcesosDeCausacion.Inexistente(), auditoria, "AccrualProcess", null, ct);
    }

    // ------------------------------------------------------------------------------------------------------- calcular --

    /// <summary>
    /// El cálculo de <paramref name="parametros"/> con el alcance de quien opera: el de la clase más los bloqueos comunes. En un reproceso
    /// (<paramref name="reprocesado"/>), con los créditos que tuvieron abonos después de su puesta en firme y lo que contabilizó de cada crédito.
    /// </summary>
    public async Task<Result<CalculoDelProceso>> CalcularAsync(ParametrosDelProceso parametros, AccrualProcess? reprocesado, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var contexto = new ContextoDelCalculo(parametros, alcance, reloj.HoyLocal, reprocesado);
        if (reprocesado is not null)
        {
            var anteriores = (await db.AccrualProcessLines.AsNoTracking()
                    .Where(l => l.AccrualProcessId == reprocesado.Id && !l.IsDeleted && l.Status == AccrualProcessItemStatus.Included)
                    .Select(l => new { l.LoanId, l.FactType, l.Concept, l.Amount })
                    .ToListAsync(ct))
                .GroupBy(l => (l.LoanId, l.FactType, l.Concept))
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
            contexto = contexto with
            {
                Anteriores = anteriores,
                CreditosConAbonos = await AbonadosDespuesAsync(anteriores.Keys.Select(k => k.LoanId).Distinct().ToList(),
                    reprocesado.ConfirmedAt ?? DateTime.MinValue, ct),
            };
        }

        var calculado = await Clase(parametros.Clase).CalcularAsync(contexto, ct);
        if (calculado.IsFailure) return calculado;
        return Result.Success(calculado.Value.Con(await BloqueosComunesAsync(parametros, alcance, ct)));
    }

    /// <summary>
    /// Reproceso: los créditos (de <paramref name="creditos"/>) con abonos —pagos aplicados— registrados después de <paramref name="desde"/>, la puesta en
    /// firme del proceso que se reprocesa. Quedan intactos.
    /// </summary>
    private async Task<IReadOnlySet<int>> AbonadosDespuesAsync(IReadOnlyList<int> creditos, DateTime desde, CancellationToken ct)
    {
        var abonados = new HashSet<int>();
        foreach (var trozo in creditos.Chunk(Trozo))
            abonados.UnionWith(await db.LoanOperations.AsNoTracking()
                .Where(o => trozo.Contains(o.LoanId) && o.Kind == LoanOperationKind.Payment && o.RegisteredAt > desde)
                .Select(o => o.LoanId).Distinct().ToListAsync(ct));
        return abonados;
    }

    /// <summary>
    /// Los bloqueos de todas las clases: los créditos vivos del alcance y los filtros sin causar hasta el fin del rango (el ciclo diario va primero) y, si
    /// los hay, la usura que falta en sus modalidades (sin ella el motor no causa esos días).
    /// </summary>
    private async Task<IReadOnlyList<AvisoDelCalculo>> BloqueosComunesAsync(ParametrosDelProceso p, AlcanceDeCartera alcance, CancellationToken ct)
    {
        var hasta = p.Hasta;
        var vivos = CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), hasta).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (p.OficinaId is { } oficina) vivos = vivos.Where(l => l.BranchId == oficina);
        if (p.LineaId is { } linea) vivos = vivos.Where(l => l.CreditLineId == linea);
        if (p.PagaduriaId is { } pagaduria)
            vivos = vivos.Where(l => db.LoanPayrollLinks.Any(v => v.LoanId == l.Id && !v.IsDeleted && v.ValidTo == null && v.PaymasterPersonId == pagaduria));
        var atrasados = await (from c in db.LoanControls.AsNoTracking()
                               join l in vivos on c.LoanId equals l.Id
                               where c.LastAccruedDate < hasta
                               select new { l.Id, l.Number, c.LastAccruedDate }).ToListAsync(ct);
        if (atrasados.Count == 0) return [];

        var bloqueos = new List<AvisoDelCalculo>
        {
            new("Lending.AccrualProcess.AccrualBehind",
                $"{atrasados.Count.ToString(CultureInfo.InvariantCulture)} créditos no están causados hasta el {hasta:yyyy-MM-dd} (el más atrasado va hasta el "
                + $"{atrasados.Min(a => a.LastAccruedDate):yyyy-MM-dd}; por ejemplo {string.Join(", ", atrasados.OrderBy(a => a.LastAccruedDate).Take(5).Select(a => a.Number))}): "
                + "el ciclo diario tiene que causar hasta ese día antes de ponerlo en firme.", true),
        };

        // La razón más común del atraso: la usura de un día que no llegó. Una vez por modalidad de los atrasados.
        var ids = atrasados.Select(a => a.Id).ToList();
        var modalidades = new HashSet<(string, string?)>();
        foreach (var trozo in ids.Chunk(Trozo))
            modalidades.UnionWith((await db.LoanTermsVersions.AsNoTracking().Where(t => trozo.Contains(t.LoanId))
                    .Select(t => new { t.ModalityCode, t.SubsegmentCode }).Distinct().ToListAsync(ct))
                .Select(t => (t.ModalityCode, t.SubsegmentCode)));
        var desde = atrasados.Min(a => a.LastAccruedDate).AddDays(1);
        if (desde < p.Desde) desde = p.Desde;
        foreach (var (modalidad, subsegmento) in modalidades.Where(m => !string.IsNullOrWhiteSpace(m.Item1)).OrderBy(m => m.Item1, StringComparer.Ordinal))
        {
            var leidas = await series.LeerAsync(modalidad, subsegmento, desde, hasta, null, ct);
            if (leidas.IsFailure)
            {
                bloqueos.Add(new AvisoDelCalculo("Lending.AccrualProcess.UsuryMissing", $"Modalidad {modalidad}: {leidas.Error.Message}", true));
                continue;
            }
            if (leidas.Value.ExigeUsura && leidas.Value.DiasSinUsura.Where(d => d >= desde && d <= hasta).Order().FirstOrDefault() is { } dia && dia != default)
                bloqueos.Add(new AvisoDelCalculo("Lending.AccrualProcess.UsuryMissing",
                    $"Falta la usura certificada de la modalidad {modalidad} desde el {dia:yyyy-MM-dd}: sin ella el motor no causa esos días.", true));
        }
        return bloqueos;
    }

    // ---------------------------------------------------------------------------------------------------------- cruces --

    /// <summary>Un proceso abierto (borrador o esperando la aprobación) que comparte concepto, distinto de <paramref name="excepto"/>.</summary>
    public async Task<AccrualProcess?> AbiertoQueComparteAsync(AccrualProcessConcepts conceptos, int? excepto, CancellationToken ct)
    {
        var abiertos = await db.AccrualProcesses.AsNoTracking()
            .Where(p => !p.IsDeleted && (p.Status == AccrualProcessStatus.Draft || p.Status == AccrualProcessStatus.PendingApproval)
                        && p.Id != (excepto ?? 0))
            .ToListAsync(ct);
        return abiertos.FirstOrDefault(p => p.CompartenConcepto(conceptos));
    }

    /// <summary>
    /// Un proceso en firme que se cruza: comparte concepto, su rango toca el pedido y sus filtros pueden tomar lo mismo (dos procesos de oficinas,
    /// líneas o pagadurías distintas no se cruzan). <paramref name="reprocesado"/> no cuenta: su reproceso lo reemplaza.
    /// </summary>
    public async Task<AccrualProcess?> EnFirmeQueSeCruzaAsync(ParametrosDelProceso p, int? reprocesado, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        var desde = p.Desde;
        var hasta = p.Hasta;
        var enFirme = await db.AccrualProcesses.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == AccrualProcessStatus.Confirmed && x.Id != (reprocesado ?? 0) && x.DateFrom <= hasta && x.DateTo >= desde)
            .ToListAsync(ct);
        return enFirme.FirstOrDefault(x => x.CompartenConcepto(p.Conceptos)
            && (x.BranchId is null || p.OficinaId is null || x.BranchId == p.OficinaId)
            && (x.CreditLineId is null || p.LineaId is null || x.CreditLineId == p.LineaId)
            && (x.PaymasterPersonId is null || p.PagaduriaId is null || x.PaymasterPersonId == p.PagaduriaId));
    }

    /// <summary>
    /// Dentro de la transacción: el cerrojo de los procesos (fila de <c>LND_Setup</c> compartida y la de numeración de los procesos) y, con él tomado, los
    /// dos cruces. Así dos personas que calculan o ponen en firme a la vez pasan una después de la otra.
    /// </summary>
    public async Task<Result> TomarYVerificarCrucesAsync(ParametrosDelProceso p, int? proceso, int? reprocesado, CancellationToken ct)
    {
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await numerador.SerializarProcesosDeCausacionAsync(Prefijo, ct);
        if (await AbiertoQueComparteAsync(p.Conceptos, proceso, ct) is { } abierto)
            return Result.Failure(ErroresDeProcesosDeCausacion.OtroAbierto(abierto.Number, abierto.PublicId));
        if (await EnFirmeQueSeCruzaAsync(p, reprocesado, ct) is { } cruzado)
            return Result.Failure(ErroresDeProcesosDeCausacion.SeCruza(cruzado.Number, cruzado.PublicId));
        return Result.Success();
    }

    // -------------------------------------------------------------------------------------------------------- guardar --

    /// <summary>
    /// Reemplaza el borrador del proceso por <paramref name="calculo"/> (los hechos y las líneas de antes van a baja lógica: un borrador no es un hecho) y
    /// sella sus totales y su huella. El proceso tiene que estar guardado. No guarda.
    /// </summary>
    public async Task AplicarAsync(AccrualProcess proceso, CalculoDelProceso calculo, int usuario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proceso);
        ArgumentNullException.ThrowIfNull(calculo);
        if (proceso.Id == 0) throw new InvalidOperationException("El proceso se guarda antes de aplicarle un cálculo.");
        db.AccrualProcessFacts.RemoveRange(await db.AccrualProcessFacts.Where(h => h.AccrualProcessId == proceso.Id && !h.IsDeleted).ToListAsync(ct));
        db.AccrualProcessLines.RemoveRange(await db.AccrualProcessLines.Where(l => l.AccrualProcessId == proceso.Id && !l.IsDeleted).ToListAsync(ct));
        db.AccrualProcessFacts.AddRange(calculo.Hechos.Select(h => new AccrualProcessFact
        {
            AccrualProcessId = proceso.Id, MessagePublicId = h.MensajePublicId, FactType = h.Tipo, CausedDate = h.DiaCausado, AccountingDate = h.FechaContable,
            BranchId = h.OficinaId, LoanCount = h.Creditos, Amount = h.Monto, Status = h.Estado, ExclusionReason = h.Motivo, IsBlocking = h.Bloquea,
            Detail = h.Detalle,
        }));
        db.AccrualProcessLines.AddRange(calculo.Lineas.Select(l => new AccrualProcessLine
        {
            AccrualProcessId = proceso.Id, LoanId = l.CreditoId, FactType = l.Tipo, Concept = l.Concepto, Amount = l.Monto, PreviousAmount = l.Anterior,
            Status = l.Estado, ExclusionReason = l.Motivo, BranchId = l.OficinaId, CreditLineId = l.LineaId,
        }));
        var problemas = JsonSerializer.Serialize(new ProblemasGuardados(calculo.Avisos.Select(a => new AccrualProcessIssueDto(a.Code, a.Message, a.Blocking)).ToList()), Json);
        proceso.SellarCalculo(calculo.Huella(ParametrosDelProceso.De(proceso)), problemas, calculo.Bloquea, calculo.Creditos, calculo.Incluidos.Count(),
            calculo.Excluidos.Count(), calculo.Incluidos.Sum(h => h.Monto), calculo.Excluidos.Sum(h => h.Monto), usuario, reloj.UtcNow);
    }

    /// <summary>Los bloqueos y avisos guardados de un proceso.</summary>
    public static IReadOnlyList<AccrualProcessIssueDto> Problemas(AccrualProcess proceso)
    {
        ArgumentNullException.ThrowIfNull(proceso);
        if (string.IsNullOrWhiteSpace(proceso.IssuesJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<ProblemasGuardados>(proceso.IssuesJson, Json)?.Issues ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>¿Se puede pedir que lo pongan en firme? Borrador, sin bloqueos y con algo para contabilizar.</summary>
    public static bool PuedePedirFirma(AccrualProcess proceso) =>
        proceso.Status == AccrualProcessStatus.Draft && !proceso.HasBlockingIssues && proceso.IncludedFactCount > 0;

    // ----------------------------------------------------------------------------------------------- poner en firme --

    /// <summary>
    /// La última aprobación (la llama <see cref="FuenteDeAprobacionDeCausacion"/> en la transacción del aprobador): el proceso esperando esta solicitud,
    /// recalculado con el alcance de quien lo pidió —está en su sesión—, con la misma huella, sin bloqueos y sin cruces; la clase ordena el lote y el
    /// proceso queda en firme (y el reprocesado, reprocesado). Si algo cambió, falla y la aprobación no queda: se recalcula y se vuelve a pedir.
    /// </summary>
    public async Task<Result<AccrualProcess>> PonerEnFirmeAsync(ApprovalRequest solicitud, int aprobador, string nombre, ApprovalMethod metodo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var proceso = await db.AccrualProcesses.FirstOrDefaultAsync(p => p.PublicId == solicitud.SourcePublicId && !p.IsDeleted, ct);
            if (proceso is null) return Result.Failure<AccrualProcess>(ErroresDeProcesosDeCausacion.Inexistente());
            if (proceso.Status != AccrualProcessStatus.PendingApproval || proceso.ApprovalRequestPublicId != solicitud.PublicId)
                return Result.Failure<AccrualProcess>(ErroresDeGobierno.De(proceso.PuedeIrA(AccrualProcessStatus.Confirmed)
                    ?? new ProblemaDeCartera(AccrualProcess.CodigoEstadoInvalido, "La solicitud ya no es la de este proceso.")));
            var reprocesado = proceso.ReprocessOfId is { } id ? await db.AccrualProcesses.FirstOrDefaultAsync(p => p.Id == id, ct) : null;
            var parametros = ParametrosDelProceso.De(proceso);

            var cruces = await TomarYVerificarCrucesAsync(parametros, proceso.Id, reprocesado?.Id, ct);
            if (cruces.IsFailure) return Result.Failure<AccrualProcess>(cruces.Error);
            var calculo = await CalcularAsync(parametros, reprocesado, ct);
            if (calculo.IsFailure) return Result.Failure<AccrualProcess>(calculo.Error);
            if (!string.Equals(calculo.Value.Huella(parametros), proceso.ContentSha256, StringComparison.Ordinal))
                return Result.Failure<AccrualProcess>(ErroresDeProcesosDeCausacion.Desactualizado());
            if (calculo.Value.Bloquea) return Result.Failure<AccrualProcess>(ErroresDeProcesosDeCausacion.Bloqueado(Motivos(calculo.Value)));

            var incluidos = await db.AccrualProcessFacts.AsNoTracking()
                .Where(h => h.AccrualProcessId == proceso.Id && !h.IsDeleted && h.Status == AccrualProcessItemStatus.Included).ToListAsync(ct);
            var lote = await Clase(proceso.Kind).PonerEnFirmeAsync(proceso, incluidos, proceso.Reason ?? string.Empty, ct);
            if (lote.IsFailure) return Result.Failure<AccrualProcess>(lote.Error);

            reprocesado?.MarcarReprocesado(proceso.Id);
            proceso.PonerEnFirme(aprobador, nombre, metodo, lote.Value.LotePublicId, lote.Value.Numero, reloj.UtcNow);
            await db.SaveChangesAsync(ct);
            await auditoria.EmitirProcesoDeCausacionEnFirmeAsync(proceso.PublicId, new
            {
                number = proceso.Number, kind = proceso.Kind.ToString(), from = proceso.DateFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                to = proceso.DateTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), concepts = ConceptosDeCausacion.TiposDe(proceso.Concepts),
                facts = proceso.IncludedFactCount, amount = proceso.IncludedAmount, loans = proceso.LoanCount, batch = lote.Value.Numero,
                reprocessOf = reprocesado?.Number, contentSha256 = proceso.ContentSha256, reason = proceso.Reason, approvedBy = nombre, method = metodo.ToString(),
            }, ct);
            var mensajes = incluidos.Where(h => h.MessagePublicId is not null).Select(h => h.MessagePublicId!.Value).ToList();
            if (senal is not null && mensajes.Count > 0)
            {
                if (tareasTrasElCommit is not null) tareasTrasElCommit.Agregar(_ => { senal.Avisar(mensajes); return Task.CompletedTask; });
                else senal.Avisar(mensajes);
            }
            return Result.Success(proceso);
        }, ct);
    }

    /// <summary>Lo que impide ponerlo en firme, en palabras.</summary>
    public static IReadOnlyList<string> Motivos(CalculoDelProceso calculo)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        var motivos = calculo.Avisos.Where(a => a.Blocking).Select(a => a.Message).ToList();
        motivos.AddRange(calculo.Hechos.Where(h => h.Bloquea).Select(h => h.Detalle ?? h.Tipo).Distinct().Take(5));
        return motivos;
    }

    /// <summary>Lo que impide ponerlo en firme, de lo guardado.</summary>
    public async Task<IReadOnlyList<string>> MotivosGuardadosAsync(AccrualProcess proceso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proceso);
        var motivos = Problemas(proceso).Where(i => i.Blocking).Select(i => i.Message).ToList();
        motivos.AddRange(await db.AccrualProcessFacts.AsNoTracking()
            .Where(h => h.AccrualProcessId == proceso.Id && !h.IsDeleted && h.IsBlocking)
            .Select(h => h.Detail ?? h.FactType).Distinct().Take(5).ToListAsync(ct));
        return motivos;
    }

    // --------------------------------------------------------------------------------------------------------- el DTO --

    /// <summary>El proceso con su borrador (hechos y totales en el alcance de quien consulta), su aprobación y su lote.</summary>
    public async Task<AccrualProcessDto> DtoAsync(AccrualProcess p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var hechos = await db.AccrualProcessFacts.AsNoTracking().Where(h => h.AccrualProcessId == p.Id && !h.IsDeleted)
            .OrderBy(h => h.CausedDate).ThenBy(h => h.BranchId).ThenBy(h => h.FactType).ToListAsync(ct);
        hechos = hechos.Where(h => h.BranchId is not { } o || alcance.IncluyeOficina(o)).ToList();
        var oficinasIds = hechos.Select(h => h.BranchId).OfType<int>().Append(p.BranchId ?? 0).Distinct().ToList();
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinasIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new { b.PublicId, b.Name }, ct);

        var porConceptoDeLinea = await db.AccrualProcessLines.AsNoTracking()
            .Where(l => l.AccrualProcessId == p.Id && !l.IsDeleted && l.FactType != null)
            .GroupBy(l => new { l.FactType, l.Status })
            .Select(g => new { g.Key.FactType, g.Key.Status, Creditos = g.Select(l => l.LoanId).Distinct().Count() })
            .ToListAsync(ct);
        var totalesPorConcepto = hechos.GroupBy(h => h.FactType)
            .Select(g => new AccrualProcessTotalDto(g.Key, ConceptosDeCausacion.DelTipo(g.Key)?.Nombre ?? g.Key, g.Count(),
                porConceptoDeLinea.Where(x => x.FactType == g.Key && x.Status == AccrualProcessItemStatus.Included).Sum(x => x.Creditos),
                g.Where(h => h.Status == AccrualProcessItemStatus.Included).Sum(h => h.Amount),
                g.Where(h => h.Status == AccrualProcessItemStatus.Excluded).Sum(h => h.Amount)))
            .OrderBy(t => ConceptosDeCausacion.Todos.ToList().FindIndex(c => c.TipoDeHecho == t.Key))
            .ToList();
        var totalesPorSucursal = hechos.GroupBy(h => h.BranchId)
            .Select(g => new AccrualProcessTotalDto(g.Key is { } o && oficinas.TryGetValue(o, out var of) ? of.PublicId.ToString() : "-",
                g.Key is { } o2 && oficinas.TryGetValue(o2, out var of2) ? of2.Name : "Sin sucursal", g.Count(),
                g.Where(h => h.Status == AccrualProcessItemStatus.Included).Sum(h => h.LoanCount),
                g.Where(h => h.Status == AccrualProcessItemStatus.Included).Sum(h => h.Amount),
                g.Where(h => h.Status == AccrualProcessItemStatus.Excluded).Sum(h => h.Amount)))
            .OrderBy(t => t.Label, StringComparer.CurrentCulture)
            .ToList();

        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, new int?[] { p.CreatedByUserId, p.CalculatedByUserId, p.RequestedByUserId }, ct);
        var relacionados = new[] { p.ReprocessOfId, p.ReprocessedById }.OfType<int>().ToList();
        var otros = await db.AccrualProcesses.AsNoTracking().Where(x => relacionados.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new RefDeProcesoDto(x.PublicId, x.Number), ct);

        return new AccrualProcessDto(
            p.PublicId, p.Number, p.Kind, p.Status, p.Periodicity, p.SubPeriod, p.DateFrom, p.DateTo, ConceptosDeCausacion.Separar(p.Concepts),
            await FiltrosAsync(p, oficinas.ToDictionary(o => o.Key, o => (o.Value.PublicId, o.Value.Name)), ct),
            p.CalculationCount, p.CalculatedAt, ReferenciasDeGobierno.Usuario(usuarios, p.CalculatedByUserId)?.Name, p.ContentSha256,
            p.LoanCount, p.IncludedFactCount, p.ExcludedFactCount, p.IncludedAmount, p.ExcludedAmount, p.HasBlockingIssues, PuedePedirFirma(p),
            Problemas(p), totalesPorConcepto, totalesPorSucursal,
            hechos.Select(h => new AccrualProcessFactDto(h.MessagePublicId, h.FactType, ConceptosDeCausacion.DelTipo(h.FactType)?.Concepto, h.CausedDate,
                h.AccountingDate, h.BranchId is { } o && oficinas.TryGetValue(o, out var of) ? of.Name : null, h.LoanCount, h.Amount, h.Status,
                h.ExclusionReason, h.IsBlocking, h.Detail)).ToList(),
            p.Reason,
            await AprobacionAsync(p, usuarios, ct),
            await LoteAsync(p, ct),
            p.ReprocessOfId is { } r && otros.TryGetValue(r, out var de) ? de : null,
            p.ReprocessedById is { } rr && otros.TryGetValue(rr, out var por) ? por : null,
            ReferenciasDeGobierno.Usuario(usuarios, p.CreatedByUserId)?.Name, p.CreatedAt, p.ConfirmedAt, p.VoidReason);
    }

    private async Task<AccrualProcessFiltersDto> FiltrosAsync(AccrualProcess p, IReadOnlyDictionary<int, (Guid PublicId, string Name)> oficinas, CancellationToken ct)
    {
        var linea = p.CreditLineId is { } l
            ? await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == l).Select(x => new RefDeFiltroDto(x.PublicId, x.Code, x.Name))
                .FirstOrDefaultAsync(ct)
            : null;
        var oficina = p.BranchId is { } o && oficinas.TryGetValue(o, out var of) ? new RefDeFiltroDto(of.PublicId, null, of.Name) : null;
        RefDeFiltroDto? pagaduria = null;
        if (p.PaymasterPersonId is { } pp && (await ReferenciasDeGobierno.PersonasAsync(db, [pp], ct)).TryGetValue(pp, out var persona))
            pagaduria = new RefDeFiltroDto(persona.PersonPublicId, null, persona.FullName);
        return new AccrualProcessFiltersDto(linea, oficina, pagaduria);
    }

    private async Task<AccrualProcessApprovalDto?> AprobacionAsync(AccrualProcess p, IReadOnlyDictionary<int, UsuarioRefDto> usuarios, CancellationToken ct)
    {
        if (p.ApprovalRequestPublicId is not { } id) return null;
        var solicitud = await db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == id, ct);
        if (solicitud is null) return null;
        return new AccrualProcessApprovalDto(solicitud.PublicId, solicitud.Status.ToString(), solicitud.ContentSha256,
            EvaluadorDePolitica.ReglaFija(ApprovalSubjects.LendingAccrualProcess)!.PermissionCode,
            ReferenciasDeGobierno.Usuario(usuarios, p.RequestedByUserId)?.Name, p.RequestedAt, p.ApprovedByName, p.ApprovalMethod);
    }

    /// <summary>El lote que ordenó y lo que respondió Contabilidad en cada entrega (Cartera nunca lee <c>ACC_</c>).</summary>
    private async Task<AccrualProcessBatchDto?> LoteAsync(AccrualProcess p, CancellationToken ct)
    {
        if (p.BatchPublicId is not { } loteId) return null;
        var lote = await db.IntegrationBatches.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == loteId, ct);
        if (lote is null) return null;
        var entregas = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.BatchId == lote.Id && d.Destination == IntegrationDestinations.Accounting)
            .Select(d => new
            {
                d.Status, d.ResultVoucherTypeCode, d.ResultVoucherNumber, d.LastErrorCode, d.LastErrorMessage, Mensaje = d.Message!.PublicId, Tipo = d.Message.Type,
            })
            .ToListAsync(ct);
        var comprobantes = entregas.Where(e => e.Status == DeliveryStatus.Processed && !string.IsNullOrWhiteSpace(e.ResultVoucherTypeCode))
            .GroupBy(e => (e.ResultVoucherTypeCode!, e.ResultVoucherNumber))
            .Select(g => new AccrualProcessVoucherDto(g.Key.Item1, g.Key.ResultVoucherNumber, g.Count()))
            .OrderBy(v => v.VoucherTypeCode, StringComparer.Ordinal).ThenBy(v => v.Number, StringComparer.Ordinal)
            .ToList();
        var rechazos = entregas.Where(e => e.Status is DeliveryStatus.Rejected or DeliveryStatus.ValidationFailed)
            .Select(e => new AccrualProcessRejectionDto(e.Mensaje, e.Tipo, e.LastErrorCode, e.LastErrorMessage)).ToList();
        return new AccrualProcessBatchDto(lote.PublicId, lote.Number, lote.Status, entregas.Count, entregas.Count(e => e.Status == DeliveryStatus.Processed),
            rechazos.Count, entregas.Count(e => e.Status is DeliveryStatus.InBatch or DeliveryStatus.Pending), comprobantes, rechazos);
    }
}
