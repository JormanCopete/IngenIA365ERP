using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.DocumentTypes;

/// <summary>
/// Arma <see cref="DocumentTypeDto"/> (feature 012, T150; contracts/api.md §8): bodegas y canal por los maestros, el
/// consecutivo vigente hoy y el historial, la política de aprobación vigente (<c>COR_ApprovalPolicies</c>) y el modo de
/// paso vigente (<c>LectorDeParametros</c>), los dos de sólo lectura. (nuevo)
/// </summary>
public sealed class VistaDeTiposDeDocumento(
    IApplicationDbContext db,
    IMaestrosDelDocumento maestros,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
{
    public async Task<IReadOnlyList<DocumentTypeDto>> ArmarAsync(IReadOnlyList<InventoryDocumentType> tipos, bool conHistorial, CancellationToken ct)
    {
        if (tipos.Count == 0) return [];
        var hoy = reloj.HoyLocal;
        var ids = tipos.Select(t => t.Id).ToList();
        var publicos = tipos.Select(t => (Guid?)t.PublicId).ToList();

        var secuencias = await db.DocumentSequences.AsNoTracking().Where(s => ids.Contains(s.DocumentTypeId))
            .OrderBy(s => s.ValidFrom).ToListAsync(ct);
        var bodegasDeTipos = await db.DocumentTypeWarehouses.AsNoTracking().Where(w => ids.Contains(w.DocumentTypeId)).ToListAsync(ct);
        var bodegas = (await maestros.BodegasPorIdAsync(bodegasDeTipos.Select(w => w.WarehouseId).Distinct().ToList(), ct)).ToDictionary(b => b.Id);
        var canales = (await maestros.CanalesDeVentaPorIdAsync(tipos.Select(t => t.SalesChannelId).OfType<int>().Distinct().ToList(), ct))
            .ToDictionary(c => c.Id);
        var politicas = await db.ApprovalPolicies.AsNoTracking().Include(p => p.Levels)
            .Where(p => p.Module == ApprovalPolicy.ModuloInventario && p.Subject == ApprovalSubjects.DocumentConfirmation
                        && publicos.Contains(p.DocumentTypePublicId))
            .ToListAsync(ct);

        var resultado = new List<DocumentTypeDto>(tipos.Count);
        foreach (var t in tipos)
        {
            var clase = ClasesDeDocumento.De(t.Class);
            var propias = secuencias.Where(s => s.DocumentTypeId == t.Id).Select(Secuencia).ToList();
            var vigente = secuencias.Where(s => s.DocumentTypeId == t.Id && s.VigenteEn(hoy)).OrderByDescending(s => s.ValidFrom).FirstOrDefault();
            var politica = politicas.Where(p => p.DocumentTypePublicId == t.PublicId && p.VigenteEn(hoy)).OrderByDescending(p => p.Version).FirstOrDefault();

            resultado.Add(new DocumentTypeDto(
                t.PublicId, t.Code, t.Name, t.Class, clase.Group, clase.IsFiscal, clase.NumberedBy,
                clase.NumberedBy == NumberedBy.DianResolution ? t.FiscalPrefix ?? string.Empty : vigente?.Prefix ?? string.Empty,
                new CamposObligatoriosDto(t.RequiresCounterparty, t.RequiresCostCenter || clase.RequiresCostCenter, t.RequiresReason, t.RequiresExternalReference),
                bodegasDeTipos.Where(w => w.DocumentTypeId == t.Id && bodegas.ContainsKey(w.WarehouseId))
                    .Select(w => bodegas[w.WarehouseId]).Select(b => new ReferenciaDto(b.PublicId, b.Code, b.Name)).ToList(),
                t.SalesChannelId is int c && canales.TryGetValue(c, out var canal) ? new ReferenciaDto(canal.PublicId, canal.Code, canal.Name) : null,
                t.IsTaxableWithdrawal, t.VatNonDeductible, t.AllowsFutureDate,
                vigente is null ? null : Secuencia(vigente),
                politica is null ? null : new PoliticaDelTipoDto(politica.PublicId, politica.Version,
                    politica.Levels.OrderBy(l => l.Order).Select(l => new NivelDePoliticaDelTipoDto(l.Order, l.Threshold, l.PermissionCode)).ToList()),
                await ModoDePasoAsync(t, clase, hoy, ct),
                t.IsSeeded, t.IsActive,
                conHistorial ? propias : null));
        }
        return resultado;
    }

    public static DocumentSequenceDto Secuencia(DocumentSequence s) => new(s.PublicId, s.Prefix, s.NextValue, s.ValidFrom, s.ValidTo);

    private async Task<ModoDePasoDelTipoDto?> ModoDePasoAsync(InventoryDocumentType tipo, DescripcionDeClase clase, DateOnly hoy, CancellationToken ct)
    {
        if (!ConfirmacionDeDocumento.EmiteNegocioAContabilidad(clase)) return null;

        var modo = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, hoy, ParameterScopeKind.DocumentType, tipo.Id, ct);
        var granularidad = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadGranularidad, hoy, ParameterScopeKind.DocumentType, tipo.Id, ct);
        var disparador = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadDisparadorDeLote, hoy, ParameterScopeKind.DocumentType, tipo.Id, ct);
        var hora = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadHoraDeLote, hoy, ParameterScopeKind.DocumentType, tipo.Id, ct);

        var texto = modo.IsSuccess ? modo.Value.Texto : string.Empty;
        var heredado = !modo.IsSuccess || modo.Value.Vigencia is null || modo.Value.Vigencia.ScopeKind != ParameterScopeKind.DocumentType;
        return new ModoDePasoDelTipoDto(
            texto switch { "PorLotes" => PostingMode.Batch, "NoPasa" => PostingMode.NotPosted, _ => PostingMode.Online },
            heredado,
            clase.Chain,
            granularidad.IsSuccess ? granularidad.Value.Texto : string.Empty,
            disparador.IsSuccess ? disparador.Value.Texto : string.Empty,
            hora.IsSuccess && !string.IsNullOrWhiteSpace(hora.Value.Texto) ? hora.Value.Texto : null);
    }
}
