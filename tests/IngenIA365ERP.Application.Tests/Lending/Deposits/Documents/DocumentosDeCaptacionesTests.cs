using System.Security.Cryptography;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Documents;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Documents;

/// <summary>
/// Feature 014, US13 (T196, T197; contracts/documentos.md §2.1 a §2.5, §4; FR-068, FR-101 a FR-105; SC-016): el extracto de una cuenta con sus
/// movimientos y su saldo final, el certificado de saldo con las restricciones a pedido, el certificado tributario de dos cotitulares al 60/40 que cuadra al
/// centavo con la cuenta, la anulación por el reverso de una operación de cuenta según la clase, y el título del CDAT —nace al constituir con el sufijo
/// de la versión, la versión siguiente lo reemplaza y la redención lo liquida—. Sobre <see cref="AportesDePrueba"/> (hoy 2026-12-01) con la entrega K9. (nuevo)
/// </summary>
public sealed class DocumentosDeCaptacionesTests : IDisposable
{
    private const decimal Uvt = 49_799m;
    private readonly AportesDePrueba _a = new();
    private readonly IDocumentoDeCarteraRenderer _renderizador = Substitute.For<IDocumentoDeCarteraRenderer>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly List<DocumentoDeCarteraParaImprimir> _pintados = [];

    public DocumentosDeCaptacionesTests()
    {
        _a.C.Entrega = EntregaDeCartera.K9;
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, Uvt, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
        _renderizador.Render(Arg.Any<DocumentoDeCarteraParaImprimir>()).Returns(ci =>
        {
            var d = ci.Arg<DocumentoDeCarteraParaImprimir>();
            _pintados.Add(d);
            return System.Text.Encoding.UTF8.GetBytes($"%PDF-1.7 {d.Encabezado.Numero} {d.Encabezado.CodigoDeVerificacion}");
        });
        _sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(async ci =>
        {
            var s = ci.Arg<UploadAttachmentCommand>();
            var adjunto = new Attachment
            {
                TenantId = 1, OwnerEntityType = s.OwnerEntityType, OwnerEntityPublicId = s.OwnerEntityPublicId, FileName = s.FileName,
                ContentType = s.ContentType, SizeBytes = s.Content.LongLength, Sha256Hex = Convert.ToHexString(SHA256.HashData(s.Content)).ToLowerInvariant(),
                StoragePath = $"1/2026/12/{Guid.NewGuid():N}.bin", Format = FormatoDeAdjunto.Direct, Status = EstadoDeAdjunto.Available, CreatedBy = "test",
            };
            _a.Db.Attachments.Add(adjunto);
            await _a.Db.SaveChangesAsync();
            return Result.Success(adjunto.PublicId);
        });
        _a.Cdat = Constitucion;
    }

    public void Dispose() => _a.Dispose();

    private Sessions.GobiernoDePrueba G => _a.G;

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private DocumentosDeCaptaciones Captaciones() => new(_a.Db, _a.Movimientos(), _a.C.Parametros());

    private ConstructorDeDocumentosDeCartera Constructor() => new(_a.Db, new CargadorDeReconstruccion(_a.Db, Substitute.For<ISeriesRegulatorias>()), G.Alcance,
        G.Auditoria, _a.C.Parametros(), new FirmezaDelPago(_a.C.Parametros(), _a.Calendario), G.Reloj, captaciones: Captaciones());

    private EmisionDeDocumentosDeCartera Emision() => new(_a.Db, Constructor(), null!, new GeneradorDeCodigosDeVerificacion(), _a.Numerador(), _a.C.Cerrojo,
        _renderizador, _sender, Substitute.For<IBlobStore>(), G.Reloj);

    private TitulosDeCdat Titulos() => new(_a.Db, Emision(), Constructor(), G.Reloj);

    private LectorDeCdat Lector() => new(_a.Db, _a.Calendario, _a.LectorDeInteres());

    private ConstitucionDeCdat Constitucion() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Movimientos(), _a.Retiro(), Lector(),
        _a.C.Cerrojo, G.Actor, G.Reloj, Titulos());

    private NegociacionDeTasa Negociacion() => new(_a.Db, new MatrizVigenteDeAtribuciones(_a.Db), new AtribucionDelDecisor(_a.Db, G.Permisos), G.Reloj);

    private CotizacionDeCdat Cotizacion() => new(_a.Db, Lector(), Negociacion(), new ReglasDeRetencion(_a.C.Parametros(), G.Valores));

    private AperturaDeCuentas Apertura() => new(_a.Db, _a.C.Contexto, _a.C.Titularidad(), _a.C.Parametros(), _a.C.Listas, G.Intervenciones(),
        _a.RegistroDeCuentas(), _a.Numerador(), _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);

    private async Task FirmanteAsync()
    {
        foreach (var (clave, valor) in new[]
                 {
                     (ParametrosDeCartera.DocumentosFirmanteNombre, "María Gerente"), (ParametrosDeCartera.DocumentosFirmanteCargo, "Gerente general"),
                 })
            _a.Db.ParameterVersions.Add(new ParameterVersion
            {
                Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
                Reason = "prueba", CreatedAt = G.UtcNow.AddDays(-10),
            });
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
    }

    private async Task<(MemberAccountDto Dto, MemberAccount Cuenta)> AhorroAsync(params decimal[] consignaciones)
    {
        var linea = await _a.C.LineaDeCaptacionAsync("AHVISTA", CuentasDePrueba.ALaVista());
        var dto = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        foreach (var valor in consignaciones)
            await _a.ConsignadaAsync(_a.Consignacion(dto, valor, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer));
        var cuenta = await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.PublicId == dto.PublicId);
        return (dto, cuenta);
    }

    private PedidoDeDocumentoDeCartera Pedido(IssuedDocumentKind clase, Person titular, MemberAccount? cuenta = null) =>
        new(clase, titular, null, null, DateTime.SpecifyKind(G.UtcNow.AddYears(5), DateTimeKind.Utc), G.HoyLocal) { Cuenta = cuenta };

    private static DocumentoArmado Ok(Result<DocumentoArmado> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    // ---------------------------------------------------------------------------------------------------------- extracto --

    [Fact]
    public async Task El_extracto_lleva_los_movimientos_por_fecha_valor_el_saldo_final_y_el_disponible_y_cubre_la_cuenta()
    {
        var (_, cuenta) = await AhorroAsync(300_000m, 200_000m);

        var armado = Ok(await Captaciones().ArmarAsync(Pedido(IssuedDocumentKind.AccountStatement, _a.C.Ana, cuenta) with
        {
            Periodo = new PeriodoDeDocumento(G.HoyLocal.AddDays(-30), G.HoyLocal),
        }, default));

        var cuerpo = armado.Cuerpo.Should().BeOfType<CuerpoDeExtractoDeCuenta>().Subject;
        cuerpo.SaldoInicial.Should().Be(0m);
        cuerpo.Movimientos.Should().HaveCount(2).And.OnlyContain(m => m.Tipo == "Consignación");
        cuerpo.SaldoFinal.Should().Be(500_000m);
        cuerpo.Disponible.Disponible.Should().Be(500_000m);
        cuerpo.Titulares.Should().ContainSingle().Which.Papel.Should().Be("Titular");
        cuerpo.Retencion!.SinRetencion.Should().Be("Sin liquidaciones de intereses en el período.");
        armado.Cuentas.Should().ContainSingle().Which.MemberAccountId.Should().Be(cuenta.Id);
        armado.Leyendas.Should().Contain(DocumentosDeCaptaciones.LeyendaDelExtracto);
    }

    [Fact]
    public async Task El_certificado_de_saldo_es_a_la_fecha_y_lista_las_restricciones_sólo_si_se_piden()
    {
        var (_, cuenta) = await AhorroAsync(750_000m);

        var sin = Ok(await Captaciones().ArmarAsync(Pedido(IssuedDocumentKind.AccountBalanceCertificate, _a.C.Ana, cuenta) with { Corte = G.HoyLocal }, default));
        var con = Ok(await Captaciones().ArmarAsync(Pedido(IssuedDocumentKind.AccountBalanceCertificate, _a.C.Ana, cuenta) with
        {
            Corte = G.HoyLocal, IncluyeRestricciones = true,
        }, default));

        var cuerpo = sin.Cuerpo.Should().BeOfType<CuerpoDeCertificadoDeSaldoDeCuenta>().Subject;
        cuerpo.SaldoDelTitular.Should().Be(750_000m);
        cuerpo.Restricciones.Should().BeNull();
        ((CuerpoDeCertificadoDeSaldoDeCuenta)con.Cuerpo).Restricciones.Should().BeEmpty();
        sin.Leyendas[0].Should().StartWith("Certifica el saldo registrado al 01/12/2026");
        sin.SettlementSha256.Should().HaveLength(64);
    }

    // --------------------------------------------------------------------------------------------- certificado tributario --

    [Fact]
    public async Task El_certificado_tributario_de_dos_cotitulares_al_60_40_cuadra_al_centavo_con_la_cuenta_SC016()
    {
        var (_, cuenta) = await AhorroAsync(1_000_000m);
        _a.Db.MemberAccountParties.Add(new MemberAccountParty
        {
            MemberAccountId = cuenta.Id, PersonId = _a.C.Beto.Id, Role = MemberAccountPartyRole.CoHolder, Share = 0.40m, ValidFrom = cuenta.OpenedOn,
            CreatedBy = "test",
        });
        var operacion = await _a.Db.MemberAccountOperations.AsNoTracking().Where(o => o.MemberAccountId == cuenta.Id).OrderBy(o => o.Id).FirstAsync();
        foreach (var (persona, bruto, retenido) in new[] { (_a.C.Ana.Id, 6_000.01m, 420.00m), (_a.C.Beto.Id, 4_000.00m, 280.00m) })
            _a.Db.WithholdingAccumulations.Add(new WithholdingAccumulation
            {
                PersonId = persona, TaxYear = 2026, MemberAccountId = cuenta.Id, DepositClass = DepositClass.Demand, OperationId = operacion.Id,
                Share = persona == _a.C.Beto.Id ? 0.40m : 0.60m, GrossInterest = bruto, WithholdingBase = bruto, WithholdingAmount = retenido,
                CreatedBy = "test",
            });
        _a.Db.GmfMonthlyUsages.Add(new GmfMonthlyUsage
        {
            PersonId = _a.C.Ana.Id, Year = 2026, Month = 12, MemberAccountId = cuenta.Id, OperationId = operacion.Id, TaxableEventCode = "Withdrawal",
            DisposedAmount = 1_000_000m, TaxableBase = 1_000_000m, GmfAmount = 4_000.01m, Policy = GmfExemptionPolicy.SingleMarkedAccount, Rate = 0.004m,
            CreatedBy = "test",
        });
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();

        var deAna = (CuerpoDeCertificadoTributarioDeCaptaciones)Ok(await Captaciones().ArmarAsync(Pedido(IssuedDocumentKind.DepositTaxCertificate, _a.C.Ana) with
        {
            Año = 2026,
        }, default)).Cuerpo;
        var armadoBeto = Ok(await Captaciones().ArmarAsync(Pedido(IssuedDocumentKind.DepositTaxCertificate, _a.C.Beto) with { Año = 2026 }, default));
        var deBeto = (CuerpoDeCertificadoTributarioDeCaptaciones)armadoBeto.Cuerpo;

        (deAna.TotalSaldo + deBeto.TotalSaldo).Should().Be(1_000_000m);
        deBeto.TotalSaldo.Should().Be(400_000m);
        (deAna.TotalGmf + deBeto.TotalGmf).Should().Be(4_000.01m, "el GMF de la cuenta se reparte por la proporción y suma exacto");
        (deAna.TotalRendimientos + deBeto.TotalRendimientos).Should().Be(10_000.01m);
        (deAna.TotalRetencion + deBeto.TotalRetencion).Should().Be(700m);
        deAna.IncluyeAportes.Should().BeFalse();
        armadoBeto.Cuentas.Should().ContainSingle().Which.MemberAccountId.Should().Be(cuenta.Id);
    }

    [Fact]
    public async Task El_certificado_de_retencion_de_un_periodo_sin_rendimientos_no_tiene_nada_que_certificar()
    {
        await AhorroAsync(100_000m);
        var r = await Captaciones().ArmarAsync(Pedido(IssuedDocumentKind.WithholdingCertificate, _a.C.Ana) with
        {
            Periodo = new PeriodoDeDocumento(new DateOnly(2026, 1, 1), new DateOnly(2026, 11, 30)),
        }, default);
        r.Error.Code.Should().Be(DocumentErrors.CodigoNadaQueCertificar);
    }

    // ------------------------------------------------------------------------------------------------- anulación por reverso --

    [Fact]
    public async Task El_reverso_de_una_consignacion_anula_el_extracto_y_el_tributario_del_año_pero_no_el_de_retencion_ni_el_titulo_ajeno()
    {
        var (_, cuenta) = await AhorroAsync(300_000m);
        var operacion = await _a.Db.MemberAccountOperations.AsNoTracking().Where(o => o.MemberAccountId == cuenta.Id).OrderBy(o => o.Id).FirstAsync();
        var extracto = await DocumentoAsync(IssuedDocumentKind.AccountStatement, cuenta.Id, desde: G.HoyLocal.AddDays(-30), hasta: G.HoyLocal);
        var tributario = await DocumentoAsync(IssuedDocumentKind.DepositTaxCertificate, cuenta.Id, año: 2026);
        var retencion = await DocumentoAsync(IssuedDocumentKind.WithholdingCertificate, cuenta.Id, desde: G.HoyLocal.AddDays(-30), hasta: G.HoyLocal);
        var titulo = await DocumentoAsync(IssuedDocumentKind.TermDepositTitle, cuenta.Id);

        var reverso = new MemberAccountOperation { MemberAccountId = cuenta.Id, Number = "REV-1", ValueDate = operacion.ValueDate, RegisteredAt = G.UtcNow };
        var anulados = await new AnulacionDeDocumentosDeCuentas(_a.Db).AnularAsync(operacion, reverso, default);

        anulados.Should().BeEquivalentTo([extracto, tributario]);
        anulados.Should().NotContain([retencion, titulo]);
    }

    private async Task<Guid> DocumentoAsync(IssuedDocumentKind clase, int cuenta, DateOnly? desde = null, DateOnly? hasta = null, short? año = null,
        long? selladoPor = null)
    {
        var documento = new IssuedLendingDocument
        {
            Kind = clase, Number = $"DOC-{Guid.NewGuid():N}"[..20], PersonId = _a.C.Ana.Id, PeriodFrom = desde, PeriodTo = hasta, TaxYear = año,
            AsOfDate = hasta, InformationCutoff = G.UtcNow.AddYears(1), VerificationCode = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
            IssuedByUserId = _a.C.Asesora.Id, IssuedAt = G.UtcNow, RulesVersion = "LND-2026.1", CreatedBy = "test",
        };
        _a.Db.IssuedLendingDocuments.Add(documento);
        await _a.Db.SaveChangesAsync();
        // Un título nombra la operación que lo selló (aquí, una que no es la que se reversa).
        _a.Db.IssuedDocumentAccounts.Add(new IssuedDocumentAccount
        {
            IssuedDocumentId = documento.Id, MemberAccountId = cuenta, LastOperationId = selladoPor, CreatedBy = "test",
        });
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        return documento.PublicId;
    }

    // ------------------------------------------------------------------------------------------------------------- título --

    private async Task<LendingLineDto> LineaDeCdatAsync() =>
        await _a.C.LineaDeCaptacionAsync("CDAT", new DepositLineVersionInput
        {
            ValidFrom = CuentasDePrueba.Hoy,
            Reason = "Reglamento de CDAT, acta 30",
            Rate = new DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.10m },
            DayCount = DayCountConvention.Actual360,
            Minimums = new DepositMinimumsInput(1_000_000m),
            SettlementPeriodicity = RepaymentPeriodicity.SinglePayment,
            TermDeposit = new TermDepositLineInput
            {
                MinTermDays = 30, MaxTermDays = 720, InterestPeriodicities = [RepaymentPeriodicity.SinglePayment, RepaymentPeriodicity.Monthly],
                Timings = [InterestTiming.Arrears, InterestTiming.Advance],
            },
        }, DepositClass.TermDeposit);

    [Fact]
    public async Task El_titulo_nace_al_constituir_con_el_sufijo_de_su_version_la_siguiente_lo_reemplaza_y_la_redencion_lo_liquida()
    {
        await FirmanteAsync();
        var linea = await LineaDeCdatAsync();
        var (ahorro, _) = await AhorroAsync(20_000_000m);
        var pedido = new ConstituteTermDepositInput
        {
            LinePublicId = linea.PublicId, HolderPersonPublicId = _a.C.Ana.PublicId, OfficePublicId = _a.C.Oficina.PublicId, Amount = 10_000_000m, TermDays = 180,
            InterestPeriodicity = RepaymentPeriodicity.SinglePayment, Timing = InterestTiming.Arrears,
            PaymentDestination = new TermDepositDestinationInput(TermDepositDestinationKind.Account, ahorro.PublicId),
            Funding = new TermDepositFundingInput(TermDepositFundingKind.TransferFromAccount, ahorro.PublicId, [_a.C.Ana.PublicId]),
        };
        var r = await new ConstituteTermDepositCommandHandler(_a.Db, _a.C.Contexto, Apertura(), Cotizacion(), Constitucion(), Lector(), G.Intervenciones(),
            _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj).Handle(new ConstituteTermDepositCommand(pedido) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _a.Db.ChangeTracker.Clear();

        var cdat = await _a.Db.MemberAccounts.AsNoTracking().SingleAsync(a => a.PublicId == r.Value.Account.PublicId);
        var primero = (await RespuestasDeCdat.TitulosAsync(_a.Db, cdat.Id, default)).Should().ContainSingle().Subject;
        primero.Number.Should().MatchRegex(@"^TIT-\d+-1$");
        primero.TermsVersion.Should().Be(1);
        primero.Status.Should().Be(nameof(IssuedDocumentStatus.Valid));
        var titulo = _pintados.Single().Cuerpo.Should().BeOfType<CuerpoDeTituloDeCdat>().Subject;
        titulo.Capital.Should().Be(10_000_000m);
        titulo.PlazoEnDias.Should().Be(180);
        _pintados.Single().Encabezado.Leyendas.Should().Contain(CuerpoDeTituloDeCdat.LeyendaNoNegociable);

        // La versión 2 (la de una prórroga): el título siguiente conserva el número con el sufijo -2 y el primero queda reemplazado.
        var v1 = await _a.Db.MemberAccountTermsVersions.AsNoTracking().SingleAsync(v => v.MemberAccountId == cdat.Id);
        var v2 = new MemberAccountTermsVersion
        {
            MemberAccountId = cdat.Id, VersionNumber = 2, OperationId = v1.OperationId, EffectiveValueDate = v1.MaturityDate!.Value, RegisteredAt = G.UtcNow,
            Cause = AccountTermsCause.Rollover, DepositLineVersionId = v1.DepositLineVersionId, RulesVersion = v1.RulesVersion, InputsSha256 = v1.InputsSha256,
            PrincipalAmount = v1.PrincipalAmount, TermDays = 180, StartDate = v1.MaturityDate, MaturityDate = v1.MaturityDate.Value.AddDays(180),
            TableAnnualRate = v1.TableAnnualRate, AppliedAnnualRate = v1.AppliedAnnualRate, InterestPeriodicity = v1.InterestPeriodicity,
            InterestTiming = v1.InterestTiming, DayCount = v1.DayCount, RoundingUnit = v1.RoundingUnit, RoundingMode = v1.RoundingMode,
            RoundingMoment = v1.RoundingMoment, RolloverCount = 1, PreviousVersionId = v1.Id, CreatedBy = "test",
        };
        _a.Db.MemberAccountTermsVersions.Add(v2);
        await _a.Db.SaveChangesAsync();
        var operacion = await _a.Db.MemberAccountOperations.AsNoTracking().SingleAsync(o => o.Id == v1.OperationId);
        await G.Db.Database.BeginTransactionAsync();
        var segundo = await Titulos().EmitirAsync(cdat, v2, operacion, _a.C.Asesora.Id, default);
        await Titulos().LiquidarAsync(cdat, operacion, _a.C.Asesora.Id, G.UtcNow, default);
        await _a.Db.SaveChangesAsync();
        await G.Db.Database.CommitTransactionAsync();
        _a.Db.ChangeTracker.Clear();

        segundo.Should().NotBeNull();
        segundo!.Number.Should().Be(DocumentosDeCaptaciones.BaseDelTitulo(primero.Number) + "-2");
        var titulos = await RespuestasDeCdat.TitulosAsync(_a.Db, cdat.Id, default);
        titulos.Select(t => (t.TermsVersion, t.Status)).Should().Equal((1, nameof(IssuedDocumentStatus.Superseded)), (2, nameof(IssuedDocumentStatus.Settled)));
    }

    [Fact]
    public async Task Sin_firmante_el_CDAT_se_constituye_igual_sin_titulo()
    {
        var linea = await LineaDeCdatAsync();
        var (ahorro, _) = await AhorroAsync(20_000_000m);
        var r = await new ConstituteTermDepositCommandHandler(_a.Db, _a.C.Contexto, Apertura(), Cotizacion(), Constitucion(), Lector(), G.Intervenciones(),
            _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj).Handle(new ConstituteTermDepositCommand(new ConstituteTermDepositInput
        {
            LinePublicId = linea.PublicId, HolderPersonPublicId = _a.C.Ana.PublicId, OfficePublicId = _a.C.Oficina.PublicId, Amount = 5_000_000m, TermDays = 90,
            InterestPeriodicity = RepaymentPeriodicity.SinglePayment, Timing = InterestTiming.Arrears,
            PaymentDestination = new TermDepositDestinationInput(TermDepositDestinationKind.Account, ahorro.PublicId),
            Funding = new TermDepositFundingInput(TermDepositFundingKind.TransferFromAccount, ahorro.PublicId, [_a.C.Ana.PublicId]),
        }) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(MemberAccountStatus.Active);
        (await _a.Db.IssuedLendingDocuments.CountAsync()).Should().Be(0);
    }

    // --------------------------------------------------------------------------------------------------------------- lote --

    [Theory]
    [InlineData("Mensual", "2026-11-01", "2026-11-30")]
    [InlineData("Trimestral", "2026-07-01", "2026-09-30")]
    [InlineData("Semestral", "2026-01-01", "2026-06-30")]
    [InlineData("Anual", "2025-01-01", "2025-12-31")]
    public void El_lote_de_extractos_toma_el_ultimo_periodo_terminado_de_la_periodicidad(string periodicidad, string desde, string hasta)
    {
        var p = IssueLendingDocumentBatchCommandHandler.UltimoPeriodoTerminado(periodicidad, new DateOnly(2026, 12, 1));
        p.Should().Be(new PeriodoDeDocumento(DateOnly.Parse(desde), DateOnly.Parse(hasta)));
    }

    [Fact]
    public void Los_textos_propios_de_la_cooperativa_se_leen_por_clase()
    {
        DocumentosDeCaptaciones.TextoDeLaClase("{\"TermDepositTitle\": \"Consulte el reglamento en la oficina.\"}", IssuedDocumentKind.TermDepositTitle)
            .Should().Be("Consulte el reglamento en la oficina.");
        DocumentosDeCaptaciones.TextoDeLaClase("{\"TermDepositTitle\": \"x\"}", IssuedDocumentKind.AccountStatement).Should().BeNull();
        DocumentosDeCaptaciones.TextoDeLaClase("no es json", IssuedDocumentKind.AccountStatement).Should().BeNull();
    }
}
