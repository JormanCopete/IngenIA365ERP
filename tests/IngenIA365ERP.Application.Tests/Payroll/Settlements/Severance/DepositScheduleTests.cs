using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Severance;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Severance;

/// <summary>
/// Feature 010 US2 (T048, FR-012): la relación de consignación por fondo —dos fondos, totales que
/// son la suma de las líneas <c>CESANTIAS</c>, fecha límite desde el parámetro <c>DateInYear</c>—,
/// la lista con los fondos y su estado, y la marca de consignado: sólo sobre la aprobada, una sola
/// vez por fondo (la segunda es 422 <c>Payroll.Severance.AlreadyDeposited</c>), con el valor de la
/// relación guardado y conservada tras reversar.
/// </summary>
public class DepositScheduleTests
{
    [Fact]
    public async Task La_relacion_tiene_un_bloque_por_fondo_y_los_totales_son_la_suma_de_las_lineas_CESANTIAS()
    {
        var e = new EscenarioDeCesantias();
        var calculo = await e.CalcularAsync();

        var r = await e.Relacion().Handle(new GetDepositScheduleQuery(calculo.Value.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var relacion = r.Value;
        relacion.Year.Should().Be(2026);
        relacion.Status.Should().Be("Draft", "la relación se puede revisar antes de aprobar");
        relacion.Funds.Should().HaveCount(2);
        relacion.Funds.Select(f => f.FundName).Should().Equal(["Porvenir", "Proteccion"], "ordenados por nombre del fondo");

        var porvenir = relacion.Funds.Single(f => f.FundPublicId == e.FondoPorvenir.PublicId);
        porvenir.FundNit.Should().Be(e.Porvenir.TaxId, "el NIT es el de la persona del fondo (FR-088)");
        porvenir.Employees.Should().Be(2);
        porvenir.Lines.Select(l => l.Name).Should().Equal(["Alba Prueba", "Gema Prueba"]);
        porvenir.Total.Should().Be(2_749_095m + 666_666.67m);
        porvenir.InterestTotal.Should().Be(329_891.40m + 26_666.67m);
        porvenir.Deposited.Should().BeFalse();
        var gema = porvenir.Lines.Single(l => l.EmployeePublicId == e.G.PublicId);
        gema.HireDate.Should().Be(new DateOnly(2026, 9, 1));
        gema.Days.Should().Be(120m);
        gema.BaseSalary.Should().Be(2_000_000m, "mínimo más auxilio");
        gema.DocumentType.Should().Be("C");

        var proteccion = relacion.Funds.Single(f => f.FundPublicId == e.FondoProteccion.PublicId);
        proteccion.Employees.Should().Be(1, "E está retirada con definitiva: no consigna nada");
        proteccion.Total.Should().Be(2_315_761.67m);

        relacion.GrandTotal.Should().Be(porvenir.Total + proteccion.Total);
        relacion.Employees.Should().Be(3);

        // Cuadre contra las líneas de la corrida, que es lo que el comprobante contabiliza.
        var run = await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == calculo.Value.RunPublicId);
        var lineas = await (from l in e.D.Db.PayrollRunLines.AsNoTracking()
                            join f in e.D.Db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals f.Id
                            where f.PayrollRunId == run.Id && l.ConceptCode == WellKnownConceptCodes.Severance
                            select l.Amount).ToListAsync();
        relacion.GrandTotal.Should().Be(lineas.Sum());

        // Fechas límite: los parámetros DateInYear de la semilla, llevados al año siguiente.
        relacion.DueDate.Should().Be(new DateOnly(2027, 2, 14));
        relacion.InterestDueDate.Should().Be(new DateOnly(2027, 1, 31));
    }

    [Fact]
    public async Task Un_empleado_sin_fondo_en_la_ficha_va_en_un_bloque_propio_para_que_el_total_general_cuadre()
    {
        var e = new EscenarioDeCesantias();
        e.G.SeveranceFundId = 0;
        await e.D.Db.SaveChangesAsync();
        var calculo = await e.CalcularAsync();

        var relacion = (await e.Relacion().Handle(new GetDepositScheduleQuery(calculo.Value.RunPublicId), CancellationToken.None)).Value;

        relacion.Funds.Should().HaveCount(3);
        var sinFondo = relacion.Funds.Last();
        sinFondo.FundPublicId.Should().BeNull();
        sinFondo.FundName.Should().Be(DepositScheduleBuilder.SinFondo);
        sinFondo.Lines.Should().ContainSingle().Which.EmployeePublicId.Should().Be(e.G.PublicId);
        relacion.GrandTotal.Should().Be(2_749_095m + 2_315_761.67m + 666_666.67m);
    }

    [Fact]
    public async Task La_lista_trae_totales_de_cesantias_e_intereses_y_los_fondos_con_su_consignacion()
    {
        var e = new EscenarioDeCesantias(conContabilidad: true);
        var calculo = await e.CalcularAsync();
        (await e.AprobarAsync(calculo.Value.RunPublicId)).IsSuccess.Should().BeTrue();
        (await e.Marcador().Handle(new MarkFundDepositedCommand(calculo.Value.RunPublicId, e.FondoPorvenir.PublicId, new DateOnly(2027, 1, 12), "PLANILLA-77"), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var lista = await e.Lista().Handle(new ListSeveranceRunsQuery(2026), CancellationToken.None);

        lista.IsSuccess.Should().BeTrue();
        var item = lista.Value.Should().ContainSingle().Subject;
        item.Status.Should().Be("Approved");
        item.SeveranceTotal.Should().Be(2_749_095m + 2_315_761.67m + 666_666.67m);
        item.InterestTotal.Should().Be(329_891.40m + 277_891.40m + 26_666.67m);
        item.Total.Should().Be(item.SeveranceTotal + item.InterestTotal, "sin retención, el neto es la suma de los dos rubros");
        item.PostedDocumentNumber.Should().StartWith("NM-");
        item.PaidCount.Should().Be(0);
        item.Funds.Should().HaveCount(2);
        var porvenir = item.Funds.Single(f => f.FundPublicId == e.FondoPorvenir.PublicId);
        porvenir.DepositedAt.Should().Be(new DateOnly(2027, 1, 12));
        porvenir.DepositedBy.Should().Be("contadora@demo");
        porvenir.Reference.Should().Be("PLANILLA-77");
        item.Funds.Single(f => f.FundPublicId == e.FondoProteccion.PublicId).DepositedAt.Should().BeNull();

        (await e.Lista().Handle(new ListSeveranceRunsQuery(2026, "Draft"), CancellationToken.None)).Value.Should().BeEmpty();
        (await e.Lista().Handle(new ListSeveranceRunsQuery(2025), CancellationToken.None)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Marcar_consignado_exige_aprobada_guarda_el_valor_de_la_relacion_y_la_segunda_vez_es_AlreadyDeposited()
    {
        var e = new EscenarioDeCesantias(conContabilidad: true);
        var calculo = await e.CalcularAsync();
        var runId = calculo.Value.RunPublicId;

        var enBorrador = await e.Marcador().Handle(new MarkFundDepositedCommand(runId, e.FondoPorvenir.PublicId, new DateOnly(2027, 1, 12), null), CancellationToken.None);
        enBorrador.Error.Code.Should().Be("Payroll.Severance.NotApproved");

        (await e.AprobarAsync(runId)).IsSuccess.Should().BeTrue();

        var fondoAjeno = e.D.Db.SeveranceProviders.Add(new Domain.Entities.Payroll.SeveranceProvider { Code = "FNA", Name = "Fondo Nacional del Ahorro", CreatedBy = "test" }).Entity;
        await e.D.Db.SaveChangesAsync();
        var sinEmpleados = await e.Marcador().Handle(new MarkFundDepositedCommand(runId, fondoAjeno.PublicId, new DateOnly(2027, 1, 12), null), CancellationToken.None);
        sinEmpleados.Error.Code.Should().Be("Payroll.Severance.FundNotInRun");

        var inexistente = await e.Marcador().Handle(new MarkFundDepositedCommand(runId, Guid.NewGuid(), new DateOnly(2027, 1, 12), null), CancellationToken.None);
        inexistente.Error.Code.Should().Be("Payroll.SeveranceFund.NotFound");

        var antesDelCorte = await e.Marcador().Handle(new MarkFundDepositedCommand(runId, e.FondoPorvenir.PublicId, new DateOnly(2026, 12, 1), null), CancellationToken.None);
        antesDelCorte.Error.Code.Should().Be("Payroll.Severance.DepositDateInvalid");
        var enElFuturo = await e.Marcador().Handle(new MarkFundDepositedCommand(runId, e.FondoPorvenir.PublicId, new DateOnly(2027, 2, 1), null), CancellationToken.None);
        enElFuturo.Error.Code.Should().Be("Payroll.Severance.DepositDateInvalid", "hoy es el 15-01-2027 en la prueba");

        var ok = await e.Marcador().Handle(new MarkFundDepositedCommand(runId, e.FondoPorvenir.PublicId, new DateOnly(2027, 1, 12), " PLANILLA-77 "), CancellationToken.None);
        ok.IsSuccess.Should().BeTrue(ok.Error.Message);
        ok.Value.Amount.Should().Be(2_749_095m + 666_666.67m, "lo consignado es el total del bloque del fondo");
        ok.Value.Reference.Should().Be("PLANILLA-77");
        ok.Value.DepositedBy.Should().Be("contadora@demo");

        var repetida = await e.Marcador().Handle(new MarkFundDepositedCommand(runId, e.FondoPorvenir.PublicId, new DateOnly(2027, 1, 13), null), CancellationToken.None);
        repetida.IsFailure.Should().BeTrue();
        repetida.Error.Code.Should().Be("Payroll.Severance.AlreadyDeposited", "responde 422 por el sobre de errores");
        repetida.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { fundPublicId = e.FondoPorvenir.PublicId });
        (await e.D.Db.SeveranceFundDeposits.CountAsync()).Should().Be(1);

        // La relación refleja la marca en su bloque y el otro fondo sigue pendiente.
        var relacion = (await e.Relacion().Handle(new GetDepositScheduleQuery(runId), CancellationToken.None)).Value;
        var porvenir = relacion.Funds.Single(f => f.FundPublicId == e.FondoPorvenir.PublicId);
        porvenir.Deposited.Should().BeTrue();
        porvenir.DepositedAmount.Should().Be(porvenir.Total);
        relacion.Funds.Single(f => f.FundPublicId == e.FondoProteccion.PublicId).Deposited.Should().BeFalse();

        // Reversar la liquidación no borra la consignación: la plata ya salió (data-model §2.7a).
        (await e.Reversor().Handle(new ReverseSeveranceCommand(runId, "Corrección"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await e.D.Db.SeveranceFundDeposits.CountAsync(d => d.DepositedAt == new DateOnly(2027, 1, 12))).Should().Be(1);
    }

    [Fact]
    public async Task El_archivo_plano_del_fondo_responde_FundFormatMissing_hasta_que_exista_el_formato_en_N4()
    {
        var e = new EscenarioDeCesantias();
        var calculo = await e.CalcularAsync();

        var r = await new GetFundDepositFileQueryHandler(e.D.Db).Handle(new GetFundDepositFileQuery(calculo.Value.RunPublicId, e.FondoPorvenir.PublicId), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Severance.FundFormatMissing");
    }
}
