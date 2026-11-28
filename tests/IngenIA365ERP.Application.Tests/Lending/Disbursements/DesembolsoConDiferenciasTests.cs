using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 028 (T032–T037, T040, T041, T043; HU3, FR-012–FR-018, FR-027a; api.md §3): el desembolso toma las recogidas de la solicitud —la lista del
/// cuerpo es opcional y, si viene, es la misma—, las liquida a la fecha valor con lo de ahora, las compara con lo proyectado (por concepto, con su
/// causa), omite lo ya cancelado, aplica según <c>Recogida.AlDesembolsar</c>, bloquea sobre <c>Recogida.ToleranciaDeDiferencia</c>, exige reconocer
/// las diferencias, vuelve a liquidar bajo el cerrojo, gira por lo <b>aplicado</b> y deja una fila <c>LND_DisbursementPayoffs</c> por producto; un tramo
/// siguiente no recoge. El escenario es <see cref="DesembolsoDePrueba"/> con el contrato doble y el crédito de libro de Ana (1.000.000); lo que pide el
/// contrato real va en <see cref="DesembolsoConElContratoRealTests"/>. (nuevo)
/// </summary>
public sealed class DesembolsoConDiferenciasTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private Loan _viejo = null!;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _viejo = _d.O.Credito(_d.O.Ana, 1_000_000m, "CR-VIEJO");
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    internal static PayoffInput Total(Loan credito) => new(PayoffProductKind.Loan, credito.PublicId, PayoffMode.Total);

    internal static PayoffInput Parcial(Loan credito, decimal valor) => new(PayoffProductKind.Loan, credito.PublicId, PayoffMode.Partial, valor);

    private Task<CreditApplicationDto> AprobadaAsync(params PayoffInput[] recogidas) =>
        _d.AprobadaAsync(_d.O.Solicitud() with { Payoffs = recogidas.Length == 0 ? [Total(_viejo)] : recogidas });

    private async Task<DisbursementPreviewDto> VistaAsync(DisbursementInput entrada)
    {
        var r = await _d.VistaPrevia().Handle(new PreviewDisbursementQuery(entrada), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _d.Db.ChangeTracker.Clear();
        return r.Value;
    }

    /// <summary>Una fila del libro del crédito de libro, registrada ahora (después de la proyección).</summary>
    private void Movimiento(Loan credito, LoanConcept concepto, decimal monto, LoanTransactionKind clase = LoanTransactionKind.Charge)
    {
        _d.Db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = credito.Id, Kind = clase, Concept = concepto, Amount = monto, ValueDate = DesembolsoDePrueba.Hoy, AccountingDate = DesembolsoDePrueba.Hoy,
            RegisteredAt = _d.G.UtcNow.AddMinutes(5), OperationId = 1,
        });
        _d.Db.SaveChanges();
        _d.Db.ChangeTracker.Clear();
    }

    /// <summary>Un pago en caja después de la proyección: su operación y lo que bajó del capital.</summary>
    private void Pago(Loan credito, decimal monto)
    {
        var operacion = new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.Payment, Number = "PAG-CAJA", Sequence = 1, ValueDate = DesembolsoDePrueba.Hoy,
            AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = _d.G.UtcNow.AddMinutes(5), Amount = monto, CollectorCode = "REC",
            IdempotencyKey = $"REC:{Guid.NewGuid():N}", Payload = "{}", InputsSha256 = "x", RulesVersion = "LND-2026.1",
        };
        _d.Db.LoanOperations.Add(operacion);
        _d.Db.SaveChanges();
        _d.Db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = credito.Id, Kind = LoanTransactionKind.PaymentAllocation, Concept = LoanConcept.Principal, Amount = -monto, ValueDate = DesembolsoDePrueba.Hoy,
            AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = operacion.RegisteredAt, OperationId = operacion.Id,
        });
        _d.Db.SaveChanges();
        _d.Db.ChangeTracker.Clear();
    }

    private void Parametro(string clave, string valor)
    {
        _d.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, Value = valor, ScopeKind = ParameterScopeKind.None, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedBy = "test", CreatedAt = _d.G.UtcNow.AddDays(-10),
        });
        _d.Db.SaveChanges();
        _d.Db.ChangeTracker.Clear();
    }

    private void CerrarElViejo()
    {
        var credito = _d.Db.Loans.Single(l => l.Id == _viejo.Id);
        credito.Status = LoanStatus.PaidOff;
        _d.Db.SaveChanges();
        _d.Db.ChangeTracker.Clear();
    }

    // ------------------------------------------------------------------------------------------------- sin diferencias --

    [Fact]
    public async Task Sin_diferencias_no_se_pide_reconocer_y_la_fila_queda_aplicada()
    {
        var s = await AprobadaAsync();

        var vista = await VistaAsync(_d.Entrada(s));
        var r = await _d.DesembolsadoAsync(_d.Entrada(s));

        var p = vista.Payoffs.Should().ContainSingle().Subject;
        (p.ProductPublicId, p.Kind, p.Mode, p.RequestedMode).Should().Be((_viejo.PublicId, "Loan", "Total", "Total"));
        (p.Projected, p.Current, p.ToApply, p.Difference, p.Omitted, p.ToPayOff).Should()
            .Be(((decimal?)1_000_000m, 1_000_000m, 1_000_000m, (decimal?)0m, false, 1_000_000m));
        p.Causes.Should().BeEmpty();
        p.Detail.Should().BeEmpty();
        vista.PayoffDifferences.Should().Be(new DisbursementPayoffDifferencesDto(0, 0m, false, false));
        vista.NetToDisburse.Should().Be(2_000_000m);
        (await _d.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingPayoffViewed, "ver lo que cuesta recogerlo es ver su liquidación");

        r.NetToDisburse.Should().Be(2_000_000m, "la lista del cuerpo es opcional: las recogidas salen de la solicitud");
        var fila = await _d.Db.DisbursementPayoffs.AsNoTracking().SingleAsync();
        (fila.ProjectedAmount, fila.CurrentAmount, fila.AppliedAmount, fila.Difference, fila.DifferenceCause, fila.Omitted).Should()
            .Be((1_000_000m, 1_000_000m, 1_000_000m, 0m, PayoffDifferenceCause.None, false));
        (fila.Mode, fila.InstallmentTreatment, fila.AcknowledgedBy).Should().Be((PayoffMode.Total, (InstallmentTreatment?)InstallmentTreatment.Include, (string?)null));
        fila.IdempotencyKey.Should().Be(RecogidaEnElDesembolso.Clave(r.OperationPublicId, _viejo.PublicId));
        fila.CreditApplicationPayoffId.Should().Be((await _d.Db.CreditApplicationPayoffs.AsNoTracking().SingleAsync(f => !f.IsDeleted)).Id);
        fila.DisbursementOperationId.Should().Be((await _d.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == r.OperationPublicId)).Id);
    }

    // ------------------------------------------------------------------------------------------------ recalcular (HU3-2) --

    [Fact]
    public async Task Un_pago_desde_la_proyeccion_es_una_diferencia_por_pagos_que_se_reconoce_y_la_orden_sale_por_lo_aplicado()
    {
        var s = await AprobadaAsync();
        Pago(_viejo, 250_000m);

        var vista = await VistaAsync(_d.Entrada(s));
        var sinReconocer = await _d.DesembolsarAsync(_d.Entrada(s));
        var r = await _d.DesembolsadoAsync(_d.Entrada(s) with { AcknowledgedPayoffDifferences = true, ExpectedPreviewSha256 = vista.PreviewSha256 });

        var p = vista.Payoffs.Single();
        (p.Projected, p.Current, p.ToApply, p.Difference).Should().Be(((decimal?)1_000_000m, 750_000m, 750_000m, (decimal?)(-250_000m)));
        p.Causes.Should().Equal("Payments");
        p.Detail.Should().Equal(new Dictionary<string, decimal> { ["principal"] = -250_000m });
        p.Warnings.Should().ContainSingle(w => w.Contains("pagos recibidos desde la proyección"));
        vista.PayoffDifferences.Should().Be(new DisbursementPayoffDifferencesDto(1, -250_000m, true, false));
        vista.NetToDisburse.Should().Be(2_250_000m, "el neto sube lo que bajó la recogida");

        sinReconocer.Error.Code.Should().Be(ErroresDeRecogida.CodigoDiferenciasSinReconocer);

        var pago = _d.Aplicadas.Should().ContainSingle().Subject.Payments.Single();
        (pago.Modality, pago.Amount).Should().Be((ModalidadDePago.FullPayoff, 750_000m));
        r.NetToDisburse.Should().Be(2_250_000m, "la orden sale por el neto que mostró la vista previa");
        var orden = await _d.Db.DisbursementOrders.AsNoTracking().SingleAsync();
        (orden.PayoffAmount, orden.NetAmount).Should().Be((750_000m, 2_250_000m));
        var aplicada = r.Payoffs.Should().ContainSingle().Subject;
        (aplicada.Applied, aplicada.Projected, aplicada.Current, aplicada.Difference).Should()
            .Be((750_000m, (decimal?)1_000_000m, 750_000m, (decimal?)(-250_000m)));
        aplicada.Causes.Should().Equal("Payments");

        var fila = await _d.Db.DisbursementPayoffs.AsNoTracking().SingleAsync();
        (fila.ProjectedAmount, fila.CurrentAmount, fila.AppliedAmount, fila.Difference, fila.DifferenceCause).Should()
            .Be((1_000_000m, 750_000m, 750_000m, -250_000m, PayoffDifferenceCause.Payments));
        (fila.AcknowledgedBy, fila.AcknowledgedAt).Should().Be((_d.Cajera.Username, (DateTime?)_d.G.UtcNow), "quien desembolsó reconoció la diferencia");

        // T041: la auditoría del desembolso lleva lo proyectado, lo aplicado, la diferencia y quién la reconoció.
        var evento = (await _d.Db.AuditOutbox.AsNoTracking().ToListAsync()).Select(f => f.PayloadJson!)
            .Single(j => AuditoriaEncadenada.LeerCarga(j).Action == AuditEventTypes.LendingOperationRegistered);
        evento.Should().Contain("payoffDetail").And.Contain("acknowledgedBy").And.Contain(_d.Cajera.Username);
    }

    // --------------------------------------------------------------------------------------- mantener lo proyectado (D5) --

    [Fact]
    public async Task Manteniendo_lo_proyectado_si_ahora_cuesta_mas_se_abona_lo_proyectado_y_el_credito_queda_con_saldo()
    {
        Parametro(ParametrosDeCartera.RecogidaAlDesembolsar, ParametrosDeCartera.AlDesembolsarMantenerLoProyectado);
        var s = await AprobadaAsync();
        Movimiento(_viejo, LoanConcept.OtherCharge, 200_000m);

        var vista = await VistaAsync(_d.Entrada(s));
        await _d.DesembolsadoAsync(_d.Entrada(s) with { AcknowledgedPayoffDifferences = true });

        var p = vista.Payoffs.Single();
        (p.Current, p.ToApply, p.Mode, p.Difference).Should().Be((1_200_000m, 1_000_000m, "Partial", (decimal?)200_000m));
        p.Causes.Should().Equal("Other");
        p.Warnings.Should().Contain(w => w.Contains("queda con un saldo de $200.000"));
        vista.Warnings.Should().Contain(w => w.Contains("queda con un saldo"), "el aviso es claro también en la vista previa");
        vista.NetToDisburse.Should().Be(2_000_000m);
        var pago = _d.Aplicadas.Single().Payments.Single();
        (pago.Modality, pago.Amount, pago.Choice).Should().Be((ModalidadDePago.ExtraordinaryPrincipal, 1_000_000m, (EleccionDeAbono?)EleccionDeAbono.ReduceTerm));
        (await _d.Db.DisbursementPayoffs.AsNoTracking().SingleAsync()).Mode.Should().Be(PayoffMode.Partial, "se aplicó como abono");
    }

    [Fact]
    public async Task Manteniendo_lo_proyectado_si_ahora_cuesta_menos_se_cancela_por_lo_de_ahora()
    {
        Parametro(ParametrosDeCartera.RecogidaAlDesembolsar, ParametrosDeCartera.AlDesembolsarMantenerLoProyectado);
        var s = await AprobadaAsync();
        Pago(_viejo, 300_000m);

        var vista = await VistaAsync(_d.Entrada(s));
        await _d.DesembolsadoAsync(_d.Entrada(s) with { AcknowledgedPayoffDifferences = true });

        (vista.Payoffs.Single().ToApply, vista.Payoffs.Single().Mode).Should().Be((700_000m, "Total"), "lo proyectado se topa en lo que cancela");
        var pago = _d.Aplicadas.Single().Payments.Single();
        (pago.Modality, pago.Amount).Should().Be((ModalidadDePago.FullPayoff, 700_000m));
    }

    // ---------------------------------------------------------------------------------------------- omitido (HU3-4) --

    [Fact]
    public async Task Un_credito_ya_cancelado_se_omite_sube_el_neto_y_hay_que_reconocerlo()
    {
        var s = await AprobadaAsync();
        CerrarElViejo();

        var vista = await VistaAsync(_d.Entrada(s));
        var sinReconocer = await _d.DesembolsarAsync(_d.Entrada(s));
        var r = await _d.DesembolsadoAsync(_d.Entrada(s) with { AcknowledgedPayoffDifferences = true });

        var p = vista.Payoffs.Single();
        (p.Omitted, p.ToApply, p.Current, p.Difference).Should().Be((true, 0m, 0m, (decimal?)(-1_000_000m)));
        p.Causes.Should().Equal("ProductClosed");
        p.Warnings.Single().Should().Contain("ya está cancelado").And.Contain("No se recoge");
        vista.NetToDisburse.Should().Be(3_000_000m);
        vista.PayoffDifferences.Should().Be(new DisbursementPayoffDifferencesDto(1, -1_000_000m, true, false));
        sinReconocer.Error.Code.Should().Be(ErroresDeRecogida.CodigoDiferenciasSinReconocer);

        _d.Aplicadas.Should().BeEmpty("lo ya cancelado no se manda al contrato");
        r.Payoffs.Should().BeEmpty();
        r.OmittedPayoffs.Should().ContainSingle(o => o.LoanPublicId == _viejo.PublicId && o.Projected == 1_000_000m);
        r.NetToDisburse.Should().Be(3_000_000m);
        var fila = await _d.Db.DisbursementPayoffs.AsNoTracking().SingleAsync();
        (fila.Omitted, fila.AppliedAmount, fila.CurrentAmount, fila.Difference, fila.DifferenceCause, fila.PaymentOperationId).Should()
            .Be((true, 0m, 0m, -1_000_000m, PayoffDifferenceCause.ProductClosed, (long?)null));
        fila.AcknowledgedBy.Should().Be(_d.Cajera.Username);
    }

    // ------------------------------------------------------------------------------------------------ tolerancia (D6) --

    [Theory]
    [InlineData("100000", true)]
    [InlineData("500000", false)]
    public async Task Una_diferencia_mayor_que_la_tolerancia_bloquea(string tolerancia, bool bloquea)
    {
        Parametro(ParametrosDeCartera.RecogidaToleranciaDeDiferencia, tolerancia);
        var s = await AprobadaAsync();
        Movimiento(_viejo, LoanConcept.OtherCharge, 200_000m);

        var vista = await VistaAsync(_d.Entrada(s));
        var r = await _d.DesembolsarAsync(_d.Entrada(s) with { AcknowledgedPayoffDifferences = true });

        vista.PayoffDifferences.AboveTolerance.Should().Be(bloquea);
        vista.Payoffs.Single().AboveTolerance.Should().Be(bloquea);
        vista.Blocking.Contains(ErroresDeRecogida.CodigoDiferenciaSobreLaTolerancia).Should().Be(bloquea);
        if (bloquea)
        {
            r.Error.Code.Should().Be(ErroresDeRecogida.CodigoDiferenciaSobreLaTolerancia, "reconocerla no basta: hay que devolver la solicitud");
            DesembolsoDePrueba.Datos(r.Error).GetProperty("tolerance").GetDecimal().Should().Be(100_000m);
        }
        else r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    // --------------------------------------------------------------------------------------- la lista del cuerpo (D9) --

    [Fact]
    public async Task La_lista_del_cuerpo_es_opcional_y_si_viene_es_la_de_la_solicitud()
    {
        var s = await AprobadaAsync(Parcial(_viejo, 400_000m));

        var otroModo = await _d.DesembolsarAsync(_d.Entrada(s) with { Payoffs = [Total(_viejo)] });
        var otroValor = await _d.DesembolsarAsync(_d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(_viejo.PublicId, "Partial", 300_000m)] });
        var lasDos = await _d.DesembolsarAsync(_d.Entrada(s) with
        {
            Payoffs = [Parcial(_viejo, 400_000m)], OwnLoanPayoffs = [new OwnLoanPayoffInput(_viejo.PublicId, "Partial", 400_000m)],
        });
        var vista = await VistaAsync(_d.Entrada(s) with { Payoffs = [Total(_viejo)] });
        var r = await _d.DesembolsadoAsync(_d.Entrada(s) with { Payoffs = [Parcial(_viejo, 400_000m)] });

        otroModo.Error.Code.Should().Be(ErroresDeRecogida.CodigoDebeCoincidirConLaSolicitud);
        DesembolsoDePrueba.Datos(otroModo.Error).GetProperty("authorized")[0].GetString().Should().Be("CR-VIEJO parcial $400.000");
        otroValor.Error.Code.Should().Be(ErroresDeRecogida.CodigoDebeCoincidirConLaSolicitud);
        lasDos.Error.Code.Should().Be(RecogidasPedidas.CodigoCuerpoInvalido);
        vista.Blocking.Should().Contain(ErroresDeRecogida.CodigoDebeCoincidirConLaSolicitud, "la vista previa también lo dice");
        r.Payoffs.Single().Applied.Should().Be(400_000m);
    }

    [Fact]
    public void El_validador_admite_el_cuerpo_sin_recogidas_y_pide_el_valor_de_un_parcial()
    {
        var validador = new ExecuteLoanDisbursementCommandValidator();
        var sinLista = new DisbursementInput { ApplicationPublicId = Guid.NewGuid() };

        validador.Validate(new ExecuteLoanDisbursementCommand(sinLista)).IsValid.Should().BeTrue("las recogidas salen de la solicitud");
        validador.Validate(new ExecuteLoanDisbursementCommand(sinLista with
        {
            Payoffs = [new PayoffInput(PayoffProductKind.Loan, Guid.NewGuid(), PayoffMode.Partial)],
        })).IsValid.Should().BeFalse("un parcial lleva su valor");
    }

    // ------------------------------------------------------------------------------------------- defensa del giro (H4) --

    [Fact]
    public async Task Si_el_contrato_aplica_otra_cosa_que_lo_liquidado_el_desembolso_no_existe()
    {
        var s = await AprobadaAsync();
        _d.Contrato.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>()).Returns(ci => Task.FromResult(new RespuestaDeAplicacionDto(
            ci.Arg<SolicitudDeAplicacion>().Payments.Select(p => new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(ResultadoDelContrato.Applied,
                "PagoAplicado", "Aplicado", DesembolsoDePrueba.Comprobante(p) with { AppliedAmount = p.Amount - 1m })).ToList(), [])));

        var r = await _d.DesembolsarAsync(_d.Entrada(s));

        r.Error.Code.Should().Be(ErroresDeRecogida.CodigoAplicadoDistinto);
        var datos = DesembolsoDePrueba.Datos(r.Error);
        (datos.GetProperty("expected").GetDecimal(), datos.GetProperty("applied").GetDecimal()).Should().Be((1_000_000m, 999_999m));
        (await _d.Db.Loans.CountAsync()).Should().Be(1, "sólo queda el recogido: el crédito nuevo no nació");
        (await _d.Db.DisbursementOrders.CountAsync()).Should().Be(0);
        (await _d.Db.DisbursementPayoffs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Si_lo_que_se_recoge_cambia_bajo_el_cerrojo_se_pide_otra_vista_previa()
    {
        var s = await AprobadaAsync();
        // Un pago entra entre la evaluación y el cerrojo: la puesta al día es el primer paso bajo el cerrojo.
        _d.Motor = new MotorQueCambiaAlPonerAlDia(_d.MotorDelCredito(), () =>
        {
            _d.Db.LoanTransactions.Add(new LoanTransaction
            {
                LoanId = _viejo.Id, Kind = LoanTransactionKind.PaymentAllocation, Concept = LoanConcept.Principal, Amount = -100_000m,
                ValueDate = DesembolsoDePrueba.Hoy, AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = _d.G.UtcNow, OperationId = 1,
            });
            _d.Db.SaveChanges();
        });

        var r = await _d.DesembolsarAsync(_d.Entrada(s));

        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoVistaPreviaCambio);
        _d.Aplicadas.Should().BeEmpty();
    }

    // --------------------------------------------------------------------------------------------------------- tramos --

    [Fact]
    public async Task Un_tramo_siguiente_no_recoge()
    {
        using var d = await DesembolsoDePrueba.CrearAsync(OriginacionDePrueba.VersionDeLinea() with { Amortization = new AmortizationInput { AllowsTranches = true } });
        var viejo = d.O.Credito(d.O.Ana, 1_000_000m, "CR-VIEJO");
        var s = await d.AprobadaAsync(d.O.Solicitud() with { Payoffs = [Total(viejo)] });
        var primero = await d.DesembolsadoAsync(d.Entrada(s) with { TrancheAmount = 2_000_000m });
        var segundaEntrada = d.Entrada(s) with { LoanPublicId = primero.LoanPublicId, TrancheAmount = 1_000_000m };

        var vista = await d.VistaPrevia().Handle(new PreviewDisbursementQuery(segundaEntrada), default);
        d.Db.ChangeTracker.Clear();
        var conLista = await d.DesembolsarAsync(segundaEntrada with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId)] });
        var segundo = await d.DesembolsadoAsync(segundaEntrada);

        primero.NetToDisburse.Should().Be(1_000_000m, "el primer tramo recoge");
        vista.Value.Payoffs.Should().BeEmpty();
        vista.Value.PayoffDifferences.RequiresAcknowledgement.Should().BeFalse();
        conLista.Error.Code.Should().Be(ErroresDeRecogida.CodigoDebeCoincidirConLaSolicitud);
        segundo.Payoffs.Should().BeEmpty();
        segundo.NetToDisburse.Should().Be(1_000_000m);
        d.Aplicadas.Should().ContainSingle("sólo el primer tramo pagó por el contrato");
        (await d.Db.DisbursementPayoffs.CountAsync()).Should().Be(1);
    }

    /// <summary>El motor del crédito con un cambio que llega justo al ponerse al día (bajo el cerrojo).</summary>
    private sealed class MotorQueCambiaAlPonerAlDia(IMotorDeCreditoEnDesembolso real, Action cambio) : IMotorDeCreditoEnDesembolso
    {
        public Task<LiquidacionParaRecogida> LiquidarAsync(int loanId, DateOnly fecha, CancellationToken ct) => real.LiquidarAsync(loanId, fecha, ct);

        public async Task PonerAlDiaAsync(IReadOnlyCollection<int> loanIds, DateOnly fecha, CancellationToken ct)
        {
            cambio();
            await real.PonerAlDiaAsync(loanIds, fecha, ct);
        }

        public Task<Result<IReadOnlyList<IntegrationMessage>>> RegistrarPuestaAlDiaAsync(LoanOperation disparadora, CancellationToken ct) =>
            real.RegistrarPuestaAlDiaAsync(disparadora, ct);

        public Task<IReadOnlyList<LoanTransaction>> ConciliarCausacionAsync(int loanId, DateOnly desde, LoanOperation reverso, DateOnly fechaContable,
            CancellationToken ct) => real.ConciliarCausacionAsync(loanId, desde, reverso, fechaContable, ct);
    }
}

/// <summary>
/// Feature 028 (T036, T037, T043; FR-017, FR-027a): con el contrato de obligaciones <b>real</b> (<see cref="PagosDePrueba"/>: el crédito de Ana de
/// 3.000.000 desembolsado hoy, con plan), un pago en caja después de la proyección baja lo que se recoge y sube el neto; la recogida parcial baja el
/// capital y no deja anticipo; y la regla contable del pago de la recogida que falta se ve en la vista previa. (nuevo)
/// </summary>
public sealed class DesembolsoConElContratoRealTests
{
    private static Task<CreditApplicationDto> AprobadaAsync(PagosDePrueba p, PayoffInput recogida) =>
        p.D.AprobadaAsync(p.D.O.Solicitud(monto: 5_000_000m) with { Payoffs = [recogida] });

    [Fact]
    public async Task Un_pago_en_caja_despues_de_la_proyeccion_baja_lo_que_se_recoge_y_sube_el_neto()
    {
        using var p = await PagosDePrueba.CrearAsync();
        var s = await AprobadaAsync(p, DesembolsoConDiferenciasTests.Total(p.Credito));
        p.G.UtcNow = p.G.UtcNow.AddMinutes(30);
        await p.AplicadoAsync(p.Pago(ModalidadDePago.ExtraordinaryPrincipal, 500_000m, eleccion: EleccionDeAbono.ReduceTerm));

        var vista = (await p.D.VistaPrevia().Handle(new PreviewDisbursementQuery(p.D.Entrada(s)), default)).Value;
        p.Db.ChangeTracker.Clear();
        var r = await p.D.DesembolsadoAsync(p.D.Entrada(s) with { AcknowledgedPayoffDifferences = true }, p.Contrato());

        var recogida = vista.Payoffs.Single();
        recogida.Difference.Should().Be(-500_000m);
        recogida.Causes.Should().Contain("Payments");
        vista.PayoffDifferences.RequiresAcknowledgement.Should().BeTrue();
        var aplicada = r.Payoffs.Single();
        aplicada.Applied.Should().Be(recogida.ToApply, "lo aplicado es lo que mostró la vista previa");
        r.NetToDisburse.Should().Be(5_000_000m - recogida.ToApply).And.Be(vista.NetToDisburse);
        (await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id)).Status.Should().Be(LoanStatus.PaidOff);
        var operacion = await p.OperacionAsync(aplicada.ReceiptOperationPublicId);
        (await p.Db.DisbursementPayoffs.AsNoTracking().SingleAsync()).PaymentOperationId.Should().Be(operacion.Id);
    }

    [Fact]
    public async Task La_recogida_parcial_baja_el_capital_con_la_eleccion_del_deudor_y_no_deja_anticipo()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var s = await AprobadaAsync(p, DesembolsoConDiferenciasTests.Parcial(p.Credito, 1_000_000m));

        var r = await p.D.DesembolsadoAsync(p.D.Entrada(s), p.Contrato());

        r.Payoffs.Single().Applied.Should().Be(1_000_000m);
        r.NetToDisburse.Should().Be(4_000_000m);
        var libro = p.Db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == p.Credito.Id && !t.InControl);
        (await libro.Where(t => t.Concept == LoanConcept.Principal).SumAsync(t => t.Amount)).Should().Be(2_000_000m, "el parcial abona a capital (D7)");
        (await libro.Where(t => t.Concept == LoanConcept.Advance).SumAsync(t => t.Amount)).Should().Be(0m, "no queda como anticipo (H3)");
        (await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id)).Status.Should().Be(LoanStatus.Active);
        var pago = await p.OperacionAsync(r.Payoffs.Single().ReceiptOperationPublicId);
        pago.PaymentModality.Should().Be(PaymentModality.ExtraordinaryPrincipal);
        await p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task La_regla_que_falta_para_el_pago_de_la_recogida_bloquea_desde_la_vista_previa()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var s = await AprobadaAsync(p, DesembolsoConDiferenciasTests.Total(p.Credito));
        p.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.ContabilidadFaltaDeRegla, ScopeKind = ParameterScopeKind.None, Value = "Bloquear",
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedAt = p.G.UtcNow.AddDays(-10),
        });
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();
        p.D.Contabilidad.EvaluarAsync(Arg.Is<IReadOnlyList<MensajeContableDto>>(l => l.Any(m => m.Payload is PagoAplicadoV1)), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ResultadoDeEvaluacionContableDto(false,
                [new HallazgoDeCarteraDto("PagoAplicado", "RecaudoPorAplicar", null, null, "RuleMissing", "Falta la regla", new QuienCorrigeDto("Accounting", null, null))],
                [])));

        var vista = (await p.D.VistaPrevia().Handle(new PreviewDisbursementQuery(p.D.Entrada(s)), default)).Value;
        p.Db.ChangeTracker.Clear();
        var r = await p.D.DesembolsarAsync(p.D.Entrada(s), contrato: p.Contrato());

        vista.Blocking.Should().Contain(ErroresDeDesembolso.CodigoNoContabilizable);
        vista.Prevalidation.Postable.Should().BeFalse();
        vista.Prevalidation.Errors.Should().NotBeEmpty();
        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoNoContabilizable, "se ve antes de confirmar, no al ejecutar");
        (await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id)).Status.Should().Be(LoanStatus.Active);
    }
}
