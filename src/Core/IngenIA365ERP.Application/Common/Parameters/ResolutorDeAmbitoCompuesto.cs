using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Parameters;

namespace IngenIA365ERP.Application.Common.Parameters;

/// <summary>
/// Lo que un módulo registra para resolver los ámbitos de parámetro que son suyos (feature 013, T43; data-model parte 3 §4.15):
/// Inventario la bodega, el tipo de documento, el punto y la caja; Cartera la línea de crédito (<see cref="ParameterScopeKind.CreditLine"/>)
/// y el tipo de hecho (<see cref="ParameterScopeKind.LendingFactType"/>) cuando existan <c>LND_CreditLines</c> y <c>LND_FactTypes</c>. (nuevo)
/// </summary>
/// <param name="Modulo">El módulo dueño de las entidades (<c>INV</c>, <c>LND</c>).</param>
/// <param name="Ambitos">Los ámbitos que resuelve; dos módulos no registran el mismo.</param>
/// <param name="Resolutor">Quien resuelve por <c>PublicId</c> dentro del alcance de quien pide.</param>
public sealed record ResolutorDeAmbitoDeModulo(string Modulo, IReadOnlyCollection<ParameterScopeKind> Ambitos, IResolutorDeAmbitoDeParametro Resolutor);

/// <summary>
/// El <see cref="IResolutorDeAmbitoDeParametro"/> de la plataforma (feature 013, T43): compuesto por módulo. Cada ámbito lo resuelve
/// el módulo que lo registró (<see cref="ResolutorDeAmbitoDeModulo"/>); un ámbito que nadie registró responde como antes el
/// resolutor vacío —404 al resolver, nada al describir—, así que sólo se pueden registrar vigencias generales de él. Dos módulos que
/// registran el mismo ámbito hacen fallar la construcción: el ámbito tiene un solo dueño. (nuevo)
/// </summary>
public sealed class ResolutorDeAmbitoCompuesto : IResolutorDeAmbitoDeParametro
{
    private readonly Dictionary<ParameterScopeKind, ResolutorDeAmbitoDeModulo> _porAmbito = [];

    public ResolutorDeAmbitoCompuesto(IEnumerable<ResolutorDeAmbitoDeModulo> registrados)
    {
        foreach (var registro in registrados)
        foreach (var ambito in registro.Ambitos)
        {
            if (ambito == ParameterScopeKind.None)
                throw new InvalidOperationException($"El módulo {registro.Modulo} registró el ámbito general: no se resuelve, no tiene entidad.");
            if (_porAmbito.TryGetValue(ambito, out var otro))
                throw new InvalidOperationException($"El ámbito {ambito} lo registran dos módulos ({otro.Modulo} y {registro.Modulo}).");
            _porAmbito[ambito] = registro;
        }
    }

    /// <summary>Los ámbitos con dueño y quién es.</summary>
    public IReadOnlyDictionary<ParameterScopeKind, string> Duenos =>
        _porAmbito.ToDictionary(p => p.Key, p => p.Value.Modulo);

    public Task<Result<AmbitoDeParametro>> ResolverAsync(ParameterScopeKind ambito, Guid publicId, CancellationToken ct) =>
        _porAmbito.TryGetValue(ambito, out var registro)
            ? registro.Resolutor.ResolverAsync(ambito, publicId, ct)
            : Task.FromResult(Result.Failure<AmbitoDeParametro>(Error.NotFound));

    public Task<IReadOnlyList<AmbitoDeParametro>> DescribirAsync(ParameterScopeKind ambito, IReadOnlyCollection<int> ids, CancellationToken ct) =>
        _porAmbito.TryGetValue(ambito, out var registro)
            ? registro.Resolutor.DescribirAsync(ambito, ids, ct)
            : Task.FromResult<IReadOnlyList<AmbitoDeParametro>>([]);
}
