using IngenIA365ERP.Application.Admin.Branches.Common;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

using CrearAgencia = IngenIA365ERP.Application.Core.Branches.Commands.CreateBranch.CreateBranchCommand;

namespace IngenIA365ERP.Application.Admin.Branches.CreateBranch;

public sealed class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, Result<Guid>>
{
    private readonly IAdminDbContext _db;
    private readonly IApplicationDbContext _cooperativa;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentTenantService _cooperativaActual;
    private readonly ICurrentCentralUserContext _usuario;
    private readonly ISender _sender;

    public CreateBranchCommandHandler(
        IAdminDbContext db,
        IApplicationDbContext cooperativa,
        ICurrentUserService currentUser,
        ICurrentTenantService cooperativaActual,
        ICurrentCentralUserContext usuario,
        ISender sender)
    {
        _db = db;
        _cooperativa = cooperativa;
        _currentUser = currentUser;
        _cooperativaActual = cooperativaActual;
        _usuario = usuario;
        _sender = sender;
    }

    public async Task<Result<Guid>> Handle(CreateBranchCommand request, CancellationToken ct)
    {
        // La cooperativa es la activa: hasta el 2026-11-06 venía en el cuerpo sin compararla con la de quien llama, y
        // hasta la feature 018 había que escribir su identificador en la pantalla.
        var alcance = await AlcanceDeSucursales.ResolverParaEscribirAsync(_db, _cooperativaActual, _usuario, ct);
        if (!alcance.Permitido) return Result.Failure<Guid>(alcance.Codigo!, alcance.Mensaje!);
        var cooperativa = alcance.Cooperativa!.Value;

        if (await _db.TenantBranches
                .AnyAsync(b => b.TenantId == cooperativa && b.Code == request.Code, ct))
        {
            return Result.Failure<Guid>(BranchErrorCodes.CodeTaken,
                $"Ya existe una oficina con código '{request.Code}' en esta cooperativa.");
        }

        if (request.IsHeadquarters && await _db.TenantBranches
                .AnyAsync(b => b.TenantId == cooperativa && b.IsHeadquarters, ct))
        {
            return Result.Failure<Guid>(BranchErrorCodes.HeadquartersExists,
                "La cooperativa ya tiene una oficina principal.");
        }

        // Antes de crear la oficina: son dos bases y lo que falle después ya no se puede deshacer junto.
        if (request.CrearAgencia)
        {
            var repetida = await _cooperativa.Branches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.LegacyCode == request.Code && !b.IsDeleted, ct);
            if (repetida is not null)
                return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("una agencia", request.Code, repetida.Name));
        }

        var actor = _currentUser.UserName ?? "SYSTEM";
        var branch = new TenantBranch
        {
            TenantId = cooperativa,
            Code = request.Code,
            Name = request.Name,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            IsActive = true,
            IsHeadquarters = request.IsHeadquarters,
            CreatedBy = actor,
            UpdatedBy = actor
        };
        _db.TenantBranches.Add(branch);
        await _db.SaveChangesAsync(ct);

        if (request.CrearAgencia)
        {
            var agencia = await _sender.Send(new CrearAgencia
            {
                Code = request.Code,
                Name = request.Name,
                TenantBranchPublicId = branch.PublicId,
            }, ct);
            if (agencia.IsFailure)
            {
                return Result.Failure<Guid>(BranchErrorCodes.AgencyNotCreated,
                    $"La oficina quedó creada, pero su agencia no: {agencia.Error.Message} Créela en Maestros › Agencias y vincúlela a esta oficina.");
            }
        }

        return Result.Success(branch.PublicId);
    }
}
