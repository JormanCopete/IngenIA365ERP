using IngenIA365ERP.Domain.Enums.Dian;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Channels;

/// <summary>
/// Lo que declara un canal que habla con un servicio externo por cooperativa (feature 028; contracts/canal-conecta.md §1, §3 y §9):
/// el ambiente que exige, los extremos de su lista blanca y si emite con validez legal. Lo leen <c>ConfigureEmissionCommand</c> (rechazos
/// de §9), la guardia fiscal y la pantalla. Un canal que no lo implementa (como <c>SIMULADO</c>) no pide tenant ni extremo. (nuevo)
/// </summary>
public interface IDeclaraParametrosDelCanal
{
    /// <summary>El único ambiente en que se admite el canal.</summary>
    DianEnvironment AmbienteExigido { get; }

    /// <summary>Las claves de los extremos habilitados en esta instalación (<c>QA</c>, <c>PDN</c>), nunca las URL.</summary>
    IReadOnlyList<string> Extremos { get; }

    /// <summary>
    /// Emite con validez legal: sólo en un proceso de producción, con el permiso <c>ElectronicInvoicing.Settings.ActivateProduction</c> y la
    /// confirmación escrita <c>PRODUCCION</c>.
    /// </summary>
    bool EsDeProduccion { get; }

    /// <summary>Nombre para la pantalla («IngenIA Conecta – pruebas»).</summary>
    string Nombre { get; }
}

/// <summary>
/// Lo que el canal recibe de la vigencia sellada además de lo de siempre (feature 028): el tenant del servicio externo y la clave del extremo.
/// Nunca una URL ni un secreto. (nuevo)
/// </summary>
/// <param name="TenantId">El tenant de la cooperativa en el servicio externo.</param>
/// <param name="Endpoint">La clave del extremo de la lista blanca.</param>
/// <param name="CanalEntregaCorreo">
/// <c>EmailDeliveryBy = Channel</c>: el canal entrega el documento al comprador. Si no, el adaptador no le pasa el correo, para que no haya dos
/// correos (plan 028 D9).
/// </param>
public sealed record ParametrosDelCanal(Guid? TenantId, string? Endpoint, bool CanalEntregaCorreo = false);

/// <summary>Las reglas de configuración de un canal con parámetros (contracts/canal-conecta.md §9). Puras. (nuevo)</summary>
public static class ReglasDeParametrosDelCanal
{
    /// <summary>Lo que hay que escribir para activar un canal de producción.</summary>
    public const string Confirmacion = "PRODUCCION";

    /// <summary>El permiso adicional para activar un canal de producción.</summary>
    public const string PermisoDeProduccion = "ElectronicInvoicing.Settings.ActivateProduction";

    /// <summary>¿El canal puede usarse en este proceso? Uno de producción sólo en un proceso de producción.</summary>
    public static bool AdmitidoEnEsteProceso(IDeclaraParametrosDelCanal canal, bool procesoDePruebas) => !canal.EsDeProduccion || !procesoDePruebas;

    /// <summary>¿El ambiente es el que exige el canal?</summary>
    public static bool AmbienteAdmitido(IDeclaraParametrosDelCanal canal, DianEnvironment ambiente) => canal.AmbienteExigido == ambiente;

    /// <summary>¿El extremo está en la lista blanca del canal? (sin distinguir mayúsculas)</summary>
    public static bool ExtremoAdmitido(IDeclaraParametrosDelCanal canal, string? extremo) =>
        !string.IsNullOrWhiteSpace(extremo) && canal.Extremos.Contains(extremo.Trim(), StringComparer.OrdinalIgnoreCase);
}
