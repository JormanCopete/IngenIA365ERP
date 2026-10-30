using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Activation;

// Feature 015, R1, T0352 (FR-071, FR-147, FR-149; contratos-v2.md §1, §3.1): Recaudo adopta las versiones 2 de los contratos de Cartera y Captaciones. Las v1 siguen
// admitidas: una cooperativa que no adopta la v2 sigue como en R0 (la identificación tardía con la fecha más antigua que admita el contrato y la alerta
// Recaudo.FechaValorTardia).

/// <summary>Las versiones de los contratos que Recaudo usa hoy (lo que guarda <c>RCD_Setup</c>). (nuevo)</summary>
public sealed record VersionesDeContratosDto(int Obligations, int Accounts, DateOnly? AdoptedFrom);

/// <summary>
/// <c>POST /api/receipts/activation/contract-versions</c> (<c>Receipts.Activation.Activate</c>) <b>(nuevo)</b>: Recaudo adopta la versión 2 de
/// <c>ProveedorDeObligaciones</c> y de <c>ProveedorDeCuentas</c>. Suma las dos adopciones a la fila vigente <c>REC × ObligationsCollector</c> por
/// <see cref="EscritorDelRegistroDeIntegracion.AdoptarAsync"/> —cierra la vigente la víspera y abre otra con las de antes más las nuevas: la historia queda— y
/// actualiza <c>RCD_Setup.ContractVersionObligations/Accounts</c>, todo en el mismo guardado. Rige desde mañana (la fila del día de hoy ya contó para lo que se
/// aplicó hoy). Exige Recaudo activado (<c>Receipts.NotActivated</c>). Repetirlo con las v2 ya adoptadas responde las versiones vigentes sin escribir. (nuevo)
/// </summary>
public sealed record AdoptContractVersionCommand(int Obligations = 2, int Accounts = 2) : IRequest<Result<VersionesDeContratosDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AdoptContractVersionCommandValidator : AbstractValidator<AdoptContractVersionCommand>
{
    public AdoptContractVersionCommandValidator()
    {
        RuleFor(x => x.Obligations).Must(v => ContratosDeIntegracion.VersionesDelContratoDeObligaciones.Contains(v))
            .WithMessage("La versión del contrato de obligaciones no la implementa este despliegue.");
        RuleFor(x => x.Accounts).Must(v => ContratosDeIntegracion.VersionesDelContratoDeCuentas.Contains(v))
            .WithMessage("La versión del contrato de cuentas no la implementa este despliegue.");
    }
}

public sealed class AdoptContractVersionCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeRecaudo cerrojo,
    EscritorDelRegistroDeIntegracion registro,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<AdoptContractVersionCommand, Result<VersionesDeContratosDto>>
{
    public async Task<Result<VersionesDeContratosDto>> Handle(AdoptContractVersionCommand request, CancellationToken ct)
    {
        await cerrojo.TomarSetupAsync(exclusivo: true, ct: ct);
        var setup = await db.ReceiptsSetups.FirstAsync(ct);
        if (!setup.Activo) return Result.Failure<VersionesDeContratosDto>(ErroresDeRecaudo.NoActivado());
        if (setup.ContractVersionObligations >= request.Obligations && setup.ContractVersionAccounts >= request.Accounts)
            return Result.Success(new VersionesDeContratosDto(setup.ContractVersionObligations, setup.ContractVersionAccounts, null));

        var hoy = reloj.HoyLocal;
        var vigente = await db.IntegrationParticipants.AsNoTracking()
            .Where(p => p.Code == ClavesDeRecaudo.Codigo && p.Role == IntegrationParticipantRole.ObligationsCollector && p.ValidTo == null && !p.IsDeleted)
            .Select(p => (DateOnly?)p.ValidFrom).FirstOrDefaultAsync(ct);
        // Rige desde mañana, y nunca el mismo día en que abrió la fila vigente (el registro no admite dos filas que empiecen el mismo día).
        var desde = hoy.AddDays(1);
        if (vigente is { } abrio && abrio >= desde) desde = abrio.AddDays(1);
        var nuevas = new List<VersionAdoptada>();
        if (request.Obligations > setup.ContractVersionObligations) nuevas.Add(new(ContratosDeIntegracion.ProveedorDeObligaciones, request.Obligations));
        if (request.Accounts > setup.ContractVersionAccounts) nuevas.Add(new(ContratosDeIntegracion.ProveedorDeCuentas, request.Accounts));
        var adoptado = await registro.AdoptarAsync(ClavesDeRecaudo.Codigo, IntegrationParticipantRole.ObligationsCollector, string.Empty, nuevas, desde,
            "Recaudo adopta la versión 2 de los contratos (partida bancaria citada)", ct);
        if (adoptado.IsFailure) return Result.Failure<VersionesDeContratosDto>(adoptado.Error);

        var antes = new { obligations = setup.ContractVersionObligations, accounts = setup.ContractVersionAccounts };
        setup.ContractVersionObligations = (short)Math.Max(setup.ContractVersionObligations, request.Obligations);
        setup.ContractVersionAccounts = (short)Math.Max(setup.ContractVersionAccounts, request.Accounts);
        await auditoria.AgregarAsync("Receipts.ContractVersionAdopted", "ReceiptsSetup", setup.PublicId, antes,
            new { obligations = setup.ContractVersionObligations, accounts = setup.ContractVersionAccounts, from = desde }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new VersionesDeContratosDto(setup.ContractVersionObligations, setup.ContractVersionAccounts, desde));
    }
}
