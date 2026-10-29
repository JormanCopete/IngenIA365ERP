using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>La conciliación de Cartera con Contabilidad a una fecha contable (api.md §16.3, <c>ReconciliationDto</c>). (nuevo)</summary>
public sealed record ConciliacionContableDto(
    DateOnly Date,
    IReadOnlyList<ConjuntoConciliadoDto> Sets,
    IReadOnlyList<PuenteConciliadoDto> Bridges,
    IReadOnlyList<CombinacionSinReglaDto> UnresolvedCombinations,
    decimal UnexplainedTotal);

/// <summary>
/// Un conjunto de cuentas conciliado: su familia, las combinaciones de Cartera que reúne, las cuentas (dato para mostrar), el saldo de Cartera y el
/// contable (los dos en el sentido de Cartera), la diferencia, lo que la explican los hechos sin procesar por estado y hecho por hecho, y lo que
/// queda sin explicar —en un puente, «del otro lado»—. (nuevo)
/// </summary>
public sealed record ConjuntoConciliadoDto(
    string Family,
    IReadOnlyList<CombinacionConciliadaDto> Combinations,
    IReadOnlyList<CuentaConciliadaDto> Accounts,
    decimal LendingBalance,
    decimal AccountingBalance,
    decimal Difference,
    ExplicacionPorEstadoDto ExplainedBy,
    IReadOnlyList<HechoQueExplicaDto> Facts,
    decimal Unexplained,
    bool UnexplainedIsOtherSide);

public sealed record CombinacionConciliadaDto(
    string Role, string? CreditLine, string? Modality, string? Subsegment, string? Category, string? Concept, string? CollectorClass, string? Reason,
    Guid Branch, decimal LendingBalance, string? Nature = null, string? DepositClass = null, string? ContributionClass = null, string? AccountFlag = null);

public sealed record CuentaConciliadaDto(string Code, string Name);

public sealed record ExplicacionPorEstadoDto(decimal Pending, decimal InBatch, decimal Rejected, decimal NotPosted);

/// <summary>Un hecho de Cartera sin procesar que explica parte de una diferencia. (nuevo)</summary>
public sealed record HechoQueExplicaDto(Guid MessagePublicId, string Type, string? Number, DateOnly AccountingDate, string Status, decimal Amount);

/// <summary>El saldo de un puente de recaudo por clase. (nuevo)</summary>
public sealed record PuenteConciliadoDto(string CollectorClass, decimal Balance);

/// <summary>Una combinación de Cartera sin regla en la matriz al corte: Contabilidad no tiene dónde mirarla. (nuevo)</summary>
public sealed record CombinacionSinReglaDto(string Role, string? CreditLine, string? Modality, string? Category, string? Concept, Guid Branch, decimal LendingBalance);

/// <summary>
/// La conciliación contable de Cartera (feature 013, US7, T0658; contracts/contabilidad.md §14.2; T58, FR-130, SC-006): compara por <b>conjunto
/// de cuentas</b> (Cartera no conoce cuentas: se las dice Contabilidad por <see cref="IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync"/>)
/// el saldo de Cartera por fecha contable (<see cref="SaldosContablesDeCartera"/>) con el contable, y explica cada diferencia con los hechos de
/// Cartera de fecha contable ≤ corte que están <c>Pending</c>, <c>InBatch</c>, <c>Rejected</c> o <c>NotApplicable</c> (no pasa): su efecto se
/// recalcula con las filas del libro que llevaron (las del mismo mensaje o la misma operación), hecho por hecho. Con cero hechos sin procesar la
/// diferencia es cero. En un puente lo que los hechos no explican es «del otro lado» (el recaudador o Tesorería no han contabilizado). Un
/// descuadre sin explicar fuera de los puentes levanta <c>Cartera.DescuadreContable</c>, una por corte y conjunto. Sin contabilidad iniciada,
/// <c>Lending.Accounting.NotStarted</c>. (nuevo)
/// </summary>
public sealed class ConciliacionContableDeCartera(
    IApplicationDbContext db,
    SaldosContablesDeCartera saldos,
    IContabilidadParaCartera contabilidad,
    IAlertas? alertas = null,
    SaldosContablesDeCaptaciones? captaciones = null)
{
    public const string CodigoSinContabilidad = "Lending.Accounting.NotStarted";

    /// <summary>Los destinatarios de <c>Cartera.DescuadreContable</c>: el contador y el jefe de cartera (data-model §10.3).</summary>
    public static IReadOnlyList<string> DestinatariosDelDescuadre { get; } = ["Accounting.LendingRules.Manage", "Lending.Messages.Reprocess"];

    public static Error SinContabilidad() => new(CodigoSinContabilidad,
        "La contabilidad de la cooperativa no está iniciada: no hay con qué conciliar la cartera. Iníciela en Contabilidad › Configuración.");

    private static readonly DeliveryStatus[] SinProcesar = [DeliveryStatus.Pending, DeliveryStatus.InBatch, DeliveryStatus.Rejected, DeliveryStatus.NotApplicable];

    /// <summary>Concilia a <paramref name="corte"/> dentro de <paramref name="alcance"/> (nulo: todo).</summary>
    public async Task<Result<ConciliacionContableDto>> ConciliarAsync(DateOnly corte, AlcanceDeCartera? alcance, CancellationToken ct)
    {
        var estado = await contabilidad.EstadoDelPeriodoAsync(corte, ct);
        if (estado.IsFailure) return Result.Failure<ConciliacionContableDto>(estado.Error);
        if (!estado.Value.AccountingInitialized) return Result.Failure<ConciliacionContableDto>(SinContabilidad());

        var movimientos = (await saldos.MovimientosAsync(corte, alcance, ct)).ToList();
        // Feature 014, US8 (T146; contabilidad.md §8.1): los saldos de captaciones y aportes por línea, concepto (rol y clase) y oficina.
        if (captaciones is not null) movimientos.AddRange(await captaciones.MovimientosAsync(corte, alcance, ct));
        var deCartera = SaldosContablesDeCartera.Agrupar(movimientos).ToList();
        deCartera.AddRange(await saldos.OrdenesAbiertasAsync(corte, alcance, ct));
        deCartera.AddRange(await saldos.PuentesEnCeroAsync(corte, ct));
        if (deCartera.Count == 0) return Result.Success(new ConciliacionContableDto(corte, [], [], [], 0m));

        var mapeados = await contabilidad.SaldosDeCuentasMapeadasAsync(corte, deCartera.Select(s => s.Clave.ComoCombinacion()).ToList(), ct);
        if (mapeados.IsFailure)
            return mapeados.Error.Code == "Accounting.NotInitialized"
                ? Result.Failure<ConciliacionContableDto>(SinContabilidad())
                : Result.Failure<ConciliacionContableDto>(mapeados.Error);

        var sucursalesVisibles = alcance is null || alcance.TodasLasOficinas ? null
            : (await db.Branches.AsNoTracking().Where(b => alcance.Oficinas.Contains(b.Id)).Select(b => b.PublicId).ToListAsync(ct)).ToHashSet();
        var pendientes = await PendientesAsync(corte, movimientos, ct);

        var conjuntos = new List<ConjuntoConciliadoDto>();
        foreach (var set in mapeados.Value.Sets)
        {
            var combinaciones = set.Combinations.Select(i => deCartera[i]).ToList();
            var rol = combinaciones[0].Clave.Rol;
            var signo = SaldosContablesDeCartera.Signo(rol);
            var esPuente = SaldosContablesDeCartera.Puentes.Contains(rol);
            var contable = set.Accounts.Sum(a => a.BalanceByBranch.Where(b => sucursalesVisibles is null || sucursalesVisibles.Contains(b.BranchPublicId))
                .Sum(b => b.Balance)) * signo;
            var deLaCartera = combinaciones.Sum(c => c.Saldo);
            var diferencia = deLaCartera - contable;

            var claves = combinaciones.Select(c => c.Clave).ToHashSet();
            var hechos = pendientes.Select(p => (p.Hecho, Monto: Efecto(p, claves, esPuente, combinaciones)))
                .Where(x => x.Monto != 0m)
                .Select(x => x.Hecho with { Amount = x.Monto })
                .ToList();
            var explicado = hechos.Sum(h => h.Amount);
            var porEstado = new ExplicacionPorEstadoDto(
                hechos.Where(h => h.Status == nameof(DeliveryStatus.Pending)).Sum(h => h.Amount),
                hechos.Where(h => h.Status == nameof(DeliveryStatus.InBatch)).Sum(h => h.Amount),
                hechos.Where(h => h.Status == nameof(DeliveryStatus.Rejected)).Sum(h => h.Amount),
                hechos.Where(h => h.Status == nameof(DeliveryStatus.NotApplicable)).Sum(h => h.Amount));
            var sinExplicar = diferencia - explicado;

            conjuntos.Add(new ConjuntoConciliadoDto(set.RoleFamily,
                combinaciones.Select(c => new CombinacionConciliadaDto(c.Clave.Rol, c.Clave.Linea, c.Clave.Modalidad, c.Clave.Subsegmento, c.Clave.Categoria,
                    c.Clave.Concepto, c.Clave.ClaseDeRecaudador, c.Clave.Motivo, c.Clave.Sucursal, c.Saldo, c.Clave.Naturaleza, c.Clave.ClaseDeCaptacion,
                    c.Clave.ClaseDeAporte, c.Clave.Marca)).ToList(),
                set.Accounts.Select(a => new CuentaConciliadaDto(a.AccountCode, a.AccountName)).ToList(),
                deLaCartera, contable, diferencia, porEstado, hechos, sinExplicar, esPuente && sinExplicar != 0m));

            if (sinExplicar != 0m && !esPuente) await AlertarAsync(corte, set, rol, sinExplicar, ct);
        }

        var sinRegla = mapeados.Value.UnresolvedCombinations.Select(i => deCartera[i])
            .Select(c => new CombinacionSinReglaDto(c.Clave.Rol, c.Clave.Linea, c.Clave.Modalidad, c.Clave.Categoria, c.Clave.Concepto, c.Clave.Sucursal, c.Saldo))
            .ToList();
        var puentes = conjuntos.Where(s => s.Combinations.Any(c => c.Role == "RecaudoPorAplicar"))
            .SelectMany(s => s.Combinations.Where(c => c.CollectorClass is not null).Select(c => new PuenteConciliadoDto(c.CollectorClass!, s.AccountingBalance)))
            .DistinctBy(p => p.CollectorClass).ToList();
        return Result.Success(new ConciliacionContableDto(corte, conjuntos, puentes, sinRegla,
            conjuntos.Where(s => !s.UnexplainedIsOtherSide).Sum(s => s.Unexplained)));
    }

    // ------------------------------------------------------------------------------------------- hechos sin procesar --

    /// <summary>
    /// Los contingentes (§19) no mueven el libro del crédito y no entran a la conciliación: no explican nada. Hasta el 2026-10-13 el
    /// <c>CreditoAprobadoSinDesembolsar</c> (siempre <c>NotApplicable</c>, «no pasa») tomaba las filas de su operación —el desembolso— y «explicaba»
    /// el capital ya contabilizado, dejando un descuadre sin explicar falso del tamaño del crédito.
    /// </summary>
    private static readonly string[] Contingentes = [.. MovimientoContingenteDeCartera.Tipos];

    private sealed record Pendiente(HechoQueExplicaDto Hecho, Guid OrigenPublicId, string? ClaseDeRecaudador, IReadOnlyList<MovimientoDeCombinacionDeCartera> Movimientos)
    {
        /// <summary>
        /// Feature 015 (T0261; FR-144, FR-146): lo que un hecho sin procesar de Recaudo o de Tesorería le lleva a un puente de Cartera —el recibo acredita
        /// <c>RecaudoPorAplicar</c> de su clase de recaudador, el giro debita <c>DesembolsoPorGirar</c>—, como débito menos crédito. Vacío en los de Cartera.
        /// </summary>
        public IReadOnlyList<(string Rol, string? Clase, decimal Neto)> PuentesDeOtroModulo { get; init; } = [];
    }

    /// <summary>Los módulos cuyos hechos sin procesar explican la diferencia de un puente de Cartera (feature 015, T0261).</summary>
    private static readonly string[] ModulosDelPuente = [ModulosDeOrigen.Cartera, ModulosDeOrigen.Recaudo, ModulosDeOrigen.Tesoreria];

    private async Task<List<Pendiente>> PendientesAsync(DateOnly corte, IReadOnlyList<MovimientoDeCombinacionDeCartera> movimientos, CancellationToken ct)
    {
        var filas = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.Destination == IntegrationDestinations.Accounting && SinProcesar.Contains(d.Status)
                && ModulosDelPuente.Contains(d.Message!.OriginModule) && d.Message.Kind == IntegrationMessageKind.Business
                && d.Message.OperationDate <= corte && !Contingentes.Contains(d.Message.Type))
            .Select(d => new { d.Status, d.Message!.PublicId, d.Message.Type, d.Message.OriginNumber, d.Message.OperationDate, d.Message.OriginPublicId, d.Message.PayloadJson,
                d.Message.OriginModule })
            .ToListAsync(ct);
        var porMensaje = movimientos.Where(m => m.Mensaje is not null).ToLookup(m => m.Mensaje!.Value);
        var porOperacion = movimientos.Where(m => m.Mensaje is null && m.Operacion is not null).ToLookup(m => m.Operacion!.Value);
        return filas.OrderBy(f => f.OperationDate).ThenBy(f => f.OriginNumber)
            .Select(f =>
            {
                if (f.OriginModule != ModulosDeOrigen.Cartera)
                    return new Pendiente(new HechoQueExplicaDto(f.PublicId, f.Type, f.OriginNumber, f.OperationDate, f.Status.ToString(), 0m), f.OriginPublicId, null, [])
                    {
                        PuentesDeOtroModulo = PuentesDeOtroModulo(f.PayloadJson),
                    };
                var propios = porMensaje[f.PublicId].ToList();
                if (propios.Count == 0) propios = porOperacion[f.OriginPublicId].ToList();
                return new Pendiente(new HechoQueExplicaDto(f.PublicId, f.Type, f.OriginNumber, f.OperationDate, f.Status.ToString(), 0m), f.OriginPublicId,
                    ClaseDe(f.PayloadJson), propios);
            })
            .ToList();
    }

    /// <summary>
    /// El efecto de un hecho sin procesar sobre un conjunto: en los saldos, la suma de sus filas del libro en las combinaciones del conjunto; en un
    /// puente, lo que el hecho le habría llevado (el total que movió en el libro, con el sentido del puente).
    /// </summary>
    private static decimal Efecto(Pendiente p, IReadOnlySet<ClaveDeSaldoDeCartera> claves, bool esPuente, IReadOnlyList<SaldoDeCombinacionDeCartera> combinaciones)
    {
        if (!esPuente) return p.Movimientos.Where(m => claves.Contains(m.Clave)).Sum(m => m.Monto);

        var rol = combinaciones[0].Clave.Rol;
        // Feature 015 (T0261): el lado de Recaudo y de Tesorería del puente, con el signo de su naturaleza.
        if (p.PuentesDeOtroModulo.Count > 0)
            return p.PuentesDeOtroModulo.Where(x => x.Rol == rol && (rol != "RecaudoPorAplicar" || combinaciones.Any(c => c.Clave.ClaseDeRecaudador == x.Clase)))
                .Sum(x => x.Neto) * SaldosContablesDeCartera.Signo(rol);
        var total = p.Movimientos.Sum(m => m.Monto);
        return rol switch
        {
            // El pago acredita la cartera y debita el puente de su clase: lo que Contabilidad no recibió es lo aplicado.
            "RecaudoPorAplicar" when p.ClaseDeRecaudador is { } clase && combinaciones.Any(c => c.Clave.ClaseDeRecaudador == clase) => -total,
            // El desembolso abona el puente con Tesorería por lo girado (§2.1, CreditoDesembolsado).
            "DesembolsoPorGirar" when p.Hecho.Type == IngenIA365ERP.Application.Common.Integration.Contracts.Lending.CreditoDesembolsadoV1.Type => total,
            _ => 0m,
        };
    }

    /// <summary>
    /// Las líneas de un hecho de Recaudo o de Tesorería que tocan un puente de Cartera (contabilidad.md §4 de la 015): <c>PuenteDelModulo</c> de Cartera →
    /// <c>RecaudoPorAplicar</c> de su clase; <c>PuenteDeGiroDelOrigen</c> de un desembolso → <c>DesembolsoPorGirar</c>. Se leen del contenido como texto: Cartera no
    /// conoce los contratos de Contabilidad.
    /// </summary>
    private static IReadOnlyList<(string Rol, string? Clase, decimal Neto)> PuentesDeOtroModulo(string payloadJson)
    {
        var lista = new List<(string, string?, decimal)>();
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("lines", out var lineas) || lineas.ValueKind != JsonValueKind.Array) return lista;
            foreach (var l in lineas.EnumerateArray())
            {
                var rol = l.TryGetProperty("role", out var r) ? r.GetString() : null;
                var debito = l.TryGetProperty("side", out var s) && string.Equals(s.GetString(), "Debit", StringComparison.OrdinalIgnoreCase);
                var monto = l.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDecimal() : 0m;
                var dim = l.TryGetProperty("dimensions", out var d) && d.ValueKind == JsonValueKind.Object ? d : default;
                string? Texto(string nombre) => dim.ValueKind == JsonValueKind.Object && dim.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var neto = debito ? monto : -monto;
                if (rol == "PuenteDelModulo" && Texto("targetModule") == ModulosDeOrigen.Cartera)
                    lista.Add(("RecaudoPorAplicar", Texto("collectorClass"), neto));
                else if (rol == "PuenteDeGiroDelOrigen" && Texto("orderOrigin") == "LendingDisbursement")
                    lista.Add(("DesembolsoPorGirar", null, neto));
            }
        }
        catch (JsonException)
        {
            return lista;
        }
        return lista;
    }

    private static string? ClaseDe(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return doc.RootElement.TryGetProperty("dimensions", out var d) && d.ValueKind == JsonValueKind.Object
                && d.TryGetProperty("collectorClass", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task AlertarAsync(DateOnly corte, ConjuntoDeCuentasDeCarteraDto set, string rol, decimal sinExplicar, CancellationToken ct)
    {
        if (alertas is null) return;
        var fecha = corte.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var cuentas = string.Join(",", set.Accounts.Select(a => a.AccountCode).Order(StringComparer.Ordinal));
        await alertas.LevantarAsync(new AlertaALevantar(
            TiposDeAlerta.DescuadreContable,
            $"Cartera y Contabilidad no cuadran en {rol} al {fecha}",
            $"La conciliación al {fecha} encontró una diferencia de {sinExplicar.ToString("N2", CultureInfo.GetCultureInfo("es-CO"))} en {rol} "
                + $"(cuentas {cuentas}) que ningún hecho de Cartera sin procesar explica. Revísela en Cartera › Conciliación contable.",
            DedupKey: $"{TiposDeAlerta.DescuadreContable}:{fecha}:{rol}:{cuentas}",
            RecipientPermissions: DestinatariosDelDescuadre,
            ModuloDeOrigen: "Lending"), ct);
    }
}
