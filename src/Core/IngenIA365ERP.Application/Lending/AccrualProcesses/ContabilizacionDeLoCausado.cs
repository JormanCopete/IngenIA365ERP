using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

/// <summary>
/// La clase <c>Posting</c> del proceso de causación (2026-11-16, pedido del dueño; feature 024, fase 3): contabiliza lo causado de los conceptos en modo
/// manual. El motor ya lo causó día a día y su hecho espera (<see cref="CausacionPorContabilizar"/>, alcance <c>CausacionManual:{día}</c>); el borrador
/// dice, hecho por hecho, qué entra y qué queda fuera con su motivo, y la puesta en firme ordena un lote manual con <b>exactamente</b> los hechos que
/// entraron (los comprobantes salen por día y, con <c>Contabilidad.CausacionSeparadaPorConcepto</c>, por concepto, porque la marca va sellada en cada
/// entrega). Nada de esto recalcula intereses: el valor de cada hecho es el del libro que lo nombra.
/// <list type="bullet">
/// <item><b>El hecho no se parte</b>: lo causado de un día, una sucursal y un concepto es un solo mensaje con todos sus créditos. La oficina filtra exacto
/// (el hecho es de una sucursal); la línea, la pagaduría, el alcance y los abonos posteriores filtran por crédito, así que un hecho con un crédito que no
/// entra queda fuera entero (<c>OutOfFilter</c>, <c>OutOfScope</c>, <c>LaterPayment</c>) y sus demás créditos llevan <c>SharedFact</c>.</item>
/// <item><b>Fuera sin bloquear</b>: mes contable cerrado o inexistente, contabilidad sin iniciar, mes ya cerrado por Cartera; el resto se contabiliza y
/// lo excluido sigue esperando (un reproceso lo toma cuando se corrija).</item>
/// <item><b>Fuera y bloquea</b>: la falta de regla con <c>Contabilidad.FaltaDeRegla = Bloquear</c> (o Contabilidad sin respuesta con
/// <c>PoliticaSinRespuesta = Bloquear</c>): se pregunta una vez por cada combinación de tipo, sucursal y dimensiones de las partes, con el hecho más
/// pequeño de la combinación.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class ContabilizacionDeLoCausado(
    IApplicationDbContext db,
    CausacionPorContabilizar porContabilizar,
    IContabilidadParaCartera contabilidad,
    ValidacionPreviaContableDeCartera validacion,
    IActorActual actorActual,
    IDateTimeService reloj) : IClaseDeProcesoDeCausacion
{
    /// <summary>Cuántos Guid van en un <c>IN</c> (SQL Server admite 2.100 parámetros).</summary>
    private const int Trozo = 500;

    public AccrualProcessKind Clase => AccrualProcessKind.Posting;

    private sealed record Pendiente(Guid Mensaje, string Tipo, DateOnly Dia, DateOnly FechaContable, int? OficinaId);

    private sealed record Parte(int Creditos, decimal Total, string Totales);

    private sealed record Agregado(int CreditoId, LoanConcept Concepto, bool Correccion, decimal Monto);

    public async Task<Result<CalculoDelProceso>> CalcularAsync(ContextoDelCalculo contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var p = contexto.Parametros;
        var alcance = contexto.Alcance;
        var tipos = ConceptosDeCausacion.TiposDe(p.Conceptos);

        // 1. Lo que espera en el rango, de los conceptos pedidos y, si viene, de la oficina (el hecho es de una sucursal).
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Select(b => new { b.Id, b.PublicId }).ToListAsync(ct);
        var oficinaDe = oficinas.ToDictionary(b => b.PublicId, b => b.Id);
        var pendientes = (await porContabilizar.Entregas(p.Desde, p.Hasta, tipos).AsNoTracking()
                .Select(d => new { d.BatchScopeKey, d.Message!.PublicId, d.Message.Type, d.Message.BranchPublicId, d.Message.OperationDate })
                .ToListAsync(ct))
            .Select(x => new Pendiente(x.PublicId, x.Type, DiaCausado(x.BatchScopeKey!), x.OperationDate,
                oficinaDe.TryGetValue(x.BranchPublicId, out var o) ? o : null))
            .Where(x => p.OficinaId is null || x.OficinaId == p.OficinaId)
            .DistinctBy(x => x.Mensaje)
            .ToList();
        var avisos = new List<AvisoDelCalculo>();
        if (pendientes.Count == 0)
        {
            avisos.Add(new AvisoDelCalculo("Lending.AccrualProcess.NothingPending",
                "No hay causación esperando contabilizarse en ese rango para esos conceptos y filtros.", false));
            return Result.Success(new CalculoDelProceso([], [], avisos));
        }
        var ids = pendientes.Select(x => x.Mensaje).ToList();

        // 2. Las partes de cada hecho: créditos, total de control y totales por dimensión (para preguntar a Contabilidad por combinación).
        var partes = new Dictionary<Guid, Parte>();
        foreach (var trozo in ids.Chunk(Trozo))
        {
            var deTrozo = await db.AccountingFactParts.AsNoTracking()
                .Where(x => x.MessagePublicId != null && trozo.Contains(x.MessagePublicId.Value))
                .Select(x => new { Mensaje = x.MessagePublicId!.Value, x.LoanCount, x.TotalAmount, x.TotalsByDimensionJson })
                .ToListAsync(ct);
            foreach (var g in deTrozo.GroupBy(x => x.Mensaje))
                partes[g.Key] = new Parte(g.Sum(x => x.LoanCount), g.Sum(x => x.TotalAmount), string.Join("|", g.Select(x => x.TotalsByDimensionJson)));
        }

        // 3. Los hechos con créditos que no entran: fuera del alcance, del filtro de línea o pagaduría, o con abonos posteriores (reproceso).
        var todasLasOficinas = alcance.TodasLasOficinas;
        var todasLasLineas = alcance.TodasLasLineas;
        var oficinasDelAlcance = alcance.Oficinas.ToArray();
        var lineasDelAlcance = alcance.Lineas.ToArray();
        var fueraDelAlcance = todasLasOficinas && todasLasLineas
            ? new HashSet<Guid>()
            : await MensajesConAsync(ids, t => (!todasLasOficinas && !oficinasDelAlcance.Contains(t.Loan!.BranchId))
                                              || (!todasLasLineas && !lineasDelAlcance.Contains(t.Loan!.CreditLineId)), ct);
        var fueraDelFiltro = new HashSet<Guid>();
        if (p.LineaId is { } linea)
            fueraDelFiltro.UnionWith(await MensajesConAsync(ids, t => t.Loan!.CreditLineId != linea, ct));
        if (p.PagaduriaId is { } pagaduria)
            fueraDelFiltro.UnionWith(await MensajesConAsync(ids, t => !db.LoanPayrollLinks.Any(v => v.LoanId == t.LoanId && !v.IsDeleted
                && v.ValidTo == null && v.PaymasterPersonId == pagaduria), ct));
        var abonados = contexto.CreditosConAbonos.ToArray();
        var conAbonos = abonados.Length == 0 ? new HashSet<Guid>() : await MensajesConAsync(ids, t => abonados.Contains(t.LoanId), ct);

        // 4. El motivo de cada hecho, en orden: Contabilidad (mes contable), Cartera (mes cerrado), alcance, filtro, abonos; lo que sigue, por regla.
        // El estado contable se pregunta por fecha (el puerto responde por el mes de la fecha): una vez por fecha contable distinta del rango.
        var estados = new Dictionary<DateOnly, Result<EstadoDelPeriodoDto>>();
        var cierres = new Dictionary<(int, int), bool>();
        var motivos = new Dictionary<Guid, (AccrualExclusionReason Motivo, bool Bloquea, string Detalle)>();
        foreach (var x in pendientes)
        {
            if (!estados.TryGetValue(x.FechaContable, out var estado))
                estados[x.FechaContable] = estado = await contabilidad.EstadoDelPeriodoAsync(x.FechaContable, ct);
            if (estado.IsFailure)
            {
                motivos[x.Mensaje] = (AccrualExclusionReason.AccountingNoResponse, true, $"Contabilidad no respondió por el mes {Mes(x.FechaContable)}: {estado.Error.Message}");
                continue;
            }
            if (!estado.Value.AccountingInitialized)
            {
                motivos[x.Mensaje] = (AccrualExclusionReason.AccountingNotStarted, false, "La contabilidad no está iniciada.");
                continue;
            }
            if (estado.Value.State != EstadoDePeriodoContable.Open)
            {
                motivos[x.Mensaje] = estado.Value.State == EstadoDePeriodoContable.Closed
                    ? (AccrualExclusionReason.AccountingPeriodClosed, false, $"El mes contable {Mes(x.FechaContable)} está cerrado.")
                    : (AccrualExclusionReason.AccountingPeriodMissing, false, $"El mes contable {Mes(x.FechaContable)} no existe en Contabilidad.");
                continue;
            }
            var mesCausado = (x.Dia.Year, x.Dia.Month);
            if (!cierres.TryGetValue(mesCausado, out var cerrado))
                cierres[mesCausado] = cerrado = await RequisitosDelCierreMensual.MesConCierreVigenteAsync(db, x.Dia.Year, x.Dia.Month, ct);
            if (cerrado)
            {
                motivos[x.Mensaje] = (AccrualExclusionReason.LendingMonthClosed, false,
                    $"Cartera ya cerró {Mes(x.Dia)}: reabra su cierre o contabilícelo por un lote manual de Cartera.");
                continue;
            }
            if (fueraDelAlcance.Contains(x.Mensaje))
                motivos[x.Mensaje] = (AccrualExclusionReason.OutOfScope, false, "El hecho lleva créditos de oficinas o líneas fuera de su alcance.");
            else if (fueraDelFiltro.Contains(x.Mensaje))
                motivos[x.Mensaje] = (AccrualExclusionReason.OutOfFilter, false,
                    "El hecho lleva también créditos de otras líneas o pagadurías: no se parte; contabilícelo sin ese filtro.");
            else if (conAbonos.Contains(x.Mensaje))
                motivos[x.Mensaje] = (AccrualExclusionReason.LaterPayment, false,
                    "El hecho lleva créditos con abonos después de la puesta en firme del proceso reprocesado: queda intacto.");
        }

        // 5. La validación previa contable de lo que queda, una pregunta por combinación (el hecho más pequeño de cada una).
        var candidatos = pendientes.Where(x => !motivos.ContainsKey(x.Mensaje)).ToList();
        foreach (var combinacion in candidatos.GroupBy(x => Combinacion(x, partes.GetValueOrDefault(x.Mensaje))))
        {
            var muestra = combinacion.OrderBy(x => partes.GetValueOrDefault(x.Mensaje)?.Creditos ?? 0).ThenBy(x => x.Mensaje).First();
            var mensaje = await db.IntegrationMessages.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == muestra.Mensaje, ct);
            if (mensaje is null) continue;
            var contenido = MensajesEntrantes.Deserializar(mensaje);
            if (contenido is null) continue;
            var decision = await validacion.DecidirAsync([new MensajeContableDto(MensajesEntrantes.Sobre(mensaje, contenido), contenido)],
                OrigenDeValidacionPrevia.Pantalla, $"Proceso de causación {p.Desde:yyyy-MM-dd} a {p.Hasta:yyyy-MM-dd}", alertar: false, ct);
            if (decision.Bloqueo is { } bloqueo)
            {
                var sinRespuesta = bloqueo.Code == ErroresDeValidacionPreviaDeCartera.CodigoSinRespuesta;
                var detalle = sinRespuesta
                    ? "Contabilidad no respondió la validación previa y la política la exige."
                    : "Falta la regla contable: " + string.Join(" ", decision.Resultado.Errors.Take(3).Select(h => h.Message).Distinct());
                foreach (var x in combinacion)
                    motivos[x.Mensaje] = (sinRespuesta ? AccrualExclusionReason.AccountingNoResponse : AccrualExclusionReason.PostingRuleMissing, true,
                        Recortar(detalle));
            }
            else if (decision.Avisos.Count > 0)
            {
                foreach (var a in decision.Avisos.Distinct())
                    if (avisos.All(v => v.Message != a)) avisos.Add(new AvisoDelCalculo("Lending.AccrualProcess.AccountingWarning", a, false));
            }
        }

        var hechos = pendientes
            .Select(x =>
            {
                var parte = partes.GetValueOrDefault(x.Mensaje);
                var excluido = motivos.TryGetValue(x.Mensaje, out var m);
                return new HechoDelCalculo(x.Mensaje, x.Tipo, x.Dia, x.FechaContable, x.OficinaId, parte?.Creditos ?? 0, parte?.Total ?? 0m,
                    excluido ? AccrualProcessItemStatus.Excluded : AccrualProcessItemStatus.Included, excluido ? m.Motivo : null, excluido && m.Bloquea,
                    excluido ? m.Detalle : null);
            })
            .OrderBy(h => h.DiaCausado).ThenBy(h => h.OficinaId).ThenBy(h => h.Tipo, StringComparer.Ordinal)
            .ToList();

        // 6. Las líneas: lo de cada crédito y concepto, sumado sobre los hechos de cada estado y motivo (sólo los créditos que se ven).
        var lineas = await LineasAsync(contexto, hechos, ct);
        return Result.Success(new CalculoDelProceso(hechos, lineas, avisos));
    }

    public async Task<Result<LoteDelProceso>> PonerEnFirmeAsync(AccrualProcess proceso, IReadOnlyList<AccrualProcessFact> incluidos, string motivo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proceso);
        ArgumentNullException.ThrowIfNull(incluidos);
        var mensajes = incluidos.Where(h => h.MessagePublicId is not null).Select(h => h.MessagePublicId!.Value).Distinct().ToList();
        if (mensajes.Count == 0) return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.NadaQueContabilizar());
        var entregas = new List<IntegrationMessageDelivery>();
        foreach (var trozo in mensajes.Chunk(Trozo))
            entregas.AddRange(await db.IntegrationMessageDeliveries.Include(d => d.Message)
                .Where(d => d.Destination == IntegrationDestinations.Accounting && trozo.Contains(d.Message!.PublicId))
                .ToListAsync(ct));
        // Exactamente lo del borrador: cada hecho sigue esperando, sin lote, con el alcance de la causación manual.
        if (entregas.Count != mensajes.Count || entregas.Any(d => d.Status != DeliveryStatus.InBatch || d.BatchId is not null
                || d.BatchScopeKey is null || !d.BatchScopeKey.StartsWith(ModoDePasoDeCartera.PrefijoDeCausacionManual, StringComparison.Ordinal)))
            return Result.Failure<LoteDelProceso>(ErroresDeProcesosDeCausacion.Desactualizado());

        var actor = await actorActual.ObtenerAsync(ct);
        var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting, BatchTrigger.Manual,
            actor, $"Proceso de causación {proceso.Number}: {motivo}", reloj.UtcNow);
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

    // ------------------------------------------------------------------------------------------------------- piezas --

    /// <summary>El día causado del alcance <c>CausacionManual:{aaaa-mm-dd}</c>.</summary>
    private static DateOnly DiaCausado(string alcance) =>
        DateOnly.ParseExact(alcance[ModoDePasoDeCartera.PrefijoDeCausacionManual.Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Mes(DateOnly fecha) => fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string Recortar(string texto) => texto.Length <= 500 ? texto : texto[..500];

    /// <summary>Los hechos (de <paramref name="mensajes"/>) con alguna fila del libro que cumple <paramref name="predicado"/>.</summary>
    private async Task<HashSet<Guid>> MensajesConAsync(IReadOnlyList<Guid> mensajes, Expression<Func<LoanTransaction, bool>> predicado, CancellationToken ct)
    {
        var con = new HashSet<Guid>();
        foreach (var trozo in mensajes.Chunk(Trozo))
            con.UnionWith(await db.LoanTransactions.AsNoTracking()
                .Where(t => t.MessagePublicId != null && trozo.Contains(t.MessagePublicId.Value))
                .Where(predicado)
                .Select(t => t.MessagePublicId!.Value)
                .Distinct()
                .ToListAsync(ct));
        return con;
    }

    /// <summary>
    /// La combinación con que se pregunta a Contabilidad: tipo, sucursal y, de los totales por dimensión de la parte, las dimensiones y los conceptos con
    /// su signo (los importes no cuentan, como en <c>ValidacionPreviaContableDeCartera.ClaveDeCombinacion</c>).
    /// </summary>
    private static string Combinacion(Pendiente x, Parte? parte)
    {
        var b = new StringBuilder().Append(x.Tipo).Append('|').Append(x.OficinaId?.ToString(CultureInfo.InvariantCulture) ?? "-");
        if (parte is null) return b.Append('|').Append(x.Mensaje.ToString("N")).ToString();
        var firmas = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var json in parte.Totales.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;
                foreach (var total in doc.RootElement.EnumerateArray())
                {
                    var dimensiones = total.TryGetProperty("dimensions", out var d) ? d.GetRawText() : string.Empty;
                    var conceptos = total.TryGetProperty("amounts", out var a) && a.ValueKind == JsonValueKind.Array
                        ? string.Join(",", a.EnumerateArray().Select(c =>
                            (c.TryGetProperty("concept", out var n) ? n.GetString() : null) + ":"
                            + (c.TryGetProperty("amount", out var v) && v.TryGetDecimal(out var monto) ? Math.Sign(monto) : 0)
                            + (c.TryGetProperty("onBehalfOfAssociate", out var s) && s.ValueKind == JsonValueKind.True ? "+" : string.Empty))
                            .Order(StringComparer.Ordinal))
                        : string.Empty;
                    firmas.Add(dimensiones + "#" + conceptos);
                }
            }
            catch (JsonException)
            {
                // Una parte ilegible pregunta sola: su combinación es el propio hecho.
                return b.Append('|').Append(x.Mensaje.ToString("N")).ToString();
            }
        }
        return b.Append('|').AppendJoin(";", firmas).ToString();
    }

    /// <summary>Las líneas del borrador, por estado y motivo, sólo de los créditos que se ven (dentro del alcance y del filtro).</summary>
    private async Task<IReadOnlyList<LineaDelCalculo>> LineasAsync(ContextoDelCalculo contexto, IReadOnlyList<HechoDelCalculo> hechos, CancellationToken ct)
    {
        var p = contexto.Parametros;
        var grupos = hechos.Where(h => h.MensajePublicId is not null)
            .GroupBy(h => (h.Estado, h.Motivo))
            .Select(g => (g.Key.Estado, g.Key.Motivo, Mensajes: g.Select(h => h.MensajePublicId!.Value).ToList()))
            .ToList();
        var agregados = new List<(AccrualProcessItemStatus Estado, AccrualExclusionReason? Motivo, Agregado Fila)>();
        foreach (var (estado, motivo, mensajes) in grupos)
            foreach (var fila in await AgregarAsync(mensajes, ct))
                agregados.Add((estado, motivo, fila));

        // Lo que se sabe de cada crédito: oficina, línea y, si el filtro la pide, su pagaduría vigente.
        var creditos = agregados.Select(a => a.Fila.CreditoId).Concat(contexto.CreditosConAbonos).Distinct().ToList();
        var datos = new Dictionary<int, (int Oficina, int Linea)>();
        foreach (var trozo in creditos.Chunk(Trozo))
            foreach (var c in await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => trozo.Contains(l.Id))
                         .Select(l => new { l.Id, l.BranchId, l.CreditLineId }).ToListAsync(ct))
                datos[c.Id] = (c.BranchId, c.CreditLineId);
        var conPagaduria = new HashSet<int>();
        if (p.PagaduriaId is { } pagaduria)
            foreach (var trozo in creditos.Chunk(Trozo))
                conPagaduria.UnionWith(await db.LoanPayrollLinks.AsNoTracking()
                    .Where(v => trozo.Contains(v.LoanId) && !v.IsDeleted && v.ValidTo == null && v.PaymasterPersonId == pagaduria)
                    .Select(v => v.LoanId).ToListAsync(ct));

        bool SeVe(int credito) => datos.TryGetValue(credito, out var d)
            && (p.OficinaId is null || d.Oficina == p.OficinaId) && (p.LineaId is null || d.Linea == p.LineaId)
            && (p.PagaduriaId is null || conPagaduria.Contains(credito))
            && contexto.Alcance.IncluyeOficina(d.Oficina) && contexto.Alcance.IncluyeLinea(d.Linea);

        var lineas = new List<LineaDelCalculo>();
        foreach (var (estado, motivo, fila) in agregados.Where(a => SeVe(a.Fila.CreditoId)))
        {
            var tipo = fila.Correccion ? CorreccionMonetariaUvrCausadaV1.Type : Accrual.PartesDeHechosDeCartera.TipoDe(fila.Concepto);
            var suyo = motivo switch
            {
                AccrualExclusionReason.LaterPayment => contexto.CreditosConAbonos.Contains(fila.CreditoId) ? AccrualExclusionReason.LaterPayment
                    : AccrualExclusionReason.SharedFact,
                AccrualExclusionReason.OutOfFilter or AccrualExclusionReason.OutOfScope => AccrualExclusionReason.SharedFact,
                _ => motivo,
            };
            var (oficina, linea) = datos[fila.CreditoId];
            lineas.Add(new LineaDelCalculo(fila.CreditoId, tipo, fila.Concepto, fila.Monto, Anterior(contexto, fila.CreditoId, tipo, fila.Concepto), estado, suyo,
                oficina, linea));
        }

        // Reproceso: los créditos con abonos posteriores aparecen aunque no tengan nada pendiente (quedan intactos y se informan).
        foreach (var credito in contexto.CreditosConAbonos.Where(c => SeVe(c) && lineas.All(l => l.CreditoId != c)).Order())
        {
            var (oficina, linea) = datos[credito];
            lineas.Add(new LineaDelCalculo(credito, null, null, 0m, null, AccrualProcessItemStatus.Excluded, AccrualExclusionReason.LaterPayment, oficina, linea));
        }

        // Varias filas del mismo crédito, concepto, estado y motivo (de hechos distintos) van en una línea.
        return lineas
            .GroupBy(l => (l.CreditoId, l.Tipo, l.Concepto, l.Estado, l.Motivo))
            .Select(g => g.First() with { Monto = g.Sum(l => l.Monto) })
            .OrderBy(l => l.CreditoId).ThenBy(l => l.Tipo, StringComparer.Ordinal).ThenBy(l => l.Concepto).ThenBy(l => l.Estado)
            .ToList();
    }

    private static decimal? Anterior(ContextoDelCalculo contexto, int credito, string? tipo, LoanConcept? concepto) =>
        contexto.Reprocesado is null ? null : contexto.Anteriores.TryGetValue((credito, tipo, concepto), out var monto) ? monto : 0m;

    /// <summary>Las filas del libro de esos hechos, sumadas por crédito, concepto y si son de corrección monetaria (que va en su propio hecho).</summary>
    private async Task<IReadOnlyList<Agregado>> AgregarAsync(IReadOnlyList<Guid> mensajes, CancellationToken ct)
    {
        var suma = new Dictionary<(int, LoanConcept, bool), decimal>();
        foreach (var trozo in mensajes.Chunk(Trozo))
        {
            var filas = await db.LoanTransactions.AsNoTracking()
                .Where(t => t.MessagePublicId != null && trozo.Contains(t.MessagePublicId.Value))
                .GroupBy(t => new { t.LoanId, t.Concept, Correccion = t.Kind == LoanTransactionKind.MonetaryCorrection })
                .Select(g => new { g.Key.LoanId, g.Key.Concept, g.Key.Correccion, Monto = g.Sum(t => t.Amount) })
                .ToListAsync(ct);
            foreach (var f in filas)
                suma[(f.LoanId, f.Concept, f.Correccion)] = suma.GetValueOrDefault((f.LoanId, f.Concept, f.Correccion)) + f.Monto;
        }
        return suma.Select(s => new Agregado(s.Key.Item1, s.Key.Item2, s.Key.Item3, s.Value)).ToList();
    }
}
