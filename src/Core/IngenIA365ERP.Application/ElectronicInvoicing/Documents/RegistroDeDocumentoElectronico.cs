using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Lo que la confirmación del documento comercial le entrega a la plataforma para registrar su documento electrónico (T711). (nuevo)
/// </summary>
/// <param name="SourceModule">«INV».</param>
/// <param name="SourceDocumentPublicId">El documento comercial confirmado.</param>
/// <param name="SourceDocumentTypeCode">El código de su tipo, para la bandeja (máx. 10).</param>
/// <param name="Configuracion">La configuración de emisión vigente a la fecha: se sella en el documento.</param>
/// <param name="Canonico">El canónico construido en la misma transacción (su SHA-256 y su huella quedan en la versión 1).</param>
/// <param name="ResolutionId">La resolución con que numeró <c>NumeradorFiscal</c>; nula en las notas.</param>
/// <param name="TipoDeResolucion">El tipo de esa resolución: <c>Contingency</c> = numerado en contingencia 03.</param>
/// <param name="Corrige">Notas y notas de ajuste: el documento electrónico que corrigen.</param>
/// <param name="EsperaA">El documento que debe quedar validado antes de transmitir éste (nota sobre un original en contingencia).</param>
public sealed record PedidoDeRegistroElectronico(
    string SourceModule,
    Guid SourceDocumentPublicId,
    string SourceDocumentTypeCode,
    ElectronicEmissionSetting Configuracion,
    CanonicoConstruido Canonico,
    int? ResolutionId,
    ResolutionKind? TipoDeResolucion,
    ElectronicDocument? Corrige = null,
    ElectronicDocument? EsperaA = null);

/// <summary>
/// El registro del documento electrónico <b>dentro</b> de la transacción de confirmación (feature 012, I4, T711; decisiones-transversales §1.3
/// paso 10; contracts/dian.md §6.1): agrega la fila <c>COR_ElectronicDocuments</c> en <c>Pending</c> —o en <c>IssuerContingency</c> unida
/// al evento 03 abierto del canal, si se numeró con la resolución de contingencia— con el canal, el modo, el software, el ambiente y
/// <c>EmailDeliveryBy</c> <b>sellados</b> de la configuración vigente, <c>CorrectsDocumentId</c> y <c>WaitsForDocumentId</c> cuando aplican,
/// y la versión 1 (<c>Initial</c>) con <c>CanonicalSha256</c> y <c>EconomicFingerprint</c>. <b>Nunca llama al canal ni guarda</b>: el
/// <c>SaveChanges</c> de la confirmación guarda el número, el documento y la versión juntos, y el canónico se sube después del commit
/// (<see cref="GuardadoDeArtefactos"/>). La conecta <c>ConfirmInventoryDocumentCommand</c> (T734). (nuevo)
/// </summary>
public sealed class RegistroDeDocumentoElectronico(IApplicationDbContext db, IDateTimeService reloj)
{
    public async Task<Result<ElectronicDocument>> RegistrarAsync(PedidoDeRegistroElectronico pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var canonico = pedido.Canonico.Documento;
        var configuracion = pedido.Configuracion;
        var canal = ReglasDeResolucion.Canal(configuracion.ChannelCode);

        DianContingencyEvent? evento03 = null;
        if (pedido.TipoDeResolucion == ResolutionKind.Contingency)
        {
            evento03 = await db.DianContingencyEvents
                .Where(e => e.Type == ContingencyType.Issuer03 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == canal)
                .OrderByDescending(e => e.StartedAt)
                .FirstOrDefaultAsync(ct);
            if (evento03 is null) return Result.Failure<ElectronicDocument>(ErroresDeDocumentosElectronicos.ContingencyNotOpen(canal));
        }

        var emitido = canonico.IssuedAt;
        var ahora = reloj.UtcNow;
        var documento = new ElectronicDocument
        {
            SourceModule = pedido.SourceModule,
            SourceDocumentPublicId = pedido.SourceDocumentPublicId,
            SourceDocumentTypeCode = pedido.SourceDocumentTypeCode,
            Kind = canonico.Kind,
            DianDocumentTypeCode = canonico.DianDocumentTypeCode,
            ResolutionId = pedido.ResolutionId,
            Prefix = canonico.Number.Prefix,
            Consecutive = canonico.Number.Consecutive,
            Number = canonico.Number.Full,
            Environment = configuracion.Environment,
            EmissionSettingId = configuracion.Id,
            Mode = configuracion.Mode,
            ChannelCode = canal,
            SoftwareId = configuracion.SoftwareId,
            IssuedAt = emitido.UtcDateTime,
            IssueDate = DateOnly.FromDateTime(emitido.DateTime),
            CounterpartyTaxId = Recortar(canonico.Counterparty.TaxId, 20),
            CounterpartyName = Recortar(canonico.Counterparty.Name, 200),
            TotalAmount = canonico.Totals.Payable,
            EmailDeliveryBy = configuracion.EmailDeliveryBy,
            CorrectsDocumentId = pedido.Corrige?.Id,
            WaitsForDocumentId = pedido.EsperaA?.Id,
            NextAttemptAt = ahora,
        };
        documento.Iniciar(contingencia03Abierta: evento03 is not null);
        if (evento03 is not null) documento.ContingencyEvent = evento03;

        documento.Versions.Add(new ElectronicDocumentVersion
        {
            VersionNumber = 1,
            SourceDocumentPublicId = pedido.SourceDocumentPublicId,
            Reason = DocumentVersionReason.Initial,
            CanonicalSchemaVersion = (short)canonico.SchemaVersion,
            CanonicalSha256 = pedido.Canonico.CanonicalSha256,
            EconomicFingerprint = pedido.Canonico.EconomicFingerprint,
        });

        db.ElectronicDocuments.Add(documento);
        return Result.Success(documento);
    }

    private static string? Recortar(string? texto, int maximo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= maximo ? texto : texto[..maximo];
}
