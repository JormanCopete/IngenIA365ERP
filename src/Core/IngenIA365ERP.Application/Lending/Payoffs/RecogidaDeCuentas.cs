using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payoffs;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>
/// Una cuenta del titular que un crédito nuevo podría recoger, leída a la fecha D (feature 028, HU6, T078; FR-001, FR-002): su producto
/// (<see cref="ProductosAdmitidos.DeCuenta"/>; nulo en un CDAT, que nunca se recoge), su línea, las cuotas pendientes que vencen a D o antes (lo que se
/// recoge «por cuotas»), si la línea admite un abono libre (en aportes, sólo con aportes extraordinarios) y lo que impide recogerla (la cuenta no operable o el
/// titular fallecido). (nuevo)
/// </summary>
public sealed record CuentaRecogible(MemberAccount Cuenta, ProductoDeRecogida? Producto, string CodigoDeLinea, string Linea, IReadOnlyList<CuotaLeida> Cuotas,
    bool AdmiteAbono, string? MotivoSinAbono, BloqueoDeRecogida? Bloqueo)
{
    /// <summary>Lo que se debe de las cuotas que vencen a D o antes.</summary>
    public decimal Pendiente => Cuotas.Sum(c => c.Pendiente);

    /// <summary>
    /// Los modos con que se puede recoger según <paramref name="admision"/> (<c>Recogida.ProductosAdmitidos</c> de la línea del crédito nuevo) y la línea de la
    /// cuenta: el abono, sólo si la línea lo admite.
    /// </summary>
    public IReadOnlyList<PayoffMode> Admitidos(AdmisionDeRecogida admision)
    {
        ArgumentNullException.ThrowIfNull(admision);
        return Producto is { } p
            ? ProductosAdmitidos.ModosPosibles[p].Where(m => admision.Admite(p, m) && (m != PayoffMode.Deposit || AdmiteAbono)).ToList()
            : [];
    }

    /// <summary>Lo que se recoge en <paramref name="modo"/>: por cuotas, lo pendiente a D; como abono, el valor pedido.</summary>
    public decimal Valor(PayoffMode modo, decimal? pedido) => modo == PayoffMode.Installments ? Pendiente : pedido ?? 0m;

    /// <summary>
    /// <c>sha256:</c> + la huella de lo que se recoge de la cuenta a D (como la de un crédito, <c>LND_CreditApplicationPayoffs.ProjectionSha256</c>): la cuenta,
    /// el modo, el valor y cada cuota con lo pendiente (por cuotas).
    /// </summary>
    public string Huella(DateOnly d, PayoffMode? modo = null, decimal? pedido = null) => "sha256:" + new HuellaDeCartera()
        .Identificador("account", Cuenta.PublicId)
        .Fecha("date", d)
        .Enumeracion("mode", modo)
        .Decimal("amount", modo is { } m ? Valor(m, pedido) : null, 2)
        .Lista("installments", Cuotas, (h, c) => h.Identificador("installment", c.Cuota.PublicId).Decimal("pending", c.Pendiente, 2))
        .Calcular();
}

/// <summary>
/// Una cuenta que el desembolso recoge, evaluada a la fecha valor (feature 028, HU6, T077; FR-025): cómo (<see cref="Modo"/>, cuotas o abono), lo que se le
/// consigna (<see cref="AAplicar"/>), las cuotas que cubre, lo que proyectó la solicitud, lo de ahora, la diferencia con su causa, si se omite porque ya no hay
/// nada que recoger y la validación previa contable con que se sella la consignación. (nuevo)
/// </summary>
public sealed record RecogidaDeCuentaEvaluada(int AccountId, Guid AccountPublicId, string Number, PayoffMode Modo, decimal AAplicar)
{
    public int? ProyectadaId { get; init; }
    public decimal? ValorPedido { get; init; }
    public bool ConProyeccion { get; init; }
    public decimal Proyectado { get; init; }
    public decimal Actual { get; init; }
    public ResultadoDeDiferencia Diferencia { get; init; } = RecogidaEvaluada.SinDiferencia;

    /// <summary>Ya no hay nada que recoger (la cuenta se canceló, o sus cuotas se pagaron por otro camino): no se consigna y el neto sube.</summary>
    public bool Omitida { get; init; }

    public bool SobreLaTolerancia { get; init; }
    public IReadOnlyList<string> Avisos { get; init; } = [];

    /// <summary>Por cuotas: las cuotas que cubre, en su orden (las pendientes que vencen a la fecha valor o antes).</summary>
    public IReadOnlyList<Guid> Cuotas { get; init; } = [];

    public ProductoDeRecogida? Producto { get; init; }
    public int CreditLineId { get; init; }
    public string CodigoDeLinea { get; init; } = string.Empty;
    public LineNature Naturaleza { get; init; }
    public DepositClass? Clase { get; init; }
    public int BranchId { get; init; }
    public bool Embargada { get; init; }

    /// <summary>La validación previa contable de su consignación (se sella en la operación de la cuenta).</summary>
    public PrevalidationOutcome Validacion { get; init; } = PrevalidationOutcome.NotApplicable;

    public IReadOnlyList<string> AvisosDeValidacion { get; init; } = [];

    /// <summary>Confirmar exige reconocerla: una diferencia u omisión.</summary>
    public bool PorReconocer => Omitida || Diferencia.HayDiferencia;
}

/// <summary>Una cuenta recogida ya consignada en el desembolso: la recogida, la clave y el comprobante de la operación de la cuenta. (nuevo)</summary>
public sealed record RecogidaDeCuentaAplicada(RecogidaDeCuentaEvaluada Recogida, string Clave, ComprobanteDeCuentaDto Comprobante);

/// <summary>Una consignación de recogida viva de un desembolso: su operación, la cuenta, la clave y lo consignado. (nuevo)</summary>
public sealed record ConsignacionDeRecogida(long OperacionId, Guid OperacionPublicId, int CuentaId, Guid CuentaPublicId, string NumeroDeCuenta, string Clave,
    decimal Monto, LineNature Naturaleza);

/// <summary>
/// <b>La recogida de aportes y ahorros en el desembolso</b> (2026-11-27, pedido del dueño; feature 028, HU6, T077–T079; R13, FR-025): un crédito nuevo paga
/// cuotas pendientes de aportes o del ahorro contractual, o abona (aporte extraordinario, consignación al ahorro a la vista o al permanente), en la
/// transacción del desembolso y <b>por dentro de Cartera</b>: consigna con <see cref="ConsignacionEnCuentas"/> —el mismo reparto, las mismas reglas de cada
/// línea (mínimo no reducible, límite de participación, extraordinario admitido, topes, embargos) y el mismo hecho contable que una consignación del
/// contrato— y <see cref="Deposits.Ledger.RegistroDeCuentas"/>, como el cruce del retiro pero al revés (aquél debita las cuentas; ésta las acredita), con el
/// recaudador <c>CAR</c> de clase <c>OwnLoanPayoff</c>, el medio <c>InternalOffset</c> (<see cref="ConsignacionEnCurso.Interna"/>: el contrato público lo
/// sigue rechazando) y la clave <c>CAR:{desembolso:N}:{cuenta:N}</c>. Si una cuenta no recibe la consignación, el desembolso aborta con su código. La
/// consignación debita el puente <c>RecaudoPorAplicar</c>/<c>OwnLoanPayoff</c> con el deudor de tercero y la cuenta de cruce, que el comprobante del
/// desembolso acredita con las mismas dimensiones (FR-027). La anulación del crédito nuevo reversa cada consignación por <see cref="ReversionDeCuentas"/> con
/// la clave <c>{clave}:reverso</c>. Cerrojo: el desembolso toma setup → deudores → créditos → <b>estas cuentas</b> (con las de su titular embargado), antes de
/// numerar nada (research E-14). (nuevo)
/// </summary>
public sealed class RecogidaDeCuentas(
    IApplicationDbContext db,
    ConsignacionEnCuentas consignacion,
    ReversionDeCuentas reversion,
    ReglasDeAportes reglas,
    ValidacionPreviaContableDeCartera validacionPrevia,
    IDateTimeService reloj)
{
    /// <summary>Quién inicia el reverso de la consignación cuando se anula el crédito nuevo antes del giro.</summary>
    public const string IniciadoPorLaAnulacion = "LoanVoidedBeforeDisbursement";

    /// <summary>Los estados en que una cuenta se recoge (recibe una consignación): activa o inactiva (la consignación la reactiva).</summary>
    public static readonly MemberAccountStatus[] Vivas = [MemberAccountStatus.Active, MemberAccountStatus.Inactive];

    /// <summary>El recaudador de la recogida: el mismo de los créditos (<c>CAR</c>, <c>OwnLoanPayoff</c>).</summary>
    public static Recaudador Recaudador => RecogidaEnElDesembolso.Recaudador;

    /// <summary>La clave de la consignación de <paramref name="cuenta"/> en el desembolso <paramref name="desembolso"/>: <c>CAR:{desembolso:N}:{cuenta:N}</c>.</summary>
    public static string Clave(Guid desembolso, Guid cuenta) => RecogidaEnElDesembolso.Clave(desembolso, cuenta);

    /// <summary>El hecho con que la cuenta recibe la recogida: <c>AporteRecibido</c> en aportes, <c>ConsignacionEnCuentaAplicada</c> en ahorros.</summary>
    public static string HechoDeLaConsignacion(LineNature naturaleza) =>
        naturaleza == LineNature.Contribution ? AporteRecibidoV1.Type : ConsignacionEnCuentaAplicadaV1.Type;

    /// <summary>
    /// Una cuenta consignada como la lleva el hecho del desembolso (<see cref="PayoffDeductionV1"/>, T080): la clase <c>Account</c>, la cuenta, su operación de
    /// consignación, lo consignado y el hecho de la consignación (la sucursal y el pagador los completa la emisión).
    /// </summary>
    public static PayoffDeductionV1 ComoDeduccion(RecogidaDeCuentaAplicada a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new PayoffDeductionV1
        {
            ProductKind = PayoffDeductionV1.CuentaDelAsociado,
            AccountRef = new MemberAccountRefV1 { AccountPublicId = a.Recogida.AccountPublicId, AccountNumber = a.Recogida.Number },
            OperationPublicId = a.Comprobante.OperationPublicId,
            Amount = a.Comprobante.Amount,
            DepositFactType = HechoDeLaConsignacion(a.Recogida.Naturaleza),
        };
    }

    /// <summary>Una consignación viva como la lleva el hecho del desembolso (el giro nuevo, H6).</summary>
    public static PayoffDeductionV1 ComoDeduccion(ConsignacionDeRecogida c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new PayoffDeductionV1
        {
            ProductKind = PayoffDeductionV1.CuentaDelAsociado,
            AccountRef = new MemberAccountRefV1 { AccountPublicId = c.CuentaPublicId, AccountNumber = c.NumeroDeCuenta },
            OperationPublicId = c.OperacionPublicId,
            Amount = c.Monto,
            DepositFactType = HechoDeLaConsignacion(c.Naturaleza),
        };
    }

    // ------------------------------------------------------------------------------------------------------------ leer --

    /// <summary>
    /// Las cuentas como se pueden recoger a la fecha <paramref name="d"/>: su producto, su línea, las cuotas pendientes que vencen a D o antes, si su línea
    /// admite un abono y lo que lo impide. Un CDAT sale sin producto. Sólo lee.
    /// </summary>
    public async Task<IReadOnlyList<CuentaRecogible>> LeerAsync(IReadOnlyList<MemberAccount> cuentas, DateOnly d, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuentas);
        if (cuentas.Count == 0) return [];
        var ids = cuentas.Select(c => c.Id).ToList();
        var cuotas = (await LectorDeCuotas.DeCuentasAsync(db, ids, ct)).Where(c => c.Pendiente > 0m && c.Cuota.DueDate <= d)
            .GroupBy(c => c.Cuota.MemberAccountId).ToDictionary(g => g.Key, g => (IReadOnlyList<CuotaLeida>)g.ToList());
        var idsDeLinea = cuentas.Select(c => c.CreditLineId).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => idsDeLinea.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => (l.Code, l.Name), ct);
        var fallecidos = await Deposits.Deceased.TitularesFallecidos.DeAsync(db, cuentas.Select(c => c.HolderPersonId).Distinct().ToList(), ct);

        var resultado = new List<CuentaRecogible>(cuentas.Count);
        foreach (var cuenta in cuentas)
        {
            var (codigo, nombre) = lineas.GetValueOrDefault(cuenta.CreditLineId);
            codigo ??= string.Empty;
            var admiteAbono = true;
            string? sinAbono = null;
            if (cuenta.Nature == LineNature.Contribution)
            {
                var version = await reglas.VersionAsync(cuenta, d, ct);
                admiteAbono = version is { AllowsExtraordinary: true };
                if (!admiteAbono) sinAbono = ErroresDeRecogida.SinAbonoEnLaLinea(cuenta.Number, codigo).Mensaje;
            }
            var bloqueo = !Vivas.Contains(cuenta.Status)
                ? ErroresDeRecogida.CuentaNoOperable(cuenta.Number, cuenta.Status)
                : fallecidos.Contains(cuenta.HolderPersonId) ? ErroresDeRecogida.TitularFallecido(cuenta.Number) : null;
            resultado.Add(new CuentaRecogible(cuenta, ProductosAdmitidos.DeCuenta(cuenta.Nature, cuenta.DepositClass), codigo, $"{codigo} · {nombre}",
                cuotas.GetValueOrDefault(cuenta.Id) ?? [], admiteAbono, sinAbono, bloqueo));
        }
        return resultado;
    }

    /// <summary>
    /// Lo que impide recoger <paramref name="c"/> en <paramref name="modo"/> por <paramref name="pedido"/> con lo que admite la línea del crédito nuevo
    /// (<paramref name="admision"/>); nulo si se puede (HU6-1 a HU6-4):
    /// <list type="number">
    /// <item>un CDAT no se recoge (no es una deuda): <c>Lending.Payoff.ProductNotAdmitted</c>;</item>
    /// <item>la cuenta no operable o el titular fallecido: <c>Lending.Payoff.NotPayable</c>;</item>
    /// <item>la cooperativa no recoge ese producto: <c>ProductNotAdmitted</c> («la cooperativa no los recoge»); un modo que el producto no tiene (el
    /// contractual, sólo cuotas) o que la línea del crédito nuevo no admite: <c>ModeNotAdmitted</c>; un abono en aportes cuya línea no admite extraordinarios,
    /// <c>ModeNotAdmitted</c> con el motivo de la línea;</item>
    /// <item>un abono sin valor; por cuotas, sin cuotas pendientes a la fecha (<c>NotPayable</c>, sin saldo);</item>
    /// <item>lo que diga el reparto de la consignación (el límite de participación, el excedente no admitido), sin escribir.</item>
    /// </list>
    /// </summary>
    public async Task<Error?> ValidarAsync(CuentaRecogible c, PayoffMode modo, decimal? pedido, AdmisionDeRecogida admision, DateOnly d, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(admision);
        if (c.Producto is not { } producto) return ErroresDeRecogida.CdatNoSeRecoge(c.Cuenta.Number);
        if (c.Bloqueo is { } bloqueo) return bloqueo.ComoError();
        if (!admision.Admite(producto)) return ErroresDeRecogida.CuentaNoAdmitida(c.Cuenta.Number, producto).ComoError();
        if (!ProductosAdmitidos.ModosPosibles[producto].Contains(modo) || !admision.Admite(producto, modo))
            return ErroresDeRecogida.ModoDeCuentaNoAdmitido(c.Cuenta.Number, producto, modo, c.Admitidos(admision));
        if (modo == PayoffMode.Deposit && !c.AdmiteAbono) return ErroresDeRecogida.SinAbonoEnLaLinea(c.Cuenta.Number, c.CodigoDeLinea).ComoError();
        if (modo == PayoffMode.Deposit && pedido is not > 0m)
            return ErroresDeLineasDeCredito.Invalido("payoffs", $"El abono a la cuenta {c.Cuenta.Number} lleva un valor mayor que cero.");
        if (modo == PayoffMode.Installments && c.Pendiente <= 0m) return ErroresDeRecogida.SinCuotasPendientes(c.Cuenta.Number, d).ComoError();
        return await RepartoAsync(c.Cuenta, modo, c.Valor(modo, pedido), c.Cuotas.Select(q => q.Cuota.PublicId).ToList(), d, ct);
    }

    /// <summary>El reparto de la consignación sin escribir (las reglas de la línea): nulo si cabe.</summary>
    private async Task<Error?> RepartoAsync(MemberAccount cuenta, PayoffMode modo, decimal monto, IReadOnlyList<Guid> cuotas, DateOnly d, CancellationToken ct)
    {
        if (monto <= 0m) return null;
        var reparto = await consignacion.RepartirAsync(cuenta, Modalidad(modo), monto, modo == PayoffMode.Installments ? cuotas : null, d, ct);
        return reparto.IsFailure ? ErroresDeRecogida.ConLaCuenta(reparto.Error, cuenta.PublicId, cuenta.Number) : null;
    }

    /// <summary>Por cuotas, las nombradas (las pendientes a la fecha, exactas); como abono, libre (el extraordinario en aportes, el saldo en ahorros).</summary>
    public static ModalidadDeConsignacion Modalidad(PayoffMode modo) =>
        modo == PayoffMode.Installments ? ModalidadDeConsignacion.SpecificInstallments : ModalidadDeConsignacion.Free;

    // ------------------------------------------------------------------------------------------------------- desembolsar --

    /// <summary>
    /// Una cuenta autorizada por la solicitud, a la fecha valor del desembolso con lo de ahora (T077; como <c>EvaluarRecogidasAsync</c> con los créditos):
    /// <list type="number">
    /// <item>la cuenta que ya no existe o no es del solicitante como titular: <c>Lending.Payoff.NotRequested</c>;</item>
    /// <item>la cuenta cancelada, o por cuotas sin nada pendiente (se pagaron por otro camino), se <b>omite</b>: no se consigna y el neto sube;</item>
    /// <item>la cuenta no operable o el titular fallecido: <c>Lending.Payoff.NotPayable</c>;</item>
    /// <item>lo de ahora: por cuotas, lo pendiente a la fecha valor; como abono, el valor pedido. Se compara con lo proyectado: menos, por pagos; más, por las
    /// cuotas que vencieron desde la proyección; <c>Recogida.AlDesembolsar = MantenerLoProyectado</c> consigna lo proyectado topado en lo de ahora; la
    /// tolerancia bloquea como en un crédito;</item>
    /// <item>el reparto de la consignación sin escribir (las reglas de la línea).</item>
    /// </list>
    /// Los bloqueos van a <paramref name="hallados"/> y los avisos generales a <paramref name="avisos"/>; nulo si no hay recogida que evaluar.
    /// </summary>
    public async Task<RecogidaDeCuentaEvaluada?> EvaluarAsync(CreditApplicationPayoff fila, MemberAccount? cuenta, int solicitante, DateOnly fecha,
        bool mantenerLoProyectado, decimal? tolerancia, List<Error> hallados, List<string> avisos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fila);
        if (cuenta is null || cuenta.IsDeleted || cuenta.HolderPersonId != solicitante)
        {
            hallados.Add(ErroresDeRecogida.CuentaNoAutorizada(cuenta?.PublicId ?? Guid.Empty, fila.ProductNumber));
            return null;
        }
        var proyectado = fila.TieneProyeccion ? fila.Desglose() : DesgloseDeRecogida.Vacio;
        var base_ = new RecogidaDeCuentaEvaluada(cuenta.Id, cuenta.PublicId, cuenta.Number, fila.Mode, 0m)
        {
            ProyectadaId = fila.Id, ValorPedido = fila.RequestedAmount, ConProyeccion = fila.TieneProyeccion, Proyectado = proyectado.Total,
            Producto = ProductosAdmitidos.DeCuenta(cuenta.Nature, cuenta.DepositClass), CreditLineId = cuenta.CreditLineId, Naturaleza = cuenta.Nature,
            Clase = cuenta.DepositClass, BranchId = cuenta.BranchId, Embargada = cuenta.IsGarnished,
        };
        if (cuenta.Status == MemberAccountStatus.Closed)
            return Omitida(base_, proyectado, $"La cuenta {cuenta.Number} ya está cancelada.", PayoffDifferenceCause.ProductClosed, avisos);

        var leida = (await LeerAsync([cuenta], fecha, ct))[0];
        base_ = base_ with { CodigoDeLinea = leida.CodigoDeLinea };
        if (leida.Bloqueo is { } bloqueo)
        {
            hallados.Add(bloqueo.ComoError());
            return base_;
        }
        if (fila.Mode == PayoffMode.Installments && leida.Pendiente <= 0m)
            return Omitida(base_, proyectado, $"Las cuotas de la cuenta {cuenta.Number} ya se pagaron.", PayoffDifferenceCause.Payments, avisos);

        var actual = leida.Valor(fila.Mode, fila.RequestedAmount);
        var diferencia = RecogidaEvaluada.SinDiferencia;
        if (fila.TieneProyeccion)
        {
            var d = DiferenciaDeRecogida.Comparar(proyectado, new DesgloseDeRecogida(Capital: actual));
            if (d.HayDiferencia)
            {
                // Menos que lo proyectado: entraron pagos; más: vencieron cuotas desde la proyección (lo que se causa con los días).
                var causa = actual < proyectado.Total ? PayoffDifferenceCause.Payments : PayoffDifferenceCause.Accrual;
                if (await db.MemberAccountOperations.AsNoTracking().AnyAsync(o => o.MemberAccountId == cuenta.Id && o.RegisteredAt > fila.RecordedAsOf, ct))
                    causa |= PayoffDifferenceCause.Payments;
                diferencia = d with { Causa = causa };
            }
        }
        var aplicar = mantenerLoProyectado && fila.TieneProyeccion ? Math.Min(proyectado.Total, actual) : actual;
        var avisosDeLaCuenta = new List<string>();
        if (diferencia.HayDiferencia)
            avisosDeLaCuenta.Add($"Recoger la cuenta {cuenta.Number} cuesta {ErroresDeRecogida.Pesos(actual)} y la solicitud proyectó "
                + $"{ErroresDeRecogida.Pesos(proyectado.Total)} (diferencia de {ErroresDeRecogida.Pesos(diferencia.Total)}): "
                + $"{RequisitosDelDesembolso.TextoDeCausas(diferencia.Causa)}.");
        if (aplicar < actual)
        {
            var aviso = $"Se mantiene lo proyectado: a la cuenta {cuenta.Number} se le consignan {ErroresDeRecogida.Pesos(aplicar)} de "
                        + $"{ErroresDeRecogida.Pesos(actual)} pendientes.";
            avisosDeLaCuenta.Add(aviso);
            avisos.Add(aviso);
        }
        var sobre = tolerancia is { } tope && fila.TieneProyeccion && Math.Abs(diferencia.Total) > tope;
        if (await RepartoAsync(cuenta, fila.Mode, aplicar, leida.Cuotas.Select(q => q.Cuota.PublicId).ToList(), fecha, ct) is { } reparto)
            hallados.Add(reparto);

        return base_ with
        {
            AAplicar = aplicar,
            Actual = actual,
            Diferencia = diferencia,
            SobreLaTolerancia = sobre,
            Avisos = avisosDeLaCuenta,
            Cuotas = fila.Mode == PayoffMode.Installments ? leida.Cuotas.Select(q => q.Cuota.PublicId).ToList() : [],
        };
    }

    private static RecogidaDeCuentaEvaluada Omitida(RecogidaDeCuentaEvaluada r, DesgloseDeRecogida proyectado, string motivo, PayoffDifferenceCause causa,
        List<string> avisos)
    {
        var aviso = r.Proyectado > 0m ? $"{motivo} No se recoge: el neto a girar sube {ErroresDeRecogida.Pesos(r.Proyectado)}." : $"{motivo} No se recoge.";
        avisos.Add(aviso);
        var diferencia = DiferenciaDeRecogida.Comparar(proyectado, DesgloseDeRecogida.Vacio, productoCerrado: true);
        return r with { Omitida = true, AAplicar = 0m, Actual = 0m, Diferencia = diferencia with { Causa = causa }, Avisos = [aviso] };
    }

    /// <summary>
    /// La validación previa contable de las consignaciones (FR-027a; como <c>ContratoDeCuentasDeCartera</c>): un hecho representativo por combinación de línea,
    /// naturaleza, clase, oficina y embargo con el recaudador de la recogida, con origen pantalla y sin alerta. Su bloqueo (con <c>Contabilidad.FaltaDeRegla =
    /// Bloquear</c>) entra a <paramref name="bloqueos"/> si no está ya; sus avisos a <paramref name="avisos"/>. Devuelve las recogidas con lo que sellará cada
    /// consignación y el resultado (nulo si no hay nada que preguntar).
    /// </summary>
    public async Task<(IReadOnlyList<RecogidaDeCuentaEvaluada> Recogidas, ResultadoDeValidacionPreviaDeCartera? Resultado)> ValidacionPreviaAsync(
        IReadOnlyList<RecogidaDeCuentaEvaluada> recogidas, int titular, string operacion, List<Error> bloqueos, List<string> avisos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recogidas);
        var aPreguntar = recogidas.Where(r => !r.Omitida).ToList();
        if (aPreguntar.Count == 0) return (recogidas, null);
        var oficinas = aPreguntar.Select(r => r.BranchId).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        var titularPublicId = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == titular).Select(p => p.PublicId).FirstOrDefaultAsync(ct);
        var decisiones = new Dictionary<int, DecisionDeValidacionPrevia>();
        ResultadoDeValidacionPreviaDeCartera? peor = null;
        foreach (var grupo in aPreguntar.GroupBy(r => (r.CreditLineId, r.Naturaleza, r.Clase, r.BranchId, r.Embargada)))
        {
            var k = grupo.Key;
            var hechos = ContratoDeCuentasDeCartera.Representativo(Recaudador, grupo.First().CodigoDeLinea, k.Naturaleza, k.Clase, sucursales.GetValueOrDefault(k.BranchId),
                reloj.HoyLocal, null, k.Embargada, titularPublicId, grupo.First().Number);
            var decision = await validacionPrevia.DecidirAsync(hechos, OrigenDeValidacionPrevia.Pantalla, operacion, alertar: false, ct);
            if (decision.Bloqueo is { } bloqueo && bloqueos.All(b => b.Code != bloqueo.Code)) bloqueos.Add(bloqueo);
            avisos.AddRange(decision.Avisos.Where(a => !avisos.Contains(a)));
            foreach (var r in grupo) decisiones[r.AccountId] = decision;
            peor = Peor(peor, decision.Resultado);
        }
        return (recogidas.Select(r => decisiones.TryGetValue(r.AccountId, out var d) ? r with { Validacion = d.Outcome, AvisosDeValidacion = d.Avisos } : r).ToList(),
            peor);
    }

    /// <summary>Junta dos resultados: el que no se puede contabilizar o no respondió manda; los hallazgos se suman.</summary>
    private static ResultadoDeValidacionPreviaDeCartera Peor(ResultadoDeValidacionPreviaDeCartera? a, ResultadoDeValidacionPreviaDeCartera b)
    {
        if (a is null) return b;
        var outcome = new[] { a.Outcome, b.Outcome }.OrderBy(o => o switch
        {
            PrevalidationOutcome.NotPostable => 0,
            PrevalidationOutcome.NoResponse => 1,
            PrevalidationOutcome.Postable => 2,
            _ => 3,
        }).First();
        return new ResultadoDeValidacionPreviaDeCartera(outcome, [.. a.Errors, .. b.Errors], [.. a.Warnings, .. b.Warnings]);
    }

    /// <summary>
    /// Las cuentas que el desembolso bloquea (después de los créditos y antes de numerar nada, R13, E-14): las recogidas y, si el titular tiene un embargo
    /// vigente, todas las suyas —la consignación recalcula el embargo y reescribe los bloqueos de todas sus cuentas—. Por Id.
    /// </summary>
    public async Task<IReadOnlyList<int>> CuentasABloquearAsync(IReadOnlyList<RecogidaDeCuentaEvaluada> recogidas, int titular, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recogidas);
        if (recogidas.Count == 0) return [];
        return recogidas.Select(r => r.AccountId)
            .Concat(await Deposits.Holds.RetencionDeEmbargos.CuentasDeLasPersonasEmbargadasAsync(db, [titular], ct))
            .Distinct().Order().ToList();
    }

    /// <summary>
    /// Consigna las recogidas de cuentas del desembolso <paramref name="operacion"/> pagadas por <paramref name="deudor"/> (las omitidas no), en su orden y en la
    /// transacción del desembolso, ya con las cuentas bloqueadas. La primera que no se aplica aborta con su código; lo consignado tiene que ser lo evaluado
    /// (<c>Lending.Payoff.AppliedDiffers</c>, la defensa de H4).
    /// </summary>
    /// <param name="creditoNuevo">El número del crédito nuevo, para el motivo de la operación de la cuenta («Por el desembolso … del crédito …»).</param>
    public async Task<Result<IReadOnlyList<RecogidaDeCuentaAplicada>>> AplicarAsync(LoanOperation operacion, Guid deudor,
        IReadOnlyList<RecogidaDeCuentaEvaluada> recogidas, CancellationToken ct, string? creditoNuevo = null)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        ArgumentNullException.ThrowIfNull(recogidas);
        var aplicadas = new List<RecogidaDeCuentaAplicada>();
        foreach (var r in recogidas.Where(x => !x.Omitida && x.AAplicar > 0m))
        {
            var clave = Clave(operacion.PublicId, r.AccountPublicId);
            var item = new ConsignacionAAplicar(clave, r.AccountPublicId, Modalidad(r.Modo), r.AAplicar, operacion.ValueDate, ClaseDeMedioDeRecaudo.InternalOffset,
                deudor, new ReferenciaDeRecibo(operacion.Number, operacion.ValueDate, null, null, InteresesAlCancelar.DocumentoDelDesembolso),
                r.Modo == PayoffMode.Installments ? r.Cuotas : null);
            var motivo = string.IsNullOrWhiteSpace(creditoNuevo)
                ? $"Por el desembolso {operacion.Number}"
                : $"Por el desembolso {operacion.Number} del crédito {creditoNuevo}";
            var consignada = await consignacion.AplicarAsync(new ConsignacionEnCurso(Recaudador, item, r.Validacion, r.AvisosDeValidacion)
            {
                Interna = true, Motivo = motivo,
            }, ct);
            if (consignada.IsFailure)
                return Result.Failure<IReadOnlyList<RecogidaDeCuentaAplicada>>(ErroresDeRecogida.ConLaCuenta(consignada.Error, r.AccountPublicId, r.Number));
            var comprobante = consignada.Value.Comprobante;
            if (comprobante.Amount != r.AAplicar)
                return Result.Failure<IReadOnlyList<RecogidaDeCuentaAplicada>>(ErroresDeRecogida.ConsignadoDistinto(r.AccountPublicId, r.Number, r.AAplicar,
                    comprobante.Amount));
            aplicadas.Add(new RecogidaDeCuentaAplicada(r, clave, comprobante));
        }
        return Result.Success<IReadOnlyList<RecogidaDeCuentaAplicada>>(aplicadas);
    }

    // ------------------------------------------------------------------------------------------------------- vivas y anular --

    /// <summary>
    /// Las consignaciones de recogida <b>vivas</b> (sin reversar) de los desembolsos <paramref name="desembolsos"/>: por cada operación <c>Deposit</c> de
    /// <c>CAR</c> con la clave <c>CAR:{desembolso:N}:…</c>, la cuenta y lo consignado, en el orden en que se aplicaron. Las leen el giro nuevo (que vuelve a
    /// descontarlas, H6) y la anulación del crédito (que las reversa). Sólo lee.
    /// </summary>
    public static async Task<IReadOnlyList<ConsignacionDeRecogida>> VivasAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> desembolsos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(desembolsos);
        var resultado = new List<ConsignacionDeRecogida>();
        foreach (var desembolso in desembolsos.Distinct())
        {
            var prefijo = $"{ClavesDeRecaudador.Cartera}:{desembolso:N}:";
            var deEste = await (from o in db.MemberAccountOperations.AsNoTracking()
                                join a in db.MemberAccounts.AsNoTracking().IgnoreQueryFilters() on o.MemberAccountId equals a.Id
                                where o.CollectorCode == ClavesDeRecaudador.Cartera && o.Kind == MemberAccountOperationKind.Deposit
                                      && o.ReversedByOperationId == null && o.IdempotencyKey != null && o.IdempotencyKey.StartsWith(prefijo)
                                select new { o.Id, o.PublicId, o.MemberAccountId, Cuenta = a.PublicId, a.Number, o.IdempotencyKey, o.Amount, a.Nature })
                .ToListAsync(ct);
            resultado.AddRange(deEste.Where(o => RecogidaEnElDesembolso.DesembolsoDeLaClave(o.IdempotencyKey) == desembolso)
                .Select(o => new ConsignacionDeRecogida(o.Id, o.PublicId, o.MemberAccountId, o.Cuenta, o.Number, o.IdempotencyKey!, o.Amount, o.Nature)));
        }
        return resultado.OrderBy(o => o.OperacionId).ToList();
    }

    /// <summary>
    /// Reversa las consignaciones <paramref name="vivas"/> (la anulación del crédito nuevo antes del giro, T079) por <see cref="ReversionDeCuentas"/>: cada una
    /// deja su reverso con la clave <c>{clave}:reverso</c>, los espejos de sus filas y <c>OperacionDeCuentaReversada</c>. Las cuentas ya están bloqueadas. El
    /// primero que no se reversa aborta con su código. Devuelve la clave y el reverso de cada una.
    /// </summary>
    public async Task<Result<IReadOnlyList<(string Clave, Guid Reverso)>>> ReversarAsync(IReadOnlyList<ConsignacionDeRecogida> vivas, string motivo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(vivas);
        var hechos = new List<(string, Guid)>();
        foreach (var viva in vivas)
        {
            var operacion = await db.MemberAccountOperations.FirstAsync(o => o.Id == viva.OperacionId, ct);
            var reverso = await reversion.ReversarAsync(operacion, motivo, IniciadoPorLaAnulacion, ct);
            if (reverso.IsFailure)
                return Result.Failure<IReadOnlyList<(string, Guid)>>(ErroresDeRecogida.ConLaCuenta(reverso.Error, viva.CuentaPublicId, viva.NumeroDeCuenta));
            hechos.Add((viva.Clave, reverso.Value.Comprobante.OperationPublicId));
        }
        return Result.Success<IReadOnlyList<(string, Guid)>>(hechos);
    }
}
