namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>Una oficina de la cooperativa (<c>GET /api/admin/branches</c>, BranchDto de Administración).</summary>
public sealed record OficinaDeLaCooperativa(Guid PublicId, string Code, string Name, bool IsActive = true);

/// <summary>Una agencia (sucursal contable, <c>COR_Branches</c>) con su vínculo a la oficina.</summary>
public sealed record AgenciaConOficina(Guid PublicId, string Name, Guid? TenantBranchPublicId);

/// <summary>Opción del selector de oficina de Agencias; <see cref="Ocupada"/> la deshabilita.</summary>
public sealed record OpcionDeOficina(Guid PublicId, string Etiqueta, bool Ocupada);

/// <summary>
/// El vínculo agencia → oficina en la pantalla de Agencias (feature 009, FR-035; R7). Del vínculo
/// (<c>COR_Branches.TenantBranchPublicId</c>) depende que el alcance de sucursal de un usuario con oficinas asignadas
/// alcance a esa agencia en Contabilidad y en Cartera. El servidor es quien decide «una oficina, una sola agencia»
/// (<c>Branch.OfficeAlreadyLinked</c>); aquí sólo se anticipa: la oficina que ya tiene otra agencia aparece con su nombre y
/// deshabilitada. (nuevo)
/// </summary>
public static class VinculoAgenciaOficina
{
    /// <summary>Lo que significa una agencia sin oficina, dicho igual en el aviso y en la ayuda del formulario.</summary>
    public const string ConsecuenciaSinOficina =
        "Un usuario con oficinas asignadas no ve ni digita sus movimientos en Contabilidad; quien no tiene oficinas asignadas la ve como siempre.";

    /// <summary>
    /// Las opciones del selector al editar <paramref name="editando"/> (nulo al crear): las oficinas activas y, si la agencia está
    /// vinculada a una inactiva, también ésa —si no, el selector la perdería y guardar quitaría el vínculo sin que nadie lo pidiera—.
    /// La oficina vinculada a otra agencia viene marcada con el nombre de esa agencia.
    /// </summary>
    public static IReadOnlyList<OpcionDeOficina> Opciones(
        IEnumerable<OficinaDeLaCooperativa> oficinas, IEnumerable<AgenciaConOficina> agencias, Guid? editando)
    {
        var propia = agencias.FirstOrDefault(a => a.PublicId == editando)?.TenantBranchPublicId;
        var ocupadas = agencias
            .Where(a => a.TenantBranchPublicId is not null && a.PublicId != editando)
            .GroupBy(a => a.TenantBranchPublicId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Name);

        return oficinas
            .Where(o => o.IsActive || o.PublicId == propia)
            .Select(o =>
            {
                var etiqueta = $"{o.Code} · {o.Name}";
                if (!o.IsActive) etiqueta += " (inactiva)";
                var ocupada = ocupadas.TryGetValue(o.PublicId, out var agencia);
                if (ocupada) etiqueta += $" — vinculada a «{agencia}»";
                return new OpcionDeOficina(o.PublicId, etiqueta, ocupada);
            })
            .OrderBy(o => o.Ocupada)
            .ThenBy(o => o.Etiqueta, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// El texto de la columna Oficina. <paramref name="oficinasVisibles"/> falso = quien mira no tiene <c>Admin.Branches.View</c>:
    /// se dice que hay vínculo sin nombrar la oficina.
    /// </summary>
    public static string Texto(Guid? vinculo, IEnumerable<OficinaDeLaCooperativa> oficinas, bool oficinasVisibles)
    {
        if (vinculo is null) return "Sin oficina";
        var oficina = oficinas.FirstOrDefault(o => o.PublicId == vinculo);
        if (oficina is not null) return $"{oficina.Code} · {oficina.Name}{(oficina.IsActive ? "" : " (inactiva)")}";
        return oficinasVisibles ? "Oficina no encontrada" : "Vinculada";
    }

    /// <summary>Las agencias que todavía no tienen oficina, por nombre.</summary>
    public static IReadOnlyList<string> SinOficina(IEnumerable<AgenciaConOficina> agencias) =>
        agencias.Where(a => a.TenantBranchPublicId is null).Select(a => a.Name)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
}
