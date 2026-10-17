using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Accrual;

/// <summary>
/// El efecto de Cartera sobre la réplica que encola la corrección por valor nacional (feature 013, US4, T0500; T28, C03, C05; data-model §22.1):
/// cuando llega una <c>USURA</c> que <b>corrige</b> otra (<c>SupersedesSourcePublicId</c>) y cubre días ya causados (algún crédito tiene su marca
/// en o después de la vigencia corregida), inserta <b>en la transacción de la réplica</b> una corrida <c>NationalValueCorrection</c>
/// <c>Requested</c> por valor corregido, con <c>AffectedFrom</c>/<c>AffectedTo</c> = los días causados que toca. Sólo con Cartera activada; la
/// encola la réplica, no el ciclo. La corrida guarda en <c>ExecutionOrigin</c> el valor que la provocó (<c>Replica:{SourcePublicId}</c>): la fila de
/// la réplica se crea después de sus efectos y su <c>Id</c> lo toma la primera tanda (<c>NationalValueReplicationId</c>). Sin guardar. (nuevo)
/// <para>K6b (fase 24): también una <c>UVR</c> que corrige otra. Ésa sólo toca a los créditos <b>en UVR</b>
/// (<see cref="CreditosAfectados"/>): sin ninguno, o sin ninguno causado en o después del día corregido, no se encola nada. Antes sólo reaccionaba a la
/// usura y corregir una UVR ya replicada dejaba los días causados con la UVR vieja.</para>
/// <para>K6b (decisión del dueño del 2026-10-17): la corrida de una <c>UVR</c> llega <b>hasta la marca</b>, no hasta el fin de la vigencia del valor
/// (que en la serie diaria es el mismo día): la UVR de un día cambia también la corrección monetaria del día siguiente (la víspera se valora con
/// ella) y, si ese día se aplicó un pago, el pago es otra cantidad de UVR y cambia el capital en UVR, la causación y los repartos que siguen.</para>
/// </summary>
public sealed class EfectoDeReplicacionCorreccionDeValorNacional : IEfectoDeReplicacion
{
    public const string NombreDelEfecto = "cartera-correccion-de-valor-nacional";
    public const string ConteoCorridas = "corridasDeCorreccion";
    public const string PrefijoDeOrigen = "Replica:";

    /// <summary>Los códigos cuya corrección recalcula días ya causados: la usura (topa la tasa) y la UVR (valora en pesos los créditos en UVR).</summary>
    public static IReadOnlySet<string> CodigosQueCorrigenLaCausacion { get; } =
        new HashSet<string>(StringComparer.Ordinal) { CodigosDeValorNacional.Usura, CodigosDeValorNacional.Uvr };

    /// <summary>
    /// Los controles de los créditos que toca la corrección de <paramref name="codigo"/>: todos para la usura; sólo los créditos en UVR para la UVR
    /// (la unidad del capital se sella en las condiciones y no cambia). La misma consulta la usan el efecto y la corrida.
    /// </summary>
    public static IQueryable<LoanControl> CreditosAfectados(IApplicationDbContext db, string codigo)
    {
        ArgumentNullException.ThrowIfNull(db);
        var controles = db.LoanControls.AsNoTracking();
        return codigo == CodigosDeValorNacional.Uvr
            ? controles.Where(c => db.LoanTermsVersions.Any(t => t.LoanId == c.LoanId && t.DenominationUnit == DenominationUnit.Uvr))
            : controles;
    }

    public string Nombre => NombreDelEfecto;

    public async Task<EfectoAplicado> AlReplicarAsync(IApplicationDbContext db, LoteReplicado lote, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(lote);
        var correcciones = lote.Valores.Where(v => CodigosQueCorrigenLaCausacion.Contains(v.Code) && v.SupersedesSourcePublicId is not null).ToList();
        if (correcciones.Count == 0) return EfectoAplicado.Nada;
        var activada = await db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);
        if (!activada) return EfectoAplicado.Nada;

        // La última marca de los créditos que toca cada código (todos para la usura; los en UVR para la UVR), una consulta por código.
        var marcas = new Dictionary<string, DateOnly?>(StringComparer.Ordinal);
        foreach (var codigo in correcciones.Select(v => v.Code).Distinct(StringComparer.Ordinal))
            marcas[codigo] = await CreditosAfectados(db, codigo).MaxAsync(c => (DateOnly?)c.LastAccruedDate, ct);

        var notas = new List<string>();
        var corridas = 0;
        foreach (var v in correcciones.OrderBy(v => v.SourceSequence))
        {
            if (marcas[v.Code] is not { } marca || v.ValidFrom > marca) continue;
            var hasta = v.Code != CodigosDeValorNacional.Uvr && v.ValidTo is { } fin && fin < marca ? fin : marca;
            db.LendingProcessRuns.Add(new LendingProcessRun
            {
                Kind = LendingProcessKind.NationalValueCorrection,
                Status = LendingProcessRunStatus.Requested,
                AffectedFrom = v.ValidFrom,
                AffectedTo = hasta,
                RequestedByKind = ActorKind.Process,
                ExecutionOrigin = PrefijoDeOrigen + v.SourcePublicId.ToString("D"),
                Reason = $"Corrección de {v.Code} {v.Key} desde el {v.ValidFrom:yyyy-MM-dd}",
                RequestedAt = lote.Ahora,
            });
            corridas++;
            notas.Add($"{v.Code} {v.Key}: corrección desde el {v.ValidFrom:yyyy-MM-dd}; se reconcilia la causación hasta el {hasta:yyyy-MM-dd}.");
        }
        return corridas == 0
            ? EfectoAplicado.Nada
            : new EfectoAplicado(new Dictionary<string, int> { [ConteoCorridas] = corridas }, notas, []);
    }
}
