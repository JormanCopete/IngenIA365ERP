using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Novelties.ImportNovelties;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Storage.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Novelties;

/// <summary>T116 — FR-006: la importación es todo o nada, con errores por fila y columna, y deja el mismo rastro que el registro manual.</summary>
public class ImportNoveltiesCommandHandlerTests
{
    private static ImportNoveltiesCommandHandler Handler(NominaTestData d) =>
        new(d.Db, new CsvNoveltyFileParser(), d.Clock, d.User, d.StaleMarker, d.CarryOver);

    private static ImportNoveltiesCommand Comando(NominaTestData d, string csv, string nombre = "novedades.csv")
    {
        var bytes = new UTF8Encoding(true).GetBytes(csv);
        return new ImportNoveltiesCommand(d.Marzo.PublicId, new MemoryStream(bytes), nombre, bytes.Length);
    }

    private static string Documento(NominaTestData d, Domain.Entities.Payroll.Employee e) =>
        d.Db.People.Single(p => p.Id == e.PersonId).TaxId;

    [Fact]
    public async Task Un_archivo_valido_crea_las_novedades_con_origen_Import_y_el_mismo_lote()
    {
        var d = new NominaTestData();
        var bruno = d.Empleado("Bruno", 3_000_000m, new DateTime(2024, 2, 1));
        var run = d.Borrador(d.Marzo);
        var csv = "documento;concepto;cantidad;valor;desde;hasta;observacion\n" +
                  $"{Documento(d, d.Ana)};HEX_NOCTURNA;6;;;;Turno del 12\n" +
                  $"{Documento(d, bruno)};INCAP_GENERAL;;;2026-03-28;2026-04-03;Incapacidad general\n" +
                  $"{Documento(d, bruno)};HEX_NOCTURNA;2,5;;;;\n";

        var r = await Handler(d).Handle(Comando(d, csv), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.HasErrors.Should().BeFalse();
        r.Value.Applied.Should().Be(3);
        var novedades = await d.Db.PayrollNovelties.Where(n => n.PayPeriodId == d.Marzo.Id).ToListAsync();
        novedades.Where(n => n.Origin == NoveltyOrigin.Import).Should().HaveCount(3)
            .And.OnlyContain(n => n.ImportBatchId == r.Value.BatchId && n.Status == NoveltyStatus.Active && n.CreatedBy == "ana@demo");
        novedades.Single(n => n.ConceptCode == "INCAP_GENERAL").DaysInPeriod.Should().BeInRange(3, 4, "28..31 de marzo en calendario comercial");
        novedades.Single(n => n.ConceptCode == "INCAP_GENERAL").CarryOverDays.Should().Be(3, "1..3 de abril se trasladan");
        novedades.Single(n => n.ConceptCode == "HEX_NOCTURNA" && n.EmployeeId == bruno.Id).Quantity.Should().Be(2.5m);
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Stale, "el borrador queda desactualizado como con una novedad manual");
    }

    [Fact]
    public async Task Dos_filas_invalidas_dejan_todo_sin_aplicar_y_nombran_fila_y_columna()
    {
        var d = new NominaTestData();
        var csv = "documento;concepto;cantidad;valor;desde;hasta;observacion\n" +
                  $"{Documento(d, d.Ana)};HEX_NOCTURNA;6;;;;\n" +
                  "999999;HEX_NOCTURNA;1;;;;\n" +
                  $"{Documento(d, d.Ana)};NO_EXISTE;1;;;;\n" +
                  $"{Documento(d, d.Ana)};HEX_NOCTURNA;abc;;;;\n";

        var r = await Handler(d).Handle(Comando(d, csv), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Applied.Should().Be(0);
        r.Value.HasErrors.Should().BeTrue();
        r.Value.Code.Should().Be("Payroll.ImportInvalid");
        // El parser corta antes: «abc» no es número, y el archivo no se sigue evaluando fila a fila.
        r.Value.Errors.Should().ContainSingle(e => e.Row == 5 && e.Column == "cantidad");
        (await d.Db.PayrollNovelties.CountAsync()).Should().Be(0, "nada persistido");

        // Sin errores de formato, los errores de negocio se reportan todos, con su fila.
        var csv2 = "documento;concepto;cantidad;valor;desde;hasta;observacion\n" +
                   $"{Documento(d, d.Ana)};HEX_NOCTURNA;6;;;;\n" +
                   "999999;HEX_NOCTURNA;1;;;;\n" +
                   $"{Documento(d, d.Ana)};NO_EXISTE;1;;;;\n" +
                   $"{Documento(d, d.Ana)};LIC_MATERNIDAD;;;2026-03-01;2026-03-15;\n" +
                   $"{Documento(d, d.Ana)};LIC_MATERNIDAD;;;2026-03-20;2026-03-25;\n";
        var r2 = await Handler(d).Handle(Comando(d, csv2), CancellationToken.None);
        r2.Value.Applied.Should().Be(0);
        r2.Value.Errors.Select(e => (e.Row, e.Column)).Should().BeEquivalentTo([(3, "documento"), (4, "concepto"), (6, "concepto")]);
        r2.Value.Errors.Single(e => e.Row == 6).Message.Should().Contain("más de una vez", "la licencia de maternidad no admite repetirse en el período");
        (await d.Db.PayrollNovelties.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_las_columnas_de_la_plantilla_no_se_intenta_nada()
    {
        var d = new NominaTestData();
        var r = await Handler(d).Handle(Comando(d, "cedula;codigo\n123;HEX_NOCTURNA\n"), CancellationToken.None);

        r.Value.Applied.Should().Be(0);
        r.Value.Errors.Should().Contain(e => e.Row == 1 && e.Column == "documento").And.Contain(e => e.Row == 1 && e.Column == "concepto");
    }

    [Fact]
    public async Task Un_archivo_de_mas_de_5_MB_se_rechaza_sin_leerlo()
    {
        var d = new NominaTestData();
        var cmd = new ImportNoveltiesCommand(d.Marzo.PublicId, new MemoryStream([1, 2, 3]), "grande.csv", 6 * 1024 * 1024);

        var r = await Handler(d).Handle(cmd, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ImportFileTooLarge");
    }

    [Fact]
    public async Task Un_periodo_aprobado_no_admite_importacion()
    {
        var d = new NominaTestData();
        d.Marzo.Status = PayPeriodStatus.Approved;
        await d.Db.SaveChangesAsync();

        var r = await Handler(d).Handle(Comando(d, $"documento;concepto;cantidad;valor;desde;hasta;observacion\n{Documento(d, d.Ana)};HEX_NOCTURNA;1;;;;\n"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.PeriodApproved");
    }

    [Fact]
    public void La_plantilla_tiene_los_encabezados_que_el_parser_espera()
    {
        var parser = new CsvNoveltyFileParser();
        var plantilla = parser.Template();

        var resultado = parser.Parse(new MemoryStream(plantilla));

        resultado.Errors.Should().BeEmpty();
        resultado.Rows.Should().HaveCount(3);
        resultado.Rows[0].ConceptCode.Should().Be("HEX_NOCTURNA");
        resultado.Rows[2].StartDate.Should().Be(new DateTime(2026, 3, 10));
    }

    [Fact]
    public void El_parser_acepta_coma_o_punto_decimal_y_dos_formatos_de_fecha()
    {
        var parser = new CsvNoveltyFileParser();
        var csv = "Documento;Concepto;Cantidad;Valor;Desde;Hasta;Observación\n" +
                  "1;A;1.234,5;1234.5;10/03/2026;2026-03-14;x\n" +
                  "2;B;;;31/02/2026;;\n";

        var r = parser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(csv)));

        r.Rows.Should().HaveCount(2);
        r.Rows[0].Quantity.Should().Be(1234.5m);
        r.Rows[0].Amount.Should().Be(1234.5m);
        r.Rows[0].StartDate.Should().Be(new DateTime(2026, 3, 10));
        r.Rows[0].EndDate.Should().Be(new DateTime(2026, 3, 14));
        r.Errors.Should().ContainSingle(e => e.Row == 3 && e.Column == "desde");
    }
}
