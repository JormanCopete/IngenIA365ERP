using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Payoffs;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>De dónde salió la liquidación de un crédito recogido (feature 028, R3). (nuevo)</summary>
public enum FuenteDeLaLiquidacion
{
    /// <summary>La reconstrucción a (D, R): la de siempre, con desglose exacto, modo de intereses y cuota causada.</summary>
    Reconstruccion = 1,

    /// <summary>Un crédito sin condiciones selladas: su última posición diaria hasta D.</summary>
    PosicionDiaria = 2,

    /// <summary>Un crédito sin condiciones selladas ni posición: lo que dice su libro hasta D, sin anticipos, saldos a favor ni mora.</summary>
    Libro = 3,
}

/// <summary>Por qué no se puede recoger un crédito: el código del error (<c>Lending.Payoff.*</c>), el motivo corto y el mensaje para la persona. (nuevo)</summary>
public sealed record BloqueoDeRecogida(string Codigo, string Motivo, string Mensaje)
{
    public Error ComoError() => new ErrorConDatos(Codigo, Mensaje, new { reason = Motivo });
}

/// <summary>
/// Los motivos y los errores que impiden recoger un crédito (feature 028, FR-002; api.md §1 <c>blockReason</c>, §2): en mora es una reestructuración
/// (<c>Lending.Payoff.RequiresRestructuring</c>, el mismo código del desembolso, que la tramita en Novedades); castigado, en insolvencia que negocia, sin
/// saldo o no activo, <c>Lending.Payoff.NotPayable</c> con el motivo. (nuevo)
/// </summary>
public static class ErroresDeRecogida
{
    public const string CodigoRequiereReestructuracion = ErroresDeDesembolso.CodigoRequiereReestructuracion;
    public const string CodigoNoPagable = "Lending.Payoff.NotPayable";

    public const string MotivoEnMora = "RequiresRestructuring";
    public const string MotivoCastigado = "WrittenOff";
    public const string MotivoInsolvencia = "Insolvency";
    public const string MotivoSinSaldo = "NoBalance";
    public const string MotivoNoActivo = "NotActive";

    public static BloqueoDeRecogida EnMora(string numero, int dias) => new(CodigoRequiereReestructuracion, MotivoEnMora,
        $"El crédito {numero} tiene {dias} {(dias == 1 ? "día" : "días")} de mora: recogerlo es una reestructuración. Tramítela en Novedades como una "
        + "refinanciación que lo recoge.");

    public static BloqueoDeRecogida Castigado(string numero) => new(CodigoNoPagable, MotivoCastigado,
        $"El crédito {numero} está castigado: no se recoge con un crédito nuevo.");

    public static BloqueoDeRecogida EnInsolvencia(string numero) => new(CodigoNoPagable, MotivoInsolvencia,
        $"El deudor del crédito {numero} está en un proceso de insolvencia que negocia: sus créditos no se recogen mientras dure.");

    public static BloqueoDeRecogida SinSaldo(string numero) => new(CodigoNoPagable, MotivoSinSaldo,
        $"El crédito {numero} no tiene saldo por recoger.");

    public static BloqueoDeRecogida NoActivo(string numero, LoanStatus estado) => new(CodigoNoPagable, MotivoNoActivo, estado switch
    {
        LoanStatus.PaidOff => $"El crédito {numero} ya está cancelado.",
        LoanStatus.PendingDisbursement => $"El crédito {numero} todavía no se ha girado: sólo se recoge un crédito vigente.",
        LoanStatus.Voided => $"El crédito {numero} está anulado.",
        LoanStatus.Novated => $"El crédito {numero} se novó: su saldo pasó a otro crédito.",
        _ => $"El crédito {numero} no está vigente: sólo se recoge un crédito activo.",
    });

    // 2026-11-27 (feature 028, T014): los de la consulta de productos para recoger (api.md §1 y §2). (nuevo)

    public const string CodigoNoEsDelSolicitante = "Lending.Payoff.NotOfApplicant";
    public const string CodigoProductoNoAdmitido = "Lending.Payoff.ProductNotAdmitted";
    public const string CodigoFechaPasada = "Lending.Payoff.DateInPast";

    /// <summary>El motivo de un producto que la línea del crédito nuevo no admite recoger (<c>Recogida.ProductosAdmitidos</c>).</summary>
    public const string MotivoNoAdmitido = "NotAdmitted";

    /// <summary>El motivo de un crédito que no se pudo liquidar (una regla del motor o la usura del día sin publicar).</summary>
    public const string MotivoSinLiquidar = "NotSettled";

    public static Error NoEsDelSolicitante(string persona) => new(CodigoNoEsDelSolicitante,
        $"Uno de los productos elegidos no es de {persona} como deudora: sólo se recogen sus propios productos.");

    public static Error FechaPasada() => new(CodigoFechaPasada, "La fecha proyectada de desembolso es hoy o una fecha futura.");

    public static BloqueoDeRecogida NoAdmitido(string numero) => new(CodigoProductoNoAdmitido, MotivoNoAdmitido,
        $"La línea del crédito nuevo no admite recoger créditos (parámetro Recogida.ProductosAdmitidos): el crédito {numero} no se puede recoger con ella.");

    public static BloqueoDeRecogida SinLiquidar(string numero, Error error) => new(error.Code, MotivoSinLiquidar,
        $"El crédito {numero} no se pudo liquidar a la fecha: {error.Message}");

    // 2026-11-27 (feature 028, T024): los de guardar y radicar una solicitud que recoge (api.md §2). (nuevo)

    public const string CodigoModoNoAdmitido = "Lending.Payoff.ModeNotAdmitted";
    public const string CodigoNetoNoPositivo = "Lending.Payoff.NetNotPositive";

    /// <summary>En la entrega E1 sólo se recogen créditos: una cuenta o una factura todavía no (llegan con E3 y E4).</summary>
    public static Error ClaseNoAdmitida(PayoffProductKind clase) => new ErrorConDatos(CodigoProductoNoAdmitido,
        clase == PayoffProductKind.Account
            ? "Por ahora el crédito nuevo sólo recoge créditos: las cuentas de aportes y ahorros se recogerán en otra entrega."
            : "Por ahora el crédito nuevo sólo recoge créditos: las facturas a crédito se recogerán en otra entrega.",
        new { kind = clase.ToString() });

    /// <summary>La línea del crédito nuevo admite recoger créditos, pero no de ese modo (<c>Recogida.ProductosAdmitidos</c>).</summary>
    public static Error ModoNoAdmitido(string numero, PayoffMode modo, IReadOnlyList<PayoffMode> admitidos) => new ErrorConDatos(CodigoModoNoAdmitido,
        $"La línea del crédito nuevo no admite recoger el crédito {numero} {TextoDelModo(modo)}. Admite: {string.Join(", ", admitidos.Select(TextoDelModo))}.",
        new { number = numero, mode = modo.ToString(), admittedModes = admitidos.Select(m => m.ToString()).ToList() });

    /// <summary>Lo que se recoge deja el neto a girar en cero o negativo: así no se radica (FR-007).</summary>
    public static Error NetoNoPositivo(decimal monto, decimal recogido) => new ErrorConDatos(CodigoNetoNoPositivo,
        $"Lo que se recoge ({Pesos(recogido)}) deja el neto a girar del crédito nuevo ({Pesos(monto)}) en {Pesos(monto - recogido)}: suba el monto o "
        + "recoja menos.",
        new { amount = monto, payoffTotal = recogido, net = monto - recogido });

    private static string TextoDelModo(PayoffMode modo) => modo switch
    {
        PayoffMode.Total => "total",
        PayoffMode.Partial => "parcial",
        PayoffMode.Installments => "por cuotas",
        PayoffMode.Deposit => "como abono",
        _ => modo.ToString(),
    };

    private static string Pesos(decimal valor) => "$" + valor.ToString("#,##0.##", CultureInfo.GetCultureInfo("es-CO"));
}

/// <summary>
/// La liquidación de un crédito recogido a la fecha proyectada D con la información hasta R (feature 028, R3; FR-003, FR-024): lo que cobraría el
/// <c>FullPayoff</c> de la recogida por el contrato (<see cref="ValorARecoger"/>, al peso), su desglose por concepto, el modo de intereses con su
/// explicación, los días de mora, lo que impide recogerlo, la cuota causada, la huella y los avisos. (nuevo)
/// </summary>
public sealed record LiquidacionDeCreditoRecogido
{
    public required int LoanId { get; init; }
    public required Guid LoanPublicId { get; init; }
    public required string Number { get; init; }

    /// <summary>D.</summary>
    public required DateOnly Fecha { get; init; }

    /// <summary>R: lo conocido hasta ese instante (ahora, si no se pidió otro).</summary>
    public required DateTime InformacionHasta { get; init; }

    /// <summary>Lo que cobra la recogida total: exactamente lo que aplica el contrato con <c>FullPayoff</c> y el documento del desembolso.</summary>
    public required decimal ValorARecoger { get; init; }

    /// <summary>Por concepto; su <see cref="DesgloseDeRecogida.Total"/> es <see cref="ValorARecoger"/> (salvo un crédito sin saldo, que no se recoge).</summary>
    public required DesgloseDeRecogida Desglose { get; init; }

    /// <summary>Cómo cobra el interés de la cuota en curso: <c>Corrido</c> o <c>CuotaCompleta</c>, con su motivo legal si difiere de lo pedido.</summary>
    public required ModoDeInteresesAlCancelar Intereses { get; init; }

    /// <summary>Lo que dice <c>Recogida.InteresesProporcionales</c> para la línea del crédito a D: <c>ComoAlCancelar</c>, <c>Si</c> o <c>No</c>.</summary>
    public required string Proporcionales { get; init; }

    /// <summary>El modo de intereses en palabras (por qué se cobra el corrido o la cuota completa); nula si no hay nada que explicar.</summary>
    public string? ExplicacionDeIntereses { get; init; }

    /// <summary>La línea causa por período calendario a D (lo causado de un tramo sin empezar no se cobra: <see cref="DesgloseDeRecogida.InteresDevuelto"/>).</summary>
    public bool PeriodoCalendario { get; init; }

    public int DiasDeMora { get; init; }

    /// <summary>Lo que impide recogerlo, en orden; vacío si se puede.</summary>
    public IReadOnlyList<BloqueoDeRecogida> Bloqueos { get; init; } = [];

    /// <summary>La cuota del período en curso a D (en E1 siempre se incluye en la recogida); nula sin cuota por vencer o sin reconstrucción.</summary>
    public CuotaCausadaDeRecogida? CuotaCausada { get; init; }

    /// <summary>El saldo a favor del deudor en el crédito: la cancelación no lo usa (D7), así que no se descuenta; queda a favor y se resuelve aparte.</summary>
    public decimal SaldoAFavorSinDescontar { get; init; }

    /// <summary><c>sha256:</c> + la huella de la reconstrucción con el modo de intereses, el período calendario y el valor.</summary>
    public required string Huella { get; init; }

    public IReadOnlyList<string> Avisos { get; init; } = [];

    public FuenteDeLaLiquidacion Fuente { get; init; } = FuenteDeLaLiquidacion.Reconstruccion;

    public bool Recogible => Bloqueos.Count == 0;

    public BloqueoDeRecogida? Bloqueo => Bloqueos.Count == 0 ? null : Bloqueos[0];

    /// <summary>Lo que guarda la solicitud (<c>LND_CreditApplicationPayoffs</c>) con el tratamiento de la cuota causada ya resuelto (en E1, incluirla).</summary>
    public ProyeccionDeRecogida Proyeccion(InstallmentTreatment tratamiento = InstallmentTreatment.Include) =>
        new(Fecha, InformacionHasta, Desglose, Huella, Intereses.Efectivo, tratamiento, CuotaCausada);
}

/// <summary>
/// <b>Un solo sitio liquida una recogida</b> (feature 028, R3, T010; FR-003, FR-024): la consulta de productos (la proyección a D con R), la vista previa
/// y el comando del desembolso dicen lo mismo al peso, y lo mismo que aplica el contrato. Sale de la reconstrucción (<see cref="CargadorDeReconstruccion"/>
/// + <see cref="MotorDeCartera.Reconstruir"/>) a D con lo conocido hasta R —una D futura se proyecta sin pagos con la última usura conocida, como la
/// liquidación del crédito—, aplica el modo de intereses de la recogida (<see cref="InteresesAlCancelar.ModoAlRecogerAsync"/>, <c>Recogida.InteresesProporcionales</c>
/// con las excepciones legales) y el período calendario de la línea, y cobra lo mismo que <see cref="AplicadorDePagos.ValorParaCancelar"/>, que es lo que
/// reparte el <c>FullPayoff</c> de la recogida (T050). Un crédito sin condiciones selladas cae a su última posición diaria o, sin ella, a su libro, como
/// <c>MotorDeCreditoEnDesembolso</c>. No audita ni asegura el alcance: eso lo hace quien llama. No escribe. La vigila
/// <c>LaRecogidaSeLiquidaEnUnSoloSitio</c>. (nuevo)
/// </summary>
public sealed class LiquidacionDeRecogida(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    InteresesAlCancelar intereses,
    IDateTimeService reloj)
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>
    /// La liquidación para recoger <paramref name="credito"/> a <paramref name="fecha"/> (D) con lo conocido hasta <paramref name="informacionHasta"/> (R; nulo,
    /// ahora). Falla sólo si no se puede liquidar: una regla del motor (<c>ReglaDeCarteraException</c>) o, con D ≤ hoy, la usura del día sin publicar
    /// (<c>NationalValues.Missing</c>, como la liquidación del crédito). Lo que impide recogerlo (mora, castigo, insolvencia, sin saldo, no activo) no es una
    /// falla: va en <see cref="LiquidacionDeCreditoRecogido.Bloqueos"/>.
    /// </summary>
    public async Task<Result<LiquidacionDeCreditoRecogido>> LiquidarCreditoAsync(Loan credito, DateOnly fecha, DateTime? informacionHasta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credito);
        var r = informacionHasta is { } instante
            ? DateTime.SpecifyKind(instante.Kind == DateTimeKind.Local ? instante.ToUniversalTime() : instante, DateTimeKind.Utc)
            : (DateTime?)null;
        var hasta = r ?? reloj.UtcNow;
        var insolvencia = await db.InsolvencyProceedings.AsNoTracking()
            .AnyAsync(i => i.PersonId == credito.DebtorPersonId && i.Status == InsolvencyProceedingStatus.Negotiating && !i.IsDeleted, ct);

        var cargado = await cargador.CargarAsync(credito.Id, r, fecha, ct);
        if (cargado is { Listo: true })
        {
            EstadoDelCredito estado;
            try
            {
                estado = MotorDeCartera.Reconstruir(cargado.Entrada!, fecha);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Result.Failure<LiquidacionDeCreditoRecogido>(ErroresDeGobierno.De(ex.Problema));
            }
            if (estado.Origen is not null)
            {
                if (fecha <= reloj.HoyLocal && cargado.Entrada!.Condiciones.UsuraRequerida && estado.PrimerDiaSinUsura is { } sinUsura && sinUsura <= fecha)
                    return Result.Failure<LiquidacionDeCreditoRecogido>(ErroresDeValoresNacionales.Faltante(CodigosDeValorNacional.Usura,
                        cargado.Series?.ClaveDeUsura ?? cargado.Pactadas.ModalityCode, sinUsura));
                return Result.Success(await DeLaReconstruccionAsync(credito, estado, hasta, insolvencia, ct));
            }
        }
        return Result.Success(await SinReconstruccionAsync(credito, fecha, r, hasta, insolvencia, ct));
    }

    /// <summary>La liquidación de la reconstrucción: el modo de la recogida, el período calendario y el desglose de lo que cobra el <c>FullPayoff</c>.</summary>
    private async Task<LiquidacionDeCreditoRecogido> DeLaReconstruccionAsync(Loan credito, EstadoDelCredito estado, DateTime hasta, bool insolvencia,
        CancellationToken ct)
    {
        var d = estado.Fecha;
        var proporcionales = await intereses.RecogidaConfiguradaAsync(d, credito.CreditLineId, ct);
        // Como la recogida por el contrato (AplicacionDePagos.ModoAsync): un crédito en UVR (vivienda) va con el corrido y sin período calendario.
        var modo = estado.EnUvr ? ModoDeInteresesAlCancelar.Corrido : await intereses.ModoAlRecogerAsync(credito, estado.RulesVersion, d, ct);
        var calendario = !estado.EnUvr && await intereses.PeriodoCalendarioAsync(d, credito.CreditLineId, ct);

        var valor = AplicadorDePagos.ValorParaCancelar(estado, modo.CuotaCompleta, calendario);
        var cobrado = modo.CuotaCompleta ? AplicadorDePagos.InteresDeLaCuotaCompleta(estado, calendario) : [];
        var devuelto = calendario ? AplicadorDePagos.InteresSinTramo(estado, d) : [];
        decimal S(params LoanConcept[] conceptos) => conceptos.Sum(c => estado.Saldo(c));
        var desglose = new DesgloseDeRecogida(
            Capital: S(LoanConcept.Principal),
            InteresCorriente: S(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense),
            InteresDevuelto: devuelto.Sum(l => l.Monto),
            InteresCobradoPorProporcionales: cobrado.Sum(l => l.Monto),
            Mora: S(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense),
            Seguro: S(LoanConcept.Insurance),
            Administracion: S(LoanConcept.Administration),
            OtrosCargos: S(LoanConcept.CollectionFee, LoanConcept.OtherCharge),
            Anticipo: estado.Anticipo,
            // D7: la cancelación usa el anticipo del mismo crédito, nunca el saldo a favor; no se descuenta (va en los avisos).
            SaldoAFavor: 0m);

        var avisos = new List<string>();
        var explicacion = Explicacion(proporcionales, modo);
        if (explicacion is not null) avisos.Add(explicacion);
        if (cobrado.Count > 0 && cobrado[0].Cuota is { } enCurso && estado.Plan?.Cuotas.FirstOrDefault(c => c.Numero == enCurso) is { } cuotaEnCurso)
            avisos.Add($"Para recoger se cobra el interés completo de la cuota {enCurso} (vence el {cuotaEnCurso.Vencimiento:dd/MM/yyyy}): "
                + $"{Pesos(desglose.InteresCobradoPorProporcionales)} que todavía no se ha causado.");
        if (devuelto.Count > 0)
            avisos.Add($"Por período calendario no se cobra el interés ya causado de {Cuotas(devuelto)}, cuyo período no ha empezado: "
                + $"{Pesos(desglose.InteresDevuelto)}.");
        if (estado.SaldoAFavor > 0m)
            avisos.Add($"El crédito tiene un saldo a favor de {Pesos(estado.SaldoAFavor)} que la recogida no descuenta: queda a favor del deudor y se "
                + "resuelve aparte.");
        if (estado.EnUvr)
            avisos.Add($"Crédito en UVR: se recoge en pesos a la UVR del {d:dd/MM/yyyy}, con el interés corrido (Ley 546 de 1999).");
        if (estado.ValidaHasta is { } validaHasta)
            avisos.Add($"La liquidación usa la última usura conocida desde el {validaHasta.AddDays(1):dd/MM/yyyy}: vale hasta el {validaHasta:dd/MM/yyyy}.");
        if (estado.Suspendida) avisos.Add("La causación está suspendida: los intereses van a suspenso.");

        var huella = "sha256:" + HuellaDeCartera.DeTexto(estado.Huella + "|" + new HuellaDeCartera()
            .Texto("fuente", nameof(FuenteDeLaLiquidacion.Reconstruccion))
            .Texto("intereses", modo.Efectivo)
            .Texto("motivo", modo.Motivo)
            .Texto("proporcionales", proporcionales)
            .Logico("periodoCalendario", calendario)
            .Decimal("valor", valor)
            .Canonico());

        return new LiquidacionDeCreditoRecogido
        {
            LoanId = credito.Id, LoanPublicId = credito.PublicId, Number = credito.Number, Fecha = d, InformacionHasta = hasta,
            ValorARecoger = valor, Desglose = desglose, Intereses = modo, Proporcionales = proporcionales, ExplicacionDeIntereses = explicacion,
            PeriodoCalendario = calendario, DiasDeMora = estado.AlturaDeMora.Dias,
            Bloqueos = Bloqueos(credito, insolvencia, estado.AlturaDeMora.Dias, valor),
            CuotaCausada = CuotaCausada(estado, calendario), SaldoAFavorSinDescontar = Math.Max(0m, estado.SaldoAFavor),
            Huella = huella, Avisos = avisos, Fuente = FuenteDeLaLiquidacion.Reconstruccion,
        };
    }

    /// <summary>
    /// Un crédito sin condiciones selladas (o sin poder armar su reconstrucción): su última posición diaria hasta D o, sin ella, lo que dice su libro, sin
    /// anticipos ni saldos a favor y sin mora —lo mismo que <c>MotorDeCreditoEnDesembolso</c>—. Sin modo de intereses ni cuota causada. Con R, sólo lo
    /// registrado hasta R.
    /// </summary>
    private async Task<LiquidacionDeCreditoRecogido> SinReconstruccionAsync(Loan credito, DateOnly fecha, DateTime? r, DateTime hasta, bool insolvencia,
        CancellationToken ct)
    {
        var proporcionales = await intereses.RecogidaConfiguradaAsync(fecha, credito.CreditLineId, ct);
        var posiciones = db.LoanDailyPositions.AsNoTracking().Where(p => p.LoanId == credito.Id && p.PositionDate <= fecha);
        if (r is { } corte) posiciones = posiciones.Where(p => p.RegisteredAt <= corte);
        var posicion = await posiciones.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).FirstOrDefaultAsync(ct);

        DesgloseDeRecogida desglose;
        decimal valor;
        int dias;
        FuenteDeLaLiquidacion fuente;
        string huella;
        var avisos = new List<string>();
        if (posicion is not null)
        {
            desglose = new DesgloseDeRecogida(
                Capital: posicion.Principal,
                InteresCorriente: posicion.CurrentInterest + posicion.CurrentInterestInSuspense,
                Mora: posicion.DefaultInterest + posicion.DefaultInterestInSuspense,
                Seguro: posicion.Insurance,
                Administracion: posicion.Administration,
                OtrosCargos: posicion.CollectionFee + posicion.OtherCharge,
                Anticipo: posicion.Advance);
            valor = posicion.PayoffAmount;
            // Lo que la posición no desglosa (una posición de antes de que llevara todos los conceptos) va en otros cargos: el total es el de la posición.
            var sinDesglosar = valor - desglose.Total;
            if (sinDesglosar != 0m) desglose = desglose with { OtrosCargos = desglose.OtrosCargos + sinDesglosar };
            dias = posicion.DaysPastDue;
            fuente = FuenteDeLaLiquidacion.PosicionDiaria;
            avisos.Add($"El crédito no tiene condiciones selladas: la liquidación sale de su posición del {posicion.PositionDate:dd/MM/yyyy}, sin intereses "
                + "proporcionales.");
            if (posicion.CreditBalance > 0m)
                avisos.Add($"El crédito tiene un saldo a favor de {Pesos(posicion.CreditBalance)} que la recogida no descuenta: queda a favor del deudor y se "
                    + "resuelve aparte.");
            huella = new HuellaDeCartera()
                .Texto("fuente", nameof(FuenteDeLaLiquidacion.PosicionDiaria))
                .Fecha("fecha", posicion.PositionDate)
                .Entero("version", posicion.Version)
                .Texto("insumos", posicion.InputsSha256)
                .Decimal("valor", valor)
                .Canonico();
        }
        else
        {
            var filas = db.LoanTransactions.AsNoTracking()
                .Where(t => t.LoanId == credito.Id && t.ValueDate <= fecha && !t.InControl
                            && t.Concept != LoanConcept.Advance && t.Concept != LoanConcept.CreditBalance
                            // 2026-11-16 (feature 024, fase 4): lo reconocido al inicio del ciclo no es deuda.
                            && t.Concept < LoanConcept.RecognizedInterest);
            if (r is { } corteDelLibro) filas = filas.Where(t => t.RegisteredAt <= corteDelLibro);
            var porConcepto = (await filas.GroupBy(t => t.Concept).Select(g => new { Concepto = g.Key, Total = g.Sum(t => t.Amount) }).ToListAsync(ct))
                .ToDictionary(x => x.Concepto, x => x.Total);
            decimal S(params LoanConcept[] conceptos) => conceptos.Sum(c => porConcepto.GetValueOrDefault(c));
            desglose = new DesgloseDeRecogida(
                Capital: S(LoanConcept.Principal),
                InteresCorriente: S(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense),
                Mora: S(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense),
                Seguro: S(LoanConcept.Insurance),
                Administracion: S(LoanConcept.Administration),
                OtrosCargos: S(LoanConcept.CollectionFee, LoanConcept.OtherCharge));
            valor = Math.Max(0m, porConcepto.Values.Sum());
            dias = 0;
            fuente = FuenteDeLaLiquidacion.Libro;
            avisos.Add("El crédito no tiene condiciones selladas ni posición diaria: la liquidación sale de su libro, sin intereses proporcionales ni mora.");
            huella = new HuellaDeCartera()
                .Texto("fuente", nameof(FuenteDeLaLiquidacion.Libro))
                .Lista("conceptos", porConcepto.OrderBy(x => x.Key), (h, x) => h.Enumeracion("concepto", (LoanConcept?)x.Key).Decimal("total", x.Value))
                .Decimal("valor", valor)
                .Canonico();
        }

        return new LiquidacionDeCreditoRecogido
        {
            LoanId = credito.Id, LoanPublicId = credito.PublicId, Number = credito.Number, Fecha = fecha, InformacionHasta = hasta,
            ValorARecoger = valor, Desglose = desglose, Intereses = ModoDeInteresesAlCancelar.Corrido, Proporcionales = proporcionales,
            DiasDeMora = dias, Bloqueos = Bloqueos(credito, insolvencia, dias, valor), SaldoAFavorSinDescontar = Math.Max(0m, posicion?.CreditBalance ?? 0m),
            Huella = "sha256:" + HuellaDeCartera.DeTexto(huella), Avisos = avisos, Fuente = fuente,
        };
    }

    /// <summary>Lo que impide recogerlo, en este orden: castigado o no activo, insolvencia que negocia, mora (reestructuración) y sin saldo.</summary>
    private static List<BloqueoDeRecogida> Bloqueos(Loan credito, bool insolvencia, int diasDeMora, decimal valor)
    {
        var bloqueos = new List<BloqueoDeRecogida>();
        if (credito.Status == LoanStatus.WrittenOff) bloqueos.Add(ErroresDeRecogida.Castigado(credito.Number));
        else if (credito.Status != LoanStatus.Active) bloqueos.Add(ErroresDeRecogida.NoActivo(credito.Number, credito.Status));
        if (insolvencia) bloqueos.Add(ErroresDeRecogida.EnInsolvencia(credito.Number));
        if (diasDeMora > 0) bloqueos.Add(ErroresDeRecogida.EnMora(credito.Number, diasDeMora));
        if (valor <= 0m && credito.Status == LoanStatus.Active) bloqueos.Add(ErroresDeRecogida.SinSaldo(credito.Number));
        return bloqueos;
    }

    /// <summary>
    /// La cuota del período en curso a la fecha del estado —la próxima que vence o, por período calendario, la que vence en el tramo en curso (la misma que
    /// la caja ofrece como cuota del período)— con lo que le falta por pagar al vencer (su valor menos lo anticipado).
    /// </summary>
    private static CuotaCausadaDeRecogida? CuotaCausada(EstadoDelCredito estado, bool calendario)
    {
        if (estado.Plan is not { } plan) return null;
        var delTramo = calendario ? AplicadorDePagos.CuotasDelTramoEnCurso(plan, estado.Fecha) : null;
        var cuota = delTramo is null
            ? plan.Cuotas.FirstOrDefault(c => c.Vencimiento > estado.Fecha)
            : plan.Cuotas.FirstOrDefault(c => delTramo.Contains(c.Numero));
        return cuota is null ? null : new CuotaCausadaDeRecogida(cuota.Numero, AplicadorDePagos.ValorParaAnticipar(estado, cuota.Numero), cuota.Vencimiento);
    }

    /// <summary>El modo de intereses en palabras: el motivo legal si lo hay; si no, lo que pidió la recogida.</summary>
    private static string? Explicacion(string proporcionales, ModoDeInteresesAlCancelar modo) =>
        modo.Explicacion ?? proporcionales switch
        {
            ParametrosDeCartera.ProporcionalesSi =>
                "Se cobra el interés corrido hasta la fecha del desembolso: la cooperativa cobra o devuelve los intereses proporcionales al recoger.",
            ParametrosDeCartera.ProporcionalesNo when modo.CuotaCompleta =>
                "Se cobra completo el interés de la cuota en curso: la cooperativa no hace proporcionales al recoger.",
            _ when modo.CuotaCompleta => "Se cobra completo el interés de la cuota en curso, como en una cancelación en caja.",
            _ => null,
        };

    private static string Cuotas(IReadOnlyList<LineaDeDistribucion> lineas)
    {
        var numeros = lineas.Select(l => l.Cuota).OfType<int>().Distinct().Order().ToList();
        return numeros.Count == 1 ? $"la cuota {numeros[0]}" : $"las cuotas {string.Join(", ", numeros)}";
    }

    private static string Pesos(decimal valor) => "$" + valor.ToString("#,##0.##", Colombia);
}
