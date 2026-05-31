using FluentValidation;

namespace IngenIA365ERP.Application.Invitations.AcceptInvitation;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El token de la invitación es obligatorio.")
            .MaximumLength(512);

        // XOR estricto entre las 3 ramas: exactamente UNA debe estar activa.
        RuleFor(x => x)
            .Must(ExactlyOneBranchSelected)
            .WithErrorCode("Invitation.AmbiguousBranch")
            .WithMessage("Debe indicarse exactamente una opción: registro nuevo, " +
                         "credenciales existentes, o uso de sesión activa.");

        // Política de password: ≥ 12 chars (FR-043). El Pwned check vive en
        // ICentralIdentityProvider.CreateUserAsync / ChangePasswordAsync.
        When(x => x.Registration is not null, () =>
        {
            RuleFor(x => x.Registration!.Password)
                .NotEmpty().WithMessage("La contraseña es obligatoria.")
                .MinimumLength(12).WithMessage("La contraseña debe tener al menos 12 caracteres.")
                .MaximumLength(256);
        });

        When(x => x.ExistingCredentials is not null, () =>
        {
            RuleFor(x => x.ExistingCredentials!.Password)
                .NotEmpty().WithMessage("La contraseña es obligatoria.")
                .MaximumLength(256);
        });
    }

    private static bool ExactlyOneBranchSelected(AcceptInvitationCommand cmd)
    {
        var count =
            (cmd.Registration is not null ? 1 : 0) +
            (cmd.ExistingCredentials is not null ? 1 : 0) +
            (cmd.UseActiveSession ? 1 : 0);
        return count == 1;
    }
}
