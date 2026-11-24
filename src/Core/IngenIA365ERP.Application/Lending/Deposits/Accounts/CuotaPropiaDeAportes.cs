using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

/// <summary>
/// 2026-11-24: lo que se digita de la cuota de una cuenta de aportes al abrirla o al corregirla. Con <c>Aportes.OrigenDeLaCuota = DeLaCuenta</c> van la
/// cuota, la periodicidad, la fecha del primer pago y el canal; con <c>DeLaLinea</c>, sólo el canal (opcional). (nuevo)
/// </summary>
public sealed record ContributionAccountInstallmentInput(
    decimal? Amount = null,
    RepaymentPeriodicity? Periodicity = null,
    DateOnly? FirstDueDate = null,
    RepaymentChannel? PaymentChannel = null);

/// <summary>Las condiciones de la cuota de una cuenta de aportes: las propias si las tiene; si no, rige la línea. (nuevo)</summary>
public sealed record ContributionAccountInstallmentDto(
    bool OwnInstallment,
    decimal? Amount,
    RepaymentPeriodicity? Periodicity,
    DateOnly? FirstDueDate,
    RepaymentChannel? PaymentChannel);

/// <summary>Lo que la apertura de una cuenta de aportes pide hoy: el origen de la cuota y los canales de pago que se ofrecen. (nuevo)</summary>
public sealed record ContributionInstallmentModeDto(string Mode, IReadOnlyList<RepaymentChannel> Channels);

/// <summary>
/// 2026-11-24 (pedido del dueño): la cuota de una cuenta de aportes puede ser propia de la cuenta. El parámetro
/// <see cref="ParametrosDeCartera.AportesOrigenDeLaCuota"/> decide si se digita al abrirla (<c>DeLaCuenta</c>) o rige la de la línea (<c>DeLaLinea</c>, de
/// fábrica). Lo digitado queda en la cuenta y lo usa el generador de cuotas, aunque después cambie el parámetro. El canal de pago dice quién descuenta:
/// la nómina propia, la pagaduría o ninguno de los dos (caja); el débito automático no se ofrece porque hoy sólo paga créditos. (nuevo)
/// </summary>
public static class CuotaPropiaDeAportes
{
    public const string CodigoCuotaRequerida = "Lending.Contributions.InstallmentRequired";
    public const string CodigoCuotaDeLaLinea = "Lending.Contributions.InstallmentFromLine";
    public const string CodigoCuotaInvalida = "Lending.Contributions.InstallmentInvalid";
    public const string CodigoCanalNoAdmitido = "Lending.Contributions.ChannelNotAllowed";
    public const string CodigoSoloAportes = "Lending.Contributions.InstallmentOnlyForContributions";

    /// <summary>Los canales que se ofrecen para una cuenta de aportes.</summary>
    public static IReadOnlyList<RepaymentChannel> Canales { get; } =
        [RepaymentChannel.Cashier, RepaymentChannel.OwnPayroll, RepaymentChannel.PaymasterDeduction];

    /// <summary>¿La cooperativa pide la cuota al abrir cada cuenta a <paramref name="fecha"/>? Si el parámetro no se lee, su defecto (la de la línea).</summary>
    public static async Task<bool> DeLaCuentaAsync(ReglasDeParametrosDeCartera parametros, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.AportesOrigenDeLaCuota, fecha, ct: ct);
        return leido.IsSuccess && string.Equals(leido.Value.Valor?.ToString(), ParametrosDeCartera.CuotaDeLaCuenta, StringComparison.Ordinal);
    }

    /// <inheritdoc cref="DeLaCuentaAsync(ReglasDeParametrosDeCartera, DateOnly, CancellationToken)"/>
    public static async Task<bool> DeLaCuentaAsync(ILectorDeParametros lector, DateOnly fecha, CancellationToken ct)
    {
        var leido = await lector.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.AportesOrigenDeLaCuota, fecha, ct: ct);
        return leido.IsSuccess && string.Equals(leido.Value.Valor?.ToString(), ParametrosDeCartera.CuotaDeLaCuenta, StringComparison.Ordinal);
    }

    /// <summary>
    /// Valida lo digitado. Con <paramref name="deLaCuenta"/>: cuota mayor que cero, periodicidad con cuotas, fecha del primer pago no anterior a la
    /// apertura y canal, todos obligatorios. Sin él: sólo el canal, opcional; la cuota, la periodicidad y la fecha son de la línea.
    /// </summary>
    public static Result Validar(ContributionAccountInstallmentInput? e, bool deLaCuenta, DateOnly apertura)
    {
        if (e?.PaymentChannel is { } canal && !Canales.Contains(canal))
            return Result.Failure(new ErrorConDatos(CodigoCanalNoAdmitido,
                "Una cuenta de aportes se paga por caja, por nómina o por pagaduría; el débito automático sólo paga créditos.",
                new { field = "contributionInstallment.paymentChannel" }));
        if (!deLaCuenta)
        {
            return e is { Amount: not null } or { Periodicity: not null } or { FirstDueDate: not null }
                ? Result.Failure(new ErrorConDatos(CodigoCuotaDeLaLinea,
                    "La cooperativa toma la cuota de aportes de la línea: no se digitan cuota, periodicidad ni fecha del primer pago. Para digitarlas, "
                    + "cambie «Aportes.OrigenDeLaCuota» a «DeLaCuenta» en Parámetros de Cartera.", new { field = "contributionInstallment" }))
                : Result.Success();
        }

        var faltan = new List<string>();
        if (e?.Amount is null) faltan.Add("la cuota");
        if (e?.Periodicity is null) faltan.Add("la periodicidad");
        if (e?.FirstDueDate is null) faltan.Add("la fecha del primer pago");
        if (e?.PaymentChannel is null) faltan.Add("el canal de pago");
        if (faltan.Count > 0)
            return Result.Failure(new ErrorConDatos(CodigoCuotaRequerida,
                $"La cooperativa pide la cuota de cada cuenta de aportes: falta {string.Join(", ", faltan)}.", new { field = "contributionInstallment", missing = faltan }));
        if (e!.Amount <= 0m) return Invalida("La cuota de aportes es mayor que cero.", "amount");
        if (CuotasDeAportes.MesesPorPeriodo(e.Periodicity!.Value) == 0) return Invalida("Las cuotas de aportes son periódicas: elija una periodicidad.", "periodicity");
        if (e.FirstDueDate < apertura)
            return Invalida($"La fecha del primer pago no puede ser anterior a la apertura de la cuenta ({apertura:dd/MM/yyyy}).", "firstDueDate");
        return Result.Success();
    }

    /// <summary>Deja en la cuenta lo digitado (lo que no viene queda nulo: rige la línea).</summary>
    public static void Aplicar(MemberAccount cuenta, ContributionAccountInstallmentInput? e)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        cuenta.ContributionInstallmentAmount = e?.Amount;
        cuenta.ContributionInstallmentPeriodicity = e?.Periodicity;
        cuenta.ContributionFirstDueDate = e?.FirstDueDate;
        cuenta.ContributionPaymentChannel = e?.PaymentChannel;
    }

    /// <summary>Las condiciones de la cuota de una cuenta de aportes; nulo en las demás naturalezas.</summary>
    public static ContributionAccountInstallmentDto? Dto(MemberAccount cuenta) => cuenta.Nature != LineNature.Contribution
        ? null
        : new ContributionAccountInstallmentDto(cuenta.ContributionInstallmentAmount is not null, cuenta.ContributionInstallmentAmount,
            cuenta.ContributionInstallmentPeriodicity, cuenta.ContributionFirstDueDate, cuenta.ContributionPaymentChannel);

    /// <summary>¿Trae algún dato?</summary>
    public static bool TieneDatos(ContributionAccountInstallmentInput? e) =>
        e is { Amount: not null } or { Periodicity: not null } or { FirstDueDate: not null } or { PaymentChannel: not null };

    public static Error SoloAportes() => new ErrorConDatos(CodigoSoloAportes,
        "La cuota propia, la periodicidad, la fecha del primer pago y el canal de pago son de las cuentas de aportes.", new { field = "contributionInstallment" });

    private static Result Invalida(string mensaje, string campo) =>
        Result.Failure(new ErrorConDatos(CodigoCuotaInvalida, mensaje, new { field = $"contributionInstallment.{campo}" }));
}
