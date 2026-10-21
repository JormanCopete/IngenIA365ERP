using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Un doble del lado contable para la conciliación de Cartera (US7, T0628): cada rol que tiene «regla» va a su propio conjunto con una cuenta
/// <c>C-{rol}</c> y el saldo contable (débito − crédito) que la prueba le ponga por sucursal; los roles sin regla vuelven en
/// <c>UnresolvedCombinations</c>. Registra cada pregunta. (nuevo)
/// </summary>
internal sealed class ContabilidadDePruebaParaConciliar
{
    private readonly Dictionary<string, Dictionary<Guid, decimal>> _saldos = new(StringComparer.Ordinal);

    public ContabilidadDePruebaParaConciliar(IContabilidadParaCartera puerto, params string[] rolesConRegla)
    {
        foreach (var rol in rolesConRegla) _saldos[rol] = [];
        puerto.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<IReadOnlyList<CombinacionDeSaldo>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Responder(ci.Arg<IReadOnlyList<CombinacionDeSaldo>>())));
    }

    public List<IReadOnlyList<CombinacionDeSaldo>> Preguntas { get; } = [];

    /// <summary>El saldo contable (débito − crédito) de la cuenta del rol en una sucursal.</summary>
    public void Saldo(string rol, Guid sucursal, decimal debitoMenosCredito) => _saldos[rol][sucursal] = debitoMenosCredito;

    private Result<SaldosMapeadosDeCarteraDto> Responder(IReadOnlyList<CombinacionDeSaldo> combinaciones)
    {
        Preguntas.Add(combinaciones);
        var sinRegla = new List<int>();
        var conjuntos = new List<ConjuntoDeCuentasDeCarteraDto>();
        foreach (var grupo in combinaciones.Select((c, i) => (c, i)).GroupBy(x => x.c.Role))
        {
            if (!_saldos.TryGetValue(grupo.Key, out var porSucursal))
            {
                sinRegla.AddRange(grupo.Select(x => x.i));
                continue;
            }
            var cuenta = new CuentaDelConjuntoDto("C-" + grupo.Key, "Cuenta de " + grupo.Key, grupo.Key,
                porSucursal.Select(p => new SaldoPorSucursalDto(p.Key, p.Value)).ToList());
            conjuntos.Add(new ConjuntoDeCuentasDeCarteraDto(grupo.Key, grupo.Select(x => x.i).ToList(), [cuenta], porSucursal.Values.Sum()));
        }
        return Result.Success(new SaldosMapeadosDeCarteraDto(conjuntos, sinRegla));
    }
}
