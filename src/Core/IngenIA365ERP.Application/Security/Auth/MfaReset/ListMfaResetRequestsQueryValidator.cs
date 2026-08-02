using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.MfaReset;

public sealed class ListMfaResetRequestsQueryValidator
    : AbstractValidator<ListMfaResetRequestsQuery>
{
    public ListMfaResetRequestsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("El tamaño de página va de 1 a 100.");
    }
}
