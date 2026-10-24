using System.Text.Json;
using System.Text.Json.Nodes;

namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// La huella de un ítem de los contratos de Cartera y Captaciones en su versión 2 (feature 015, R1; E-01 a, E-02 a; contratos-v2.md §1.2, §3.1). La v2 suma la
/// partida bancaria citada (<c>BankItem</c>) a los DTOs de entrada; un ítem que no la cita tiene que medirse exactamente como en la v1 para que una clave de
/// antes de la v2 conserve su huella (y repetirla devuelva el comprobante original). (nuevo)
/// </summary>
public static class HuellasDeLaVersion2
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// El ítem como JSON, sin la clave <c>bankItem</c> cuando <paramref name="sinPartida"/> (la v1). Con partida, el ítem completo: la partida entra en la huella.
    /// </summary>
    public static JsonNode SinPartidaNula<T>(T item, bool sinPartida)
    {
        var nodo = JsonSerializer.SerializeToNode(item, Json) ?? throw new InvalidOperationException("El ítem del contrato no se pudo serializar.");
        if (sinPartida && nodo is JsonObject objeto) objeto.Remove("bankItem");
        return nodo;
    }
}
