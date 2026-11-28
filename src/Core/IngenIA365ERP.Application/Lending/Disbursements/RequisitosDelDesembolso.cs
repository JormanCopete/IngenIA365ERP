using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Payoffs;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payoffs;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// Un crédito propio que el desembolso recoge: lo que se le aplica (<see cref="AAplicar"/>, en el modo <see cref="Modo"/> con que se aplica) y lo que
/// costaba cancelarlo. 2026-11-27 (feature 028, T033; FR-012–FR-015): con lo que proyectó la solicitud, lo de ahora, la diferencia por concepto con su causa,
/// si se omite porque ya se canceló por otro camino y si pasa de la tolerancia. (nuevo)
/// </summary>
public sealed record RecogidaEvaluada(int LoanId, Guid LoanPublicId, string Number, string Modo, decimal AAplicar, LiquidacionParaRecogida Liquidacion)
{
    /// <summary>En mora o con pérdida de capacidad: recogerlo es una reestructuración (hasta K3, rechazada).</summary>
    public bool RequiereReestructuracion => Liquidacion.DiasDeMora > 0 || Liquidacion.PerdioCapacidad;

    /// <summary>Se aplica como cancelación (<c>FullPayoff</c>); si no, como abono (lo exigible y el resto a capital, D7).</summary>
    public bool Total => Modo != RequisitosDelDesembolso.ModoParcial;

    /// <summary>2026-11-27 (feature 028): la recogida proyectada de la que viene (<c>LND_CreditApplicationPayoffs.Id</c>). (nuevo)</summary>
    public int? ProyectadaId { get; init; }

    /// <summary>2026-11-27 (feature 028): cómo la pidió la solicitud (total o parcial). (nuevo)</summary>
    public PayoffMode ModoPedido { get; init; } = PayoffMode.Total;

    /// <summary>2026-11-27 (feature 028): el valor pedido de un parcial. (nuevo)</summary>
    public decimal? ValorPedido { get; init; }

    /// <summary>2026-11-27 (feature 028): si la solicitud traía proyección (las filas copiadas de <c>PayoffRequest</c> no: no hay con qué comparar). (nuevo)</summary>
    public bool ConProyeccion { get; init; }

    /// <summary>2026-11-27 (feature 028): lo que proyectó la solicitud (cero sin proyección). (nuevo)</summary>
    public decimal Proyectado { get; init; }

    /// <summary>2026-11-27 (feature 028): lo de ahora, a la fecha valor: todo en un total, el valor pedido en un parcial (topado); cero si se omite. (nuevo)</summary>
    public decimal Actual { get; init; }

    /// <summary>2026-11-27 (feature 028): la diferencia por concepto con su causa (ninguna sin proyección). (nuevo)</summary>
    public ResultadoDeDiferencia Diferencia { get; init; } = SinDiferencia;

    /// <summary>2026-11-27 (feature 028, FR-012): el crédito ya se canceló por otro camino (pagado, anulado, novado o sin saldo): no se recoge. (nuevo)</summary>
    public bool Omitida { get; init; }

    /// <summary>2026-11-27 (feature 028): el modo de intereses con que se liquidó (<c>Corrido</c> o <c>CuotaCompleta</c>). (nuevo)</summary>
    public string? ModoDeIntereses { get; init; }

    /// <summary>2026-11-27 (feature 028, D7): la elección del deudor con que un abono baja el capital (la de la solicitud; sin ella, reducir plazo). (nuevo)</summary>
    public EleccionDeAbono? Eleccion { get; init; }

    /// <summary>2026-11-27 (feature 028, FR-015): la diferencia pasa de <c>Recogida.ToleranciaDeDiferencia</c>. (nuevo)</summary>
    public bool SobreLaTolerancia { get; init; }

    /// <summary>2026-11-27 (feature 028): lo que la vista previa dice de este producto. (nuevo)</summary>
    public IReadOnlyList<string> Avisos { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, FR-015): confirmar exige reconocerla: una diferencia u omisión. (nuevo)</summary>
    public bool PorReconocer => Omitida || Diferencia.HayDiferencia;

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T066; FR-020): el tratamiento de la cuota causada con que se aplica: <c>Exclude</c> sólo en una cancelación que deja la
    /// cuota a su canal (<c>PayoffExceptCurrentInstallment</c>). (nuevo)
    /// </summary>
    public InstallmentTreatment Tratamiento { get; init; } = InstallmentTreatment.Include;

    /// <summary>2026-11-27 (feature 028, E2, T066): la cuota causada que se deja a su canal; nula si se recoge todo. (nuevo)</summary>
    public int? CuotaDejada { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T066): lo que la cuota dejada debe al vencer (lo que su canal paga para cerrarla al peso). (nuevo)</summary>
    public decimal? ValorDejado { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T066): la recogida con el tratamiento de la cuota que rige (valor, desglose, cuota y explicaciones). (nuevo)</summary>
    public Payoffs.RecogidaSegunCuota? Segun { get; init; }

    /// <summary>Sin diferencia: lo de una fila sin proyección o igual a lo de ahora.</summary>
    public static ResultadoDeDiferencia SinDiferencia { get; } = new(0m, DesgloseDeRecogida.Vacio, PayoffDifferenceCause.None);
}

/// <summary>2026-11-27 (feature 028, T035, E3): las recogidas vueltas a evaluar bajo el cerrojo: los créditos y las cuentas. (nuevo)</summary>
public sealed record RecogidasReevaluadas(IReadOnlyList<RecogidaEvaluada> Creditos, IReadOnlyList<RecogidaDeCuentaEvaluada> Cuentas)
{
    /// <summary>2026-11-27 (feature 028, E4, T087): las facturas a crédito de Comercial, vueltas a evaluar bajo el cerrojo. (nuevo)</summary>
    public IReadOnlyList<RecogidaDeFacturaEvaluada> Facturas { get; init; } = [];
}

/// <summary>El beneficiario del giro resuelto: la persona del maestro con su copia y la cuenta, tal como se sellará en la orden (I-1). (nuevo)</summary>
public sealed record BeneficiarioResuelto(
    int PersonId,
    Guid PersonPublicId,
    string TaxIdType,
    string TaxId,
    string Name,
    int? BankId,
    string? BankTransferCode,
    byte? AccountType,
    string? AccountNumber)
{
    public PayeeV1 ComoPayee() => new()
    {
        PersonPublicId = PersonPublicId,
        TaxIdType = TaxIdType,
        TaxId = TaxId,
        Name = Name,
        BankCode = BankTransferCode,
        BankAccountNumber = AccountNumber,
        BankAccountType = BeneficiaryInput.Texto(AccountType),
    };
}

/// <summary>
/// Lo que evaluó <see cref="RequisitosDelDesembolso"/> (sin seguimiento: el comando relee lo que escribe dentro de su transacción). Los
/// <see cref="Bloqueos"/> van en el orden en que el comando los responde; la vista previa los muestra todos. (nuevo)
/// </summary>
public sealed record RequisitosEvaluados
{
    public required CreditApplication Solicitud { get; init; }
    public required CreditLine Linea { get; init; }
    public required IReadOnlyList<CreditApplicationParty> Partes { get; init; }
    public required IReadOnlyDictionary<int, Person> Personas { get; init; }
    public required SimulacionCalculada Simulacion { get; init; }
    public int? CreditoId { get; init; }
    public required int Tramo { get; init; }
    public required decimal Bruto { get; init; }
    public required decimal Aprobado { get; init; }
    public required decimal YaDesembolsado { get; init; }
    public required int CuotasAprobadas { get; init; }
    public required DateOnly FechaValor { get; init; }
    public required DisbursementForm Forma { get; init; }
    public required bool MoraSobreIntereses { get; init; }
    public required IReadOnlyList<GarantiaPendienteDto> Pendientes { get; init; }
    public required IReadOnlyList<RecogidaEvaluada> Recogidas { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, E3, T077; HU6): las cuentas de aportes y ahorros que recoge el desembolso, aparte de los créditos (se consignan por
    /// <see cref="RecogidaDeCuentas"/>, no por el contrato de obligaciones). Vacía sin ellas. (nuevo)
    /// </summary>
    public IReadOnlyList<RecogidaDeCuentaEvaluada> RecogidasDeCuentas { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, E3, T080): la validación previa de las consignaciones de las cuentas recogidas; nula sin ellas. (nuevo)</summary>
    public ResultadoDeValidacionPreviaDeCartera? PrevalidacionDeCuentas { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, E4, T087; HU7): las facturas a crédito de Comercial que recoge el desembolso, aparte de los créditos y las cuentas (se pagan con un
    /// recibo sin caja de Recaudo por <see cref="RecogidaDeFacturas"/>). Vacía sin ellas. (nuevo)
    /// </summary>
    public IReadOnlyList<RecogidaDeFacturaEvaluada> RecogidasDeFacturas { get; init; } = [];

    public BeneficiarioResuelto? Beneficiario { get; init; }

    /// <summary>2026-11-23 (descuento condicionado): la compra del bien con la parte que paga el fondo; nula en los demás créditos.</summary>
    public CompraSubsidiada? Compra { get; init; }
    public Guid? SucursalPublicId { get; init; }
    public required ResultadoDeValidacionPreviaDeCartera Prevalidacion { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, T037; FR-027a): la validación previa de los <c>PagoAplicado</c> con que el contrato aplicará las recogidas (clase
    /// <c>OwnLoanPayoff</c>); nula sin recogidas que preguntar. No se sella en el desembolso: el contrato sella la suya al aplicar. (nuevo)
    /// </summary>
    public ResultadoDeValidacionPreviaDeCartera? PrevalidacionDeRecogidas { get; init; }
    public required bool BloquearSinRegla { get; init; }
    public IntervencionVerificada? Intervencion { get; init; }
    public string? ConflictoDeSegregacion { get; init; }
    public required IReadOnlyList<Error> Bloqueos { get; init; }
    public required IReadOnlyList<string> Avisos { get; init; }
    public required string Huella { get; init; }

    /// <summary>2026-11-27 (feature 028): <c>Recogida.ToleranciaDeDiferencia</c> a la fecha valor; nula = nunca bloquea. (nuevo)</summary>
    public decimal? Tolerancia { get; init; }

    public bool EsElPrimero => CreditoId is null;

    /// <summary>
    /// Lo que se aplica a los productos recogidos (los omitidos no suman). 2026-11-27 (feature 028, E3): también lo que se consigna en cuentas; (E4) y lo que se
    /// aplica a facturas.
    /// </summary>
    public decimal Recogido => Recogidas.Where(r => !r.Omitida).Sum(r => r.AAplicar) + RecogidasDeCuentas.Where(r => !r.Omitida).Sum(r => r.AAplicar)
                               + RecogidasDeFacturas.Where(r => !r.Omitida).Sum(r => r.AAplicar);

    /// <summary>2026-11-27 (feature 028, FR-015): hay una diferencia o un producto omitido: confirmar exige reconocerlo. (nuevo)</summary>
    public bool RequiereReconocimiento => Recogidas.Any(r => r.PorReconocer) || RecogidasDeCuentas.Any(r => r.PorReconocer)
                                          || RecogidasDeFacturas.Any(r => r.PorReconocer);
    public decimal Neto => Bruto - Recogido;
    public decimal Desembolsado => YaDesembolsado + Bruto;

    /// <summary>Lo que paga el fondo en este giro (cero sin descuento condicionado).</summary>
    public decimal Subsidio => Compra?.Subsidio ?? 0m;
    public bool EsElUltimo => Desembolsado >= Aprobado;
}

/// <summary>
/// La evaluación única de los requisitos del desembolso (feature 013, US3, T0442; FR-042, FR-045, FR-046; data-model §7.2, §7.3), <b>fuera del
/// cerrojo</b> y sin escribir, que comparten la vista previa y el comando:
/// <list type="number">
/// <item>Cartera activada; la solicitud en el alcance, aprobada y vigente (<c>ApprovalValidUntil</c> frente a <c>HoyLocal</c>, C53) o, en un tramo
/// siguiente, desembolsada con su crédito vivo; el tramo cabe en lo aprobado y la línea admite tramos;</item>
/// <item>quien desembolsa tiene persona y no decidió el expediente (<c>Decidir × Desembolsar</c>, T66);</item>
/// <item>la usura del día del desembolso con <see cref="SimuladorDeCredito"/> sobre lo desembolsado (que sella la sobretasa de cargos, T28) y
/// <c>Mora.SobreIntereses</c> dentro del techo nacional;</item>
/// <item>garantías y pagaré perfeccionados según la regla de <c>LND_CreditLineCollateralRules</c> que casa por monto y plazo (la primera por
/// <c>Order</c>): codeudores (<c>Lending.Collateral.CoDebtorsInsufficient</c>), cobertura mínima y el pagaré —en producción un simulado no
/// perfecciona (T73)—;</item>
/// <item>listas sin liberar, bloqueos vigentes y concentración con patrimonio técnico vigente (T59);</item>
/// <item>el monto operativo de <c>Lending.Disbursements.Execute</c> (<c>SEC_PermissionAmountLimits</c>), la sucursal contable de la oficina y la
/// cuenta del beneficiario en transferencia;</item>
/// <item>las recogidas autorizadas por la solicitud, su liquidación y si alguna es reestructuración (FR-046), y el neto positivo. 2026-11-27 (feature 028,
/// T033; FR-012–FR-015): <b>todas</b> las de la solicitud y sólo en el primer tramo; la lista del cuerpo es opcional y, si viene, debe ser la misma
/// (<c>Lending.Payoff.MustMatchApplication</c>); cada una se liquida a la fecha valor con lo de ahora, se compara con lo proyectado (la diferencia por
/// concepto y su causa) y se aplica según <c>Recogida.AlDesembolsar</c>; la ya cancelada se omite; castigada o en insolvencia bloquea; una diferencia
/// sobre <c>Recogida.ToleranciaDeDiferencia</c> bloquea;</item>
/// <item>la validación previa contable (<see cref="ValidacionPreviaContableDeCartera"/>): bloquea sólo con <c>Contabilidad.FaltaDeRegla =
/// Bloquear</c> (A8); sin respuesta sigue (D7). 2026-11-27 (feature 028, T037; FR-027a): también la de los <c>PagoAplicado</c> de las recogidas.</item>
/// </list>
/// Lo que impide seguir evaluando (solicitud inexistente o no aprobada, la usura del día sin publicar) es una falla; lo demás va a
/// <see cref="RequisitosEvaluados.Bloqueos"/>. (nuevo)
/// </summary>
public sealed class RequisitosDelDesembolso(
    IApplicationDbContext db,
    SimuladorDeCredito simulador,
    ValidacionDeConcentracion concentracion,
    IBloqueosDelDeudor bloqueos,
    RegistroDeIntervenciones intervenciones,
    GuardiaDeCanalesExternos guardia,
    ReglasDeParametrosDeCartera parametros,
    ILectorDeParametros lector,
    ValidacionPreviaContableDeCartera validacionPrevia,
    LiquidacionDeRecogida liquidacionDeRecogida,
    EmisionDeCartera emision,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj,
    Housing.LectorDeVivienda? vivienda = null,
    RecogidaDeCuentas? cuentas = null,
    RecogidaDeFacturas? facturas = null)
{
    /// <summary>El permiso que desembolsa (y cuyo monto operativo se mira).</summary>
    public const string PermisoDeDesembolsar = "Lending.Disbursements.Execute";

    public const string ModoTotal = "Full";
    public const string ModoParcial = "Partial";

    /// <summary>El valor de <c>Contabilidad.FaltaDeRegla</c> que bloquea en las pantallas de Cartera (A8).</summary>
    public const string Bloquear = "Bloquear";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<RequisitosEvaluados>> EvaluarAsync(DisbursementInput e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return Falla(Collections.ErroresDeMensajesAlDeudor.CarteraSinActivar());

        var s = await db.CreditApplications.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == e.ApplicationPublicId && !a.IsDeleted, ct);
        if (s is null) return Falla(ErroresDeOriginacion.SolicitudInexistente());
        var dentro = await alcance.AsegurarAsync(s.BranchId, s.CreditLineId, ErroresDeOriginacion.SolicitudInexistente(), auditoria,
            nameof(CreditApplication), s.PublicId, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);
        // 2026-11-27 (feature 028, T032): la lista del cuerpo es opcional; las dos listas a la vez son un cuerpo que no se puede leer (400).
        var pedidas = RecogidasPedidas.Unificar(e.Payoffs, e.OwnLoanPayoffs);
        if (pedidas.IsFailure) return Falla(pedidas.Error);

        var hoy = reloj.HoyLocal;
        var fechaValor = e.ValueDate ?? hoy;
        var bloqueosHallados = new List<Error>();
        var avisos = new List<string>();

        // 1. Aprobación, tramo y lo que queda por desembolsar.
        var aprobado = s.ApprovedAmount ?? 0m;
        int? creditoId = null;
        decimal yaDesembolsado = 0m;
        var tramo = 1;
        var linea = await db.CreditLines.AsNoTracking().FirstAsync(l => l.Id == s.CreditLineId, ct);
        var version = await new VersionVigenteDeLinea(db).LeerAsync(linea.Id, fechaValor, null, ct);
        if (version is null) return Falla(ErroresDeLineasDeCredito.LineaInexistente());
        if (e.LoanPublicId is null)
        {
            if (s.Status != CreditApplicationStatus.Approved) return Falla(ErroresDeDesembolso.NoAprobada(s.Status.ToString()));
            if (s.AprobacionVencida(hoy))
                return Falla(new ErrorConDatos(CreditApplication.CodigoAprobacionVencida,
                    $"La aprobación venció el {ErroresDeOriginacion.Fecha(s.ApprovalValidUntil!.Value)}. Vuelva a enrutar la solicitud para decidirla otra vez.",
                    new { expiredAt = ErroresDeOriginacion.Fecha(s.ApprovalValidUntil!.Value) }));
        }
        else
        {
            var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == e.LoanPublicId && l.ApplicationId == s.Id, ct);
            if (credito is null) return Falla(ErroresDeDesembolso.CreditoInexistente());
            if (credito.Status != LoanStatus.Active) return Falla(ErroresDeDesembolso.NoAprobada(credito.Status.ToString()));
            creditoId = credito.Id;
            var tramos = await db.LoanOperations.AsNoTracking()
                .Where(o => o.LoanId == credito.Id && o.Kind == LoanOperationKind.Disbursement && o.ReversedByOperationId == null)
                .Select(o => new { o.Amount, o.TrancheNumber })
                .ToListAsync(ct);
            yaDesembolsado = tramos.Sum(t => t.Amount);
            tramo = (tramos.Max(t => t.TrancheNumber) ?? 0) + 1;
        }

        var bruto = e.TrancheAmount ?? aprobado - yaDesembolsado;
        if (bruto <= 0m || yaDesembolsado + bruto > aprobado) return Falla(ErroresDeDesembolso.ExcedeLoAprobado(aprobado, yaDesembolsado, bruto));
        if (!version.Version.AllowsTranches && (creditoId is not null || bruto < aprobado)) return Falla(ErroresDeDesembolso.TramoNoAdmitido(linea.Code));

        // 2. Quien desembolsa (persona y segregación).
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(SubmitCreditApplicationCommandHandler.Expediente, s.PublicId),
            AccionesDeSegregacion.Desembolsar, hoy, ct);
        string? conflicto = null;
        if (verificada.IsFailure)
        {
            bloqueosHallados.Add(verificada.Error);
            conflicto = verificada.Error.Code;
        }

        // 3. Usura del día sobre lo desembolsado y mora sobre intereses.
        var partes = await db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == s.Id && !p.IsDeleted).OrderBy(p => p.Order).ToListAsync(ct);
        var ids = partes.Select(p => p.PersonId).Append(s.ApplicantPersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var cuotas = DecisionConditionsInput.DeJson(s.ApprovedTerms)?.Installments ?? s.Installments ?? 1;
        var simulada = await SimularAsync(s, linea, personas[s.ApplicantPersonId], yaDesembolsado + bruto, cuotas, fechaValor, ct);
        if (simulada.IsFailure) return Falla(simulada.Error);
        var simulacion = simulada.Value;
        if (!simulacion.Usura.Pasa)
            bloqueosHallados.Add(ErroresDeLineasDeCredito.UsuraExcedida("amount", fechaValor, simulacion.Usura, simulacion.Combinacion.ModalityCode));

        var mora = await parametros.LeerAsync(ParametrosDeCartera.MoraSobreIntereses, fechaValor, ct: ct);
        if (mora.IsFailure) return Falla(mora.Error);
        var moraSobreIntereses = mora.Value.Valor is true && !string.Equals(version.Version.ModalityCode, "VIVIENDA", StringComparison.OrdinalIgnoreCase);

        // 4. Garantías, codeudores y pagaré.
        var desembolsado = yaDesembolsado + bruto;
        var pendientes = await GarantiasAsync(s, version, desembolsado, partes, fechaValor, bloqueosHallados, ct);
        // K6b (T1055): un crédito de vivienda exige la hipoteca de primer grado y cumple la Ley 546 (plazo, financiable, cuota-ingreso).
        if (await Housing.ValidacionDeVivienda.GarantiasAsync(db, s, simulacion, ct) is { } sinHipoteca) bloqueosHallados.Add(sinHipoteca);
        if (vivienda is not null && await Housing.ValidacionDeVivienda.SolicitudAsync(db, vivienda, s, simulacion, desembolsado, fechaValor, ct) is { IsFailure: true } ley546)
            bloqueosHallados.Add(ley546.Error);

        // 5. Listas, bloqueos y concentración.
        var personasDeLaSolicitud = partes.Select(p => p.PersonId).Append(s.ApplicantPersonId).Distinct().ToList();
        var listas = await db.ScreeningResults.AsNoTracking()
            .Where(r => r.ApplicationId == s.Id && personasDeLaSolicitud.Contains(r.PersonId) && !r.IsDeleted)
            .ToListAsync(ct);
        var coincidencia = listas.GroupBy(r => r.PersonId).Select(g => g.OrderByDescending(r => r.QueriedAt).First()).FirstOrDefault(r => r.Bloquea);
        if (coincidencia is not null)
            bloqueosHallados.Add(new ErrorConDatos(ErroresDeOriginacion.CodigoCoincidenciaEnListas,
                "Hay una coincidencia en listas restrictivas sin liberar en este expediente. El oficial de cumplimiento debe revisarla antes de desembolsar.",
                new { screeningResultPublicId = coincidencia.PublicId }));
        var vigentes = await bloqueos.VigentesAsync(personasDeLaSolicitud, ct);
        if (vigentes.Count > 0)
            bloqueosHallados.Add(new ErrorConDatos(ErroresDeOriginacion.CodigoDeudorBloqueado,
                $"La persona tiene un {ErroresDeDecisionesYCupos.Bloqueo(vigentes[0].Reason)} vigente desde el {ErroresDeOriginacion.Fecha(vigentes[0].StartedOn)}: " +
                "no se desembolsa hasta que se levante.",
                new { blocks = vigentes.Select(b => new { kind = b.Reason.ToString(), since = ErroresDeOriginacion.Fecha(b.StartedOn), blockPublicId = b.PublicId }).ToList() }));
        var conGarantia = await db.Collaterals.AsNoTracking().AnyAsync(c => c.ApplicationId == s.Id && !c.IsDeleted
            && (c.Status == CollateralStatus.Constituted || c.Status == CollateralStatus.Valid), ct);
        // 2026-11-27 (feature 028, R8, T025): la concentración se mide después de recoger —sin el capital de lo que recoge este desembolso (total: todo el
        // capital del crédito; parcial: lo que se le aplica, topado)—, así que las recogidas se evalúan aquí; sus bloqueos se anotan en su paso (7), en el
        // mismo orden de siempre.
        var bloqueosDeRecogidas = new List<Error>();
        var avisosDeRecogidas = new List<string>();
        var (recogidas, deCuentas, deFacturas, tolerancia) = await RecogidasAsync(s, personas[s.ApplicantPersonId].PublicId, pedidas.Value, creditoId is null,
            fechaValor, bloqueosDeRecogidas, avisosDeRecogidas, ct);
        // Un producto omitido (ya cancelado) no libera nada: ya no estaba.
        var recogidasQueLiberan = recogidas.Where(r => !r.Omitida).Select(r => new RecogidaEnLaValidacion(r.LoanId, r.Number, r.Total, r.AAplicar)).ToList();
        var medida = await concentracion.ValidarAsync(s.ApplicantPersonId, bruto, conGarantia, fechaValor, s.Id,
            recogidasQueLiberan.Count > 0 ? recogidasQueLiberan : null, ct);
        if (medida.IsFailure) bloqueosHallados.Add(medida.Error);

        // 6. Monto operativo, sucursal contable y beneficiario.
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is { } usuario && await LimitesDePermisoPorUsuario.DeAsync(db, usuario, PermisoDeDesembolsar, hoy, ct) is { } maximo && bruto > maximo)
            bloqueosHallados.Add(ErroresDeDesembolso.MontoOperativo(maximo, bruto));
        var sucursal = await emision.SucursalContableAsync(s.BranchId, ct);
        if (sucursal.IsFailure) bloqueosHallados.Add(sucursal.Error);
        var beneficiario = await BeneficiarioAsync(e, personas[s.ApplicantPersonId], ct);
        if (beneficiario.IsFailure) bloqueosHallados.Add(beneficiario.Error);

        // 7. Recogidas (evaluadas con la concentración, arriba).
        bloqueosHallados.AddRange(bloqueosDeRecogidas);
        avisos.AddRange(avisosDeRecogidas);
        // 2026-11-27 (feature 028, E3): lo que se consigna en cuentas también sale del giro; (E4) y lo que se aplica a facturas.
        var recogido = recogidas.Where(r => !r.Omitida).Sum(r => r.AAplicar) + deCuentas.Where(r => !r.Omitida).Sum(r => r.AAplicar)
                       + deFacturas.Where(r => !r.Omitida).Sum(r => r.AAplicar);
        if (recogidas.Count + deCuentas.Count + deFacturas.Count > 0 && bruto - recogido <= 0m)
            bloqueosHallados.Add(ErroresDeDesembolso.NetoNoPositivo(bruto, recogido));

        // 7b. 2026-11-23 (descuento condicionado): el giro al proveedor por el valor del bien; el fondo pone la diferencia.
        var compra = await CompraAsync(s, personas, creditoId, aprobado, bruto, e.Form, beneficiario.IsSuccess ? beneficiario.Value : null,
            recogidas.Count + deCuentas.Count + deFacturas.Count, bloqueosHallados, ct);

        // 8. Validación previa contable.
        // Origen pantalla (T0655, §13.2): bloquea sólo con Contabilidad.FaltaDeRegla o .PoliticaSinRespuesta en «Bloquear»; la alerta la levanta
        // el desembolso al confirmar (AlertarAsync), no la vista previa.
        var decision = await validacionPrevia.DecidirAsync(
            [HechoParaValidar(linea, simulacion, personas[s.ApplicantPersonId], sucursal.IsSuccess ? sucursal.Value : Guid.Empty, fechaValor, bruto, tramo, e.Form,
                subsidio: compra?.ComoHecho())],
            OrigenDeValidacionPrevia.Pantalla, s.Number ?? s.PublicId.ToString("N"), alertar: false, ct);
        var prevalidacion = decision.Resultado;
        var bloquearSinRegla = decision.Bloqueo?.Code == ErroresDeValidacionPreviaDeCartera.CodigoNoContabilizable;
        if (decision.Bloqueo is { } bloqueoContable) bloqueosHallados.Add(bloqueoContable);
        avisos.AddRange(decision.Avisos);
        // 2026-11-27 (feature 028, T037; FR-027a): los PagoAplicado con que el contrato aplicará las recogidas (clase OwnLoanPayoff), con la misma pregunta
        // que hace el contrato al aplicar; lo que bloquearía allá se ve aquí, antes de confirmar. No se sella en el desembolso.
        var prevalidacionDeRecogidas = await PrevalidacionDeRecogidasAsync(s, recogidas, bloqueosHallados, avisos, ct);
        // 2026-11-27 (feature 028, E3, T080; FR-027a): las consignaciones de las cuentas recogidas, con la misma pregunta que haría el contrato de cuentas
        // (AporteRecibido o ConsignacionEnCuentaAplicada con la clase OwnLoanPayoff); su resultado se sella en cada consignación.
        ResultadoDeValidacionPreviaDeCartera? prevalidacionDeCuentas = null;
        if (deCuentas.Count > 0 && cuentas is not null)
            (deCuentas, prevalidacionDeCuentas) = await cuentas.ValidacionPreviaAsync(deCuentas, s.ApplicantPersonId, s.Number ?? s.PublicId.ToString("N"),
                bloqueosHallados, avisos, ct);

        var huella = Huella(s, creditoId, tramo, bruto, fechaValor, e.Form, beneficiario.IsSuccess ? beneficiario.Value : null, recogidas, simulacion,
            compra?.Subsidio ?? 0m, deCuentas, deFacturas);
        return Result.Success(new RequisitosEvaluados
        {
            Solicitud = s,
            Linea = linea,
            Partes = partes,
            Personas = personas,
            Simulacion = simulacion,
            CreditoId = creditoId,
            Tramo = tramo,
            Bruto = bruto,
            Aprobado = aprobado,
            YaDesembolsado = yaDesembolsado,
            CuotasAprobadas = cuotas,
            FechaValor = fechaValor,
            Forma = e.Form,
            MoraSobreIntereses = moraSobreIntereses,
            Pendientes = pendientes,
            Recogidas = recogidas,
            RecogidasDeCuentas = deCuentas,
            PrevalidacionDeCuentas = prevalidacionDeCuentas,
            RecogidasDeFacturas = deFacturas,
            Beneficiario = beneficiario.IsSuccess ? beneficiario.Value : null,
            Compra = compra,
            SucursalPublicId = sucursal.IsSuccess ? sucursal.Value : null,
            Prevalidacion = prevalidacion,
            PrevalidacionDeRecogidas = prevalidacionDeRecogidas,
            Tolerancia = tolerancia,
            BloquearSinRegla = bloquearSinRegla,
            Intervencion = verificada.IsSuccess ? verificada.Value : null,
            ConflictoDeSegregacion = conflicto,
            Bloqueos = bloqueosHallados,
            Avisos = avisos,
            Huella = huella,
        });
    }

    /// <summary>La alerta de un desembolso confirmado con la validación previa en <c>NotPostable</c> (T0655, §13.2).</summary>
    public Task AlertarNoContabilizableAsync(RequisitosEvaluados r, string operacion, CancellationToken ct) =>
        validacionPrevia.AlertarAsync(r.Prevalidacion, operacion, ct);

    /// <summary>La simulación de la solicitud sobre lo desembolsado, con las cuotas aprobadas y la fecha valor del tramo (D12).</summary>
    private async Task<Result<SimulacionCalculada>> SimularAsync(CreditApplication s, CreditLine linea, Person solicitante, decimal monto, int cuotas,
        DateOnly fecha, CancellationToken ct)
    {
        var perfil = await PersonaDeOriginacion.PerfilAsync(db, solicitante, fecha, ct);
        var gracia = s.GraceKind is { } g ? new ScheduleGraceInput(g, s.GraceInstallments ?? 0) : null;
        var extraordinarias = s.ExtraordinaryInstallments is { } x ? JsonSerializer.Deserialize<List<ScheduleExtraordinaryInput>>(x, Json) : null;
        var seguros = s.SelectedInsurances is { } y ? JsonSerializer.Deserialize<List<string>>(y) : null;
        var primerVencimiento = s.FirstDueDate is { } v && v > fecha ? v : (DateOnly?)null;
        var entrada = new ScheduleSimulationInput(linea.PublicId, monto, cuotas, s.Periodicity, fecha, primerVencimiento, s.AmortizationSystem,
            extraordinarias, gracia, seguros, solicitante.PublicId);
        return await simulador.SimularAsync(linea, entrada, perfil, fecha, ct);
    }

    /// <summary>
    /// La regla de garantías que casa por monto (lo desembolsado) y plazo aprobado (la primera por <c>Order</c>, FR-026): codeudores, cobertura
    /// mínima con las garantías admisibles perfeccionadas y el pagaré. Devuelve lo pendiente; los errores van a <paramref name="hallados"/>.
    /// </summary>
    private async Task<IReadOnlyList<GarantiaPendienteDto>> GarantiasAsync(CreditApplication s, VersionDeLineaLeida version, decimal monto,
        IReadOnlyList<CreditApplicationParty> partes, DateOnly fecha, List<Error> hallados, CancellationToken ct)
    {
        var plazo = s.TermMonths ?? 0;
        var regla = version.Garantias.OrderBy(g => g.Order).FirstOrDefault(g =>
            (g.MinAmount is not { } mn || monto >= mn) && (g.MaxAmount is not { } mx || monto <= mx)
            && (g.MinTermMonths is not { } pmn || plazo >= pmn) && (g.MaxTermMonths is not { } pmx || plazo <= pmx));
        var pendientes = new List<GarantiaPendienteDto>();
        if (regla is null) return pendientes;

        var codeudores = partes.Count(p => p.Role == CreditPartyRole.CoDebtor);
        if (codeudores < regla.RequiredCoDebtors)
            hallados.Add(new ErrorConDatos(ErroresDeOriginacion.CodigoCodeudoresInsuficientes,
                $"La línea exige {regla.RequiredCoDebtors} codeudores para este monto y plazo; la solicitud tiene {codeudores}.",
                new { required = regla.RequiredCoDebtors, present = codeudores, ruleOrder = regla.Order }));

        var admitidas = ListaDeEnums.Leer<CollateralKind>(regla.AdmittedKinds);
        var garantias = await db.Collaterals.AsNoTracking().Where(c => c.ApplicationId == s.Id && !c.IsDeleted).ToListAsync(ct);
        var reales = admitidas.Where(k => k is CollateralKind.Pledge or CollateralKind.Mortgage or CollateralKind.GuaranteeFund or CollateralKind.Contributions).ToHashSet();
        if (regla.MinimumCoverage is { } exigida && reales.Count > 0)
        {
            var perfeccionadas = garantias.Where(c => reales.Contains(c.Kind) && c.PerfeccionadaA(fecha)).ToList();
            var cobertura = monto == 0m ? 0m : perfeccionadas.Sum(c => c.AdmissibleValue) / monto;
            if (perfeccionadas.Count == 0)
            {
                var sinPerfeccionar = garantias.Where(c => reales.Contains(c.Kind)).ToList();
                pendientes.AddRange(sinPerfeccionar.Count > 0
                    ? sinPerfeccionar.Select(c => new GarantiaPendienteDto(c.Kind.ToString(), $"la garantía {c.Description} está en «{c.Status}»"))
                    : [new GarantiaPendienteDto(string.Join("|", reales), "falta registrar la garantía que exige la línea")]);
            }
            else if (cobertura < exigida)
                hallados.Add(ErroresDeDesembolso.CoberturaInsuficiente(exigida, decimal.Round(cobertura, 6), regla.Order));
        }

        if (regla.RequiredPromissoryNoteKind is not null || admitidas.Contains(CollateralKind.PromissoryNote))
        {
            var pagares = await db.PromissoryNotes.AsNoTracking()
                .Where(p => p.ApplicationId == s.Id && !p.IsDeleted && p.Status != PromissoryNoteStatus.Voided)
                .ToListAsync(ct);
            var deLaClase = pagares.Where(p => regla.RequiredPromissoryNoteKind is not { } k || p.Kind == k).ToList();
            var perfeccionado = deLaClase.FirstOrDefault(p => p.Perfeccionado);
            if (perfeccionado is null)
                pendientes.Add(new GarantiaPendienteDto(nameof(CollateralKind.PromissoryNote), deLaClase.Count == 0
                    ? "falta el pagaré"
                    : $"el pagaré está en «{deLaClase[0].Status}»: falta la firma o la custodia"));
            else if (perfeccionado.Kind == PromissoryNoteKind.Dematerialized
                     && guardia.Evaluar(EfectoLegalDeCanal.PromissoryNotePerfection, perfeccionado.CustodianChannelCode, perfeccionado.IsSimulated).ComoError() is { } simulado)
                hallados.Insert(0, simulado);
        }

        if (pendientes.Count > 0) hallados.Insert(0, ErroresDeDesembolso.GarantiasPendientes(pendientes));
        return pendientes;
    }

    /// <summary>El beneficiario: el deudor si no viene; su cuenta la de la entrada o, si no trae, la registrada en su persona (I10).</summary>
    private async Task<Result<BeneficiarioResuelto>> BeneficiarioAsync(DisbursementInput e, Person deudor, CancellationToken ct)
    {
        var persona = deudor;
        if (e.Beneficiary is { } b && b.PersonPublicId != deudor.PublicId)
        {
            var otra = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == b.PersonPublicId && !p.IsDeleted, ct);
            if (otra is null) return Result.Failure<BeneficiarioResuelto>(new Error(ErroresDeGobierno.CodigoPersonaInexistente, "El beneficiario no existe en el maestro de personas."));
            persona = otra;
        }

        int? bancoId = null;
        string? codigo = null;
        var tipo = BeneficiaryInput.Tipo(e.Beneficiary?.AccountType);
        var cuenta = string.IsNullOrWhiteSpace(e.Beneficiary?.AccountNumber) ? null : e.Beneficiary!.AccountNumber!.Trim();
        if (e.Beneficiary?.BankPublicId is { } banco)
        {
            var b2 = await db.Banks.AsNoTracking().Where(x => x.PublicId == banco && !x.IsDeleted).Select(x => new { x.Id, x.TransferCode }).FirstOrDefaultAsync(ct);
            bancoId = b2?.Id;
            codigo = b2?.TransferCode;
        }
        else if (persona.SupplierBankCode is { Length: > 0 } propio && e.Form == DisbursementForm.Transfer)
        {
            var b2 = await db.Banks.AsNoTracking().Where(x => !x.IsDeleted && (x.TransferCode == propio || x.LegacyCode == propio))
                .Select(x => new { x.Id, x.TransferCode }).FirstOrDefaultAsync(ct);
            bancoId = b2?.Id;
            codigo = b2?.TransferCode;
            tipo ??= BeneficiaryInput.Tipo(persona.SupplierBankAccountType);
            cuenta ??= persona.SupplierBankAccountNumber;
        }

        if (e.Form == DisbursementForm.Transfer && (bancoId is null || tipo is null || string.IsNullOrWhiteSpace(cuenta)))
            return Result.Failure<BeneficiarioResuelto>(ErroresDeDesembolso.SinCuentaDelBeneficiario());
        if (e.Form == DisbursementForm.Check) (bancoId, codigo, tipo, cuenta) = (null, null, null, null);

        return Result.Success(new BeneficiarioResuelto(persona.Id, persona.PublicId, persona.IdType, persona.TaxId, ReferenciasDeGobierno.Nombre(persona),
            bancoId, codigo, tipo, cuenta));
    }

    /// <summary>
    /// Las recogidas del desembolso (FR-046). 2026-11-27 (feature 028, T010): la liquidación sale de <see cref="LiquidacionDeRecogida"/> —el único sitio que
    /// liquida una recogida—, que cobra lo mismo que aplica el contrato con el modo de <c>Recogida.InteresesProporcionales</c>. 2026-11-27 (feature 028, T023):
    /// lo autorizado son las recogidas proyectadas vivas de la solicitud (<c>LND_CreditApplicationPayoffs</c>), no el JSON <c>PayoffRequest</c>.
    /// 2026-11-27 (feature 028, T032–T033; D9): se recogen <b>todas</b> las autorizadas —nadie las elige de nuevo— y sólo en el primer tramo; la lista del
    /// cuerpo es opcional y, si viene, debe ser la de la solicitud (<c>Lending.Payoff.MustMatchApplication</c>). Cada una se evalúa con
    /// <see cref="EvaluarRecogidasAsync"/>. Devuelve también la tolerancia leída (nula si no hay recogidas o el parámetro está vacío).
    /// </summary>
    private async Task<(IReadOnlyList<RecogidaEvaluada> Recogidas, IReadOnlyList<RecogidaDeCuentaEvaluada> Cuentas, IReadOnlyList<RecogidaDeFacturaEvaluada> Facturas,
        decimal? Tolerancia)> RecogidasAsync(CreditApplication s, Guid solicitante, IReadOnlyList<PayoffInput> pedidas, bool primerTramo, DateOnly fecha,
        List<Error> hallados, List<string> avisos, CancellationToken ct)
    {
        var autorizadas = primerTramo ? await AutorizadasAsync(s.Id, ct) : [];
        if (pedidas.Count > 0 && !Coinciden(pedidas, autorizadas))
            hallados.Add(ErroresDeRecogida.DebeCoincidirConLaSolicitud(autorizadas.Select(Descripcion).ToList()));
        if (autorizadas.Count == 0) return ([], [], [], null);
        var contexto = await ContextoAsync(s, fecha, ct);
        var (creditos, deCuentas, deFacturas) = await EvaluarRecogidasAsync(s, solicitante, autorizadas, contexto, fecha, hallados, avisos, ct);
        return (creditos, deCuentas, deFacturas, contexto.Tolerancia);
    }

    /// <summary>
    /// 2026-11-27 (feature 028, T035): vuelve a evaluar las recogidas de <paramref name="r"/> con lo de ahora y las mismas reglas. El comando la llama bajo el
    /// cerrojo, después de la puesta al día: si lo que se aplica cambió desde la vista previa, se pide otra (<c>Lending.Disbursement.PreviewChanged</c>). Falla
    /// con el primer bloqueo que aparezca. 2026-11-27 (feature 028, E3, T077): también las cuentas (sin volver a preguntar la validación previa: se sella la de la
    /// vista previa).
    /// </summary>
    public async Task<Result<RecogidasReevaluadas>> ReevaluarRecogidasAsync(RequisitosEvaluados r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (!r.EsElPrimero || r.Recogidas.Count + r.RecogidasDeCuentas.Count + r.RecogidasDeFacturas.Count == 0)
            return Result.Success(new RecogidasReevaluadas([], []));
        var autorizadas = await AutorizadasAsync(r.Solicitud.Id, ct);
        var contexto = await ContextoAsync(r.Solicitud, r.FechaValor, ct);
        var hallados = new List<Error>();
        var (creditos, deCuentas, deFacturas) = await EvaluarRecogidasAsync(r.Solicitud, r.Personas[r.Solicitud.ApplicantPersonId].PublicId, autorizadas, contexto,
            r.FechaValor, hallados, [], ct);
        return hallados.Count > 0
            ? Result.Failure<RecogidasReevaluadas>(hallados[0])
            : Result.Success(new RecogidasReevaluadas(creditos, deCuentas) { Facturas = deFacturas });
    }

    /// <summary>Una recogida autorizada por la solicitud y su crédito o su cuenta (nulos si no existen).</summary>
    private sealed record RecogidaAutorizada(CreditApplicationPayoff Fila, Loan? Credito, Domain.Entities.Lending.Deposits.Accounts.MemberAccount? Cuenta = null);

    /// <summary>Con qué se evalúan las recogidas a la fecha valor: <c>Recogida.AlDesembolsar</c> de la línea del crédito nuevo, la tolerancia y la elección.</summary>
    private sealed record ContextoDeRecogidas(bool MantenerLoProyectado, decimal? Tolerancia, EleccionDeAbono Eleccion);

    /// <summary>Las recogidas proyectadas vivas de la solicitud, en el orden de la pantalla, con su crédito.</summary>
    private async Task<IReadOnlyList<RecogidaAutorizada>> AutorizadasAsync(int solicitudId, CancellationToken ct)
    {
        var filas = await db.CreditApplicationPayoffs.AsNoTracking().Where(f => f.CreditApplicationId == solicitudId && !f.IsDeleted)
            .OrderBy(f => f.Order).ThenBy(f => f.Id).ToListAsync(ct);
        var ids = filas.Where(f => f.LoanId != null).Select(f => f.LoanId!.Value).Distinct().ToList();
        var creditos = ids.Count == 0
            ? new Dictionary<int, Loan>()
            : await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        // 2026-11-27 (feature 028, E3, T077): las cuentas de las filas de cuentas.
        var idsDeCuenta = filas.Where(f => f.AccountId != null).Select(f => f.AccountId!.Value).Distinct().ToList();
        var deCuentas = idsDeCuenta.Count == 0
            ? new Dictionary<int, Domain.Entities.Lending.Deposits.Accounts.MemberAccount>()
            : await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => idsDeCuenta.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        return filas.Select(f => new RecogidaAutorizada(f, f.LoanId is { } id ? creditos.GetValueOrDefault(id) : null,
            f.AccountId is { } cuenta ? deCuentas.GetValueOrDefault(cuenta) : null)).ToList();
    }

    /// <summary>
    /// <c>Recogida.AlDesembolsar</c> con la línea del crédito nuevo (caída al general), <c>Recogida.ToleranciaDeDiferencia</c> (general; vacía, nula) y la
    /// elección del deudor de la solicitud para el abono a capital de un parcial (D7; sin ella, reducir plazo).
    /// </summary>
    private async Task<ContextoDeRecogidas> ContextoAsync(CreditApplication s, DateOnly fecha, CancellationToken ct)
    {
        var alDesembolsar = await parametros.LeerAsync(ParametrosDeCartera.RecogidaAlDesembolsar, fecha, null, ParameterScopeKind.CreditLine, s.CreditLineId, ct);
        var tolerancia = await parametros.LeerAsync(ParametrosDeCartera.RecogidaToleranciaDeDiferencia, fecha, ct: ct);
        return new ContextoDeRecogidas(
            alDesembolsar.IsSuccess
            && string.Equals(alDesembolsar.Value.Texto?.Trim(), ParametrosDeCartera.AlDesembolsarMantenerLoProyectado, StringComparison.Ordinal),
            tolerancia.IsSuccess ? tolerancia.Value.Valor as decimal? : null,
            s.PrepaymentChoice == PrepaymentChoice.ReduceInstallment ? EleccionDeAbono.ReduceInstallment : EleccionDeAbono.ReduceTerm);
    }

    /// <summary>
    /// Cada recogida autorizada a la fecha valor con lo de ahora (feature 028, T033):
    /// <list type="number">
    /// <item>el crédito ya cancelado por otro camino (pagado, anulado, novado o sin saldo) se <b>omite</b>: no se recoge, la causa es <c>ProductClosed</c> y
    /// el neto sube;</item>
    /// <item>se liquida con <see cref="LiquidacionDeRecogida"/>; en mora es una reestructuración (<c>Lending.Payoff.RequiresRestructuring</c>, como siempre);
    /// castigado, en insolvencia que negocia o no vigente, <c>Lending.Payoff.NotPayable</c>;</item>
    /// <item>lo de ahora es lo que cuesta recogerlo todo o, en un parcial, el valor pedido mientras no lo cubra todo (si lo cubre, es un total);</item>
    /// <item>se compara con lo proyectado (<see cref="DiferenciaDeRecogida"/>) y la causa se completa con lo que pasó en el crédito desde R: pagos, el modo de
    /// intereses que cambió (parámetros) o la cuota causada; una fila sin proyección no tiene con qué compararse;</item>
    /// <item><c>Recogida.AlDesembolsar</c>: «Recalcular» aplica lo de ahora; «MantenerLoProyectado», lo proyectado topado en lo de ahora, y si no alcanza
    /// a cancelar, se abona (lo exigible y el resto a capital) y el crédito queda con saldo;</item>
    /// <item>con <c>Recogida.ToleranciaDeDiferencia</c>, una diferencia absoluta mayor bloquea (no la de un omitido: ésa sólo sube el neto).</item>
    /// </list>
    /// Se aplica como cancelación si lo que se aplica cubre lo que cuesta recogerlo; si no, como abono con la elección del deudor (D7).
    /// </summary>
    private async Task<(IReadOnlyList<RecogidaEvaluada> Creditos, IReadOnlyList<RecogidaDeCuentaEvaluada> Cuentas, IReadOnlyList<RecogidaDeFacturaEvaluada> Facturas)>
        EvaluarRecogidasAsync(CreditApplication s, Guid solicitante, IReadOnlyList<RecogidaAutorizada> autorizadas, ContextoDeRecogidas contexto, DateOnly fecha,
            List<Error> hallados, List<string> avisos, CancellationToken ct)
    {
        var resultado = new List<RecogidaEvaluada>(autorizadas.Count);
        var deCuentas = new List<RecogidaDeCuentaEvaluada>();
        var deFacturas = new List<RecogidaDeFacturaEvaluada>();
        FacturasRecogibles? pendientes = null;
        var enMora = new List<RecogidaEnMoraDto>();
        var sobreLaTolerancia = new List<(string Numero, decimal Diferencia)>();
        var cuentasSobreLaTolerancia = new HashSet<string>(StringComparer.Ordinal);
        var facturasSobreLaTolerancia = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (fila, credito, cuenta) in autorizadas)
        {
            // 2026-11-27 (feature 028, E3, T077; HU6): una cuenta de aportes o ahorros se evalúa con RecogidaDeCuentas (lo pendiente a la fecha valor o el abono,
            // su diferencia, su omisión y las reglas de su línea) y se consigna aparte de los créditos.
            if (fila.ProductKind == PayoffProductKind.Account && cuentas is not null)
            {
                var deLaCuenta = await cuentas.EvaluarAsync(fila, cuenta, s.ApplicantPersonId, fecha, contexto.MantenerLoProyectado, contexto.Tolerancia, hallados,
                    avisos, ct);
                if (deLaCuenta is null) continue;
                if (deLaCuenta.SobreLaTolerancia)
                {
                    sobreLaTolerancia.Add((deLaCuenta.Number, deLaCuenta.Diferencia.Total));
                    cuentasSobreLaTolerancia.Add(deLaCuenta.Number);
                }
                deCuentas.Add(deLaCuenta);
                continue;
            }
            // 2026-11-27 (feature 028, E4, T087; HU7): una factura a crédito de Comercial se evalúa con lo pendiente que da Recaudo (una lectura para todas) y se
            // paga aparte, con un recibo sin caja de Recaudo.
            if (fila.ProductKind == PayoffProductKind.CommercialReceivable && facturas is not null)
            {
                pendientes ??= await facturas.LeerAsync(solicitante, ct);
                var deLaFactura = RecogidaDeFacturas.Evaluar(fila, pendientes, contexto.MantenerLoProyectado, contexto.Tolerancia, hallados, avisos);
                if (deLaFactura.SobreLaTolerancia)
                {
                    sobreLaTolerancia.Add((deLaFactura.Numero, deLaFactura.Diferencia.Total));
                    facturasSobreLaTolerancia.Add(deLaFactura.Numero);
                }
                deFacturas.Add(deLaFactura);
                continue;
            }
            if (fila.ProductKind != PayoffProductKind.Loan)
            {
                hallados.Add(ErroresDeRecogida.ClaseNoAdmitida(fila.ProductKind));
                continue;
            }
            if (credito is null || credito.IsDeleted || credito.DebtorPersonId != s.ApplicantPersonId)
            {
                hallados.Add(ErroresDeDesembolso.RecogidaNoAutorizada(credito?.PublicId ?? Guid.Empty));
                continue;
            }

            var proyectado = fila.TieneProyeccion ? fila.Desglose() : DesgloseDeRecogida.Vacio;
            var pedida = new RecogidaEvaluada(credito.Id, credito.PublicId, credito.Number, ModoTotal, 0m, new LiquidacionParaRecogida(0m, 0, false))
            {
                ProyectadaId = fila.Id, ModoPedido = fila.Mode, ValorPedido = fila.RequestedAmount, ConProyeccion = fila.TieneProyeccion,
                Proyectado = proyectado.Total, ModoDeIntereses = fila.InterestMode,
            };
            if (credito.Status is LoanStatus.PaidOff or LoanStatus.Voided or LoanStatus.Novated)
            {
                resultado.Add(Omitida(pedida, proyectado, ErroresDeRecogida.NoActivo(credito.Number, credito.Status).Mensaje, avisos));
                continue;
            }

            var liquidada = await liquidacionDeRecogida.LiquidarCreditoAsync(credito, fecha, null, ct);
            if (liquidada.IsFailure)
            {
                hallados.Add(liquidada.Error);
                continue;
            }
            var l = liquidada.Value;
            var liquidacion = new LiquidacionParaRecogida(l.ValorARecoger, l.DiasDeMora, false) { Detalle = l };
            if (l.Bloqueos.Count > 0 && l.Bloqueos.All(b => b.Motivo == ErroresDeRecogida.MotivoSinSaldo))
            {
                resultado.Add(Omitida(pedida with { Liquidacion = liquidacion }, proyectado, l.Bloqueos[0].Mensaje, avisos));
                continue;
            }
            foreach (var bloqueo in l.Bloqueos.Where(b => b.Motivo is not (ErroresDeRecogida.MotivoEnMora or ErroresDeRecogida.MotivoSinSaldo)))
                hallados.Add(bloqueo.ComoError());

            // 2026-11-27 (feature 028, E2, T066; FR-020): la cuota causada con Recogida.CuotaCausada de la línea del crédito a la fecha valor y, con «Preguntar»,
            // la elección que guardó la solicitud; sin rechazar (lo autorizado no se vuelve a validar): una «Excluir» guardada con la línea ya en «Incluir» se
            // incluye, y un crédito que ya no admite dejarla se recoge completo. El cambio frente a lo proyectado es la causa CurrentInstallment. Un parcial no
            // la toca.
            var tratamiento = fila.Mode == PayoffMode.Total && l.ResolverTratamiento(fila.InstallmentTreatment) is { IsSuccess: true } resuelto
                ? resuelto.Value
                : InstallmentTreatment.Include;
            var segun = l.Segun(tratamiento);
            var parcial = fila.Mode == PayoffMode.Partial && fila.RequestedAmount is { } pedido && pedido < l.ValorARecoger;
            var desgloseActual = parcial ? new DesgloseDeRecogida(Capital: fila.RequestedAmount!.Value) : segun.Desglose;
            var actual = parcial ? fila.RequestedAmount!.Value : segun.Valor;
            var diferencia = fila.TieneProyeccion
                ? await CausasAsync(DiferenciaDeRecogida.Comparar(proyectado, desgloseActual), fila, credito, l, segun, ct)
                : RecogidaEvaluada.SinDiferencia;
            var aplicar = contexto.MantenerLoProyectado && fila.TieneProyeccion ? Math.Min(proyectado.Total, actual) : actual;
            var cancela = aplicar >= segun.Valor;
            var dejaLaCuota = cancela && !parcial && segun.CuotaDejada is not null;

            var avisosDelProducto = new List<string>();
            if (diferencia.HayDiferencia)
                avisosDelProducto.Add($"Recoger el crédito {credito.Number} cuesta {ErroresDeRecogida.Pesos(actual)} y la solicitud proyectó "
                    + $"{ErroresDeRecogida.Pesos(proyectado.Total)} (diferencia de {ErroresDeRecogida.Pesos(diferencia.Total)}): {TextoDeCausas(diferencia.Causa)}.");
            if (aplicar < actual)
            {
                var aviso = $"Se mantiene lo proyectado: al crédito {credito.Number} se le abonan {ErroresDeRecogida.Pesos(aplicar)} y queda con un saldo de "
                    + $"{ErroresDeRecogida.Pesos(segun.Valor - aplicar)}.";
                avisosDelProducto.Add(aviso);
                avisos.Add(aviso);
            }
            var sobre = contexto.Tolerancia is { } tope && fila.TieneProyeccion && Math.Abs(diferencia.Total) > tope;
            if (sobre) sobreLaTolerancia.Add((credito.Number, diferencia.Total));

            var evaluada = pedida with
            {
                Modo = cancela ? ModoTotal : ModoParcial,
                AAplicar = cancela ? segun.Valor : aplicar,
                Tratamiento = dejaLaCuota ? InstallmentTreatment.Exclude : InstallmentTreatment.Include,
                CuotaDejada = dejaLaCuota ? segun.CuotaDejada : null,
                ValorDejado = dejaLaCuota ? segun.ValorDejado : null,
                Segun = segun,
                Liquidacion = liquidacion,
                Actual = actual,
                Diferencia = diferencia,
                ModoDeIntereses = l.Intereses.Efectivo,
                Eleccion = cancela ? null : contexto.Eleccion,
                SobreLaTolerancia = sobre,
                Avisos = avisosDelProducto,
            };
            if (evaluada.RequiereReestructuracion) enMora.Add(new RecogidaEnMoraDto(credito.PublicId, credito.Number, l.DiasDeMora));
            resultado.Add(evaluada);
        }
        if (enMora.Count > 0) hallados.Add(ErroresDeDesembolso.RequiereReestructuracion(enMora));
        if (sobreLaTolerancia.Count > 0)
            hallados.Add(ErroresDeRecogida.DiferenciaSobreLaTolerancia(contexto.Tolerancia!.Value, sobreLaTolerancia, cuentasSobreLaTolerancia,
                facturasSobreLaTolerancia));
        return (resultado, deCuentas, deFacturas);
    }

    /// <summary>Un producto ya cancelado: no se recoge, la diferencia es todo lo proyectado (el neto sube) y la causa, <c>ProductClosed</c>.</summary>
    private static RecogidaEvaluada Omitida(RecogidaEvaluada r, DesgloseDeRecogida proyectado, string motivo, List<string> avisos)
    {
        var aviso = r.Proyectado > 0m
            ? $"{motivo} No se recoge: el neto a girar sube {ErroresDeRecogida.Pesos(r.Proyectado)}."
            : $"{motivo} No se recoge.";
        avisos.Add(aviso);
        return r with
        {
            Omitida = true, Modo = ModoTotal, AAplicar = 0m, Actual = 0m,
            Diferencia = DiferenciaDeRecogida.Comparar(proyectado, DesgloseDeRecogida.Vacio, productoCerrado: true), Avisos = [aviso],
        };
    }

    /// <summary>Las operaciones que, registradas desde la proyección, explican la diferencia como pagos recibidos.</summary>
    private static readonly LoanOperationKind[] OperacionesDePago =
        [LoanOperationKind.Payment, LoanOperationKind.PaymentReversal, LoanOperationKind.CreditBalanceResolution, LoanOperationKind.ProvisionalCollection];

    /// <summary>
    /// Completa la causa de una diferencia con lo que el comparador no ve en los conceptos (R9): un pago registrado en el crédito después de R
    /// (<c>Payments</c>), el modo de intereses que cambió desde la proyección (<c>Parameters</c>) y la cuota causada distinta (<c>CurrentInstallment</c>).
    /// 2026-11-27 (feature 028, E2, T066): también <c>CurrentInstallment</c> si cambió el tratamiento de la cuota (la solicitud la dejaba y ya no se puede
    /// dejar), comparando con la cuota del tratamiento que rige.
    /// </summary>
    private async Task<ResultadoDeDiferencia> CausasAsync(ResultadoDeDiferencia d, CreditApplicationPayoff fila, Loan credito, LiquidacionDeCreditoRecogido l,
        Payoffs.RecogidaSegunCuota segun, CancellationToken ct)
    {
        if (!d.HayDiferencia) return d;
        var causa = d.Causa;
        var desde = fila.RecordedAsOf;
        if (await db.LoanOperations.AsNoTracking().AnyAsync(o => o.LoanId == credito.Id && o.RegisteredAt > desde && OperacionesDePago.Contains(o.Kind), ct))
            causa |= PayoffDifferenceCause.Payments;
        if (fila.InterestMode is { Length: > 0 } antes && !string.Equals(antes, l.Intereses.Efectivo, StringComparison.Ordinal))
            causa |= PayoffDifferenceCause.Parameters;
        if (fila.CurrentInstallmentNumber is { } proyectada && segun.Cuota?.Numero is { } ahora && proyectada != ahora)
            causa |= PayoffDifferenceCause.CurrentInstallment;
        if (fila.InstallmentTreatment is { } antes2 && fila.Mode == PayoffMode.Total && antes2 != segun.Tratamiento)
            causa |= PayoffDifferenceCause.CurrentInstallment;
        return d with { Causa = causa };
    }

    /// <summary>
    /// La lista del cuerpo es la autorizada (D9): los mismos productos, del mismo modo y, en un parcial o un abono, con el mismo valor. 2026-11-27 (feature
    /// 028, E3): las cuentas, por su <c>PublicId</c>.
    /// </summary>
    private static bool Coinciden(IReadOnlyList<PayoffInput> pedidas, IReadOnlyList<RecogidaAutorizada> autorizadas)
    {
        var deLaSolicitud = autorizadas.Select(a => Llave(a.Fila.ProductKind, a.Fila.ProductKind switch
            {
                PayoffProductKind.Loan => a.Credito?.PublicId.ToString("N"),
                PayoffProductKind.CommercialReceivable => a.Fila.CommercialReceivableReference,
                _ => a.Cuenta?.PublicId.ToString("N") ?? a.Fila.AccountId?.ToString(CultureInfo.InvariantCulture),
            }, a.Fila.Mode, a.Fila.RequestedAmount))
            .Order(StringComparer.Ordinal).ToList();
        var delCuerpo = pedidas.Select(p => Llave(p.Kind, p.Kind == PayoffProductKind.CommercialReceivable ? p.Reference?.Trim() : p.ProductPublicId?.ToString("N"),
                p.Mode, p.Amount))
            .Order(StringComparer.Ordinal).ToList();
        return deLaSolicitud.SequenceEqual(delCuerpo, StringComparer.Ordinal);

        static string Llave(PayoffProductKind clase, string? producto, PayoffMode modo, decimal? valor) =>
            $"{clase}|{producto}|{modo}|"
            + (modo is PayoffMode.Partial or PayoffMode.Deposit && valor is { } v ? decimal.Round(v, 2).ToString("0.00", CultureInfo.InvariantCulture) : string.Empty);
    }

    /// <summary>Cómo nombra el error de la lista una recogida autorizada: «número total», «número parcial $valor», «número por cuotas» o «número abono $valor».</summary>
    private static string Descripcion(RecogidaAutorizada a) => a.Fila.Mode switch
    {
        PayoffMode.Partial when a.Fila.RequestedAmount is { } valor => $"{a.Fila.ProductNumber} parcial {ErroresDeRecogida.Pesos(valor)}",
        PayoffMode.Deposit when a.Fila.RequestedAmount is { } abono => $"{a.Fila.ProductNumber} abono {ErroresDeRecogida.Pesos(abono)}",
        PayoffMode.Installments => $"{a.Fila.ProductNumber} por cuotas",
        _ => $"{a.Fila.ProductNumber} total",
    };

    /// <summary>Las causas de una diferencia por su nombre (<c>Payments</c>, <c>Accrual</c>…), en el orden de las banderas.</summary>
    public static IReadOnlyList<string> Causas(PayoffDifferenceCause causa) =>
        Enum.GetValues<PayoffDifferenceCause>().Where(c => c != PayoffDifferenceCause.None && causa.HasFlag(c)).Select(c => c.ToString()).ToList();

    /// <summary>Las causas de una diferencia en palabras.</summary>
    public static string TextoDeCausas(PayoffDifferenceCause causa)
    {
        var textos = Enum.GetValues<PayoffDifferenceCause>().Where(c => c != PayoffDifferenceCause.None && causa.HasFlag(c)).Select(c => c switch
        {
            PayoffDifferenceCause.Payments => "pagos recibidos desde la proyección",
            PayoffDifferenceCause.Accrual => "la causación de los días que pasaron",
            PayoffDifferenceCause.Arrears => "mora",
            PayoffDifferenceCause.CurrentInstallment => "cambió la cuota causada",
            PayoffDifferenceCause.Parameters => "cambió cómo se cobran los intereses al recoger",
            PayoffDifferenceCause.ProductClosed => "el producto ya se canceló",
            _ => "otros movimientos del crédito",
        }).ToList();
        return textos.Count == 0 ? "sin causa" : string.Join(", ", textos);
    }

    /// <summary>
    /// 2026-11-27 (feature 028, T037; FR-027a): la validación previa de los <c>PagoAplicado</c> con que el contrato aplicará las recogidas no omitidas, con
    /// el recaudador de la recogida (<c>CAR</c>, <c>OwnLoanPayoff</c>) y la misma pregunta del contrato (<see cref="PagosParaLaValidacionPrevia"/>). Su
    /// bloqueo (con <c>Contabilidad.FaltaDeRegla = Bloquear</c>) entra a <paramref name="bloqueos"/> si no está ya el mismo código; sus avisos, a
    /// <paramref name="avisos"/>. Nula si no hay nada que preguntar.
    /// </summary>
    private async Task<ResultadoDeValidacionPreviaDeCartera?> PrevalidacionDeRecogidasAsync(CreditApplication s, IReadOnlyList<RecogidaEvaluada> recogidas,
        List<Error> bloqueos, List<string> avisos, CancellationToken ct)
    {
        var obligaciones = recogidas.Where(r => !r.Omitida).Select(r => r.LoanPublicId).ToList();
        if (obligaciones.Count == 0) return null;
        var hechos = (await PagosParaLaValidacionPrevia.CombinacionesAsync(db, obligaciones, ct))
            .Select(c => PagosParaLaValidacionPrevia.Hecho(RecogidaEnElDesembolso.Recaudador, c.Combinacion)).ToList();
        if (hechos.Count == 0) return null;
        var decision = await validacionPrevia.DecidirAsync(hechos, OrigenDeValidacionPrevia.Pantalla, s.Number ?? s.PublicId.ToString("N"), alertar: false, ct);
        if (decision.Bloqueo is { } bloqueo && bloqueos.All(b => b.Code != bloqueo.Code)) bloqueos.Add(bloqueo);
        avisos.AddRange(decision.Avisos.Where(a => !avisos.Contains(a)));
        return decision.Resultado;
    }

    /// <summary>
    /// El hecho como se emitiría, para la validación previa (FR-127, T56). 2026-11-12: el desembolso en especie de una venta de Comercial lleva
    /// <paramref name="enEspecie"/>, como el hecho real: sin él, la contrapartida salía por «desembolso por girar» con el motivo InKind, que esa
    /// regla no admite, y toda venta a crédito con Cartera quedaba rechazada con <c>Contabilidad.FaltaDeRegla = Bloquear</c>.
    /// </summary>
    public static MensajeContableDto HechoParaValidar(CreditLine linea, SimulacionCalculada sim, Person deudor, Guid sucursal,
        DateOnly fecha, decimal bruto, int tramo, DisbursementForm forma, InKindDisbursementV1? enEspecie = null, SubsidyV1? subsidio = null)
    {
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = Guid.Empty,
            Type = CreditoDesembolsadoV1.Type,
            Kind = IntegrationMessageKind.Business,
            OriginModule = IngenIA365ERP.Application.Common.Integration.ModulosDeOrigen.Cartera,
            OriginEventKey = IngenIA365ERP.Application.Common.Integration.ClavesDeEvento.Confirmacion,
            BranchPublicId = sucursal,
            PersonPublicId = deudor.PublicId,
        };
        var contenido = new CreditoDesembolsadoV1
        {
            DebtorPersonPublicId = deudor.PublicId,
            ValueDate = fecha,
            Dimensions = new LoanFactDimensionsV1
            {
                CreditLineCode = linea.Code,
                ModalityCode = sim.Combinacion.ModalityCode,
                SubsegmentCode = sim.Combinacion.SubsegmentCode,
                RiskCategory = CategoriaContableDeCartera.CategoriaDeCreditoNuevo,
                RiskSubcategory = "None",
                IsEmployee = deudor.IsEmployee,
                BranchPublicId = sucursal,
                ImpairmentModel = "ArrearsAnnex1",
                RiskSegment = sim.Combinacion.ModalityCode,
                ValueUnit = "Pesos",
            },
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = bruto, ReasonCode = forma.ToString() }],
            RulesVersion = sim.Plan.RulesVersion,
            Tranche = new TrancheV1 { Number = tramo },
            DisbursementForm = forma.ToString(),
            GrossAmount = bruto,
            InKind = enEspecie,
            Subsidy = subsidio,
        };
        return new MensajeContableDto(sobre, contenido);
    }

    /// <summary>
    /// La huella de la vista previa (<c>expectedPreviewSha256</c>): lo que, si cambia, cambia lo que se confirma. 2026-11-27 (feature 028, T033): por cada
    /// recogida, además de lo que se aplica, cómo se aplica, la diferencia con su causa y si se omite (sin recogidas, la huella de siempre).
    /// </summary>
    /// <remarks>
    /// 2026-11-27 (feature 028, E3, T077): con cuentas recogidas suma, por cuenta, el modo, lo que se consigna, las cuotas que cubre, la diferencia con su
    /// causa y la omisión (sin cuentas, la huella de siempre).
    /// </remarks>
    private static string Huella(CreditApplication s, int? credito, int tramo, decimal bruto, DateOnly fecha, DisbursementForm forma,
        BeneficiarioResuelto? beneficiario, IReadOnlyList<RecogidaEvaluada> recogidas, SimulacionCalculada sim, decimal subsidio,
        IReadOnlyList<RecogidaDeCuentaEvaluada>? cuentas = null, IReadOnlyList<RecogidaDeFacturaEvaluada>? facturas = null) =>
        new HuellaDeCartera()
            .Identificador("application", s.PublicId)
            .Entero("loan", credito)
            .Entero("tranche", tramo)
            .Decimal("gross", bruto)
            .Fecha("valueDate", fecha)
            .Enumeracion("form", (DisbursementForm?)forma)
            .Identificador("payee", beneficiario?.PersonPublicId)
            .Entero("bank", beneficiario?.BankId)
            .Texto("account", beneficiario?.AccountNumber)
            .Lista("payoffs", recogidas.OrderBy(r => r.LoanId), (h, r) => h.Identificador("loan", r.LoanPublicId).Decimal("amount", r.AAplicar)
                .Texto("mode", r.Modo).Decimal("difference", r.Diferencia.Total).Enumeracion("causes", (PayoffDifferenceCause?)r.Diferencia.Causa)
                .Logico("omitted", r.Omitida)
                // 2026-11-27 (feature 028, E2, T066): la cuota que se deja a su canal (sin ella, la huella de siempre).
                .Si(r.CuotaDejada is not null, x => x.Entero("leftInstallment", r.CuotaDejada).Decimal("leftAmount", r.ValorDejado ?? 0m)))
            .Si(cuentas is { Count: > 0 }, h => h.Lista("accountPayoffs", cuentas!.OrderBy(c => c.AccountId), (x, c) => x
                .Identificador("account", c.AccountPublicId).Enumeracion("mode", (PayoffMode?)c.Modo).Decimal("amount", c.AAplicar)
                .Textos("installments", c.Cuotas.Select(q => q.ToString("N"))).Decimal("difference", c.Diferencia.Total)
                .Enumeracion("causes", (PayoffDifferenceCause?)c.Diferencia.Causa).Logico("omitted", c.Omitida)))
            // 2026-11-27 (feature 028, E4, T087): las facturas, por su número (sin facturas, la huella de siempre).
            .Si(facturas is { Count: > 0 }, h => h.Lista("invoicePayoffs", facturas!.OrderBy(f => f.Numero, StringComparer.Ordinal), (x, f) => x
                .Texto("invoice", f.Numero).Enumeracion("mode", (PayoffMode?)f.Modo).Decimal("amount", f.AAplicar).Decimal("difference", f.Diferencia.Total)
                .Enumeracion("causes", (PayoffDifferenceCause?)f.Diferencia.Causa).Logico("omitted", f.Omitida)))
            .Texto("simulation", sim.Huella)
            .Si(subsidio > 0m, h => h.Decimal("subsidy", subsidio))
            .Calcular();

    /// <summary>
    /// 2026-11-27 (feature 028, T035): la huella de <paramref name="r"/> con otras recogidas (las que se releen bajo el cerrojo). 2026-11-27 (E3): y otras
    /// cuentas. (nuevo)
    /// </summary>
    public static string HuellaCon(RequisitosEvaluados r, IReadOnlyList<RecogidaEvaluada> recogidas, IReadOnlyList<RecogidaDeCuentaEvaluada>? cuentas = null,
        IReadOnlyList<RecogidaDeFacturaEvaluada>? facturas = null)
    {
        ArgumentNullException.ThrowIfNull(r);
        return Huella(r.Solicitud, r.CreditoId, r.Tramo, r.Bruto, r.FechaValor, r.Forma, r.Beneficiario, recogidas, r.Simulacion, r.Subsidio,
            cuentas ?? r.RecogidasDeCuentas, facturas ?? r.RecogidasDeFacturas);
    }

    /// <summary>
    /// 2026-11-23 (descuento condicionado, el caso del SOAT): la compra de la solicitud con la opción de la versión que selló. Se gira al proveedor
    /// (giro a tercero), en un solo giro, sin recoger créditos, y lo aprobado más lo que paga el fondo es el valor del bien: si quien decidió bajó el
    /// monto, la opción ya no cuadra y hay que devolver la solicitud para corregirla. Cada falla va a los bloqueos.
    /// </summary>
    private async Task<CompraSubsidiada?> CompraAsync(CreditApplication s, IReadOnlyDictionary<int, Person> personas, int? creditoId, decimal aprobado,
        decimal bruto, DisbursementForm forma, BeneficiarioResuelto? beneficiario, int recogidas, List<Error> bloqueos, CancellationToken ct)
    {
        if (s.AssetValue is not { } valor || s.DiscountOptionCode is not { } codigo || s.SupplierPersonId is not { } proveedor) return null;
        var version = await db.CreditLineVersions.AsNoTracking().IgnoreQueryFilters().FirstAsync(v => v.Id == s.CreditLineVersionId, ct);
        var opcion = version.LeerOpcionesDeDescuento().FirstOrDefault(o => o.Code == codigo);
        if (opcion is null)
        {
            bloqueos.Add(ErroresDeDesembolso.CompraConDescuento($"la opción {codigo} ya no está en la versión de la línea."));
            return null;
        }
        var subsidio = OpcionesDeDescuento.Subsidio(valor, opcion);
        var proveedorPublicId = personas.TryGetValue(proveedor, out var p) ? p.PublicId
            : await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == proveedor).Select(x => x.PublicId).FirstAsync(ct);
        if (creditoId is not null || bruto != aprobado)
            bloqueos.Add(ErroresDeDesembolso.CompraConDescuento("se desembolsa en un solo giro por todo lo aprobado."));
        if (aprobado + subsidio != valor)
            bloqueos.Add(ErroresDeDesembolso.CompraConDescuento(
                "lo aprobado más lo que paga el fondo no da el valor del bien. Devuelva la solicitud para corregir la opción o el monto."));
        if (forma != DisbursementForm.ThirdPartyPayment || beneficiario is not null && beneficiario.PersonId != proveedor)
            bloqueos.Add(ErroresDeDesembolso.CompraConDescuento("se gira al proveedor del bien, con la forma «giro a tercero»."));
        if (recogidas > 0) bloqueos.Add(ErroresDeDesembolso.CompraConDescuento("no recoge otros créditos."));
        return new CompraSubsidiada(valor, proveedor, proveedorPublicId, opcion.Code, opcion.Name, opcion.Fraction, opcion.FundCode ?? string.Empty, subsidio);
    }

    private static Result<RequisitosEvaluados> Falla(Error e) => Result.Failure<RequisitosEvaluados>(e);
}

/// <summary>
/// 2026-11-23 (descuento condicionado): la compra de un bien a un proveedor con la parte que paga un fondo, como se sella en las condiciones del
/// crédito (<c>LoanTermsVersion.SubsidizedPurchase</c>) y se emite en el desembolso (<see cref="SubsidyV1"/>). (nuevo)
/// </summary>
public sealed record CompraSubsidiada(decimal ValorDelBien, int ProveedorId, Guid ProveedorPublicId, string Opcion, string NombreDeLaOpcion,
    decimal Fraccion, string Fondo, decimal Subsidio)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public SubsidyV1? ComoHecho() => Subsidio <= 0m
        ? null
        : new SubsidyV1 { Amount = Subsidio, OptionCode = Opcion, FundCode = Fondo, SupplierPersonPublicId = ProveedorPublicId };

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public static CompraSubsidiada? Leer(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<CompraSubsidiada>(json, Json);
}
