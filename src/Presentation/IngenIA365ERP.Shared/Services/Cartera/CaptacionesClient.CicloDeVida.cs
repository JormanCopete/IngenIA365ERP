using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US14 (T212; contracts/api.md §13 y §14; research §23): los DTO del fallecimiento del titular y de los saldos no reclamados tal como los responde
// la API (los enums viajan como número). (nuevo)

public sealed record CuentaDelFallecidoDto(Guid PublicId, string Number, int? DepositClass, int Status, decimal Balance, bool Deliverable, string? Note);

public sealed record BeneficiarioDelFallecidoDto(Guid AccountPublicId, string AccountNumber, PersonaDeCuentaDto Person, decimal Percent);

public sealed record PorcionDeEntregaDto(Guid AccountPublicId, string AccountNumber, PersonaDeCuentaDto Person, decimal Percent, decimal Amount);

public sealed record TotalDeDestinatarioDto(PersonaDeCuentaDto Person, decimal Amount);

public sealed record TopeDeEntregaDto(decimal? Amount, string Source, DateOnly ValidFrom);

/// <summary>La vista previa de la entrega por fallecimiento, con la huella que el registro compara. (nuevo)</summary>
public sealed record VistaPreviaDeEntregaDto(
    int Route,
    DateOnly Date,
    IReadOnlyList<CuentaDelFallecidoDto> Accounts,
    IReadOnlyList<PorcionDeEntregaDto> Portions,
    IReadOnlyList<TotalDeDestinatarioDto> Recipients,
    decimal Total,
    TopeDeEntregaDto? CapWithoutSuccession,
    IReadOnlyList<string> SupportsRequired,
    IReadOnlyList<string> Warnings,
    string Sha256);

public sealed record DestinatarioPlaneadoDto(PersonaDeCuentaDto Person, decimal? Percent, int Form, string? TargetAccountNumber);

public sealed record EntregaHechaDto(Guid OperationPublicId, string OperationNumber, Guid AccountPublicId, string AccountNumber, PersonaDeCuentaDto Recipient,
    decimal Amount, string Form, string? OrderNumber, string? OrderStatus, bool Reversed);

/// <summary>Un caso de fallecimiento. (nuevo)</summary>
public sealed record CasoDeFallecimientoDto(
    Guid PublicId,
    string Number,
    PersonaDeCuentaDto Person,
    DateOnly DeathDate,
    DateOnly ReportedOn,
    int Status,
    int? Route,
    decimal TotalAmount,
    decimal? WithoutProbateCap,
    bool MasterMarksDeceased,
    Guid DeathCertificateAttachmentPublicId,
    IReadOnlyList<Guid> SupportAttachmentPublicIds,
    IReadOnlyList<CuentaDelFallecidoDto> Accounts,
    IReadOnlyList<BeneficiarioDelFallecidoDto> Beneficiaries,
    IReadOnlyList<DestinatarioPlaneadoDto> PlannedRecipients,
    IReadOnlyList<EntregaHechaDto> Deliveries,
    string Reason,
    DateTime RegisteredAt,
    DateTime? DeliveryRegisteredAt,
    DateTime? ApprovedAt,
    DateTime? DeliveredAt,
    string? CancelReason,
    IReadOnlyList<string> Warnings);

/// <summary>Un destinatario pedido (porcentaje como fracción; forma 1 giro, 2 traslado). (nuevo)</summary>
public sealed record DestinatarioDeEntregaRequest(Guid PersonPublicId, decimal? Percent, int Form, Guid? TargetAccountPublicId, Guid? BankPublicId,
    byte? AccountType, string? AccountNumber);

public sealed record RegistrarFallecimientoRequest(Guid PersonPublicId, DateOnly DateOfDeath, Guid DeathCertificateAttachmentPublicId, string Reason);

public sealed record VistaPreviaDeEntregaRequest(int Mode, IReadOnlyList<DestinatarioDeEntregaRequest>? Recipients);

public sealed record RegistrarEntregaRequest(int Mode, IReadOnlyList<DestinatarioDeEntregaRequest>? Recipients, IReadOnlyList<Guid> SupportAttachmentPublicIds,
    string ExpectedPreviewSha256, string Reason);

/// <summary>Un saldo no reclamado. (nuevo)</summary>
public sealed record SaldoNoReclamadoDto(
    Guid PublicId,
    PersonaDeCuentaDto Person,
    int Source,
    Guid AccountPublicId,
    string AccountNumber,
    string? SourceReference,
    decimal Amount,
    DateOnly TransferredOn,
    string TransferOperationNumber,
    int Status,
    DateOnly? ClaimedOn,
    string? ClaimOrderNumber,
    string Reason);

public sealed record TrasladarANoReclamadosRequest(IReadOnlyList<Guid>? AccountPublicIds, IReadOnlyList<Guid>? RefundPublicIds, string Reason);

public sealed record BancoDelTitularRequest(Guid? BankPublicId, byte? AccountType, string? AccountNumber);

public sealed record PagarNoReclamadoRequest(int Form, Guid? TargetAccountPublicId, BancoDelTitularRequest? Payee, string Reason);

/// <summary>Los textos del fallecimiento y de los no reclamados en pantalla. (nuevo)</summary>
public static class TextosDeCicloDeVida
{
    public const string DuenoDeSoportes = "LendingDeceasedSupport";

    public static string EstadoDelCaso(int estado) => estado switch
    {
        1 => "Registrado",
        2 => "Entrega aprobada (giros en curso)",
        3 => "Entregado",
        4 => "Anulado",
        5 => "Entrega por aprobar",
        _ => estado.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string Ruta(int? ruta) => ruta switch
    {
        1 => "A los beneficiarios",
        2 => "Sin juicio de sucesión",
        3 => "Por sucesión",
        _ => "—",
    };

    public static string Origen(int fuente) => fuente switch
    {
        1 => "Devolución no cobrada",
        2 => "Cuenta inactiva",
        3 => "Giro no cobrado",
        _ => fuente.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string EstadoNoReclamado(int estado) => estado == 2 ? "Pagado" : "Retenido";
}

public sealed partial class CaptacionesClient
{
    public const string RutaDeFallecimientos = Base + "/deceased-cases";
    public const string RutaDeNoReclamados = Base + "/unclaimed";

    public Task<ResultadoDeInventario<PaginaDeCarteraDto<CasoDeFallecimientoDto>>> CasosDeFallecimientoAsync(int? estado = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<CasoDeFallecimientoDto>>(HttpMethod.Get, ConQuery(RutaDeFallecimientos, Query(("status", Texto(estado)), ("page", Texto(pagina)))),
            null, null, ct);

    public Task<ResultadoDeInventario<CasoDeFallecimientoDto>> CasoDeFallecimientoAsync(Guid caso, CancellationToken ct = default) =>
        Enviar<CasoDeFallecimientoDto>(HttpMethod.Get, $"{RutaDeFallecimientos}/{caso}", null, null, ct);

    public Task<ResultadoDeInventario<CasoDeFallecimientoDto>> RegistrarFallecimientoAsync(RegistrarFallecimientoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CasoDeFallecimientoDto>(HttpMethod.Post, RutaDeFallecimientos, request, clave, ct);

    public Task<ResultadoDeInventario<VistaPreviaDeEntregaDto>> VistaPreviaDeEntregaAsync(Guid caso, VistaPreviaDeEntregaRequest request,
        CancellationToken ct = default) =>
        Enviar<VistaPreviaDeEntregaDto>(HttpMethod.Post, $"{RutaDeFallecimientos}/{caso}/delivery/preview", request, null, ct);

    public Task<ResultadoDeInventario<CasoDeFallecimientoDto>> RegistrarEntregaAsync(Guid caso, RegistrarEntregaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CasoDeFallecimientoDto>(HttpMethod.Post, $"{RutaDeFallecimientos}/{caso}/delivery", request, clave, ct);

    public Task<ResultadoDeInventario<CasoDeFallecimientoDto>> AprobarEntregaAsync(Guid caso, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CasoDeFallecimientoDto>(HttpMethod.Post, $"{RutaDeFallecimientos}/{caso}/delivery/approve", new MotivoDeCarteraRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<CasoDeFallecimientoDto>> AnularCasoDeFallecimientoAsync(Guid caso, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CasoDeFallecimientoDto>(HttpMethod.Post, $"{RutaDeFallecimientos}/{caso}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<PaginaDeCarteraDto<SaldoNoReclamadoDto>>> NoReclamadosAsync(Guid? persona = null, int? estado = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<SaldoNoReclamadoDto>>(HttpMethod.Get, ConQuery(RutaDeNoReclamados, Query(("personPublicId", persona?.ToString()),
            ("status", Texto(estado)), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<SaldoNoReclamadoDto>>> TrasladarANoReclamadosAsync(TrasladarANoReclamadosRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<SaldoNoReclamadoDto>>(HttpMethod.Post, $"{RutaDeNoReclamados}/transfer", request, clave, ct);

    public Task<ResultadoDeInventario<SaldoNoReclamadoDto>> PagarNoReclamadoAsync(Guid saldo, PagarNoReclamadoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SaldoNoReclamadoDto>(HttpMethod.Post, $"{RutaDeNoReclamados}/{saldo}/pay", request, clave, ct);
}
