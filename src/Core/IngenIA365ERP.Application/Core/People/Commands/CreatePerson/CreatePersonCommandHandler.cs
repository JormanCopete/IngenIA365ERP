using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

public class CreatePersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePersonCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        var person = new Person
        {
            LastName = request.LastName,
            FirstName = request.FirstName,
            TaxId = request.TaxId,
            TaxIdCheckDigit = request.TaxIdCheckDigit,
            IdType = request.IdType,
            PersonType = request.PersonType,
            BusinessName = request.BusinessName,
            Email = request.Email,
            Phone1 = request.Phone1,
            Phone2 = request.Phone2,
            Mobile = request.Mobile,
            Address = request.Address,
            CityId = request.CityId,
            Gender = request.Gender,
            MaritalStatus = request.MaritalStatus,
            DateOfBirth = request.DateOfBirth,
            EducationLevel = request.EducationLevel,
            IsAssociate = request.IsAssociate,
            IsEmployee = request.IsEmployee,
            Status = "A",
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.People.Add(person);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(person.PublicId);
    }
}
