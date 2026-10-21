using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US5, T0523 (interfaces §3.4, §3.7; data-model §24.9; FR-112, T92, C32, C56): un <c>NoDescontado</c> del lote de Nómina se guarda en
/// <c>LND_InstallmentNonDeductions</c> con <c>Origin = Contract</c>, el recaudador, el motivo (también <c>DeductionPriority</c>) y la clave; repetir la
/// clave devuelve lo ya registrado; la cuota sigue exigible; alerta <c>Cartera.CuotaNoDescontadaPorNomina</c>; no emite hecho contable; responde
/// <c>Applied</c> diciendo que la cuota sigue exigible. Sólo lo informa la nómina propia sin interacción.
/// </summary>
public class NoDescontadosDeNominaTests : IAsyncLifetime
{
    private PagosDePrueba _p = null!;
    private DateOnly _vencimiento;

    public async Task InitializeAsync()
    {
        _p = await PagosDePrueba.CrearAsync();
        _vencimiento = (await _p.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync()).DueDate;
        await _p.CausarHastaAsync(_vencimiento.AddDays(1));
    }

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private string Clave(Guid corrida) => $"NOM:{corrida:N}:{_p.Credito.PublicId:N}";

    private NoDescontado NoDescontado(string clave, MotivoDeNoDescuento motivo = MotivoDeNoDescuento.DeductionPriority) =>
        new(clave, _p.Credito.PublicId, null, 160_000m, 60_000m, motivo, "Prelación entre descuentos", Guid.NewGuid());

    private async Task<RespuestaDeAplicacionDto> AplicarAsync(IReadOnlyList<PagoAAplicar> pagos, IReadOnlyList<NoDescontado> noDescontados,
        Recaudador? recaudador = null, FormaDeAplicacion forma = FormaDeAplicacion.NonInteractive)
    {
        var r = await _p.Contrato().AplicarAsync(new SolicitudDeAplicacion(recaudador ?? Recaudador.Nomina, forma, pagos, noDescontados), default);
        _p.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Se_guarda_con_su_motivo_la_cuota_sigue_exigible_alerta_y_no_emite_hecho()
    {
        var clave = Clave(Guid.NewGuid());
        var pago = _p.Pago(ModalidadDePago.CycleInstallment, 100_000m, _vencimiento, clave, medio: ClaseDeMedioDeRecaudo.OwnPayrollDeduction);
        var mensajesAntes = await _p.Db.IntegrationMessages.CountAsync();

        var r = await AplicarAsync([pago], [NoDescontado(clave)]);

        r.Payments.Should().ContainSingle().Which.Result.Should().Be(ResultadoDelContrato.Applied);
        var registrado = r.NotDeducted.Should().ContainSingle().Subject;
        (registrado.Result, registrado.Code).Should().Be((ResultadoDelContrato.Applied, "Lending.ObligationContract.Applied"));
        registrado.Message.Should().Contain("la cuota sigue exigible");
        registrado.Value!.InstallmentNumber.Should().Be(1);
        var fila = await _p.Db.InstallmentNonDeductions.AsNoTracking().SingleAsync();
        (fila.Origin, fila.Reason, fila.CollectorCode, fila.ContractOperationKey, fila.InstallmentNumber, fila.NotDeductedAmount)
            .Should().Be((NonDeductionOrigin.Contract, NonDeductionReason.DeductionPriority, "NOM", clave, 1, 60_000m));
        fila.AlertPublicId.Should().NotBeNull();
        _p.D.O.L.Levantadas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.CuotaNoDescontadaPorNomina
                                                        && a.DedupKey == $"{TiposDeAlerta.CuotaNoDescontadaPorNomina}:{_p.Credito.PublicId:D}:1");
        var operacion = r.Payments[0].Value!.OperationPublicId;
        (await _p.Db.IntegrationMessages.AsNoTracking().OrderBy(m => m.Id).Skip(mensajesAntes).ToListAsync())
            .Should().OnlyContain(m => m.OriginPublicId == operacion, "sólo la unidad del pago descontado: el no descontado no emite hecho");
        var lista = await _p.Contrato().ListarAsync(new SolicitudDeListado(Recaudador.Nomina, _p.D.O.Ana.PublicId, _p.G.HoyLocal), default);
        lista.Value!.Obligations[0].PastDue.Should().BeGreaterThan(0m, "lo no descontado sigue exigible");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Repetir_la_clave_devuelve_lo_ya_registrado()
    {
        var clave = Clave(Guid.NewGuid());
        var primero = (await AplicarAsync([], [NoDescontado(clave)])).NotDeducted[0];

        var otra = (await AplicarAsync([], [NoDescontado(clave)])).NotDeducted[0];

        otra.Result.Should().Be(ResultadoDelContrato.AlreadyApplied);
        otra.Value!.NonDeductionPublicId.Should().Be(primero.Value!.NonDeductionPublicId);
        (await _p.Db.InstallmentNonDeductions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Solo_la_nomina_propia_sin_interaccion_informa_no_descontados()
    {
        var r = await AplicarAsync([], [NoDescontado(PagosDePrueba.Clave())], PagosDePrueba.Prueba);
        r.NotDeducted.Should().ContainSingle().Which.Code.Should().Be("Validation.Invalid");
        (await _p.Db.InstallmentNonDeductions.CountAsync()).Should().Be(0);
    }
}
