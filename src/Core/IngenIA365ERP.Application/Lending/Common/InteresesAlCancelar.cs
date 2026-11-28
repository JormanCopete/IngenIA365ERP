using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// Cómo se cobra el interés de la cuota en curso al cancelar un crédito a una fecha (2026-11-16, pedido del dueño): lo que dice el parámetro
/// <c>Pagos.InteresesAlPagarAntesDelVencimiento</c> (<see cref="Configurado"/>), lo que se cobra de verdad (<see cref="Efectivo"/>) y, si difieren, por qué
/// (<see cref="Motivo"/>: <c>Housing</c>, <c>WithinLegalPrepaymentCap</c> o <c>LegalPrepaymentCapUnknown</c>). (nuevo)
/// </summary>
public sealed record ModoDeInteresesAlCancelar(string Configurado, string Efectivo, string? Motivo)
{
    /// <summary>El de fábrica: el interés corrido, sin nada que explicar.</summary>
    public static ModoDeInteresesAlCancelar Corrido { get; } =
        new(ParametrosDeCartera.InteresesCorridos, ParametrosDeCartera.InteresesCorridos, null);

    /// <summary>Al cancelar se cobra el interés completo de la cuota en curso.</summary>
    public bool CuotaCompleta => string.Equals(Efectivo, ParametrosDeCartera.InteresesDeLaCuotaCompleta, StringComparison.Ordinal);

    /// <summary>La explicación en palabras para quien lee la liquidación o la simulación; nula si se cobra lo que pide el parámetro.</summary>
    public string? Explicacion => Motivo switch
    {
        InteresesAlCancelar.MotivoVivienda =>
            "Se cobra el interés corrido: un crédito de vivienda se prepaga sin penalidad (Ley 546 de 1999) aunque la empresa cobre la cuota completa.",
        InteresesAlCancelar.MotivoDentroDelTope =>
            "Se cobra el interés corrido: el crédito está dentro del tope legal del prepago sin penalidad (Ley 1555 de 2012) aunque la empresa cobre la cuota completa.",
        InteresesAlCancelar.MotivoTopeDesconocido =>
            "Se cobra el interés corrido: faltan el salario mínimo o el tope del prepago a la fecha del desembolso para saber si el crédito está fuera del tope legal.",
        _ => null,
    };
}

/// <summary>
/// 2026-11-27 (feature 028, D13 ajustada por el dueño): con qué regla deja la cuota causada una cancelación salvo la cuota causada
/// (<c>PayoffExceptCurrentInstallment</c>): <c>TalComoSeEnvio</c> (D13-A, fuera del tope legal del prepago: con el interés del período sobre todo el saldo) o
/// <c>Proporcional</c> (dentro del tope —Ley 1555 de 2012— o sin los valores nacionales para saberlo: lo causado hasta la fecha sobre todo el saldo y el resto
/// del período sólo sobre su propio capital), con el motivo legal (<see cref="InteresesAlCancelar.MotivoDentroDelTope"/>,
/// <see cref="InteresesAlCancelar.MotivoTopeDesconocido"/>). Independiente de <c>Recogida.InteresesProporcionales</c> y de
/// <c>Pagos.InteresesAlPagarAntesDelVencimiento</c>: sólo el motivo legal la cambia. (nuevo)
/// </summary>
public sealed record ReglaDeCuotaDejada(string Regla, string? Motivo)
{
    /// <summary>Fuera del tope legal del prepago (o sin la pieza que lo sabe): la cuota queda tal como se envió (D13-A).</summary>
    public static ReglaDeCuotaDejada TalComoSeEnvio { get; } = new(AplicadorDePagos.ReglaDeLaCuotaDejada, null);

    /// <summary>La cuota se deja con el interés proporcional.</summary>
    public bool Proporcional => string.Equals(Regla, AplicadorDePagos.ReglaDeLaCuotaDejadaProporcional, StringComparison.Ordinal);

    /// <summary>Por qué la ley obliga al proporcional, en palabras («dentro del tope legal del prepago»…); nula tal como se envió.</summary>
    public string? Razon => Motivo switch
    {
        InteresesAlCancelar.MotivoDentroDelTope => "dentro del tope legal del prepago sin penalidad, Ley 1555 de 2012",
        InteresesAlCancelar.MotivoTopeDesconocido =>
            "faltan el salario mínimo o el tope del prepago a la fecha del desembolso para saber si el crédito está fuera del tope legal",
        InteresesAlCancelar.MotivoVivienda => "crédito de vivienda, que se prepaga sin penalidad (Ley 546 de 1999)",
        _ => null,
    };
}

/// <summary>
/// <b>Un solo sitio</b> que decide si una cancelación cobra el interés completo de la cuota en curso (2026-11-16, pedido del dueño; decisiones del dueño
/// del 2026-11-16):
/// <list type="number">
/// <item>el parámetro <c>Pagos.InteresesAlPagarAntesDelVencimiento</c> a la fecha valor, el de la línea del crédito con caída al general (ilegible =
/// «Corrido»);</item>
/// <item>con «CuotaCompleta», un crédito de <b>vivienda</b> va siempre con el corrido (Ley 546 de 1999, <see cref="ReglasDeCalculo.PrepagoSinPenalidad"/>);</item>
/// <item>y uno <b>dentro del tope legal</b> del prepago sin penalidad (Ley 1555 de 2012 y 2032 de 2020; <see cref="TopeLegalDePrepago"/> con el SMMLV y
/// <c>PREPAGO_TOPE_SMMLV</c> a la fecha del desembolso, como el tope que sella el pago) también. Si falta alguno de esos valores nacionales no se sabe si
/// está fuera del tope y se cobra el corrido: nunca se bloquea ni se le cobra de más al deudor por un dato que falta.</item>
/// </list>
/// Lo usan la aplicación de pagos (lo sella en la decisión del pago), la liquidación, el listado y el detalle de la caja, la recogida del desembolso, el cruce
/// del retiro y la simulación de cancelación del estado de cuenta: todos dicen lo mismo que cobra la caja. Vive en <c>Lending/Common</c> y no en
/// <c>Lending/Payments</c> porque es una regla, no un servicio de pagos: el desembolso la consulta para saber cuánto recoger y paga igual por el contrato
/// (<c>La_recogida_del_desembolso_paga_por_el_contrato</c>). (nuevo)
/// </summary>
public sealed class InteresesAlCancelar(ReglasDeParametrosDeCartera parametros, IValoresNacionales valores)
{
    public const string MotivoVivienda = "Housing";
    public const string MotivoDentroDelTope = "WithinLegalPrepaymentCap";
    public const string MotivoTopeDesconocido = "LegalPrepaymentCapUnknown";

    /// <summary>
    /// 2026-11-27 (feature 028, T050): el documento con que la recogida del desembolso paga el crédito recogido por el contrato
    /// (<c>ReferenciaDeRecibo.DocumentKind</c>); con el recaudador <c>OwnLoanPayoff</c>, es lo que distingue la recogida del cruce del retiro y del débito
    /// automático. (nuevo)
    /// </summary>
    public const string DocumentoDelDesembolso = "LoanDisbursement";

    /// <summary>Lo que dice el parámetro a <paramref name="fecha"/>: el de la línea si lo tiene, si no el general; ilegible = «Corrido».</summary>
    public async Task<string> ConfiguradoAsync(DateOnly fecha, int? lineaId, CancellationToken ct)
    {
        var leido = lineaId is { } l
            ? await parametros.LeerAsync(ParametrosDeCartera.PagosInteresesAlPagarAntesDelVencimiento, fecha, null, ParameterScopeKind.CreditLine, l, ct)
            : await parametros.LeerAsync(ParametrosDeCartera.PagosInteresesAlPagarAntesDelVencimiento, fecha, ct: ct);
        return leido.IsSuccess && string.Equals(leido.Value.Texto?.Trim(), ParametrosDeCartera.InteresesDeLaCuotaCompleta, StringComparison.Ordinal)
            ? ParametrosDeCartera.InteresesDeLaCuotaCompleta
            : ParametrosDeCartera.InteresesCorridos;
    }

    /// <summary>Cómo cobra el interés de la cuota en curso una cancelación de <paramref name="credito"/> con fecha valor <paramref name="fecha"/>.</summary>
    /// <param name="rulesVersion">La versión de reglas del crédito (la de vivienda va siempre con el corrido).</param>
    public async Task<ModoDeInteresesAlCancelar> ModoAsync(Loan credito, string? rulesVersion, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credito);
        var configurado = await ConfiguradoAsync(fecha, credito.CreditLineId, ct);
        if (configurado != ParametrosDeCartera.InteresesDeLaCuotaCompleta) return ModoDeInteresesAlCancelar.Corrido;
        return await CuotaCompletaConExcepcionesAsync(credito, rulesVersion, ct);
    }

    /// <summary>
    /// 2026-11-27 (feature 028, HU5): lo que dice <c>Recogida.InteresesProporcionales</c> a <paramref name="fecha"/> —el de la línea del crédito que se recoge
    /// si lo tiene, si no el general—: <c>ComoAlCancelar</c>, <c>Si</c> o <c>No</c>; ilegible = <c>ComoAlCancelar</c> (lo de siempre). (nuevo)
    /// </summary>
    public async Task<string> RecogidaConfiguradaAsync(DateOnly fecha, int? lineaId, CancellationToken ct)
    {
        var leido = lineaId is { } l
            ? await parametros.LeerAsync(ParametrosDeCartera.RecogidaInteresesProporcionales, fecha, null, ParameterScopeKind.CreditLine, l, ct)
            : await parametros.LeerAsync(ParametrosDeCartera.RecogidaInteresesProporcionales, fecha, ct: ct);
        return (leido.IsSuccess ? leido.Value.Texto?.Trim() : null) switch
        {
            ParametrosDeCartera.ProporcionalesSi => ParametrosDeCartera.ProporcionalesSi,
            ParametrosDeCartera.ProporcionalesNo => ParametrosDeCartera.ProporcionalesNo,
            _ => ParametrosDeCartera.ProporcionalesComoAlCancelar,
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, HU5, R5): cómo cobra el interés de la cuota en curso la <b>recogida</b> de <paramref name="credito"/> en el desembolso de otro
    /// con fecha valor <paramref name="fecha"/>, según <c>Recogida.InteresesProporcionales</c> (<see cref="RecogidaConfiguradaAsync"/>):
    /// <list type="bullet">
    /// <item><c>ComoAlCancelar</c> (de fábrica): exactamente lo que cobraría una cancelación en caja (<see cref="ModoAsync"/>), como siempre;</item>
    /// <item><c>Si</c>: el corrido —se cobra el interés causado hasta la fecha y, por período calendario, se devuelve lo causado de un tramo sin
    /// empezar—, sin importar lo que diga <c>Pagos.InteresesAlPagarAntesDelVencimiento</c>;</item>
    /// <item><c>No</c>: la cuota completa, con las mismas excepciones legales que la cancelación: vivienda, dentro del tope legal del prepago o sin los
    /// valores nacionales para saberlo, el corrido.</item>
    /// </list>
    /// Lo sella la decisión del pago igual que el de la cancelación (<c>InterestBeforeDueDate</c>): la reconstrucción lo repite. (nuevo)
    /// </summary>
    public async Task<ModoDeInteresesAlCancelar> ModoAlRecogerAsync(Loan credito, string? rulesVersion, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credito);
        return await RecogidaConfiguradaAsync(fecha, credito.CreditLineId, ct) switch
        {
            ParametrosDeCartera.ProporcionalesSi => ModoDeInteresesAlCancelar.Corrido,
            ParametrosDeCartera.ProporcionalesNo => await CuotaCompletaConExcepcionesAsync(credito, rulesVersion, ct),
            _ => await ModoAsync(credito, rulesVersion, fecha, ct),
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, R5): el gemelo de <see cref="ValorParaCancelarAsync"/> para la recogida: lo que cobra el <c>FullPayoff</c> de la recogida
    /// a la fecha de <paramref name="estado"/>, con el modo de <see cref="ModoAlRecogerAsync"/> y el período calendario de la línea. (nuevo)
    /// </summary>
    public async Task<decimal> ValorParaRecogerAsync(Loan credito, EstadoDelCredito estado, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var cuotaCompleta = !estado.EnUvr && (await ModoAlRecogerAsync(credito, estado.RulesVersion, estado.Fecha, ct)).CuotaCompleta;
        var calendario = !estado.EnUvr && await PeriodoCalendarioAsync(estado.Fecha, credito.CreditLineId, ct);
        return AplicadorDePagos.ValorParaCancelar(estado, cuotaCompleta, calendario);
    }

    /// <summary>
    /// 2026-11-27 (feature 028, D13 ajustada por el dueño; Ley 1555 de 2012): con qué regla deja la cuota causada una cancelación salvo la cuota causada de
    /// <paramref name="credito"/> —la recogida del desembolso y la caja (modalidad 8), las dos por el contrato—: dentro del tope legal del prepago, o sin el
    /// SMMLV o el límite <c>PREPAGO_TOPE_SMMLV</c> a la fecha del desembolso para saberlo (el mismo criterio de <see cref="ModoAsync"/>), con el interés
    /// <b>proporcional</b>; fuera del tope, <b>tal como se envió</b> (D13-A). Un crédito de vivienda se recoge completo y no deja cuota; si llegara aquí, el
    /// proporcional. No lee <c>Recogida.InteresesProporcionales</c> ni <c>Pagos.InteresesAlPagarAntesDelVencimiento</c>: sólo el motivo legal cambia la cuota
    /// dejada. Lo sella la decisión del pago (<c>LeftInstallmentRule</c>, <c>LeftInstallmentRuleReason</c>). (nuevo)
    /// </summary>
    public async Task<ReglaDeCuotaDejada> CuotaDejadaAsync(Loan credito, string? rulesVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credito);
        const string proporcional = AplicadorDePagos.ReglaDeLaCuotaDejadaProporcional;
        if (VersionesDeReglas.EsConocida(rulesVersion) && VersionesDeReglas.Para(rulesVersion).PrepagoSinPenalidad) return new(proporcional, MotivoVivienda);
        var tope = await TopeAsync(credito, ct);
        if (tope is null) return new(proporcional, MotivoTopeDesconocido);
        return tope.DentroDelTope ? new(proporcional, MotivoDentroDelTope) : ReglaDeCuotaDejada.TalComoSeEnvio;
    }

    /// <summary>
    /// La cuota completa pedida (por <c>Pagos.InteresesAlPagarAntesDelVencimiento</c> o por <c>Recogida.InteresesProporcionales = No</c>) con sus
    /// excepciones legales: vivienda, dentro del tope legal del prepago o sin los valores nacionales para saberlo, el corrido.
    /// </summary>
    private async Task<ModoDeInteresesAlCancelar> CuotaCompletaConExcepcionesAsync(Loan credito, string? rulesVersion, CancellationToken ct)
    {
        const string configurado = ParametrosDeCartera.InteresesDeLaCuotaCompleta;
        if (VersionesDeReglas.EsConocida(rulesVersion) && VersionesDeReglas.Para(rulesVersion).PrepagoSinPenalidad)
            return new(configurado, ParametrosDeCartera.InteresesCorridos, MotivoVivienda);
        var tope = await TopeAsync(credito, ct);
        if (tope is null) return new(configurado, ParametrosDeCartera.InteresesCorridos, MotivoTopeDesconocido);
        return tope.DentroDelTope
            ? new(configurado, ParametrosDeCartera.InteresesCorridos, MotivoDentroDelTope)
            : new(configurado, ParametrosDeCartera.InteresesDeLaCuotaCompleta, null);
    }

    /// <summary>
    /// Lo que cuesta cancelar <paramref name="credito"/> a la fecha de <paramref name="estado"/>: el valor para cancelar y, si la cancelación cobra la
    /// cuota completa, el interés de la cuota en curso que falta por causar (<see cref="AplicadorDePagos.ValorParaCancelar"/>), nunca negativo. Un crédito
    /// en UVR (vivienda) cancela con el corrido. Lo que ofrecen el listado y el detalle de la caja, la recogida y el cruce del retiro.
    /// </summary>
    public async Task<decimal> ValorParaCancelarAsync(Loan credito, EstadoDelCredito estado, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var cuotaCompleta = !estado.EnUvr && (await ModoAsync(credito, estado.RulesVersion, estado.Fecha, ct)).CuotaCompleta;
        // 2026-11-24 (feature 025, fase 5, D3): por período calendario, sin lo causado de un tramo sin empezar (lo mismo que cobra la cancelación).
        var calendario = !estado.EnUvr && await PeriodoCalendarioAsync(estado.Fecha, credito.CreditLineId, ct);
        return AplicadorDePagos.ValorParaCancelar(estado, cuotaCompleta, calendario);
    }

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5, HU5): ¿la línea causa por período calendario a <paramref name="fecha"/> (<c>Causacion.Periodo</c>, con caída al
    /// general)? (nuevo)
    /// </summary>
    public Task<bool> PeriodoCalendarioAsync(DateOnly fecha, int lineaId, CancellationToken ct) =>
        PeriodoDeLaCuota.CalendarioAsync(parametros, fecha, lineaId, ct);

    /// <summary>
    /// 2026-11-26 (pedido del dueño): la fracción del interés de una cuota anticipada que la cooperativa descuenta en caja
    /// (<c>Pagos.DescuentoPorPagoAnticipado</c>, la de la línea con caída al general); ilegible o fuera de 0..1 = sin descuento. (nuevo)
    /// </summary>
    public async Task<decimal> DescuentoPorPagoAnticipadoAsync(DateOnly fecha, int lineaId, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.PagosDescuentoPorPagoAnticipado, fecha, null, ParameterScopeKind.CreditLine, lineaId, ct);
        return leido.IsSuccess && leido.Value.Valor is decimal fraccion && fraccion is > 0m and <= 1m ? fraccion : 0m;
    }

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T071; FR-023, D4): lo que dice <c>Pagos.CuotaEnviadaAlCancelar</c> a <paramref name="fecha"/> —el de la línea del crédito
    /// que se cancela si lo tiene, si no el general—: <c>Incluir</c>, <c>Excluir</c> o <c>Preguntar</c>; ilegible = <c>Incluir</c> (lo de siempre). (nuevo)
    /// </summary>
    public async Task<string> CuotaEnviadaAlCancelarAsync(DateOnly fecha, int lineaId, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.PagosCuotaEnviadaAlCancelar, fecha, null, ParameterScopeKind.CreditLine, lineaId, ct);
        return (leido.IsSuccess ? leido.Value.Texto?.Trim() : null) switch
        {
            ParametrosDeCartera.CuotaExcluir => ParametrosDeCartera.CuotaExcluir,
            ParametrosDeCartera.CuotaPreguntar => ParametrosDeCartera.CuotaPreguntar,
            _ => ParametrosDeCartera.CuotaIncluir,
        };
    }

    /// <summary>El tope legal del prepago del crédito con el SMMLV y <c>PREPAGO_TOPE_SMMLV</c> a la fecha del desembolso; nulo si falta algo.</summary>
    private async Task<ResultadoDeTopeDePrepago?> TopeAsync(Loan credito, CancellationToken ct)
    {
        if (credito.ApprovedAmount <= 0m) return null;
        var smmlv = await valores.LeerAsync(CodigosDeValorNacional.Smmlv, string.Empty, credito.OpenedOn, null, ct);
        if (smmlv.IsFailure || smmlv.Value.Valor is not { } salario || salario <= 0m) return null;
        var limite = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.PrepagoTopeSmmlv, credito.OpenedOn, null, ct);
        if (limite.IsFailure || limite.Value.Valor is not { } veces || veces <= 0m) return null;
        return TopeLegalDePrepago.Evaluar(credito.ApprovedAmount, salario, veces);
    }
}
