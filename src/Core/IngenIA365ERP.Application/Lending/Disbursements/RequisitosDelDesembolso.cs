using System.Text.Json;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
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
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>Un crédito propio que el desembolso recoge: lo que se le aplica y lo que costaba cancelarlo. (nuevo)</summary>
public sealed record RecogidaEvaluada(int LoanId, Guid LoanPublicId, string Number, string Modo, decimal AAplicar, LiquidacionParaRecogida Liquidacion)
{
    /// <summary>En mora o con pérdida de capacidad: recogerlo es una reestructuración (hasta K3, rechazada).</summary>
    public bool RequiereReestructuracion => Liquidacion.DiasDeMora > 0 || Liquidacion.PerdioCapacidad;

    public bool Total => Modo != RequisitosDelDesembolso.ModoParcial;
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
    public BeneficiarioResuelto? Beneficiario { get; init; }
    public Guid? SucursalPublicId { get; init; }
    public required ResultadoDeValidacionPreviaDeCartera Prevalidacion { get; init; }
    public required bool BloquearSinRegla { get; init; }
    public IntervencionVerificada? Intervencion { get; init; }
    public string? ConflictoDeSegregacion { get; init; }
    public required IReadOnlyList<Error> Bloqueos { get; init; }
    public required IReadOnlyList<string> Avisos { get; init; }
    public required string Huella { get; init; }

    public bool EsElPrimero => CreditoId is null;
    public decimal Recogido => Recogidas.Sum(r => r.AAplicar);
    public decimal Neto => Bruto - Recogido;
    public decimal Desembolsado => YaDesembolsado + Bruto;
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
/// <item>las recogidas autorizadas por la solicitud, su liquidación y si alguna es reestructuración (FR-046), y el neto positivo;</item>
/// <item>la validación previa contable (<see cref="ValidacionPreviaContableDeCartera"/>): bloquea sólo con <c>Contabilidad.FaltaDeRegla =
/// Bloquear</c> (A8); sin respuesta sigue (D7).</item>
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
    IMotorDeCreditoEnDesembolso motor,
    EmisionDeCartera emision,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj)
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
        var medida = await concentracion.ValidarAsync(s.ApplicantPersonId, bruto, conGarantia, fechaValor, s.Id, ct);
        if (medida.IsFailure) bloqueosHallados.Add(medida.Error);

        // 6. Monto operativo, sucursal contable y beneficiario.
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is { } usuario && await LimitesDePermisoPorUsuario.DeAsync(db, usuario, PermisoDeDesembolsar, hoy, ct) is { } maximo && bruto > maximo)
            bloqueosHallados.Add(ErroresDeDesembolso.MontoOperativo(maximo, bruto));
        var sucursal = await emision.SucursalContableAsync(s.BranchId, ct);
        if (sucursal.IsFailure) bloqueosHallados.Add(sucursal.Error);
        var beneficiario = await BeneficiarioAsync(e, personas[s.ApplicantPersonId], ct);
        if (beneficiario.IsFailure) bloqueosHallados.Add(beneficiario.Error);

        // 7. Recogidas.
        var recogidas = await RecogidasAsync(s, e, fechaValor, bloqueosHallados, ct);
        var recogido = recogidas.Sum(r => r.AAplicar);
        if (recogidas.Count > 0 && bruto - recogido <= 0m) bloqueosHallados.Add(ErroresDeDesembolso.NetoNoPositivo(bruto, recogido));

        // 8. Validación previa contable.
        var prevalidacion = await validacionPrevia.EvaluarAsync(
            [HechoParaValidar(linea, simulacion, personas[s.ApplicantPersonId], sucursal.IsSuccess ? sucursal.Value : Guid.Empty, fechaValor, bruto, tramo, e.Form)], ct);
        var falta = await lector.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.ContabilidadFaltaDeRegla, fechaValor, ct: ct);
        var bloquearSinRegla = falta.IsSuccess && string.Equals(falta.Value.Texto, Bloquear, StringComparison.OrdinalIgnoreCase);
        if (prevalidacion.Outcome == PrevalidationOutcome.NotPostable)
        {
            if (bloquearSinRegla) bloqueosHallados.Add(ErroresDeDesembolso.NoContabilizable(prevalidacion.Errors));
            else avisos.Add("Contabilidad no tiene regla para todo lo que registra este desembolso: queda marcado para que la contadora lo complete.");
        }
        else if (prevalidacion.Outcome == PrevalidationOutcome.NoResponse)
            avisos.Add("Contabilidad no respondió a tiempo: el desembolso sigue y queda marcado para revisión.");
        avisos.AddRange(prevalidacion.Warnings.Select(w => w.Message));

        var huella = Huella(s, creditoId, tramo, bruto, fechaValor, e, beneficiario.IsSuccess ? beneficiario.Value : null, recogidas, simulacion);
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
            Beneficiario = beneficiario.IsSuccess ? beneficiario.Value : null,
            SucursalPublicId = sucursal.IsSuccess ? sucursal.Value : null,
            Prevalidacion = prevalidacion,
            BloquearSinRegla = bloquearSinRegla,
            Intervencion = verificada.IsSuccess ? verificada.Value : null,
            ConflictoDeSegregacion = conflicto,
            Bloqueos = bloqueosHallados,
            Avisos = avisos,
            Huella = huella,
        });
    }

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

    /// <summary>Las recogidas pedidas: sólo las que autoriza la solicitud (FR-046), del deudor, vivas, con su liquidación a la fecha valor.</summary>
    private async Task<IReadOnlyList<RecogidaEvaluada>> RecogidasAsync(CreditApplication s, DisbursementInput e, DateOnly fecha, List<Error> hallados,
        CancellationToken ct)
    {
        var resultado = new List<RecogidaEvaluada>();
        if (e.OwnLoanPayoffs.Count == 0) return resultado;

        var autorizadas = (string.IsNullOrWhiteSpace(s.PayoffRequest)
                ? null
                : JsonSerializer.Deserialize<List<OwnLoanPayoffInput>>(s.PayoffRequest, Json)) ?? [];
        var enMora = new List<RecogidaEnMoraDto>();
        foreach (var pedido in e.OwnLoanPayoffs)
        {
            if (autorizadas.All(a => a.LoanPublicId != pedido.LoanPublicId))
            {
                hallados.Add(ErroresDeDesembolso.RecogidaNoAutorizada(pedido.LoanPublicId));
                continue;
            }
            var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == pedido.LoanPublicId, ct);
            if (credito is null || credito.DebtorPersonId != s.ApplicantPersonId || credito.Status != LoanStatus.Active)
            {
                hallados.Add(ErroresDeDesembolso.RecogidaNoAutorizada(pedido.LoanPublicId));
                continue;
            }
            var liquidacion = await motor.LiquidarAsync(credito.Id, fecha, ct);
            var modo = string.Equals(pedido.Mode, ModoParcial, StringComparison.OrdinalIgnoreCase) ? ModoParcial : ModoTotal;
            var aplicar = modo == ModoParcial ? Math.Min(pedido.Amount ?? 0m, liquidacion.ParaCancelar) : liquidacion.ParaCancelar;
            var r = new RecogidaEvaluada(credito.Id, credito.PublicId, credito.Number, modo, aplicar, liquidacion);
            if (r.RequiereReestructuracion) enMora.Add(new RecogidaEnMoraDto(credito.PublicId, credito.Number, liquidacion.DiasDeMora));
            resultado.Add(r);
        }
        if (enMora.Count > 0) hallados.Add(ErroresDeDesembolso.RequiereReestructuracion(enMora));
        return resultado;
    }

    /// <summary>El hecho como se emitiría, para la validación previa (FR-127, T56).</summary>
    private static MensajeContableDto HechoParaValidar(CreditLine linea, SimulacionCalculada sim, Person deudor, Guid sucursal,
        DateOnly fecha, decimal bruto, int tramo, DisbursementForm forma)
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
        };
        return new MensajeContableDto(sobre, contenido);
    }

    /// <summary>La huella de la vista previa (<c>expectedPreviewSha256</c>): lo que, si cambia, cambia lo que se confirma.</summary>
    private static string Huella(CreditApplication s, int? credito, int tramo, decimal bruto, DateOnly fecha, DisbursementInput e,
        BeneficiarioResuelto? beneficiario, IReadOnlyList<RecogidaEvaluada> recogidas, SimulacionCalculada sim) =>
        new HuellaDeCartera()
            .Identificador("application", s.PublicId)
            .Entero("loan", credito)
            .Entero("tranche", tramo)
            .Decimal("gross", bruto)
            .Fecha("valueDate", fecha)
            .Enumeracion("form", (DisbursementForm?)e.Form)
            .Identificador("payee", beneficiario?.PersonPublicId)
            .Entero("bank", beneficiario?.BankId)
            .Texto("account", beneficiario?.AccountNumber)
            .Lista("payoffs", recogidas.OrderBy(r => r.LoanId), (h, r) => h.Identificador("loan", r.LoanPublicId).Decimal("amount", r.AAplicar))
            .Texto("simulation", sim.Huella)
            .Calcular();

    private static Result<RequisitosEvaluados> Falla(Error e) => Result.Failure<RequisitosEvaluados>(e);
}
