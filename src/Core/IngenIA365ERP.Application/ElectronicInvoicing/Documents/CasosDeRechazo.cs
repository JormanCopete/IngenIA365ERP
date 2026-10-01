using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Lo común de los casos a, b y c de un documento rechazado (feature 012, I4, T722–T725; FR-066; contracts/dian.md §8; api.md §24.5):
/// <list type="bullet">
/// <item>cargar el documento y decidir si el caso procede <b>antes</b> de tocar nada, por la misma máquina de estados que lo aplicará
/// (<c>Sent</c> → <c>AwaitingResponse</c>; otro estado → <c>NotRejected</c>; b y c sin el rechazo confirmado → <c>RejectionNotConfirmed</c>);</item>
/// <item>el rechazo <b>confirmado</b>: después del último envío de la versión vigente hay una consulta de estado que respondió rechazo
/// (<c>Rejected</c>/<c>InvalidData</c>) o que el documento nunca llegó a la DIAN (<c>NotFound</c>). El caso a no lo exige;</item>
/// <item>la fuente sellada del documento (la plataforma no lee <c>INV_</c>) y, en b y c, el permiso de confirmar de su clase, que la fuente
/// conoce: 422 <c>ClassPermissionRequired</c>.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class CasosDeRechazo(IApplicationDbContext db, IEnumerable<IFuenteDeDocumentoElectronico> fuentes, IPermissionChecker permisos)
{
    /// <summary>El documento con lo que los casos necesitan, si el caso procede en su estado.</summary>
    public async Task<Result<(ElectronicDocument Documento, IFuenteDeDocumentoElectronico Fuente)>> PrepararAsync(
        Guid electronicDocumentPublicId, EventoDelDocumentoElectronico caso, CancellationToken ct, bool exigirPermisoDeLaClase = true)
    {
        var documento = await db.ElectronicDocuments
            .Include(d => d.Versions)
            .Include(d => d.EmissionSetting)
            .Include(d => d.Resolution)
            .Include(d => d.CorrectsDocument)
            .FirstOrDefaultAsync(d => d.PublicId == electronicDocumentPublicId, ct);
        if (documento is null) return Falla(ErroresDeDocumentosElectronicos.NotFound());

        var confirmado = caso != EventoDelDocumentoElectronico.CorregirCasoA && await RechazoConfirmadoAsync(documento, ct);
        var previa = TransicionesDelDocumentoElectronico.Aplicar(documento.Status, caso, rechazoConfirmado: confirmado);
        // Un final (validado, cancelado) tampoco es un rechazado: api.md §24.5 responde NotRejected a todo estado que no sea Rejected.
        if (previa.Codigo == TransicionesDelDocumentoElectronico.CodigoFinal)
            previa = previa with { Codigo = TransicionesDelDocumentoElectronico.CodigoNoRechazado };
        if (!previa.Procede) return Falla(ErroresDeDocumentosElectronicos.DeLaTransicion(previa));

        var fuente = fuentes.FirstOrDefault(f => string.Equals(f.SourceModule, documento.SourceModule, StringComparison.OrdinalIgnoreCase));
        if (fuente is null)
            return Falla(new Error(ReconstruccionDelCanonico.SourceUnknownCode,
                $"No hay un módulo fuente «{documento.SourceModule}» registrado para el documento {documento.Number}."));

        if (exigirPermisoDeLaClase && caso is EventoDelDocumentoElectronico.ReemplazarCasoB or EventoDelDocumentoElectronico.CancelarCasoC)
        {
            var permiso = fuente.PermisoDeConfirmar(documento.Kind);
            if (!await permisos.HasPermissionAsync(permiso, ct)) return Falla(ErroresDeDocumentosElectronicos.ClassPermissionRequired(permiso));
        }
        return Result.Success((documento, fuente));
    }

    /// <summary>
    /// ¿El rechazo está confirmado por la consulta de estado (contracts/dian.md §8)? Sí si, después del último envío (<c>Emit</c> o
    /// <c>TransmitContingency</c>) de la versión vigente, una consulta respondió <c>Rejected</c>, <c>InvalidData</c> o <c>NotFound</c>.
    /// </summary>
    public async Task<bool> RechazoConfirmadoAsync(ElectronicDocument documento, CancellationToken ct)
    {
        if (documento.Status != ElectronicDocumentStatus.Rejected) return false;
        var vigente = Vigente(documento);
        if (vigente is null) return false;

        var intentos = await db.ElectronicDocumentTransmissions.AsNoTracking()
            .Where(t => t.ElectronicDocumentId == documento.Id && t.VersionId == vigente.Id)
            .OrderBy(t => t.AttemptNumber)
            .Select(t => new { t.Operation, t.Outcome })
            .ToListAsync(ct);
        var ultimoEnvio = intentos.FindLastIndex(t => t.Operation != TransmissionOperation.QueryStatus);
        return intentos.Skip(ultimoEnvio + 1).Any(t => t.Operation == TransmissionOperation.QueryStatus
            && t.Outcome is ChannelOutcome.Rejected or ChannelOutcome.InvalidData or ChannelOutcome.NotFound);
    }

    /// <summary>La versión vigente: la de mayor número (la que transmite el intento).</summary>
    public static ElectronicDocumentVersion? Vigente(ElectronicDocument documento) => documento.Versions.MaxBy(v => v.VersionNumber);

    /// <summary>
    /// Agrega la versión n + 1 y deja el documento listo para reemitirse por el mismo canal y con el mismo número: la guarda, apunta
    /// <c>CurrentVersionId</c> a ella y vuelve a guardar (la versión no tiene Id antes del primer guardado). Quien llama ya aplicó el evento.
    /// </summary>
    public async Task<ElectronicDocumentVersion> AgregarVersionAsync(ElectronicDocument documento, Guid sourceDocumentPublicId,
        DocumentVersionReason motivoDeVersion, CanonicoConstruido canonico, string razon, string? camposCambiados, DateTime ahora, CancellationToken ct)
    {
        var numero = (short)((Vigente(documento)?.VersionNumber ?? 0) + 1);
        var version = new ElectronicDocumentVersion
        {
            ElectronicDocumentId = documento.Id,
            VersionNumber = numero,
            SourceDocumentPublicId = sourceDocumentPublicId,
            Reason = motivoDeVersion,
            CanonicalSchemaVersion = (short)canonico.Documento.SchemaVersion,
            CanonicalSha256 = canonico.CanonicalSha256,
            EconomicFingerprint = canonico.EconomicFingerprint,
            CorrectionReason = Recortar(razon.Trim(), ValidadorDeMotivo),
            ChangedFieldsJson = camposCambiados,
        };
        documento.Versions.Add(version);
        documento.NextAttemptAt = ahora;
        documento.AttemptCount = 0;
        documento.LastMessagesJson = null;
        await db.SaveChangesAsync(ct);

        documento.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(ct);
        return version;
    }

    /// <summary>El largo de <c>CorrectionReason</c> (el mismo del motivo de toda operación).</summary>
    private const int ValidadorDeMotivo = Common.Behaviors.ValidadorConMotivo<CorrectRejectedDocumentCommand>.LargoMaximo;

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];

    private static Result<(ElectronicDocument, IFuenteDeDocumentoElectronico)> Falla(Error error) =>
        Result.Failure<(ElectronicDocument, IFuenteDeDocumentoElectronico)>(error);
}
