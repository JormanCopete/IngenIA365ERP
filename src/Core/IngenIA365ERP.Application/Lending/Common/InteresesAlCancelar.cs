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
        return AplicadorDePagos.ValorParaCancelar(estado, cuotaCompleta);
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
