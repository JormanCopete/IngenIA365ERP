using System.Security.Claims;
using FluentAssertions;
using IngenIA365ERP.API.Filters;
using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// Atajo del administrador maestro en <see cref="PermissionAuthorizationFilter"/>.
///
/// <para>
/// Por qué existe: el emisor de identidad central no pone claims <c>perm</c> en
/// el token, así que el maestro veía 404 en todo endpoint con permiso. Podía
/// registrar una cooperativa y luego no encontrarla en la lista.
/// </para>
///
/// <para>
/// La prueba que de verdad importa es <see cref="MaestroConTokenAMedioAutenticar_NoPasa"/>.
/// El atajo se apoya en <c>purpose=full</c>: si alguien lo relaja para que
/// acepte cualquier token del maestro, un token de MFA pendiente —el que se
/// entrega ANTES del segundo factor— abriría los 47 endpoints protegidos. Si
/// esa prueba se pone en verde aflojando la condición, el atajo deja de ser un
/// atajo y pasa a ser un agujero.
/// </para>
/// </summary>
public class PermissionFilterMasterAdminTests
{
    private const string Permiso = "Admin.Tenants.View";
    private static readonly object Continuo = new();

    private static ClaimsPrincipal Principal(params (string Tipo, string Valor)[] claims) =>
        new(new ClaimsIdentity(
            claims.Select(c => new Claim(c.Tipo, c.Valor)),
            authenticationType: "Test"));

    /// <summary>Devuelve el objeto centinela si el filtro dejó pasar, o el IResult del rechazo.</summary>
    private static async Task<object?> EjecutarAsync(ClaimsPrincipal usuario)
    {
        var http = new DefaultHttpContext { User = usuario };
        http.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequirePermissionAttribute(Permiso)),
            "prueba"));

        var contexto = EndpointFilterInvocationContext.Create(http);
        return await new PermissionAuthorizationFilter()
            .InvokeAsync(contexto, _ => ValueTask.FromResult<object?>(Continuo));
    }

    private static void DebeSer404(object? resultado)
    {
        resultado.Should().NotBeSameAs(Continuo, "el filtro no debió dejar pasar la petición");
        resultado.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound,
                "FR-017: la falta de permiso es indistinguible de un endpoint inexistente");
    }

    [Fact]
    public async Task MaestroSinClaimsPerm_Pasa()
    {
        // El caso real: el token del maestro NO trae ni un solo `perm`.
        var r = await EjecutarAsync(Principal(
            ("purpose", "full"), ("is_global_master_admin", "true")));

        r.Should().BeSameAs(Continuo);
    }

    [Fact]
    public async Task MaestroConTokenAMedioAutenticar_NoPasa()
    {
        // purpose distinto de "full" = segundo factor pendiente. El atajo NO aplica.
        var r = await EjecutarAsync(Principal(
            ("purpose", "mfa_pending"), ("is_global_master_admin", "true")));

        DebeSer404(r);
    }

    [Fact]
    public async Task NoMaestroSinPermiso_NoPasa()
    {
        var r = await EjecutarAsync(Principal(
            ("purpose", "full"), ("is_global_master_admin", "false")));

        DebeSer404(r);
    }

    [Fact]
    public async Task NoMaestroConElPermisoExacto_Pasa()
    {
        // El camino normal sigue funcionando: el atajo no lo sustituye.
        var r = await EjecutarAsync(Principal(
            ("purpose", "full"), ("is_global_master_admin", "false"), ("perm", Permiso)));

        r.Should().BeSameAs(Continuo);
    }

    [Fact]
    public async Task NoMaestroConOtroPermiso_NoPasa()
    {
        var r = await EjecutarAsync(Principal(
            ("purpose", "full"), ("is_global_master_admin", "false"), ("perm", "Admin.Users.View")));

        DebeSer404(r);
    }

    [Fact]
    public async Task Anonimo_NoPasa()
    {
        // Sin identidad autenticada no hay atajo posible.
        var r = await EjecutarAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        DebeSer404(r);
    }

    [Theory]
    [InlineData("True")]
    [InlineData("TRUE")]
    public async Task ElValorDelClaimSeComparaSinDistinguirMayusculas(string valor)
    {
        // ClaimValueTypes.Boolean no obliga a un formato; el emisor podría cambiarlo.
        var r = await EjecutarAsync(Principal(
            ("purpose", "full"), ("is_global_master_admin", valor)));

        r.Should().BeSameAs(Continuo);
    }
}
