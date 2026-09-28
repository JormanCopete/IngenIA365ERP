using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// Los prefijos de las notas electrónicas de Inventario (feature 012, I4, T710; data-model §27 duda 9): la implementación de
/// <see cref="IPrefijosDeModulos"/> sobre <c>INV_DocumentSequences</c> de los tipos de nota (crédito, débito, ajuste del documento
/// equivalente y ajuste del documento soporte), en cualquier vigencia —lo emitido con un prefijo viejo sigue ocupándolo—. Y la regla
/// inversa: <see cref="ValidarPrefijoDeNotaAsync"/>, que llaman los comandos de secuencias antes de dar a una nota un prefijo que ya es
/// de una resolución DIAN. (nuevo)
/// </summary>
public sealed class PrefijosDeInventario(IApplicationDbContext db) : IPrefijosDeModulos
{
    /// <summary>Las clases de nota electrónica: numeran con su propia secuencia y comparten la unicidad con las resoluciones.</summary>
    public static readonly IReadOnlySet<DocumentClass> ClasesDeNota = new HashSet<DocumentClass>
    {
        DocumentClass.CreditNote, DocumentClass.DebitNote, DocumentClass.PosAdjustmentNote, DocumentClass.SupportDocumentAdjustmentNote,
    };

    public async Task<PrefijoDeModulo?> UsoDeAsync(string prefijo, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prefijo)) return null;
        var clases = ClasesDeNota.ToArray();
        var tipo = await db.DocumentSequences.AsNoTracking()
            .Where(s => s.Prefix == prefijo && s.DocumentType != null && clases.Contains(s.DocumentType.Class))
            .Select(s => new { s.DocumentType!.Code, s.DocumentType.Name })
            .FirstOrDefaultAsync(ct);
        return tipo is null ? null : new PrefijoDeModulo(prefijo, $"el tipo de documento {tipo.Code} ({tipo.Name}) de Inventario");
    }

    /// <summary>
    /// <c>ElectronicInvoicing.Resolution.PrefixInUse</c> si <paramref name="clase"/> es una nota electrónica y <paramref name="prefijo"/> ya
    /// es el de una resolución DIAN (de cualquier ambiente). Un prefijo vacío o de otra clase no se revisa.
    /// </summary>
    public static async Task<Result> ValidarPrefijoDeNotaAsync(IApplicationDbContext db, DocumentClass clase, string prefijo, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(prefijo) || !ClasesDeNota.Contains(clase)) return Result.Success();
        var resolucion = await db.DianNumberingResolutions.AsNoTracking()
            .Where(r => r.Prefix == prefijo)
            .Select(r => r.ResolutionNumber)
            .FirstOrDefaultAsync(ct);
        return resolucion is null
            ? Result.Success()
            : Result.Failure(ErroresDeNumeracionYConfiguracion.PrefixInUse(prefijo, $"la resolución DIAN {resolucion}"));
    }
}
