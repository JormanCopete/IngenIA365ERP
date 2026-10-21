using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>Un gasto de cobranza causado tal como se emite (mensajes.md §10.19; K4, T0948). (nuevo)</summary>
public sealed record GastoDeCobranzaAEmitir(
    HechoDeCredito Hecho,
    LoanOperation Operacion,
    decimal Valor,
    Guid AvisoPublicId,
    string Etapa,
    Guid? Beneficiario,
    PrevalidationOutcome ValidacionPrevia);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de un gasto de cobranza (mensajes.md §3: <c>LoanCharge</c>).</summary>
    public const string ClaseCargo = "LoanCharge";

    /// <summary>
    /// El contenido de <c>GastoDeCobranzaCausado</c> (mensajes.md §10.19), sin emitir (lo usa también la validación previa): <c>amounts</c> =
    /// <c>CollectionFee</c> por el valor del gasto, el mensaje al deudor que lo informó, la etapa y, si es una cuenta por pagar a un tercero (el abogado
    /// de un honorario), su persona. <b>Ningún hecho lleva una cuenta</b> (<c>LosHechosDeCarteraNoLlevanCuentas</c>).
    /// </summary>
    public static GastoDeCobranzaCausadoV1 ContenidoDeGasto(HechoDeCredito h, CategoriaContableVigente categoria, DateOnly fechaValor, DateTime registrado,
        decimal valor, Guid aviso, string etapa, Guid? beneficiario) => new()
    {
        Loan = h.Referencia,
        DebtorPersonPublicId = h.DeudorPublicId,
        ValueDate = fechaValor,
        RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(registrado, DateTimeKind.Utc)),
        Dimensions = Dimensiones(h, categoria),
        Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.CollectionFee), Amount = valor }],
        RulesVersion = h.Condiciones.RulesVersion,
        NoticeDebtorMessagePublicId = aviso,
        StageCode = etapa,
        PayeePersonPublicId = beneficiario,
    };

    /// <summary>
    /// Emite <c>GastoDeCobranzaCausado</c> (mensajes.md §10.19; T0948, T77): origen la operación <c>Charge</c> (clase <c>LoanCharge</c>), clave
    /// <c>Confirmation</c>, el modo del tipo, tercero el deudor, el sello de la validación previa. Lo origina la persona que causa el gasto (no admite
    /// proceso). Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> GastoDeCobranzaAsync(GastoDeCobranzaAEmitir g, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(g);
        var h = g.Hecho;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = ContenidoDeGasto(h, categoria, g.Operacion.ValueDate, g.Operacion.RegisteredAt, g.Valor, g.AvisoPublicId, g.Etapa, g.Beneficiario);
        var modo = await modoDePaso.ResolverAsync(GastoDeCobranzaCausadoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<IntegrationMessage>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, g.Operacion.PublicId, ClaseCargo, Prefijo(g.Operacion.Number), g.Operacion.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value, ValidacionPrevia: g.ValidacionPrevia), ct);
        return Result.Success(mensajes[0]);
    }
}
