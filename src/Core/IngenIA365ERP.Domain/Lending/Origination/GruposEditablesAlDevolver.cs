namespace IngenIA365ERP.Domain.Lending.Origination;

/// <summary>
/// Los grupos de datos de una solicitud de crédito que se pueden corregir cuando se devuelve para corregir (pedido del dueño del
/// 2026-11-23). Cada cooperativa —o cada línea— elige cuáles con el parámetro <c>Originacion.GruposEditablesAlDevolver</c>; lo sensible
/// se puede dejar fijo con el parámetro; de fábrica todo se corrige. El solicitante nunca cambia: para otra persona, otra solicitud. (nuevo)
/// </summary>
[Flags]
public enum GrupoEditableAlDevolver
{
    Ninguno = 0,

    /// <summary>Monto, cuotas, periodicidad, sistema, fechas, gracia, cuotas extraordinarias y la marca de vivienda de interés social.</summary>
    Condiciones = 1,

    /// <summary>La línea de crédito.</summary>
    Linea = 2,

    /// <summary>La oficina que radica.</summary>
    Oficina = 4,

    /// <summary>El destino del crédito.</summary>
    Destino = 8,

    /// <summary>La forma de pago y, en la libranza, el convenio y la identificación del empleado.</summary>
    FormaDePago = 16,

    /// <summary>Los seguros elegidos.</summary>
    Seguros = 32,

    /// <summary>Ingresos, gastos y cuotas externas que declara el solicitante.</summary>
    Capacidad = 64,

    /// <summary>Codeudores y avalistas, con su capacidad.</summary>
    Codeudores = 128,

    /// <summary>Los créditos propios que la solicitud recoge.</summary>
    Recogidas = 256,

    /// <summary>La excepción a la política y su motivo.</summary>
    ExcepcionDePolitica = 512,

    /// <summary>Qué prefiere el deudor al abonar a capital.</summary>
    PreferenciaDeAbono = 1024,

    /// <summary>Adjuntar y quitar documentos.</summary>
    Documentos = 2048,

    /// <summary>Registrar, cambiar o quitar garantías.</summary>
    Garantias = 4096,

    Todos = Condiciones | Linea | Oficina | Destino | FormaDePago | Seguros | Capacidad | Codeudores | Recogidas | ExcepcionDePolitica
        | PreferenciaDeAbono | Documentos | Garantias,
}

/// <summary>
/// El texto del parámetro <c>Originacion.GruposEditablesAlDevolver</c>: los nombres de <see cref="GrupoEditableAlDevolver"/> separados por
/// coma (sin <c>Ninguno</c> ni <c>Todos</c>), en cualquier orden. Mismo molde que el saldo del certificado tributario. (nuevo)
/// </summary>
public static class GruposEditablesAlDevolver
{
    /// <summary>Los nombres admitidos, en el orden en que se muestran.</summary>
    public static IReadOnlyList<string> Nombres { get; } =
    [
        nameof(GrupoEditableAlDevolver.Condiciones), nameof(GrupoEditableAlDevolver.Linea), nameof(GrupoEditableAlDevolver.Oficina),
        nameof(GrupoEditableAlDevolver.Destino), nameof(GrupoEditableAlDevolver.FormaDePago), nameof(GrupoEditableAlDevolver.Seguros),
        nameof(GrupoEditableAlDevolver.Capacidad), nameof(GrupoEditableAlDevolver.Codeudores), nameof(GrupoEditableAlDevolver.Recogidas),
        nameof(GrupoEditableAlDevolver.ExcepcionDePolitica), nameof(GrupoEditableAlDevolver.PreferenciaDeAbono),
        nameof(GrupoEditableAlDevolver.Documentos), nameof(GrupoEditableAlDevolver.Garantias),
    ];

    /// <summary>
    /// El defecto: todo (pedido del dueño del 2026-11-24, «que puedan digitar todos los campos al modificar la solicitud»). Hasta ese día
    /// dejaba fijas la línea y la oficina; quien las quiera fijas lo dice en el parámetro.
    /// </summary>
    public static GrupoEditableAlDevolver PorDefecto { get; } = GrupoEditableAlDevolver.Todos;

    public static string Defecto { get; } = Escribir(PorDefecto);

    /// <summary>La forma del texto: uno o más nombres admitidos separados por coma, sin espacios.</summary>
    public static string Patron { get; } = $"^({string.Join("|", Nombres)})(,({string.Join("|", Nombres)}))*$";

    /// <summary>Lee el texto; vacío o con un nombre desconocido, nulo (quien lee usa el defecto).</summary>
    public static GrupoEditableAlDevolver? Leer(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var resultado = GrupoEditableAlDevolver.Ninguno;
        foreach (var parte in texto.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var nombre = Nombres.FirstOrDefault(n => string.Equals(n, parte, StringComparison.OrdinalIgnoreCase));
            if (nombre is null) return null;
            resultado |= Enum.Parse<GrupoEditableAlDevolver>(nombre);
        }
        return resultado == GrupoEditableAlDevolver.Ninguno ? null : resultado;
    }

    /// <summary>Los grupos como texto del parámetro, en el orden de <see cref="Nombres"/>.</summary>
    public static string Escribir(GrupoEditableAlDevolver grupos) =>
        string.Join(",", Nombres.Where(n => grupos.HasFlag(Enum.Parse<GrupoEditableAlDevolver>(n))));

    /// <summary>Cada grupo encendido, en el orden de <see cref="Nombres"/>.</summary>
    public static IReadOnlyList<GrupoEditableAlDevolver> Separar(GrupoEditableAlDevolver grupos) =>
        [.. Nombres.Select(Enum.Parse<GrupoEditableAlDevolver>).Where(g => grupos.HasFlag(g))];

    /// <summary>El nombre que ve la persona.</summary>
    public static string Texto(GrupoEditableAlDevolver grupo) => grupo switch
    {
        GrupoEditableAlDevolver.Condiciones => "Condiciones (monto, cuotas, fechas)",
        GrupoEditableAlDevolver.Linea => "Línea de crédito",
        GrupoEditableAlDevolver.Oficina => "Oficina",
        GrupoEditableAlDevolver.Destino => "Destino",
        GrupoEditableAlDevolver.FormaDePago => "Forma de pago",
        GrupoEditableAlDevolver.Seguros => "Seguros",
        GrupoEditableAlDevolver.Capacidad => "Ingresos y gastos del solicitante",
        GrupoEditableAlDevolver.Codeudores => "Codeudores",
        GrupoEditableAlDevolver.Recogidas => "Créditos que recoge",
        GrupoEditableAlDevolver.ExcepcionDePolitica => "Excepción a la política",
        GrupoEditableAlDevolver.PreferenciaDeAbono => "Preferencia de abono a capital",
        GrupoEditableAlDevolver.Documentos => "Documentos",
        GrupoEditableAlDevolver.Garantias => "Garantías",
        _ => grupo.ToString(),
    };
}
