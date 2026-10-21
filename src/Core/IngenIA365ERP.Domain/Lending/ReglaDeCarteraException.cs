namespace IngenIA365ERP.Domain.Lending;

/// <summary>
/// Una regla de Cartera que un dato o una transición no cumple (feature 013, T0280–T0283, T0305): el código del contrato
/// (<c>Área.Recurso.Motivo</c>, api.md §1.2), un mensaje en español que dice qué hacer y los datos para corregir (camelCase). Lo
/// devuelven los <c>Validar…</c> de las entidades de Cartera para que el comando responda su 422 sin excepciones. (nuevo)
/// </summary>
public sealed record ProblemaDeCartera(string Codigo, string Mensaje, IReadOnlyDictionary<string, object?>? Datos = null)
{
    /// <summary>Un problema con un solo dato.</summary>
    public static ProblemaDeCartera Con(string codigo, string mensaje, string dato, object? valor) =>
        new(codigo, mensaje, new Dictionary<string, object?> { [dato] = valor });
}

/// <summary>
/// La excepción que lanzan las fábricas y transiciones de las entidades de Cartera cuando se las llama sin haber validado antes
/// (feature 013, T0280–T0283, T0305). Es un error de programación, como <c>InvalidDocumentTransitionException</c> de la 012: los
/// comandos preguntan primero (<c>Validar…</c>, <c>Puede…</c>) y responden el código del <see cref="Problema"/>; la excepción es la
/// segunda defensa para que un hecho inválido nunca llegue a la base. (nuevo)
/// </summary>
public sealed class ReglaDeCarteraException(ProblemaDeCartera problema) : InvalidOperationException($"{problema.Codigo}: {problema.Mensaje}")
{
    public ProblemaDeCartera Problema { get; } = problema;

    public string Codigo => Problema.Codigo;

    /// <summary>Lanza si hay problema.</summary>
    public static void SiHay(ProblemaDeCartera? problema)
    {
        if (problema is not null) throw new ReglaDeCarteraException(problema);
    }
}
