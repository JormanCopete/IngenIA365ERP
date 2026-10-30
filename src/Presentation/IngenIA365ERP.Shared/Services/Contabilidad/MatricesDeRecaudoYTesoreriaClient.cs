using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>Las dimensiones de una regla, por código; nula = cualquiera. (nuevo)</summary>
public sealed record DimensionesDeReglaDeModuloDto(
    string? BranchCode = null, string? CollectionPointCode = null, string? CashBoxCode = null, string? MeansCode = null, int? MeansClass = null,
    string? MiscConceptCode = null, string? TargetModule = null, string? TargetNature = null, string? CollectorClass = null, string? OwnBankAccountCode = null,
    string? OrderOrigin = null, string? OrderKind = null, string? Cause = null)
{
    /// <summary>La combinación como texto: <c>CashBoxCode=C1 · MeansCode=EF</c>; «cualquiera» si no fija nada.</summary>
    public string Texto()
    {
        var partes = new List<string>();
        void Poner(string clave, string? valor) { if (!string.IsNullOrWhiteSpace(valor)) partes.Add($"{clave}={valor}"); }
        Poner("Oficina", BranchCode); Poner("Punto", CollectionPointCode); Poner("Caja", CashBoxCode); Poner("Medio", MeansCode);
        Poner("Clase de medio", MeansClass?.ToString()); Poner("Concepto", MiscConceptCode); Poner("Módulo", TargetModule); Poner("Naturaleza", TargetNature);
        Poner("Recaudador", CollectorClass); Poner("Cuenta propia", OwnBankAccountCode); Poner("Origen", OrderOrigin); Poner("Clase de orden", OrderKind); Poner("Causa", Cause);
        return partes.Count == 0 ? "cualquiera" : string.Join(" · ", partes);
    }
}

public sealed record ReglaDeModuloDto(Guid PublicId, string Module, string FactType, string Role, DimensionesDeReglaDeModuloDto Dimensions, Guid AccountPublicId,
    string AccountCode, string AccountName, Guid? CostCenterPublicId, DateOnly ValidFrom, DateOnly? ValidTo, int SpecificityWeight, string Notes);

public sealed record FilaDePlantillaDeModuloDto(string FactType, string Role, string RoleName, DimensionesDeReglaDeModuloDto Dimensions, string Description,
    IReadOnlyList<string> Facts);

public sealed record FilaDeCompletitudDeModuloDto(string FactType, string FactName, string Role, string RoleName, DimensionesDeReglaDeModuloDto Dimensions, string Status,
    string? AccountCode, string? Detail);

public sealed record CompletitudDeModuloDto(string Module, DateOnly AsOf, bool NotPosted, decimal Percent, int Complete, int Total, IReadOnlyList<FilaDeCompletitudDeModuloDto> Rows);

public sealed record MapeoDeModuloDto(string FactType, string FactName, string SuggestedVoucherType, string? VoucherTypeCode, Guid? VoucherTypePublicId,
    string? CrossDocumentTypeCode, bool IsSeeded, DateTime? ValidatedAt);

public sealed record LoteDeModuloDto(Guid BatchPublicId, long Number, string Trigger, string Status, DateTime CreatedAt, DateOnly? DateFrom, DateOnly? DateTo, int Facts,
    int Vouchers, int WithoutVoucher, decimal TotalDebit, decimal TotalCredit);

public sealed record CrearReglaDeModuloRequest(string? FactType, string Role, DimensionesDeReglaDeModuloDto? Dimensions, Guid AccountPublicId, Guid? CostCenterPublicId,
    DateOnly ValidFrom, string? Notes, string Reason);

public sealed record DesactivarReglaDeModuloRequest(DateOnly ValidTo, string Reason);

public sealed record MapeoDeModuloRequest(Guid VoucherTypePublicId, Guid? CrossDocumentTypePublicId, string Reason);

/// <summary>
/// El cliente de las matrices contables de Recaudo y de Tesorería (feature 015, US8, T0266; api.md §16): el mismo juego de rutas bajo
/// <c>/api/accounting/receipts</c> y <c>/api/accounting/treasury</c>, elegido por <c>modulo</c> (<c>receipts</c> o <c>treasury</c>). La cabecera
/// <c>Authorization</c> la pone el handler de la sesión. (nuevo)
/// </summary>
public sealed class MatricesDeRecaudoYTesoreriaClient(HttpClient http, CentralAuthClient auth)
{
    public const string Recaudo = "receipts";
    public const string Tesoreria = "treasury";

    private static string Base(string modulo) => $"/api/accounting/{modulo}";

    public Task<ResultadoDeInventario<IReadOnlyList<ReglaDeModuloDto>>> ReglasAsync(string modulo, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ReglaDeModuloDto>>(HttpMethod.Get, ConQuery($"{Base(modulo)}/rules", Query(("asOf", Texto(fecha)))), null, ct);

    public Task<ResultadoDeInventario<ReglaDeModuloDto>> CrearReglaAsync(string modulo, CrearReglaDeModuloRequest r, CancellationToken ct = default) =>
        Enviar<ReglaDeModuloDto>(HttpMethod.Post, $"{Base(modulo)}/rules", r, ct);

    public Task<ResultadoDeInventario<object>> DesactivarReglaAsync(string modulo, Guid regla, DesactivarReglaDeModuloRequest r, CancellationToken ct = default) =>
        Enviar<object>(HttpMethod.Post, $"{Base(modulo)}/rules/{regla}/deactivate", r, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<FilaDePlantillaDeModuloDto>>> PlantillaAsync(string modulo, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<FilaDePlantillaDeModuloDto>>(HttpMethod.Get, $"{Base(modulo)}/rules/template", null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> PlantillaXlsxAsync(string modulo, CancellationToken ct = default) =>
        DescargarAsync(http, auth, $"{Base(modulo)}/rules/template.xlsx", $"plantilla-matriz-{modulo}.xlsx", ct);

    public Task<ResultadoDeInventario<CompletitudDeModuloDto>> CompletitudAsync(string modulo, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<CompletitudDeModuloDto>(HttpMethod.Get, ConQuery($"{Base(modulo)}/rules/completeness", Query(("asOf", Texto(fecha)))), null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<MapeoDeModuloDto>>> TiposDeComprobanteAsync(string modulo, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<MapeoDeModuloDto>>(HttpMethod.Get, $"{Base(modulo)}/voucher-types", null, ct);

    public Task<ResultadoDeInventario<MapeoDeModuloDto>> AsignarTipoAsync(string modulo, string hecho, MapeoDeModuloRequest r, CancellationToken ct = default) =>
        Enviar<MapeoDeModuloDto>(HttpMethod.Put, $"{Base(modulo)}/voucher-types/{Uri.EscapeDataString(hecho)}", r, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<LoteDeModuloDto>>> LotesAsync(string modulo, DateOnly? desde = null, DateOnly? hasta = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<LoteDeModuloDto>>(HttpMethod.Get, ConQuery($"{Base(modulo)}/batches", Query(("from", Texto(desde)), ("to", Texto(hasta)))), null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, null, ct);
}
