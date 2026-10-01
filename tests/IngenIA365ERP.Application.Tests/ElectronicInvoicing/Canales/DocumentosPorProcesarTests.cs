using IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.ElectronicInvoicing.Processor;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales;

/// <summary>
/// Lo que elige <c>ProcesadorDeDocumentosElectronicos</c> (feature 012, I4, T731; contracts/dian.md §6.4): <c>NextAttemptAt ≤ ahora</c> y sin
/// arrendamiento vigente, en <c>Pending</c>, <c>Sent</c>, <c>DianContingency</c> e <c>IssuerContingency</c> con su evento cerrado, en orden de
/// consecutivo, y lo que espera a otro documento sólo cuando ése quedó validado. (nuevo)
/// </summary>
public sealed class DocumentosPorProcesarTests
{
    private readonly EscenarioDeEmision _s = new();

    private async Task<ElectronicDocument> DocumentoAsync(long consecutivo, ElectronicDocumentStatus estado, DateTime? siguiente = null)
    {
        var d = await _s.FacturaAsync(consecutivo);
        d.Status = estado;
        d.NextAttemptAt = siguiente ?? _s.Ahora.AddMinutes(-1);
        await _s.E.Db.SaveChangesAsync();
        return d;
    }

    private Task<IReadOnlyList<Guid>> ElegiblesAsync(int tanda = 50) => DocumentosPorProcesar.ElegiblesAsync(_s.E.Db, _s.Ahora, tanda, default);

    [Fact]
    public async Task Elige_lo_vencido_en_los_estados_que_se_transmiten_en_orden_de_consecutivo()
    {
        var tercero = await DocumentoAsync(990000303, ElectronicDocumentStatus.DianContingency);
        var primero = await DocumentoAsync(990000301, ElectronicDocumentStatus.Pending);
        var segundo = await DocumentoAsync(990000302, ElectronicDocumentStatus.Sent);

        var elegidos = await ElegiblesAsync();

        Assert.Equal([primero.PublicId, segundo.PublicId, tercero.PublicId], elegidos);
    }

    [Fact]
    public async Task No_elige_lo_que_todavia_no_toca_ni_lo_arrendado_ni_lo_final_ni_lo_rechazado()
    {
        await DocumentoAsync(990000311, ElectronicDocumentStatus.Pending, _s.Ahora.AddMinutes(5));
        var arrendado = await DocumentoAsync(990000312, ElectronicDocumentStatus.Pending);
        arrendado.LeaseUntil = _s.Ahora.AddMinutes(1);
        arrendado.LeaseOwner = "otra";
        await DocumentoAsync(990000313, ElectronicDocumentStatus.Validated);
        await DocumentoAsync(990000314, ElectronicDocumentStatus.Rejected);
        var sinEspera = await DocumentoAsync(990000315, ElectronicDocumentStatus.Pending);
        sinEspera.NextAttemptAt = null;
        await _s.E.Db.SaveChangesAsync();

        Assert.Empty(await ElegiblesAsync());
    }

    [Fact]
    public async Task Un_arrendamiento_vencido_no_impide_elegirlo()
    {
        var d = await DocumentoAsync(990000321, ElectronicDocumentStatus.Sent);
        d.LeaseUntil = _s.Ahora.AddMinutes(-3);
        d.LeaseOwner = "caida";
        await _s.E.Db.SaveChangesAsync();

        Assert.Equal([d.PublicId], await ElegiblesAsync());
    }

    [Fact]
    public async Task La_contingencia_03_se_transmite_solo_con_su_evento_cerrado()
    {
        var evento = _s.E.Contingencia03();
        var d = await DocumentoAsync(990000331, ElectronicDocumentStatus.IssuerContingency);
        d.ContingencyEventId = evento.Id;
        await _s.E.Db.SaveChangesAsync();
        var abierta = await ElegiblesAsync();

        evento.Status = ContingencyEventStatus.Closed;
        evento.EndedAt = _s.Ahora.AddMinutes(-2);
        await _s.E.Db.SaveChangesAsync();
        var cerrada = await ElegiblesAsync();

        Assert.Empty(abierta);
        Assert.Equal([d.PublicId], cerrada);
    }

    [Fact]
    public async Task Lo_que_espera_a_otro_documento_sale_cuando_ese_quedo_validado()
    {
        var original = await DocumentoAsync(990000341, ElectronicDocumentStatus.DianContingency, _s.Ahora.AddHours(1));
        var nota = await DocumentoAsync(990000342, ElectronicDocumentStatus.Pending);
        nota.WaitsForDocumentId = original.Id;
        await _s.E.Db.SaveChangesAsync();
        var mientras = await ElegiblesAsync();

        original.Status = ElectronicDocumentStatus.Validated;
        original.NextAttemptAt = null;
        await _s.E.Db.SaveChangesAsync();
        var despues = await ElegiblesAsync();

        Assert.Empty(mientras);
        Assert.Equal([nota.PublicId], despues);
    }

    [Fact]
    public async Task Respeta_la_tanda()
    {
        for (var i = 0; i < 4; i++) await DocumentoAsync(990000351 + i, ElectronicDocumentStatus.Pending);

        Assert.Equal(2, (await ElegiblesAsync(tanda: 2)).Count);
    }
}
