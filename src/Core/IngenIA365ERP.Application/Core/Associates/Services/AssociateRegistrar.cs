using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Associates.Contracts;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Associates.Services;

/// <summary>
/// Cómo se afilia un asociado, en un solo sitio (feature 008). Recibe la persona ya
/// resuelta —existente y rastreada, o recién agregada por <c>PersonFactory</c> y sin Id— y
/// deja en el contexto la afiliación enlazada por navegación y la bandera <c>IsAssociate</c>
/// encendida, <b>sin guardar</b>: el que llama hace el único <c>SaveChangesAsync</c>.
/// </summary>
public sealed class AssociateRegistrar(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
{
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

        var associate = new Associate
        {
            Person = person,
            PersonId = person.Id,
            JoinDate = input.JoinDate,
            ContributionRate = input.ContributionRate,
            EmployerCompanyId = await IdDeAsync(() => context.EmployerCompanies, input.EmployerCompanyPublicId, ct),
            BranchId = await IdDeAsync(() => context.Branches, input.BranchPublicId, ct),
            SectionId = await IdDeAsync(() => context.Sections, input.SectionPublicId, ct),
            CommitteeId = await IdDeAsync(() => context.Committees, input.CommitteePublicId, ct),
            CategoryRating = input.CategoryRating,
            AssociateClass = input.AssociateClass,
            PaymentType = input.PaymentType,
            ContributionPledged = input.ContributionPledged,
            Status = "A",
            ExternalEmployerName = input.ExternalEmployerName,
            ExternalEmploymentStartDate = input.ExternalEmploymentStartDate,
            ExternalSalary = input.ExternalSalary,
            ExternalSalaryType = input.ExternalSalaryType,
            ExternalSeverance = input.ExternalSeverance,
            ExternalSeveranceFund = input.ExternalSeveranceFund,
            DepositBankId = await IdDeAsync(() => context.Banks, input.DepositBankPublicId, ct),
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

        return Result.Success(associate);
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
