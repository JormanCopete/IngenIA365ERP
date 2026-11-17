using IngenIA365ERP.Domain.Common.Parametros;
using static IngenIA365ERP.Domain.Common.Parametros.CatalogoDeParametros;
using static IngenIA365ERP.Domain.Common.Parametros.EntregaDelComercio;

namespace IngenIA365ERP.Domain.Common.Correo;

/// <summary>
/// Catálogo cerrado de las claves <c>CORREO</c> (pedido del dueño del 2026-11-17, COOFLOPAL haciendo facturas, compras y cotizaciones
/// de prueba en producción): el <b>modo de prueba del correo</b> de cada cooperativa. Con <see cref="EnvioATerceros"/> en
/// <see cref="Suspender"/> ningún correo de la cooperativa sale; en <see cref="Redirigir"/> todos van a <see cref="BuzonDePrueba"/>
/// en lugar del destinatario real. Lo aplica un solo sitio, <c>CorreoSegunModoDeEnvio</c>, sobre el remitente de la plataforma: los
/// correos que salen sin cooperativa (entrar, recuperar la clave, el segundo factor) y los marcados <c>SiempreAlDestinatario</c>
/// (invitaciones y avisos de identidad) no se tocan nunca. (nuevo)
/// </summary>
public static class ParametrosDeCorreo
{
    public const string Modulo = "CORREO";

    public const string EnvioATerceros = "Correo.EnvioATerceros";
    public const string BuzonDePrueba = "Correo.BuzonDePrueba";
    public const string SuspendidosQuedanPendientes = "Correo.SuspendidosQuedanPendientes";

    public const string Enviar = "Enviar";
    public const string Suspender = "Suspender";
    public const string Redirigir = "Redirigir";

    /// <summary>Una o varias direcciones separadas por coma o punto y coma.</summary>
    public const string PatronDeBuzones = @"^\s*[^@\s,;]+@[^@\s,;]+\.[^@\s,;]+(\s*[,;]\s*[^@\s,;]+@[^@\s,;]+\.[^@\s,;]+)*\s*$";

    public static IReadOnlyList<DefinicionDeParametro> Definiciones { get; } =
    [
        Eleccion(Modulo, EnvioATerceros,
            "Envío de correos a clientes, proveedores, asociados y usuarios: Enviar (normal), Suspender (no sale ninguno) o Redirigir (todos van al buzón de prueba). Las invitaciones y los correos para entrar al sistema siempre salen.",
            [Enviar, Suspender, Redirigir], Enviar, I1),
        new DefinicionDeParametro
        {
            Modulo = Modulo, Clave = BuzonDePrueba,
            Descripcion = "Buzón de prueba: una o varias direcciones separadas por coma o punto y coma, adonde llegan los correos con el envío en Redirigir.",
            Tipo = TipoDeParametro.Text, DefectoSeguro = string.Empty, AdmiteVacio = true, Patron = PatronDeBuzones, DisponibleDesde = I1,
        },
        SiNo(Modulo, SuspendidosQuedanPendientes,
            "Con el envío en Suspender, los correos no enviados quedan pendientes para reenviarlos después (Sí) o se descartan dejando constancia (No).",
            false, I1),
    ];

    /// <summary>Las direcciones del buzón de prueba, sin vacías ni repetidas.</summary>
    public static IReadOnlyList<string> Buzones(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
