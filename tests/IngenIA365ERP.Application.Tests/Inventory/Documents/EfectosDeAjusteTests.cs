using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, T241 (FR-036, FR-037; contracts/api.md §10): las estrategias del grupo <c>Adjustments</c>. El ajuste positivo
/// entra al promedio del ámbito o al último costo con existencia cero, o al costo digitado sólo con
/// <c>Inventory.Adjustments.SetUnitCost</c> (422 <c>UnitCostNotAllowed</c> con <c>permissionCode</c>, no 404); una salida no
/// admite costo (<c>UnitCostOnlyOnEntries</c>); negativo y baja exigen causa, el consumo interno centro de costo y el retiro
/// gravado no existe antes de I3; el ensamble no está disponible; ningún ajuste toca el tránsito; y cada clase emite
/// <c>AjusteInventarioAprobado</c> con la operación de la matriz y <c>causeCode</c> = código de la causa.
/// </summary>
public class EfectosDeAjusteTests
{
    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "(éxito)";

    [Fact]
    public async Task El_ajuste_positivo_sin_costo_entra_al_promedio_del_ambito()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);
        await k.EntradaAsync(k.P1, 10m, 1300m, k.Segunda);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", lineas: [k.Linea(k.P1, 5m)]));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var ultimo = await k.C.Db.KardexEntries.OrderByDescending(e => e.Id).FirstAsync();
        ultimo.UnitCost.Should().Be(1150m, "10 × 1.000 + 10 × 1.300 en toda la cooperativa promedian 1.150");
        ultimo.TotalCost.Should().Be(5750m);
    }

    [Fact]
    public async Task Con_existencia_cero_el_ajuste_positivo_entra_al_ultimo_costo()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 5m, 1000m);
        (await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 5m)]))).Confirmacion.IsSuccess.Should().BeTrue();

        (await k.AjusteAsync(k.Borrador("AJP", lineas: [k.Linea(k.P1, 2m)]))).Confirmacion.IsSuccess.Should().BeTrue();

        (await k.C.Db.KardexEntries.OrderByDescending(e => e.Id).FirstAsync()).UnitCost.Should().Be(1000m);
    }

    [Fact]
    public async Task El_costo_digitado_sin_permiso_es_un_422_con_el_permiso_que_falta()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Permisos.HasPermissionAsync(EfectoDeAjustePositivo.PermisoDeCosto, Arg.Any<CancellationToken>()).Returns(false);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", lineas: [k.Linea(k.P1, 5m, 1200m)]));

        Codigo(r).Should().Be("Inventory.Adjustment.UnitCostNotAllowed");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { lineNumber = 1, permissionCode = "Inventory.Adjustments.SetUnitCost" });
    }

    [Fact]
    public async Task El_costo_digitado_sin_permiso_vuelve_como_aviso_al_guardar()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Permisos.HasPermissionAsync(EfectoDeAjustePositivo.PermisoDeCosto, Arg.Any<CancellationToken>()).Returns(false);

        var r = await k.GuardarAsync(k.Borrador("AJP", lineas: [k.Linea(k.P1, 5m, 1200m)]));

        r.IsSuccess.Should().BeTrue("el aviso no detiene el guardado");
        r.Value.Warnings.Select(w => w.Code).Should().Contain("Inventory.Adjustment.UnitCostNotAllowed");
    }

    [Fact]
    public async Task Con_permiso_el_ajuste_positivo_entra_al_costo_digitado()
    {
        var k = await KardexDePrueba.CrearAsync();

        await k.EntradaAsync(k.P1, 5m, 1200m);

        var hecho = await k.C.Db.KardexEntries.SingleAsync();
        hecho.UnitCost.Should().Be(1200m);
        hecho.TotalCost.Should().Be(6000m);
    }

    [Fact]
    public async Task Una_salida_no_admite_costo_digitado()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 5m, 1000m);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 1m, 900m)]));

        Codigo(r).Should().Be("Inventory.Adjustment.UnitCostOnlyOnEntries");
    }

    [Theory]
    [InlineData("AJN")]
    [InlineData("BAJ")]
    public async Task El_negativo_y_la_baja_exigen_la_causa(string tipo)
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 5m, 1000m);

        var (_, r) = await k.AjusteAsync(k.Borrador(tipo, lineas: [k.Linea(k.P1, 1m)]));

        Codigo(r).Should().Be("Inventory.Document.FieldRequired");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { field = "adjustmentCause" });
    }

    [Fact]
    public async Task El_consumo_interno_exige_centro_de_costo()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 5m, 1000m);

        var (_, r) = await k.AjusteAsync(k.Borrador("CI", lineas: [k.Linea(k.P1, 1m)]));

        Codigo(r).Should().Be("Inventory.Document.FieldRequired");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { field = "costCenter" });
    }

    [Fact]
    public async Task El_retiro_gravado_no_esta_disponible_antes_de_I3()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 5m, 1000m);
        k.Tipo("CI").IsTaxableWithdrawal = true;
        await k.C.Db.SaveChangesAsync();

        var (_, r) = await k.AjusteAsync(k.Borrador("CI", centro: k.Centro.PublicId, lineas: [k.Linea(k.P1, 1m)]));

        Codigo(r).Should().Be("Inventory.Adjustment.TaxableWithdrawalNotAvailable");
    }

    [Fact]
    public async Task El_ensamble_no_esta_disponible()
    {
        var k = await KardexDePrueba.CrearAsync();

        var r = await k.GuardarAsync(k.Borrador("ENS", lineas: [k.Linea(k.P1, 1m)]));

        Codigo(r).Should().Be("Inventory.DocumentClass.NotAvailable");
    }

    [Theory]
    [InlineData("AJP")]
    [InlineData("BAJ")]
    public async Task Ningun_ajuste_entra_al_transito_ni_sale_de_el(string tipo)
    {
        var k = await KardexDePrueba.CrearAsync();

        var (_, r) = await k.AjusteAsync(k.Borrador(tipo, k.Transito, k.Causa(), lineas: [k.Linea(k.P1, 1m, tipo == "AJP" ? 100m : null)]));

        Codigo(r).Should().Be("Inventory.Document.TransitNotAllowed");
    }

    [Theory]
    [InlineData("AJP", "AjustePositivo", null)]
    [InlineData("AJN", "AjusteNegativo", "MERMA")]
    [InlineData("CI", "ConsumoInterno", null)]
    [InlineData("BAJ", "Baja", "DANO")]
    public async Task Cada_clase_emite_AjusteInventarioAprobado_con_su_operacion_y_la_causa(string tipo, string operacion, string? causa)
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 5m, 1000m);
        var antes = await k.C.Db.IntegrationMessages.CountAsync();

        var (documento, r) = await k.AjusteAsync(k.Borrador(tipo, causa: causa is null ? null : k.Causa(causa),
            centro: tipo == "CI" ? k.Centro.PublicId : null, lineas: [k.Linea(k.P1, 2m, tipo == "AJP" ? 1000m : null)]));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        (await k.C.Db.IntegrationMessages.CountAsync()).Should().Be(antes + 1);
        var mensaje = await k.C.Db.IntegrationMessages.SingleAsync(m => m.OriginPublicId == documento);
        mensaje.Type.Should().Be(AjusteInventarioAprobadoV1.Type);
        mensaje.OriginEventKey.Should().Be("Confirmation");
        mensaje.PayloadJson.Should().Contain($"\"operation\":\"{operacion}\"");
        mensaje.PayloadJson.Should().Contain(causa is null ? "\"causeCode\":null" : $"\"causeCode\":\"{causa}\"");
        var linea = System.Text.Json.JsonDocument.Parse(mensaje.PayloadJson).RootElement.GetProperty("lines").EnumerateArray().Single();
        linea.GetProperty("accountingGroupCode").GetString().Should().Be("ABARR");
        linea.GetProperty("warehouseCode").GetString().Should().Be("PRIN");
        linea.GetProperty("quantityBase").GetDecimal().Should().Be(2m, "positivo: es el efecto natural del mensaje");
        linea.GetProperty("cost").GetDecimal().Should().Be(2000m);
        linea.GetProperty("movement").GetString().Should().Be(tipo == "AJP" ? "Entry" : "Exit");
        linea.GetProperty("documentLines").EnumerateArray().Select(n => n.GetInt32()).Should().Equal(1);
    }

    [Fact]
    public async Task El_monto_que_se_aprueba_es_el_valor_al_costo_vigente()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);
        var guardado = await k.GuardarAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 3m)]));

        await k.ConfirmarAsync(guardado.Value.PublicId);

        await k.Motor.Received().EvaluarAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<DateOnly>(), 3000m, "Inventory.Adjustments.Confirm", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_borrador_avisa_la_existencia_que_falta_sin_bloquear()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 1m, 1000m);

        var r = await k.GuardarAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 3m)]));

        r.IsSuccess.Should().BeTrue();
        r.Value.Warnings.Should().Contain(w => w.Code == "Inventory.Stock.Insufficient");
        r.Value.Status.Should().Be(DocumentStatus.Draft);
    }

    [Fact]
    public void Cada_clase_de_ajuste_de_I1_tiene_su_estrategia()
    {
        new[] { typeof(EfectoDeAjustePositivo), typeof(EfectoDeAjusteNegativo), typeof(EfectoDeConsumoInterno), typeof(EfectoDeBaja) }
            .Should().OnlyContain(t => typeof(IEfectoDeClase).IsAssignableFrom(t));
    }
}
