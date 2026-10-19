using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;
using static IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones.EscenarioDeCaptaciones;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones;

/// <summary>
/// Feature 014, US8 (T143, T148; contracts/contabilidad.md §1 a §5 y §9 de la 014; research C20, E106 a E110): el constructor de líneas de los hechos de
/// captaciones y aportes. Cada concepto de la cuenta va a su rol por su lado natural y lo que no cuadra va a la contrapartida del hecho; la marca, la clase y
/// el plazo de emisión eligen la cuenta; el número de la cuenta es el cruce <c>CTA</c>; el cruce al retiro y el pago del crédito van en un comprobante
/// <c>RA</c> donde el puente se anula (D45); el reverso es el espejo con la contrapartida del original y toma el comprobante del original.
/// </summary>
public class ConstructorDeCaptacionesTests
{
    private readonly EscenarioDeCaptaciones S = new();

    private int Persona(string alias) => S.E.Personas[alias].Id;

    private static IReadOnlyList<(string Cuenta, decimal Debito, decimal Credito, int? Tercero, string? Cruce)> Lineas(ConstruccionDeCartera c) =>
        c.Request!.Lines.Select(l => (l.AccountCode, l.Debit, l.Credit, l.PersonId,
            l.CrossDocumentType is null ? null : $"{l.CrossDocumentType} {l.CrossDocumentNumber}")).ToList();

    private static void SinFallos(ConstruccionDeCartera c) =>
        c.Fallos.Select(f => $"{f.FactType} {f.Role}: {f.Error.Code} {f.Error.Message}").Should().BeEmpty();

    // --------------------------------------------------------------------------------------------- consignaciones --

    [Fact]
    public async Task La_consignacion_por_caja_va_al_capital_con_el_titular_y_el_cruce_CTA_contra_el_puente_del_recaudador()
    {
        var unidad = S.Unidad("AccountDeposit", "CON-000001", new ConsignacionEnCuentaAplicadaV1
        {
            Account = Cuenta("AHVISTA-000001"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), Collector = Recaudador("Receipts", S.Pagador),
            Amounts = [Valor("Principal", 100_000m)],
        });

        var c = await S.ConstruirAsync(unidad);

        SinFallos(c);
        c.Tipo!.VoucherTypeCode.Should().Be("MA");
        c.Request!.Description.Should().Be("Consignación CON-000001 · cuenta AHVISTA-000001");
        Lineas(c).Should().BeEquivalentTo(new[]
        {
            (S.PuenteCaja.Code, 100_000m, 0m, (int?)Persona("pagador"), (string?)null),
            (S.Ahorro.Code, 0m, 100_000m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000001"),
        });
        c.Mapa.Single(m => m.Rol == R.CapitalDeCaptacion).CuentasDeCaptacion.Should().Equal("AHVISTA-000001");
        (await S.E.D.Poster.ValidarAsync(c.Request, default)).Errores.Should().BeEmpty("el contrato de la 009 lo acepta");
    }

    [Fact]
    public async Task El_aporte_que_recauda_Nomina_va_en_DN_una_linea_por_clase_de_aporte()
    {
        var unidad = S.Unidad("AccountDeposit", "CON-000002", new AporteRecibidoV1
        {
            Account = Cuenta("APORTES-000001"), HolderPersonPublicId = S.Titular, Dimensions = S.Aportes(), Collector = Recaudador("Payroll", S.Pagador),
            Amounts = [Valor("ContributionOrdinary", 80_000m, claseDeAporte: "Ordinary"), Valor("ContributionMinimum", 20_000m, claseDeAporte: "Minimum")],
        });

        var c = await S.ConstruirAsync(unidad);

        SinFallos(c);
        c.Tipo!.VoucherTypeCode.Should().Be("DN", "lo que recauda Nómina va a su tipo (contabilidad.md §5)");
        c.Request!.Lines.Should().Contain(l => l.AccountCode == S.PuenteNomina.Code && l.Debit == 100_000m);
        c.Request.Lines.Should().Contain(l => l.AccountCode == S.AportesOrdinarios.Code && l.Credit == 80_000m && l.CrossDocumentType == "CTA");
        c.Request.Lines.Should().Contain(l => l.AccountCode == S.AportesMinimos.Code && l.Credit == 20_000m && l.CrossDocumentType == null);
        c.Request.Lines.Should().HaveCount(3);
    }

    // ---------------------------------------------------------------------------------------------------- retiros --

    [Fact]
    public async Task El_retiro_por_giro_con_GMF_sale_en_un_MA_contra_la_devolucion_por_pagar_y_el_GMF_de_la_clase_que_lo_genero()
    {
        var cuenta = Cuenta("AHVISTA-000003");
        var unidad = S.Unidad("AccountWithdrawal", "RET-000001",
            new RetiroDeCuentaAplicadoV1
            {
                Account = cuenta, HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), DisbursementOrderPublicId = Guid.NewGuid(),
                Amounts = [Valor("Principal", -400_000m, "DepositWithdrawal")],
            },
            new GmfRetenidoV1
            {
                Account = cuenta, HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), Amounts = [Valor("Gmf", 1_600m)],
                Gmf = new GmfDetailV1 { GeneratingDepositClass = "Demand", Base = 400_000m, TaxedBase = 400_000m, Rate = 0.004m },
            });

        var c = await S.ConstruirAsync(unidad);

        SinFallos(c);
        c.Tipo!.VoucherTypeCode.Should().Be("MA");
        Lineas(c).Should().BeEquivalentTo(new[]
        {
            (S.Ahorro.Code, 401_600m, 0m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000003"),
            (S.DevolucionDeDepositos.Code, 0m, 400_000m, (int?)Persona("ana"), (string?)null),
            (S.Gmf.Code, 0m, 1_600m, (int?)null, (string?)null),
        }, "contabilidad.md §9: retiro de 400.000 con GMF de 1.600; el capital de las dos partidas se suma en una línea");
    }

    [Fact]
    public async Task Un_retiro_sin_recaudador_ni_orden_no_dice_a_donde_va_y_lo_informa_como_defecto_del_emisor()
    {
        var unidad = S.Unidad("AccountWithdrawal", "RET-000002", new RetiroDeCuentaAplicadoV1
        {
            Account = Cuenta("AHVISTA-000004"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), Amounts = [Valor("Principal", -10m)],
        });

        var c = await S.ConstruirAsync(unidad);

        c.EsValida.Should().BeFalse();
        c.Fallos[0].Error.Code.Should().Be("Accounting.LendingFact.CounterpartUnknown");
    }

    [Fact]
    public async Task La_marca_y_la_inactividad_eligen_la_cuenta_del_capital()
    {
        var exenta = await S.ConstruirAsync(S.Unidad("AccountDeposit", "CON-000010", new ConsignacionEnCuentaAplicadaV1
        {
            Account = Cuenta("AHVISTA-000010"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista("GmfExempt"),
            Collector = Recaudador("Receipts", S.Pagador), Amounts = [Valor("Principal", 50m)],
        }));
        var inactiva = await S.ConstruirAsync(S.Unidad("AccountDeposit", "CON-000011", new ConsignacionEnCuentaAplicadaV1
        {
            Account = Cuenta("AHVISTA-000011"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(inactiva: true),
            Collector = Recaudador("Receipts", S.Pagador), Amounts = [Valor("Principal", 50m)],
        }));

        SinFallos(exenta);
        SinFallos(inactiva);
        exenta.Request!.Lines.Should().Contain(l => l.AccountCode == S.AhorroExenta.Code && l.Credit == 50m, "la marca GmfExempt es exigida (C20)");
        inactiva.Request!.Lines.Should().Contain(l => l.AccountCode == S.AhorroInactiva.Code && l.Credit == 50m, "la inactividad cambia el rol, no la marca");
        inactiva.Mapa.Should().Contain(m => m.Rol == R.CapitalDeCaptacionInactiva);
    }

    [Fact]
    public async Task Sin_regla_para_la_combinacion_falla_con_la_combinacion_que_falta()
    {
        var c = await S.ConstruirAsync(S.Unidad("AccountDeposit", "CON-000012", new ConsignacionEnCuentaAplicadaV1
        {
            Account = Cuenta("PERM-000001"), HolderPersonPublicId = S.Titular, Collector = Recaudador("Receipts", S.Pagador),
            Dimensions = S.AlaVista() with { DepositClass = "Permanent", CreditLineCode = "PERM" }, Amounts = [Valor("Principal", 50m)],
        }));

        c.EsValida.Should().BeFalse();
        var fallo = c.Fallos.Single();
        fallo.Error.Code.Should().Be("Accounting.LendingRule.Missing");
        fallo.Role.Should().Be(R.CapitalDeCaptacion);
        fallo.Combination!.Should().Contain("depositClass", "Permanent").And.Contain("accountFlag", "Standard");
    }

    // ------------------------------------------------------------------------------------------------ intereses --

    [Fact]
    public async Task La_causacion_del_dia_lleva_el_gasto_contra_el_interes_por_pagar_de_cada_cuenta_en_un_CC()
    {
        var unidad = S.Unidad("AccountingFactPart", "CCA-20261114-0001", new InteresDeCaptacionCausadoV1
        {
            FactDate = Dia, BatchNumber = 1, PartNumber = 1,
            Lines =
            [
                new DepositFactLineV1 { Account = Cuenta("AHVISTA-000020"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), Amounts = [Valor("InterestPayable", 229.47m)] },
                new DepositFactLineV1 { Account = Cuenta("AHVISTA-000021"), HolderPersonPublicId = S.Otro, Dimensions = S.AlaVista(), Amounts = [Valor("InterestPayable", 12.03m)] },
            ],
        });

        var c = await S.ConstruirAsync(unidad);

        SinFallos(c);
        c.Tipo!.VoucherTypeCode.Should().Be("CC");
        Lineas(c).Should().BeEquivalentTo(new[]
        {
            (S.Gasto.Code, 241.50m, 0m, (int?)null, (string?)null),
            (S.InteresPorPagar.Code, 0m, 229.47m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000020"),
            (S.InteresPorPagar.Code, 0m, 12.03m, (int?)Persona("luis"), (string?)"CTA AHVISTA-000021"),
        }, "el gasto sin tercero se suma; el interés por pagar exige tercero y cruce: una línea por cuenta");
    }

    [Fact]
    public async Task La_liquidacion_con_retencion_cuadra_sola_en_un_CC()
    {
        var cuenta = Cuenta("AHVISTA-000030");
        var unidad = S.Unidad("InterestSettlement", "LQI-000001",
            new InteresDeCaptacionLiquidadoV1
            {
                FactDate = Dia, Lines = [new DepositFactLineV1 { Account = cuenta, HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(),
                    Amounts = [Valor("InterestPayable", -6_884m), Valor("Principal", 6_884m)] }],
            },
            new RetencionPracticadaV1
            {
                FactDate = Dia, Lines = [new DepositFactLineV1 { Account = cuenta, HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(),
                    Amounts = [Valor("Principal", -482m), Valor("Withholding", 482m)] }],
            });

        var c = await S.ConstruirAsync(unidad);

        SinFallos(c);
        c.Tipo!.VoucherTypeCode.Should().Be("CC");
        Lineas(c).Should().BeEquivalentTo(new[]
        {
            (S.InteresPorPagar.Code, 6_884m, 0m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000030"),
            (S.Ahorro.Code, 0m, 6_884m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000030"),
            (S.Ahorro.Code, 482m, 0m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000030"),
            (S.Retencion.Code, 0m, 482m, (int?)null, (string?)null),
        });
    }

    [Fact]
    public async Task El_capital_del_CDAT_exige_el_plazo_de_emision()
    {
        var unidad = S.Unidad("TermDepositOpening", "CDT-000001", new CdatConstituidoV1
        {
            Account = Cuenta("CDAT-000001"), HolderPersonPublicId = S.Titular, Dimensions = S.Cdat(), Amounts = [Valor("Principal", 10_000_000m)],
            Funding = new TermDepositFundingV1 { Kind = "Collector", Collector = Recaudador("Receipts", S.Titular) },
        });

        var c = await S.ConstruirAsync(unidad);

        SinFallos(c);
        c.Tipo!.VoucherTypeCode.Should().Be("TD");
        c.Request!.Lines.Should().Contain(l => l.AccountCode == S.Cdat612.Code && l.Credit == 10_000_000m);

        var otroPlazo = await S.ConstruirAsync(S.Unidad("TermDepositOpening", "CDT-000002", new CdatConstituidoV1
        {
            Account = Cuenta("CDAT-000002"), HolderPersonPublicId = S.Titular, Dimensions = S.Cdat("EighteenOrMore"), Amounts = [Valor("Principal", 1m)],
            Funding = new TermDepositFundingV1 { Kind = "Collector", Collector = Recaudador("Receipts", S.Titular) },
        }));
        otroPlazo.Fallos.Should().ContainSingle(f => f.Error.Code == "Accounting.LendingRule.Missing" && f.Combination!["issueTermBand"] == "EighteenOrMore");
    }

    [Fact]
    public async Task US6_un_CDAT_fondeado_desde_una_cuenta_de_ahorro_cuadra_contra_esa_cuenta()
    {
        // Pendiente de la fase 9 (research E107): el hecho trae la cuenta origen con sus dimensiones y su cambio de saldo; ya no es CounterpartUnknown.
        var unidad = S.Unidad("TermDepositConstitution", "CDC-000001", new CdatConstituidoV1
        {
            Account = Cuenta("CDAT-000003"), HolderPersonPublicId = S.Titular, Dimensions = S.Cdat(), Amounts = [Valor("Principal", 10_000_000m)],
            Funding = new TermDepositFundingV1
            {
                Kind = "TransferFromAccount", SourceAccount = Cuenta("AHVISTA-000031"), SourceHolderPersonPublicId = S.Titular, SourceDimensions = S.AlaVista(),
                SourceAmounts = [Valor("Principal", -10_000_000m)],
            },
        });

        var c = await S.ConstruirAsync(unidad);

        SinFallos(c);
        c.Request!.Lines.Should().Contain(l => l.AccountCode == S.Cdat612.Code && l.Credit == 10_000_000m);
        c.Request.Lines.Should().Contain(l => l.AccountCode == S.Ahorro.Code && l.Debit == 10_000_000m);
        c.Request.Lines.Sum(l => l.Debit).Should().Be(c.Request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task US6_un_CDAT_fondeado_desde_otra_cuenta_sin_sus_dimensiones_sigue_sin_contrapartida()
    {
        var c = await S.ConstruirAsync(S.Unidad("TermDepositConstitution", "CDC-000002", new CdatConstituidoV1
        {
            Account = Cuenta("CDAT-000004"), HolderPersonPublicId = S.Titular, Dimensions = S.Cdat(), Amounts = [Valor("Principal", 5m)],
            Funding = new TermDepositFundingV1 { Kind = "TransferFromAccount", SourceAccount = Cuenta("AHVISTA-000032") },
        }));

        c.Fallos.Should().ContainSingle(f => f.Error.Code == "Accounting.LendingFact.CounterpartUnknown");
    }

    [Fact]
    public async Task US6_la_cancelacion_abonada_en_cuenta_cuadra_contra_la_cuenta_destino()
    {
        var c = await S.ConstruirAsync(S.Unidad("TermDepositRedemption", "RDC-000001", new CdatCanceladoV1
        {
            Account = Cuenta("CDAT-000005"), HolderPersonPublicId = S.Titular, Dimensions = S.Cdat(), Amounts = [Valor("Principal", -10_000_000m)],
            Cause = "Instruction",
            Destination = new TermDepositDestinationV1
            {
                Kind = "Account", TargetAccount = Cuenta("AHVISTA-000033"), TargetHolderPersonPublicId = S.Titular, TargetDimensions = S.AlaVista(),
                TargetAmounts = [Valor("Principal", 10_000_000m)],
            },
        }));

        SinFallos(c);
        c.Request!.Lines.Should().Contain(l => l.AccountCode == S.Cdat612.Code && l.Debit == 10_000_000m);
        c.Request.Lines.Should().Contain(l => l.AccountCode == S.Ahorro.Code && l.Credit == 10_000_000m);
    }

    [Fact]
    public async Task US6_una_contrapartida_que_no_cuadra_con_la_otra_cuenta_se_rechaza()
    {
        var c = await S.ConstruirAsync(S.Unidad("TermDepositRedemption", "RDC-000002", new CdatCanceladoV1
        {
            Account = Cuenta("CDAT-000006"), HolderPersonPublicId = S.Titular, Dimensions = S.Cdat(), Amounts = [Valor("Principal", -100m)],
            Cause = "Instruction",
            Destination = new TermDepositDestinationV1
            {
                Kind = "Account", TargetAccount = Cuenta("AHVISTA-000034"), TargetDimensions = S.AlaVista(), TargetAmounts = [Valor("Principal", 90m)],
            },
        }));

        c.Fallos.Should().ContainSingle(f => f.Error.Code == "Accounting.LendingFact.CounterpartUnknown");
    }

    // ----------------------------------------------------------------------------------------- retiro del asociado --

    [Fact]
    public async Task D45_el_cruce_al_retiro_y_el_pago_del_credito_van_en_un_RA_y_el_puente_se_anula()
    {
        var pago = EscenarioContableDeCartera.UnidadDe("02-recogida-de-otro-credito-por-el-contrato").Select(HechoDeCartera.De).Single();
        var cruce = new HechoDeCartera(pago.Sobre with { MessageId = Guid.NewGuid(), Type = CruceAlRetiroAplicadoV1.Type }, new CruceAlRetiroAplicadoV1
        {
            Account = Cuenta("APORTES-000040"), HolderPersonPublicId = S.Titular, Dimensions = S.Aportes(),
            Parts =
            [
                new WithdrawalAccountPartV1 { Account = Cuenta("APORTES-000040"), Dimensions = S.Aportes(),
                    Amounts = [Valor("ContributionOrdinary", -2_000_000m, claseDeAporte: "Ordinary")], OperationNumber = "CRU-1" },
                new WithdrawalAccountPartV1 { Account = Cuenta("AHVISTA-000040"), Dimensions = S.AlaVista(), Amounts = [Valor("Principal", -300_000m)],
                    OperationNumber = "CRU-2" },
            ],
        }, "PaymentApplication");

        var c = await S.ConstruirAsync([pago, cruce]);

        SinFallos(c);
        c.Principal.Tipo.Should().Be(CruceAlRetiroAplicadoV1.Type, "el cruce es el principal y el pago su acompañante (D45)");
        c.Tipo!.VoucherTypeCode.Should().Be("RA");
        var puente = c.Request!.Lines.Where(l => l.AccountCode == S.PuenteInterno.Code).ToList();
        puente.Sum(l => l.Debit).Should().Be(2_300_000m);
        puente.Sum(l => l.Credit).Should().Be(2_300_000m, "el crédito del cruce y el débito del pago al puente OwnLoanPayoff se anulan en el mismo comprobante");
        c.Request.Lines.Should().Contain(l => l.AccountCode == "14410501" && l.Credit == 2_250_000m && l.CrossDocumentType == "PG" && l.CrossDocumentNumber == "000077",
            "la línea del crédito cruza con el pagaré (el mapeo de su propio hecho), aunque el comprobante sea RA");
        c.Request.Lines.Should().Contain(l => l.AccountCode == S.AportesOrdinarios.Code && l.Debit == 2_000_000m && l.CrossDocumentType == "CTA"
            && l.CrossDocumentNumber == "APORTES-000040");
        c.Request.Lines.Should().Contain(l => l.AccountCode == S.Ahorro.Code && l.Debit == 300_000m && l.CrossDocumentNumber == "AHVISTA-000040");
        (await S.E.D.Poster.ValidarAsync(c.Request, default)).Errores.Should().BeEmpty();
    }

    [Fact]
    public async Task D45_el_reverso_del_cruce_y_el_del_pago_pueden_ir_en_un_solo_comprobante()
    {
        // Hoy el emisor los saca en unidades separadas (decisión pendiente del dueño, research E110); el constructor ya admite la unidad conjunta.
        var reversoDelPago = new HechoDeCartera(S.Sobre(PagoReversadoV1.Type, Guid.NewGuid(), "RVP-1", S.Titular), new PagoReversadoV1
        {
            Loan = new LoanRefV1 { LoanNumber = "000077" }, DebtorPersonPublicId = S.Titular,
            Dimensions = new LoanFactDimensionsV1
            {
                CreditLineCode = "LIB01", ModalityCode = "CONSUMO", SubsegmentCode = "ConLibranza", RiskCategory = "A", CollectorClass = "OwnLoanPayoff",
                BranchPublicId = S.Sucursal,
            },
            Collector = new CollectorRefV1 { CollectorCode = "CAR", CollectorClass = "OwnLoanPayoff", PayerPersonPublicId = S.Titular },
            Amounts = [new ConceptAmountV1 { Concept = "Principal", Amount = 300_000m }],
        }, "PaymentReversal");
        var reversoDelCruce = new HechoDeCartera(reversoDelPago.Sobre with { MessageId = Guid.NewGuid(), Type = OperacionDeCuentaReversadaV1.Type },
            new OperacionDeCuentaReversadaV1
            {
                Account = Cuenta("AHVISTA-000041"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), Amounts = [Valor("Principal", 300_000m)],
                Original = new OriginalFactRefV1 { Type = CruceAlRetiroAplicadoV1.Type }, CollectorClass = "OwnLoanPayoff",
                Mirrors = [new DepositFactMirrorV1 { Type = CruceAlRetiroAplicadoV1.Type, Amounts = [Valor("Principal", 300_000m)] }],
            }, "AccountReversal");

        var c = await S.ConstruirAsync([reversoDelCruce, reversoDelPago]);

        SinFallos(c);
        c.Principal.Tipo.Should().Be(OperacionDeCuentaReversadaV1.Type);
        c.Tipo!.VoucherTypeCode.Should().Be("RA", "el reverso toma el comprobante de su original");
        var puente = c.Request!.Lines.Where(l => l.AccountCode == S.PuenteInterno.Code).ToList();
        puente.Sum(l => l.Debit).Should().Be(puente.Sum(l => l.Credit));
        c.Request.Lines.Should().Contain(l => l.AccountCode == S.Ahorro.Code && l.Credit == 300_000m);
        c.Request.Lines.Should().Contain(l => l.AccountCode == "14410501" && l.Debit == 300_000m);
    }

    [Fact]
    public async Task La_devolucion_al_retiro_va_al_puente_de_Tesoreria_por_naturaleza_y_la_perdida_a_la_absorbida()
    {
        var devolucion = await S.ConstruirAsync(S.Unidad("MembershipWithdrawalSettlement", "DEV-1", new DevolucionOrdenadaV1
        {
            Account = Cuenta("APORTES-000050"), HolderPersonPublicId = S.Titular, Dimensions = S.Aportes(), OrderKind = "MembershipRefund",
            PayeePersonPublicId = S.Titular, Amounts = [Valor("ContributionOrdinary", -500_000m, "MembershipRefund", "Ordinary")],
        }));
        var perdida = await S.ConstruirAsync(S.Unidad("MembershipWithdrawalSettlement", "PER-1", new PerdidaAplicadaAlRetiroV1
        {
            Account = Cuenta("APORTES-000050"), HolderPersonPublicId = S.Titular, Dimensions = S.Aportes(),
            Amounts = [Valor("ContributionOrdinary", -30_000m, claseDeAporte: "Ordinary")],
        }));

        SinFallos(devolucion);
        SinFallos(perdida);
        devolucion.Tipo!.VoucherTypeCode.Should().Be("RA");
        Lineas(devolucion).Should().Contain((S.DevolucionDeAportes.Code, 0m, 500_000m, (int?)Persona("ana"), (string?)null));
        Lineas(perdida).Should().Contain((S.Perdidas.Code, 0m, 30_000m, (int?)null, (string?)null));
    }

    // ------------------------------------------------------------------------------------------------- reversos --

    [Fact]
    public async Task El_reverso_de_una_consignacion_es_el_espejo_contra_el_puente_del_recaudador_en_el_comprobante_del_original()
    {
        var c = await S.ConstruirAsync(S.Unidad("AccountReversal", "RVC-1", new OperacionDeCuentaReversadaV1
        {
            Account = Cuenta("AHVISTA-000060"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), CollectorClass = "Receipts",
            Original = new OriginalFactRefV1 { Type = ConsignacionEnCuentaAplicadaV1.Type },
            Mirrors = [new DepositFactMirrorV1 { Type = ConsignacionEnCuentaAplicadaV1.Type, Amounts = [Valor("Principal", -100_000m)] }],
        }));

        SinFallos(c);
        c.Tipo!.VoucherTypeCode.Should().Be("MA");
        Lineas(c).Should().BeEquivalentTo(new[]
        {
            (S.Ahorro.Code, 100_000m, 0m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000060"),
            (S.PuenteCaja.Code, 0m, 100_000m, (int?)Persona("ana"), (string?)null),
        });
    }

    [Fact]
    public async Task El_reverso_de_un_traslado_espeja_las_dos_cuentas_y_cuadra_sin_contrapartida()
    {
        var c = await S.ConstruirAsync(S.Unidad("AccountReversal", "RVC-2", new OperacionDeCuentaReversadaV1
        {
            Account = Cuenta("AHVISTA-000061"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(),
            Original = new OriginalFactRefV1 { Type = TrasladoEntreCuentasAplicadoV1.Type },
            Mirrors =
            [
                new DepositFactMirrorV1 { Type = TrasladoEntreCuentasAplicadoV1.Type, Amounts = [Valor("Principal", 70_000m)] },
                new DepositFactMirrorV1
                {
                    Type = TrasladoEntreCuentasAplicadoV1.Type, Account = Cuenta("APORTES-000061"), HolderPersonPublicId = S.Titular, Dimensions = S.Aportes(),
                    Amounts = [Valor("ContributionOrdinary", -70_000m, claseDeAporte: "Ordinary")],
                },
            ],
        }));

        SinFallos(c);
        Lineas(c).Should().BeEquivalentTo(new[]
        {
            (S.Ahorro.Code, 0m, 70_000m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000061"),
            (S.AportesOrdinarios.Code, 70_000m, 0m, (int?)Persona("ana"), (string?)"CTA APORTES-000061"),
        });
    }

    // ------------------------------------------------------------------------------------------- cargos y otros --

    [Fact]
    public async Task El_cargo_cobrado_y_el_pendiente_van_al_ingreso_por_su_clase_y_la_consignacion_que_cobra_el_pendiente_cuadra_sola()
    {
        var cobrado = await S.ConstruirAsync(S.Unidad("AccountCharge", "CGO-1", new CargoDeCuentaCausadoV1
        {
            Account = Cuenta("AHVISTA-000070"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), Charge = new AccountChargeRefV1 { Kind = "MaintenanceFee" },
            Amounts = [Valor("Principal", -5_000m, "MaintenanceFee")],
        }));
        var pendiente = await S.ConstruirAsync(S.Unidad("AccountCharge", "CGO-2", new CargoDeCuentaCausadoV1
        {
            Account = Cuenta("AHVISTA-000071"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(inactiva: true),
            Charge = new AccountChargeRefV1 { Kind = "Inactivity" }, Amounts = [Valor("ChargeReceivable", 3_000m, "Inactivity")],
        }));
        var cuenta = Cuenta("AHVISTA-000071");
        var consignacion = await S.ConstruirAsync(S.Unidad("AccountDeposit", "CON-70",
            new ConsignacionEnCuentaAplicadaV1
            {
                Account = cuenta, HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), Collector = Recaudador("Receipts", S.Pagador),
                Amounts = [Valor("Principal", 10_000m)],
            },
            new CargoPendienteCobradoV1
            {
                Account = cuenta, HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(),
                Amounts = [Valor("ChargeReceivable", -3_000m), Valor("Principal", -3_000m)],
            }));

        SinFallos(cobrado);
        SinFallos(pendiente);
        SinFallos(consignacion);
        Lineas(cobrado).Should().Contain((S.IngresoPorCargos.Code, 0m, 5_000m, (int?)null, (string?)null));
        Lineas(pendiente).Should().Contain((S.CargoPorCobrar.Code, 3_000m, 0m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000071"));
        consignacion.Request!.Lines.Should().Contain(l => l.AccountCode == S.CargoPorCobrar.Code && l.Credit == 3_000m);
        consignacion.Request.Lines.Where(l => l.AccountCode == S.Ahorro.Code).Sum(l => l.Credit - l.Debit).Should().Be(7_000m);
    }

    [Fact]
    public async Task La_reclasificacion_mueve_el_saldo_al_rol_de_la_inactiva_y_sin_cambio_de_cuenta_no_deja_lineas()
    {
        var aInactiva = await S.ConstruirAsync(S.Unidad("AccountingFactPart", "RCC-1", new CaptacionReclasificadaV1
        {
            Account = Cuenta("AHVISTA-000080"), HolderPersonPublicId = S.Titular, Cause = "Inactivity", PreviousDimensions = S.AlaVista(),
            Dimensions = S.AlaVista(inactiva: true), Amounts = [Valor("Principal", 900m)],
        }));
        var embargo = await S.ConstruirAsync(S.Unidad("AccountingFactPart", "RCC-2", new CaptacionReclasificadaV1
        {
            Account = Cuenta("AHVISTA-000081"), HolderPersonPublicId = S.Titular, Cause = "Garnishment", PreviousDimensions = S.AlaVista(),
            Dimensions = S.AlaVista("Garnished"), Amounts = [Valor("Principal", 900m)],
        }));

        SinFallos(aInactiva);
        aInactiva.Tipo!.VoucherTypeCode.Should().Be("CN");
        Lineas(aInactiva).Should().BeEquivalentTo(new[]
        {
            (S.Ahorro.Code, 900m, 0m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000080"),
            (S.AhorroInactiva.Code, 0m, 900m, (int?)Persona("ana"), (string?)"CTA AHVISTA-000080"),
        });
        SinFallos(embargo);
        embargo.SinComprobante.Should().Be(LendingPosting.SinComprobanteMismasCuentas, "la embargada va a la misma cuenta que la estándar (C21)");
    }
}
