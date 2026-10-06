using IngenIA365ERP.Application.Common.Integration;

namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// Feature 013 (C68; data-model parte 3 §4.14; SC-015): el punto de extensión del alcance de Cartera sobre los destinatarios de
/// una alerta. Recibe a quienes ya resolvió <see cref="IDestinatariosPorPermiso"/> (permiso, bodega y punto) y deja sólo a
/// quienes tienen la oficina y la línea de la alerta en su alcance. El defecto (<see cref="SinFiltroDeAlcanceDeAlertas"/>) no
/// filtra; la implementación con <c>IAlcanceDeCartera</c> llega con la fase de US17. Si no deja a nadie, la alerta va a
/// <c>CompanyAdmin</c> marcada <c>WithoutRecipient</c>, como toda alerta sin destinatario (SC-022). (nuevo)
/// </summary>
public interface IFiltroDeAlcanceDeAlertas
{
    Task<IReadOnlyList<DestinatarioDeAlerta>> FiltrarAsync(
        IReadOnlyList<DestinatarioDeAlerta> destinatarios,
        Guid? oficinaPublicId,
        Guid? lineaDeCreditoPublicId,
        CancellationToken ct);
}

/// <summary>El defecto hasta US17: no filtra por oficina ni por línea. (nuevo)</summary>
public sealed class SinFiltroDeAlcanceDeAlertas : IFiltroDeAlcanceDeAlertas
{
    public Task<IReadOnlyList<DestinatarioDeAlerta>> FiltrarAsync(
        IReadOnlyList<DestinatarioDeAlerta> destinatarios, Guid? oficinaPublicId, Guid? lineaDeCreditoPublicId, CancellationToken ct) =>
        Task.FromResult(destinatarios);
}

/// <summary>
/// Feature 013 (PM-15, C68; data-model parte 3 §4.14): de la lista de destinatarios de un tipo, los permisos del otro módulo no
/// reciben el caso. Con origen Cartera (<c>CAR</c>) se descartan <c>Inventory.*</c> y <c>Accounting.Inventory*</c>; con origen
/// Comercial (<c>INV</c>), <c>Lending.*</c> y <c>Accounting.Lending*</c>. Cualquier otro origen (o ninguno) deja la lista igual.
/// Así un tipo compartido (<c>Integracion.MensajeRechazado</c>) lleva en su versión la unión de los dos módulos y cada caso llega
/// sólo a los suyos. (nuevo)
/// </summary>
public static class FiltroPorModuloDeOrigen
{
    public static IReadOnlyList<string> Aplicar(IReadOnlyList<string> permisos, string? moduloDeOrigen) => moduloDeOrigen switch
    {
        ModulosDeOrigen.Cartera => permisos.Where(p => !EsDeInventario(p)).ToList(),
        ModulosDeOrigen.Inventario => permisos.Where(p => !EsDeCartera(p)).ToList(),
        _ => permisos,
    };

    private static bool EsDeInventario(string permiso) =>
        permiso.StartsWith("Inventory.", StringComparison.Ordinal) || permiso.StartsWith("Accounting.Inventory", StringComparison.Ordinal);

    private static bool EsDeCartera(string permiso) =>
        permiso.StartsWith("Lending.", StringComparison.Ordinal) || permiso.StartsWith("Accounting.Lending", StringComparison.Ordinal);
}
