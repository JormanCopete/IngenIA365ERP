using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Application.Tests.Payroll.Vacations;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Reports;

/// <summary>
/// Feature 010 US4 (T061): las dos vistas de vacaciones del centro de reportes salen de las mismas
/// consultas de la pantalla y llevan los mismos números; los movimientos anulados se listan con su
/// motivo y no suman.
/// </summary>
public class ReportesDeVacacionesTests
{
    [Fact]
    public async Task Saldos_de_vacaciones_lista_a_cada_empleado_con_sus_piezas_y_el_total()
    {
        var d = VacacionesDePrueba.Escenario();
        d.Empleado("Beto", 3_000_000m, new DateTime(2026, 1, 15));
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetVacationBalancesQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => new GetVacationBalancesQueryHandler(d.Db, VacacionesDePrueba.Calculador(d), d.Clock).Handle(ci.Arg<GetVacationBalancesQuery>(), CancellationToken.None));

        var r = await new SaldosVacacionesReportQueryHandler(sender, d.Clock).Handle(new SaldosVacacionesReportQuery(VacacionesDePrueba.CorteDe540Dias), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        t.Titulo.Should().Be("Saldos de vacaciones");
        t.Subtitulo.Should().Contain("14/07/2026");
        t.Columnas.Select(c => c.Nombre).Should().ContainInOrder("Empleado", "Causados", "Saldo inicial", "Disfrutados", "Compensados", "Ajustes", "Pendientes");
        t.Filas.Should().HaveCount(2);
        var ana = t.Filas.Single(f => (string)f.Valores[0]! == "Ana Prueba");
        ana.Valores[t.Columnas.ToList().FindIndex(c => c.Nombre == "Causados")].Should().Be(22.5m);
        ana.Valores[t.Columnas.ToList().FindIndex(c => c.Nombre == "Pendientes")].Should().Be(22.5m);
        t.Totales.Should().NotBeNull();
        t.Totales!.Resaltada.Should().BeTrue();
        ((decimal)t.Totales.Valores[t.Columnas.ToList().FindIndex(c => c.Nombre == "Pendientes")]!).Should().BeGreaterThan(22.5m, "Beto suma lo suyo");
        t.Notas.Should().Contain(n => n.Contains("VACACIONES_DIAS_ANIO"));
    }

    [Fact]
    public async Task Movimientos_de_vacaciones_del_rango_con_estado_y_los_anulados_sin_sumar()
    {
        var d = VacacionesDePrueba.Escenario();
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        var disfrute = await VacacionesDePrueba.Registrar(d).Handle(VacacionesDePrueba.Disfrute(d.Ana, new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 28)), CancellationToken.None);
        disfrute.IsSuccess.Should().BeTrue(disfrute.Error.Message);
        d.Db.VacationMovements.Add(new Domain.Entities.Payroll.VacationMovement
        {
            EmployeeId = d.Ana.Id, Kind = VacationMovementKind.Adjustment, StartDate = new DateOnly(2026, 7, 1), BusinessDays = 1m,
            Status = VacationMovementStatus.Cancelled, CancelReason = "Error de digitación", CreatedBy = "test",
        });
        await d.Db.SaveChangesAsync();

        var r = await new MovimientosVacacionesReportQueryHandler(d.Db).Handle(new MovimientosVacacionesReportQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)), CancellationToken.None);
        var filtrado = await new MovimientosVacacionesReportQueryHandler(d.Db).Handle(new MovimientosVacacionesReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), d.Ana.PublicId), CancellationToken.None);
        var desconocido = await new MovimientosVacacionesReportQueryHandler(d.Db).Handle(new MovimientosVacacionesReportQuery(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), Guid.NewGuid()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Filas.Should().HaveCount(2);
        var tipo = r.Value.Columnas.ToList().FindIndex(c => c.Nombre == "Tipo");
        var estado = r.Value.Columnas.ToList().FindIndex(c => c.Nombre == "Estado");
        var habiles = r.Value.Columnas.ToList().FindIndex(c => c.Nombre == "Días hábiles");
        var notas = r.Value.Columnas.ToList().FindIndex(c => c.Nombre == "Notas");
        r.Value.Filas.Should().ContainSingle(f => (string)f.Valores[tipo]! == "Disfrute" && (string)f.Valores[estado]! == "Registrado" && (decimal)f.Valores[habiles]! == 11m);
        r.Value.Filas.Should().ContainSingle(f => (string)f.Valores[tipo]! == "Ajuste" && (string)f.Valores[estado]! == "Anulado" && ((string)f.Valores[notas]!).Contains("Error de digitación"));
        r.Value.Totales!.Valores[habiles].Should().Be(11m, "el anulado no suma");
        r.Value.Columnas[habiles].Tipo.Should().Be(TipoDeColumna.Decimal);

        filtrado.Value.Filas.Should().BeEmpty();
        filtrado.Value.Subtitulo.Should().Contain("Ana Prueba");
        desconocido.IsFailure.Should().BeTrue();
        desconocido.Error.Code.Should().Be("Payroll.Employee.NotFound");
    }
}
