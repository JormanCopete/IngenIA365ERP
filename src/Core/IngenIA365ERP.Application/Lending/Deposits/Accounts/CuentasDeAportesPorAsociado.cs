using System.Globalization;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

/// <summary>
/// 2026-11-24 (pedido del dueño, cooperativas de transporte: el asociado aporta por cada vehículo): con
/// <see cref="ParametrosDeCartera.AportesVariasCuentasPorAsociado"/> (sí de fábrica) un asociado tiene varias cuentas de aportes vivas. Cada una se numera
/// <c>{código de la línea}-{usuario del documento}-{número elegido}</c> (<c>APORTES-16261749-2</c>): el número elegido es único para ese asociado y esa
/// línea, y el número completo sigue siendo único en la Cartera. Sin número elegido, el siguiente del asociado (1, 2, 3…). El mínimo no reducible sigue
/// siendo del asociado: se exige sobre la suma de sus cuentas. (nuevo)
/// </summary>
public static class CuentasDeAportesPorAsociado
{
    public const int LargoDelNumeroElegido = 6;

    public const string CodigoNumeroInvalido = "Lending.Contributions.AccountNumberInvalid";
    public const string CodigoNumeroTomado = "Lending.Contributions.AccountNumberTaken";
    public const string CodigoNumeroSinVarias = "Lending.Contributions.AccountNumberNotAllowed";


    private static readonly Regex Patron = new("^[A-Z0-9]{1,6}$", RegexOptions.Compiled);

    /// <summary>Si la cooperativa admite varias cuentas de aportes por asociado a <paramref name="fecha"/>; si el parámetro no se puede leer, su defecto (sí).</summary>
    public static async Task<bool> VariasAsync(ILectorDeParametros lector, DateOnly fecha, CancellationToken ct)
    {
        var leido = await lector.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.AportesVariasCuentasPorAsociado, fecha, ct: ct);
        return leido.IsFailure || leido.Value.Valor is not false;
    }

    /// <inheritdoc cref="VariasAsync(ILectorDeParametros, DateOnly, CancellationToken)"/>
    public static async Task<bool> VariasAsync(ReglasDeParametrosDeCartera parametros, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.AportesVariasCuentasPorAsociado, fecha, ct: ct);
        return leido.IsFailure || leido.Value.Valor is not false;
    }

    /// <summary>El usuario del documento de la persona (feature 017): el guardado o, si falta, el derivado de su tipo y número.</summary>
    public static string UsuarioDe(Person persona)
    {
        ArgumentNullException.ThrowIfNull(persona);
        return string.IsNullOrWhiteSpace(persona.Username)
            ? UsuarioDeDocumento.DerivarParaPersona(persona.IdType, persona.TaxId)
            : persona.Username.Trim().ToUpperInvariant();
    }

    public static string Prefijo(string codigoDeLinea, string usuario) => $"{codigoDeLinea}-{usuario}-";

    /// <summary>El número elegido en mayúsculas, o nulo si no viene (el siguiente). Letras y dígitos, hasta <see cref="LargoDelNumeroElegido"/>.</summary>
    public static Result<string?> Normalizar(string? elegido)
    {
        if (string.IsNullOrWhiteSpace(elegido)) return Result.Success<string?>(null);
        var normal = elegido.Trim().ToUpperInvariant();
        return Patron.IsMatch(normal)
            ? Result.Success<string?>(normal)
            : Result.Failure<string?>(new ErrorConDatos(CodigoNumeroInvalido,
                $"El número del aporte admite letras y dígitos, hasta {LargoDelNumeroElegido} (por ejemplo, 2 o el número del vehículo).",
                new { field = "contributionNumber", value = elegido }));
    }

    /// <summary>El siguiente número del asociado: uno más que el mayor número puramente numérico tras el <paramref name="prefijo"/>; 1 si no hay.</summary>
    public static string Siguiente(IEnumerable<string> numeros, string prefijo)
    {
        var mayor = 0L;
        foreach (var numero in numeros)
        {
            if (!numero.StartsWith(prefijo, StringComparison.Ordinal)) continue;
            if (long.TryParse(numero.AsSpan(prefijo.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > mayor) mayor = n;
        }
        return (mayor + 1).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// El número de una cuenta de aportes nueva de <paramref name="titular"/> en la línea <paramref name="codigoDeLinea"/>. Cuenta también las dadas de
    /// baja: el número de una cuenta no se reutiliza (el índice único lo cubre igual).
    /// </summary>
    public static async Task<Result<string>> NumerarAsync(IApplicationDbContext db, string codigoDeLinea, Person titular, string? elegido, CancellationToken ct)
    {
        var normal = Normalizar(elegido);
        if (normal.IsFailure) return Result.Failure<string>(normal.Error);
        var prefijo = Prefijo(codigoDeLinea, UsuarioDe(titular));
        var existentes = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => a.Number.StartsWith(prefijo)).Select(a => a.Number)
            .ToListAsync(ct);
        var numero = prefijo + (normal.Value ?? Siguiente(existentes, prefijo));
        if (numero.Length > MemberAccount.LargoDelNumero)
            return Result.Failure<string>(new ErrorConDatos(CodigoNumeroInvalido,
                $"El número de la cuenta ({numero}) pasaría de {MemberAccount.LargoDelNumero} caracteres: use un número más corto.",
                new { field = "contributionNumber", number = numero }));
        return existentes.Contains(numero, StringComparer.Ordinal)
            ? Result.Failure<string>(new ErrorConDatos(CodigoNumeroTomado, $"El asociado ya tiene la cuenta de aportes {numero}: elija otro número.",
                new { field = "contributionNumber", existingAccountNumber = numero }))
            : Result.Success(numero);
    }

    public static Error NumeroSinVarias() => new ErrorConDatos(CodigoNumeroSinVarias,
        "La cooperativa tiene una sola cuenta de aportes por asociado: el número lo pone el sistema. Para admitir varias, actívelo en Parámetros de Cartera.",
        new { field = "contributionNumber" });

    public static Error NumeroSoloDeAportes() => new ErrorConDatos(CodigoNumeroInvalido,
        "El número del aporte es de una cuenta de aportes: en una de captación el número lo pone el sistema.", new { field = "contributionNumber" });

    /// <summary>Las cuentas de aportes vivas de una persona (no canceladas ni dadas de baja), en orden de apertura.</summary>
    public static IQueryable<MemberAccount> VivasDe(IApplicationDbContext db, int personaId) => db.MemberAccounts
        .Where(a => a.HolderPersonId == personaId && a.Nature == LineNature.Contribution && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
        .OrderBy(a => a.Id);
}
