using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Tenants.ProvisionSchema;

public sealed class ProvisionTenantSchemaCommandHandler
    : IRequestHandler<ProvisionTenantSchemaCommand, Result<ProvisionTenantSchemaResult>>
{
    private const string HeadquartersCode = "MAT";
    private const string HeadquartersName = "Sede Principal";

    private readonly IAdminDbContext _admin;
    private readonly ITenantDatabaseProvisioner _aprovisionador;
    private readonly ITenantCacheSlotAllocator _ranuras;
    private readonly ITenantDbContextFactory _fabrica;
    private readonly ICurrentUserService _currentUser;

    public ProvisionTenantSchemaCommandHandler(
        IAdminDbContext admin,
        ITenantDatabaseProvisioner aprovisionador,
        ITenantCacheSlotAllocator ranuras,
        ITenantDbContextFactory fabrica,
        ICurrentUserService currentUser)
    {
        _admin = admin;
        _aprovisionador = aprovisionador;
        _ranuras = ranuras;
        _fabrica = fabrica;
        _currentUser = currentUser;
    }

    public async Task<Result<ProvisionTenantSchemaResult>> Handle(
        ProvisionTenantSchemaCommand request, CancellationToken ct)
    {
        // El tenant vive en IngenIA365ERP_Admin; los roles en IngenIA365ERP
        // (operacional). Resolvemos primero el Id interno via PublicId.
        var tenant = await _admin.Tenants
            .Where(t => t.PublicId == request.TenantPublicId)
            .Select(t => new { t.Id, t.Name, t.SchemaName, t.Subdomain, t.DatabaseName, t.ConnectionString, t.RedisDbIndex })
            .FirstOrDefaultAsync(ct);
        if (tenant is null)
        {
            return Result.Failure<ProvisionTenantSchemaResult>(
                "Generic.NotFound", "La cooperativa no existe.");
        }

        var actor = _currentUser.UserName ?? "SYSTEM";

        // Ya no se clonan roles desde plantillas. Ese camino nunca pudo funcionar:
        // insertaba filas en SEC_Roles con TenantId apuntando a ADM_Tenants, y la
        // clave foranea resuelve al ADM_Tenants LOCAL de cada esquema, que tiene
        // cero filas. Cada intento violaba la FK. Es la razon de que ningun esquema
        // de cooperativa tuviera roles.
        //
        // Con aislamiento por esquema no hay nada que clonar: los roles se SIEMBRAN
        // dentro del esquema, con TenantId nulo, porque ahi dentro todos son suyos.
        // De eso se encarga el aprovisionador, que ademas crea el esquema si falta
        // y lo migra. Es idempotente, asi que este endpoint sigue sirviendo para
        // reparar una cooperativa que quedo a medias.
        await _aprovisionador.AprovisionarAsync(
            tenant.DatabaseName ?? tenant.SchemaName,
            tenant.Subdomain ?? tenant.SchemaName,
            tenant.ConnectionString,
            ct);

        // Reparacion: este endpoint existe para dejar al dia una cooperativa que
        // quedo a medias, y una sin ranura de cache lo esta. Sin esto habria que
        // tocar la fila a mano.
        if (tenant.RedisDbIndex is null)
        {
            var fila = await _admin.Tenants.FirstAsync(t => t.Id == tenant.Id, ct);
            fila.RedisDbIndex = await _ranuras.ReservarAsync(tenant.Name, ct);
            await _admin.SaveChangesAsync(ct);
        }

        var headquartersCreated = await EnsureHeadquartersAsync(tenant.Id, tenant.Name, actor, ct);

        // Los conteos de clonado se quedan en cero: el DTO es contrato publico y
        // vaciarlo romperia a quien lo lea. Lo que antes contaba ya no ocurre.
        return Result.Success(new ProvisionTenantSchemaResult(0, 0, headquartersCreated));
    }

    /// <summary>
    /// Clona los roles built-in plantilla (TenantId NULL) al tenant indicado.
    /// Idempotente: si el rol ya existe para ese tenant (por Code), se omite.
    /// </summary>

    /// <summary>
    /// Por cada rol clonado, copia sus vínculos a permisos. Mapea
    /// rol-plantilla → rol-tenant por <c>Code</c>.
    /// </summary>

    private async Task<bool> EnsureHeadquartersAsync(
        int tenantId, string tenantName, string actor, CancellationToken ct)
    {
        var any = await _admin.TenantBranches
            .AnyAsync(b => b.TenantId == tenantId && b.IsHeadquarters, ct);
        if (any) return false;

        _admin.TenantBranches.Add(new TenantBranch
        {
            TenantId = tenantId,
            Code = HeadquartersCode,
            Name = $"{HeadquartersName} — {tenantName}",
            IsActive = true,
            IsHeadquarters = true,
            CreatedBy = actor,
            UpdatedBy = actor
        });
        await _admin.SaveChangesAsync(ct);
        return true;
    }
}
