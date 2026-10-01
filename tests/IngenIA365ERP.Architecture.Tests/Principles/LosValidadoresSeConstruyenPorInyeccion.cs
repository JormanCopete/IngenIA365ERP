using FluentValidation;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// <c>AddValidatorsFromAssembly</c> y Carter registran todo validador de Application —Carter también los internos y los anidados—
/// y el contenedor valida sus constructores al arrancar: un validador que pide un <c>bool</c>, un número o un texto tumba la API
/// entera («Unable to resolve service for type 'System.Boolean'»). Pasó el 2026-09-27 con <c>PaymentMeansInputValidator(bool
/// conCodigo)</c>, y ni una sola prueba sin contenedores lo vio: sólo las e2e, que no arrancaban. Unas reglas parametrizadas por
/// quien las usa van en un método estático que se aplica con <c>ChildRules</c> (<c>ReglasDeFormaDelMedio</c>).
/// </summary>
public class LosValidadoresSeConstruyenPorInyeccion
{
    [Fact]
    public void Ningun_validador_pide_un_valor_que_el_contenedor_no_puede_dar()
    {
        var ensamblado = typeof(Application.DependencyInjection).Assembly;
        var malos = ensamblado.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && typeof(IValidator).IsAssignableFrom(t))
            .Where(t => t.GetConstructors().All(c => c.GetParameters().Any(p =>
                p.ParameterType.IsPrimitive || p.ParameterType.IsEnum || p.ParameterType == typeof(string) || p.ParameterType == typeof(decimal))))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(malos.Count == 0,
            "El escaneo de validadores los registra y el contenedor no sabe construirlos; háganlos internal: " + string.Join(", ", malos));
    }
}
