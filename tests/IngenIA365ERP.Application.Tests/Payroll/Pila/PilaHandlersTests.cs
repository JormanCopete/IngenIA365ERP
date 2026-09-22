using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Pila;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Pila;

/// <summary>Validación previa (FR-025): sin guardar, con inconsistencias enlazadas.</summary>
public class ValidatePilaQueryHandlerTests
{
    [Fact]
    public async Task Fichas_completas_validan_limpio_salvo_las_alertas_del_layout()
    {
        var f = new PilaDePrueba();
        var r = await f.Validador.Handle(new ValidatePilaQuery(2026, 12), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.CanGenerate.Should().BeTrue(string.Join("; ", r.Value.Issues.Select(i => i.Message)));
        r.Value.Blocking.Should().Be(0);
        r.Value.Issues.Should().Contain(i => i.Code == "Pila.LayoutSinCotejar" && i.Severity == PilaIssueSeverity.Warning);
        r.Value.Contributors.Should().Be(2);
        r.Value.Lines.Should().Be(2);
        r.Value.Sources.Should().HaveCount(1);
        (await f.D.Db.PilaGenerations.CountAsync()).Should().Be(0, "validar no guarda");
    }

    [Fact]
    public async Task Sin_eps_en_la_ficha_es_bloqueante_con_enlace_a_la_ficha()
    {
        var f = new PilaDePrueba();
        f.Ana.HealthInsuranceId = 0;
        await f.D.Db.SaveChangesAsync();
        var r = await f.Validador.Handle(new ValidatePilaQuery(2026, 12), CancellationToken.None);
        r.Value.CanGenerate.Should().BeFalse();
        var issue = r.Value.Issues.Single(i => i.Code == "Pila.SinEps");
        issue.EmployeePublicId.Should().Be(f.Ana.PublicId);
        issue.Link.Should().Be($"/nomina/empleados/{f.Ana.PublicId}");
    }

    [Fact]
    public async Task Segundo_apellido_faltante_es_alerta_y_no_impide_generar()
    {
        var f = new PilaDePrueba();
        var p = f.D.Db.People.Single(x => x.Id == f.Ana.PersonId);
        p.SecondLastName = null;
        await f.D.Db.SaveChangesAsync();
        var r = await f.Validador.Handle(new ValidatePilaQuery(2026, 12), CancellationToken.None);
        r.Value.CanGenerate.Should().BeTrue();
        r.Value.Issues.Should().Contain(i => i.Code == "Pila.SegundoApellidoFaltante" && i.Severity == PilaIssueSeverity.Warning);
    }

    [Fact]
    public async Task Sin_corridas_aprobadas_del_mes_responde_NoApprovedRuns()
    {
        var f = new PilaDePrueba();
        var r = await f.Validador.Handle(new ValidatePilaQuery(2026, 11), CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Pila.NoApprovedRuns");
    }

    [Fact]
    public async Task Datos_del_aportante_incompletos_son_bloqueantes()
    {
        var f = new PilaDePrueba(conAjustes: false);
        var r = await f.Validador.Handle(new ValidatePilaQuery(2026, 12), CancellationToken.None);
        r.Value.CanGenerate.Should().BeFalse();
        r.Value.Issues.Should().Contain(i => i.Code == "Pila.AportanteIncompleto");
    }
}

/// <summary>Generación versionada (FR-026), exoneración por política, cuadre (FR-027) y archivo guardado antes de la fila.</summary>
public class GeneratePilaCommandHandlerTests
{
    [Fact]
    public async Task Empresa_exonerada_quien_gana_bajo_el_umbral_no_aporta_sena_icbf_ni_salud_empleador()
    {
        var f = new PilaDePrueba(exonerada: true);
        var r = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, AcknowledgeWarnings: true), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(PilaGenerationStatus.Generated);
        r.Value.Lines.Should().Be(2);
        r.Value.Totals.Sena.Should().Be(0m);
        r.Value.Totals.Icbf.Should().Be(0m);
        r.Value.Totals.Health.Should().Be(80_000m + 320_000m, "salud sólo la parte del empleado (4 %)");
        r.Value.Totals.Fsp.Should().Be(80_000m, "Gloria supera 4 SMMLV");
        r.Value.Reconciliation.Balanced.Should().BeTrue(string.Join("; ", r.Value.Reconciliation.BySubsystem.Select(x => $"{x.Subsystem} {x.FileTotal}/{x.LedgerTotal}")));
        r.Value.FileName.Should().Be("PILA_900123456_2026-12_v1.txt");
        f.Subidas.Should().ContainSingle(s => s.OwnerEntityType == "PilaGeneration" && s.FileName == r.Value.FileName);
        var g = await f.D.Db.PilaGenerations.Include(x => x.Lines).Include(x => x.Issues).SingleAsync();
        g.Lines.Should().HaveCount(2);
        g.Lines.Should().OnlyContain(l => l.RecordText.Length == 693 && l.Exempt);
        g.Issues.Should().Contain(i => i.Code == "Pila.LayoutSinCotejar");
        g.FileAttachmentPublicId.Should().NotBeNull();
        g.ExemptionApplied.Should().BeTrue();
    }

    [Fact]
    public async Task Empresa_no_exonerada_aporta_completo_y_cuadra_con_la_nomina()
    {
        var f = new PilaDePrueba(exonerada: false);
        var r = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Totals.Sena.Should().Be(40_000m + 160_000m);
        r.Value.Totals.Icbf.Should().Be(60_000m + 240_000m);
        r.Value.Totals.Health.Should().Be(250_000m + 1_000_000m);
        r.Value.Reconciliation.Balanced.Should().BeTrue();
    }

    [Fact]
    public async Task Con_alertas_sin_reconocer_no_genera()
    {
        var f = new PilaDePrueba();
        var r = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, AcknowledgeWarnings: false), CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Pila.WarningsNotAcknowledged");
    }

    [Fact]
    public async Task Con_bloqueantes_queda_Validated_sin_archivo()
    {
        var f = new PilaDePrueba();
        f.Ana.HealthInsuranceId = 0;
        await f.D.Db.SaveChangesAsync();
        var r = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(PilaGenerationStatus.Validated);
        r.Value.FileName.Should().BeNull();
        f.Subidas.Should().BeEmpty();
        var g = await f.D.Db.PilaGenerations.Include(x => x.Issues).SingleAsync();
        g.BlockingIssueCount.Should().BeGreaterThan(0);
        g.Issues.Should().Contain(i => i.Code == "Pila.SinEps" && i.EmployeeId == f.Ana.Id);
    }

    [Fact]
    public async Task Regenerar_crea_la_version_siguiente_y_deja_la_anterior_Superseded_con_su_archivo()
    {
        var f = new PilaDePrueba();
        var v1 = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
        var v2 = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
        v2.Value.Version.Should().Be(2);
        var anterior = await f.D.Db.PilaGenerations.SingleAsync(g => g.PublicId == v1.Value.GenerationPublicId);
        anterior.Status.Should().Be(PilaGenerationStatus.Superseded);
        anterior.FileAttachmentPublicId.Should().NotBeNull("la versión anterior conserva su archivo");
        (await f.D.Db.PilaGenerations.SingleAsync(g => g.PublicId == v2.Value.GenerationPublicId)).Status.Should().Be(PilaGenerationStatus.Generated);
    }

    [Fact]
    public async Task La_vigente_ya_cargada_en_el_operador_no_se_regenera()
    {
        var f = new PilaDePrueba();
        var v1 = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
        (await f.Marcador().Handle(new MarkPilaUploadedCommand(v1.Value.GenerationPublicId, new DateTime(2027, 1, 5), "PL-77"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var v2 = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
        v2.IsFailure.Should().BeTrue();
        v2.Error.Code.Should().Be("Payroll.Pila.AlreadyUploaded");
    }

    [Fact]
    public async Task Diferencia_con_la_nomina_se_muestra_y_bloquea_la_descarga_sin_reconocerla()
    {
        var f = new PilaDePrueba();
        // La nómina liquidó menos ARL del que el archivo calcula: el cuadre no balancea.
        var arl = await f.D.Db.PayrollRunLines.SingleAsync(l => l.ConceptCode == "ARL" && l.Amount == 10_500m);
        arl.Amount = 10_000m;
        await f.D.Db.SaveChangesAsync();
        var r = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        r.Value.Reconciliation.Balanced.Should().BeFalse();
        r.Value.Reconciliation.BySubsystem.Single(x => x.Subsystem == "Arl").Difference.Should().Be(500m);

        var sender = f.Sender;
        var descarga = await new DownloadPilaFileQueryHandler(f.D.Db, sender).Handle(new DownloadPilaFileQuery(r.Value.GenerationPublicId), CancellationToken.None);
        descarga.IsFailure.Should().BeTrue();
        descarga.Error.Code.Should().Be("Payroll.Pila.Unreconciled");
    }

    [Fact]
    public async Task Sin_parametros_legales_del_mes_responde_ParametersMissing()
    {
        var f = new PilaDePrueba();
        foreach (var p in f.D.Db.PayrollLegalParameters.Where(p => p.Code == "PILA_APORTE_REDONDEO_MULTIPLO")) p.IsDeleted = true;
        await f.D.Db.SaveChangesAsync();
        var r = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Pila.ParametersMissing");
    }
}

public class MarkPilaUploadedCommandHandlerTests
{
    [Fact]
    public async Task Marca_cargada_con_radicado_y_solo_una_Generated()
    {
        var f = new PilaDePrueba();
        var g = await f.Generador().Handle(new GeneratePilaCommand(2026, 12, true), CancellationToken.None);
        var r = await f.Marcador().Handle(new MarkPilaUploadedCommand(g.Value.GenerationPublicId, new DateTime(2027, 1, 6, 10, 0, 0), "PL-2027-000123", new DateOnly(2027, 1, 6), new DateOnly(2027, 1, 8)), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var fila = await f.D.Db.PilaGenerations.SingleAsync();
        fila.Status.Should().Be(PilaGenerationStatus.Uploaded);
        fila.OperatorFilingNumber.Should().Be("PL-2027-000123");
        fila.PaidAt.Should().Be(new DateOnly(2027, 1, 8));
        var otraVez = await f.Marcador().Handle(new MarkPilaUploadedCommand(g.Value.GenerationPublicId, new DateTime(2027, 1, 6), "X"), CancellationToken.None);
        otraVez.Error.Code.Should().Be("Payroll.Pila.NotGenerated");
    }
}
