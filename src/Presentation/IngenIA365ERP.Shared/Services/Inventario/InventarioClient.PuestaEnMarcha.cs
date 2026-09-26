using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// La puesta en marcha (feature 012, T319; contracts/api.md §13.1–§13.3): el saldo inicial (plantilla 14 con
/// <see cref="RevisarPlantillaAsync"/>/<see cref="AplicarPlantillaAsync"/> sobre <see cref="RutaDeSaldoInicial"/>, la lista, el
/// detalle y confirmar, descartar y anular por el ciclo común de <see cref="RutasDeGrupo.SaldoInicial"/>), las cifras de SOLIDO
/// (plantilla 15, lotes y filas) y la vista previa y la activación de una bodega. Toda escritura con la
/// <see cref="ClaveDeOperacion"/> de la pantalla, nueva por operación.
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDeSaldoInicial = RutasDeGrupo.SaldoInicial;
    public const string RutaDeCifrasDeSolido = Base + "/legacy-figures";

    /// <summary>La clave de <c>extra</c> con el resumen por bodega del saldo inicial (§14).</summary>
    public const string ExtraSaldoPorBodega = "byWarehouse";

    /// <summary>La clave de <c>extra</c> con el resumen por fecha, bodega y grupo de las cifras de SOLIDO (§15).</summary>
    public const string ExtraCifrasPorFechaBodegaGrupo = "byDateWarehouseGroup";

    /// <summary>Los documentos de saldo inicial del alcance (clase <c>OpeningBalance</c>), con su estado y valor.</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ResumenDeDocumentoDto>>> SaldosInicialesAsync(Guid? bodega = null, int? estado = null,
        CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<ResumenDeDocumentoDto>>(HttpMethod.Get, ConQuery(RutaDeSaldoInicial, Query(
            ("warehousePublicId", bodega?.ToString()), ("status", estado?.ToString()), ("pageSize", "200"))), null, null, ct);

    public Task<ResultadoDeInventario<DocumentoDeInventarioDto>> SaldoInicialAsync(Guid id, CancellationToken ct = default) =>
        ObtenerDocumentoAsync(id, RutaDeSaldoInicial, ct);

    /// <summary>Confirma el saldo inicial; normalmente queda en aprobación (<c>Inventory.OpeningBalance.Approve</c>).</summary>
    public Task<ResultadoDeInventario<ResultadoDeConfirmacionDto>> ConfirmarSaldoInicialAsync(Guid id, byte[]? rowVersion, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        ConfirmarAsync(RutaDeSaldoInicial, id, rowVersion, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> DescartarSaldoInicialAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        DescartarAsync(RutaDeSaldoInicial, id, motivo, clave, ct);

    /// <summary>Anula un saldo confirmado; sólo mientras la bodega no esté activa (si no, <c>Inventory.OpeningBalance.WarehouseActive</c>).</summary>
    public Task<ResultadoDeInventario<ResultadoDeAnulacionDto>> AnularSaldoInicialAsync(Guid id, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        AnularAsync(RutaDeSaldoInicial, id, motivo, null, clave, ct);

    // ------------------------------------------------------------------------------------ cifras de SOLIDO --

    public Task<ResultadoDeInventario<IReadOnlyList<LoteDeCifrasDto>>> LotesDeCifrasAsync(DateOnly? fecha = null, string? bodega = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<LoteDeCifrasDto>>(HttpMethod.Get, ConQuery(RutaDeCifrasDeSolido, Query(
            ("asOf", fecha?.ToString("yyyy-MM-dd")), ("warehouseCode", bodega))), null, null, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<FilaDeCifraDto>>> FilasDeCifrasAsync(Guid lote, bool soloSinResolver, int pagina = 1, int tamano = 50,
        CancellationToken ct = default) =>
        EnviarAsync<PaginaDeInventarioDto<FilaDeCifraDto>>(HttpMethod.Get, ConQuery($"{RutaDeCifrasDeSolido}/{lote}/rows", Query(
            ("unresolvedOnly", soloSinResolver ? "true" : null), ("page", pagina.ToString()), ("pageSize", tamano.ToString()))), null, null, ct);

    // ------------------------------------------------------------------------------------------- activación --

    /// <summary>La vista previa de la activación de una bodega a <paramref name="corte"/> (§13.3).</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeActivacionDto>> VistaPreviaDeActivacionAsync(Guid bodega, DateOnly? corte, CancellationToken ct = default) =>
        EnviarAsync<VistaPreviaDeActivacionDto>(HttpMethod.Get, ConQuery($"{Base}/warehouses/{bodega}/activation", Query(("cutoffDate", corte?.ToString("yyyy-MM-dd")))),
            null, null, ct);

    /// <summary>Activa la bodega; con diferencia (o sin comparación contable) exige aceptarla con permiso y motivo.</summary>
    public Task<ResultadoDeInventario<ResultadoDeActivacionDto>> ActivarBodegaAsync(Guid bodega, ActivacionDeBodegaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeActivacionDto>(HttpMethod.Post, $"{Base}/warehouses/{bodega}/activation", request, clave, ct);

    /// <summary>Un comparativo con SOLIDO (<c>legacy-comparison-valuation</c> o <c>legacy-comparison-kardex</c>).</summary>
    public Task<ResultadoDeInventario<TablaReporteDto>> ComparativoConSolidoAsync(string vista, DateOnly? fecha, Guid? bodega, CancellationToken ct = default) =>
        InformeAsync(vista, Query(("asOf", fecha?.ToString("yyyy-MM-dd")), ("warehouse", bodega?.ToString())), ct);
}

// --------------------------------------------------------------------------------------------------- DTO --

/// <summary>El resumen de una bodega de la plantilla 14 (<c>extra.byWarehouse</c>, §14).</summary>
public sealed record ResumenDeSaldoInicialDto(string Bodega, DateOnly FechaDeCorte, int Lineas, int Documentos, decimal CantidadTotal, decimal ValorTotal,
    IReadOnlyList<ValorPorGrupoDto> ValorPorGrupo);

public sealed record ValorPorGrupoDto(string GrupoContable, decimal Valor);

/// <summary>Cantidad y valor de SOLIDO por (fecha, bodega, grupo) de la plantilla 15 (<c>extra.byDateWarehouseGroup</c>, §15).</summary>
public sealed record CifraPorFechaBodegaGrupoDto(DateOnly Fecha, string Bodega, string GrupoContable, decimal Cantidad, decimal Valor, int Filas);

/// <summary>Un lote de cifras de SOLIDO por fecha y bodega (§13.2).</summary>
public sealed record LoteDeCifrasDto(Guid BatchPublicId, DateOnly AsOf, string WarehouseCode, Guid? WarehousePublicId, int Rows, int ResolvedRows,
    int UnresolvedRows, decimal Quantity, decimal Value, DateTime ImportedAt, string? ImportedBy, bool Superseded, string SourceFileName);

public sealed record FilaDeCifraDto(string WarehouseCode, string ProductCode, Guid? WarehousePublicId, Guid? ProductPublicId, string? AccountingGroupCode,
    DateOnly AsOf, decimal? Quantity, decimal? Value);

/// <summary>La vista previa de la activación (<c>ActivationPreviewDto</c>, §13.3). En I1 no trae conjuntos.</summary>
public sealed record VistaPreviaDeActivacionDto
{
    public BodegaDeActivacionDto Warehouse { get; init; } = new(Guid.Empty, "", "");
    public DateOnly CutoffDate { get; init; }
    public SaldoDeActivacionDto OpeningBalance { get; init; } = new(false, 0m, []);
    public IReadOnlyList<ConjuntoDeActivacionDto> Sets { get; init; } = [];
    public decimal TotalDifference { get; init; }
    public IReadOnlyList<BloqueoDeActivacionDto> Blockers { get; init; } = [];
    public bool CanActivate { get; init; }
    public bool RequiresAcceptance { get; init; }
    public ResultadoDeActivacionDto? Activation { get; init; }
}

public sealed record BodegaDeActivacionDto(Guid PublicId, string Code, string Name);

public sealed record SaldoDeActivacionDto(bool Confirmed, decimal Value, IReadOnlyList<DocumentoDeActivacionDto> Documents);

/// <summary><c>Status</c>: el <c>DocumentStatus</c> (0 borrador, 1 en aprobación, 2 confirmado).</summary>
public sealed record DocumentoDeActivacionDto(Guid PublicId, string? DisplayNumber, int Status, decimal Value);

public sealed record ConjuntoDeActivacionDto(IReadOnlyList<CodigoYNombreDeActivacionDto> AccountingGroups, decimal LedgerBalance, decimal Valuation, decimal Difference);

public sealed record CodigoYNombreDeActivacionDto(string Code, string Name);

public sealed record BloqueoDeActivacionDto(string Code, string Message, System.Text.Json.JsonElement? Data);

public sealed record ResultadoDeActivacionDto(Guid ActivationPublicId, Guid WarehousePublicId, DateTime ActivatedAt, int ActivatedBy, DateOnly CutoffDate,
    decimal TotalDifference, bool DifferenceAccepted, string? Reason);

/// <summary>El cuerpo de la activación (§13.3).</summary>
public sealed record ActivacionDeBodegaRequest(DateOnly CutoffDate, bool AcceptDifference, string? Reason);
