using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 028 (T023–T025, T027, T030; HU2, FR-006–FR-009, FR-011; R8): la solicitud que recoge créditos los guarda en filas
/// (<c>LND_CreditApplicationPayoffs</c>) con su proyección —D, R, desglose y huella— y ya no escribe <c>PayoffRequest</c>; la lista vieja
/// <c>ownLoanPayoffs</c> se sigue aceptando; al guardar y al radicar valida lo que recoge (del solicitante, vivo y elegible, admitido, modo, neto
/// positivo) y al radicar lo vuelve a proyectar; un crédito en otra solicitud viva es un aviso; las cinco validaciones (capacidad de pago, de descuento,
/// endeudamiento consolidado, cupo y concentración) miden la situación después de recoger y lo dicen en <c>releasedByPayoffs</c>; la decisión y el
/// desembolso miden la concentración igual; el desembolso lee lo autorizado de las filas. El escenario es <see cref="OriginacionDePrueba"/> con créditos
/// de libro de Ana. (nuevo)
/// </summary>
public sealed class SolicitudConRecogidasTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = OriginacionDePrueba.Hoy;

    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private static PayoffInput Total(Loan credito) => new(PayoffProductKind.Loan, credito.PublicId, PayoffMode.Total);

    private static PayoffInput Parcial(Loan credito, decimal valor) => new(PayoffProductKind.Loan, credito.PublicId, PayoffMode.Partial, valor);

    private CreditApplicationInput Recogiendo(params PayoffInput[] recogidas) => _o.Solicitud() with { Payoffs = recogidas };

    private async Task<Result<CreditApplicationDto>> CrearAsync(CreditApplicationInput entrada)
    {
        var r = await _o.Crear().Handle(new CreateCreditApplicationCommand(entrada) { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<CreditApplicationDto> LeerAsync(Guid solicitud)
    {
        var r = await _o.Leer().Handle(new GetCreditApplicationQuery(solicitud), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _o.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private Task<List<Domain.Entities.Lending.Payoffs.CreditApplicationPayoff>> FilasAsync(Guid solicitud, bool conBajas = false) =>
        (conBajas ? _o.Db.CreditApplicationPayoffs.IgnoreQueryFilters() : _o.Db.CreditApplicationPayoffs).AsNoTracking()
            .Where(f => (conBajas || !f.IsDeleted) && _o.Db.CreditApplications.Any(a => a.Id == f.CreditApplicationId && a.PublicId == solicitud))
            .OrderBy(f => f.Id).ToListAsync();

    private void Parametro(string clave, string valor, int? linea = null, DateOnly? desde = null)
    {
        _o.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, Value = valor, ValidFrom = desde ?? new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test",
            ScopeKind = linea is null ? ParameterScopeKind.None : ParameterScopeKind.CreditLine, ScopeId = linea ?? 0,
        });
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();
    }

    /// <summary>El plan vigente de un crédito de libro con una próxima cuota de <paramref name="cuota"/>.</summary>
    private void ConCuota(Loan credito, decimal cuota)
    {
        var plan = new RepaymentScheduleVersion
        {
            LoanId = credito.Id, VersionNumber = 1, EffectiveValueDate = credito.OpenedOn, RegisteredAt = _o.G.UtcNow.AddMonths(-10), PrincipalBase = cuota,
            FirstRegeneratedInstallment = 1, InstallmentCount = 1,
        };
        _o.Db.RepaymentScheduleVersions.Add(plan);
        _o.Db.SaveChanges();
        _o.Db.RepaymentInstallments.Add(new RepaymentInstallment
        {
            ScheduleVersionId = plan.Id, InstallmentNumber = 1, PeriodStart = Hoy, DueDate = Hoy.AddMonths(1), ExigibleDate = Hoy.AddMonths(1),
            OpeningPrincipal = cuota, Principal = cuota, Total = cuota,
        });
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();
    }

    private static CreditApplicationCheckDto Validacion(CreditApplicationDto s, ApplicationCheckKind clase) =>
        s.Checks.Where(c => c.Kind == clase).OrderByDescending(c => c.At).First();

    private static JsonElement Liberado(CreditApplicationCheckDto c) => c.Detail!.Value.GetProperty("releasedByPayoffs");

    // ------------------------------------------------------------------------------------------------------- guardar --

    [Fact]
    public async Task Guarda_lo_que_recoge_en_filas_con_su_proyeccion_y_ya_no_escribe_PayoffRequest()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        var d = Hoy.AddDays(3);

        var r = await CrearAsync(Recogiendo(Total(viejo)) with { PayoffProjection = new PayoffProjectionInput(d, _o.G.UtcNow.AddMinutes(-5)) });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var fila = (await FilasAsync(r.Value.PublicId)).Should().ContainSingle().Subject;
        (fila.ProductKind, fila.LoanId, fila.Mode, fila.RequestedAmount, fila.Order).Should().Be((PayoffProductKind.Loan, viejo.Id, PayoffMode.Total, null, 1));
        (fila.ProjectedDate, fila.ProjectedAmount, fila.ProductNumber).Should().Be((d, 1_000_000m, "CR-VIEJO"));
        fila.RecordedAsOf.Should().Be(_o.G.UtcNow.AddMinutes(-5), "R es la de la proyección que mandó la pantalla");
        fila.ProjectionSha256.Should().StartWith("sha256:");
        fila.InstallmentTreatment.Should().Be(InstallmentTreatment.Include, "en E1 la cuota causada siempre se incluye");
        fila.Desglose().Capital.Should().Be(1_000_000m);
        (await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == r.Value.PublicId)).PayoffRequest.Should().BeNull();

        r.Value.Payoffs.Should().ContainSingle().Which.Should().Match<CreditApplicationPayoffDto>(p =>
            p.ProductPublicId == viejo.PublicId && p.Number == "CR-VIEJO" && p.Mode == PayoffMode.Total && p.ProjectedAmount == 1_000_000m && p.Projected
            && p.ProjectedDate == d && p.InOtherApplication == null && p.Breakdown.Principal == 1_000_000m);
        r.Value.PayoffSummary.Should().Be(new CreditApplicationPayoffSummaryDto(1_000_000m, 2_000_000m, d, _o.G.UtcNow.AddMinutes(-5)));
        r.Value.UsesAdvancedTerms.Should().BeFalse("las recogidas ya no impiden corregir la solicitud en la pantalla");
    }

    [Fact]
    public async Task Sin_proyeccion_la_fecha_es_la_esperada_de_desembolso_y_nunca_antes_de_hoy()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");

        var conFecha = await CrearAsync(Recogiendo(Total(viejo)) with { ExpectedDisbursementDate = Hoy.AddDays(5) });
        var pasada = await CrearAsync(Recogiendo(Total(_o.Credito(_o.Ana, 500_000m, "CR-OTRO"))) with
        {
            PayoffProjection = new PayoffProjectionInput(Hoy.AddDays(-2)),
        });

        conFecha.IsSuccess.Should().BeTrue(conFecha.IsFailure ? $"{conFecha.Error.Code}: {conFecha.Error.Message}" : string.Empty);
        (await FilasAsync(conFecha.Value.PublicId)).Single().ProjectedDate.Should().Be(Hoy.AddDays(5));
        (await FilasAsync(pasada.Value.PublicId)).Single().ProjectedDate.Should().Be(Hoy, "una fecha pasada no se proyecta");
    }

    [Fact]
    public async Task La_lista_vieja_ownLoanPayoffs_se_sigue_aceptando_y_las_dos_a_la_vez_es_un_cuerpo_ilegible()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");

        var vieja = await CrearAsync(_o.Solicitud() with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId, "Partial", 400_000m)] });
        var ambas = await CrearAsync(Recogiendo(Total(viejo)) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId)] });

        vieja.IsSuccess.Should().BeTrue(vieja.IsFailure ? $"{vieja.Error.Code}: {vieja.Error.Message}" : string.Empty);
        var fila = (await FilasAsync(vieja.Value.PublicId)).Single();
        (fila.Mode, fila.RequestedAmount, fila.ProjectedAmount).Should().Be((PayoffMode.Partial, 400_000m, 400_000m));
        fila.Desglose().Capital.Should().Be(400_000m, "un parcial de un crédito al día va a capital");
        ambas.Error.Code.Should().Be(RecogidasPedidas.CodigoCuerpoInvalido);
    }

    [Fact]
    public async Task Un_parcial_que_cubre_todo_se_proyecta_como_total()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");

        var r = await CrearAsync(Recogiendo(Parcial(viejo, 1_500_000m)));

        var fila = (await FilasAsync(r.Value.PublicId)).Single();
        (fila.Mode, fila.RequestedAmount, fila.ProjectedAmount).Should().Be((PayoffMode.Partial, 1_500_000m, 1_000_000m));
    }

    [Fact]
    public async Task Corregir_reescribe_las_filas_y_deja_de_baja_las_anteriores()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        var otro = _o.Credito(_o.Ana, 500_000m, "CR-OTRO");
        var s = (await CrearAsync(Recogiendo(Total(viejo)))).Value;

        var r = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, Recogiendo(Total(otro), Parcial(viejo, 300_000m)))
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _o.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var vivas = await FilasAsync(s.PublicId);
        vivas.Select(f => (f.LoanId, f.Mode, f.Order)).Should().Equal((otro.Id, PayoffMode.Total, 1), (viejo.Id, PayoffMode.Partial, 2));
        (await FilasAsync(s.PublicId, conBajas: true)).Count(f => f.IsDeleted).Should().Be(1);

        var sin = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, _o.Solicitud()) { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();
        sin.IsSuccess.Should().BeTrue();
        (await FilasAsync(s.PublicId)).Should().BeEmpty("sin recogidas en el cuerpo, la solicitud deja de recoger");
        sin.Value.PayoffSummary.Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------------- reglas al guardar --

    [Fact]
    public async Task Un_credito_que_no_es_del_solicitante_como_deudor_no_se_recoge()
    {
        var deBeto = _o.Credito(_o.Beto, 1_000_000m, "CR-BETO");
        var comoCodeudora = _o.Credito(_o.Beto, 800_000m, "CR-CODEUDA", _o.Ana);

        (await CrearAsync(Recogiendo(Total(deBeto)))).Error.Code.Should().Be(ErroresDeRecogida.CodigoNoEsDelSolicitante);
        (await CrearAsync(Recogiendo(Total(comoCodeudora)))).Error.Code.Should().Be(ErroresDeRecogida.CodigoNoEsDelSolicitante);
        (await CrearAsync(Recogiendo(new PayoffInput(PayoffProductKind.Loan, Guid.NewGuid(), PayoffMode.Total))))
            .Error.Code.Should().Be(ErroresDeRecogida.CodigoNoEsDelSolicitante);
    }

    [Fact]
    public async Task En_mora_requiere_reestructuracion_y_castigado_o_cancelado_no_es_pagable()
    {
        var mora = _o.Credito(_o.Ana, 500_000m, "CR-MORA");
        _o.Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = mora.Id, PositionDate = Hoy.AddDays(-1), Version = 1, RegisteredAt = _o.G.UtcNow.AddDays(-1), Status = LoanStatus.Active,
            BranchId = mora.BranchId, CreditLineId = mora.CreditLineId, ModalityCode = "CONSUMO", Principal = 500_000m, PayoffAmount = 520_000m,
            DaysPastDue = 12,
        });
        var castigado = _o.Credito(_o.Ana, 300_000m, "CR-CASTIGO");
        castigado.Status = LoanStatus.WrittenOff;
        var cancelado = _o.Credito(_o.Ana, 200_000m, "CR-PAGADO");
        cancelado.Status = LoanStatus.PaidOff;
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();

        (await CrearAsync(Recogiendo(Total(mora)))).Error.Code.Should().Be(ErroresDeRecogida.CodigoRequiereReestructuracion);
        var r = await CrearAsync(Recogiendo(Total(castigado)));
        r.Error.Code.Should().Be(ErroresDeRecogida.CodigoNoPagable);
        OriginacionDePrueba.Datos(r.Error).GetProperty("reason").GetString().Should().Be(ErroresDeRecogida.MotivoCastigado);
        var p = await CrearAsync(Recogiendo(Total(cancelado)));
        OriginacionDePrueba.Datos(p.Error).GetProperty("reason").GetString().Should().Be(ErroresDeRecogida.MotivoNoActivo);
    }

    [Fact]
    public async Task En_insolvencia_que_negocia_no_es_pagable()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        _o.Db.InsolvencyProceedings.Add(new InsolvencyProceeding { PersonId = _o.Ana.Id, StartDate = Hoy.AddDays(-10) });
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();

        var r = await CrearAsync(Recogiendo(Total(viejo)));

        r.Error.Code.Should().Be(ErroresDeRecogida.CodigoNoPagable);
        OriginacionDePrueba.Datos(r.Error).GetProperty("reason").GetString().Should().Be(ErroresDeRecogida.MotivoInsolvencia);
    }

    [Fact]
    public async Task La_linea_decide_si_admite_recoger_creditos_y_de_que_modo()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        var linea = await _o.Db.CreditLines.AsNoTracking().SingleAsync(l => l.PublicId == _o.Linea.PublicId);

        Parametro(ParametrosDeCartera.RecogidaProductosAdmitidos, "Creditos:Total", linea.Id);
        var modo = await CrearAsync(Recogiendo(Parcial(viejo, 200_000m)));
        modo.Error.Code.Should().Be(ErroresDeRecogida.CodigoModoNoAdmitido);
        OriginacionDePrueba.Datos(modo.Error).GetProperty("admittedModes").EnumerateArray().Select(x => x.GetString()).Should().Equal("Total");
        (await CrearAsync(Recogiendo(Total(viejo)))).IsSuccess.Should().BeTrue("total sí lo admite");

        Parametro(ParametrosDeCartera.RecogidaProductosAdmitidos, "Aportes:Cuotas", linea.Id, new DateOnly(2026, 6, 1));
        (await CrearAsync(Recogiendo(Total(viejo)))).Error.Code.Should().Be(ErroresDeRecogida.CodigoProductoNoAdmitido);
    }

    [Fact]
    public async Task El_neto_tiene_que_quedar_positivo()
    {
        var viejo = _o.Credito(_o.Ana, 3_000_000m, "CR-GRANDE");

        var r = await CrearAsync(Recogiendo(Total(viejo)));

        r.Error.Code.Should().Be(ErroresDeRecogida.CodigoNetoNoPositivo);
        OriginacionDePrueba.Datos(r.Error).GetProperty("net").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public void La_forma_la_revisa_el_validador()
    {
        var v = new CreditApplicationInputValidator();
        var credito = Guid.NewGuid();

        v.Validate(_o.Solicitud() with { Payoffs = [new PayoffInput(PayoffProductKind.Loan, credito, PayoffMode.Partial)] }).IsValid
            .Should().BeFalse("un parcial lleva valor");
        v.Validate(_o.Solicitud() with { Payoffs = [new PayoffInput(PayoffProductKind.Account, credito, PayoffMode.Deposit, 1m)] }).IsValid
            .Should().BeFalse("en E1 sólo se recogen créditos");
        v.Validate(_o.Solicitud() with { Payoffs = [new PayoffInput(PayoffProductKind.Loan, credito, PayoffMode.Total), new PayoffInput(PayoffProductKind.Loan, credito, PayoffMode.Total)] })
            .IsValid.Should().BeFalse("un producto no va dos veces");
        v.Validate(_o.Solicitud() with { Payoffs = [new PayoffInput(PayoffProductKind.Loan, credito, PayoffMode.Total, InstallmentTreatment: InstallmentTreatment.Exclude)] })
            .IsValid.Should().BeFalse("dejar la cuota causada llega con E2");
        v.Validate(_o.Solicitud() with { OwnLoanPayoffs = [new OwnLoanPayoffInput(credito, "Partial")] }).IsValid.Should().BeFalse();
        v.Validate(_o.Solicitud() with { Payoffs = [new PayoffInput(PayoffProductKind.Loan, credito, PayoffMode.Partial, 1_000m)] }).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Un_credito_que_otra_solicitud_viva_recoge_es_un_aviso_y_no_bloquea()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        var primera = (await CrearAsync(Recogiendo(Total(viejo)))).Value;

        var segunda = await CrearAsync(Recogiendo(Total(viejo)));

        segunda.IsSuccess.Should().BeTrue(segunda.IsFailure ? $"{segunda.Error.Code}: {segunda.Error.Message}" : string.Empty);
        segunda.Value.Payoffs.Single().InOtherApplication.Should().Be(string.Empty, "la otra es un borrador sin número");
        (await LeerAsync(primera.PublicId)).Payoffs.Single().InOtherApplication.Should().Be(string.Empty);
    }

    // ----------------------------------------------------------------------------------------------------- radicar --

    [Fact]
    public async Task Al_radicar_vuelve_a_proyectar_con_lo_de_ahora()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        var s = (await CrearAsync(Recogiendo(Total(viejo)) with { PayoffProjection = new PayoffProjectionInput(Hoy.AddDays(2), _o.G.UtcNow.AddHours(-1)) }))
            .Value;
        _o.Db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = viejo.Id, Kind = LoanTransactionKind.Disbursement, Concept = LoanConcept.Principal, Amount = 250_000m, ValueDate = viejo.OpenedOn,
            AccountingDate = viejo.OpenedOn, RegisteredAt = _o.G.UtcNow, OperationId = 2,
        });
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();
        await _o.DocumentosAsync(s);

        var r = await _o.Radicar().Handle(_o.Radicacion(s), default);
        _o.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var fila = (await FilasAsync(s.PublicId)).Single();
        (fila.ProjectedAmount, fila.ProjectedDate, fila.RecordedAsOf).Should().Be((1_250_000m, Hoy.AddDays(2), _o.G.UtcNow), "R = ahora; la D que guardó");
        (await FilasAsync(s.PublicId, conBajas: true)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Al_radicar_un_credito_que_entro_en_mora_lo_rechaza()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        var s = (await CrearAsync(Recogiendo(Total(viejo)))).Value;
        _o.Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = viejo.Id, PositionDate = Hoy, Version = 1, RegisteredAt = _o.G.UtcNow, Status = LoanStatus.Active, BranchId = viejo.BranchId,
            CreditLineId = viejo.CreditLineId, ModalityCode = "CONSUMO", Principal = 1_000_000m, PayoffAmount = 1_030_000m, DaysPastDue = 5,
        });
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();
        await _o.DocumentosAsync(s);

        var r = await _o.Radicar().Handle(_o.Radicacion(s), default);

        r.Error.Code.Should().Be(ErroresDeRecogida.CodigoRequiereReestructuracion);
    }

    // ------------------------------------------------------------------------------ validaciones después de recoger --

    [Fact]
    public async Task La_capacidad_de_pago_no_cuenta_la_cuota_de_lo_recogido_total_y_el_parcial_no_la_reduce()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        ConCuota(viejo, 3_400_000m);

        var sin = await _o.RadicadaAsync(_o.Solicitud());
        var con = await _o.RadicadaAsync(Recogiendo(Total(viejo)));
        var parcial = await _o.RadicadaAsync(Recogiendo(Parcial(viejo, 300_000m)));

        var c = Validacion(sin, ApplicationCheckKind.PaymentCapacity);
        c.Outcome.Should().Be(CheckOutcome.Blocking, "5.000.000 − 1.500.000 − 3.400.000 no alcanza para la cuota nueva");
        Liberado(c).ValueKind.Should().Be(JsonValueKind.Null, "sin recogidas no se dice nada");
        var conRecogida = Validacion(con, ApplicationCheckKind.PaymentCapacity);
        conRecogida.Outcome.Should().Be(CheckOutcome.Passed, "la cuota del crédito recogido total se libera");
        Liberado(conRecogida).GetDecimal().Should().Be(3_400_000m);
        conRecogida.Detail!.Value.GetProperty("ownInstallments").GetDecimal().Should().Be(0m);
        var conParcial = Validacion(parcial, ApplicationCheckKind.PaymentCapacity);
        conParcial.Outcome.Should().Be(CheckOutcome.Blocking, "un parcial no reduce la cuota (conservador)");
        Liberado(conParcial).GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task La_capacidad_de_descuento_por_nomina_propia_no_cuenta_la_cuota_de_lo_recogido()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-NOMINA");
        ConCuota(viejo, 2_400_000m);
        _o.Db.LoanPayrollLinks.Add(LoanPayrollLink.AlOriginar(viejo.Id, RepaymentChannel.OwnPayroll, _o.Dario.Id, 1, viejo.OpenedOn));
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();
        var porNomina = _o.Solicitud() with { PaymentForm = RepaymentChannel.OwnPayroll };

        var sin = await _o.RadicadaAsync(porNomina);
        var con = await _o.RadicadaAsync(porNomina with { Payoffs = [Total(viejo)] });

        Validacion(sin, ApplicationCheckKind.DeductionCapacity).Outcome.Should().Be(CheckOutcome.Blocking, "2.500.000 permitidos − 2.400.000 − la cuota nueva");
        var c = Validacion(con, ApplicationCheckKind.DeductionCapacity);
        c.Outcome.Should().Be(CheckOutcome.Passed);
        Liberado(c).GetDecimal().Should().Be(2_400_000m);
        c.Detail!.Value.GetProperty("currentDeductions").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task La_libranza_no_cuenta_lo_que_la_pagaduria_deja_de_descontar_por_lo_recogido()
    {
        var mismo = _o.Credito(_o.Ana, 1_000_000m, "CR-LIB1");
        var otro = _o.Credito(_o.Ana, 1_000_000m, "CR-LIB2");
        ConCuota(mismo, 400_000m);
        ConCuota(otro, 250_000m);
        foreach (var credito in new[] { mismo, otro })
            _o.Db.LoanPayrollLinks.Add(new LoanPayrollLink
            {
                LoanId = credito.Id, RepaymentChannel = RepaymentChannel.PaymasterDeduction, PayrollAgreementId = 77, PaymasterPersonId = _o.Dario.Id,
                ValidFrom = credito.OpenedOn, OperationId = 1,
            });
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();

        var (vigentes, liberado) = await _o.Convenios().DescuentosVigentesSinAsync(_o.Ana.Id, 77, null, Hoy, null, [mismo.Id], default);

        (vigentes, liberado).Should().Be((250_000m, 400_000m));
        (await _o.Convenios().DescuentosVigentesAsync(_o.Ana.Id, 77, null, Hoy, null, default)).Should().Be(650_000m, "sin recogidas, como siempre");
    }

    [Fact]
    public async Task El_endeudamiento_consolidado_no_cuenta_el_capital_de_lo_recogido()
    {
        using var o = await OriginacionDePrueba.CrearAsync(version: OriginacionDePrueba.VersionDeLinea() with
        {
            CreditLimit = new CreditLimitInput(MaxIndebtedness: 3_500_000m),
        });
        var viejo = o.Credito(o.Ana, 1_000_000m, "CR-VIEJO");

        var sin = await o.RadicadaAsync(o.Solicitud());
        var con = await o.RadicadaAsync(o.Solicitud() with { Payoffs = [Total(viejo)] });
        var parcial = await o.RadicadaAsync(o.Solicitud() with { Payoffs = [Parcial(viejo, 400_000m)] });

        Validacion(sin, ApplicationCheckKind.ConsolidatedExposure).Outcome.Should().Be(CheckOutcome.Blocking, "3.000.000 + 1.000.000 pasan de 3.500.000");
        var c = Validacion(con, ApplicationCheckKind.ConsolidatedExposure);
        c.Outcome.Should().Be(CheckOutcome.Passed);
        (Liberado(c).GetDecimal(), c.Detail!.Value.GetProperty("consolidated").GetDecimal()).Should().Be((1_000_000m, 3_000_000m));
        var p = Validacion(parcial, ApplicationCheckKind.ConsolidatedExposure);
        (p.Outcome, Liberado(p).GetDecimal()).Should().Be((CheckOutcome.Blocking, 400_000m), "el parcial cuenta el saldo que queda: 3.600.000");
        con.ConsolidatedExposure.Should().Be(4_000_000m, "el consolidado del enrutamiento (quién decide) no cambia");
    }

    [Fact]
    public async Task El_cupo_vuelve_a_tener_disponible_el_capital_de_lo_recogido()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        _o.CupoDe[_o.Ana.Id] = new DisponibleDeCupoCalculado(true, 3_500_000m, 1_000_000m, 2_500_000m, Guid.NewGuid())
        {
            CapitalPorCredito = new Dictionary<int, decimal> { [viejo.Id] = 1_000_000m },
        };

        var sin = await _o.RadicadaAsync(_o.Solicitud());
        var con = await _o.RadicadaAsync(Recogiendo(Total(viejo)));

        Validacion(sin, ApplicationCheckKind.CreditLimit).Outcome.Should().Be(CheckOutcome.Blocking, "2.500.000 disponibles para 3.000.000");
        var c = Validacion(con, ApplicationCheckKind.CreditLimit);
        c.Outcome.Should().Be(CheckOutcome.Passed);
        (Liberado(c).GetDecimal(), c.Detail!.Value.GetProperty("available").GetDecimal()).Should().Be((1_000_000m, 3_500_000m));
    }

    [Fact]
    public void El_disponible_despues_de_recoger_nunca_baja_de_cero_ni_pasa_del_cupo()
    {
        var cupo = new DisponibleDeCupoCalculado(true, 3_000_000m, 4_000_000m, 0m, null);

        cupo.DisponibleDespuesDe(500_000m).Should().Be(0m, "sigue consumido por encima del cupo");
        cupo.DisponibleDespuesDe(1_500_000m).Should().Be(500_000m);
        cupo.DisponibleDespuesDe(9_000_000m).Should().Be(3_000_000m);
        new RecogidaEnLaValidacion(1, "X", false, 700_000m).CapitalQueLibera(500_000m).Should().Be(500_000m, "lo liberado no pasa del capital del crédito");
        new RecogidaEnLaValidacion(1, "X", true, null).CapitalQueLibera(-10m).Should().Be(0m);
    }

    /// <summary>
    /// Con un patrimonio técnico de 40.000.000 el límite individual es 4.000.000: Ana debe 1.800.000 y pide 3.000.000 (pasa del límite: no se radica);
    /// si recoge 1.000.000 de su crédito, queda en 3.800.000 y se radica, se aprueba (la decisión revalida igual) y se desembolsa (el desembolso mide sobre
    /// lo que queda después de recoger).
    /// </summary>
    [Fact]
    public async Task La_concentracion_se_mide_despues_de_recoger_al_radicar_al_decidir_y_al_desembolsar()
    {
        using var d = await DesembolsoDePrueba.CrearAsync();
        d.O.PatrimonioTecnico = new IndicadorRegistradoDto(Guid.NewGuid(), 40_000_000m, "COP", new DateOnly(2026, 11, 30), "Contabilidad");
        var viejo = d.O.Credito(d.O.Ana, 1_800_000m, "CR-VIEJO");

        var borrador = await d.O.BorradorAsync();
        await d.O.DocumentosAsync(borrador);
        var negada = await d.O.Radicar().Handle(d.O.Radicacion(borrador), default);
        negada.Error.Code.Should().Be(ErroresDeOriginacion.CodigoConcentracionExcedida, "1.800.000 + 3.000.000 pasan de 4.000.000");
        d.Db.ChangeTracker.Clear();

        var s = await d.AprobadaAsync(d.O.Solicitud() with { Payoffs = [Parcial(viejo, 1_000_000m)] });
        var leida = await d.O.Leer().Handle(new GetCreditApplicationQuery(s.PublicId), default);
        d.Db.ChangeTracker.Clear();
        leida.Value.Status.Should().Be(CreditApplicationStatus.Approved);
        var c = Validacion(leida.Value, ApplicationCheckKind.Concentration);
        c.Outcome.Should().Be(CheckOutcome.Passed);
        Liberado(c).GetDecimal().Should().Be(1_000_000m);
        c.Detail!.Value.GetProperty("debtor").GetProperty("current").GetDecimal().Should().Be(800_000m);

        var r = await d.DesembolsadoAsync(d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId, "Partial", 1_000_000m)] });

        r.NetToDisburse.Should().Be(2_000_000m);
    }

    // --------------------------------------------------------------------------------------- desembolso: lo autorizado --

    [Fact]
    public async Task El_desembolso_lee_lo_autorizado_de_las_filas_y_no_de_PayoffRequest()
    {
        using var d = await DesembolsoDePrueba.CrearAsync();
        var viejo = d.O.Credito(d.O.Ana, 1_000_000m, "CR-VIEJO");
        var otro = d.O.Credito(d.O.Ana, 500_000m, "CR-OTRO");
        var s = await d.AprobadaAsync(d.O.Solicitud() with { Payoffs = [Total(viejo)] });
        // Un PayoffRequest de antes de la feature que nombra otro crédito ya no autoriza nada.
        var solicitud = await d.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId);
        solicitud.PayoffRequest = JsonSerializer.Serialize(new[] { new OwnLoanPayoffInput(otro.PublicId) }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await d.Db.SaveChangesAsync();
        d.Db.ChangeTracker.Clear();

        var noAutorizada = await d.DesembolsarAsync(d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(otro.PublicId)] });
        var autorizada = await d.DesembolsarAsync(d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId)] });

        noAutorizada.Error.Code.Should().Be(ErroresDeRecogida.CodigoDebeCoincidirConLaSolicitud, "la lista del cuerpo, si viene, es la de la solicitud (D9)");
        autorizada.IsSuccess.Should().BeTrue(autorizada.IsFailure ? $"{autorizada.Error.Code}: {autorizada.Error.Message}" : string.Empty);
        autorizada.Value.Payoffs.Should().ContainSingle(p => p.LoanPublicId == viejo.PublicId);
    }

    // ------------------------------------------------------------------------------------------- huella del enrutamiento --

    [Fact]
    public async Task Lo_que_recoge_entra_en_la_huella_que_compara_quien_decide()
    {
        var viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        var s = await _o.RadicadaAsync(Recogiendo(Total(viejo)));
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");
        var antes = (await _o.Enrutado().Handle(new GetCreditApplicationRoutingQuery(s.PublicId), default)).Value.ContentSha256;
        _o.Db.ChangeTracker.Clear();

        var fila = await _o.Db.CreditApplicationPayoffs.SingleAsync(f => f.LoanId == viejo.Id && !f.IsDeleted);
        fila.IsDeleted = true;
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
        var despues = (await _o.Enrutado().Handle(new GetCreditApplicationRoutingQuery(s.PublicId), default)).Value.ContentSha256;
        _o.Db.ChangeTracker.Clear();

        despues.Should().NotBe(antes, "sin la recogida es otra solicitud");
        var r = await _o.Decidir().Handle(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditApplication, s.PublicId, CreditDecisionOutcome.Approve,
            null, null, antes) { OperationKey = Guid.NewGuid() }, default);
        r.Error.Code.Should().Be(ErroresDeDecisionesYCupos.CodigoContenidoCambiado);
    }
}
