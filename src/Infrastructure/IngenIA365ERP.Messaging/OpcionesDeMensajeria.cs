namespace IngenIA365ERP.Messaging;

/// <summary>
/// La sección <c>Mensajeria</c> (feature 023; contracts/mensajeria.md §6). Valores técnicos: usuario y clave llegan sólo por el Secret
/// <c>erp-rabbitmq</c> (variables <c>Mensajeria__Usuario</c> y <c>Mensajeria__Clave</c>), nunca por appsettings del repositorio.
/// </summary>
public sealed class OpcionesDeMensajeria
{
    public const string Seccion = "Mensajeria";

    public const string ProveedorEnProceso = "EnProceso";
    public const string ProveedorRabbitMq = "RabbitMq";

    /// <summary><see cref="ProveedorEnProceso"/> (defecto) o <see cref="ProveedorRabbitMq"/>.</summary>
    public string Proveedor { get; set; } = ProveedorEnProceso;

    public string Host { get; set; } = string.Empty;

    public int Puerto { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/erp";

    public string Usuario { get; set; } = string.Empty;

    public string Clave { get; set; } = string.Empty;

    /// <summary>Mensajes sin confirmar por canal de consumo (<c>basic.qos</c>).</summary>
    public ushort Prefetch { get; set; } = 8;

    /// <summary>Manejadores simultáneos por cola y por réplica.</summary>
    public ushort ConcurrenciaPorCola { get; set; } = 4;

    /// <summary>Cuánto se espera la confirmación del broker antes de dar la publicación por fallida.</summary>
    public int ConfirmacionSegundos { get; set; } = 5;

    /// <summary>El barrido vuelve a avisar lo que lleva más que esto en la cola sin atenderse.</summary>
    public int RepublicarTrasSegundos { get; set; } = 120;

    /// <summary>Un aviso de integración por cooperativa y destino como máximo cada tanto, por réplica.</summary>
    public int AvisoIntegracionMinimoMs { get; set; } = 250;

    /// <summary>Falso: la réplica sólo publica (pruebas de «dos réplicas» y diagnóstico).</summary>
    public bool Consumir { get; set; } = true;

    /// <summary>Entregas antes de apartar un aviso a <c>erp.muertos</c> (<c>x-delivery-limit</c>).</summary>
    public int LimiteDeEntregas { get; set; } = 5;

    public bool EsRabbitMq => string.Equals(Proveedor, ProveedorRabbitMq, StringComparison.OrdinalIgnoreCase);

    /// <summary>Lo que impide arrancar; vacío si todo está bien.</summary>
    public IReadOnlyList<string> Problemas()
    {
        var p = new List<string>();
        if (!EsRabbitMq && !string.Equals(Proveedor, ProveedorEnProceso, StringComparison.OrdinalIgnoreCase))
            p.Add($"Mensajeria:Proveedor «{Proveedor}» no existe; admite {ProveedorEnProceso} o {ProveedorRabbitMq}.");
        if (EsRabbitMq)
        {
            if (string.IsNullOrWhiteSpace(Host)) p.Add("Mensajeria:Host es obligatorio con RabbitMq.");
            if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Clave))
                p.Add("Mensajeria:Usuario y Mensajeria:Clave son obligatorios con RabbitMq (vienen del Secret erp-rabbitmq).");
            if (Puerto is <= 0 or > 65535) p.Add("Mensajeria:Puerto fuera de rango.");
        }
        if (Prefetch == 0) p.Add("Mensajeria:Prefetch debe ser mayor que cero.");
        if (ConcurrenciaPorCola == 0) p.Add("Mensajeria:ConcurrenciaPorCola debe ser mayor que cero.");
        if (ConfirmacionSegundos <= 0) p.Add("Mensajeria:ConfirmacionSegundos debe ser mayor que cero.");
        if (RepublicarTrasSegundos < 10) p.Add("Mensajeria:RepublicarTrasSegundos debe ser al menos 10.");
        if (LimiteDeEntregas < 1) p.Add("Mensajeria:LimiteDeEntregas debe ser al menos 1.");
        return p;
    }
}
