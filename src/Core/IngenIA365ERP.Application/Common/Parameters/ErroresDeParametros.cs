using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Parametros;

namespace IngenIA365ERP.Application.Common.Parameters;

/// <summary>
/// Los errores de los parámetros con vigencia (feature 012, T21; contracts/api.md §7; decisiones-transversales
/// §2.17). <c>Parameters.KeyNotFound</c> es 404 (lo mapea <c>ErrorEnvelopeFilter</c>, porque no termina en
/// <c>.NotFound</c>); los demás, 422 con <c>data</c> para que la pantalla diga qué hacer. (nuevo)
/// </summary>
public static class ErroresDeParametros
{
    public const string CodigoClaveInexistente = "Parameters.KeyNotFound";
    public const string CodigoValorNoAdmitido = "Parameters.ValueNotAllowed";
    public const string CodigoAmbitoNoAdmitido = "Parameters.ScopeNotAllowed";
    public const string CodigoSeCruza = "Parameters.Overlaps";
    public const string CodigoPermisoRequerido = "Parameters.PermissionRequired";
    public const string CodigoRequiereInicioDePeriodo = "Parameters.RequiresPeriodStart";
    public const string CodigoEnPeriodoCerrado = "Parameters.ValidFromInClosedPeriod";

    /// <summary>§7 (US3, T286): <c>Costeo.Metodo</c> y <c>Costeo.Ambito</c> sólo desde el primer día de un período abierto sin movimientos posteriores.</summary>
    public static Error RequiereInicioDePeriodo(string clave, DateOnly? earliestAllowed) => new ErrorConDatos(CodigoRequiereInicioDePeriodo,
        earliestAllowed is { } desde
            ? $"«{clave}» sólo cambia desde el primer día de un período abierto sin movimientos posteriores: la primera fecha posible es el {desde:yyyy-MM-dd}."
            : $"«{clave}» sólo cambia desde el primer día de un período abierto sin movimientos posteriores.",
        new { earliestAllowed });

    /// <summary>§7 (US3, T286): <c>validFrom</c> dentro de un período de inventario cerrado.</summary>
    public static Error EnPeriodoCerrado(DateOnly lastClosedDate) => new ErrorConDatos(CodigoEnPeriodoCerrado,
        $"La vigencia no puede empezar en un período de inventario cerrado (el último cierre es del {lastClosedDate:yyyy-MM-dd}): use una fecha posterior.",
        new { lastClosedDate });

    public static Error ClaveInexistente(string? modulo, string? clave) => new(CodigoClaveInexistente,
        $"No existe el parámetro «{modulo}/{clave}».");

    public static Error ValorNoAdmitido(DefinicionDeParametro definicion, string? valor) => new ErrorConDatos(CodigoValorNoAdmitido,
        $"El valor «{valor}» no es admitido en «{definicion.Clave}». Elegí uno de los admitidos.",
        new { allowed = definicion.Admitidos(CatalogoDeParametros.EntregaVigente) });

    public static Error AmbitoNoAdmitido(DefinicionDeParametro definicion) => new ErrorConDatos(CodigoAmbitoNoAdmitido,
        $"«{definicion.Clave}» no admite ese ámbito.",
        new { allowed = definicion.AmbitosAdmitidos.Select(a => a.ToString()).ToArray() });

    public static Error SeCruza(DateOnly existente) => new ErrorConDatos(CodigoSeCruza,
        $"Ya hay una vigencia que empieza el {existente:yyyy-MM-dd}. Registrá la nueva desde después de esa fecha.",
        new { existingValidFrom = existente });

    public static Error PermisoRequerido(string permiso) => new ErrorConDatos(CodigoPermisoRequerido,
        "Este parámetro exige un permiso que no tenés. Pedíselo al administrador de la cooperativa.",
        new { permissionCode = permiso });

    public static Error FuenteLegalRequerida(DefinicionDeParametro definicion) => new(Error.Validation.Code,
        $"«{definicion.Clave}» exige la norma o el acta que respalda el valor (legalSource).");
}
