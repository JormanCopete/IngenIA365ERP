using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 024, fase 3 (2026-11-16): <see cref="ProcesosDeCausacionClient"/>. Las consultas (opciones, lista, detalle, líneas) van sin clave; armar,
/// recalcular, anular, pedir la firma, reprocesar y el desafío y la decisión del aprobador presente llevan la suya; ninguna pone <c>Authorization</c>.
/// Y el rango que propone cada periodicidad en la pantalla. (nuevo)
/// </summary>
public class ProcesosDeCausacionClientTests
{
    private static readonly Guid Proceso = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
    private static readonly Guid Solicitud = Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb");

    private readonly ServidorDeCartera _servidor = new();
    private readonly ProcesosDeCausacionClient _cliente;

    public ProcesosDeCausacionClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new ProcesosDeCausacionClient(http, auth);
    }

    [Fact]
    public async Task Las_consultas_van_sin_clave_por_sus_rutas()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 20, totalCount = 0 });

        await _cliente.OpcionesAsync();
        await _cliente.ListarAsync("Confirmed", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        await _cliente.ProcesoAsync(Proceso);
        await _cliente.LineasAsync(Proceso, "Excluded", "LaterPayment", "InteresCausado", "CR-0001", 2);

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("GET", "/api/lending/accrual-processes/options"),
            ("GET", "/api/lending/accrual-processes?status=Confirmed&from=2026-11-01&to=2026-11-30&page=1&pageSize=20"),
            ("GET", $"/api/lending/accrual-processes/{Proceso}"),
            ("GET", $"/api/lending/accrual-processes/{Proceso}/lines?status=Excluded&reason=LaterPayment&factType=InteresCausado&search=CR-0001&page=2&pageSize=50"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public async Task Las_escrituras_llevan_su_clave_y_ninguna_pone_Authorization()
    {
        var pedido = new ProcesoDeCausacionRequest(TextosDeCausacion.Quincenal, 1, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 15), [1, 2]);

        await _cliente.ArmarAsync(pedido, new ClaveDeOperacion());
        await _cliente.RecalcularAsync(Proceso, null, new ClaveDeOperacion());
        await _cliente.AnularAsync(Proceso, "Se armó con el rango equivocado", new ClaveDeOperacion());
        await _cliente.PedirFirmaAsync(Proceso, "Cierre de la primera quincena", new string('a', 64), new ClaveDeOperacion());
        await _cliente.ReprocesarAsync(Proceso, "Se reabrió el mes", new ClaveDeOperacion());
        await _cliente.DesafioDePresenciaAsync(Solicitud, "gerente@coop.test", new ClaveDeOperacion());
        await _cliente.DecidirEnPersonaAsync(Solicitud, new DecisionEnPersonaRequest("Approve", null, "InPersonTotp",
            new PresenciaRequest(Guid.NewGuid(), null, "123456"), new string('a', 64)), new ClaveDeOperacion());

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta, v.Clave is not null)).Should().Equal(
            ("POST", "/api/lending/accrual-processes", true),
            ("POST", $"/api/lending/accrual-processes/{Proceso}/recalculate", true),
            ("POST", $"/api/lending/accrual-processes/{Proceso}/void", true),
            ("POST", $"/api/lending/accrual-processes/{Proceso}/confirm", true),
            ("POST", $"/api/lending/accrual-processes/{Proceso}/reprocess", true),
            ("POST", $"/api/lending/accrual-processes/approvals/{Solicitud}/presence-challenge", true),
            ("POST", $"/api/lending/accrual-processes/approvals/{Solicitud}/decide", true));
        _servidor.Vistas.Should().OnlyContain(v => v.Authorization == null, "la cabecera la pone sólo el handler de la sesión");
    }

    [Theory]
    [InlineData(TextosDeCausacion.Mensual, "2026-11-17", null, "2026-11-01", "2026-11-30")]
    [InlineData(TextosDeCausacion.Quincenal, "2026-11-17", (byte)1, "2026-11-01", "2026-11-15")]
    [InlineData(TextosDeCausacion.Quincenal, "2027-02-03", (byte)2, "2027-02-16", "2027-02-28")]
    [InlineData(TextosDeCausacion.Decadal, "2026-12-31", null, "2026-12-21", "2026-12-31")]
    [InlineData(TextosDeCausacion.Semanal, "2026-11-19", null, "2026-11-16", "2026-11-22")]
    [InlineData(TextosDeCausacion.Diaria, "2026-11-19", null, "2026-11-19", "2026-11-19")]
    public void La_pantalla_propone_el_mismo_rango_que_el_servidor(int periodicidad, string referencia, byte? subperiodo, string desde, string hasta)
    {
        TextosDeCausacion.Proponer(periodicidad, DateOnly.Parse(referencia), subperiodo)
            .Should().Be((DateOnly.Parse(desde), DateOnly.Parse(hasta)));
    }
}
