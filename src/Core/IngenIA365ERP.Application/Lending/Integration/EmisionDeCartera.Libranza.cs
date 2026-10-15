using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>Una línea del archivo de descuentos tal como viaja a Recaudo (mensajes.md §11.6, C19). (nuevo)</summary>
public sealed record LineaDeDescuentoAEmitir(
    Guid LinePublicId,
    LoanRefV1 Credito,
    Guid DeudorPublicId,
    string? EmployeeIdAtPaymaster,
    int? InstallmentNumber,
    DateOnly? DueDate,
    PayrollNoveltyKind Novedad,
    decimal Total,
    decimal Vencido);

/// <summary>El archivo de descuentos tal como se emite (mensajes.md §11.6): sus datos sellados, sus líneas y la versión que reemplaza. (nuevo)</summary>
public sealed record ArchivoDeDescuentosAEmitir(
    Guid FilePublicId,
    string Number,
    int FileVersion,
    int Year,
    int Month,
    int SubPeriod,
    Guid AttachmentPublicId,
    string FileName,
    string Sha256,
    long Bytes,
    string CodigoDelFormato,
    IReadOnlyList<LineaDeDescuentoAEmitir> Lineas,
    string CodigoDelConvenio,
    int VersionDelConvenio,
    Guid PagaduriaPublicId,
    DateOnly EnviarA,
    PayrollDeductionFile? Reemplazado,
    Guid? MensajeDelReemplazado,
    DateOnly Hoy);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen del archivo de descuentos (mensajes.md §11.6).</summary>
    public const string ClaseArchivoDeDescuentos = "PayrollDeductionFile";

    /// <summary>La clase del origen del cambio de vínculo de libranza (traslado o forma alterna, FR-116).</summary>
    public const string ClaseVinculoDeLibranza = "LoanPayrollLink";

    /// <summary>
    /// <c>DescuentosPorPagaduria</c> v1 (K2, T0836; mensajes.md §11.6; C19): origen la versión del archivo (<c>PayrollDeductionFile</c>), clave
    /// <c>Confirmation</c>, destino <c>Receipts</c> y siempre (<c>Always</c>), tercero la pagaduría, <c>related</c> = la versión que reemplaza. Sin
    /// bytes: el adjunto por su <c>PublicId</c> y su huella, los totales y las líneas estructuradas. Lo origina la persona que genera. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> DescuentosPorPagaduriaAsync(ArchivoDeDescuentosAEmitir a, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(a);
        var lineas = a.Lineas.Select(l => new DeductionFileLineV1
        {
            DeductionFileLinePublicId = l.LinePublicId,
            Loan = l.Credito,
            DebtorPersonPublicId = l.DeudorPublicId,
            PayerEmployeeId = l.EmployeeIdAtPaymaster ?? string.Empty,
            InstallmentNumber = l.InstallmentNumber,
            DueDate = l.DueDate ?? a.EnviarA,
            NoveltyKind = l.Novedad.ToString(),
            Amount = l.Total,
            PastDueAmount = l.Vencido,
        }).ToList();
        var contenido = new DescuentosPorPagaduriaV1
        {
            Agreement = new PayrollAgreementRefV1
            {
                PaymasterPersonPublicId = a.PagaduriaPublicId, AgreementCode = a.CodigoDelConvenio, AgreementVersion = a.VersionDelConvenio,
            },
            Period = new PayrollPeriodRefV1 { Year = a.Year, Month = a.Month, SubPeriod = a.SubPeriod > 1 ? a.SubPeriod : null },
            FileVersion = a.FileVersion,
            File = new DeductionFileRefV1
            {
                AttachmentPublicId = a.AttachmentPublicId, FileName = a.FileName, Sha256 = a.Sha256, SizeBytes = a.Bytes, FormatCode = a.CodigoDelFormato,
            },
            SendBy = a.EnviarA,
            Lines = lineas,
            Totals = new DeductionFileTotalsV1
            {
                LineCount = lineas.Count,
                Amount = lineas.Sum(l => l.Amount),
                PastDueAmount = lineas.Sum(l => l.PastDueAmount),
                ByNovelty = lineas.GroupBy(l => l.NoveltyKind).OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new DeductionNoveltyTotalV1 { NoveltyKind = g.Key, Lines = g.Count(), Amount = g.Sum(l => l.Amount) }).ToList(),
            },
            SupersedesFile = a.Reemplazado is { } r
                ? new SupersededDeductionFileV1 { FilePublicId = r.PublicId, FileVersion = r.Version, MessageId = a.MensajeDelReemplazado ?? Guid.Empty }
                : null,
            GeneratedBy = await UsuarioAsync(ct),
        };
        var sucursal = await SucursalPrincipalAsync(ct);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, a.FilePublicId, ClaseArchivoDeDescuentos, Prefijo(a.Number), a.Number, a.Hoy, sucursal,
                PersonPublicId: a.PagaduriaPublicId),
            ClavesDeEvento.Confirmacion, [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: a.Reemplazado is { } previo ? new DocumentoRelacionado(previo.PublicId, ClaseArchivoDeDescuentos, previo.Number) : null), ct);
        return mensajes[0];
    }

    /// <summary>
    /// El contenido de <c>DimensionContableCambiada</c> por el cambio de vínculo de libranza (K2, T0843; FR-116, C44): las dimensiones vigentes con el
    /// sub-segmento anterior y el nuevo, y los saldos vivos que se mueven. Lo usan la validación previa (antes de confirmar) y la emisión.
    /// </summary>
    public async Task<DimensionContableCambiadaV1> DimensionPorCambioDeLibranzaAsync(HechoDeCredito h, string subsegmentoAnterior, string subsegmentoNuevo,
        IReadOnlyList<ConceptAmountV1> saldos, DateOnly fechaValor, DateTime registrado, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(h);
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var anteriores = Dimensiones(h, categoria) with { SubsegmentCode = subsegmentoAnterior };
        return new DimensionContableCambiadaV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = fechaValor,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(registrado, DateTimeKind.Utc)),
            Dimensions = anteriores with { SubsegmentCode = subsegmentoNuevo },
            Amounts = saldos,
            RulesVersion = h.Condiciones.RulesVersion,
            PreviousDimensions = anteriores,
            ChangedDimensions = ["subsegmentCode"],
            EffectiveDate = fechaValor,
        };
    }

    /// <summary>
    /// Emite el <c>DimensionContableCambiada</c> del cambio de vínculo de libranza (K2, T0843): origen el vínculo nuevo (<c>LoanPayrollLink</c>, no es
    /// una operación del libro), clave <c>Confirmation:{crédito:N}</c>, modo del tipo emitido por una persona (<c>EnLinea</c> sin vigencia propia),
    /// tercero el deudor. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> EmitirDimensionPorCambioDeLibranzaAsync(HechoDeCredito h, LoanPayrollLink vinculo,
        DimensionContableCambiadaV1 contenido, PrevalidationOutcome validacionPrevia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(h);
        ArgumentNullException.ThrowIfNull(vinculo);
        var modo = await modoDePaso.ResolverAsync(DimensionContableCambiadaV1.Type, h.FechaContable, emitidoPorElProceso: false, ct);
        if (modo.IsFailure) return Result.Failure<IntegrationMessage>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, vinculo.PublicId, ClaseVinculoDeLibranza, null, h.Credito.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.ConfirmacionPor(h.Credito.PublicId), [contenido], modo.Value, ValidacionPrevia: validacionPrevia), ct);
        return Result.Success(mensajes[0]);
    }

    /// <summary>
    /// El sub-segmento contable que rige un crédito (K2, T0843; FR-116, C44): el de sus condiciones, salvo que después de nacer su forma de pago haya
    /// cambiado por un traslado o por el paso a la forma alterna y las condiciones distingan con y sin libranza: entonces sigue al vínculo vigente.
    /// </summary>
    public static string? SubsegmentoVigente(string? deLasCondiciones, LoanPayrollLink? vinculoVigente)
    {
        if (vinculoVigente is null || vinculoVigente.Origin is not (LoanPayrollLinkOrigin.Transfer or LoanPayrollLinkOrigin.ChannelChange)) return deLasCondiciones;
        return deLasCondiciones is SubsegmentoConLibranza or SubsegmentoSinLibranza
            ? vinculoVigente.ConLibranza ? SubsegmentoConLibranza : SubsegmentoSinLibranza
            : deLasCondiciones;
    }

    /// <summary>Los sub-segmentos de consumo que distinguen la libranza (catálogo nacional de modalidades).</summary>
    public const string SubsegmentoConLibranza = "ConLibranza";

    public const string SubsegmentoSinLibranza = "SinLibranza";
}
