using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028, HU6 (T076–T082; R13, FR-025, FR-027): la recogida de aportes y ahorros en el desembolso. Sobre el escenario de los aportes
/// (<see cref="AportesDePrueba"/>: línea de cuota mensual de 50.000 que vence el 5, mínimo de 500.000 primero, extraordinarios), con la consignación y el
/// reverso reales: por cuotas consigna lo pendiente a la fecha; como abono, un aporte extraordinario si la línea lo admite (si no, el motivo de la línea);
/// el ahorro a la vista recibe el abono al saldo; el contractual sólo cuotas; el CDAT no se recoge; de fábrica «la cooperativa no los recoge»; la
/// consignación entra con la recogida propia (<c>CAR</c>, <c>OwnLoanPayoff</c>, <c>InternalOffset</c>) que el contrato público sigue rechazando; la
/// anulación la reversa con su clave de reverso; el giro nuevo la vuelve a descontar; y el puente queda en cero entre el comprobante del desembolso y la
/// consignación. (nuevo)
/// </summary>
public sealed class RecogidaDeCuentasTests : IDisposable
{
    private static readonly DateOnly Enero10 = new(2027, 1, 10);

    private static readonly AdmisionDeRecogida AdmiteCuentas =
        ProductosAdmitidos.Leer("Creditos:Total,Parcial;Aportes:Cuotas,Abono;AhorroALaVista:Abono;AhorroPermanente:Abono;AhorroContractual:Cuotas");

    private readonly AportesDePrueba _a = new();

    public void Dispose() => _a.Dispose();

    private RecogidaDeCuentas Recogida() => new(_a.Db, _a.Consignacion(), _a.Reversion(), _a.Reglas(), _a.ValidacionPrevia(), _a.G.Reloj);

    /// <summary>La operación del desembolso del crédito nuevo, como la ve la recogida (sólo su PublicId, su número y su fecha valor).</summary>
    private static LoanOperation Desembolso(DateOnly fecha) => new() { Number = "DES-000001", ValueDate = fecha };

    private async Task<MemberAccount> CuentaAsync(Guid publicId) => await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.PublicId == publicId);

    private async Task<CuentaRecogible> LeidaAsync(Guid publicId, DateOnly d) => (await Recogida().LeerAsync([await CuentaAsync(publicId)], d, default)).Single();

    private static RecogidaDeCuentaEvaluada Evaluada(CuentaRecogible c, PayoffMode modo, decimal valor) =>
        new(c.Cuenta.Id, c.Cuenta.PublicId, c.Cuenta.Number, modo, valor)
        {
            Cuotas = modo == PayoffMode.Installments ? c.Cuotas.Select(q => q.Cuota.PublicId).ToList() : [],
            Naturaleza = c.Cuenta.Nature, Clase = c.Cuenta.DepositClass, CreditLineId = c.Cuenta.CreditLineId, BranchId = c.Cuenta.BranchId,
            CodigoDeLinea = c.CodigoDeLinea,
        };

    private async Task<(MemberAccountDto Cuenta, CuentaRecogible Leida)> AportesConDosCuotasAsync(bool extraordinarios = true)
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version(extraordinarios: extraordinarios));
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;
        return (cuenta, await LeidaAsync(cuenta.PublicId, Enero10));
    }

    // --------------------------------------------------------------------------------------------------- T076 el camino --

    [Fact]
    public async Task T076_el_contrato_publico_sigue_rechazando_la_recogida_propia_y_solo_el_camino_interno_la_admite()
    {
        var (cuenta, _) = await AportesConDosCuotasAsync();
        var clave = RecogidaDeCuentas.Clave(Guid.NewGuid(), cuenta.PublicId);

        var r = await _a.ConsignarAsync(_a.Consignacion(cuenta, 50_000m, clave: clave, medio: ClaseDeMedioDeRecaudo.InternalOffset), RecogidaDeCuentas.Recaudador);

        r.Result.Should().Be(ResultadoDelContrato.Rejected, "el puerto público no admite CAR");
        (await _a.Db.MemberAccountOperations.CountAsync()).Should().Be(0);
        ConsignacionEnCuentas.ClaseDeMedio(ClaseDeRecaudador.OwnLoanPayoff, ClaseDeMedioDeRecaudo.InternalOffset).Should().NotBeNull("por el contrato, nunca");
        ConsignacionEnCuentas.ClaseDeMedio(ClaseDeRecaudador.OwnLoanPayoff, ClaseDeMedioDeRecaudo.InternalOffset, interna: true).Should().BeNull();
        ConsignacionEnCuentas.ClaseDeMedio(ClaseDeRecaudador.OwnLoanPayoff, ClaseDeMedioDeRecaudo.Cash, interna: true).Should().NotBeNull("sólo el cruce interno");
        ConsignacionEnCuentas.ClaseDeMedio(ClaseDeRecaudador.Receipts, ClaseDeMedioDeRecaudo.InternalOffset, interna: true).Should().NotBeNull(
            "el camino interno es sólo de la recogida propia");
    }

    // ------------------------------------------------------------------------------------------------------ T077 cuotas --

    [Fact]
    public async Task HU6_por_cuotas_consigna_las_cuotas_de_aportes_pendientes_a_la_fecha_con_la_recogida_propia()
    {
        var (cuenta, leida) = await AportesConDosCuotasAsync();
        leida.Pendiente.Should().Be(100_000m, "diciembre y enero, pendientes al 10 de enero");
        leida.Admitidos(AdmiteCuentas).Should().Equal(PayoffMode.Installments, PayoffMode.Deposit);
        (await Recogida().ValidarAsync(leida, PayoffMode.Installments, null, AdmiteCuentas, Enero10, default)).Should().BeNull();
        var desembolso = Desembolso(Enero10);

        var r = await Recogida().AplicarAsync(desembolso, _a.C.Ana.PublicId, [Evaluada(leida, PayoffMode.Installments, leida.Pendiente)], default);
        _a.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var aplicada = r.Value.Should().ContainSingle().Subject;
        aplicada.Clave.Should().Be($"CAR:{desembolso.PublicId:N}:{cuenta.PublicId:N}");
        aplicada.Comprobante.InstallmentsCovered.Select(q => (q.Period, q.Amount)).Should().Equal(("2026-12", 50_000m), ("2027-01", 50_000m));
        var operacion = await _a.Db.MemberAccountOperations.AsNoTracking().SingleAsync();
        (operacion.CollectorCode, operacion.CollectorClass, operacion.PaymentMeansClass, operacion.ReceiptNumber, operacion.Reason)
            .Should().Be(("CAR", nameof(ClaseDeRecaudador.OwnLoanPayoff), nameof(ClaseDeMedioDeRecaudo.InternalOffset), "DES-000001",
                "Por el desembolso DES-000001"));
        (await _a.ExigiblesAsync(_a.C.Ana)).Should().BeEmpty();
        var hecho = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == AporteRecibidoV1.Type);
        hecho.PayloadJson.Should().Contain("\"collectorClass\":\"OwnLoanPayoff\"");
    }

    [Fact]
    public async Task HU6_un_abono_de_aportes_es_extraordinario_si_la_linea_lo_admite()
    {
        var (_, leida) = await AportesConDosCuotasAsync();
        (await Recogida().ValidarAsync(leida, PayoffMode.Deposit, 200_000m, AdmiteCuentas, Enero10, default)).Should().BeNull();

        var r = await Recogida().AplicarAsync(Desembolso(Enero10), _a.C.Ana.PublicId, [Evaluada(leida, PayoffMode.Deposit, 200_000m)], default);
        _a.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Single().Comprobante.Transactions.Should().ContainSingle()
            .Which.Should().Be(new LineaDeCuentaDto(ConceptoDeCuenta.ContributionExtraordinary, nameof(MemberAccountTransactionKind.Deposit), 200_000m));
        (await _a.ExigiblesAsync(_a.C.Ana)).Should().HaveCount(2, "un abono libre no paga las cuotas");
    }

    [Fact]
    public async Task HU6_2_una_linea_de_aportes_sin_extraordinarios_no_admite_abono_con_el_motivo_de_la_linea()
    {
        var (_, leida) = await AportesConDosCuotasAsync(extraordinarios: false);

        leida.AdmiteAbono.Should().BeFalse();
        leida.Admitidos(AdmiteCuentas).Should().Equal(PayoffMode.Installments);
        var error = await Recogida().ValidarAsync(leida, PayoffMode.Deposit, 200_000m, AdmiteCuentas, Enero10, default);
        error!.Code.Should().Be(ErroresDeRecogida.CodigoModoNoAdmitido);
        error.Message.Should().Contain("no admite aportes extraordinarios").And.Contain(leida.CodigoDeLinea);
        (await Recogida().ValidarAsync(leida, PayoffMode.Installments, null, AdmiteCuentas, Enero10, default)).Should().BeNull("por cuotas sí");
    }

    [Fact]
    public async Task HU6_el_ahorro_a_la_vista_recibe_el_abono_al_saldo()
    {
        var linea = await _a.C.LineaDeCaptacionAsync();
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        var leida = await LeidaAsync(cuenta.PublicId, _a.G.HoyLocal);

        leida.Producto.Should().Be(ProductoDeRecogida.AhorroALaVista);
        leida.Admitidos(AdmiteCuentas).Should().Equal(PayoffMode.Deposit);
        (await Recogida().ValidarAsync(leida, PayoffMode.Installments, null, AdmiteCuentas, _a.G.HoyLocal, default))!.Code
            .Should().Be(ErroresDeRecogida.CodigoModoNoAdmitido, "a la vista no tiene cuotas");
        var r = await Recogida().AplicarAsync(Desembolso(_a.G.HoyLocal), _a.C.Ana.PublicId, [Evaluada(leida, PayoffMode.Deposit, 300_000m)], default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Single().Comprobante.Transactions.Should().ContainSingle().Which.Concept.Should().Be(ConceptoDeCuenta.Principal);
        (await _a.Db.IntegrationMessages.AsNoTracking().AnyAsync(m => m.Type == ConsignacionEnCuentaAplicadaV1.Type)).Should().BeTrue();
    }

    [Fact]
    public async Task HU6_3_el_ahorro_contractual_solo_admite_sus_cuotas_y_HU6_4_el_CDAT_no_se_recoge()
    {
        var contractual = new MemberAccount { Number = "AC-1", Nature = LineNature.Deposit, DepositClass = DepositClass.Contractual, Status = MemberAccountStatus.Active };
        var leida = new CuentaRecogible(contractual, ProductosAdmitidos.DeCuenta(contractual.Nature, contractual.DepositClass), "AHCONT", "AHCONT · Contractual", [],
            true, null, null);

        leida.Producto.Should().Be(ProductoDeRecogida.AhorroContractual);
        leida.Admitidos(AdmiteCuentas).Should().Equal(PayoffMode.Installments);
        var abono = await Recogida().ValidarAsync(leida, PayoffMode.Deposit, 10_000m, AdmiteCuentas, Enero10, default);
        abono!.Code.Should().Be(ErroresDeRecogida.CodigoModoNoAdmitido);
        abono.Message.Should().Contain("ahorro contractual");
        (await Recogida().ValidarAsync(leida, PayoffMode.Installments, null, AdmiteCuentas, Enero10, default))!.Code
            .Should().Be(ErroresDeRecogida.CodigoNoPagable, "sin cuotas pendientes no hay nada que recoger");

        ProductosAdmitidos.DeCuenta(LineNature.Deposit, DepositClass.TermDeposit).Should().BeNull();
        var cdat = leida with { Cuenta = new MemberAccount { Number = "CD-1", Nature = LineNature.Deposit, DepositClass = DepositClass.TermDeposit }, Producto = null };
        (await Recogida().ValidarAsync(cdat, PayoffMode.Deposit, 10_000m, AdmiteCuentas, Enero10, default))!.Code
            .Should().Be(ErroresDeRecogida.CodigoProductoNoAdmitido);
    }

    [Fact]
    public async Task HU6_1_de_fabrica_la_cooperativa_no_recoge_aportes_ni_ahorros()
    {
        var (_, leida) = await AportesConDosCuotasAsync();

        leida.Admitidos(ProductosAdmitidos.PorDefecto).Should().BeEmpty();
        var error = await Recogida().ValidarAsync(leida, PayoffMode.Installments, null, ProductosAdmitidos.PorDefecto, Enero10, default);
        error!.Code.Should().Be(ErroresDeRecogida.CodigoProductoNoAdmitido);
        error.Message.Should().Contain("La cooperativa no recoge aportes");
    }

    // ------------------------------------------------------------------------------------------- T078 candidatos, solicitud --

    /// <summary>La consulta de productos sobre el escenario de cuentas: Ana no tiene créditos (el listado vuelve vacío) y sí cuentas.</summary>
    private GetPayoffCandidatesQueryHandler Candidatos()
    {
        var sender = NSubstitute.Substitute.For<MediatR.ISender>();
        sender.Send(NSubstitute.Arg.Any<Application.Lending.Loans.ListLoansQuery>(), NSubstitute.Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(IngenIA365ERP.Application.Common.Models.Result.Success(
                new IngenIA365ERP.Application.Common.Paging.PagedResult<Application.Lending.Loans.LoanListItemDto>([], 1, 200, 0))));
        return new GetPayoffCandidatesQueryHandler(_a.Db, sender, null!, new Application.Lending.Origination.CapacidadDePago(_a.Db, _a.G.Valores),
            _a.C.Parametros(), _a.G.Alcance, _a.G.Auditoria, _a.G.Reloj, NSubstitute.Substitute.For<Application.Common.Interfaces.Storage.IBlobStore>(),
            cuentas: Recogida());
    }

    private void Admitir(string valor)
    {
        _a.Db.ParameterVersions.Add(new Domain.Entities.Parameters.ParameterVersion
        {
            Module = Domain.Lending.Parameters.ParametrosDeCartera.Modulo, Key = Domain.Lending.Parameters.ParametrosDeCartera.RecogidaProductosAdmitidos,
            Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test", ScopeKind = Domain.Enums.Parameters.ParameterScopeKind.None,
        });
        _a.Db.SaveChanges();
        _a.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task HU6_1_de_fabrica_los_productos_para_recoger_muestran_las_cuentas_sin_poder_marcarse()
    {
        await _a.C.AbiertaAsync(_a.C.Apertura(await _a.C.LineaDeCaptacionAsync(), _a.C.Ana));
        await AportesConDosCuotasAsync();

        var r = await Candidatos().Handle(new GetPayoffCandidatesQuery(_a.C.Ana.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Products.Should().HaveCount(2).And.OnlyContain(p => p.Kind == PayoffProductKind.Account && !p.Eligible
            && p.BlockReason == ErroresDeRecogida.MotivoNoAdmitido && p.BlockMessage!.StartsWith("La cooperativa no recoge"));
        var aportes = r.Value.Products.Single(p => p.AccountProduct == nameof(ProductoDeRecogida.Aportes));
        (aportes.ToPayOff, aportes.PendingInstallments.Count, aportes.AdmittedModes.Count).Should().Be((100_000m, 2, 0));
    }

    [Fact]
    public async Task HU6_con_las_cuentas_admitidas_se_marcan_por_cuotas_y_abono_y_suman_al_neto()
    {
        var vista = await _a.C.AbiertaAsync(_a.C.Apertura(await _a.C.LineaDeCaptacionAsync(), _a.C.Ana));
        var (aportes, _) = await AportesConDosCuotasAsync();
        Admitir("Creditos:Total,Parcial;Aportes:Cuotas,Abono;AhorroALaVista:Abono");

        var r = await Candidatos().Handle(new GetPayoffCandidatesQuery(_a.C.Ana.PublicId, ProjectedDisbursementDate: Enero10,
            Selected: [new PayoffInput(PayoffProductKind.Account, aportes.PublicId, PayoffMode.Installments),
                new PayoffInput(PayoffProductKind.Account, vista.PublicId, PayoffMode.Deposit, 300_000m)],
            NewLoan: new PayoffNewLoanInput(5_000_000m)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Products.Single(p => p.ProductPublicId == aportes.PublicId).AdmittedModes.Should().Equal(PayoffMode.Installments, PayoffMode.Deposit);
        r.Value.Products.Single(p => p.ProductPublicId == vista.PublicId).AdmittedModes.Should().Equal(PayoffMode.Deposit);
        (r.Value.Summary.SelectedTotal, r.Value.Summary.NetToDisburse, r.Value.Summary.SelectedCount, r.Value.Summary.ReleasedInstallments)
            .Should().Be((400_000m, 4_600_000m, 2, 0m), "las cuotas de aportes y el abono salen del giro; no liberan cuotas de crédito");
    }

    [Fact]
    public async Task HU6_la_solicitud_proyecta_la_cuenta_por_cuotas_y_rechaza_un_abono_sin_extraordinarios()
    {
        var (aportes, leida) = await AportesConDosCuotasAsync(extraordinarios: false);
        Admitir("Creditos:Total,Parcial;Aportes:Cuotas,Abono");
        var recogidas = new RecogidasDeLaSolicitud(_a.Db, null!, _a.C.Parametros(), _a.G.Reloj, Recogida());
        var linea = await _a.Db.CreditLines.AsNoTracking().Select(l => l.Id).FirstAsync();

        var porCuotas = await recogidas.PrepararAsync(new Application.Lending.Origination.CreditApplicationInput
        {
            Amount = 5_000_000m, ExpectedDisbursementDate = Enero10,
            Payoffs = [new PayoffInput(PayoffProductKind.Account, aportes.PublicId, PayoffMode.Installments)],
        }, _a.C.Ana.Id, linea, Enero10, default);
        var abono = await recogidas.PrepararAsync(new Application.Lending.Origination.CreditApplicationInput
        {
            Amount = 5_000_000m, Payoffs = [new PayoffInput(PayoffProductKind.Account, aportes.PublicId, PayoffMode.Deposit, 100_000m)],
        }, _a.C.Ana.Id, linea, Enero10, default);

        porCuotas.IsSuccess.Should().BeTrue(porCuotas.IsFailure ? $"{porCuotas.Error.Code}: {porCuotas.Error.Message}" : string.Empty);
        var preparada = porCuotas.Value.Recogidas.Should().ContainSingle().Subject;
        (preparada.Producto.Clase, preparada.Producto.AccountId, preparada.ValorProyectado, preparada.Proyeccion.Desglose.Capital)
            .Should().Be((PayoffProductKind.Account, leida.Cuenta.Id, 100_000m, 100_000m));
        preparada.Proyeccion.Huella.Should().StartWith("sha256:");
        abono.Error.Code.Should().Be(ErroresDeRecogida.CodigoModoNoAdmitido, "HU6-2: la línea no admite extraordinarios");
    }

    // --------------------------------------------------------------------------------------------- T079 anulación, giro --

    [Fact]
    public async Task T079_la_anulacion_reversa_la_consignacion_con_su_clave_de_reverso_y_el_giro_nuevo_la_descuenta_mientras_viva()
    {
        var (cuenta, leida) = await AportesConDosCuotasAsync();
        var desembolso = Desembolso(Enero10);
        (await Recogida().AplicarAsync(desembolso, _a.C.Ana.PublicId, [Evaluada(leida, PayoffMode.Installments, 100_000m)], default)).IsSuccess.Should().BeTrue();
        _a.Db.ChangeTracker.Clear();

        // El giro nuevo (H6) vuelve a descontar lo consignado: la deducción de la cuenta, con el hecho de su consignación.
        var deducciones = await RecogidaEnElDesembolso.VivasAsync(_a.Db, [desembolso.PublicId], default);
        var deduccion = deducciones.Should().ContainSingle().Subject;
        (deduccion.ClaseDeProducto(), deduccion.AccountRef!.AccountPublicId, deduccion.Amount, deduccion.DepositFactType)
            .Should().Be((PayoffDeductionV1.CuentaDelAsociado, cuenta.PublicId, 100_000m, AporteRecibidoV1.Type));

        var vivas = await RecogidaDeCuentas.VivasAsync(_a.Db, [desembolso.PublicId], default);
        var r = await Recogida().ReversarAsync(vivas, "Se anula el crédito nuevo", default);
        _a.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var reverso = await _a.Db.MemberAccountOperations.AsNoTracking().SingleAsync(o => o.Kind == MemberAccountOperationKind.Reversal);
        reverso.IdempotencyKey.Should().Be($"CAR:{desembolso.PublicId:N}:{cuenta.PublicId:N}:reverso");
        (await _a.ExigiblesAsync(_a.C.Ana)).Should().HaveCount(2, "las cuotas vuelven a estar pendientes");
        (await RecogidaDeCuentas.VivasAsync(_a.Db, [desembolso.PublicId], default)).Should().BeEmpty();
        (await RecogidaEnElDesembolso.VivasAsync(_a.Db, [desembolso.PublicId], default)).Should().BeEmpty("reversada, el giro nuevo ya no la descuenta");
        (await _a.HechosAsync()).Should().Equal(AporteRecibidoV1.Type, OperacionDeCuentaReversadaV1.Type);
    }

    // -------------------------------------------------------------------------------------------------- T080 contabilidad --

    [Fact]
    public async Task T080_el_puente_queda_en_cero_entre_el_comprobante_del_desembolso_y_la_consignacion_del_aporte()
    {
        var (cuenta, leida) = await AportesConDosCuotasAsync();
        var aplicadas = await Recogida().AplicarAsync(Desembolso(Enero10), _a.C.Ana.PublicId, [Evaluada(leida, PayoffMode.Installments, 100_000m)], default);
        aplicadas.IsSuccess.Should().BeTrue();
        _a.Db.ChangeTracker.Clear();
        var sucursal = await _a.Db.Branches.AsNoTracking().Where(b => b.Id == leida.Cuenta.BranchId).Select(b => b.PublicId).SingleAsync();

        // El DS con la recogida de la cuenta, como lo arma la emisión (la sucursal de la oficina de la cuenta y el deudor como pagador).
        var recogidas = await _a.Emision().RecogidasDelHechoAsync([RecogidaDeCuentas.ComoDeduccion(aplicadas.Value.Single())], _a.C.Ana.PublicId, default);
        recogidas.Single().BranchPublicId.Should().Be(sucursal);
        var ds = Desglose(Hecho(CreditoDesembolsadoV1.Type, sucursal, new CreditoDesembolsadoV1
        {
            Loan = new LoanRefV1 { LoanPublicId = Guid.NewGuid(), LoanNumber = "CR-NUEVO" },
            DebtorPersonPublicId = _a.C.Ana.PublicId,
            ValueDate = Enero10,
            Dimensions = new LoanFactDimensionsV1
            {
                CreditLineCode = "CONLIB", ModalityCode = "CONSUMO", RiskCategory = "A", RiskSubcategory = "None", BranchPublicId = sucursal,
                ImpairmentModel = "ArrearsAnnex1", RiskSegment = "CONSUMO", ValueUnit = "Pesos",
            },
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = 1_000_000m, ReasonCode = nameof(DisbursementForm.Transfer) }],
            Tranche = new TrancheV1 { Number = 1, IsLast = true },
            DisbursementForm = nameof(DisbursementForm.Transfer),
            PayeePersonPublicId = _a.C.Ana.PublicId,
            GrossAmount = 1_000_000m,
            PayoffDeductions = recogidas,
            NetToDisburse = 900_000m,
        }));
        var aporte = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == AporteRecibidoV1.Type);
        var contenido = ConstructorDeLineasDeCartera.ContenidoTipado(aporte.Type, aporte.PayloadJson);
        var consignacion = Desglose(new HechoDeCartera(MensajesEntrantes.Sobre(aporte, contenido), contenido, aporte.OriginDocumentClass));

        var puenteDelDs = ds.Where(EsElPuente).ToList();
        puenteDelDs.Should().ContainSingle().Which.Should().Match<ConstructorDeLineasDeCartera.Partida>(p =>
            !p.DebitoSiPositivo && p.Importe == 100_000m && p.Cuenta == cuenta.Number && p.Credito == null && p.Tercero == _a.C.Ana.PublicId
            && p.Valores.BranchPublicId == sucursal && p.HechoDelCruce == AporteRecibidoV1.Type);
        ds.Where(p => p.Rol == R.DesembolsoPorGirar).Sum(p => p.Importe).Should().Be(900_000m, "«desembolso por girar», sólo el neto");
        var puente = puenteDelDs.Concat(consignacion.Where(EsElPuente))
            .GroupBy(p => (p.Cuenta, p.Tercero ?? p.TerceroDelSobre, p.Valores.BranchPublicId))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.DebitoSiPositivo ? p.Importe : -p.Importe));
        puente.Should().ContainSingle().Which.Value.Should().Be(0m, "la consignación debita lo que el desembolso acredita, con el mismo tercero, cruce y sucursal");
    }

    private static bool EsElPuente(ConstructorDeLineasDeCartera.Partida p) =>
        p.Rol == R.RecaudoPorAplicar && p.Valores.CollectorClass == ConstructorDeLineasDeCartera.ClaseDeLaRecogidaPropia;

    private static HechoDeCartera Hecho(string tipo, Guid sucursal, object contenido) => new(new IntegrationEnvelopeV1
    {
        MessageId = Guid.NewGuid(), Type = tipo, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
        OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = sucursal,
    }, contenido, EmisionDeCarteraClases.Desembolso);

    private static IReadOnlyList<ConstructorDeLineasDeCartera.Partida> Desglose(HechoDeCartera hecho)
    {
        var d = ConstructorDeLineasDeCartera.Desglosar([hecho]);
        d.Fallos.Should().BeEmpty($"el hecho {hecho.Tipo} cuadra: {string.Join("; ", d.Fallos.Select(f => f.Error.Message))}");
        return d.Partidas;
    }

    /// <summary>La clase del documento de origen del desembolso.</summary>
    private static class EmisionDeCarteraClases
    {
        public const string Desembolso = Application.Lending.Integration.EmisionDeCartera.ClaseDesembolso;
    }
}
