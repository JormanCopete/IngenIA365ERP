using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// La copia de los valores nacionales en cada cooperativa al sembrarla (feature 013, T0059, T39, R4; DT §2.21; data-model parte 3
/// §12.4): <c>Scope = Tenant</c>, <b>Order 88</b> —después de 71 (<c>PayrollLegalParametersSeeder</c>) y 74, y antes de todo lo
/// que lee valores—. Corre el núcleo único de la réplica (<see cref="ReplicacionDeValoresNacionales"/>) con origen
/// <see cref="NationalValueReplicationOrigin.TenantSeeder"/> sobre el contexto de la cooperativa que arma el orquestador, con los
/// mismos efectos que la tarea: Nómina recibe así el SMMLV, la UVT y los festivos que ya no siembran sus semillas (T93 regla 2), y
/// en una cooperativa existente los adopta.
///
/// <para>Sin la migración <c>PlataformaParaCartera</c> en la cooperativa o sin <c>ValoresNacionales</c> en la base administrativa
/// no hace nada. Las alertas que pidan los efectos sólo van al log: el orquestador no tiene el ámbito de la cooperativa para
/// entregarlas (la tarea programada sí lo tiene).</para>
/// </summary>
public sealed class NationalValuesReplicaSeeder(
    AdminDbContext admin,
    IEnumerable<IEfectoDeReplicacion> efectos,
    ILoggerFactory? registros = null) : IDataSeeder
{
    public const string MigracionDeLaCooperativa = "PlataformaParaCartera";

    /// <summary>El actor con que la semilla firma la réplica.</summary>
    public const string Actor = "Semilla del alta de la cooperativa";

    public int Order => 88;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!aplicadas.Any(m => m.EndsWith("_" + MigracionDeLaCooperativa, StringComparison.Ordinal)))
        {
            context.Logger.LogInformation("[ValoresNacionales.ReplicaSinTablas] La cooperativa no tiene todavía la migración {Migracion}: no se replica.", MigracionDeLaCooperativa);
            return 0;
        }
        if (!await PlatformNationalValuesSeeder.TieneLaMigracionAsync(admin, context.Logger, ct)) return 0;

        return await AplicarAsync(db, new RegistroCentralDeValoresNacionales(admin), efectos,
            registros?.CreateLogger<ReplicacionDeValoresNacionales>(), ct);
    }

    /// <summary>
    /// Replica hasta dejar la cooperativa al día (varias pasadas si la central tiene más de
    /// <see cref="ReplicacionDeValoresNacionales.MaximoPorPasada"/> filas nuevas). Devuelve cuántas filas copió.
    /// </summary>
    public static async Task<int> AplicarAsync(
        IApplicationDbContext db, IRegistroCentralDeValoresNacionales central, IEnumerable<IEfectoDeReplicacion> efectos,
        ILogger<ReplicacionDeValoresNacionales>? logger, CancellationToken ct)
    {
        var replicacion = new ReplicacionDeValoresNacionales(db, central, efectos, new RelojDeLaSemilla(), logger, servicios: null);
        var copiadas = 0;
        while (true)
        {
            var r = await replicacion.AplicarAsync(NationalValueReplicationOrigin.TenantSeeder, Actor, ct);
            if (!r.CentralDisponible || r.Motivo is not null)
            {
                // No detiene el alta: la tarea valores-nacionales.replicar la pone al día en su pasada siguiente.
                logger?.LogWarning("[ValoresNacionales.ReplicaDelAltaFallida] {Motivo}", r.Motivo);
                return copiadas;
            }
            if (!r.Copio) return copiadas;
            copiadas += r.Valores + r.Documentos;
        }
    }

    private sealed class RelojDeLaSemilla : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
