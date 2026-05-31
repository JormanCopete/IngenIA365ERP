using System.Net.Mail;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.ValueObjects;

/// <summary>
/// Correo electrónico normalizado. La igualdad se calcula sobre <see cref="Normalized"/>
/// (UPPER-invariant), lo que hace el lookup insensible a mayúsculas/minúsculas (FR-002).
/// </summary>
public sealed class Email : ValueObject
{
    public string Value { get; }
    public string Normalized { get; }

    private Email(string value, string normalized)
    {
        Value = value;
        Normalized = normalized;
    }

    public static Email Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("El correo no puede estar vacío.", nameof(raw));

        var trimmed = raw.Trim();
        if (!IsValidFormat(trimmed))
            throw new ArgumentException($"'{raw}' no es un correo válido.", nameof(raw));

        return new Email(trimmed, trimmed.ToUpperInvariant());
    }

    public static bool TryCreate(string? raw, out Email? email)
    {
        try
        {
            email = Create(raw ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            email = null;
            return false;
        }
    }

    public static string Normalize(string raw) =>
        string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToUpperInvariant();

    private static bool IsValidFormat(string candidate)
    {
        try
        {
            var addr = new MailAddress(candidate);
            return string.Equals(addr.Address, candidate, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Normalized;
    }

    public override string ToString() => Value;
}
