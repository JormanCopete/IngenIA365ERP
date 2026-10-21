using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, US2 (bloque 6C; contracts/api.md §1.2, §1.14): <c>ErrorEnvelopeFilter</c> infiere el estado por el código, y la regla general
/// «termina en <c>.Conflict</c> → 409» atrapaba <c>Lending.Segregation.Conflict</c>, que el contrato publica como 422 (una regla de negocio:
/// la persona ya intervino en el expediente). La e2e <c>OriginacionConAtribucionesTests</c> lo destapó. Esta regla fija que la excepción
/// exista y vaya antes de la general (el <c>switch</c> toma la primera que casa). Es sobre el fuente porque este proyecto no referencia la API.
/// (nuevo)
/// </summary>
public class LosErroresDeCarteraSalenConSuEstado
{
    [Fact]
    public void La_segregacion_de_Cartera_sale_422_y_no_409()
    {
        var fuente = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Filters",
            "ErrorEnvelopeFilter.cs"));
        var excepcion = fuente.IndexOf("\"Lending.Segregation.Conflict\" => StatusCodes.Status422UnprocessableEntity", StringComparison.Ordinal);
        var general = fuente.IndexOf("code.EndsWith(\".Conflict\"", StringComparison.Ordinal);

        Assert.True(excepcion >= 0, "ErrorEnvelopeFilter no saca Lending.Segregation.Conflict como 422 (api.md §1.14).");
        Assert.True(general < 0 || excepcion < general, "La excepción de la segregación va antes de la regla general «.Conflict» → 409.");
    }
}
