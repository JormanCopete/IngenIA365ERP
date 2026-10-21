using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US2 (T073–T074; data-model §3.2, §3.8; FR-033, FR-034, FR-036): las reglas puras de titularidad —proporciones, manejo conjunto, menores
/// y personas jurídicas con su representante, aportes sólo con titular y beneficiarios, beneficiarios que suman 100 %—, la mayoría de edad y la marca de
/// exención del GMF. (nuevo)
/// </summary>
public class ReglasDeTitularidadTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private static ContextoDeTitularidad Contexto(LineNature naturaleza = LineNature.Deposit, bool menor = false, bool juridico = false,
        bool cotitulares = true, bool menores = true) =>
        new(naturaleza, 1, cotitulares, menores, AdmitePersonasJuridicas: true, menor, juridico);

    [Fact]
    public void Sin_proporciones_se_reparte_por_igual_y_el_titular_absorbe_el_redondeo()
    {
        var (partes, problema) = ReglasDeTitularidad.Resolver(Contexto(), HandlingMode.Indistinct,
            [new ParteSolicitada(2, MemberAccountPartyRole.CoHolder), new ParteSolicitada(3, MemberAccountPartyRole.CoHolder)]);

        problema.Should().BeNull();
        partes.Single(p => p.PersonaId == 2).Proporcion.Should().Be(0.333333m);
        partes.Single(p => p.PersonaId == 1).Proporcion.Should().Be(0.333334m);
        partes.Where(p => p.Proporcion is not null).Sum(p => p.Proporcion!.Value).Should().Be(1m);
    }

    [Fact]
    public void Proporciones_de_unos_y_no_de_otros_o_que_no_dejan_nada_al_titular_no_cuadran()
    {
        ReglasDeTitularidad.Resolver(Contexto(), HandlingMode.Indistinct,
                [new ParteSolicitada(2, MemberAccountPartyRole.CoHolder, 0.4m), new ParteSolicitada(3, MemberAccountPartyRole.CoHolder)])
            .Problema!.Codigo.Should().Be(ReglasDeTitularidad.CodigoProporcionesNoCuadran);
        ReglasDeTitularidad.Resolver(Contexto(), HandlingMode.Indistinct,
                [new ParteSolicitada(2, MemberAccountPartyRole.CoHolder, 0.6m), new ParteSolicitada(3, MemberAccountPartyRole.CoHolder, 0.4m)])
            .Problema!.Codigo.Should().Be(ReglasDeTitularidad.CodigoProporcionesNoCuadran);
    }

    [Fact]
    public void Manejo_conjunto_exige_dos_que_firmen_y_una_persona_no_repite()
    {
        ReglasDeTitularidad.Resolver(Contexto(), HandlingMode.Joint, []).Problema!.Codigo.Should().Be(ReglasDeTitularidad.CodigoConjuntoSinFirmantes);
        ReglasDeTitularidad.Resolver(Contexto(), HandlingMode.Joint, [new ParteSolicitada(2, MemberAccountPartyRole.Signatory, LimiteDeFirma: 1_000m)])
            .Problema.Should().BeNull();
        ReglasDeTitularidad.Resolver(Contexto(), HandlingMode.Indistinct,
                [new ParteSolicitada(2, MemberAccountPartyRole.CoHolder), new ParteSolicitada(2, MemberAccountPartyRole.Signatory)])
            .Problema!.Codigo.Should().Be(ReglasDeTitularidad.CodigoParteRepetida);
        ReglasDeTitularidad.Resolver(Contexto(), HandlingMode.Indistinct, [new ParteSolicitada(1, MemberAccountPartyRole.Signatory)])
            .Problema!.Codigo.Should().Be(ReglasDeTitularidad.CodigoParteRepetida);
    }

    [Fact]
    public void Un_menor_o_una_juridica_exige_un_representante_con_fundamento_y_soporte()
    {
        ReglasDeTitularidad.Resolver(Contexto(menor: true), HandlingMode.Indistinct, []).Problema!.Datos!["supportRequired"].Should().Be(true);
        ReglasDeTitularidad.Resolver(Contexto(menor: true, menores: false), HandlingMode.Indistinct, []).Problema!.Codigo
            .Should().Be(ReglasDeTitularidad.CodigoMenoresNoAdmitidos);
        var representante = new ParteSolicitada(2, MemberAccountPartyRole.LegalRepresentative, Fundamento: "LegalEntityRepresentative", Soporte: Guid.NewGuid());
        ReglasDeTitularidad.Resolver(Contexto(juridico: true), HandlingMode.Indistinct, [representante]).Problema.Should().BeNull();
        ReglasDeTitularidad.Resolver(Contexto(menor: true), HandlingMode.Indistinct, [representante with { Fundamento = "Abuelo" }]).Problema!.Codigo
            .Should().Be(ReglasDeTitularidad.CodigoParteInvalida);
    }

    [Fact]
    public void Aportes_solo_titular_e_indistinto()
    {
        ReglasDeTitularidad.Resolver(Contexto(LineNature.Contribution), HandlingMode.Indistinct, [new ParteSolicitada(2, MemberAccountPartyRole.Signatory)])
            .Problema!.Codigo.Should().Be(ReglasDeTitularidad.CodigoSoloTitularYBeneficiarios);
        var (partes, problema) = ReglasDeTitularidad.Resolver(Contexto(LineNature.Contribution, menor: true), HandlingMode.Indistinct, []);
        problema.Should().BeNull("los aportes del menor no llevan representante en la cuenta");
        partes.Should().ContainSingle(p => p.Papel == MemberAccountPartyRole.Holder && p.Proporcion == null);
    }

    [Fact]
    public void Los_beneficiarios_suman_exactamente_100_y_se_guardan_como_fraccion()
    {
        ReglasDeTitularidad.ResolverBeneficiarios(1, [new(2, 60m), new(3, 30m)]).Problema!.Datos!["sum"].Should().Be(90m);
        ReglasDeTitularidad.ResolverBeneficiarios(1, [new(1, 100m)]).Problema!.Codigo.Should().Be(ReglasDeTitularidad.CodigoParteRepetida);
        ReglasDeTitularidad.ResolverBeneficiarios(1, [new(2, 60m), new(3, 40m)]).Beneficiarios
            .Should().BeEquivalentTo([(2, 0.6m), (3, 0.4m)]);
        ReglasDeTitularidad.ResolverBeneficiarios(1, []).Problema.Should().BeNull();
    }

    [Fact]
    public void Es_menor_hasta_el_dia_en_que_cumple_la_mayoria()
    {
        ReglasDeTitularidad.EsMenor(new DateOnly(2008, 12, 1), Hoy, 18).Should().BeFalse("cumple 18 hoy");
        ReglasDeTitularidad.EsMenor(new DateOnly(2008, 12, 2), Hoy, 18).Should().BeTrue("cumple 18 mañana");
        ReglasDeTitularidad.EsMenor(new DateOnly(2008, 12, 1), Hoy, 21).Should().BeTrue("la mayoría es parámetro");
        ReglasDeTitularidad.CumpleMayoriaEl(new DateOnly(2008, 2, 29), 18).Should().Be(new DateOnly(2026, 2, 28));
        ReglasDeTitularidad.EsMenor(null, Hoy, 18).Should().BeFalse("sin fecha no se presume menor (la apertura exige la fecha)");
    }

    [Fact]
    public void La_marca_del_GMF_solo_en_ahorro_y_termina_desde_la_vispera()
    {
        var aportes = MemberAccount.Abrir(1, LineNature.Contribution, null, 7, 3, HandlingMode.Indistinct, Hoy, 5, DateTime.UtcNow);
        var cdat = MemberAccount.Abrir(1, LineNature.Deposit, DepositClass.TermDeposit, 7, 3, HandlingMode.Indistinct, Hoy, 5, DateTime.UtcNow);
        var vista = MemberAccount.Abrir(1, LineNature.Deposit, DepositClass.Demand, 7, 3, HandlingMode.Indistinct, Hoy, 5, DateTime.UtcNow);

        GmfExemptAccount.ValidarCuenta(aportes)!.Codigo.Should().Be(GmfExemptAccount.CodigoNoEsAhorro);
        GmfExemptAccount.ValidarCuenta(cdat)!.Codigo.Should().Be(GmfExemptAccount.CodigoNoEsAhorro);
        var marca = GmfExemptAccount.Marcar(vista, Hoy, "La pide el titular", 5);
        marca.PersonId.Should().Be(7);
        marca.Terminar(Hoy.AddDays(-1), "Cambio el mismo día", 5);
        marca.VigenteEn(Hoy).Should().BeFalse();
        var otra = GmfExemptAccount.Marcar(vista, Hoy, "Otra", 5);
        otra.Invoking(m => m.Terminar(Hoy.AddDays(-2), "Antes de empezar", 5)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
