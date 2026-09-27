using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Integration;

namespace IngenIA365ERP.Domain.Tests.Integration;

/// <summary>
/// Feature 012, T477 (data-model §19; contracts/contabilidad.md §5.4; T12): el lote de integración es a la vez orden y
/// registro. Nace <c>Requested</c>; <c>Iniciar</c> lo pasa a <c>Running</c> una sola vez, y se cierra con una de tres
/// salidas: <c>Completar</c> (sin rechazos), <c>CompletarConRechazos</c> o <c>MarcarVacio</c> (corrió sin mensajes: prueba
/// que corrió). Un lote cerrado no se vuelve a cerrar. El contador entrega números consecutivos desde 1.
/// </summary>
public class IntegrationBatchTests
{
    private static readonly DateTime Pedido = new(2026, 12, 1, 23, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Inicio = Pedido.AddMinutes(1);
    private static readonly DateTime Fin = Pedido.AddMinutes(4);

    private static IntegrationBatch Lote() => new()
    {
        Number = 1, Destination = IntegrationDestinations.Accounting, Trigger = BatchTrigger.Manual,
        RequestedByKind = ActorKind.Person, RequestedByName = "Ana", Reason = "Cierre de mes", RequestedAt = Pedido,
    };

    [Fact]
    public void Nace_pedido_y_sin_tiempos_de_ejecucion()
    {
        var lote = Lote();
        lote.Status.Should().Be(BatchStatus.Requested);
        lote.StartedAt.Should().BeNull();
        lote.FinishedAt.Should().BeNull();
        lote.EstaCerrado.Should().BeFalse();
    }

    [Fact]
    public void Iniciar_lo_pone_a_correr_una_sola_vez()
    {
        var lote = Lote();
        lote.Iniciar(Inicio);
        lote.Status.Should().Be(BatchStatus.Running);
        lote.StartedAt.Should().Be(Inicio);

        ((Action)(() => lote.Iniciar(Fin))).Should().Throw<InvalidOperationException>();
        lote.StartedAt.Should().Be(Inicio);
    }

    [Fact]
    public void Completar_guarda_los_totales_y_exige_que_no_haya_rechazos()
    {
        var lote = Lote();
        lote.Iniciar(Inicio);

        ((Action)(() => lote.Completar(new TotalesDeLote(3, 2, 2, 1, 1, 100m, 100m, null), Fin))).Should().Throw<InvalidOperationException>();

        lote.Completar(new TotalesDeLote(3, 2, 3, 0, 2, 267_830.50m, 267_830.50m, "{\"grupos\":2}"), Fin);
        lote.Status.Should().Be(BatchStatus.Completed);
        lote.FinishedAt.Should().Be(Fin);
        lote.MessageCount.Should().Be(3);
        lote.DocumentCount.Should().Be(2);
        lote.ProcessedCount.Should().Be(3);
        lote.RejectedCount.Should().Be(0);
        lote.VoucherCount.Should().Be(2);
        lote.TotalDebit.Should().Be(267_830.50m);
        lote.TotalCredit.Should().Be(267_830.50m);
        lote.ResultSummaryJson.Should().Be("{\"grupos\":2}");
        lote.EstaCerrado.Should().BeTrue();
    }

    [Fact]
    public void CompletarConRechazos_exige_al_menos_un_rechazo()
    {
        var lote = Lote();
        lote.Iniciar(Inicio);
        ((Action)(() => lote.CompletarConRechazos(new TotalesDeLote(2, 2, 2, 0, 2, 1m, 1m, null), Fin))).Should().Throw<InvalidOperationException>();

        lote.CompletarConRechazos(new TotalesDeLote(2, 2, 1, 1, 1, 1m, 1m, null), Fin);
        lote.Status.Should().Be(BatchStatus.CompletedWithRejections);
        lote.RejectedCount.Should().Be(1);
    }

    [Fact]
    public void MarcarVacio_deja_la_prueba_de_que_corrio_sin_mensajes()
    {
        var lote = Lote();
        lote.Iniciar(Inicio);
        lote.MarcarVacio(Fin);
        lote.Status.Should().Be(BatchStatus.Empty);
        lote.FinishedAt.Should().Be(Fin);
        lote.MessageCount.Should().Be(0);
        lote.EstaCerrado.Should().BeTrue();
    }

    [Fact]
    public void Nada_se_cierra_sin_haber_corrido_ni_dos_veces()
    {
        var pedido = Lote();
        ((Action)(() => pedido.MarcarVacio(Fin))).Should().Throw<InvalidOperationException>();
        ((Action)(() => pedido.Completar(new TotalesDeLote(1, 1, 1, 0, 1, 1m, 1m, null), Fin))).Should().Throw<InvalidOperationException>();

        var cerrado = Lote();
        cerrado.Iniciar(Inicio);
        cerrado.MarcarVacio(Fin);
        ((Action)(() => cerrado.MarcarVacio(Fin))).Should().Throw<InvalidOperationException>();
        ((Action)(() => cerrado.CompletarConRechazos(new TotalesDeLote(1, 1, 0, 1, 0, 0m, 0m, null), Fin))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void El_contador_entrega_numeros_consecutivos_desde_uno()
    {
        var contador = new IntegrationBatchCounter();
        contador.NextValue.Should().Be(1);
        contador.TomarSiguiente().Should().Be(1);
        contador.TomarSiguiente().Should().Be(2);
        contador.NextValue.Should().Be(3);
    }
}
