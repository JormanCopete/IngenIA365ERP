using System.Globalization;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Los valores de <c>originEventKey</c> (feature 012, T8; contracts/mensajes.md §10.1; decisiones-transversales §2.6):
/// ésos y ningún otro. Con el origen y el tipo forman el único del mensaje; con el origen y el destino, la unidad
/// de contabilización (T11).
/// </summary>
public static partial class ClavesDeEvento
{
    /// <summary>Todo mensaje de un documento, salvo los dos de <see cref="ConfirmacionPor"/>.</summary>
    public const string Confirmacion = "Confirmation";

    /// <summary><c>GrupoContableReclasificado</c>.</summary>
    public const string Reclasificacion = "Reclassification";

    /// <summary>
    /// <c>Confirmation:{publicId:N}</c>: cada parte de <c>AjusteDeCostoReconocido</c> (el documento afectado) y los
    /// mensajes a Cartera (el pago de crédito).
    /// </summary>
    public static string ConfirmacionPor(Guid publicId) => $"{Confirmacion}:{publicId:N}";

    /// <summary><c>Close:{closingVersion}</c>.</summary>
    public static string Cierre(int closingVersion) => $"Close:{closingVersion.ToString(CultureInfo.InvariantCulture)}";

    /// <summary><c>Reopen:{closingVersion}</c>.</summary>
    public static string Reapertura(int closingVersion) => $"Reopen:{closingVersion.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Si la clave tiene la forma que el tipo admite.</summary>
    public static bool EsValida(FormaDeClaveDeEvento forma, string? clave) => clave is not null && forma switch
    {
        FormaDeClaveDeEvento.Confirmacion => clave == Confirmacion,
        FormaDeClaveDeEvento.ConfirmacionPorPublicId => ConPublicId().IsMatch(clave),
        FormaDeClaveDeEvento.Cierre => ConVersion().Match(clave) is { Success: true } m && m.Groups[1].Value == "Close",
        FormaDeClaveDeEvento.Reapertura => ConVersion().Match(clave) is { Success: true } m && m.Groups[1].Value == "Reopen",
        FormaDeClaveDeEvento.Reclasificacion => clave == Reclasificacion,
        _ => false,
    };

    [GeneratedRegex("^Confirmation:[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex ConPublicId();

    [GeneratedRegex("^(Close|Reopen):[1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ConVersion();
}
