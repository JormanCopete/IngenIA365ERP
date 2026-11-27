using System.Globalization;
using System.Net;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Identity.PasesDeIngreso;

/// <summary>
/// Feature 027: los tres correos del pase de ingreso. Salen por <see cref="IEmailSender"/> directo, como los de la recuperación por correo: al
/// canjear todavía no hay cooperativa elegida, y los tres son de identidad central.
/// </summary>
public interface IAvisosDelPaseDeIngreso
{
    /// <summary>El pase al usuario: el único sitio donde existe el enlace en claro.</summary>
    Task EnviarPaseAsync(string correo, string cooperativa, string token, DateTime venceEn, string concedidoPor, string motivo,
        CancellationToken ct);

    /// <summary>A los demás administradores activos, sin el enlace.</summary>
    Task AvisarAdministradorAsync(string correo, string cooperativa, string destinatario, DateTime venceEn, string concedidoPor,
        string motivo, CancellationToken ct);

    /// <summary>Al usuario, en cada ingreso con el pase.</summary>
    Task AvisarUsoAsync(string correo, string cooperativa, DateTime cuando, string? ip, string? equipo, CancellationToken ct);
}

internal sealed class AvisosDelPaseDeIngreso(
    IIdentityEmailTemplates plantillas,
    IEmailSender correo,
    IOptions<IdentityEmailOptions> opciones,
    ILogger<AvisosDelPaseDeIngreso> logger) : IAvisosDelPaseDeIngreso
{
    /// <summary>El enlace lleva al login con el pase en <c>?pase=</c>; el login lo guarda y el desafío del segundo factor lo canjea.</summary>
    public const string RutaDelEnlace = "/security/login";

    public async Task EnviarPaseAsync(string destino, string cooperativa, string token, DateTime venceEn, string concedidoPor,
        string motivo, CancellationToken ct)
    {
        var html = await plantillas.RenderAsync("LoginPassGranted", new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Enlace"] = Enlace(token),
            ["Cooperativa"] = Texto(cooperativa),
            ["ConcedidoPor"] = Texto(concedidoPor),
            ["Motivo"] = Texto(motivo),
            ["VenceEn"] = HoraDeColombia(venceEn),
        }, ct);
        await correo.SendAsync(new EmailMessage(destino, "Tu pase de ingreso sin segundo factor — IngenIA365ERP", html,
            SiempreAlDestinatario: true), ct);
        logger.LogInformation("Pase de ingreso enviado a {Correo}; vence {VenceEn}.", destino, venceEn);
    }

    public async Task AvisarAdministradorAsync(string destino, string cooperativa, string destinatario, DateTime venceEn,
        string concedidoPor, string motivo, CancellationToken ct)
    {
        var html = await plantillas.RenderAsync("LoginPassAdminNotice", new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cooperativa"] = Texto(cooperativa),
            ["ConcedidoPor"] = Texto(concedidoPor),
            ["Destinatario"] = Texto(destinatario),
            ["Motivo"] = Texto(motivo),
            ["VenceEn"] = HoraDeColombia(venceEn),
        }, ct);
        await correo.SendAsync(new EmailMessage(destino, $"Pase de ingreso sin segundo factor para {destinatario} — IngenIA365ERP", html,
            SiempreAlDestinatario: true), ct);
    }

    public async Task AvisarUsoAsync(string destino, string cooperativa, DateTime cuando, string? ip, string? equipo, CancellationToken ct)
    {
        var html = await plantillas.RenderAsync("LoginPassUsed", new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cooperativa"] = Texto(cooperativa),
            ["Cuando"] = HoraDeColombia(cuando),
            ["Ip"] = Texto(ip ?? "desconocida"),
            ["Equipo"] = Texto(string.IsNullOrWhiteSpace(equipo) ? "equipo desconocido" : equipo.Length > 120 ? equipo[..120] : equipo),
        }, ct);
        await correo.SendAsync(new EmailMessage(destino, "Entraste con un pase temporal — IngenIA365ERP", html,
            SiempreAlDestinatario: true), ct);
    }

    private string Enlace(string token) =>
        $"{opciones.Value.BaseUrl.TrimEnd('/')}{RutaDelEnlace}?pase={Uri.EscapeDataString(token)}";

    /// <summary>El renderizador de plantillas no escapa: todo texto libre (el motivo lo escribe una persona) se escapa aquí.</summary>
    private static string Texto(string valor) => WebUtility.HtmlEncode(valor);

    /// <summary>Colombia no tiene horario de verano: UTC−5 todo el año.</summary>
    internal static string HoraDeColombia(DateTime utc) =>
        utc.AddHours(-5).ToString("dd/MM/yyyy hh:mm tt", CultureInfo.GetCultureInfo("es-CO"));
}
