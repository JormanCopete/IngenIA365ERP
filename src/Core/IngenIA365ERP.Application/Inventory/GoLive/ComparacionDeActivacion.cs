using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Contable = IngenIA365ERP.Application.Common.Integration.Accounting;

namespace IngenIA365ERP.Application.Inventory.GoLive;

/// <summary>
/// El cálculo común de la vista previa y de la activación (feature 012, T313; US7, T523; FR-090; api.md §13.3; data-model §6.4)
/// <b>(nuevo)</b>: la bodega (del alcance, si no 404), su saldo inicial, los bloqueos y los conjuntos de cuentas con su cuadre.
/// <list type="bullet">
/// <item>Los conjuntos los responde Contabilidad por <see cref="Contable.IContabilidadParaInventario.SaldosDeCuentasMapeadasAsync"/>
/// al corte (grupos y cuentas de rol <c>Inventario</c> y <c>Transito</c>, unidos cuando comparten cuenta, con su saldo sin alcance
/// de sucursal). Se muestran los que tocan algún grupo con valorizado en la bodega al corte.</item>
/// <item>Por cada conjunto se suma el valorizado al corte (<see cref="Periods.ValorizadoALaFecha"/>, grupo a esa fecha) de la bodega
/// que se activa y de las activas que usan esas cuentas; las <b>no activas</b> que las comparten suman con sus cifras de referencia a esa
/// misma fecha (<c>INV_LegacyFigures</c>) y se muestran aparte. Una bodega «usa» las cuentas de un grupo si el conjunto tiene el par
/// (grupo, su código) o (grupo, <c>*</c>).</item>
/// <item>La diferencia es valorizado − saldo contable; <c>explanation</c> cuenta los mensajes de negocio a Contabilidad hasta el corte
/// de esas bodegas pendientes, en lote, rechazados y «no aplica».</item>
/// <item>Bloqueos: ya activa, saldo inicial sin confirmar, corte distinto, período cerrado, bodegas no activas sin cifras
/// (<c>.LegacyFiguresMissing</c>), grupos de la bodega sin regla de inventario (<c>.RulesMissing</c>) y, si Contabilidad no responde
/// (sin puerto registrado, una falla o una excepción), <c>.AccountingUnavailable</c>, que el comando trata según el ambiente.</item>
/// </list>
/// </summary>
public sealed class ComparacionDeActivacion(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IDateTimeService reloj,
    Contable.IContabilidadParaInventario? contabilidad = null,
    ILectorDeParametros? parametros = null)
{
    /// <summary>Lo calculado: la vista, la bodega seguida por el contexto y el primer bloqueo que impide activar (nulo si ninguno).</summary>
    public sealed record Calculo(ActivationPreviewDto Vista, Warehouse Bodega, Error? BloqueoDuro);

    public async Task<Result<Calculo>> CalcularAsync(Guid bodegaPublicId, DateOnly? corte, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var bodega = await db.Warehouses.FirstOrDefaultAsync(w => w.PublicId == bodegaPublicId, ct);
        if (bodega is null || !alcance.IncluyeBodega(bodega.Id)) return Result.Failure<Calculo>(ErroresDeAlcance.BodegaInexistente());

        var saldos = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.Class == DocumentClass.OpeningBalance && d.WarehouseId == bodega.Id
                && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.PendingApproval || d.Status == DocumentStatus.Confirmed))
            .OrderBy(d => d.Id)
            .Select(d => new { d.PublicId, d.Prefix, d.Number, d.Status, d.CostTotal, d.OperationDate })
            .ToListAsync(ct);
        var confirmados = saldos.Where(s => s.Status == DocumentStatus.Confirmed).ToList();
        var pendientes = saldos.Where(s => s.Status != DocumentStatus.Confirmed).ToList();
        var fecha = corte ?? bodega.CutoffDate ?? reloj.HoyLocal.AddDays(-1);

        var bloqueos = new List<Error>();
        if (bodega.EstaActiva) bloqueos.Add(GoLiveErrors.ActivationAlreadyActive(bodega.Code, bodega.CutoffDate));
        if (pendientes.Count > 0)
            bloqueos.Add(GoLiveErrors.ActivationOpeningBalanceNotConfirmed(bodega.Code,
                pendientes.Select(p => (object)new { publicId = p.PublicId, status = p.Status.ToString() }).ToList()));
        var fechaDelSaldo = confirmados.Select(c => (DateOnly?)c.OperationDate).FirstOrDefault() ?? bodega.CutoffDate;
        if (!bodega.EstaActiva && fechaDelSaldo is { } delSaldo && delSaldo != fecha)
            bloqueos.Add(GoLiveErrors.ActivationCutoffMismatch(bodega.Code, fecha, delSaldo));
        var setup = await db.InventorySetups.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (setup?.LastClosedDate is { } cerrado && fecha <= cerrado) bloqueos.Add(InventoryErrors.PeriodClosed(fecha.Year, fecha.Month, cerrado));

        var cuadre = await CuadreAsync(bodega, fecha, ct);
        if (cuadre is not null) bloqueos.AddRange(cuadre.Bloqueos);
        var duro = bloqueos.FirstOrDefault();
        if (cuadre is null) bloqueos.Add(GoLiveErrors.ActivationAccountingUnavailable());
        var sets = cuadre?.Conjuntos ?? [];
        var diferencia = sets.Sum(s => s.Difference);

        ActivationResultDto? activacion = null;
        if (bodega.EstaActiva)
        {
            activacion = await db.WarehouseActivations.AsNoTracking().Where(a => a.WarehouseId == bodega.Id)
                .Select(a => new ActivationResultDto(a.PublicId, bodega.PublicId, a.ActivatedAt, a.ActivatedByUserId, a.CutoffDate, a.TotalDifference,
                    a.DifferenceAcceptedByUserId != null, a.AcceptanceReason))
                .FirstOrDefaultAsync(ct);
        }

        var vista = new ActivationPreviewDto(
            new BodegaDeActivacionDto(bodega.PublicId, bodega.Code, bodega.Name),
            fecha,
            new SaldoInicialDeActivacionDto(confirmados.Count > 0 && pendientes.Count == 0, confirmados.Sum(c => c.CostTotal),
                saldos.Select(s => new DocumentoDeActivacionDto(s.PublicId, VistaDeDocumentos.NumeroVisible(s.Prefix, s.Number), s.Status, s.CostTotal)).ToList()),
            sets,
            diferencia,
            bloqueos.Select(b => new BloqueoDeActivacionDto(b.Code, b.Message, (b as ErrorConDatos)?.Data)).ToList(),
            CanActivate: duro is null,
            RequiresAcceptance: cuadre is null || diferencia != 0m,
            activacion);
        return Result.Success(new Calculo(vista, bodega, duro));
    }

    /// <summary>Los conjuntos con su cuadre y los bloqueos que salen de ellos. (nuevo, T523)</summary>
    private sealed record Cuadre(IReadOnlyList<ConjuntoDeCuentasDto> Conjuntos, IReadOnlyList<Error> Bloqueos);

    private static bool Mismo(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>¿El conjunto lleva las cuentas del grupo en esa bodega? (par exacto o <c>*</c>).</summary>
    private static bool Usa(Contable.ConjuntoDeCuentasDto c, string grupo, string bodega) =>
        c.Pairs.Any(p => Mismo(p.AccountingGroupCode, grupo) && (p.WarehouseCode == "*" || Mismo(p.WarehouseCode, bodega)));

    /// <summary>
    /// Los conjuntos de cuentas al corte con su cuadre. Nulo = Contabilidad no responde (sin puerto registrado, una falla o una
    /// excepción: se trata igual, porque la bodega no se puede comparar con los libros).
    /// </summary>
    private async Task<Cuadre?> CuadreAsync(Warehouse bodega, DateOnly corte, CancellationToken ct)
    {
        if (contabilidad is null) return null;
        IReadOnlyList<Contable.ConjuntoDeCuentasDto> conjuntos;
        try
        {
            var respuesta = await contabilidad.SaldosDeCuentasMapeadasAsync(corte, ct);
            if (respuesta.IsFailure) return null;
            conjuntos = respuesta.Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }

        var valorizado = await ValorizadoPorGrupoYBodegaAsync(db, parametros, corte, ct);
        var grupos = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(g => g.Id, g => (g.Code, g.Name), ct);
        var bodegas = await db.Warehouses.AsNoTracking().ToListAsync(ct);

        // Los grupos de la bodega: los que tienen valorizado en ella al corte.
        var gruposDeLaBodega = valorizado.Where(v => v.Key.WarehouseId == bodega.Id && (v.Value.Cantidad != 0m || v.Value.Valor != 0m))
            .Select(v => v.Key.Grupo).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        var sinRegla = gruposDeLaBodega.Where(g => !conjuntos.Any(c => Usa(c, g, bodega.Code))).ToList();

        var relevantes = conjuntos.Where(c => gruposDeLaBodega.Any(g => c.AccountingGroupCodes.Any(x => Mismo(x, g)))).ToList();
        var cifras = await CifrasAlCorteAsync(db, corte, ct);
        var sinCifras = new List<string>();
        var resultado = new List<ConjuntoDeCuentasDto>();
        foreach (var c in relevantes)
        {
            var codigos = c.AccountingGroupCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool LaUsa(Warehouse w) => codigos.Any(g => Usa(c, g, w.Code));
            decimal ValorDe(int bodegaId) => valorizado.Where(v => v.Key.WarehouseId == bodegaId && codigos.Contains(v.Key.Grupo)).Sum(v => v.Value.Valor);

            var esta = ValorDe(bodega.Id);
            var activas = bodegas.Where(w => w.Id != bodega.Id && w.EstaActiva && LaUsa(w))
                .Select(w => new BodegaActivaDelConjuntoDto(w.PublicId, w.Code, ValorDe(w.Id)))
                .Where(a => a.Value != 0m).OrderBy(a => a.Code, StringComparer.Ordinal).ToList();
            var fueraDelModulo = new List<BodegaFueraDelModuloDelConjuntoDto>();
            foreach (var w in bodegas.Where(w => w.Id != bodega.Id && !w.EstaActiva && !w.EsTransito && LaUsa(w)).OrderBy(w => w.Code, StringComparer.Ordinal))
            {
                var suyas = cifras.Where(f => f.WarehouseId == w.Id).ToList();
                if (suyas.Count == 0)
                {
                    if (!sinCifras.Contains(w.Code)) sinCifras.Add(w.Code);
                    continue;
                }
                var valor = suyas.Where(f => f.AccountingGroupId is int g && grupos.TryGetValue(g, out var gr) && codigos.Contains(gr.Code)).Sum(f => f.Value);
                fueraDelModulo.Add(new BodegaFueraDelModuloDelConjuntoDto(w.Code, w.PublicId, valor, corte, suyas.OrderByDescending(f => f.CreatedAt).First().Lote));
            }

            var total = esta + activas.Sum(a => a.Value) + fueraDelModulo.Sum(l => l.Value);
            var usadas = bodegas.Where(w => w.Id == bodega.Id || (w.EstaActiva && LaUsa(w))).Select(w => w.Code).ToList();
            resultado.Add(new ConjuntoDeCuentasDto(
                c.AccountingGroupCodes.Select(g => new CodigoYNombreDto(g, grupos.Values.Where(x => Mismo(x.Code, g)).Select(x => x.Name).FirstOrDefault() ?? g)).ToList(),
                c.Accounts.Select(a => new CuentaDelConjuntoDto(a.AccountCode, a.AccountName, a.Role, a.BalanceByBranch.Sum(b => b.Balance))).ToList(),
                c.Balance,
                new ValorizadoDelConjuntoDto(esta, activas, fueraDelModulo, total),
                total - c.Balance,
                await ExplicacionAsync(db, usadas, corte, ct)));
        }

        var bloqueos = new List<Error>();
        if (sinCifras.Count > 0) bloqueos.Add(GoLiveErrors.ActivationLegacyFiguresMissing(sinCifras, corte));
        if (sinRegla.Count > 0) bloqueos.Add(GoLiveErrors.ActivationRulesMissing(bodega.Code, sinRegla));
        return new Cuadre(resultado, bloqueos);
    }

    // ------------------------------------------------------------------------------ piezas que comparte la conciliación --

    /// <summary>El valorizado al corte (<see cref="Periods.ValorizadoALaFecha"/>) por código de grupo contable a esa fecha y bodega.</summary>
    public static async Task<IReadOnlyDictionary<(string Grupo, int WarehouseId), (decimal Cantidad, decimal Valor)>> ValorizadoPorGrupoYBodegaAsync(
        IApplicationDbContext db, ILectorDeParametros? parametros, DateOnly corte, CancellationToken ct)
    {
        var filas = await new Periods.ValorizadoALaFecha(db, parametros ?? new LectorDeParametros(db)).CalcularAsync(corte, null, ct);
        var codigos = await GrupoContableALaFecha.CodigosAsync(db, filas.Select(f => f.ProductId).Distinct().ToList(), corte, ct);
        return filas
            .Where(f => codigos.GetValueOrDefault(f.ProductId) is not null)
            .GroupBy(f => (Grupo: codigos[f.ProductId]!, f.WarehouseId))
            .ToDictionary(g => g.Key, g => (g.Sum(f => f.Quantity), g.Sum(f => f.Value)));
    }

    /// <summary>Una fila de las cifras de referencia al corte: bodega, grupo, valor, lote y cuándo se importó. (nuevo)</summary>
    public sealed record CifraAlCorte(int WarehouseId, int? AccountingGroupId, decimal Value, Guid Lote, DateTime CreatedAt);

    /// <summary>Las cifras de referencia vigentes a esa fecha de corte (las dadas de baja por un lote posterior no cuentan).</summary>
    public static async Task<IReadOnlyList<CifraAlCorte>> CifrasAlCorteAsync(IApplicationDbContext db, DateOnly corte, CancellationToken ct) =>
        await db.LegacyFigures.AsNoTracking().Where(f => f.AsOfDate == corte && f.WarehouseId != null)
            .Select(f => new CifraAlCorte(f.WarehouseId!.Value, f.AccountingGroupId, f.Value ?? 0m, f.ImportBatchPublicId, f.CreatedAt))
            .ToListAsync(ct);

    /// <summary>Los mensajes de negocio a Contabilidad hasta el corte de esas bodegas, por estado de la entrega.</summary>
    public static async Task<ExplicacionDelConjuntoDto> ExplicacionAsync(IApplicationDbContext db, IReadOnlyCollection<string> bodegas, DateOnly corte, CancellationToken ct)
    {
        var lista = bodegas.ToList();
        var estados = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.Destination == IntegrationDestinations.Accounting
                && d.Message!.Kind == IntegrationMessageKind.Business
                && d.Message.OperationDate <= corte
                && d.Message.WarehouseCode != null && lista.Contains(d.Message.WarehouseCode))
            .GroupBy(d => d.Status)
            .Select(g => new { g.Key, Cuantas = g.Count() })
            .ToListAsync(ct);
        int De(DeliveryStatus s) => estados.FirstOrDefault(e => e.Key == s)?.Cuantas ?? 0;
        return new ExplicacionDelConjuntoDto(De(DeliveryStatus.Pending), De(DeliveryStatus.InBatch), De(DeliveryStatus.Rejected), De(DeliveryStatus.NotApplicable));
    }
}
