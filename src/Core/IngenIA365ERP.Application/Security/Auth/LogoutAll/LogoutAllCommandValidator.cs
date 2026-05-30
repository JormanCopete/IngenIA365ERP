using FluentValidation;

namespace IngenIA365ERP.Application.Security.Auth.LogoutAll;

public sealed class LogoutAllCommandValidator : AbstractValidator<LogoutAllCommand>
{
    public LogoutAllCommandValidator()
    {
        // No payload; identidad viene del ICurrentUserService en el handler.
    }
}
