using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core.Connectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Platform.Connectors;

/// <summary>
/// Copia a la base de la cooperativa en curso los conectores que el maestro le habilitó, con todas sus versiones (feature 020, R15, T132, T133):
/// <c>ADM_Connectors</c>/<c>ADM_ConnectorVersions</c>/<c>ADM_TenantConnectors</c> → <c>COR_Connectors</c>/<c>COR_ConnectorVersions</c>. El envío
/// lee sólo la copia (Principio IV). Idempotente: sólo escribe lo que cambió. Deshabilitar marca la copia inactiva (los envíos esperan, «en
/// espera»); habilitar de nuevo despierta los envíos que esperaban (T164). La escribe sólo esta clase.
/// </summary>
public sealed class ReplicaDeConectores(IApplicationDbContext db, IAdminDbContext admin, ICurrentTenantService cooperativa, IDateTimeService reloj,
    ILogger<ReplicaDeConectores> logger)
{
    public async Task<int> SincronizarAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(cooperativa.TenantId, out var tenant)) return 0;
        var habilitaciones = await admin.TenantConnectors.AsNoTracking().Where(t => t.TenantId == tenant).Include(t => t.Connector).ToListAsync(ct);
        var copias = await db.Connectors.Include(c => c.Versions).ToListAsync(ct);
        var cambios = 0;
        var ahora = reloj.UtcNow;

        foreach (var h in habilitaciones)
        {
            var origen = h.Connector!;
            var habilitado = h.IsEnabled && origen.IsActive && origen.CurrentVersionNumber is not null;
            var copia = copias.FirstOrDefault(c => c.PlatformPublicId == origen.PublicId);
            if (copia is null)
            {
                if (!habilitado) continue;
                // La copia lleva el mismo PublicId que el conector del maestro: es el que conocen la pantalla, las rutas y los envíos, en
                // cualquier cooperativa (cada una tiene su base, así que no chocan). Antes nacía con uno propio y la cooperativa no encontraba
                // el conector por el identificador que le mostraba el maestro.
                copia = new Connector { PublicId = origen.PublicId, PlatformPublicId = origen.PublicId, CreatedAt = ahora, CreatedBy = "Réplica" };
                db.Connectors.Add(copia);
                copias.Add(copia);
                cambios++;
            }

            var versiones = await admin.PlatformConnectorVersions.AsNoTracking().Where(v => v.ConnectorId == origen.Id).ToListAsync(ct);
            foreach (var v in versiones.Where(v => copia.Versions.All(x => x.Number != v.Number)))
            {
                var nueva = new ConnectorVersion
                {
                    Connector = copia, Number = v.Number, BaseUrl = v.BaseUrl, DefinitionJson = v.DefinitionJson, DefinitionSha256 = v.DefinitionSha256,
                    CreatedAt = ahora, CreatedBy = "Réplica",
                };
                copia.Versions.Add(nueva);
                db.ConnectorVersions.Add(nueva);
                cambios++;
            }

            var despierta = habilitado && !copia.IsEnabled && copia.Id != 0;
            if (copia.Code != origen.Code || copia.Name != origen.Name || copia.IsEnabled != habilitado || copia.CurrentVersionNumber != origen.CurrentVersionNumber)
            {
                copia.Code = origen.Code;
                copia.Name = origen.Name;
                copia.IsEnabled = habilitado;
                copia.CurrentVersionNumber = origen.CurrentVersionNumber;
                cambios++;
            }
            if (despierta) await Core.Connectors.EnviosEnEspera.DespertarAsync(db, copia.Id, ahora, ct);
        }

        // Una copia sin habilitación en la plataforma queda inactiva.
        foreach (var huerfana in copias.Where(c => c.IsEnabled && habilitaciones.All(h => h.Connector!.PublicId != c.PlatformPublicId)))
        {
            huerfana.IsEnabled = false;
            cambios++;
        }

        if (cambios > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[Conectores.Replica] {Cooperativa}: {Cambios} cambio(s) en la copia de conectores.", tenant, cambios);
        }
        return cambios;
    }
}

/// <summary>Corre la réplica de conectores en cada pasada del programador y en cada cooperativa (T133).</summary>
public sealed class TareaDeReplicaDeConectores : ITareaProgramada
{
    public const string NombreDeLaTarea = "conectores.replicar";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => true;

    public Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        servicios.GetRequiredService<ReplicaDeConectores>().SincronizarAsync(ct);
}
