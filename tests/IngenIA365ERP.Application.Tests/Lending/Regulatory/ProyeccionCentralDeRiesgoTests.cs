using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using static IngenIA365ERP.Application.Tests.Lending.Regulatory.RegulatoriosDePrueba;

namespace IngenIA365ERP.Application.Tests.Lending.Regulatory;

/// <summary>
/// T0990 (feature 013, K5, US16; FR-006, FR-054, FR-097, FR-123, FR-137; SC-012; data-model §28.2; adaptadores-y-archivos.md §5.5): la proyección del
/// archivo de una central, al corte de diciembre de 2026. Novedades desde el último archivo <c>Uploaded</c> (lo cerrado antes de la ventana no vuelve,
/// salvo su dato negativo dentro de la permanencia legal); titulares —deudor y codeudores— con su propia autorización <c>CreditBureauReport</c>; el dato
/// negativo sólo con aviso <c>Complete</c> de su episodio desde su fecha más temprana (si no, fuera y la alerta <c>Cartera.AvisoPrevioSinConstancia</c>),
/// y nunca con reclamación al seguro en trámite, insolvencia o pagaduría dentro del umbral; el reclamo en trámite va con su marca; lo reportado sin aviso
/// (de un archivo anterior o migrado) se retira; lo anulado desde el origen que ya se reportó sale como retiro, una sola vez, y lo nunca reportado no
/// aparece.
/// </summary>
public sealed class ProyeccionCentralDeRiesgoTests : IAsyncLifetime
{
    private const string Datacredito = "DATACREDITO";
    private RegulatoriosDePrueba _r = null!;
    private int _documento = 70_000_000;

    public async Task InitializeAsync() => _r = await CrearAsync();

    public Task DisposeAsync()
    {
        _r.Dispose();
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------------------------------------- piezas --

    private Person Persona(string nombre) => _r.Persona(nombre, "Prueba", (++_documento).ToString(System.Globalization.CultureInfo.InvariantCulture));

    private async Task<ProyeccionDeCentral> ProyectarAsync(string central = Datacredito)
    {
        var r = await _r.Centrales().ProyectarAsync(central, Anio, Mes, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private static TitularDeCentral Titular(ProyeccionDeCentral p, Loan credito, Person? persona = null) =>
        p.Titulares.Single(t => t.LoanId == credito.Id && t.PersonId == (persona?.Id ?? credito.DebtorPersonId));

    /// <summary>Un aviso previo migrado del episodio que empieza <paramref name="dias"/> días antes del corte, enviado el <paramref name="enviado"/>.</summary>
    private async Task<PriorNotice> AvisoAsync(Loan credito, Person persona, int dias, DateOnly enviado, int minimos = 20)
    {
        var aviso = new PriorNotice
        {
            LoanId = credito.Id, PersonId = persona.Id, EpisodeStartDate = Corte.AddDays(-dias), RequiredCommunications = 1, MinimumDays = minimos,
            MigratedFirstSentOn = enviado, CreatedBy = "test",
        };
        _r.Db.PriorNotices.Add(aviso);
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();
        return aviso;
    }

    /// <summary>El archivo de noviembre de la central, ya cargado, con sus líneas.</summary>
    private async Task<RegulatoryFile> CargadoDeNoviembreAsync(params RegulatoryFileLine[] lineas)
    {
        var archivo = RegulatoryFile.Nueva(RegulatoryFileKind.CreditBureau, Datacredito, 2026, 11, 1, "CENTRAL-DATACREDITO-v1", true, null, false, "[]", 0, 0,
            "[]", 0, lineas.Length, "{}", "ARG-000900_DATACREDITO_2026-11_v1.txt", new string('a', 64), Guid.NewGuid(), _r.D.Cajera.Id, _r.G.UtcNow.AddDays(-20));
        archivo.MarcarCargado(_r.D.Cajera.Id, _r.G.UtcNow.AddDays(-19), "RAD-NOV", new DateOnly(2026, 11, 12));
        _r.Db.RegulatoryFiles.Add(archivo);
        await _r.Db.SaveChangesAsync();
        var n = 0;
        foreach (var l in lineas)
            _r.Db.RegulatoryFileLines.Add(new RegulatoryFileLine
            {
                RegulatoryFileId = archivo.Id, LineNumber = ++n, LoanId = l.LoanId, PersonId = l.PersonId, Included = l.Included, IsNegative = l.IsNegative,
                DaysPastDue = l.DaysPastDue, PriorNoticeId = l.PriorNoticeId, ReportedStatusCode = l.ReportedStatusCode ?? "01",
                IsWithdrawalOfUnnoticedReport = l.IsWithdrawalOfUnnoticedReport,
            });
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();
        return archivo;
    }

    private static RegulatoryFileLine Reportado(Loan credito, Person? persona = null, int dias = 0, bool negativo = false, int? aviso = null, string? estado = null) =>
        new() { LoanId = credito.Id, PersonId = persona?.Id ?? credito.DebtorPersonId, Included = true, DaysPastDue = dias, IsNegative = negativo,
            PriorNoticeId = aviso, ReportedStatusCode = estado };

    private async Task CodeudorAsync(Loan credito, Person persona)
    {
        _r.Db.LoanParties.Add(LoanParty.DeLaSolicitud(credito.Id, persona.Id, CreditPartyRole.CoDebtor, new DateOnly(2026, 6, 1), null));
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();
    }

    // -------------------------------------------------------------------------------------------------------- pruebas --

    [Fact]
    public async Task Sin_autorizacion_de_reporte_con_evidencia_el_titular_queda_fuera()
    {
        var beto = await _r.CreditoAsync("CR-CEN-0001", _r.D.O.Beto);
        var carla = await _r.CreditoAsync("CR-CEN-0002", _r.D.O.Carla);
        _r.D.O.QuitarAutorizacion(_r.D.O.Carla, DataAuthorizationPurpose.CreditBureauReport);

        var p = await ProyectarAsync();

        Titular(p, beto).Should().Match<TitularDeCentral>(t => t.Incluido && t.Estado == EstadoEnCentral.AlDia && t.EsDeudor && !t.EsNegativo);
        Titular(p, carla).Should().Match<TitularDeCentral>(t => !t.Incluido && t.MotivoDeExclusion == MotivosDeExclusionDeCentral.NoReportAuthorization);
        Titular(p, _r.Credito).Incluido.Should().BeTrue("el crédito de Ana, desembolsado en la ventana, es una novedad");
        p.CodigoDeSuscriptor.Should().Be("123456", "el de DATACREDITO en Centrales.CodigosDeSuscriptor");
        p.UltimoCargado.Should().BeNull();
    }

    [Fact]
    public async Task El_dato_negativo_exige_aviso_completo_del_episodio_desde_su_fecha_mas_temprana()
    {
        var (sinAviso, conAviso, prematuro) = (Persona("Sinaviso"), Persona("Conaviso"), Persona("Prematuro"));
        var a = await _r.CreditoAsync("CR-NEG-0001", sinAviso, dias: 60);
        var b = await _r.CreditoAsync("CR-NEG-0002", conAviso, dias: 60);
        var c = await _r.CreditoAsync("CR-NEG-0003", prematuro, dias: 60);
        var valido = await AvisoAsync(b, conAviso, 60, Corte.AddDays(-50));
        await AvisoAsync(c, prematuro, 60, Corte.AddDays(-5));

        var p = await ProyectarAsync();

        Titular(p, a).Should().Match<TitularDeCentral>(t => !t.Incluido && t.MotivoDeExclusion == MotivosDeExclusionDeCentral.NoPriorNoticeEvidence && t.EsNegativo);
        Titular(p, b).Should().Match<TitularDeCentral>(t => t.Incluido && t.EsNegativo && t.Estado == EstadoEnCentral.EnMora && t.AvisoPrevioId == valido.Id
                                                            && t.InicioDelEpisodio == Corte.AddDays(-60) && t.DiasDeMora == 60);
        Titular(p, c).Should().Match<TitularDeCentral>(t => !t.Incluido && t.MotivoDeExclusion == MotivosDeExclusionDeCentral.NoPriorNoticeEvidence,
            "el aviso permite reportar 20 días después de su constancia, y eso cae después del corte");
        p.Alertas.Where(x => x.TypeCode == TiposDeAlerta.AvisoPrevioSinConstancia).Select(x => x.Subject)
            .Should().BeEquivalentTo(["Dato negativo sin aviso previo con constancia: crédito CR-NEG-0001", "Dato negativo sin aviso previo con constancia: crédito CR-NEG-0003"]);
        p.Alertas.Should().OnlyContain(x => x.DedupKey!.StartsWith($"{TiposDeAlerta.AvisoPrevioSinConstancia}:{Datacredito}:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task La_reclamacion_al_seguro_la_insolvencia_y_la_pagaduria_dentro_del_umbral_excluyen_el_dato_negativo()
    {
        var (fallecido, insolvente, libranza, alDia) = (Persona("Fallecido"), Persona("Insolvente"), Persona("Libranza"), Persona("Aldia"));
        var a = await _r.CreditoAsync("CR-EXC-0001", fallecido, dias: 60);
        var b = await _r.CreditoAsync("CR-EXC-0002", insolvente, dias: 60);
        var c = await _r.CreditoAsync("CR-EXC-0003", libranza, dias: 60);
        var d = await _r.CreditoAsync("CR-EXC-0004", alDia);
        foreach (var (credito, persona) in new[] { (a, fallecido), (b, insolvente), (c, libranza) }) await AvisoAsync(credito, persona, 60, Corte.AddDays(-50));
        _r.Db.InsuranceClaims.Add(new InsuranceClaim
        {
            LoanId = a.Id, InsurerPersonId = _r.Ana.Id, DeceasedPersonId = fallecido.Id, DeathDate = new DateOnly(2026, 11, 2), CreatedBy = "test",
        });
        _r.Db.InsolvencyProceedings.Add(new InsolvencyProceeding { PersonId = insolvente.Id, StartDate = new DateOnly(2026, 11, 15), CreatedBy = "test" });
        _r.Db.InsolvencyProceedings.Add(new InsolvencyProceeding { PersonId = alDia.Id, StartDate = new DateOnly(2026, 11, 15), CreatedBy = "test" });
        _r.Db.PayrollDeductionResults.Add(new PayrollDeductionResult
        {
            PayrollDeductionFileLineId = 1, PayrollDeductionFileId = 1, LoanId = c.Id, InboundMessageId = 1, Outcome = PayrollResultOutcome.Deducted,
            DeductedAmount = 80_000m, DeductionDate = new DateOnly(2026, 12, 20), TransferDueDate = new DateOnly(2026, 12, 30), PaymasterArrearsThresholdDays = 30,
            CreatedBy = "test",
        });
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();

        var p = await ProyectarAsync();

        Titular(p, a).MotivoDeExclusion.Should().Be(MotivosDeExclusionDeCentral.DeceasedClaimInProcess);
        Titular(p, b).MotivoDeExclusion.Should().Be(MotivosDeExclusionDeCentral.Insolvency);
        Titular(p, c).MotivoDeExclusion.Should().Be(MotivosDeExclusionDeCentral.PaymasterArrearsWithinThreshold);
        Titular(p, d).Incluido.Should().BeTrue("la insolvencia sólo detiene el dato negativo; al día se reporta");
        p.Alertas.Should().BeEmpty("ninguno de los excluidos lo está por falta de aviso");
    }

    [Fact]
    public async Task El_reclamo_en_tramite_va_incluido_con_su_marca()
    {
        var reclamante = Persona("Reclamante");
        var credito = await _r.CreditoAsync("CR-REC-0001", reclamante);
        _r.Db.CreditReportClaims.Add(new CreditReportClaim
        {
            PersonId = reclamante.Id, LoanId = credito.Id, Kind = CreditReportClaimKind.Claim, ReceivedOn = new DateOnly(2026, 12, 10), Channel = "Oficina",
            Description = "No reconoce la mora", DueDate = new DateOnly(2026, 12, 31), CreatedBy = "test",
        });
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();

        var p = await ProyectarAsync();

        var t = Titular(p, credito);
        (t.Incluido, t.ReclamoEnTramite).Should().Be((true, true));
        var layout = CatalogoDeLayoutsDeCartera.Embebido.Vigente(RegulatoryFileKind.CreditBureau, Datacredito, Corte)!.Layout;
        var filas = FilasDeCentral.Construir(layout, p.Archivo, p.Titulares);
        FilaDe(filas, "CR-REC-0001").Valor("Reporte.MarcaDeReclamo").Should().Be("R");
        filas.First().Registro.Should().Be(FilasDeCentral.Cabecera);
        filas.Last().Registro.Should().Be(FilasDeCentral.Totales);
        filas.Last().Valor("Totales.Registros").Should().Be(p.Titulares.Count(x => x.Incluido));
    }

    [Fact]
    public async Task Lo_reportado_sin_aviso_en_un_archivo_anterior_se_retira()
    {
        var (enMora, alDia) = (Persona("Moroso"), Persona("Puntual"));
        var a = await _r.CreditoAsync("CR-RET-0001", enMora, dias: 60);
        var b = await _r.CreditoAsync("CR-RET-0002", alDia);
        await CargadoDeNoviembreAsync(Reportado(a, dias: 30, negativo: true), Reportado(b, dias: 15, negativo: true));

        var p = await ProyectarAsync();

        Titular(p, a).Should().Match<TitularDeCentral>(t => t.Incluido && t.RetiroSinAviso && t.Estado == EstadoEnCentral.Retirado && !t.EsNegativo
                                                            && t.DiasDeMora == 0, "hoy tampoco tiene aviso: se retira sin dato negativo");
        Titular(p, b).Should().Match<TitularDeCentral>(t => t.Incluido && t.RetiroSinAviso && t.Estado == EstadoEnCentral.AlDia);
        p.UltimoCargado!.FilingNumber.Should().Be("RAD-NOV");
        var layout = CatalogoDeLayoutsDeCartera.Embebido.Vigente(RegulatoryFileKind.CreditBureau, Datacredito, Corte)!.Layout;
        FilaDe(FilasDeCentral.Construir(layout, p.Archivo, p.Titulares), "CR-RET-0001").Valor("Reporte.MarcaDeRetiro").Should().Be("X");
    }

    [Fact]
    public async Task En_el_primer_archivo_de_la_central_se_retira_lo_migrado_sin_aviso()
    {
        var migrado = Persona("Migrado");
        var parte = new MigrationLoadPart { MigrationLoadId = 77, PartKey = "bureau-reports", FileName = "reportes.xlsx", CreatedBy = "test" };
        _r.Db.MigrationLoadParts.Add(parte);
        await _r.Db.SaveChangesAsync();
        var credito = await _r.CreditoAsync("MIG-000123", migrado, ajustar: l => { l.Origin = LoanOrigin.Migration; l.MigrationLoadId = 77; });
        _r.Db.MigrationBureauReportRows.Add(new MigrationBureauReportRow
        {
            MigrationLoadPartId = parte.Id, RowNumber = 1, SourceLoanNumber = "MIG-000123", BureauCode = Datacredito, LastReportedYear = 2026, LastReportedMonth = 10,
            LastReportedStatus = "06", IsNegative = true, EpisodeStartDate = new DateOnly(2026, 9, 1), WithPriorNotice = false, CreatedBy = "test",
        });
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();

        Titular(await ProyectarAsync(), credito).RetiroSinAviso.Should().BeTrue("el primer archivo de DATACREDITO retira lo que la migración trajo sin aviso");
        Titular(await ProyectarAsync("TRANSUNION"), credito).RetiroSinAviso.Should().BeFalse("el reporte migrado era de otra central");
    }

    [Fact]
    public async Task Lo_cerrado_antes_de_la_ventana_vuelve_solo_con_su_dato_negativo_dentro_de_la_permanencia()
    {
        var (dentro, fuera, limpio, enLaVentana) = (Persona("Dentro"), Persona("Fuera"), Persona("Limpio"), Persona("Ventana"));
        void Pagado(Loan l, DateOnly el) { l.Status = LoanStatus.PaidOff; l.PaidOffOn = el; }
        var a = await _r.CreditoAsync("CR-PER-0001", dentro, estado: LoanStatus.PaidOff, ajustar: l => Pagado(l, new DateOnly(2026, 11, 10)));
        var b = await _r.CreditoAsync("CR-PER-0002", fuera, estado: LoanStatus.PaidOff, ajustar: l => Pagado(l, new DateOnly(2026, 11, 10)));
        var c = await _r.CreditoAsync("CR-PER-0003", limpio, estado: LoanStatus.PaidOff, ajustar: l => Pagado(l, new DateOnly(2026, 11, 20)));
        var d = await _r.CreditoAsync("CR-PER-0004", enLaVentana, estado: LoanStatus.PaidOff, ajustar: l => Pagado(l, new DateOnly(2026, 12, 5)));
        var anterior = await CargadoDeNoviembreAsync(Reportado(a, dias: 60, negativo: true, aviso: 1), Reportado(b, dias: 10, negativo: true, aviso: 2),
            Reportado(c));

        var p = await ProyectarAsync();

        // 60 días de mora por 2 veces = 120 días desde el 10 de noviembre: hasta el 10 de marzo, dentro del tope de 48 meses.
        Titular(p, a).Should().Match<TitularDeCentral>(t => t.Incluido && t.Estado == EstadoEnCentral.Cancelada && t.SaldoTotal == 0m);
        p.Titulares.Should().NotContain(t => t.LoanId == b.Id, "10 días por 2 veces vencieron el 30 de noviembre");
        p.Titulares.Should().NotContain(t => t.LoanId == c.Id, "cancelado antes de la ventana sin dato negativo: ya se reportó");
        Titular(p, d).Estado.Should().Be(EstadoEnCentral.Cancelada, "la cancelación de la ventana es una novedad");
        p.UltimoCargado!.Id.Should().Be(anterior.Id);
    }

    [Fact]
    public async Task Los_codeudores_se_reportan_con_su_propia_autorizacion()
    {
        var (deudor, conAutorizacion, sinAutorizacion) = (Persona("Deudor"), Persona("Codeudor"), Persona("Codeudora"));
        var credito = await _r.CreditoAsync("CR-COD-0001", deudor);
        await CodeudorAsync(credito, conAutorizacion);
        await CodeudorAsync(credito, sinAutorizacion);
        _r.D.O.QuitarAutorizacion(sinAutorizacion, DataAuthorizationPurpose.CreditBureauReport);

        var p = await ProyectarAsync();

        Titular(p, credito).Should().Match<TitularDeCentral>(t => t.Incluido && t.EsDeudor);
        Titular(p, credito, conAutorizacion).Should().Match<TitularDeCentral>(t => t.Incluido && !t.EsDeudor);
        Titular(p, credito, sinAutorizacion).MotivoDeExclusion.Should().Be(MotivosDeExclusionDeCentral.NoReportAuthorization);
        var layout = CatalogoDeLayoutsDeCartera.Embebido.Vigente(RegulatoryFileKind.CreditBureau, Datacredito, Corte)!.Layout;
        var filas = FilasDeCentral.Construir(layout, p.Archivo, p.Titulares).Where(f => f.Credito == "CR-COD-0001").ToList();
        filas.Select(f => f.Valor("Titular.Calidad")).Should().Equal("00", "01");
    }

    [Fact]
    public async Task Un_credito_anulado_desde_el_origen_ya_reportado_sale_como_retiro_una_sola_vez()
    {
        var (comprador, codeudor, otro) = (Persona("Comprador"), Persona("Fiador"), Persona("Otro"));
        void Anulado(Loan l) { l.Status = LoanStatus.Voided; l.VoidedOn = new DateOnly(2026, 12, 3); l.VoidReason = "Venta anulada por rechazo de la DIAN"; l.Origin = LoanOrigin.Commercial; }
        var reportado = await _r.CreditoAsync("VTA-0001", comprador, estado: LoanStatus.Voided, ajustar: Anulado);
        await CodeudorAsync(reportado, codeudor);
        var nunca = await _r.CreditoAsync("VTA-0002", otro, estado: LoanStatus.Voided, ajustar: Anulado);
        var reemplazo = await _r.CreditoAsync("VTA-0003", otro, ajustar: l => { l.Origin = LoanOrigin.Commercial; l.OriginLoanId = nunca.Id; l.OriginLinkKind = LoanLinkKind.Replacement; });
        await CargadoDeNoviembreAsync(Reportado(reportado), Reportado(reportado, codeudor));

        var p = await ProyectarAsync();

        p.Titulares.Where(t => t.LoanId == reportado.Id).Should().HaveCount(2)
            .And.OnlyContain(t => t.Incluido && t.AnuladoDesdeElOrigen && t.Estado == EstadoEnCentral.Retirado && !t.EsNegativo && t.SaldoTotal == 0m);
        p.Titulares.Should().NotContain(t => t.LoanId == nunca.Id, "lo anulado que nunca se reportó no aparece");
        Titular(p, reemplazo).Incluido.Should().BeTrue("del anulado con reemplazo se reporta sólo el crédito nuevo");

        // Marcado cargado el archivo de diciembre con los retiros, el de enero ya no los repite.
        var diciembre = RegulatoryFile.Nueva(RegulatoryFileKind.CreditBureau, Datacredito, 2026, 12, 1, "CENTRAL-DATACREDITO-v1", true, null, false, "[]", 0, 0,
            "[]", 0, 2, "{}", "ARG-000901_DATACREDITO_2026-12_v1.txt", new string('b', 64), Guid.NewGuid(), _r.D.Cajera.Id, _r.G.UtcNow);
        diciembre.MarcarCargado(_r.D.Cajera.Id, _r.G.UtcNow, "RAD-DIC", new DateOnly(2027, 1, 10));
        _r.Db.RegulatoryFiles.Add(diciembre);
        await _r.Db.SaveChangesAsync();
        _r.Db.RegulatoryFileLines.Add(new RegulatoryFileLine { RegulatoryFileId = diciembre.Id, LineNumber = 1, LoanId = reportado.Id, PersonId = comprador.Id, Included = true, ReportedStatusCode = "99" });
        _r.Db.RegulatoryFileLines.Add(new RegulatoryFileLine { RegulatoryFileId = diciembre.Id, LineNumber = 2, LoanId = reportado.Id, PersonId = codeudor.Id, Included = true, ReportedStatusCode = "99" });
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();

        var enero = (await _r.Centrales().ProyectarAsync(Datacredito, 2027, 1, default)).Value;
        enero.UltimoCargado!.Id.Should().Be(diciembre.Id);
        enero.Titulares.Should().NotContain(t => t.LoanId == reportado.Id, "el retiro ya se cargó: el crédito anulado no vuelve a reportarse");
    }

    [Fact]
    public async Task Un_archivo_generado_y_no_cargado_no_mueve_la_ventana()
    {
        var pagado = Persona("Pagado");
        var credito = await _r.CreditoAsync("CR-VEN-0001", pagado, estado: LoanStatus.PaidOff,
            ajustar: l => { l.PaidOffOn = new DateOnly(2026, 11, 20); });
        var generado = RegulatoryFile.Nueva(RegulatoryFileKind.CreditBureau, Datacredito, 2026, 11, 1, "CENTRAL-DATACREDITO-v1", true, null, false, "[]", 0, 0,
            "[]", 0, 0, "{}", "ARG-000902_DATACREDITO_2026-11_v1.txt", new string('c', 64), Guid.NewGuid(), _r.D.Cajera.Id, _r.G.UtcNow);
        _r.Db.RegulatoryFiles.Add(generado);
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();

        var p = await ProyectarAsync();

        p.UltimoCargado.Should().BeNull("sólo un archivo Uploaded marca desde dónde se cuentan las novedades");
        p.Titulares.Should().NotContain(t => t.LoanId == credito.Id, "sin archivo cargado la ventana es el mes del período");
    }
}
