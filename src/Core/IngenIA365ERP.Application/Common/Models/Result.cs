namespace IngenIA365ERP.Application.Common.Models;

/// <summary>
/// Resultado discriminado <c>Success | Failure</c> con código namespaced en
/// formato <c>Modulo.Condicion</c> (FR-048, principio IX). Convención:
/// el prefijo identifica el agrupador funcional y el sufijo la causa concreta.
/// Ej: <c>Security.Auth.InvalidCredentials</c>, <c>Concurrency.StaleRowVersion</c>,
/// <c>Validation.Invalid</c>, <c>Generic.NotFound</c>.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(string code, string message) => new(false, new Error(code, message));
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default!, false, error);
    public static Result<T> Failure<T>(string code, string message) => new(default!, false, new Error(code, message));
}

public class Result<T> : Result
{
    public T Value { get; }

    internal Result(T value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        Value = value;
    }

    public static implicit operator Result<T>(T value) => Success(value);
}

/// <summary>
/// Par <c>Code</c> + <c>Message</c>. El código sigue la convención
/// <c>Modulo.Condicion</c> (ASCII, PascalCase, sin espacios) y nunca se
/// internacionaliza — es el contrato de máquina-a-máquina. El <c>Message</c>
/// es texto en español, listo para el usuario (FR-048).
/// </summary>
public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    // Genéricos — el código del error específico debe preferirse cuando exista.
    public static readonly Error NullValue = new("Generic.NullValue", "El valor no puede ser nulo.");
    public static readonly Error NotFound = new("Generic.NotFound", "Recurso no encontrado.");
    public static readonly Error Unauthorized = new("Generic.Unauthorized", "No autorizado.");
    public static readonly Error Forbidden = new("Generic.Forbidden", "Acceso denegado.");
    public static readonly Error Conflict = new("Generic.Conflict", "Conflicto con el estado actual del recurso.");

    // Reservados de uso transversal.
    public static readonly Error Validation = new("Validation.Invalid", "La solicitud no superó la validación.");
    public static readonly Error StaleRowVersion = new("Concurrency.StaleRowVersion",
        "El registro fue modificado por otro usuario; refresca y vuelve a intentar.");
}
