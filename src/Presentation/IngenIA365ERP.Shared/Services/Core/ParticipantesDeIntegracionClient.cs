using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>
/// Cliente tipado del registro de participantes de la integración (feature 013, fase 2, T0094; contracts/api.md §21.1): recaudadores del
/// contrato de obligaciones y destinos de la bandeja, con las versiones que adoptan y si cuentan hoy. Sin <c>Authorization</c> a mano; las
/// escrituras llevan <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public sealed class ParticipantesDeIntegracionClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/core/integration-participants";

    public Task<ResultadoDeInventario<IReadOnlyList<ParticipanteDeIntegracionDto>>> ListarAsync(int? clase = null, int? rol = null, string? destino = null,
        DateOnly? vigentesAl = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ParticipanteDeIntegracionDto>>(HttpMethod.Get, ConQuery(Ruta, Query(
            ("class", clase is { } c ? TextosDePlataforma.NombreDeClaseDeParticipante(c) : null),
            ("role", rol is { } r ? TextosDePlataforma.NombreDeRolDeParticipante(r) : null),
            ("destination", destino), ("activeOn", Texto(vigentesAl)))), null, null, ct);

    public Task<ResultadoDeInventario<ParticipanteDeIntegracionDto>> RegistrarAsync(RegistroDeParticipanteRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ParticipanteDeIntegracionDto>(HttpMethod.Post, Ruta, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> RetirarAsync(Guid participante, RetiroDeParticipanteRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{Ruta}/{participante}/retire", request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
