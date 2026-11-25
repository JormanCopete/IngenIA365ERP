using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Origination;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// Qué se puede corregir de una solicitud devuelta (pedido del dueño del 2026-11-23): el parámetro
/// <c>Originacion.GruposEditablesAlDevolver</c> de su línea o, sin él, el de la cooperativa; ilegible, el defecto (todo, desde el
/// 2026-11-24). Un borrador se edita entero: la restricción es sólo para lo devuelto. Un solo sitio para la edición, los documentos y las
/// garantías. (nuevo)
/// </summary>
public sealed class GruposEditablesDeSolicitud(ReglasDeParametrosDeCartera parametros)
{
    /// <summary>Los grupos que admite la línea a la fecha.</summary>
    public async Task<GrupoEditableAlDevolver> LeerAsync(int lineaId, DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.OriginacionGruposEditablesAlDevolver, hoy,
            ambito: ParameterScopeKind.CreditLine, ambitoId: lineaId, ct: ct);
        return (leido.IsSuccess ? GruposEditablesAlDevolver.Leer(leido.Value.Texto) : null) ?? GruposEditablesAlDevolver.PorDefecto;
    }

    /// <summary>
    /// 2026-11-25 (pedido del dueño): ¿la línea (o la cooperativa) deja editar una solicitud radicada sin devolverla? Parámetro
    /// <c>Originacion.EditarRadicadaSinDevolver</c>; ilegible, lo de fábrica: sí.
    /// </summary>
    public async Task<bool> EditaRadicadaAsync(int lineaId, DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.OriginacionEditarRadicadaSinDevolver, hoy,
            ambito: ParameterScopeKind.CreditLine, ambitoId: lineaId, ct: ct);
        return !leido.IsSuccess || !bool.TryParse(leido.Value.Texto, out var si) || si;
    }

    /// <summary>
    /// Lo que se puede editar de esta solicitud ahora: todo en borrador; en devuelta, lo del parámetro; radicada y sin decidir, lo del mismo
    /// parámetro si <c>Originacion.EditarRadicadaSinDevolver</c> lo deja (desde el 2026-11-25); en otro estado, nada.
    /// </summary>
    public async Task<GrupoEditableAlDevolver> DeAsync(CreditApplication s, DateOnly hoy, CancellationToken ct) => s.Status switch
    {
        CreditApplicationStatus.Draft => GrupoEditableAlDevolver.Todos,
        CreditApplicationStatus.Returned => await LeerAsync(s.CreditLineId, hoy, ct),
        CreditApplicationStatus.Submitted or CreditApplicationStatus.PendingDecision when await EditaRadicadaAsync(s.CreditLineId, hoy, ct)
            => await LeerAsync(s.CreditLineId, hoy, ct),
        _ => GrupoEditableAlDevolver.Ninguno,
    };

    /// <summary>
    /// Si la solicitud está devuelta y el grupo no se puede corregir, el error <c>Lending.Application.FieldNotEditable</c>; si no, nulo. En
    /// borrador o en análisis decide quien llama (los documentos, por ejemplo, se adjuntan hasta la decisión).
    /// </summary>
    public Task<Error?> ExigirAsync(CreditApplication s, GrupoEditableAlDevolver grupo, DateOnly hoy, CancellationToken ct) =>
        ExigirAsync(s.Status, s.CreditLineId, grupo, hoy, ct);

    /// <summary>Lo mismo con el estado y la línea sueltos (la garantía conoce a su dueño así).</summary>
    public async Task<Error?> ExigirAsync(CreditApplicationStatus? estado, int lineaId, GrupoEditableAlDevolver grupo, DateOnly hoy, CancellationToken ct)
    {
        if (estado != CreditApplicationStatus.Returned) return null;
        var permitidos = await LeerAsync(lineaId, hoy, ct);
        return permitidos.HasFlag(grupo) ? null : ErroresDeOriginacion.NoCorregible([grupo]);
    }
}
