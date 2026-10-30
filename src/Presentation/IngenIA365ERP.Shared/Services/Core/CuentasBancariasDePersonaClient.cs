using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>Una cuenta bancaria de una persona (api.md §15). Los enums viajan como número. (nuevo)</summary>
public sealed record CuentaBancariaDePersonaDto(
    Guid PublicId,
    Guid PersonPublicId,
    Guid BankPublicId,
    string BankName,
    string? BankTransferCode,
    string AccountType,
    string AccountNumber,
    string HolderName,
    string HolderTaxId,
    Guid? CertificateAttachmentPublicId,
    int Origin,
    int Status,
    string RequestedByName,
    string? DecidedByName,
    DateTime? DecidedAt,
    DateOnly? PayableFrom,
    string? Reason,
    Guid? ReplacesPublicId,
    DateTime CreatedAt);

/// <summary>El cuerpo del alta o el cambio de una cuenta. (nuevo)</summary>
public sealed record PedirCuentaBancariaRequest(
    Guid BankPublicId,
    string AccountType,
    string AccountNumber,
    string HolderName,
    string HolderTaxId,
    Guid? CertificateAttachmentPublicId,
    Guid? ReplacesPublicId);

/// <summary>El motivo de un rechazo o un retiro. (nuevo)</summary>
public sealed record MotivoDeCuentaBancariaRequest(string Reason);

/// <summary>
/// Cliente tipado de las cuentas bancarias de una persona (feature 015, E-06, T0032; contracts/api.md §15): leer (lectura sensible auditada),
/// pedir el alta o el cambio con la certificación, y aprobar, rechazar o retirar. Sin <c>Authorization</c> a mano; las escrituras llevan
/// <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public sealed class CuentasBancariasDePersonaClient(HttpClient http, CentralAuthClient auth)
{
    /// <summary>El tipo de adjunto de la certificación bancaria (<c>AdjuntosDeModulo.CertificacionBancariaDePersona</c>).</summary>
    public const string TipoDeCertificacion = "PersonBankCertificate";

    public Task<ResultadoDeInventario<IReadOnlyList<CuentaBancariaDePersonaDto>>> ListarAsync(Guid persona, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CuentaBancariaDePersonaDto>>(HttpMethod.Get, $"/api/core/people/{persona}/bank-accounts", null, null, ct);

    public Task<ResultadoDeInventario<CuentaBancariaDePersonaDto>> PedirAsync(Guid persona, PedirCuentaBancariaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CuentaBancariaDePersonaDto>(HttpMethod.Post, $"/api/core/people/{persona}/bank-accounts", request, clave, ct);

    public Task<ResultadoDeInventario<CuentaBancariaDePersonaDto>> AprobarAsync(Guid cuenta, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CuentaBancariaDePersonaDto>(HttpMethod.Post, $"/api/core/person-bank-accounts/{cuenta}/approve", null, clave, ct);

    public Task<ResultadoDeInventario<CuentaBancariaDePersonaDto>> RechazarAsync(Guid cuenta, MotivoDeCuentaBancariaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CuentaBancariaDePersonaDto>(HttpMethod.Post, $"/api/core/person-bank-accounts/{cuenta}/reject", request, clave, ct);

    public Task<ResultadoDeInventario<CuentaBancariaDePersonaDto>> RetirarAsync(Guid cuenta, MotivoDeCuentaBancariaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CuentaBancariaDePersonaDto>(HttpMethod.Post, $"/api/core/person-bank-accounts/{cuenta}/retire", request, clave, ct);

    /// <summary>El estado en palabras de la pantalla.</summary>
    public static string Estado(int estado) => estado switch
    {
        1 => "Pendiente de aprobación",
        2 => "Activa",
        3 => "Rechazada",
        4 => "Retirada",
        _ => "Desconocido",
    };

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
