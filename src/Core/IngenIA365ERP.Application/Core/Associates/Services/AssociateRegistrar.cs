using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.AssociateGroups;
using IngenIA365ERP.Application.Core.Associates.Contracts;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Associates.Services;

/// <summary>
/// Cómo se afilia un asociado, en un solo sitio (feature 008). Recibe la persona ya
/// resuelta —existente y rastreada, o recién agregada por <c>PersonFactory</c> y sin Id— y
/// deja en el contexto la afiliación enlazada por navegación y la bandera <c>IsAssociate</c>
/// encendida, <b>sin guardar</b>: el que llama hace el único <c>SaveChangesAsync</c>.
/// <para>
/// La plantilla <c>core.associates</c> (feature 016, US3) entra por <see cref="Construir"/>, la mitad que no consulta: resuelve sus
/// catálogos en bloque y no hace una consulta por fila, pero la fila y la bandera las escribe este mismo camino.
/// </para>
/// </summary>
public sealed class AssociateRegistrar(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
{
    /// <summary>El estado de un asociado vigente (<c>COR_Associates.Status</c>).</summary>
    public const string EstadoVigente = "A";

    /// <summary>El estado de un asociado retirado.</summary>
    public const string EstadoRetirado = "R";

    public const string RetiroEsDeCarteraCode = "Associate.WithdrawalOwnedByLending";

    /// <summary>
    /// Feature 014 (T102): el retiro lo tramita la Cartera; Asociados no retira por su cuenta. Es el error si el cambio de
    /// <paramref name="estadoActual"/> a <paramref name="estadoNuevo"/> retira a un asociado que no lo estaba; nulo si no.
    /// </summary>
    public static Error? RetiroQueEsDeCartera(string? estadoActual, string? estadoNuevo) =>
        EsRetirado(estadoNuevo) && !EsRetirado(estadoActual)
            ? new Error(RetiroEsDeCarteraCode,
                "El retiro del asociado se radica y se aprueba en Cartera (Retiros de asociado): el estado cambia solo al aprobarse.")
            : null;

    public static bool EsRetirado(string? estado) =>
        string.Equals(estado?.Trim(), EstadoRetirado, StringComparison.OrdinalIgnoreCase);

    public async Task<Result<Associate>> PrepareAsync(Person person, AssociateInput input, CancellationToken ct)
    {
        if (person.Id != 0)
        {
            var yaEsAsociado = await context.Associates.AsNoTracking()
                .AnyAsync(a => a.PersonId == person.Id && !a.IsDeleted, ct);
            if (yaEsAsociado)
                return Result.Failure<Associate>(new Error("Associate.AlreadyExists",
                    "Esta persona ya esta registrada como asociado."));
        }

        int? grupoId = null;
        if (input.AssociateGroupPublicId is { } grupoPublicId)
        {
            var grupo = await context.AssociateGroups.AsNoTracking()
                .FirstOrDefaultAsync(g => g.PublicId == grupoPublicId && !g.IsDeleted, ct);
            if (grupo is null) return Result.Failure<Associate>(AssociateGroupErrors.NotFound());
            if (!grupo.IsActive) return Result.Failure<Associate>(AssociateGroupErrors.Inactive());
            grupoId = grupo.Id;
        }

        var referencias = new ReferenciasDeAfiliacion(
            grupoId,
            await IdDeAsync(() => context.EmployerCompanies, input.EmployerCompanyPublicId, ct),
            await IdDeAsync(() => context.Branches, input.BranchPublicId, ct),
            await IdDeAsync(() => context.Sections, input.SectionPublicId, ct),
            await IdDeAsync(() => context.Committees, input.CommitteePublicId, ct),
            await IdDeAsync(() => context.Banks, input.DepositBankPublicId, ct));

        return Result.Success(Construir(person, input, referencias));
    }

    /// <summary>
    /// Arma la afiliación, la agrega al contexto y enciende la bandera <b>sin consultar nada</b>: la mitad de
    /// <see cref="PrepareAsync"/> que no va a la base. Quien la llama ya comprobó que la persona no tiene afiliación y resolvió los
    /// catálogos. Sin <paramref name="historia"/> nace vigente, como en el alta; con ella, en el estado de la implantación (decisión
    /// D8 de la 016): un asociado que ya se había retirado antes de entrar al ERP, sin trámite en Cartera.
    /// </summary>
    public Associate Construir(Person person, AssociateInput input, ReferenciasDeAfiliacion referencias, HistoriaDeAfiliacion? historia = null)
    {
        var associate = new Associate
        {
            AssociateGroupId = referencias.GrupoId,
            Person = person,
            PersonId = person.Id,
            JoinDate = input.JoinDate,
            ContributionRate = input.ContributionRate,
            EmployerCompanyId = referencias.EmpresaId,
            BranchId = referencias.SucursalId,
            SectionId = referencias.SeccionId,
            CommitteeId = referencias.ComiteId,
            CategoryRating = input.CategoryRating,
            AssociateClass = input.AssociateClass,
            PaymentType = input.PaymentType,
            ContributionPledged = input.ContributionPledged,
            DeductionPeriod = input.DeductionPeriod,
            DeductionType = input.DeductionType,
            Status = historia?.Estado ?? EstadoVigente,
            WithdrawalDate = historia?.FechaRetiro,
            WithdrawalReasonId = historia?.MotivoRetiroId,
            RejoinDate = historia?.FechaReingreso,
            ExternalEmployerName = input.ExternalEmployerName,
            ExternalEmploymentStartDate = input.ExternalEmploymentStartDate,
            ExternalSalary = input.ExternalSalary,
            ExternalSalaryType = input.ExternalSalaryType,
            ExternalSeverance = input.ExternalSeverance,
            ExternalSeveranceFund = input.ExternalSeveranceFund,
            DepositBankId = referencias.BancoId,
            DepositBankAccountNumber = input.DepositBankAccountNumber,
            DepositBankAccountType = input.DepositBankAccountType,
            SpouseEmployer = input.SpouseEmployer,
            SpouseSalary = input.SpouseSalary,
            SpousePosition = input.SpousePosition,
            SpouseProfession = input.SpouseProfession,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.Associates.Add(associate);

        // La bandera derivada la escribe quien crea la fila hija (Principio V).
        person.IsAssociate = true;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        return associate;
    }

    /// <summary>Id interno de un catálogo por su PublicId; nulo si no viene o no existe (como hacía el handler).</summary>
    private static async Task<int?> IdDeAsync<T>(Func<IQueryable<T>> catalogo, Guid? publicId, CancellationToken ct)
        where T : Domain.Common.BaseEntity
    {
        if (publicId is not { } id) return null;
        return await catalogo().AsNoTracking()
            .Where(x => x.PublicId == id && !x.IsDeleted)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
    }
}

/// <summary>Los Id internos de los catálogos de la afiliación, ya resueltos.</summary>
public sealed record ReferenciasDeAfiliacion(
    int? GrupoId = null, int? EmpresaId = null, int? SucursalId = null, int? SeccionId = null, int? ComiteId = null, int? BancoId = null);

/// <summary>El estado con que nace una afiliación de implantación, con su retiro y su reingreso históricos.</summary>
public sealed record HistoriaDeAfiliacion(string Estado, DateOnly? FechaRetiro, int? MotivoRetiroId, DateOnly? FechaReingreso);
