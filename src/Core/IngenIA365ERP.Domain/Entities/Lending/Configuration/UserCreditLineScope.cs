using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Governance;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_UserCreditLineScopes</c> (feature 013, T0210; data-model §3.14; FR-004, SC-015, T62): las líneas que opera un usuario.
/// Falla cerrado: sin filas no hay líneas, salvo <c>Lending.Scope.AllLines</c>. Lo retirado se da de baja lógica.
///
/// <para>
/// T0283: <see cref="Reemplazar"/> calcula el conjunto nuevo de un usuario —mantiene las líneas que siguen, ajusta la marca de por
/// defecto, agrega las nuevas y da de baja lógica las retiradas— con a lo sumo una por defecto (<c>Lending.Scope.DefaultDuplicate</c>).
/// </para>
/// </summary>
public class UserCreditLineScope : AuditableEntity
{
    public const string CodigoDefectoDuplicado = "Lending.Scope.DefaultDuplicate";

    public int UserId { get; set; }

    public int CreditLineId { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>
    /// El conjunto nuevo de líneas de <paramref name="userId"/>: devuelve las filas a agregar; las de <paramref name="actuales"/> que
    /// siguen quedan con su marca de por defecto y las retiradas, dadas de baja (<paramref name="ahora"/>, <paramref name="quien"/>).
    /// Una línea repetida en <paramref name="nuevas"/> cuenta una vez (la marca de por defecto, si alguna la tiene).
    /// </summary>
    public static ResultadoDeAlcanceDeLineas Reemplazar(
        int userId, IReadOnlyCollection<UserCreditLineScope> actuales, IEnumerable<(int CreditLineId, bool IsDefault)> nuevas, DateTime ahora, string? quien)
    {
        ArgumentNullException.ThrowIfNull(actuales);
        var pedidas = nuevas.GroupBy(n => n.CreditLineId).ToDictionary(g => g.Key, g => g.Any(n => n.IsDefault));
        if (pedidas.Count(p => p.Value) > 1)
            return new ResultadoDeAlcanceDeLineas(new ProblemaDeCartera(CodigoDefectoDuplicado,
                "Sólo una línea puede ser la de por defecto del usuario."), [], []);

        var vivas = actuales.Where(a => a.UserId == userId && !a.IsDeleted).ToList();
        var retiradas = new List<UserCreditLineScope>();
        foreach (var actual in vivas)
        {
            if (pedidas.TryGetValue(actual.CreditLineId, out var porDefecto))
            {
                actual.IsDefault = porDefecto;
                continue;
            }
            actual.IsDeleted = true;
            actual.DeletedAt = ahora;
            actual.DeletedBy = quien;
            retiradas.Add(actual);
        }

        var yaEstan = vivas.Select(v => v.CreditLineId).ToHashSet();
        var agregadas = pedidas.Where(p => !yaEstan.Contains(p.Key))
            .Select(p => new UserCreditLineScope { UserId = userId, CreditLineId = p.Key, IsDefault = p.Value })
            .ToList();
        return new ResultadoDeAlcanceDeLineas(null, agregadas, retiradas);
    }
}
