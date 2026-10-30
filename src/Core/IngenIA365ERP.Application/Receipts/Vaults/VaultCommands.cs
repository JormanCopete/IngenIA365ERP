using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Vaults;

// Feature 015, US1 (T0073; FR-035, FR-180, FR-182; contracts/api.md §2; data-model §2.6): la bóveda de cada oficina —propia o la caja principal que hace de
// bóveda— y sus custodios con vigencia. Configuración: se admite antes de activar. Una por oficina; con Caja.DobleCustodia (activo por defecto, D34) la
// bóveda necesita dos custodios vigentes distintos para moverse (los movimientos llegan con US2). (nuevo)

internal static class BovedasDeRecaudo
{
    public static async Task<VaultDto> DtoAsync(IApplicationDbContext db, Vault b, DateOnly? hoy, CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().Where(o => o.Id == b.BranchId).Select(o => new { o.PublicId, o.Name }).FirstAsync(ct);
        var caja = b.MainCashBoxId is { } id
            ? await db.CashBoxes.AsNoTracking().Where(c => c.Id == id).Select(c => new { c.PublicId, c.Code }).FirstOrDefaultAsync(ct)
            : null;
        var custodios = await (from c in db.VaultCustodians.AsNoTracking()
                               join u in db.Users.AsNoTracking().IgnoreQueryFilters() on c.UserId equals u.Id
                               where c.VaultId == b.Id && (hoy == null || c.ValidTo == null || c.ValidTo >= hoy)
                               orderby c.ValidFrom
                               select new VaultCustodianDto(u.PublicId, u.Username, c.ValidFrom, c.ValidTo)).ToListAsync(ct);
        return new VaultDto(b.PublicId, oficina.PublicId, oficina.Name, b.Mode, caja?.PublicId, caja?.Code, b.Status, custodios);
    }

    public static async Task<Vault?> VisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.Vaults : db.Vaults.AsNoTracking();
        var boveda = await consulta.FirstOrDefaultAsync(v => v.PublicId == publicId, ct);
        return boveda is not null && (await alcance.ObtenerAsync(ct)).IncluyeOficina(boveda.BranchId) ? boveda : null;
    }

    /// <summary>La caja principal de la oficina para el modo <see cref="VaultMode.MainCashBox"/>, o el error.</summary>
    public static async Task<Result<int?>> CajaPrincipalAsync(IApplicationDbContext db, int oficina, VaultMode modo, Guid? cajaPublicId, CancellationToken ct)
    {
        if (modo == VaultMode.Vault) return Result.Success<int?>(null);
        if (cajaPublicId is not { } publica) return Result.Failure<int?>(ErroresDeRecaudo.BovedaSinCajaPrincipal());
        var caja = await (from c in db.CashBoxes.AsNoTracking()
                          join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                          where c.PublicId == publica && p.BranchId == oficina && c.IsMainCashBox && c.Status == ReceiptsCatalogStatus.Active
                          select (int?)c.Id).FirstOrDefaultAsync(ct);
        return caja is null ? Result.Failure<int?>(ErroresDeRecaudo.BovedaSinCajaPrincipal()) : Result.Success(caja);
    }
}

/// <summary><c>GET /api/receipts/vaults</c>: las bóvedas de las oficinas al alcance. (nuevo)</summary>
public sealed record ListVaultsQuery : IRequest<Result<IReadOnlyList<VaultDto>>>;

public sealed class ListVaultsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<ListVaultsQuery, Result<IReadOnlyList<VaultDto>>>
{
    public async Task<Result<IReadOnlyList<VaultDto>>> Handle(ListVaultsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var bovedas = (await db.Vaults.AsNoTracking().OrderBy(v => v.Id).ToListAsync(ct)).Where(v => mio.IncluyeOficina(v.BranchId)).ToList();
        var lista = new List<VaultDto>();
        foreach (var b in bovedas) lista.Add(await BovedasDeRecaudo.DtoAsync(db, b, reloj.HoyLocal, ct));
        return Result.Success<IReadOnlyList<VaultDto>>(lista);
    }
}

/// <summary><c>POST /api/receipts/vaults</c> (<c>Receipts.Setup.Manage</c>): la bóveda de una oficina, una por oficina (<c>Receipts.Vault.AlreadyExists</c>). (nuevo)</summary>
public sealed record CreateVaultCommand(Guid BranchPublicId, VaultMode Mode, Guid? MainCashBoxPublicId = null) : IRequest<Result<VaultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateVaultCommandValidator : AbstractValidator<CreateVaultCommand>
{
    public CreateVaultCommandValidator()
    {
        RuleFor(x => x.BranchPublicId).NotEmpty().WithMessage("Indique la oficina.");
        RuleFor(x => x.Mode).IsInEnum();
    }
}

public sealed class CreateVaultCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<CreateVaultCommand, Result<VaultDto>>
{
    public async Task<Result<VaultDto>> Handle(CreateVaultCommand request, CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.BranchPublicId, ct);
        if (oficina is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(oficina.Id))
            return Result.Failure<VaultDto>(ErroresDeRecaudo.NoEncontrado("La oficina"));
        if (await db.Vaults.AsNoTracking().AnyAsync(v => v.BranchId == oficina.Id, ct)) return Result.Failure<VaultDto>(ErroresDeRecaudo.BovedaRepetida());
        var caja = await BovedasDeRecaudo.CajaPrincipalAsync(db, oficina.Id, request.Mode, request.MainCashBoxPublicId, ct);
        if (caja.IsFailure) return Result.Failure<VaultDto>(caja.Error);
        var boveda = new Vault { BranchId = oficina.Id, Mode = request.Mode, MainCashBoxId = caja.Value };
        db.Vaults.Add(boveda);
        await db.SaveChangesAsync(ct);
        return Result.Success(await BovedasDeRecaudo.DtoAsync(db, boveda, reloj.HoyLocal, ct));
    }
}

/// <summary><c>PUT /api/receipts/vaults/{id}</c>: el modo (bóveda propia o caja principal), la caja principal y el estado. (nuevo)</summary>
public sealed record UpdateVaultCommand(Guid PublicId, VaultMode Mode, Guid? MainCashBoxPublicId, ReceiptsCatalogStatus Status) : IRequest<Result<VaultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateVaultCommandValidator : AbstractValidator<UpdateVaultCommand>
{
    public UpdateVaultCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class UpdateVaultCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<UpdateVaultCommand, Result<VaultDto>>
{
    public async Task<Result<VaultDto>> Handle(UpdateVaultCommand request, CancellationToken ct)
    {
        var boveda = await BovedasDeRecaudo.VisibleAsync(db, alcance, request.PublicId, seguir: true, ct);
        if (boveda is null) return Result.Failure<VaultDto>(ErroresDeRecaudo.NoEncontrado("La bóveda"));
        var caja = await BovedasDeRecaudo.CajaPrincipalAsync(db, boveda.BranchId, request.Mode, request.MainCashBoxPublicId, ct);
        if (caja.IsFailure) return Result.Failure<VaultDto>(caja.Error);
        boveda.Mode = request.Mode;
        boveda.MainCashBoxId = caja.Value;
        boveda.Status = request.Status;
        await db.SaveChangesAsync(ct);
        return Result.Success(await BovedasDeRecaudo.DtoAsync(db, boveda, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/vaults/{id}/custodians</c> (<c>Receipts.Setup.Manage</c>): fija los custodios vigentes desde hoy; los que salen cierran su
/// vigencia ayer y los que siguen la conservan. Con <c>Caja.DobleCustodia</c> exige al menos dos personas distintas
/// (<c>Receipts.Vault.CustodiansRequired</c>). (nuevo)
/// </summary>
public sealed record SetVaultCustodiansCommand(Guid VaultPublicId, IReadOnlyList<Guid> UserPublicIds) : IRequest<Result<VaultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetVaultCustodiansCommandValidator : AbstractValidator<SetVaultCustodiansCommand>
{
    public SetVaultCustodiansCommandValidator()
    {
        RuleFor(x => x.VaultPublicId).NotEmpty();
        RuleFor(x => x.UserPublicIds).NotNull();
    }
}

public sealed class SetVaultCustodiansCommandHandler(
    IApplicationDbContext db, IAlcanceDeRecaudo alcance, ParametrosDeCaja parametros, SegregacionDeRecaudo segregacion, IDateTimeService reloj)
    : IRequestHandler<SetVaultCustodiansCommand, Result<VaultDto>>
{
    public async Task<Result<VaultDto>> Handle(SetVaultCustodiansCommand request, CancellationToken ct)
    {
        var boveda = await BovedasDeRecaudo.VisibleAsync(db, alcance, request.VaultPublicId, seguir: false, ct);
        if (boveda is null) return Result.Failure<VaultDto>(ErroresDeRecaudo.NoEncontrado("La bóveda"));
        var pedidos = request.UserPublicIds.Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().Where(u => pedidos.Contains(u.PublicId)).Select(u => new { u.PublicId, u.Id }).ToListAsync(ct);
        if (usuarios.Count != pedidos.Count) return Result.Failure<VaultDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));

        var hoy = reloj.HoyLocal;
        if (await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaDobleCustodia, hoy, ct) && !await DistintasPersonasAsync(usuarios.Select(u => u.Id).ToList(), ct))
            return Result.Failure<VaultDto>(ErroresDeRecaudo.CustodiosInsuficientes());

        var ids = usuarios.Select(u => u.Id).ToHashSet();
        var vigentes = await db.VaultCustodians.Where(c => c.VaultId == boveda.Id && (c.ValidTo == null || c.ValidTo >= hoy)).ToListAsync(ct);
        foreach (var sale in vigentes.Where(c => !ids.Contains(c.UserId)))
        {
            if (sale.ValidFrom >= hoy) sale.IsDeleted = true;
            else sale.ValidTo = hoy.AddDays(-1);
        }
        foreach (var entra in ids.Where(id => vigentes.All(v => v.UserId != id)))
            db.VaultCustodians.Add(new VaultCustodian { VaultId = boveda.Id, UserId = entra, ValidFrom = hoy });
        await db.SaveChangesAsync(ct);
        return Result.Success(await BovedasDeRecaudo.DtoAsync(db, boveda, hoy, ct));
    }

    private async Task<bool> DistintasPersonasAsync(IReadOnlyList<int> usuarios, CancellationToken ct)
    {
        for (var i = 0; i < usuarios.Count; i++)
            for (var j = i + 1; j < usuarios.Count; j++)
                if (!await segregacion.MismaPersonaAsync(usuarios[i], usuarios[j], ct)) return true;
        return false;
    }
}
