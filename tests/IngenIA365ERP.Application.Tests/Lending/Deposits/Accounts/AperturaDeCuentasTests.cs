using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;

/// <summary>
/// Feature 014, T077 (US2; api.md §4; data-model §3.1, §3.2, §3.8; FR-006, FR-019, FR-030 a FR-036): la apertura de cuentas de captación y de aportes.
/// La prueba independiente de la historia (cuenta conjunta con beneficiarios 60/40 y exenta del GMF; la segunda exenta se rechaza), los escenarios 1 a 5
/// —el no asociado, la coincidencia en listas que retiene la apertura para el oficial, los beneficiarios que no suman 100 %, la segunda marca y los números
/// consecutivos por línea—, el menor con su representante, los aportes, la naturaleza sin activar, la autorización de datos, el depósito de apertura y el
/// alcance. La concurrencia real del número la prueba la e2e (el cerrojo es de la base). (nuevo)
/// </summary>
public sealed class AperturaDeCuentasTests : IDisposable
{
    private readonly CuentasDePrueba _c = new();

    public void Dispose() => _c.Dispose();

    private static MemberAccountPartyInput Cotitular(Domain.Entities.Core.Person p, decimal? proporcion = null) =>
        new() { PersonPublicId = p.PublicId, Role = MemberAccountPartyRole.CoHolder, Share = proporcion };

    private static MemberAccountBeneficiaryInput Beneficiario(Domain.Entities.Core.Person p, decimal porcentaje) => new(p.PublicId, porcentaje);

    // ------------------------------------------------------------------------------------------- prueba independiente --

    [Fact]
    public async Task US2_conjunta_con_beneficiarios_60_40_y_exenta_del_GMF_nace_activa_con_su_numero_sus_partes_y_su_marca()
    {
        var linea = await _c.LineaDeCaptacionAsync();

        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana, [Cotitular(_c.Beto)], [Beneficiario(_c.Carla, 60m), Beneficiario(_c.Dario, 40m)],
            HandlingMode.Joint, exenta: true));

        cuenta.Number.Should().Be("AHVISTA-000001");
        cuenta.Status.Should().Be(MemberAccountStatus.Active);
        cuenta.Nature.Should().Be(LineNature.Deposit);
        cuenta.DepositClass.Should().Be(DepositClass.Demand);
        cuenta.HandlingMode.Should().Be(HandlingMode.Joint);
        cuenta.Holder.PersonPublicId.Should().Be(_c.Ana.PublicId);
        cuenta.Parties.Should().HaveCount(2);
        cuenta.Parties.Single(p => p.Role == MemberAccountPartyRole.Holder).Share.Should().Be(0.5m, "sin proporciones se reparte por igual");
        cuenta.Parties.Single(p => p.Role == MemberAccountPartyRole.CoHolder).Share.Should().Be(0.5m);
        cuenta.Beneficiaries.Select(b => (b.Person.PersonPublicId, b.Percent, b.SetNumber))
            .Should().BeEquivalentTo([(_c.Carla.PublicId, 60m, 1), (_c.Dario.PublicId, 40m, 1)]);
        cuenta.GmfExempt.Should().NotBeNull();
        cuenta.GmfExempt!.Since.Should().Be(CuentasDePrueba.Hoy);
        cuenta.OpeningBlockers.Should().BeEmpty();
        cuenta.ActivatedOn.Should().Be(CuentasDePrueba.Hoy);

        var guardada = await _c.G.Db.MemberAccounts.SingleAsync();
        (await _c.G.Db.MemberAccountControls.SingleAsync()).MemberAccountId.Should().Be(guardada.Id);
        guardada.OpenedByUserId.Should().Be(_c.Asesora.Id);
        (await _c.G.Db.ScreeningResults.CountAsync(r => r.Context == ScreeningContext.AccountOpening)).Should().Be(2, "titular y cotitular");
        (await _c.G.Db.CaseParticipations.SingleAsync()).Action.Should().Be("Radicar");
    }

    [Fact]
    public async Task US2_4_una_segunda_cuenta_exenta_del_mismo_titular_se_rechaza_con_la_marcada_vigente()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var primera = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana, exenta: true));

        var r = await _c.AbrirAsync(_c.Apertura(linea, _c.Ana, exenta: true));

        r.Error.Code.Should().Be("Lending.Gmf.AlreadyMarked");
        var datos = GobiernoDePrueba.Datos(r.Error);
        datos.GetProperty("accountNumber").GetString().Should().Be(primera.Number);
        datos.GetProperty("since").GetString().Should().Be("2026-12-01");
        (await _c.G.Db.MemberAccounts.CountAsync()).Should().Be(1, "nada se guarda");
    }

    // ------------------------------------------------------------------------------------------------- escenarios --

    [Fact]
    public async Task US2_1_un_no_asociado_en_una_linea_que_exige_asociado_se_rechaza_y_se_ofrece_vincularlo()
    {
        var linea = await _c.LineaDeCaptacionAsync();

        var r = await _c.AbrirAsync(_c.Apertura(linea, _c.Externa));

        r.Error.Code.Should().Be("Lending.Savings.MembersOnly");
        GobiernoDePrueba.Datos(r.Error).GetProperty("howToFix").GetString().Should().Be("Vincular primero");
    }

    [Fact]
    public async Task Un_no_asociado_abre_si_ni_la_linea_ni_el_perfil_exigen_asociado_y_el_perfil_solo_asociados_lo_impide()
    {
        var linea = await _c.LineaDeCaptacionAsync(version: CuentasDePrueba.ALaVista() with { RequiresMembership = false });

        (await _c.AbrirAsync(_c.Apertura(linea, _c.Externa))).IsSuccess.Should().BeTrue();

        _c.Perfil = new(true, true, false, "AHORRO_CREDITO", null);
        (await _c.AbrirAsync(_c.Apertura(linea, _c.Externa))).Error.Code.Should().Be("Lending.Savings.MembersOnly");
    }

    [Fact]
    public async Task US2_2_una_coincidencia_en_listas_retiene_la_apertura_y_solo_se_aprueba_por_otro_tras_liberarla()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        _c.Coincide(_c.Ana);

        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana));

        cuenta.Status.Should().Be(MemberAccountStatus.Opening);
        cuenta.OpeningBlockers.Should().Equal(LectorDeCuentas.RetenidaPorListas);
        var aviso = cuenta.Warnings.Should().ContainSingle().Subject;
        aviso.Code.Should().Be("Lending.Screening.Blocked");
        aviso.ScreeningResultPublicId.Should().Be((await _c.G.Db.ScreeningResults.SingleAsync()).PublicId);
        cuenta.Number.Should().NotBeEmpty("una apertura retenida ya tiene su número");

        // Quien abre no aprueba (radicar × decidir).
        var mismo = await _c.Aprobar().Handle(new ApproveMemberAccountOpeningCommand(cuenta.PublicId, "Revisado"), default);
        mismo.Error.Code.Should().Be("Lending.Segregation.Conflict");

        // Otro tampoco mientras el oficial no libere la coincidencia.
        _c.G.ActuarComo(_c.Jefe);
        var retenida = await _c.Aprobar().Handle(new ApproveMemberAccountOpeningCommand(cuenta.PublicId, "Revisado"), default);
        retenida.Error.Code.Should().Be("Lending.Screening.Blocked");

        var bloqueo = await _c.G.Db.DebtorBlocks.SingleAsync();
        bloqueo.LiftedOn = CuentasDePrueba.Hoy;
        bloqueo.LiftReason = "Liberada por el oficial: homónimo";
        await _c.G.Db.SaveChangesAsync();
        _c.G.Db.ChangeTracker.Clear();

        var aprobada = await _c.Aprobar().Handle(new ApproveMemberAccountOpeningCommand(cuenta.PublicId, "Revisado"), default);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? aprobada.Error.Message : string.Empty);
        aprobada.Value.Status.Should().Be(MemberAccountStatus.Active);
        aprobada.Value.OpeningBlockers.Should().BeEmpty();
    }

    [Fact]
    public async Task US2_3_beneficiarios_que_suman_90_se_rechazan_con_la_suma()
    {
        var linea = await _c.LineaDeCaptacionAsync();

        var r = await _c.AbrirAsync(_c.Apertura(linea, _c.Ana, beneficiarios: [Beneficiario(_c.Carla, 50m), Beneficiario(_c.Dario, 40m)]));

        r.Error.Code.Should().Be("Lending.Savings.BeneficiariesMustSum100");
        GobiernoDePrueba.Datos(r.Error).GetProperty("sum").GetDecimal().Should().Be(90m);
        (await _c.G.Db.MemberAccounts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Los_beneficiarios_se_registran_al_abrir_con_su_constancia()
    {
        // Decisión del dueño del 2026-10-18 (research §13 D34): la constancia se exige también al abrir, no sólo al cambiarlos.
        var linea = await _c.LineaDeCaptacionAsync();
        var sinConstancia = _c.Apertura(linea, _c.Ana, beneficiarios: [Beneficiario(_c.Carla, 100m)]) with { BeneficiaryRecordAttachmentPublicId = null };

        var r = await _c.AbrirAsync(sinConstancia);

        r.Error.Code.Should().Be("Lending.Savings.BeneficiaryRecordRequired");
        GobiernoDePrueba.Datos(r.Error).GetProperty("field").GetString().Should().Be("beneficiaryRecordAttachmentPublicId");
        (await _c.G.Db.MemberAccounts.CountAsync()).Should().Be(0);
        (await _c.AbrirAsync(sinConstancia with { BeneficiaryRecordAttachmentPublicId = Guid.NewGuid() })).IsSuccess.Should().BeTrue();
        (await _c.AbrirAsync(_c.Apertura(linea, _c.Beto))).IsSuccess.Should().BeTrue("sin beneficiarios no hay constancia que pedir");
    }

    [Fact]
    public async Task US2_5_dos_aperturas_de_la_misma_linea_reciben_numeros_consecutivos_y_cada_linea_lleva_el_suyo()
    {
        var vista = await _c.LineaDeCaptacionAsync();
        var otra = await _c.LineaDeCaptacionAsync("AHNAVIDAD");

        var a = await _c.AbiertaAsync(_c.Apertura(vista, _c.Ana));
        var b = await _c.AbiertaAsync(_c.Apertura(vista, _c.Beto));
        var c = await _c.AbiertaAsync(_c.Apertura(otra, _c.Ana));

        a.Number.Should().Be("AHVISTA-000001");
        b.Number.Should().Be("AHVISTA-000002");
        c.Number.Should().Be("AHNAVIDAD-000001");
        await _c.Cerrojo.Received().TomarSetupAsync(false, Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------------------------------------------- menores --

    [Fact]
    public async Task Un_menor_titular_exige_un_representante_con_su_soporte_y_con_el_nace_activa()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var menor = _c.Persona("Sofia", "Menor", "1010", new DateOnly(2015, 5, 20), asociada: true);

        var sin = await _c.AbrirAsync(_c.Apertura(linea, menor));
        sin.Error.Code.Should().Be("Lending.Savings.RepresentativeRequired");
        GobiernoDePrueba.Datos(sin.Error).GetProperty("supportRequired").GetBoolean().Should().BeTrue();

        var sinSoporte = await _c.AbrirAsync(_c.Apertura(linea, menor,
            [new MemberAccountPartyInput { PersonPublicId = _c.Ana.PublicId, Role = MemberAccountPartyRole.LegalRepresentative, RepresentationBasis = "Parent" }]));
        sinSoporte.Error.Code.Should().Be("Lending.Savings.RepresentativeRequired");

        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, menor,
        [
            new MemberAccountPartyInput
            {
                PersonPublicId = _c.Ana.PublicId, Role = MemberAccountPartyRole.LegalRepresentative, RepresentationBasis = "Parent",
                SupportAttachmentPublicId = Guid.NewGuid(),
            },
        ]));
        cuenta.Status.Should().Be(MemberAccountStatus.Active);
        cuenta.Holder.PersonPublicId.Should().Be(menor.PublicId, "el menor es el titular");
        cuenta.Parties.Should().Contain(p => p.Role == MemberAccountPartyRole.LegalRepresentative && p.RepresentationBasis == "Parent");
    }

    [Fact]
    public async Task Una_linea_sin_menores_no_abre_al_menor_y_un_menor_no_representa()
    {
        var sinMenores = await _c.LineaDeCaptacionAsync(version: CuentasDePrueba.ALaVista(menores: false));
        var conMenores = await _c.LineaDeCaptacionAsync("AHMENOR");
        var menor = _c.Persona("Sofia", "Menor", "1010", new DateOnly(2015, 5, 20), asociada: true);

        (await _c.AbrirAsync(_c.Apertura(sinMenores, menor))).Error.Code.Should().Be("Lending.Savings.MinorsNotAllowed");
        var representadoPorMenor = await _c.AbrirAsync(_c.Apertura(conMenores, menor,
        [
            new MemberAccountPartyInput
            {
                PersonPublicId = _c.Carla.PublicId, Role = MemberAccountPartyRole.LegalRepresentative, RepresentationBasis = "Parent",
                SupportAttachmentPublicId = Guid.NewGuid(),
            },
        ]));
        representadoPorMenor.Error.Code.Should().Be("Lending.Savings.PartyInvalid");
    }

    // ------------------------------------------------------------------------------------------------------- aportes --

    [Fact]
    public async Task Aportes_una_sola_cuenta_viva_por_persona_solo_titular_y_beneficiarios_y_siempre_asociado()
    {
        var aportes = await _c.LineaDeAportesAsync();

        var cuenta = await _c.AbiertaAsync(_c.Apertura(aportes, _c.Ana, beneficiarios: [Beneficiario(_c.Carla, 100m)]));
        cuenta.Nature.Should().Be(LineNature.Contribution);
        cuenta.DepositClass.Should().BeNull();
        cuenta.Status.Should().Be(MemberAccountStatus.Active, "los aportes no esperan depósito de apertura");

        var otra = await _c.AbrirAsync(_c.Apertura(aportes, _c.Ana));
        otra.Error.Code.Should().Be("Lending.Contributions.AccountAlreadyOpen");
        GobiernoDePrueba.Datos(otra.Error).GetProperty("existingAccountNumber").GetString().Should().Be(cuenta.Number);

        (await _c.AbrirAsync(_c.Apertura(aportes, _c.Beto, [Cotitular(_c.Ana)]))).Error.Code
            .Should().Be("Lending.Contributions.OnlyHolderAndBeneficiaries");
        (await _c.AbrirAsync(_c.Apertura(aportes, _c.Externa))).Error.Code.Should().Be("Lending.Savings.MembersOnly");
        (await _c.AbrirAsync(_c.Apertura(aportes, _c.Beto, exenta: true))).Error.Code.Should().Be("Lending.Gmf.AccountNotSavings");
    }

    // ------------------------------------------------------------------------------------------------- validaciones --

    [Fact]
    public async Task Sin_la_naturaleza_activada_responde_NotActivated_de_su_naturaleza()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var aportes = await _c.LineaDeAportesAsync();
        var setup = await _c.G.Db.LendingSetups.SingleAsync();
        setup.DepositsActivationDate = null;
        setup.ContributionsActivationDate = CuentasDePrueba.Hoy.AddDays(1);
        await _c.G.Db.SaveChangesAsync();
        _c.G.Db.ChangeTracker.Clear();

        (await _c.AbrirAsync(_c.Apertura(linea, _c.Ana))).Error.Code.Should().Be("Lending.Savings.NotActivated");
        (await _c.AbrirAsync(_c.Apertura(aportes, _c.Ana))).Error.Code.Should().Be("Lending.Contributions.NotActivated", "activa desde mañana");
    }

    [Fact]
    public async Task Sin_la_autorizacion_de_datos_de_un_cotitular_no_se_abre()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        _c.SinAutorizacion(_c.Beto);

        var r = await _c.AbrirAsync(_c.Apertura(linea, _c.Ana, [Cotitular(_c.Beto)]));

        r.Error.Code.Should().Be("Lending.Authorization.Missing");
        GobiernoDePrueba.Datos(r.Error).GetProperty("personPublicId").GetGuid().Should().Be(_c.Beto.PublicId);
    }

    [Fact]
    public async Task Sin_la_fecha_de_nacimiento_falta_un_dato_de_conocimiento()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var sinFecha = _c.Persona("Hugo", "SinFecha", "1020", null, asociada: true);

        var r = await _c.AbrirAsync(_c.Apertura(linea, sinFecha));

        r.Error.Code.Should().Be("Lending.Savings.MemberDataIncomplete");
        GobiernoDePrueba.Datos(r.Error).GetProperty("missing").EnumerateArray().Select(m => m.GetString()).Should().Contain("fecha de nacimiento");
    }

    [Fact]
    public async Task Con_cotitulares_en_una_linea_que_no_los_admite_o_proporciones_que_no_dejan_nada_al_titular_se_rechaza()
    {
        var sinCotitulares = await _c.LineaDeCaptacionAsync(version: CuentasDePrueba.ALaVista(cotitulares: false));
        var conCotitulares = await _c.LineaDeCaptacionAsync("AHCONJ");

        (await _c.AbrirAsync(_c.Apertura(sinCotitulares, _c.Ana, [Cotitular(_c.Beto)]))).Error.Code.Should().Be("Lending.Savings.CoHoldersNotAllowed");
        (await _c.AbrirAsync(_c.Apertura(conCotitulares, _c.Ana, [Cotitular(_c.Beto, 1m)]))).Error.Code.Should().Be("Lending.Savings.SharesDoNotAddUp");
        (await _c.AbrirAsync(_c.Apertura(conCotitulares, _c.Ana, manejo: HandlingMode.Joint))).Error.Code
            .Should().Be("Lending.Savings.JointRequiresTwoSigners");

        var con30 = await _c.AbiertaAsync(_c.Apertura(conCotitulares, _c.Ana, [Cotitular(_c.Beto, 0.3m)]));
        con30.Parties.Single(p => p.Role == MemberAccountPartyRole.Holder).Share.Should().Be(0.7m);
    }

    [Fact]
    public async Task Con_deposito_de_apertura_nace_en_apertura_esperando_el_deposito_y_aprobarla_no_la_activa()
    {
        var linea = await _c.LineaDeCaptacionAsync(version: CuentasDePrueba.ALaVista(minimoDeApertura: 50_000m));

        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana));

        cuenta.Status.Should().Be(MemberAccountStatus.Opening);
        cuenta.OpeningBlockers.Should().Equal(LectorDeCuentas.EsperaDepositoInicial);
        _c.G.ActuarComo(_c.Jefe);
        var aprobada = await _c.Aprobar().Handle(new ApproveMemberAccountOpeningCommand(cuenta.PublicId, "Revisado"), default);
        aprobada.Value.Status.Should().Be(MemberAccountStatus.Opening, "el depósito de apertura llega por el contrato de cuentas");
    }

    [Fact]
    public async Task Una_linea_de_credito_no_abre_cuentas()
    {
        var credito = Domain.Entities.Lending.Configuration.CreditLine.Nueva("CONLIB", "Consumo libre inversión", null);
        _c.G.Db.CreditLines.Add(credito);
        await _c.G.Db.SaveChangesAsync();

        var r = await _c.AbrirAsync(new OpenAccountInput { LinePublicId = credito.PublicId, HolderPersonPublicId = _c.Ana.PublicId, OfficePublicId = _c.Oficina.PublicId });

        r.Error.Code.Should().Be(MemberAccount.CodigoNaturalezaDistinta);
    }

    [Fact]
    public async Task Fuera_del_alcance_de_oficina_la_apertura_es_404_y_la_cuenta_no_aparece_ni_se_lee()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var cuenta = await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana));
        _c.G.AlcanceActual = new AlcanceDeCartera(false, new HashSet<int> { _c.Oficina.Id + 99 }, null, true, new HashSet<int>());

        (await _c.AbrirAsync(_c.Apertura(linea, _c.Beto))).Error.Code.Should().Be("Generic.NotFound");
        var lista = await _c.Listar().Handle(new ListMemberAccountsQuery(), default);
        lista.Value.Items.Should().BeEmpty();
        (await _c.Leer().Handle(new GetMemberAccountQuery(cuenta.PublicId), default)).Error.Code.Should().Be("Lending.Savings.NotFound");
        (await _c.G.EventosAsync()).Should().Contain("Lending.Access.OutOfScope");
    }

    [Fact]
    public async Task La_lista_filtra_por_naturaleza_titular_y_busqueda_con_el_saldo_del_titular()
    {
        var linea = await _c.LineaDeCaptacionAsync();
        var aportes = await _c.LineaDeAportesAsync();
        await _c.AbiertaAsync(_c.Apertura(linea, _c.Ana, exenta: true));
        await _c.AbiertaAsync(_c.Apertura(aportes, _c.Ana));
        await _c.AbiertaAsync(_c.Apertura(linea, _c.Beto));

        var deAna = (await _c.Listar().Handle(new ListMemberAccountsQuery(PersonPublicId: _c.Ana.PublicId), default)).Value.Items;
        var deAportes = (await _c.Listar().Handle(new ListMemberAccountsQuery(Nature: LineNature.Contribution), default)).Value.Items;
        var porDocumento = (await _c.Listar().Handle(new ListMemberAccountsQuery(Search: "1002"), default)).Value.Items;

        deAna.Should().HaveCount(2);
        deAna.Single(i => i.Nature == LineNature.Deposit).Marks.Should().Contain("GmfExempt");
        deAportes.Should().ContainSingle().Which.Balance.Should().Be(0m);
        porDocumento.Should().ContainSingle().Which.Holder.PersonPublicId.Should().Be(_c.Beto.PublicId);
    }

    [Fact]
    public void El_validador_exige_linea_titular_y_oficina()
    {
        var r = new OpenMemberAccountCommandValidator().Validate(new OpenMemberAccountCommand(new OpenAccountInput()));

        r.Errors.Select(e => e.PropertyName).Should().Contain(["Input.LinePublicId", "Input.HolderPersonPublicId", "Input.OfficePublicId"]);
    }
}
