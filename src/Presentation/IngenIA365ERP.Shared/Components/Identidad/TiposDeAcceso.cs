namespace IngenIA365ERP.Shared.Components.Identidad;

/// <summary>
/// Feature 017: los tipos de documento que sirven para entrar, en el orden del catálogo del dominio
/// (<c>TiposDeDocumento.DeAcceso</c>). El cliente no referencia el dominio; si el catálogo cambia, cambia
/// aquí también. Qué tipos acepta cada cooperativa lo decide el servidor.
/// </summary>
public static class TiposDeAcceso
{
    public sealed record Tipo(string Codigo, string Nombre);

    public static readonly IReadOnlyList<Tipo> Todos =
    [
        new("CC", "Cédula de ciudadanía"),
        new("CE", "Cédula de extranjería"),
        new("PPT", "Permiso por protección temporal"),
        new("PEP", "Permiso especial de permanencia"),
        new("TI", "Tarjeta de identidad"),
        new("PA", "Pasaporte"),
    ];
}
