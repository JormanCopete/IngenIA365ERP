using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Integration;

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase de documento de origen de los hechos de un cargo masivo (la operación del crédito).</summary>
    public const string ClaseCargoMasivo = "BulkCharge";

    /// <summary>El disparador de la clave de horario de los cargos masivos: el lote lo crea la corrida al terminar, no un horario.</summary>
    public const string DisparadorCargoMasivo = "CargoMasivo";

    /// <summary>El alcance del lote de una corrida de cargos masivos (o de la anulación de su lote <paramref name="tanda"/>).</summary>
    public static string AlcanceDeCargoMasivo(Guid corrida, string? tanda = null) =>
        tanda is null ? $"CargoMasivo:{corrida:N}" : $"CargoMasivo:{corrida:N}:{tanda}";

    /// <summary>
    /// 2026-11-25 (feature 026): el modo de los hechos de una corrida de cargos masivos. Si el hecho no pasa a Contabilidad (sin contabilidad iniciada o en
    /// «No pasa»), el de siempre; si pasa, por lotes con la clave propia de la corrida, así todos sus hechos se asientan en un solo comprobante cuando la
    /// corrida termina y crea su lote.
    /// </summary>
    public async Task<Result<ModoDeEntrega>> ModoDeCargoMasivoAsync(string tipoDeHecho, string corto, DateOnly fechaContable, string alcance,
        CancellationToken ct)
    {
        var resuelto = await modoDePaso.ResolverAsync(tipoDeHecho, fechaContable, ct);
        if (resuelto.IsFailure) return resuelto;
        if (resuelto.Value is ModoDeEntrega.Sellado { Modo: DeliveryMode.NotPosted }) return resuelto;
        return Result.Success<ModoDeEntrega>(new ModoDeEntrega.Sellado(DeliveryMode.Batch,
            ClavesDeLote.Horario(ClavesDeLote.PrefijoDeCartera + corto, DisparadorCargoMasivo, null, ClavesDeLote.GranularidadPorDia), alcance));
    }

    /// <summary>
    /// 2026-11-25 (feature 026): <c>CargoMasivoRegistrado</c> de la obligación que nació de una corrida: origen la operación de desembolso, tercero el
    /// deudor, la contrapartida con el tercero de la corrida y el modo sellado por <see cref="ModoDeCargoMasivoAsync"/>. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> CargoMasivoRegistradoAsync(HechoDeCredito h, LoanOperation op, decimal monto, LoanPlanSummaryV1 plan,
        BulkChargeRun corrida, Guid contraparte, ModoDeEntrega modo, PrevalidationOutcome validacionPrevia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(h);
        ArgumentNullException.ThrowIfNull(op);
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = new CargoMasivoRegistradoV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = op.ValueDate,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(op.RegisteredAt, DateTimeKind.Utc)),
            Dimensions = Dimensiones(h, categoria),
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = monto }],
            RulesVersion = h.Condiciones.RulesVersion,
            Plan = plan,
            BulkChargeRunPublicId = corrida.PublicId,
            BulkChargeRunNumber = corrida.Number,
            CounterpartPersonPublicId = contraparte,
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, ClaseCargoMasivo, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo, ValidacionPrevia: validacionPrevia), ct);
        return Result.Success(mensajes[0]);
    }

    /// <summary>
    /// 2026-11-25 (feature 026): <c>CargoMasivoAnulado</c>, el espejo del cargo de una obligación anulada sin movimientos: origen la operación del reverso,
    /// <c>related</c> = el cargo, y el modo sellado de la anulación. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> CargoMasivoAnuladoAsync(HechoDeCredito h, LoanOperation reverso, LoanOperation cargo, decimal monto,
        BulkChargeRun corrida, Guid contraparte, Guid? mensajeOriginal, string motivo, ModoDeEntrega modo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(h);
        ArgumentNullException.ThrowIfNull(reverso);
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = new CargoMasivoAnuladoV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = reverso.ValueDate,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(reverso.RegisteredAt, DateTimeKind.Utc)),
            Dimensions = Dimensiones(h, categoria),
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = monto }],
            Original = mensajeOriginal is { } m
                ? new OriginalFactRefV1 { MessageId = m, Type = CargoMasivoRegistradoV1.Type, OriginPublicId = cargo.PublicId }
                : null,
            RulesVersion = h.Condiciones.RulesVersion,
            BulkChargeRunPublicId = corrida.PublicId,
            BulkChargeRunNumber = corrida.Number,
            CounterpartPersonPublicId = contraparte,
            Reason = motivo,
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, reverso.PublicId, ClaseCargoMasivo, Prefijo(reverso.Number), reverso.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo,
            Relacionado: new DocumentoRelacionado(cargo.PublicId, ClaseCargoMasivo, cargo.Number)), ct);
        return Result.Success(mensajes[0]);
    }
}
