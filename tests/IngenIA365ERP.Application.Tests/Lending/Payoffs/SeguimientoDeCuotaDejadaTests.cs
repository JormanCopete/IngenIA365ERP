using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration.Contrato;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (E2, T070, T073; HU4-7, HU8, FR-020): el seguimiento de la cuota que una recogida con «todo menos la cuota causada» dejó en el crédito viejo para
/// su canal. La marca pagada el pago que la cierra —de cualquier canal; uno que la cubre a medias no—, y un «no descontado» de esa cuota avisa al asesor que
/// radicó la solicitud del crédito nuevo (la cuota sigue la regla del convenio como cualquier otra). Escenario de <see cref="PagosDePrueba"/>: Ana, 3.000.000 a
/// 24 cuotas desde el 01-12-2026; la recogida a mitad de la cuota 1, que vence el 01-01-2027. (nuevo)
/// </summary>
public class SeguimientoDeCuotaDejadaTests
{
    private static readonly DateOnly AMitad = PagosDePrueba.Hoy.AddDays(10);
    private static readonly DateOnly VenceLaPrimera = new(2027, 1, 1);

    private static void Excluir(PagosDePrueba p)
    {
        p.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.RecogidaCuotaCausada, Value = ParametrosDeCartera.CuotaExcluir,
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test", CreatedAt = p.G.UtcNow.AddDays(-10),
        });
        p.Db.SaveChanges();
        p.Db.ChangeTracker.Clear();
    }

    /// <summary>El crédito de Ana recogido a mitad de la cuota 1 por un crédito nuevo de 5.000.000, dejando la cuota 1; lo que esa cuota queda debiendo.</summary>
    private static async Task<(CreditApplicationDto Solicitud, decimal Queda)> RecogidoDejandoLaCuotaAsync(PagosDePrueba p)
    {
        Excluir(p);
        var recogida = new PayoffInput(PayoffProductKind.Loan, p.Credito.PublicId, PayoffMode.Total);
        var s = await p.D.AprobadaAsync(p.D.O.Solicitud(monto: 5_000_000m) with
        {
            Payoffs = [recogida], PayoffProjection = new PayoffProjectionInput(AMitad),
        });
        await p.CausarHastaAsync(AMitad);
        var r = await p.D.DesembolsadoAsync(p.D.Entrada(s) with { AcknowledgedPayoffDifferences = true }, p.Contrato());
        var aplicada = r.Payoffs.Single();
        aplicada.LeftInstallmentNumber.Should().Be(1);
        return (s, aplicada.LeftInstallment!.Value);
    }

    private static Task<Domain.Entities.Lending.Payoffs.DisbursementPayoff> FilaAsync(PagosDePrueba p) =>
        p.Db.DisbursementPayoffs.AsNoTracking().SingleAsync(f => f.LoanId == p.Credito.Id);

    [Fact]
    public async Task El_pago_que_cierra_la_cuota_dejada_la_marca_pagada_y_uno_parcial_no()
    {
        // Con el tope del prepago sembrado: la cancelación de la cuota dejada lo consulta.
        using var p = await PagosDePrueba.CrearAsync();
        var (_, queda) = await RecogidoDejandoLaCuotaAsync(p);
        (await FilaAsync(p)).CuotaDejadaPendiente.Should().BeTrue();

        await p.CausarHastaAsync(VenceLaPrimera);
        var mitad = Math.Round(queda / 2m, 0);
        await p.AplicadoAsync(p.Pago(ModalidadDePago.FreeAmount, mitad, VenceLaPrimera));
        (await FilaAsync(p)).LeftInstallmentSettledAt.Should().BeNull("un pago que la cubre a medias no la cierra");

        var resto = await p.AplicadoAsync(p.Pago(ModalidadDePago.FullPayoff, queda - mitad, VenceLaPrimera));

        resto.PaidOff.Should().BeTrue();
        var fila = await FilaAsync(p);
        fila.LeftInstallmentSettledAt.Should().NotBeNull("su canal la pagó");
        fila.CuotaDejadaPendiente.Should().BeFalse();
        (await VinculosDeRecogida.DeLosRecogidosAsync(p.Db, [p.Credito.Id], default)).Single().CuotaDejadaPagadaEn.Should().Be(fila.LeftInstallmentSettledAt);
        await p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task La_cuota_pagada_se_reconoce_sin_capital_ni_conceptos_pendientes()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        await p.CausarHastaAsync(AMitad);
        var cargado = await p.C.Cargador().CargarAsync(p.Credito.Id, null, AMitad, default);
        var estado = MotorDeCartera.Reconstruir(cargado!.Entrada!, AMitad);

        SeguimientoDeCuotaDejada.CuotaPagada(estado, 1).Should().BeFalse("la cuota 1 debe su capital");
        SeguimientoDeCuotaDejada.CuotaPagada(estado, 99).Should().BeTrue("una cuota que el plan no tiene no debe nada");
    }

    [Fact]
    public async Task El_no_descontado_de_la_cuota_dejada_avisa_al_asesor_que_radico_la_solicitud()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var (s, _) = await RecogidoDejandoLaCuotaAsync(p);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var registro = new NoDescontadosDelLote(p.Db, p.C.Cargador(), p.D.Alertas, p.G.Reloj, null, new SeguimientoDeCuotaDejada(p.Db, sender));

        var r = await registro.RegistrarAsync(new Recaudador(ClavesDeRecaudador.Nomina, ClaseDeRecaudador.Payroll),
            new NoDescontado($"NOM:{Guid.NewGuid():N}", p.Credito.PublicId, 1, 150_000m, 150_000m, MotivoDeNoDescuento.InsufficientNet, null, null), default);

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        await p.Db.SaveChangesAsync(); // lo guarda quien llama (el contrato, al final del lote)
        var asesora = await p.Db.Users.AsNoTracking().SingleAsync(u => u.Id == p.D.O.Asesora.Id);
        await sender.Received(1).Send(Arg.Is<SendNotificationCommand>(c => c.Payload.RecipientUserPublicId == asesora.PublicId
            && c.Payload.Subject.Contains("cuota 1") && c.Payload.Body.Contains("La nómina no descontó")), Arg.Any<CancellationToken>());
        (await p.Db.InstallmentNonDeductions.AsNoTracking().SingleAsync()).InstallmentNumber.Should().Be(1, "la cuota sigue la regla de siempre");
        s.PublicId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Sin_cuota_dejada_no_se_avisa_a_nadie()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var sender = Substitute.For<ISender>();

        var avisados = await new SeguimientoDeCuotaDejada(p.Db, sender).AvisarNoDescontadoAsync(p.Credito.Id, 1, 150_000m, "La pagaduría", default);

        avisados.Should().Be(0);
        await sender.DidNotReceive().Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>());
    }
}
