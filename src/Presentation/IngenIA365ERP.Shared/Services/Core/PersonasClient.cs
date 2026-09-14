using System.Net.Http.Headers;
using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>
/// Cliente tipado del maestro de personas (feature 008), con el mismo patrón que
/// <see cref="Nomina.NominaClient"/>: adjunta el token desde <see cref="CentralAuthClient"/> y
/// devuelve <see cref="InvitationApiResult{T}"/> con el código de error del envelope, que es
/// lo que las pantallas miran para ofrecer «Usar esa persona» o «Restaurar persona».
/// Lo usan Personas, Empleados y Asociados: una sola forma de hablar con la API de personas.
/// </summary>
public sealed class PersonasClient(HttpClient http, CentralAuthClient auth)
{
    public Task<InvitationApiResult<PersonaDto>> ObtenerAsync(Guid publicId, CancellationToken ct = default) =>
        EnviarAsync<PersonaDto>(HttpMethod.Get, $"/api/core/people/{publicId}", null, ct);

    public Task<InvitationApiResult<Guid>> CrearAsync(PersonaEntradaDto persona, CancellationToken ct = default) =>
        EnviarAsync<Guid>(HttpMethod.Post, "/api/core/people", persona, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActualizarAsync(Guid publicId, PersonaEntradaDto persona, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"/api/core/people/{publicId}", persona, ct);

    /// <summary>
    /// Busca por documento, nombre, razón social o código heredado. <paramref name="rol"/>
    /// filtra por bandera: associate, employee, salesperson, customer, supplier, advisor,
    /// thirdparty. Nunca devuelve eliminadas.
    /// </summary>
    public Task<InvitationApiResult<IReadOnlyList<PersonaBusquedaDto>>> BuscarAsync(string termino, string? rol = null, CancellationToken ct = default)
    {
        var url = $"/api/core/people/search?q={Uri.EscapeDataString(termino)}";
        if (!string.IsNullOrWhiteSpace(rol)) url += $"&rol={Uri.EscapeDataString(rol)}";
        return EnviarAsync<IReadOnlyList<PersonaBusquedaDto>>(HttpMethod.Get, url, null, ct);
    }

    /// <summary>La persona dueña de un documento, eliminadas incluidas; 404 si nadie lo tiene.</summary>
    public Task<InvitationApiResult<PersonaPorDocumentoDto>> PorDocumentoAsync(string taxId, CancellationToken ct = default) =>
        EnviarAsync<PersonaPorDocumentoDto>(HttpMethod.Get, $"/api/core/people/by-document?taxId={Uri.EscapeDataString(taxId)}", null, ct);

    /// <summary>Reactiva una persona eliminada (misma fila). Exige el permiso de eliminar personas.</summary>
    public Task<InvitationApiResult<EmptyResponse>> RestaurarAsync(Guid publicId, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"/api/core/people/{publicId}/restore", null, ct);

    /// <summary>
    /// Las ciudades del desplegable del formulario. Va por el <c>HttpClient</c> «api» (que ya lleva el
    /// bearer) porque el listado viene paginado y <see cref="HttpClientListExtensions.GetListAsync{T}"/>
    /// sabe leer esa forma. Lanza si la API falla: el que llama avisa (Principio IX).
    /// </summary>
    public async Task<IReadOnlyList<OpcionDeCiudad>> ListarCiudadesAsync(CancellationToken ct = default)
    {
        var crudas = await http.GetListAsync<CiudadCruda>("/api/core/cities?PageNumber=1&PageSize=2000", ct);
        return crudas.Select(c => new OpcionDeCiudad(c.PublicId, c.Name)).ToList();
    }

    private sealed record CiudadCruda(Guid PublicId, string Name);

    /// <summary>Persona nueva y empleado en una sola operación: los dos quedan o ninguno.</summary>
    public Task<InvitationApiResult<AltaEmpleadoConPersonaResultado>> RegistrarEmpleadoConPersonaAsync(AltaEmpleadoConPersonaRequest request, CancellationToken ct = default) =>
        EnviarAsync<AltaEmpleadoConPersonaResultado>(HttpMethod.Post, "/api/payroll/employees/with-person", request, ct);

    /// <summary>Persona nueva y asociado en una sola operación: los dos quedan o ninguno.</summary>
    public Task<InvitationApiResult<AltaAsociadoConPersonaResultado>> RegistrarAsociadoConPersonaAsync(AltaAsociadoConPersonaRequest request, CancellationToken ct = default) =>
        EnviarAsync<AltaAsociadoConPersonaResultado>(HttpMethod.Post, "/api/core/associates/with-person", request, ct);

    private async Task<InvitationApiResult<T>> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo, CancellationToken ct)
    {
        var token = auth.CurrentAccessToken;
        if (token is null)
            return InvitationApiResult<T>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);

        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<T>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<T>.NetworkError(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return InvitationApiResult<T>.Failure("Generic.RespuestaInesperada",
                $"El servidor respondió con un formato inesperado: {ex.Message}", 0);
        }
    }
}
