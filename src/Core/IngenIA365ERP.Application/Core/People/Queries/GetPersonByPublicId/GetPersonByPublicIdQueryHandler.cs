using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Queries.GetPersonByPublicId;

public class GetPersonByPublicIdQueryHandler(
    IApplicationDbContext context,
    IMapper mapper)
    : IRequestHandler<GetPersonByPublicIdQuery, Result<PersonDto>>
{
    public async Task<Result<PersonDto>> Handle(
        GetPersonByPublicIdQuery request,
        CancellationToken cancellationToken)
    {
        var person = await context.People
            .AsNoTracking()
            .Include(p => p.City)
            .FirstOrDefaultAsync(p => p.PublicId == request.PublicId && !p.IsDeleted, cancellationToken);

        if (person is null)
            return Result.Failure<PersonDto>(Error.NotFound);

        var dto = mapper.Map<PersonDto>(person);
        return Result.Success(dto);
    }
}
