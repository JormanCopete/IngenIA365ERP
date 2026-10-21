using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Agreements;
using IngenIA365ERP.Application.Lending.Payroll.Files;
using IngenIA365ERP.Application.Lending.Payroll.Results;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 014, T091 (decisión del dueño del 2026-10-18, D36; contrato-de-cuentas §6.4; mensajes.md §5.4 y §5.7 de la 014): un convenio con grupos de archivo
/// genera un archivo por grupo, con el formato del grupo y el código de la pagaduría en cada línea, y viaja como <c>DescuentosPorPagaduria</c> v2 con los
/// totales por código. La respuesta v2 trae lo descontado por empleado y por código: se reparte entre las líneas del archivo con la regla del código (lo que
/// no alcanza queda no descontado) y lo que no casa —un empleado que el archivo no tiene, un código que no le pidió— queda en «no aplicados» sin bloquear
/// lo demás. (nuevo)
/// </summary>
public sealed class ArchivoPorGrupoTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateOnly Diciembre = new(2026, 12, 1);
    private static readonly DateOnly Enero = new(2027, 1, 1);
    private static readonly DateOnly Descuento = new(2027, 1, 30);

    private LibranzaDePrueba _l = null!;
    private Loan _beto = null!;

    public async Task InitializeAsync()
    {
        _l = await LibranzaDePrueba.CrearAsync();
        _beto = await _l.OtroCreditoAsync(_l.D.O.Beto, 1_200_000m, "EMP-0002");
        var ida = _l.Formato("GRUPO-IDA", BankFileScope.PayrollDeductionRequest, ("Empleado", "PayerEmployeeId"), ("Codigo", "PaymasterConceptCode"),
            ("Credito", "LoanNumber"), ("Cuenta", "AccountNumber"), ("Valor", "Amount"), ("Novedad", "NoveltyKind"));
        var vuelta = _l.Formato("GRUPO-VUELTA", BankFileScope.PayrollDeductionResult, ("Empleado", "PayerEmployeeId"), ("Codigo", "PaymasterConceptCode"),
            ("Descontado", "DeductedAmount"), ("Fecha", "DeductionDate"));
        var copia = new CopiaDeVersionDeConvenio(_l.Db, _l.Alta());
        var grupos = await new SetPayrollAgreementFileGroupsCommandHandler(_l.Db, copia).Handle(new SetPayrollAgreementFileGroupsCommand(_l.Convenio.PublicId,
            Diciembre,
            [
                new PayrollFileGroupInput("CRED", "Créditos", ida.PublicId, vuelta.PublicId),
                new PayrollFileGroupInput("APORTES", "Aportes y ahorros", ida.PublicId, vuelta.PublicId, CutoffDay: 15),
            ], "La pagaduría separa los archivos") { OperationKey = Guid.NewGuid() }, default);
        grupos.IsSuccess.Should().BeTrue(grupos.IsFailure ? $"{grupos.Error.Code}: {grupos.Error.Message}" : string.Empty);
        var equivalencias = await new SetPayrollAgreementConceptEquivalencesCommandHandler(_l.Db, copia).Handle(new SetPayrollAgreementConceptEquivalencesCommand(
            _l.Convenio.PublicId, Enero,
            [
                new PayrollConceptEquivalenceInput("101", "Cuota de crédito", ConceptAllocationRule.Priority,
                    [new PayrollEquivalenceItemInput(LineNature.Placement, "*", "Installment")], "CRED"),
                new PayrollConceptEquivalenceInput("201", "Aportes y ahorro permanente", ConceptAllocationRule.Proportional,
                    [new PayrollEquivalenceItemInput(LineNature.Contribution, "*", "Installment")], "APORTES"),
            ], "Equivalencias de la pagaduría") { OperationKey = Guid.NewGuid() }, default);
        equivalencias.IsSuccess.Should().BeTrue(equivalencias.IsFailure ? $"{equivalencias.Error.Code}: {equivalencias.Error.Message}" : string.Empty);
        _l.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private async Task<IngenIA365ERP.Application.Common.Models.Result<PayrollDeductionFileDto>> GenerarAsync(string? grupo)
    {
        _l.G.ActuarComo(_l.D.Cajera);
        var r = await _l.Generar().Handle(new GeneratePayrollDeductionFileCommand(_l.Convenio.PublicId, 2027, 1, FileGroupCode: grupo)
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    private static byte[] Respuesta(params (string Empleado, string Codigo, decimal Descontado)[] filas) =>
        Encoding.UTF8.GetBytes(string.Join("\n", filas.Select(f =>
            $"{f.Empleado};{f.Codigo};{f.Descontado.ToString("0.00", CultureInfo.InvariantCulture)};{Descuento:yyyyMMdd}")) + "\n");

    [Fact]
    public async Task Con_grupos_hay_que_decir_cual_y_el_grupo_tiene_que_existir()
    {
        (await GenerarAsync(null)).Error.Code.Should().Be(ErroresDeLibranza.CodigoGrupoRequerido);
        (await GenerarAsync("OTRO")).Error.Code.Should().Be(ErroresDeLibranza.CodigoGrupoInexistente);
        (await GenerarAsync("APORTES")).Error.Code.Should().Be(ErroresDeLibranza.CodigoArchivoVacio, "ningún asociado tiene cuotas de aportes con esta pagaduría");
    }

    [Fact]
    public async Task El_archivo_del_grupo_lleva_su_formato_el_codigo_de_cada_linea_y_viaja_como_v2()
    {
        var r = await GenerarAsync("cred");

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.FileGroupCode.Should().Be("CRED");
        var archivo = await _l.Db.PayrollDeductionFiles.AsNoTracking().SingleAsync(f => f.PublicId == r.Value.PublicId);
        (await _l.Db.PayrollDeductionFileLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == archivo.Id).Select(l => l.PaymasterConceptCode).ToListAsync())
            .Should().Equal("101", "101");
        Encoding.UTF8.GetString(_l.Subidas.Single().Content).Should().Contain("EMP-0001;101;" + _l.Credito.Number, "escrito con el formato del grupo");

        var m = await _l.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.OriginPublicId == r.Value.PublicId && x.Type == DescuentosPorPagaduriaV2.Type);
        m.Version.Should().Be(2);
        var v2 = JsonSerializer.Deserialize<DescuentosPorPagaduriaV2>(m.PayloadJson!, Json)!;
        v2.FileGroup.Code.Should().Be("CRED");
        v2.Lines.Should().OnlyContain(l => l.Kind == "Loan" && l.PaymasterConceptCode == "101");
        v2.ConceptTotals.Should().ContainSingle().Which.Should().BeEquivalentTo(new { PaymasterConceptCode = "101", Lines = 2, Amount = r.Value.Total });

        (await GenerarAsync("CRED")).Value.Version.Should().Be(2, "regenerar el mismo grupo crea la versión siguiente");
    }

    [Fact]
    public async Task La_respuesta_por_codigo_se_reparte_y_lo_que_no_casa_queda_en_no_aplicados()
    {
        var archivo = (await GenerarAsync("CRED")).Value;
        var ana = await _l.LineaAsync(archivo.PublicId, _l.Credito);
        var parcial = Math.Round(ana.TotalAmount / 2m, 2);

        var c = await _l.ResultadoYConsumoAsync(archivo.PublicId, Respuesta(("EMP-0001", "101", parcial), ("EMP-0002", "999", 100m), ("EMP-9999", "101", 5_000m)));

        c.Should().BeOfType<ResultadoDeConsumo.Processed>();
        var resultado = await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync(x => x.PayrollDeductionFileLineId == ana.Id);
        (resultado.Outcome, resultado.DeductedAmount, resultado.NotDeductedAmount)
            .Should().Be((PayrollResultOutcome.PartiallyDeducted, parcial, ana.TotalAmount - parcial), "lo que no alcanza queda no descontado");
        (await _l.Db.InstallmentNonDeductions.AsNoTracking().CountAsync(n => n.LoanId == _l.Credito.Id)).Should().Be(1);
        (await _l.Db.PayrollDeductionResults.AsNoTracking().AnyAsync(x => x.LoanId == _beto.Id)).Should().BeFalse("el código 999 no se le pidió a Beto");

        var noAplicadas = await _l.Db.PayrollDeductionUnmatchedLines.AsNoTracking().OrderBy(u => u.PayerEmployeeId).ToListAsync();
        noAplicadas.Select(u => (u.PayerEmployeeId, u.ReasonCode)).Should().Equal(
            ("EMP-0002", ErroresDeLibranza.CodigoConceptoSinEquivalencia), ("EMP-9999", ErroresDeLibranza.CodigoEmpleadoSinEquivalencia));
        var leidas = await new ListPayrollDeductionUnmatchedLinesQueryHandler(_l.Db, _l.G.Alcance, _l.G.Auditoria)
            .Handle(new ListPayrollDeductionUnmatchedLinesQuery(archivo.PublicId), default);
        leidas.Value.Should().HaveCount(2);
    }

    [Fact]
    public void El_reparto_por_codigo_sigue_la_prioridad_y_avisa_lo_que_sobra()
    {
        var lineas = new[]
        {
            new LineaPorRepartir(1, false, "E1", "201", 2, 100m),
            new LineaPorRepartir(2, true, "E1", "201", 1, 50m),
            new LineaPorRepartir(3, true, "E2", "201", 1, 30m),
        };
        var reglas = new Dictionary<string, ConceptAllocationRule> { ["201"] = ConceptAllocationRule.Priority };

        var r = RepartoDeRespuestaDePagaduria.Repartir(
        [
            new DeductionConceptLineV2 { PayerEmployeeId = "e1", PaymasterConceptCode = "201", Expected = 150m, Deducted = 80m },
            new DeductionConceptLineV2 { PayerEmployeeId = "E2", PaymasterConceptCode = "201", Expected = 30m, Deducted = 40m },
        ], lineas, reglas);

        r.Asignaciones.Select(a => (a.Linea.Id, a.Asignado)).Should().BeEquivalentTo([(1, 30m), (2, 50m), (3, 30m)],
            "primero la de prioridad 1 (la cuota de la cuenta), el resto a la siguiente");
        r.NoAplicadas.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Motivo = ErroresDeLibranza.CodigoDescuentoSobrante, Descontado = 10m });
    }
}
