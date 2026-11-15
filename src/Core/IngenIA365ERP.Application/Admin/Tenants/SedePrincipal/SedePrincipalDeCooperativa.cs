using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Admin.Tenants.SedePrincipal;

/// <summary>Qué pasó al asegurar la sede de una cooperativa.</summary>
public enum ResultadoDeSede
{
    /// <summary>Ya tenía oficina y agencia: no se tocó nada.</summary>
    SinCambios,

    /// <summary>No tenía ninguna de las dos y se crearon vinculadas.</summary>
    OficinaYAgenciaCreadas,

    /// <summary>Tenía oficina principal sin agencia y se le creó.</summary>
    AgenciaCreada,

    /// <summary>Lo que tiene no se puede completar sin adivinar: queda el aviso en el log.</summary>
    ARevisar,
}

/// <summary>
/// La oficina principal y su agencia nacen con la cooperativa (feature 018). Lo llaman el registro, la reparación y el
/// arranque. Son dos bases sin transacción común, así que se repite sin duplicar: si la agencia falla, la pasada
/// siguiente encuentra la oficina y la completa.
/// </summary>
public interface ISedePrincipalDeCooperativa
{
    Task<ResultadoDeSede> AsegurarAsync(int tenantId, CancellationToken ct);
}

internal sealed class SedePrincipalDeCooperativa(
    IAdminDbContext admin,
    ITenantDbContextFactory fabrica,
    ICurrentUserService usuario,
    IAuditAppendOnlyWriter auditoria,
    IDateTimeService reloj,
    ILogger<SedePrincipalDeCooperativa> logger) : ISedePrincipalDeCooperativa
{
    public const string Codigo = "PRINCIPAL";
    public const string Nombre = "Oficina principal";

    public async Task<ResultadoDeSede> AsegurarAsync(int tenantId, CancellationToken ct)
    {
        var cooperativa = await admin.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.PublicId, t.Name, t.DatabaseName, t.SchemaName, t.ConnectionString })
            .FirstAsync(ct);

        // Se cuentan también las dadas de baja: una agencia que alguien retiró no debe reaparecer sola en el arranque.
        var oficinas = await admin.TenantBranches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .Select(b => new { b.PublicId, b.Code, b.Name, b.IsHeadquarters, b.IsDeleted })
            .ToListAsync(ct);

        await using var escena = fabrica.Abrir(cooperativa.DatabaseName ?? cooperativa.SchemaName, cooperativa.ConnectionString);
        var hayAgencias = await escena.Db.Branches.IgnoreQueryFilters().AsNoTracking().AnyAsync(ct);

        if (oficinas.Count > 0 && hayAgencias) return ResultadoDeSede.SinCambios;

        var actor = usuario.UserName ?? "SYSTEM";
        if (oficinas.Count == 0)
        {
            if (hayAgencias)
            {
                logger.LogWarning(
                    "La cooperativa {Cooperativa} tiene agencias y ninguna oficina: no se crea una sede sin saber a cuál corresponde.",
                    cooperativa.Name);
                return ResultadoDeSede.ARevisar;
            }

            var oficina = new TenantBranch
            {
                TenantId = tenantId,
                Code = Codigo,
                Name = Nombre,
                IsActive = true,
                IsHeadquarters = true,
                CreatedBy = actor,
                UpdatedBy = actor,
            };
            admin.TenantBranches.Add(oficina);
            await admin.SaveChangesAsync(ct);

            await CrearAgenciaAsync(escena.Db, oficina.PublicId, oficina.Code, oficina.Name, actor, ct);
            await AuditarAsync(cooperativa.PublicId, oficina.PublicId, actor, ct);
            return ResultadoDeSede.OficinaYAgenciaCreadas;
        }

        var principal = oficinas.FirstOrDefault(o => o.IsHeadquarters && !o.IsDeleted);
        if (principal is null)
        {
            logger.LogWarning(
                "La cooperativa {Cooperativa} tiene oficinas, ninguna principal, y ninguna agencia: se crea a mano en Maestros › Agencias.",
                cooperativa.Name);
            return ResultadoDeSede.ARevisar;
        }

        await CrearAgenciaAsync(escena.Db, principal.PublicId, principal.Code, principal.Name, actor, ct);
        await AuditarAsync(cooperativa.PublicId, principal.PublicId, actor, ct);
        return ResultadoDeSede.AgenciaCreada;
    }

    private async Task CrearAgenciaAsync(
        IApplicationDbContext db, Guid oficina, string codigo, string nombre, string actor, CancellationToken ct)
    {
        var propio = CodigoDeCatalogo.Normalizar(codigo);
        db.Branches.Add(new Branch
        {
            // El código de una oficina admite 20 caracteres y el de una agencia 10: uno más largo se deja vacío, es opcional.
            LegacyCode = propio is { Length: <= CodigoDeCatalogo.LargoCorto } ? propio : null,
            Name = nombre.Length > 80 ? nombre[..80] : nombre,
            TenantBranchPublicId = oficina,
            CreatedAt = reloj.UtcNow,
            CreatedBy = actor,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task AuditarAsync(Guid cooperativa, Guid oficina, string actor, CancellationToken ct)
    {
        try
        {
            await auditoria.AppendAsync(new AuditEventDocument(
                TenantId: cooperativa.ToString("N"),
                UserId: actor,
                UserName: actor,
                Action: AuditEventTypes.TenantHeadquartersCreated,
                EntityType: nameof(TenantBranch),
                EntityPublicId: oficina.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null, UserAgent: null,
                Endpoint: null, HttpMethod: null, HttpStatusCode: null, DurationMs: null,
                OccurredAt: reloj.UtcNow), ct);
        }
        catch (Exception ex)
        {
            // La sede ya existe: un fallo de la auditoría no la deshace, pero tampoco se calla.
            logger.LogWarning(ex, "No se pudo auditar la sede creada de la cooperativa {Cooperativa}.", cooperativa);
        }
    }
}
