using System.Text.Json;

namespace IngenIA365ERP.Domain.Lending.Collections;

/// <summary>
/// Las acciones sugeridas de una etapa de cobranza (<c>LND_CollectionStages.SuggestedActionsJson</c>, data-model §26.4; FR-117): una lista cerrada de
/// códigos que la pantalla muestra y el ciclo diario mira. <see cref="AvisoPrevio"/> es la acción «aviso previo» que abre el aviso del episodio al
/// entrar a la etapa (<c>adaptadores-y-archivos.md</c> §5.2: «desde K4 lo crea la acción aviso previo de la etapa configurada»). Las plantillas de la
/// etapa (<c>TemplateCodesJson</c>) son códigos de <c>COR_NotificationTemplates</c> de propósito cobranza. (nuevo)
/// </summary>
public static class AccionesDeEtapa
{
    public const string Llamar = "Call";
    public const string Mensaje = "Message";
    public const string Visita = "Visit";
    public const string AvisoPrevio = "PriorNotice";
    public const string AcuerdoDePago = "PaymentAgreement";
    public const string EntregaAAbogado = "LegalHandover";

    public static IReadOnlyList<string> Todas { get; } = [Llamar, Mensaje, Visita, AvisoPrevio, AcuerdoDePago, EntregaAAbogado];

    /// <summary>La lista de códigos de un JSON <c>["…"]</c>; vacía si no hay o no se puede leer.</summary>
    public static IReadOnlyList<string> Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json) ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>El JSON de una lista de códigos, o nulo si está vacía.</summary>
    public static string? Escribir(IEnumerable<string>? codigos)
    {
        var lista = (codigos ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.Ordinal).ToList();
        return lista.Count == 0 ? null : JsonSerializer.Serialize(lista);
    }
}
