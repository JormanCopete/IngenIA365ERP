using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.BankItems;

/// <summary>
/// Feature 015, US6, T0189a (Principio X; FR-014; TR37; CO1): cada tanda de una corrida no interactiva deja, por <c>COR_AuditOutbox</c> en su transacción, el evento
/// <c>Receipts.ProcessingRun.Summary</c> con la lista <c>receipts[] { publicId, sha256 }</c> de los recibos aplicados en ella —ninguno falta ni sobra; el reproceso de una
/// rechazada aparece en su tanda—, cada huella coincide con <see cref="HuellaDeRecibo.DelReciboAsync"/> recalculada desde el recibo, alterado un recibo su verificación
/// falla, y hay un evento por línea rechazada. Lo mismo en el giro de una pagaduría (fase 12). Prueba escrita, sin correr.
/// </summary>
public class AuditoriaDeCorridasTests
{
    private readonly BancosDePrueba _b = new();

    [Fact]
    public async Task Cada_tanda_resume_exactamente_los_recibos_aplicados_con_su_huella_y_un_evento_por_rechazada()
    {
        _b.RechazarCredito(_b.K.CreditoB);
        Guid? corrida = null;
        var aplicados = new List<Guid>();
        for (var i = 0; i < 50; i++)
        {
            var rechazada = i % 17 == 3;
            var abono = _b.Abono(10_000m + i);
            var r = await _b.Identificar().Handle(new IdentifyBankItemCommand(abono.PublicId, [rechazada ? _b.CuotaB(10_000m + i) : _b.CuotaA(10_000m + i)],
                RunPublicId: corrida) { OperationKey = Guid.NewGuid() }, default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
            corrida = r.Value.Run.PublicId;
            if (!rechazada) aplicados.Add(r.Value.Run.Items.Last().ReceiptPublicId!.Value);
        }

        // Sin cooperativa resuelta (InMemory) el evento va directo al servicio de auditoría: se leen las llamadas; con cooperativa va por COR_AuditOutbox en la
        // transacción de la tanda (lo recorre la e2e).
        var eventos = _b.K.C.E.Auditoria.ReceivedCalls().Select(c => c.GetArguments().OfType<Application.Common.Interfaces.AuditLogCommand>().FirstOrDefault())
            .Where(c => c is not null).Select(c => c!).ToList();
        var resumenes = eventos.Where(e => e.Action == CorridasNoInteractivas.EventoResumen).ToList();
        var rechazos = eventos.Where(e => e.Action == CorridasNoInteractivas.EventoLineaRechazada).ToList();
        var listados = resumenes.SelectMany(e => Recibos(JsonSerializer.Serialize(e.NewValues))).ToList();

        aplicados.Should().HaveCount(47);
        listados.Select(x => x.PublicId).Should().BeEquivalentTo(aplicados, "la lista cubre exactamente los recibos aplicados");
        rechazos.Should().HaveCount(3);
        foreach ((Guid publicId, string sha) in listados)
        {
            var recibo = await _b.K.C.E.Db.IssuedReceipts.SingleAsync(r => r.PublicId == publicId);
            (await HuellaDeRecibo.DelReciboAsync(_b.K.C.E.Db, recibo, default)).Should().Be(sha);
        }
    }

    [Fact]
    public async Task Alterado_un_recibo_su_huella_ya_no_coincide()
    {
        var abono = _b.Abono(25_000m);
        var r = await _b.Identificar().Handle(new IdentifyBankItemCommand(abono.PublicId, [_b.CuotaA(25_000m)]) { OperationKey = Guid.NewGuid() }, default);
        var recibo = await _b.K.C.E.Db.IssuedReceipts.SingleAsync();
        var antes = await HuellaDeRecibo.DelReciboAsync(_b.K.C.E.Db, recibo, default);

        var aplicacion = await _b.K.C.E.Db.ReceiptApplications.SingleAsync();
        aplicacion.Amount += 1m;

        (await HuellaDeRecibo.DelReciboAsync(_b.K.C.E.Db, recibo, default)).Should().NotBe(antes);
        r.IsSuccess.Should().BeTrue();
    }

    private static IEnumerable<(Guid PublicId, string Sha)> Recibos(string json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var p in Buscar(doc.RootElement, "receipts"))
            foreach (var e in p.EnumerateArray())
                yield return (e.GetProperty("publicId").GetGuid(), e.GetProperty("sha256").GetString()!);
    }

    private static IEnumerable<JsonElement> Buscar(JsonElement e, string nombre)
    {
        if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject())
            {
                if (p.NameEquals(nombre) && p.Value.ValueKind == JsonValueKind.Array) yield return p.Value.Clone();
                foreach (var x in Buscar(p.Value, nombre)) yield return x;
            }
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (var i in e.EnumerateArray())
                foreach (var x in Buscar(i, nombre)) yield return x;
    }

    private static ClaseDeMedioDeRecaudo Medio => ClaseDeMedioDeRecaudo.Transfer;
}
