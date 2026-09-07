namespace IngenIA365ERP.Application.Common.Models;

/// <summary>
/// Valores de <c>ADM_Tenants.ProvisioningState</c>. Son tres cadenas y viven en un
/// solo sitio porque quien las compara está en dos capas distintas: el alta las
/// escribe (Application) y el directorio de cooperativas las filtra (Persistence).
/// Un literal mal tecleado en cualquiera de los dos no da error de compilación;
/// da un trabajo de fondo que recorre cooperativas a medio hacer, o una que nunca
/// aparece.
/// </summary>
public static class EstadoDeAprovisionamiento
{
    /// <summary>La fila existe; la base puede existir sin tablas todavía.</summary>
    public const string EnCurso = "Provisioning";

    /// <summary>Base migrada, ranura de caché reservada, auditoría creada. Usable.</summary>
    public const string Listo = "Ready";

    /// <summary>
    /// Algo falló después del alta. La fila se conserva con el motivo en
    /// <c>ProvisioningError</c>; <c>POST /api/saas/tenants/{publicId}/provision</c>
    /// es idempotente y la repara.
    /// </summary>
    public const string Fallido = "Failed";
}
