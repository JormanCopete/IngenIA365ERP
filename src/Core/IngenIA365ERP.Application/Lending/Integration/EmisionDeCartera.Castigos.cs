using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Un crédito castigado tal como se emite (mensajes.md §10.22; K3, US14, T0908): su hecho (sobre y dimensiones de antes del castigo), la operación
/// <c>WriteOff</c>, lo que sale del activo por concepto, lo que sale contra el deterioro individual del último cierre, el suspenso que pasa a control
/// (C81), el lote, el punto del acta del consejo y el sello de la validación previa. Con <see cref="SuspensionPorElCastigo"/>, además, la
/// <c>CausacionSuspendida</c> con causa <c>WriteOff</c> (si el crédito todavía causaba normal, la emite el ciclo del día siguiente al detectar la
/// transición: así no sale dos veces). (nuevo)
/// </summary>
public sealed record CastigoAEmitir(
    HechoDeCredito Hecho,
    LoanOperation Operacion,
    IReadOnlyList<ConceptAmountV1> Castigado,
    IReadOnlyList<ConceptAmountV1> ContraElDeterioro,
    IReadOnlyList<ConceptAmountV1> SuspensoAControl,
    Guid LotePublicId,
    Guid PuntoDeActa,
    PrevalidationOutcome ValidacionPrevia,
    bool SuspensionPorElCastigo,
    int DiasDeMora);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de un castigo (mensajes.md §3): la operación <c>WriteOff</c> del ítem del lote.</summary>
    public const string ClaseCastigo = "WriteOff";

    /// <summary>El <c>reasonCode</c> de lo castigado (mensajes.md §10.22: la causal).</summary>
    public const string MotivoCastigo = "Irrecuperable";

    /// <summary>El contenido de <c>CreditoCastigado</c> (mensajes.md §10.22), sin emitir (lo usa también la validación previa).</summary>
    public static CreditoCastigadoV1 ContenidoDeCastigo(HechoDeCredito h, CategoriaContableVigente categoria, DateOnly fechaValor, DateTime registrado,
        IReadOnlyList<ConceptAmountV1> castigado, IReadOnlyList<ConceptAmountV1> contraElDeterioro, IReadOnlyList<ConceptAmountV1> suspenso,
        Guid lotePublicId, Guid puntoDeActa) => new()
    {
        Loan = h.Referencia,
        DebtorPersonPublicId = h.DeudorPublicId,
        ValueDate = fechaValor,
        RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(registrado, DateTimeKind.Utc)),
        Dimensions = Dimensiones(h, categoria),
        Amounts = castigado,
        RulesVersion = h.Condiciones.RulesVersion,
        WriteOffBatchPublicId = lotePublicId,
        SessionItemPublicId = puntoDeActa,
        ImpairmentApplied = contraElDeterioro,
        ImpairmentShortfall = castigado.Sum(a => a.Amount) - contraElDeterioro.Sum(a => a.Amount),
        SuspenseWrittenOff = suspenso,
    };

    /// <summary>
    /// Emite <c>CreditoCastigado</c> (mensajes.md §10.22; T0908): origen la operación <c>WriteOff</c>, clave <c>Confirmation</c>, el modo del tipo (en
    /// línea por defecto, <c>PV</c>), tercero el deudor, el sello de la validación previa; y, aparte, <c>CausacionSuspendida</c> (<c>cause = WriteOff</c>,
    /// desde el día siguiente al castigo) con clave <c>Confirmation:{tipoDeHecho:N}</c> y <c>causeFact</c> = el castigo, si <see
    /// cref="CastigoAEmitir.SuspensionPorElCastigo"/>. Lo origina la persona que aplica el lote. Sin guardar. Devuelve el <c>CreditoCastigado</c>.
    /// </summary>
    public async Task<Result<IntegrationMessage>> CastigoAsync(CastigoAEmitir c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var h = c.Hecho;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = ContenidoDeCastigo(h, categoria, c.Operacion.ValueDate, c.Operacion.RegisteredAt, c.Castigado, c.ContraElDeterioro,
            c.SuspensoAControl, c.LotePublicId, c.PuntoDeActa);
        var modo = await modoDePaso.ResolverAsync(CreditoCastigadoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<IntegrationMessage>(modo.Error);
        var origen = new OrigenDeEmision(MessageOriginKind.Document, c.Operacion.PublicId, ClaseCastigo, Prefijo(c.Operacion.Number), c.Operacion.Number,
            h.FechaContable, h.SucursalPublicId, PersonPublicId: h.DeudorPublicId);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(origen, ClavesDeEvento.Confirmacion, [contenido], modo.Value,
            ValidacionPrevia: c.ValidacionPrevia), ct);
        var castigo = mensajes[0];
        if (!c.SuspensionPorElCastigo) return Result.Success(castigo);

        var tipo = await db.LendingFactTypes.AsNoTracking().Where(f => f.Code == CausacionSuspendidaV1.Type && !f.IsDeleted)
            .Select(f => (Guid?)f.PublicId).FirstOrDefaultAsync(ct);
        var clave = tipo is { } tipoDeHecho ? ClavesDeEvento.ConfirmacionPor(tipoDeHecho) : ClavesDeEvento.ConfirmacionPor(h.Credito.PublicId);
        var suspension = new CausacionSuspendidaV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            Dimensions = Dimensiones(h, categoria) with { WrittenOff = true },
            RegisteredAt = contenido.RegisteredAt,
            EffectiveDate = c.Operacion.ValueDate.AddDays(1),
            Cause = "WriteOff",
            DaysPastDue = c.DiasDeMora,
            CauseFact = new OriginalFactRefV1
            {
                MessageId = castigo.PublicId, Type = CreditoCastigadoV1.Type, OriginPublicId = c.Operacion.PublicId, Number = c.Operacion.Number,
            },
        };
        var modoSuspension = await modoDePaso.ResolverAsync(CausacionSuspendidaV1.Type, h.FechaContable, emitidoPorElProceso: false, ct);
        if (modoSuspension.IsFailure) return Result.Failure<IntegrationMessage>(modoSuspension.Error);
        await emisor.EmitirAsync(new SolicitudDeEmision(origen, clave, [suspension], modoSuspension.Value, ValidacionPrevia: c.ValidacionPrevia), ct);
        return Result.Success(castigo);
    }
}
