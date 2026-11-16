using FluentAssertions;
using IngenIA365ERP.Shared.Pages.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// 2026-11-15: el botón «Reintentar…» de la pestaña «Corridas del proceso» (<c>/cartera/bandeja-de-mensajes</c>) aparece en las corridas que el
/// servidor deja reintentar (<c>RetryLendingProcessRunCommand</c>: <c>Failed</c> o <c>CompletedWithErrors</c>) y en ninguna otra. Hasta ese día
/// reintentar sólo se podía por la API, y el ciclo del 13-nov de COOFLOPAL quedó detenido sin forma de reanudarlo desde el aplicativo. (nuevo)
/// </summary>
public class ReintentoDeCorridaTests
{
    [Theory]
    [InlineData("Failed", true)]
    [InlineData("CompletedWithErrors", true)]
    [InlineData("Requested", false)]
    [InlineData("Running", false)]
    [InlineData("Completed", false)]
    [InlineData(null, false)]
    public void Se_reintenta_sola_la_corrida_que_el_servidor_admite(string? estado, bool esperado) =>
        BandejaDeMensajes.SeReintenta(estado).Should().Be(esperado);
}
