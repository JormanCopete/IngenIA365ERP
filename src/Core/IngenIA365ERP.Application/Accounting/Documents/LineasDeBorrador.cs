using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Documents;

/// <summary>
/// Traduce las líneas digitadas (cuenta por código, referencias por <c>PublicId</c>) a
/// <see cref="PostingLine"/> para el contrato, y las líneas guardadas de un borrador de vuelta al
/// contrato para contabilizarlo. Una referencia que no existe se traduce a <c>-1</c>: las reglas
/// la marcan «inválida» en la línea que corresponde (FR-041) y, al guardar, esa misma infracción
/// impide persistir la línea porque la clave foránea no lo admitiría.
/// </summary>
public static class LineasDeBorrador
{
    public const int Inexistente = -1;

    public sealed record Referencias(
        IReadOnlyDictionary<string, ChartOfAccount> Cuentas,
        IReadOnlyDictionary<Guid, int> Sucursales,
        IReadOnlyDictionary<Guid, int> Centros,
        IReadOnlyDictionary<Guid, int> Terceros)
    {
        public int? Sucursal(Guid? id) => id is null ? null : Sucursales.GetValueOrDefault(id.Value, Inexistente);
        public int? Centro(Guid? id) => id is null ? null : Centros.GetValueOrDefault(id.Value, Inexistente);
        public int? Tercero(Guid? id) => id is null ? null : Terceros.GetValueOrDefault(id.Value, Inexistente);
    }

    /// <summary>Consulta una sola vez todo lo que las líneas referencian.</summary>
    public static async Task<Referencias> ResolverAsync(IApplicationDbContext db, IReadOnlyList<LineaDeBorradorInput> lineas, CancellationToken ct)
    {
        var codigos = lineas.Select(l => l.AccountCode.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var sucursales = lineas.Where(l => l.BranchPublicId is not null).Select(l => l.BranchPublicId!.Value).Distinct().ToList();
        var centros = lineas.Where(l => l.CostCenterPublicId is not null).Select(l => l.CostCenterPublicId!.Value).Distinct().ToList();
        var terceros = lineas.Where(l => l.PersonPublicId is not null).Select(l => l.PersonPublicId!.Value).Distinct().ToList();

        var cuentas = codigos.Count == 0
            ? new Dictionary<string, ChartOfAccount>(StringComparer.OrdinalIgnoreCase)
            : (await db.ChartOfAccounts.AsNoTracking().Where(c => !c.IsDeleted && codigos.Contains(c.Code)).ToListAsync(ct))
                .ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var sucursalesIds = sucursales.Count == 0 ? new Dictionary<Guid, int>()
            : await db.Branches.AsNoTracking().Where(b => !b.IsDeleted && sucursales.Contains(b.PublicId)).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
        var centrosIds = centros.Count == 0 ? new Dictionary<Guid, int>()
            : await db.CostCenters.AsNoTracking().Where(c => !c.IsDeleted && centros.Contains(c.PublicId)).ToDictionaryAsync(c => c.PublicId, c => c.Id, ct);
        var tercerosIds = terceros.Count == 0 ? new Dictionary<Guid, int>()
            : await db.People.AsNoTracking().Where(p => !p.IsDeleted && terceros.Contains(p.PublicId)).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        return new Referencias(cuentas, sucursalesIds, centrosIds, tercerosIds);
    }

    /// <summary>Las líneas digitadas como las entiende el contrato; la cuenta va por código para que el error la nombre así.</summary>
    public static IReadOnlyList<PostingLine> AlContrato(IReadOnlyList<LineaDeBorradorInput> lineas, Referencias refs) =>
        lineas.Select(l => new PostingLine
        {
            AccountCode = l.AccountCode.Trim(),
            Debit = l.Debit,
            Credit = l.Credit,
            Detail = l.Detail,
            PersonId = refs.Tercero(l.PersonPublicId),
            BranchId = refs.Sucursal(l.BranchPublicId),
            CostCenterId = refs.Centro(l.CostCenterPublicId),
            CrossDocumentType = string.IsNullOrWhiteSpace(l.CrossDocumentType) ? null : l.CrossDocumentType.Trim().ToUpperInvariant(),
            CrossDocumentNumber = l.CrossDocumentNumber,
            TaxBase = l.TaxBase,
        }).ToList();

    /// <summary>Las líneas vivas de un borrador guardado, en su orden, como las entiende el contrato (para contabilizar o revalidar).</summary>
    public static PostingRequest DesdeDocumento(AccountingDocument documento) =>
        new(documento.VoucherType?.Code ?? string.Empty, documento.Date, documento.Description, AccountingOrigin.Manual(documento.PublicId),
            documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).Select(l => new PostingLine
            {
                AccountId = l.AccountId,
                AccountCode = l.Account?.Code,
                Debit = l.Debit,
                Credit = l.Credit,
                Detail = l.Description,
                PersonId = l.PersonId,
                BranchId = l.BranchId,
                CostCenterId = l.CostCenterId,
                CrossDocumentType = l.CrossDocumentType?.Code,
                CrossDocumentNumber = l.CrossDocumentNumber,
                TaxBase = l.TaxBase,
            }).ToList(),
            documento.Kind);

    /// <summary>
    /// Lo que impide guardar aunque el borrador admita errores: una cuenta o una referencia que no
    /// existe no cabe en la tabla. Cada una llega como infracción de su línea y su campo.
    /// </summary>
    public static IReadOnlyList<ErrorDeLinea> Irrecuperables(IReadOnlyList<LineaDeBorradorInput> lineas, Referencias refs)
    {
        var errores = new List<ErrorDeLinea>();
        for (var i = 0; i < lineas.Count; i++)
        {
            var l = lineas[i];
            var n = i + 1;
            if (!refs.Cuentas.ContainsKey(l.AccountCode.Trim())) errores.Add(AccountingErrors.LineAccountNotFound(n, l.AccountCode.Trim()));
            if (refs.Sucursal(l.BranchPublicId) == Inexistente) errores.Add(AccountingErrors.LineBranchInvalid(n));
            if (refs.Centro(l.CostCenterPublicId) == Inexistente) errores.Add(AccountingErrors.LineCostCenterInvalid(n));
            if (refs.Tercero(l.PersonPublicId) == Inexistente) errores.Add(AccountingErrors.LineThirdPartyInvalid(n));
        }
        return errores;
    }
}
