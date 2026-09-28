using FluentAssertions;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Settings;

/// <summary>
/// Feature 012, I4, T745 (api.md §24.2 <c>GET /resolutions/{id}</c>): el detalle de una resolución es el mismo
/// <c>DianNumberingResolutionDto</c> de la lista —estado calculado, consumo, días, clave técnica enmascarada— más los documentos que numeró,
/// agrupados por mes de expedición con cuántos y del primero al último consecutivo. Una que no existe: <c>Resolution.NotFound</c>.
/// </summary>
public sealed class DetalleDeResolucionTests
{
    private readonly EscenarioDeEmision _s = new();

    private GetNumberingResolutionQueryHandler Handler() => new(_s.E.Db, _s.E.Reloj);

    [Fact]
    public async Task Trae_la_resolucion_y_sus_documentos_por_mes()
    {
        var noviembre = await _s.FacturaAsync(990000101);
        noviembre.IssueDate = new DateOnly(2026, 11, 20);
        await _s.FacturaAsync(990000102);
        await _s.FacturaAsync(990000103);
        _s.Resolucion.Channels.Single().TechnicalKey = "clave-tecnica-abcd";
        await _s.E.Db.SaveChangesAsync();

        var r = await Handler().Handle(new GetNumberingResolutionQuery(_s.Resolucion.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.Resolution.ResolutionPublicId.Should().Be(_s.Resolucion.PublicId);
        r.Value.Resolution.Channels.Single().TechnicalKeyMasked.Should().Be("••••abcd");
        r.Value.DocumentsByMonth.Should().BeEquivalentTo(new[]
        {
            new DocumentosDeResolucionPorMesDto(2026, 11, 1, 990000101, 990000101),
            new DocumentosDeResolucionPorMesDto(2026, 12, 2, 990000102, 990000103),
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Sin_documentos_trae_la_lista_vacia()
    {
        var r = await Handler().Handle(new GetNumberingResolutionQuery(_s.Resolucion.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.DocumentsByMonth.Should().BeEmpty();
    }

    [Fact]
    public async Task Una_que_no_existe_responde_NotFound()
    {
        var r = await Handler().Handle(new GetNumberingResolutionQuery(Guid.NewGuid()), default);

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionNotFoundCode);
    }
}
