using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Payroll.Policies;

/// <summary>
/// Errores de las políticas por empresa (feature 010, contracts/api.md §10.1). Los códigos son
/// el contrato con la pantalla; el mensaje va en español y nombra la clave y, cuando aplica,
/// los valores que sí se admiten, porque «valor inválido» a secas no le dice a la contadora
/// qué escribir.
/// </summary>
public static class CompanyPolicyErrors
{
    public static Error KeyUnknown(string clave) =>
        new("Payroll.CompanyPolicy.KeyUnknown", $"No existe la política «{clave}». Las claves son un catálogo cerrado.");

    public static Error ValueInvalid(string clave, string? valor, IReadOnlyList<string> admitidos, string? formato) =>
        new ErrorConDatos("Payroll.CompanyPolicy.ValueInvalid",
            admitidos.Count > 0
                ? $"«{valor}» no es un valor admitido para {clave}. Admite: {string.Join(", ", admitidos)}."
                : $"«{valor}» no tiene el formato que exige {clave}: {formato}.",
            new { allowed = admitidos, format = formato });

    public static Error VersionOverlaps(string clave, DateOnly desde, DateOnly? hastaExistente, DateOnly desdeExistente) =>
        new("Payroll.CompanyPolicy.VersionOverlaps",
            hastaExistente is { } h
                ? $"La vigencia de {clave} desde {desde:dd/MM/yyyy} se cruza con la que va del {desdeExistente:dd/MM/yyyy} al {h:dd/MM/yyyy}. Cerrá la anterior (closePrevious) o elegí otra fecha."
                : $"La vigencia de {clave} desde {desde:dd/MM/yyyy} se cruza con la abierta desde {desdeExistente:dd/MM/yyyy}. Cerrá la anterior (closePrevious) o elegí otra fecha.");

    public static Error RetroactiveNotAllowed(string clave, DateOnly desde, int corridas, DateOnly primera) =>
        new ErrorConDatos("Payroll.CompanyPolicy.RetroactiveNotAllowed",
            $"{clave} cambia aportes ya contabilizados: hay {corridas} corrida(s) aprobada(s) desde el {primera:dd/MM/yyyy} que la leyeron. Reversalas antes o elegí una vigencia posterior a la última aprobada.",
            new { approvedRuns = corridas, firstApprovedAt = primera, validFrom = desde });
}
