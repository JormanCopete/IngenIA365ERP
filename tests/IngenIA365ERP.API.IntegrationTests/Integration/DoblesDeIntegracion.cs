using System.Collections.Concurrent;
using IngenIA365ERP.Application.Accounting.Inventory;
using IngenIA365ERP.Application.Accounting.Lending;
using IngenIA365ERP.Application.Accounting.Integration;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.API.IntegrationTests.Integration;

/// <summary>
/// Los dobles de la integración contable que usa <c>EntregaGarantizadaTests</c> (T472; quickstart §4.9): «Contabilidad no
/// responde» a la validación previa y «el destino falla por algo transitorio». Viven en el host de la suite y envuelven a las
/// implementaciones reales; <b>sólo muerden en la cooperativa que la prueba marca</b> (por su <c>PublicId</c>), así que las
/// demás pruebas de la colección no los notan. (nuevo)
/// </summary>
public sealed class DoblesDeIntegracion
{
    private readonly ConcurrentDictionary<Guid, bool> _sinRespuesta = new();
    private readonly ConcurrentDictionary<Guid, int> _fallosDelDestino = new();
    private readonly ConcurrentDictionary<Guid, (decimal Valor, DateOnly Corte)> _patrimonioTecnico = new();

    /// <summary>
    /// Feature 013, US2 (T0384): desde ahora Contabilidad informa a Cartera, en esa cooperativa, un patrimonio técnico de
    /// <paramref name="valor"/> con corte <paramref name="corte"/> (los indicadores reales los registra el contador desde US7). Sin marca, el
    /// puerto responde lo del provisional: «no disponible».
    /// </summary>
    public void PatrimonioTecnico(Guid cooperativa, decimal valor, DateOnly corte) => _patrimonioTecnico[cooperativa] = (valor, corte);

    /// <summary>Quita la marca: la cooperativa vuelve a «sin patrimonio técnico vigente».</summary>
    public void SinPatrimonioTecnico(Guid cooperativa) => _patrimonioTecnico.TryRemove(cooperativa, out _);

    internal (decimal Valor, DateOnly Corte)? PatrimonioDe(Guid? cooperativa) =>
        cooperativa is { } c && _patrimonioTecnico.TryGetValue(c, out var p) ? p : null;

    /// <summary>Desde ahora la validación previa de esa cooperativa «no responde» (el puerto lanza).</summary>
    public void ContabilidadNoResponde(Guid cooperativa, bool noResponde = true)
    {
        if (noResponde) _sinRespuesta[cooperativa] = true;
        else _sinRespuesta.TryRemove(cooperativa, out _);
    }

    /// <summary>Las próximas <paramref name="veces"/> entregas a Contabilidad de esa cooperativa lanzan (fallo transitorio).</summary>
    public void DestinoFalla(Guid cooperativa, int veces)
    {
        if (veces > 0) _fallosDelDestino[cooperativa] = veces;
        else _fallosDelDestino.TryRemove(cooperativa, out _);
    }

    internal bool NoResponde(Guid? cooperativa) => cooperativa is { } c && _sinRespuesta.ContainsKey(c);

    internal bool DebeFallar(Guid? cooperativa)
    {
        if (cooperativa is not { } c) return false;
        while (_fallosDelDestino.TryGetValue(c, out var quedan))
        {
            if (quedan <= 0) return false;
            if (_fallosDelDestino.TryUpdate(c, quedan - 1, quedan)) return true;
        }
        return false;
    }

    internal static Guid? Cooperativa(ICurrentTenantService tenant) =>
        Guid.TryParse(tenant.TenantId, out var id) ? id : null;
}

/// <summary>La validación previa real, salvo en la cooperativa marcada, donde «no responde».</summary>
public sealed class ContabilidadParaInventarioConDoble(ContabilidadParaInventario real, DoblesDeIntegracion dobles, ICurrentTenantService tenant)
    : IContabilidadParaInventario
{
    public Task<Result<ResultadoDeContabilizacionDto>> EvaluarAsync(IReadOnlyList<MensajeContableDto> mensajes, CancellationToken ct) =>
        dobles.NoResponde(DoblesDeIntegracion.Cooperativa(tenant))
            ? throw new TimeoutException("Doble de pruebas: Contabilidad no responde.")
            : real.EvaluarAsync(mensajes, ct);

    public Task<Result<IReadOnlyList<ConjuntoDeCuentasDto>>> SaldosDeCuentasMapeadasAsync(DateOnly corte, CancellationToken ct) =>
        real.SaldosDeCuentasMapeadasAsync(corte, ct);

    public Task<Result<CompletitudDeLaMatrizDto>> CompletitudAsync(DateOnly fecha, CancellationToken ct) => real.CompletitudAsync(fecha, ct);

    public Task<Result<VistaPreviaDeLoteDto>> PrevisualizarLoteAsync(IReadOnlyList<Guid> messagePublicIds, CancellationToken ct) =>
        real.PrevisualizarLoteAsync(messagePublicIds, ct);

    public Task<bool> SinIniciarAsync(CancellationToken ct) => real.SinIniciarAsync(ct);
}

/// <summary>El destino Contabilidad real, salvo en la cooperativa marcada, donde lanza las veces pedidas.</summary>
public sealed class DestinoContabilidadConDoble(DestinoContabilidad real, DoblesDeIntegracion dobles, ICurrentTenantService tenant) : IDestinoDeMensajes
{
    public string Destino => real.Destino;

    public bool Acepta(string type, int version) => real.Acepta(type, version);

    public Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct) =>
        dobles.DebeFallar(DoblesDeIntegracion.Cooperativa(tenant))
            ? throw new HttpRequestException("Doble de pruebas: falla transitoria del destino.")
            : real.ConsumirAsync(trabajo, ct);

    public IReadOnlyList<TrabajoDeConsumo> PlanearLote(IReadOnlyList<MensajeEntrante> entregas) => real.PlanearLote(entregas);

    public Task<TotalesDeLoteEnDestino> TotalesDelLoteAsync(Guid batchPublicId, CancellationToken ct) => real.TotalesDelLoteAsync(batchPublicId, ct);
}

/// <summary>
/// El puerto de Contabilidad para Cartera (feature 013, US2, T0384; US7 bloque C): el adaptador real (<see cref="ContabilidadParaCartera"/>),
/// salvo en la cooperativa marcada: con <see cref="DoblesDeIntegracion.PatrimonioTecnico"/> los indicadores traen ese patrimonio técnico, y con
/// <see cref="DoblesDeIntegracion.ContabilidadNoResponde"/> la validación previa «no responde» (el puerto lanza). Molde:
/// <see cref="ContabilidadParaInventarioConDoble"/>. (nuevo)
/// </summary>
public sealed class ContabilidadParaCarteraConDoble(ContabilidadParaCartera real, DoblesDeIntegracion dobles, ICurrentTenantService tenant)
    : IContabilidadParaCartera
{
    public Task<Result<ResultadoDeEvaluacionContableDto>> EvaluarAsync(IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct) =>
        dobles.NoResponde(DoblesDeIntegracion.Cooperativa(tenant))
            ? throw new TimeoutException("Doble de pruebas: Contabilidad no responde.")
            : real.EvaluarAsync(hechos, ct);

    public Task<Result<SaldosMapeadosDeCarteraDto>> SaldosDeCuentasMapeadasAsync(DateOnly corte, IReadOnlyList<CombinacionDeSaldo> combinaciones,
        CancellationToken ct) => real.SaldosDeCuentasMapeadasAsync(corte, combinaciones, ct);

    public Task<Result<EstadoDelPeriodoDto>> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct) => real.EstadoDelPeriodoAsync(fecha, ct);

    public async Task<Result<IndicadoresDeLaEntidadDto>> IndicadoresDeLaEntidadAsync(DateOnly fecha, CancellationToken ct)
    {
        if (dobles.PatrimonioDe(DoblesDeIntegracion.Cooperativa(tenant)) is not { } p || p.Corte > fecha)
            return await real.IndicadoresDeLaEntidadAsync(fecha, ct);
        return Result.Success(new IndicadoresDeLaEntidadDto(fecha, 0m, 0m,
            new IndicadorRegistradoDto(Guid.NewGuid(), p.Valor, "COP", p.Corte, "Doble de pruebas (e2e)"), null));
    }

    public Task<Result<IReadOnlyList<CuentaBancariaDeOrigenDto>>> CuentasBancariasAsync(Guid? bancoPublicId, CancellationToken ct) =>
        real.CuentasBancariasAsync(bancoPublicId, ct);
}
