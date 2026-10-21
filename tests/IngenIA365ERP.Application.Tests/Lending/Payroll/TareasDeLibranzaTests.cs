using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Payroll.Results;
using IngenIA365ERP.Application.Lending.Payroll.Tasks;
using IngenIA365ERP.Application.Lending.Processes.Tareas;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Delinquency;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 013, K2, T0816 (DT §2.13, FR-114): las dos tareas diarias de la libranza.
/// <list type="bullet">
/// <item><c>cartera.archivos-de-descuento</c>: el convenio cuya antelación llegó sin archivo levanta <c>Cartera.ArchivoDeDescuentosPendiente</c> (DedupKey
/// convenio y período); sale si Cartera no está activada;</item>
/// <item><c>cartera.giros-de-pagaduria</c>: lo pendiente con la ventana de giro vencida levanta <c>Cartera.GiroDePagaduriaAtrasado</c> con el alcance
/// de la oficina y la línea; superado el umbral, avisa al deudor una sola vez y la cuota vuelve a contar para la altura de mora;</item>
/// <item>las dos las lanza el programador por <c>IEjecutorEnCooperativa</c>, con el actor del proceso: la tarea sólo manda su comando.</item>
/// </list>
/// </summary>
public sealed class TareasDeLibranzaTests : IAsyncLifetime
{
    private static readonly DateOnly Descuento = new(2027, 1, 30);

    private LibranzaDePrueba _l = null!;

    public async Task InitializeAsync() => _l = await LibranzaDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Lleva el reloj al mediodía local de <paramref name="dia"/> (sin causar).</summary>
    private void Reloj(DateOnly dia)
    {
        _l.G.UtcNow = dia.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc);
        _l.G.HoyLocal = dia;
    }

    private async Task<int> ArchivosAsync()
    {
        _l.G.ActuarComoProceso();
        var r = await _l.TareaDeArchivos().Handle(new AlertPendingPayrollDeductionFilesCommand(), default);
        _l.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private async Task<int> GirosAsync()
    {
        _l.G.ActuarComoProceso();
        var r = await _l.TareaDeGiros().Handle(new AlertOverduePayrollTransfersCommand(), default);
        _l.Db.ChangeTracker.Clear();
        return r.Value;
    }

    // ------------------------------------------------------------------------------------------------------------- archivos de descuento --

    [Fact]
    public async Task Llegada_la_antelacion_sin_archivo_alerta_una_vez_por_convenio_y_periodo()
    {
        Reloj(new DateOnly(2027, 1, 19));
        (await ArchivosAsync()).Should().Be(0, "el envío de enero es el 25 con 5 días de antelación: desde el 20");

        Reloj(new DateOnly(2027, 1, 20));
        (await ArchivosAsync()).Should().Be(1);

        var alerta = _l.Alertas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.ArchivoDeDescuentosPendiente).Subject;
        alerta.DedupKey.Should().StartWith($"{TiposDeAlerta.ArchivoDeDescuentosPendiente}:{_l.Convenio.PublicId:D}:").And.Contain("2027");
        alerta.EntityPublicId.Should().Be(_l.Convenio.PublicId);
    }

    [Fact]
    public async Task Con_el_archivo_generado_no_alerta_y_sin_Cartera_activada_sale()
    {
        await _l.ArchivoAsync();
        Reloj(new DateOnly(2027, 1, 21));
        (await ArchivosAsync()).Should().Be(0);
        _l.Alertas.Should().NotContain(a => a.TypeCode == TiposDeAlerta.ArchivoDeDescuentosPendiente);

        Reloj(new DateOnly(2027, 2, 21));
        var setup = await _l.Db.LendingSetups.SingleAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        setup.ActivationDate = null;
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        (await ArchivosAsync()).Should().Be(0, "sin Cartera activada la tarea sale");
        (await GirosAsync()).Should().Be(0);
    }

    // --------------------------------------------------------------------------------------------------------------- giros de pagaduría --

    [Fact]
    public async Task Vencida_la_ventana_alerta_con_alcance_y_superado_el_umbral_avisa_una_vez_y_la_cuota_cuenta()
    {
        var archivo = await _l.ArchivoAsync();
        var linea = await _l.LineaAsync(archivo.PublicId, _l.Credito);
        await _l.ConfirmarYConsumirAsync(archivo.PublicId);
        await _l.ResultadoYConsumoAsync(archivo.PublicId, LibranzaDePrueba.Respuesta((_l.Credito.Number, "00", linea.TotalAmount, Descuento)));
        var resultado = await _l.Db.PayrollDeductionResults.AsNoTracking().SingleAsync();

        Reloj(resultado.TransferDueDate!.Value);
        (await GirosAsync()).Should().Be(0, "el último día de la ventana todavía no está atrasado");

        Reloj(resultado.TransferDueDate.Value.AddDays(1));
        (await GirosAsync()).Should().Be(1);
        var alerta = _l.Alertas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.GiroDePagaduriaAtrasado).Subject;
        alerta.ScopeBranchPublicId.Should().NotBeNull("la alerta va con el alcance de la oficina del crédito");
        alerta.ScopeCreditLinePublicId.Should().Be(_l.D.O.Linea.PublicId);
        alerta.EntityPublicId.Should().Be(archivo.PublicId);
        (await _l.Db.DebtorMessages.AnyAsync(m => m.OriginPublicId == resultado.PublicId)).Should().BeFalse("dentro del umbral no se avisa al deudor");
        (await AlturaAsync(resultado.TransferDueDate.Value.AddDays(1))).CuotaMasAntigua.Should().NotBe(1, "lo descontado cubre la cuota 1 dentro del umbral");

        var superado = resultado.TransferDueDate.Value.AddDays(resultado.PaymasterArrearsThresholdDays!.Value + 1);
        Reloj(superado);
        await GirosAsync();
        await GirosAsync();

        var aviso = await _l.Db.DebtorMessages.AsNoTracking().SingleAsync(m => m.OriginPublicId == resultado.PublicId);
        (aviso.OriginKind, aviso.LoanId).Should().Be((SeguimientoDeNoAplicados.OrigenDelMensaje, (int?)_l.Credito.Id));
        var altura = await AlturaAsync(superado);
        altura.CuotaMasAntigua.Should().Be(1, "superado el umbral, la cuota cuenta desde su vencimiento");
        altura.Dias.Should().Be(superado.DayNumber - linea.DueDate!.Value.DayNumber);
    }

    private async Task<ResultadoDeAlturaDeMora> AlturaAsync(DateOnly dia)
    {
        var cargado = await _l.P.C.Cargador().CargarAsync(_l.Credito.Id, null, dia, default);
        return MotorDeCartera.Reconstruir(cargado!.Entrada!, dia).AlturaDeMora;
    }

    // ---------------------------------------------------------------------------------------------------------------------- la tarea --

    [Fact]
    public async Task Cada_tarea_manda_su_comando_y_anota_su_corrida()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<AlertPendingPayrollDeductionFilesCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(0));
        sender.Send(Arg.Any<AlertOverduePayrollTransfersCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(0));
        var servicios = new ServiceCollection().AddSingleton(sender).BuildServiceProvider();

        await new TareaDeArchivosDeDescuento().EjecutarAsync(servicios, default);
        await new TareaDeGirosDePagaduria().EjecutarAsync(servicios, default);

        await sender.Received(1).Send(Arg.Any<AlertPendingPayrollDeductionFilesCommand>(), Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Any<AlertOverduePayrollTransfersCommand>(), Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Is<RegisterDailyLendingTaskRunCommand>(c => c.Tarea == TareaDeArchivosDeDescuento.NombreDeLaTarea),
            Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Is<RegisterDailyLendingTaskRunCommand>(c => c.Tarea == TareaDeGirosDePagaduria.NombreDeLaTarea),
            Arg.Any<CancellationToken>());
        (TareaDeArchivosDeDescuento.NombreDeLaTarea, TareaDeGirosDePagaduria.NombreDeLaTarea)
            .Should().Be(("cartera.archivos-de-descuento", "cartera.giros-de-pagaduria"));
        var hoy = new DateTimeOffset(2027, 1, 20, 8, 0, 0, TimeSpan.FromHours(-5));
        new TareaDeGirosDePagaduria().DebeCorrer(hoy, hoy.AddHours(-2)).Should().BeFalse("una vez al día");
        new TareaDeGirosDePagaduria().DebeCorrer(hoy, hoy.AddDays(-1)).Should().BeTrue();
    }
}
