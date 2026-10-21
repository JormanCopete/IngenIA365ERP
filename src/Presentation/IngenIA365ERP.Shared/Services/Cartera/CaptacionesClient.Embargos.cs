using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US11 (T192; contracts/api.md §8 y §4 /holds de la 014; research E161): los DTO de los embargos y bloqueos tal como los responde la API (los
// enums viajan como número). (nuevo)

/// <summary>Lo retenido de una cuenta por un embargo. (nuevo)</summary>
public sealed record RetencionDeEmbargoDto(Guid AccountPublicId, string Number, string? DepositClass, int Order, decimal Balance, decimal Protected,
    decimal Garnishable, decimal Retained);

/// <summary>Una respuesta al juzgado. (nuevo)</summary>
public sealed record RespuestaAlJuzgadoDto(Guid PublicId, int Kind, DateOnly SentOn, decimal? RetainedAmount, decimal? ConsignedAmount, string? Text,
    Guid? AttachmentPublicId, DateTime RespondedAt);

/// <summary>Un embargo. (nuevo)</summary>
public sealed record EmbargoDto(
    Guid PublicId,
    PersonaDeCuentaDto Person,
    int Authority,
    PersonaDeCuentaDto Court,
    string CourtName,
    string ProcessNumber,
    string OfficeReference,
    DateOnly OfficeDate,
    DateOnly ReceivedOn,
    string ClaimantName,
    decimal? Amount,
    bool IsAlimony,
    int AccountsScope,
    bool IncludesContributions,
    int Status,
    decimal RetainedTotal,
    decimal Consigned,
    decimal? UnembargableLimit,
    DateOnly? ResponseDueOn,
    bool ResponseOverdue,
    IReadOnlyList<RetencionDeEmbargoDto> Retained,
    IReadOnlyList<RespuestaAlJuzgadoDto> Responses,
    IReadOnlyList<string> Explanation,
    IReadOnlyList<string> Warnings,
    DateTime? ReleasedAt,
    string? ReleaseOfficeReference);

/// <summary>El borrador de una respuesta con la plantilla de la cooperativa. (nuevo)</summary>
public sealed record BorradorDeRespuestaDto(int Kind, string Text, bool FromEntityTemplate);

/// <summary>Una línea de la consignación al juzgado. (nuevo)</summary>
public sealed record LineaDeConsignacionDto(Guid AccountPublicId, string AccountNumber, Guid OperationPublicId, string OperationNumber, Guid OrderPublicId,
    string OrderNumber, decimal Amount);

/// <summary>La consignación al juzgado. (nuevo)</summary>
public sealed record ConsignacionDeEmbargoDto(decimal Amount, IReadOnlyList<LineaDeConsignacionDto> Lines, EmbargoDto Garnishment, IReadOnlyList<string> Warnings);

/// <summary>Un bloqueo de una cuenta. (nuevo)</summary>
public sealed record BloqueoDeCuentaDto(Guid PublicId, int Reason, decimal? Amount, bool Total, DateOnly ValidFrom, DateOnly? ValidTo, string? Notes,
    string? LoanNumber, Guid? GarnishmentPublicId, DateTime? ReleasedAt, string? ReleaseReason);

/// <summary>El cuerpo de <c>POST /garnishments</c>. (nuevo)</summary>
public sealed record RegistrarEmbargoRequest
{
    public Guid PersonPublicId { get; set; }
    public int Authority { get; set; } = 1;
    public Guid CourtPersonPublicId { get; set; }
    public string? CourtCode { get; set; }
    public string ProcessNumber { get; set; } = string.Empty;
    public string OfficeReference { get; set; } = string.Empty;
    public DateOnly OfficeDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? ReceivedOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string ClaimantName { get; set; } = string.Empty;
    public string? ClaimantTaxId { get; set; }
    public decimal? Amount { get; set; }
    public bool IsAlimony { get; set; }
    public int AccountsScope { get; set; } = 1;
    public IReadOnlyList<Guid>? AccountPublicIds { get; set; }
    public bool IncludesContributions { get; set; }
    public string? DestinationAccountNumber { get; set; }
    public Guid SupportAttachmentPublicId { get; set; }
    public string? Notes { get; set; }
}

public sealed record RespuestaAlJuzgadoRequest(int Kind, Guid? AttachmentPublicId, DateOnly SentOn, string? Text);

public sealed record BeneficiarioDeConsignacionRequest(Guid? BankPublicId, string? AccountNumber, byte? AccountType);

public sealed record ConsignarEmbargoRequest(decimal Amount, BeneficiarioDeConsignacionRequest? Payee, string Reason);

public sealed record LevantarEmbargoRequest(string ReleaseOfficeReference, DateOnly? ReleaseOfficeDate, Guid? SupportAttachmentPublicId, decimal? Amount, string Reason);

public sealed record BloquearCuentaRequest(int Reason, decimal? Amount, DateOnly? ValidTo, Guid? LoanPublicId, Guid? SupportAttachmentPublicId, string Notes);

public sealed record MotivoDeLevantamientoRequest(string Reason);

/// <summary>Los textos de los embargos y bloqueos en pantalla. (nuevo)</summary>
public static class TextosDeEmbargos
{
    public static readonly IReadOnlyList<(int Valor, string Texto)> Autoridades = [(1, "Juzgado"), (2, "DIAN (cobro coactivo)"), (3, "Otra entidad pública (coactivo)")];
    public static readonly IReadOnlyList<(int Valor, string Texto)> Estados = [(2, "Activo"), (3, "Levantado"), (4, "Rechazado (sólo aportes)")];
    public static readonly IReadOnlyList<(int Valor, string Texto)> Respuestas =
    [
        (1, "Acuse de recibo"), (2, "Informe de saldos retenidos"), (3, "Aportes inembargables"), (4, "Aviso de consignación"), (5, "Acuse del desembargo"),
    ];
    public static readonly IReadOnlyList<(int Valor, string Texto)> MotivosDeBloqueo =
        [(1, "Orden interna"), (2, "Garantía de un crédito"), (3, "Firma pendiente"), (99, "Otro")];

    public static string Autoridad(int v) => Autoridades.FirstOrDefault(a => a.Valor == v).Texto ?? v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static string Estado(int v) => v switch { 1 => "Registrado", 2 => "Activo", 3 => "Levantado", 4 => "Rechazado (sólo aportes)", _ => v.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    public static string Respuesta(int v) => Respuestas.FirstOrDefault(a => a.Valor == v).Texto ?? v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static string MotivoDeBloqueo(int v) => v switch
    {
        1 => "Orden interna", 2 => "Garantía de un crédito", 3 => "Firma pendiente", 4 => "Titular fallecido", 5 => "Coincidencia en listas", 6 => "Embargo", 99 => "Otro",
        _ => v.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}

/// <summary>
/// Feature 014, US11 (T192): las rutas de embargos y bloqueos. Sin <c>Authorization</c> a mano; toda escritura con su <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed partial class CaptacionesClient
{
    public const string RutaDeEmbargos = Base + "/garnishments";

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<EmbargoDto>>> EmbargosAsync(Guid? persona = null, int? estado = null, int pagina = 1, int tamano = 20,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<EmbargoDto>>(HttpMethod.Get, ConQuery(RutaDeEmbargos, Query(("personPublicId", Texto(persona)), ("status", Texto(estado)),
            ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<EmbargoDto>> EmbargoAsync(Guid embargo, CancellationToken ct = default) =>
        Enviar<EmbargoDto>(HttpMethod.Get, $"{RutaDeEmbargos}/{embargo}", null, null, ct);

    public Task<ResultadoDeInventario<EmbargoDto>> RegistrarEmbargoAsync(RegistrarEmbargoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmbargoDto>(HttpMethod.Post, RutaDeEmbargos, request, clave, ct);

    public Task<ResultadoDeInventario<BorradorDeRespuestaDto>> BorradorDeRespuestaAsync(Guid embargo, int clase, CancellationToken ct = default) =>
        Enviar<BorradorDeRespuestaDto>(HttpMethod.Get, ConQuery($"{RutaDeEmbargos}/{embargo}/responses/draft", Query(("kind", Texto(clase)))), null, null, ct);

    public Task<ResultadoDeInventario<RespuestaAlJuzgadoDto>> RegistrarRespuestaAsync(Guid embargo, RespuestaAlJuzgadoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<RespuestaAlJuzgadoDto>(HttpMethod.Post, $"{RutaDeEmbargos}/{embargo}/responses", request, clave, ct);

    public Task<ResultadoDeInventario<ConsignacionDeEmbargoDto>> ConsignarEmbargoAsync(Guid embargo, ConsignarEmbargoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ConsignacionDeEmbargoDto>(HttpMethod.Post, $"{RutaDeEmbargos}/{embargo}/consign", request, clave, ct);

    public Task<ResultadoDeInventario<EmbargoDto>> LevantarEmbargoAsync(Guid embargo, LevantarEmbargoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EmbargoDto>(HttpMethod.Post, $"{RutaDeEmbargos}/{embargo}/release", request, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<BloqueoDeCuentaDto>>> BloqueosAsync(Guid cuenta, bool incluirLevantados = false, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<BloqueoDeCuentaDto>>(HttpMethod.Get, ConQuery($"{RutaDeCuentas}/{cuenta}/holds", Query(("includeReleased", Texto(incluirLevantados)))),
            null, null, ct);

    public Task<ResultadoDeInventario<BloqueoDeCuentaDto>> BloquearAsync(Guid cuenta, BloquearCuentaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<BloqueoDeCuentaDto>(HttpMethod.Post, $"{RutaDeCuentas}/{cuenta}/holds", request, clave, ct);

    public Task<ResultadoDeInventario<BloqueoDeCuentaDto>> LevantarBloqueoAsync(Guid cuenta, Guid bloqueo, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<BloqueoDeCuentaDto>(HttpMethod.Post, $"{RutaDeCuentas}/{cuenta}/holds/{bloqueo}/release", new MotivoDeLevantamientoRequest(motivo), clave, ct);
}
