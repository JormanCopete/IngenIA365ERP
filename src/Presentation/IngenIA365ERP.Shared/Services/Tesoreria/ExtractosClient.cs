using System.Globalization;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Una línea de la revisión previa del extracto. (nuevo)</summary>
public sealed record LineaDeRevisionDto(int Line, DateOnly MovementDate, string Direction, decimal Amount, string? Reference, string? DepositorTaxId, string? DepositorName,
    string Destination, int? DuplicatesLine);

public sealed record LineaIlegibleDto(int Line, string? Field, string Message);

/// <summary>La revisión previa o el resultado de una carga de extracto. (nuevo)</summary>
public sealed record CargaDeExtractoDto(Guid? StatementPublicId, string? Number, bool Preview, string Source, string? FormatCode, int Lines, int Loaded, int Omitted,
    int PossibleDuplicates, int Illegible, decimal Credits, decimal Debits, IReadOnlyList<LineaDeRevisionDto> Review, IReadOnlyList<LineaIlegibleDto> IllegibleLines);

/// <summary>Un extracto cargado. (nuevo)</summary>
public sealed record ExtractoDto(Guid PublicId, string Number, Guid BankAccountPublicId, string BankAccountCode, string Source, string? FormatCode, string? FileName,
    Guid? FileAttachmentPublicId, DateOnly From, DateOnly To, decimal? OpeningBalance, decimal? ClosingBalance, int Lines, int Omitted, int Illegible, int PossibleDuplicates,
    DateTime LoadedAt, string LoadedBy);

/// <summary>Un movimiento del extracto. (nuevo)</summary>
public sealed record MovimientoDeExtractoDto(Guid PublicId, int LineNumber, DateOnly MovementDate, string Direction, decimal Amount, string? Reference, string? BankSequence,
    string? DepositorTaxId, string? DepositorName, string? Description, decimal? Balance, bool HasBankIdentifier, bool PossibleDuplicate, string? DuplicateDecision,
    string RecaudoStatus, decimal RecaudoIdentifiedAmount, string ReconciliationStatus);

/// <summary>Un movimiento capturado a mano. (nuevo)</summary>
public sealed record MovimientoCapturadoRequest(DateOnly MovementDate, decimal Amount, string? Reference = null, string? BankSequence = null, string? DepositorTaxId = null,
    string? DepositorName = null, string? Description = null, decimal? Balance = null);

public sealed record CapturaDeExtractoRequest(Guid BankAccountPublicId, IReadOnlyList<MovimientoCapturadoRequest> Lines, DateOnly? From, DateOnly? To, decimal? OpeningBalance,
    decimal? ClosingBalance, bool Preview);

/// <summary>
/// Cliente tipado del extracto bancario (feature 015, US6, T0199; contracts/api.md §12): la carga única por archivo o por captura, con revisión previa, y su consulta. Lo
/// usan la pantalla de Tesorería y la de Recaudo. (nuevo)
/// </summary>
public sealed class ExtractosClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury/statements";

    public Task<ResultadoDeInventario<CargaDeExtractoDto>> CargarAsync(Guid cuenta, string nombre, byte[] contenido, bool revisar, bool aceptarIlegibles, Guid? formato,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.MultipartAsync<CargaDeExtractoDto>(http, auth, Base, nombre, contenido, new Dictionary<string, string?>
        {
            ["account"] = cuenta.ToString(), ["preview"] = revisar ? "true" : "false", ["acceptIllegible"] = aceptarIlegibles ? "true" : "false",
            ["format"] = formato?.ToString(),
        }, clave, ct);

    public Task<ResultadoDeInventario<CargaDeExtractoDto>> CapturarAsync(CapturaDeExtractoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<CargaDeExtractoDto>(http, auth, HttpMethod.Post, $"{Base}/captured", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ExtractoDto>>> ExtractosAsync(Guid? cuenta = null, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<ExtractoDto>>(http, auth, HttpMethod.Get,
            EnvioDeVentas.ConQuery(Base, EnvioDeVentas.Query(("account", cuenta?.ToString()))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<MovimientoDeExtractoDto>>> LineasAsync(Guid extracto, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<MovimientoDeExtractoDto>>(http, auth, HttpMethod.Get, $"{Base}/{extracto}/lines", null, null, ct);

    /// <summary>El texto de un destino de la revisión previa.</summary>
    public static string Destino(string d) => d switch
    {
        "Nueva" => "Se carga",
        "OmitidaPorHuella" => "Ya cargada (se omite)",
        "PosibleDuplicado" => "Posible duplicado",
        _ => d,
    };

    public static string Valor(decimal v) => v.ToString("N2", CultureInfo.GetCultureInfo("es-CO"));
}
