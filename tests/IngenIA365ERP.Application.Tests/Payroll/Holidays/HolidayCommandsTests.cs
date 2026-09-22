using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Payroll.Holidays;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Holidays;

/// <summary>
/// T032 (feature 010, R6; contracts/api.md §10.2): la cooperativa agrega festivos decretados o
/// manuales y retira sólo esos; los de la Ley 51 vienen de la semilla y se quedan.
/// </summary>
public class HolidayCommandsTests
{
    private readonly NominaTestData _d = new();

    private Holiday Sembrado(DateOnly fecha, string nombre, HolidayOrigin origen = HolidayOrigin.Ley51Fixed)
    {
        var h = new Holiday { Date = fecha, Name = nombre, Origin = origen, Year = (short)fecha.Year, CreatedBy = "system:seed" };
        _d.Db.Holidays.Add(h);
        _d.Db.SaveChanges();
        return h;
    }

    [Fact]
    public async Task Un_festivo_nuevo_nace_manual_por_defecto_o_decretado_si_se_dice_y_queda_auditado()
    {
        var crear = new CreateHolidayCommandHandler(_d.Db, _d.Clock, _d.User, _d.AuditEmitter);

        var manual = await crear.Handle(new CreateHolidayCommand(new DateOnly(2026, 12, 24), "Nochebuena (no se trabaja)"), CancellationToken.None);
        var decretado = await crear.Handle(new CreateHolidayCommand(new DateOnly(2026, 12, 31), "Puente decretado", HolidayOrigin.Decreed), CancellationToken.None);

        manual.IsSuccess.Should().BeTrue(manual.Error.Message);
        decretado.IsSuccess.Should().BeTrue(decretado.Error.Message);
        var filas = await _d.Db.Holidays.OrderBy(h => h.Date).ToListAsync();
        filas.Should().HaveCount(2);
        filas[0].Origin.Should().Be(HolidayOrigin.Manual);
        filas[0].Year.Should().Be(2026);
        filas[0].EsSembrado.Should().BeFalse();
        filas[1].Origin.Should().Be(HolidayOrigin.Decreed);
        await _d.Audit.Received(2).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == AuditEventTypes.PayrollHolidayChanged), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_fecha_que_ya_es_festivo_se_rechaza_nombrando_al_existente()
    {
        Sembrado(new DateOnly(2026, 1, 1), "Año Nuevo");

        var r = await new CreateHolidayCommandHandler(_d.Db, _d.Clock, _d.User, _d.AuditEmitter)
            .Handle(new CreateHolidayCommand(new DateOnly(2026, 1, 1), "Otro nombre"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Holiday.DateDuplicate");
        r.Error.Message.Should().Contain("Año Nuevo");
    }

    /// <summary>
    /// Revisión de N1: el origen de la semilla lo rechaza el <b>handler</b> con su código (422
    /// <c>Payroll.Holiday.OriginInvalid</c>, D-20). El validador no lo mira: cuando lo miraba, el pipeline
    /// respondía 400 <c>Validation.Invalid</c> y el código del contrato era inalcanzable por HTTP.
    /// </summary>
    [Fact]
    public async Task Solo_se_registran_decretados_o_manuales_y_lo_decide_el_handler_con_su_codigo_no_el_validador()
    {
        var validador = new CreateHolidayCommandValidator();
        validador.Validate(new CreateHolidayCommand(new DateOnly(2026, 5, 1), "Día del Trabajo", HolidayOrigin.Ley51Fixed)).IsValid
            .Should().BeTrue("el validador deja pasar el origen para que el handler responda con Payroll.Holiday.OriginInvalid");
        validador.Validate(new CreateHolidayCommand(new DateOnly(2026, 5, 1), "Día del Trabajo", (HolidayOrigin)99)).IsValid
            .Should().BeFalse("un número que no es del enum sí es validación de forma");

        var r = await new CreateHolidayCommandHandler(_d.Db, _d.Clock, _d.User, _d.AuditEmitter)
            .Handle(new CreateHolidayCommand(new DateOnly(2026, 5, 1), "Día del Trabajo", HolidayOrigin.Ley51Easter), CancellationToken.None);
        r.Error.Code.Should().Be("Payroll.Holiday.OriginInvalid");
    }

    [Fact]
    public async Task Un_festivo_sembrado_no_se_retira_y_uno_manual_si_con_retiro_suave()
    {
        var ley51 = Sembrado(new DateOnly(2026, 7, 20), "Independencia");
        var manual = Sembrado(new DateOnly(2026, 12, 24), "Nochebuena", HolidayOrigin.Manual);
        var borrar = new DeleteHolidayCommandHandler(_d.Db, _d.Clock, _d.User, _d.AuditEmitter);

        var sembrado = await borrar.Handle(new DeleteHolidayCommand(ley51.PublicId), CancellationToken.None);
        sembrado.Error.Code.Should().Be("Payroll.Holiday.Seeded");

        var retirado = await borrar.Handle(new DeleteHolidayCommand(manual.PublicId), CancellationToken.None);
        retirado.IsSuccess.Should().BeTrue(retirado.Error.Message);
        var fila = await _d.Db.Holidays.IgnoreQueryFilters().SingleAsync(h => h.Id == manual.Id);
        fila.IsDeleted.Should().BeTrue("retiro suave: la fila queda con quién y cuándo");
        fila.DeletedBy.Should().Be("ana@demo");
        (await _d.Db.Holidays.CountAsync(h => !h.IsDeleted)).Should().Be(1);

        var inexistente = await borrar.Handle(new DeleteHolidayCommand(Guid.NewGuid()), CancellationToken.None);
        inexistente.Error.Code.Should().Be("Payroll.Holiday.NotFound");
    }

    [Fact]
    public async Task El_listado_es_por_anio_en_orden_de_fecha_y_dice_cuales_vienen_de_la_semilla()
    {
        Sembrado(new DateOnly(2026, 7, 20), "Independencia");
        Sembrado(new DateOnly(2026, 1, 12), "Reyes Magos", HolidayOrigin.Ley51MovedToMonday);
        Sembrado(new DateOnly(2027, 1, 1), "Año Nuevo");
        Sembrado(new DateOnly(2026, 12, 24), "Nochebuena", HolidayOrigin.Manual);

        var r = await new ListHolidaysQueryHandler(_d.Db, _d.Clock).Handle(new ListHolidaysQuery(2026), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Select(h => h.Date).Should().BeInAscendingOrder().And.HaveCount(3);
        r.Value.Where(h => h.IsSeeded).Should().HaveCount(2);
        r.Value.Single(h => h.Origin == HolidayOrigin.Manual).IsSeeded.Should().BeFalse();

        var porDefecto = await new ListHolidaysQueryHandler(_d.Db, _d.Clock).Handle(new ListHolidaysQuery(), CancellationToken.None);
        porDefecto.Value.Should().HaveCount(3, "sin año, el del reloj (2026)");
    }
}
