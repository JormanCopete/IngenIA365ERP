using System.Security.Claims;
using FluentAssertions;
using IngenIA365ERP.API.Filters;
using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// El segundo permiso de los informes contables (feature 009 E2, contracts/api.md §8): una sola
/// ruta sirve la pantalla y la descarga, así que <c>Accounting.Reports.Export</c> sólo se exige
/// cuando <c>format</c> pide un archivo. No levanta contenedores: ejecuta el filtro a mano, como
/// <see cref="PermissionFilterMasterAdminTests"/>.
/// </summary>
public class PermisoDeExportacionFilterTests
{
    private const string Exportar = "Accounting.Reports.Export";
    private static readonly object Continuo = new();

    private static ClaimsPrincipal Principal(params (string Tipo, string Valor)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Tipo, c.Valor)), authenticationType: "Test"));

    private static async Task<object?> EjecutarAsync(ClaimsPrincipal usuario, string queryString)
    {
        var http = new DefaultHttpContext { User = usuario };
        http.Request.QueryString = new QueryString(queryString);
        http.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequirePermissionWhenExportingAttribute(Exportar)),
            "prueba"));

        var contexto = EndpointFilterInvocationContext.Create(http);
        return await new PermisoDeExportacionFilter().InvokeAsync(contexto, _ => ValueTask.FromResult<object?>(Continuo));
    }

    private static void DebeSer404(object? resultado)
    {
        resultado.Should().NotBeSameAs(Continuo, "el filtro no debió dejar pasar la petición");
        resultado.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "FR-017: la falta de permiso es indistinguible de un endpoint inexistente");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-01-01")]
    [InlineData("?format=")]
    [InlineData("?format=json")]
    [InlineData("?format=JSON")]
    public async Task SinArchivo_NoMiraElPermisoDeExportar(string query)
    {
        // Quien sólo puede ver sigue viendo: el permiso de ver lo pone el otro filtro.
        var r = await EjecutarAsync(Principal(("purpose", "full"), ("is_global_master_admin", "false")), query);

        r.Should().BeSameAs(Continuo);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("pdf")]
    [InlineData("docx")]
    public async Task ConArchivoYSinPermiso_NoPasa(string formato)
    {
        var r = await EjecutarAsync(Principal(("purpose", "full"), ("is_global_master_admin", "false")), $"?format={formato}");

        DebeSer404(r);
    }

    [Fact]
    public async Task ConArchivoYElPermiso_Pasa()
    {
        var r = await EjecutarAsync(Principal(("purpose", "full"), ("perm", Exportar)), "?format=xlsx");

        r.Should().BeSameAs(Continuo);
    }

    [Fact]
    public async Task ElMaestroExportaSinClaims()
    {
        // El mismo atajo que en PermissionAuthorizationFilter: el token central no lleva `perm`.
        var r = await EjecutarAsync(Principal(("purpose", "full"), ("is_global_master_admin", "true")), "?format=pdf");

        r.Should().BeSameAs(Continuo);
    }

    [Fact]
    public async Task UnFormatoDesconocidoTambienEsExportacion()
    {
        // «csv» no es un formato válido, pero tampoco es la pantalla: sin permiso de exportar
        // se responde 404 antes de que la entrega diga 400. No se revela ni eso.
        var r = await EjecutarAsync(Principal(("purpose", "full")), "?format=csv");

        DebeSer404(r);
    }
}
