using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using static IngenIA365ERP.Application.Tests.Lending.Regulatory.RegulatoriosDePrueba;

namespace IngenIA365ERP.Application.Tests.Lending.Regulatory;

/// <summary>
/// Decisiones del dueño del 2026-10-16 (feature 013, K5, US16) en la proyección de una central, al corte de diciembre de 2026:
/// <list type="bullet">
/// <item>el dato negativo sale de los <b>rangos de mora por tipo de cartera</b> (<c>Centrales.RangosDeMora</c>): por debajo del rango negativo la
/// obligación se reporta al día, sin mora y sin exigir aviso; desde él, en mora con su rango, y la altura (el código de la novedad) sale del rango;</item>
/// <item>el codeudor se reporta con dato negativo sólo con <c>AvisoPrevio.IncluirCodeudores</c> y <b>su</b> aviso completo; sin el parámetro queda fuera
/// con <c>NoPriorNoticeEvidence</c> y sin alerta.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class RangosYCodeudoresEnCentralesTests : IAsyncLifetime
{
    private const string Datacredito = "DATACREDITO";
    private RegulatoriosDePrueba _r = null!;
    private int _documento = 72_000_000;

    public async Task InitializeAsync() => _r = await CrearAsync();

    public Task DisposeAsync()
    {
        _r.Dispose();
        return Task.CompletedTask;
    }

    private Person Persona(string nombre) => _r.Persona(nombre, "Prueba", (++_documento).ToString(System.Globalization.CultureInfo.InvariantCulture));

    private async Task<ProyeccionDeCentral> ProyectarAsync(string central = Datacredito)
    {
        var r = await _r.Centrales().ProyectarAsync(central, Anio, Mes, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private static TitularDeCentral Titular(ProyeccionDeCentral p, Loan credito, Person? persona = null) =>
        p.Titulares.Single(t => t.LoanId == credito.Id && t.PersonId == (persona?.Id ?? credito.DebtorPersonId));

    private async Task AvisoAsync(Loan credito, Person persona, int dias)
    {
        _r.Db.PriorNotices.Add(new PriorNotice
        {
            LoanId = credito.Id, PersonId = persona.Id, EpisodeStartDate = Corte.AddDays(-dias), RequiredCommunications = 1, MinimumDays = 20,
            MigratedFirstSentOn = Corte.AddDays(-50), CreatedBy = "test",
        });
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();
    }

    private async Task CodeudorAsync(Loan credito, Person persona)
    {
        _r.Db.LoanParties.Add(LoanParty.DeLaSolicitud(credito.Id, persona.Id, CreditPartyRole.CoDebtor, new DateOnly(2026, 6, 1), null));
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();
    }

    private static string? Novedad(ProyeccionDeCentral p, string credito, bool deudor = true)
    {
        var layout = CatalogoDeLayoutsDeCartera.Embebido.Vigente(RegulatoryFileKind.CreditBureau, p.Central, Corte)!.Layout;
        return (string?)FilasDeCentral.Construir(layout, p.Archivo, p.Titulares).Where(f => f.Credito == credito)
            .ElementAt(deudor ? 0 : 1).Valor("Obligacion.Novedad");
    }

    // ------------------------------------------------------------------------------------------------- rangos de mora --

    [Fact]
    public async Task Con_el_defecto_toda_mora_es_negativa_y_la_altura_sale_del_rango()
    {
        var moroso = Persona("Moroso");
        var credito = await _r.CreditoAsync("CR-RAN-0001", moroso, dias: 60);
        await AvisoAsync(credito, moroso, 60);

        var p = await ProyectarAsync();

        Titular(p, credito).Should().Match<TitularDeCentral>(t => t.Incluido && t.EsNegativo && t.Estado == EstadoEnCentral.EnMora && t.RangoDeMora == 2
                                                                  && t.AlturaDeMora == "31-60" && t.DiasDeMora == 60);
        Novedad(p, "CR-RAN-0001").Should().Be("07", "en DataCrédito el rango 2 es la altura «mora 60» (por cotejar)");
        Novedad(await ProyectarAsync("TRANSUNION"), "CR-RAN-0001").Should().Be("MO", "la genérica no distingue alturas");
    }

    [Fact]
    public async Task Por_debajo_del_rango_negativo_se_reporta_al_dia_sin_aviso_y_desde_el_en_mora()
    {
        await _r.E.ParametroAsync(ParametrosDeCartera.CentralesRangosDeMora, "*=30,60,90|2");
        var (leve, grave) = (Persona("Leve"), Persona("Grave"));
        var a = await _r.CreditoAsync("CR-RAN-0002", leve, dias: 20);
        var b = await _r.CreditoAsync("CR-RAN-0003", grave, dias: 45);

        var p = await ProyectarAsync();

        Titular(p, a).Should().Match<TitularDeCentral>(t => t.Incluido && !t.EsNegativo && t.Estado == EstadoEnCentral.AlDia && t.DiasDeMora == 0
                                                            && t.SaldoEnMora == 0m && t.CuotasEnMora == 0 && t.RangoDeMora == null && t.InicioDelEpisodio == null);
        Novedad(p, "CR-RAN-0002").Should().Be("01");
        Titular(p, b).Should().Match<TitularDeCentral>(t => !t.Incluido && t.EsNegativo && t.RangoDeMora == 2
                                                            && t.MotivoDeExclusion == MotivosDeExclusionDeCentral.NoPriorNoticeEvidence);
        p.Alertas.Should().ContainSingle(x => x.TypeCode == TiposDeAlerta.AvisoPrevioSinConstancia && x.Subject.EndsWith("CR-RAN-0003"));
    }

    [Fact]
    public async Task Cada_tipo_de_cartera_usa_sus_rangos()
    {
        await _r.E.ParametroAsync(ParametrosDeCartera.CentralesRangosDeMora, "*=30,60,90|1;VIVIENDA=60,120,180|2");
        var (consumo, vivienda) = (Persona("Consumo"), Persona("Vivienda"));
        var a = await _r.CreditoAsync("CR-RAN-0004", consumo, dias: 45);
        var b = await _r.CreditoAsync("CR-RAN-0005", vivienda, dias: 45, modalidad: "VIVIENDA");
        await AvisoAsync(a, consumo, 45);

        var p = await ProyectarAsync();

        Titular(p, a).Should().Match<TitularDeCentral>(t => t.Incluido && t.Estado == EstadoEnCentral.EnMora && t.RangoDeMora == 2 && t.AlturaDeMora == "31-60");
        Titular(p, b).Should().Match<TitularDeCentral>(t => t.Incluido && t.Estado == EstadoEnCentral.AlDia && !t.EsNegativo,
            "en vivienda 45 días caen en el rango 1-60, que no es negativo");
    }

    // ------------------------------------------------------------------------------------------------------ codeudores --

    [Fact]
    public async Task Sin_el_parametro_el_codeudor_en_mora_queda_fuera_sin_alerta_aunque_tenga_aviso()
    {
        var (deudor, codeudor) = (Persona("Deudor"), Persona("Codeudor"));
        var credito = await _r.CreditoAsync("CR-COD-0101", deudor, dias: 60);
        await CodeudorAsync(credito, codeudor);
        await AvisoAsync(credito, deudor, 60);
        await AvisoAsync(credito, codeudor, 60);

        var p = await ProyectarAsync();

        Titular(p, credito).Should().Match<TitularDeCentral>(t => t.Incluido && t.EsNegativo);
        Titular(p, credito, codeudor).Should().Match<TitularDeCentral>(t => !t.Incluido && t.MotivoDeExclusion == MotivosDeExclusionDeCentral.NoPriorNoticeEvidence);
        p.Alertas.Should().BeEmpty("sin AvisoPrevio.IncluirCodeudores no hay gestión que hacer sobre el codeudor");
    }

    [Fact]
    public async Task Con_el_parametro_el_codeudor_se_reporta_solo_con_su_propio_aviso_completo()
    {
        await _r.E.ParametroAsync(ParametrosDeCartera.AvisoPrevioIncluirCodeudores, "true");
        var (deudor, conAviso, sinAviso) = (Persona("Deudora"), Persona("Avisado"), Persona("Sinavisar"));
        var credito = await _r.CreditoAsync("CR-COD-0102", deudor, dias: 60);
        await CodeudorAsync(credito, conAviso);
        await CodeudorAsync(credito, sinAviso);
        await AvisoAsync(credito, deudor, 60);
        await AvisoAsync(credito, conAviso, 60);

        var p = await ProyectarAsync();

        Titular(p, credito, conAviso).Should().Match<TitularDeCentral>(t => t.Incluido && t.EsNegativo && !t.EsDeudor && t.AvisoPrevioId != null);
        Titular(p, credito, sinAviso).Should().Match<TitularDeCentral>(t => !t.Incluido && t.MotivoDeExclusion == MotivosDeExclusionDeCentral.NoPriorNoticeEvidence);
        p.Alertas.Should().ContainSingle(x => x.TypeCode == TiposDeAlerta.AvisoPrevioSinConstancia && x.DedupKey!.Contains($":{sinAviso.Id}:"));
        Novedad(p, "CR-COD-0102", deudor: false).Should().Be("07");
    }
}
