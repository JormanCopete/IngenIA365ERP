using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
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
/// </summary>
public sealed class EfectoDeReplicacionCorreccionDeValorNacional : IEfectoDeReplicacion
{
    public const string NombreDelEfecto = "cartera-correccion-de-valor-nacional";
    public const string ConteoCorridas = "corridasDeCorreccion";
    public const string PrefijoDeOrigen = "Replica:";

    public string Nombre => NombreDelEfecto;

    public async Task<EfectoAplicado> AlReplicarAsync(IApplicationDbContext db, LoteReplicado lote, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(lote);
        var correcciones = lote.Valores.Where(v => v.Code == CodigosDeValorNacional.Usura && v.SupersedesSourcePublicId is not null).ToList();
        if (correcciones.Count == 0) return EfectoAplicado.Nada;
        var activada = await db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);
        if (!activada) return EfectoAplicado.Nada;

        var ultimaMarca = await db.LoanControls.AsNoTracking().MaxAsync(c => (DateOnly?)c.LastAccruedDate, ct);
        if (ultimaMarca is not { } marca) return EfectoAplicado.Nada;

        var notas = new List<string>();
        var corridas = 0;
        foreach (var v in correcciones.OrderBy(v => v.SourceSequence))
        {
            if (v.ValidFrom > marca) continue;
            var hasta = v.ValidTo is { } fin && fin < marca ? fin : marca;
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
