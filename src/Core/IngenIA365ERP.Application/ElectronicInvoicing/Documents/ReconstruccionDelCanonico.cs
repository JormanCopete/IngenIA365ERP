using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Vuelve a armar el canónico de una versión (feature 012, I4, T716; contracts/dian.md §4.1): lee el documento comercial de la versión por la
/// fuente sellada en el documento (<see cref="IFuenteDeDocumentoElectronico"/>, la plataforma no lee <c>INV_</c>) y lo pasa por el único
/// constructor con lo <b>sellado</b> al numerar —la configuración, la resolución, el número, la contingencia 03 si se numeró en ella y el
/// documento que corrige—. <see cref="NumeracionDe"/> es lo que la confirmación (T734) usa también, para que las dos construcciones den los
/// mismos bytes. (nuevo)
/// </summary>
public sealed class ReconstruccionDelCanonico(
    IApplicationDbContext db,
    IEnumerable<IFuenteDeDocumentoElectronico> fuentes,
    ConstructorDelCanonico constructor) : IReconstruccionDelCanonico
{
    public const string SourceUnknownCode = "ElectronicInvoicing.Document.SourceUnknown";

    public async Task<Result<CanonicoConstruido>> ReconstruirAsync(ElectronicDocument documento, ElectronicDocumentVersion version, CancellationToken ct)
    {
        var fuente = fuentes.FirstOrDefault(f => string.Equals(f.SourceModule, documento.SourceModule, StringComparison.OrdinalIgnoreCase));
        if (fuente is null)
            return Result.Failure<CanonicoConstruido>(SourceUnknownCode, $"No hay un módulo fuente «{documento.SourceModule}» registrado para el documento {documento.Number}.");

        var entrada = await fuente.LeerAsync(version.SourceDocumentPublicId, ct);
        if (entrada.IsFailure) return Result.Failure<CanonicoConstruido>(entrada.Error);

        var configuracion = documento.EmissionSetting ?? await db.ElectronicEmissionSettings.AsNoTracking().FirstAsync(s => s.Id == documento.EmissionSettingId, ct);
        var resolucion = documento.Resolution ?? (documento.ResolutionId is int r
            ? await db.DianNumberingResolutions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r, ct)
            : null);
        var corregido = documento.CorrectsDocument ?? (documento.CorrectsDocumentId is int c
            ? await db.ElectronicDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c, ct)
            : null);

        return await constructor.ConstruirAsync(entrada.Value, NumeracionDe(configuracion, resolucion, documento.Prefix, documento.Consecutive,
            documento.ContingencyType == ContingencyType.Issuer03, corregido), ct);
    }

    /// <summary>
    /// Lo sellado que entra al constructor: la contingencia sólo si se numeró en la 03 (la 04 la declara el canal después y no cambia lo que se
    /// firmó).
    /// </summary>
    public static NumeracionDelCanonico NumeracionDe(ElectronicEmissionSetting configuracion, DianNumberingResolution? resolucion, string prefijo,
        long consecutivo, bool numeradoEnContingencia03, ElectronicDocument? corregido) =>
        new(configuracion, resolucion, prefijo, consecutivo, numeradoEnContingencia03 ? ContingencyType.Issuer03 : null, corregido);
}
