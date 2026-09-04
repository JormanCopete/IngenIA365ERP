using FluentAssertions;
using IngenIA365ERP.API.Middleware;

namespace IngenIA365ERP.API.IntegrationTests.Middleware;

/// <summary>
/// La lista de exención de <see cref="TenantResolutionMiddleware"/> es una
/// trampa silenciosa: marcar un endpoint <c>AllowAnonymous</c> NO basta, porque
/// el middleware corta con 401 <c>Session.TenantNotSelected</c> antes de que la
/// petición llegue al endpoint.
///
/// <para>
/// Pasó de verdad: el endpoint que alimenta el panel promocional del login
/// estaba marcado AllowAnonymous y devolvía 401. Sólo se detectó al levantar la
/// pila completa; ninguna prueba lo cubría, y la verificación en navegador se
/// había hecho con la respuesta simulada.
/// </para>
/// </summary>
public class TenantResolutionExencionTests
{
    [Theory]
    // Contenido servido ANTES de iniciar sesión: por definición no hay empresa.
    [InlineData("/api/publico/promociones")]
    [InlineData("/api/publico/promociones/8c2a1f00-0000-0000-0000-000000000001/imagen")]
    // Flujo de entrada: el token todavía no lleva empresa activa.
    [InlineData("/api/auth/dev/login")]
    [InlineData("/api/sessions/select-tenant")]
    [InlineData("/api/invitations/preview")]
    // Superficie del administrador maestro, que no tiene empresa.
    [InlineData("/api/saas/register-tenant")]
    // Lo único de /api/admin que vive en la base administrativa.
    [InlineData("/api/admin/promociones")]
    [InlineData("/api/admin/branches")]
    // Infraestructura.
    [InlineData("/health/ready")]
    [InlineData("/swagger/index.html")]
    public void RutasQueDebenFuncionarSinEmpresaSeleccionada(string ruta)
    {
        TenantResolutionMiddleware.IsExempt(ruta).Should().BeTrue(
            "sin exención responde 401 Session.TenantNotSelected aunque el endpoint sea anónimo");
    }

    [Theory]
    // Lo operativo SIEMPRE exige empresa: es la garantía de aislamiento entre
    // cooperativas del principio IV, y aflojarla sería un fallo regulatorio.
    [InlineData("/api/cartera/creditos")]
    [InlineData("/api/contabilidad/comprobantes")]
    [InlineData("/api/nomina/empleados")]
    [InlineData("/api/profile/preferencias")]
    // Bajo /api/admin pero de la base DE LA COOPERATIVA. Estaban exentas por
    // el prefijo "/api/admin" entero y devolvían 500 sin cooperativa, en vez
    // del 401 tipado. Se vio en QA entrando como maestro.
    [InlineData("/api/admin/roles")]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/permissions")]
    [InlineData("/api/admin/parametros")]
    // Parecidas a una ruta exenta pero distintas: la comparación es por prefijo
    // y no debe dejar pasar un sufijo que la imite.
    [InlineData("/api/publicodemas/algo")]
    [InlineData("/api/publico")]
    public void RutasQueNoDebenQuedarExentas(string ruta)
    {
        TenantResolutionMiddleware.IsExempt(ruta).Should().BeFalse();
    }

    [Fact]
    public void LaComparacionEsSensibleAMayusculas_YElMiddlewareYaNormalizaAntes()
    {
        // IsExempt compara con StringComparison.Ordinal. Quien la llama pasa la
        // ruta en minúsculas (InvokeAsync hace ToLowerInvariant); se fija acá
        // para que nadie use IsExempt desde otro sitio sin normalizar y crea que
        // la exención aplica.
        TenantResolutionMiddleware.IsExempt("/API/PUBLICO/promociones").Should().BeFalse();
        TenantResolutionMiddleware.IsExempt("/api/publico/promociones").Should().BeTrue();
    }
}
