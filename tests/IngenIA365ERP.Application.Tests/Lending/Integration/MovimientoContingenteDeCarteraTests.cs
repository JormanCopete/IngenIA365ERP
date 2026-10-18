using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US7, T0630 (contabilidad.md §19; mensajes.md §10.24; C15, C91, C92, C94) (dueño: pregunta A3; rige emitir los tres en
/// <c>NoPasa</c>): las cuentas contingentes. Aprobación (+), desembolso de cada tramo, vencimiento, desistimiento y anulación (−); cupo rotativo
/// otorgado o ampliado (+), reducido o vencido (−), usarlo no lo mueve (C92); <c>balanceAfter</c>; la primera emisión de un origen con
/// <c>Confirmation</c> y las siguientes con su propia clave; lo detecta el ciclo cuando vence; <c>AdmiteProceso</c> sólo en los tres y los de T11.
/// Sobre <see cref="DesembolsoDePrueba"/> con los tipos contingentes sembrados.
/// </summary>
public class MovimientoContingenteDeCarteraTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private static readonly DateOnly Hoy = DesembolsoDePrueba.Hoy;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _d.Db.LendingFactTypes.AddRange(
            new LendingFactType { Code = CreditoAprobadoSinDesembolsarV1.Type, ShortCode = "CAD", Name = "Aprobado", DefaultMode = "NoPasa", CreatedBy = "test" },
            new LendingFactType { Code = CupoRotativoOtorgadoV1.Type, ShortCode = "CRO", Name = "Cupo", DefaultMode = "NoPasa", CreatedBy = "test" },
            new LendingFactType { Code = GarantiaConstituidaV1.Type, ShortCode = "GCT", Name = "Garantía", DefaultMode = "NoPasa", CreatedBy = "test" });
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private MovimientoContingenteDeCartera Contingentes() => new(_d.Db, _d.Emision(), _d.FechaContable(), _d.G.Reloj);

    private async Task<List<(IntegrationMessage Mensaje, JsonElement Contenido)>> HechosAsync(string tipo) =>
        (await _d.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == tipo).OrderBy(m => m.Id).ToListAsync())
        .Select(m => (m, JsonDocument.Parse(m.PayloadJson).RootElement.Clone())).ToList();

    private static (string Movimiento, decimal Variacion, decimal Saldo) Leer(JsonElement c) =>
        (c.GetProperty("movement").GetString()!, c.GetProperty("amounts")[0].GetProperty("amount").GetDecimal(), c.GetProperty("balanceAfter").GetDecimal());

    private async Task<CreditApplication> SolicitudAsync(Guid publicId) => await _d.Db.CreditApplications.SingleAsync(s => s.PublicId == publicId);

    private async Task SincronizarAsync(Guid solicitud, DateOnly? fecha = null)
    {
        var s = await SolicitudAsync(solicitud);
        (await Contingentes().SolicitudAsync(s, fecha, default)).IsSuccess.Should().BeTrue();
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
    }

    // ----------------------------------------------------------------------------------------- aprobado sin desembolsar --

    /// <summary>
    /// Defecto del 2026-10-13 (e2e de Cartera): el número del cupo rotativo (<c>CUP-</c> + doce hexadecimales) pasaba entero como
    /// <c>OriginDocumentTypeCode</c> cuando terminaba en letra, y el <c>varchar(10)</c> reventaba el otorgamiento. El tipo de documento del sobre es el
    /// prefijo hasta el guion y nunca pasa de diez caracteres.
    /// </summary>
    [Theory]
    [InlineData("CUP-1A2B3C4D5E6F", "CreditLimit", "CUP-")]
    [InlineData("CUP-1A2B3C4D5E60", "CreditLimit", "CUP-")]
    [InlineData("SOL000123", "CreditApplication", "SOL")]
    [InlineData("ABCDEFGHIJKLMNO", "CreditLimit", "ABCDEFGHIJ")]
    public void El_tipo_de_documento_del_sobre_es_el_prefijo_del_numero_y_cabe_en_diez(string numero, string clase, string esperado) =>
        MovimientoContingenteDeCartera.TipoDeDocumentoDe(numero, clase).Should().Be(esperado);

    [Fact]
    public async Task Aprobar_sube_el_contingente_por_lo_aprobado_y_repetir_no_emite_nada()
    {
        var s = await _d.AprobadaAsync();
        var aprobado = (await SolicitudAsync(s.PublicId)).ApprovedAmount!.Value;

        await SincronizarAsync(s.PublicId);
        await SincronizarAsync(s.PublicId);

        var hecho = (await HechosAsync(CreditoAprobadoSinDesembolsarV1.Type)).Should().ContainSingle().Subject;
        Leer(hecho.Contenido).Should().Be(("Approved", aprobado, aprobado));
        hecho.Contenido.GetProperty("amounts")[0].GetProperty("reasonCode").GetString().Should().Be("ApprovedNotDisbursed");
        hecho.Contenido.GetProperty("subject").GetProperty("kind").GetString().Should().Be("CreditApplication");
        (hecho.Mensaje.OriginPublicId, hecho.Mensaje.OriginEventKey, hecho.Mensaje.OriginDocumentClass).Should().Be((s.PublicId, "Confirmation", "CreditApplication"));
        (await _d.Db.IntegrationMessageDeliveries.SingleAsync(x => x.MessageId == hecho.Mensaje.Id)).Mode.Should().Be(DeliveryMode.NotPosted,
            "A3: los contingentes nacen NoPasa y la contadora decide cuándo pasan");
    }

    [Fact]
    public async Task Cada_tramo_desembolsado_lo_baja_y_despues_no_queda_nada_que_poner_al_dia()
    {
        var s = await _d.AprobadaAsync();
        await SincronizarAsync(s.PublicId);
        var r = await _d.DesembolsadoAsync(_d.Entrada(s));

        await SincronizarAsync(s.PublicId);

        var hechos = await HechosAsync(CreditoAprobadoSinDesembolsarV1.Type);
        hechos.Select(h => Leer(h.Contenido)).Should().Equal(("Approved", r.GrossAmount, r.GrossAmount), ("Disbursed", -r.GrossAmount, 0m));
        hechos[1].Mensaje.OriginEventKey.Should().StartWith("Confirmation:", "lo mueve otra operación: Confirmation:{tipoDeHecho:N} (C94)");
    }

    [Fact]
    public async Task Al_vencer_la_aprobacion_el_ciclo_lo_baja()
    {
        var s = await _d.AprobadaAsync();
        await SincronizarAsync(s.PublicId);
        var vence = (await SolicitudAsync(s.PublicId)).ApprovalValidUntil!.Value;
        _d.Db.ChangeTracker.Clear();

        (await Contingentes().VencimientosAsync(vence, default)).Value.Should().Be(0, "el último día de vigencia todavía no vence");
        (await Contingentes().VencimientosAsync(vence.AddDays(1), default)).Value.Should().Be(1);
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        (await Contingentes().VencimientosAsync(vence.AddDays(2), default)).Value.Should().Be(0, "ya se bajó: idempotente");

        var hechos = await HechosAsync(CreditoAprobadoSinDesembolsarV1.Type);
        Leer(hechos[^1].Contenido).Should().Be(("Expired", -hechos[0].Contenido.GetProperty("balanceAfter").GetDecimal(), 0m));
        hechos[^1].Mensaje.OriginEventKey.Should().NotBe(hechos[0].Mensaje.OriginEventKey, "un segundo hecho del mismo origen lleva su propia clave");
    }

    [Theory]
    [InlineData("Desistimiento", "Withdrawn")]
    [InlineData("Anulación", "Voided")]
    public async Task Desistir_o_anular_una_aprobada_la_baja(string prefijo, string movimiento)
    {
        var s = await _d.AprobadaAsync();
        await SincronizarAsync(s.PublicId);
        var solicitud = await SolicitudAsync(s.PublicId);
        solicitud.Cancelar(_d.Cajera.Id, DateTime.UtcNow, $"{prefijo}: el asociado ya no lo necesita");
        (await Contingentes().SolicitudAsync(solicitud, null, default)).IsSuccess.Should().BeTrue();
        await _d.Db.SaveChangesAsync();

        Leer((await HechosAsync(CreditoAprobadoSinDesembolsarV1.Type))[^1].Contenido).Movimiento.Should().Be(movimiento);
    }

    [Fact]
    public async Task Sin_la_fila_del_tipo_A3_rechazado_no_emite()
    {
        _d.Db.LendingFactTypes.RemoveRange(_d.Db.LendingFactTypes);
        await _d.Db.SaveChangesAsync();
        var s = await _d.AprobadaAsync();

        await SincronizarAsync(s.PublicId);

        (await HechosAsync(CreditoAprobadoSinDesembolsarV1.Type)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------------------ cupo rotativo --

    private async Task<CreditLimit> CupoAsync(decimal monto, int version, DateOnly desde)
    {
        var linea = await _d.Db.CreditLines.AsNoTracking().Where(l => l.Code == "CONLIB").Select(l => l.Id).SingleAsync();
        var cupo = CreditLimit.NuevaVersion(_d.O.Ana.Id, CreditLimitKind.Revolving, linea, monto, CreditLimitOrigin.Assigned, desde, version, DateTime.UtcNow,
            "cupo de prueba", _d.Cajera.Id);
        _d.Db.CreditLimits.Add(cupo);
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        return cupo;
    }

    [Fact]
    public async Task El_cupo_rotativo_sube_al_otorgarse_y_ampliarse_y_baja_al_reducirse_o_vencer()
    {
        var otorgado = await CupoAsync(2_000_000m, 1, Hoy);
        (await Contingentes().CupoAsync(otorgado, Hoy, default)).Value.Should().NotBeNull();
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var ampliado = await CupoAsync(3_000_000m, 2, Hoy.AddDays(1));
        var reducido = await CupoAsync(2_500_000m, 3, Hoy.AddDays(2));
        await CupoAsync(0m, 4, Hoy.AddDays(3));
        for (var i = 1; i <= 3; i++)
        {
            (await Contingentes().VencimientosAsync(Hoy.AddDays(i), default)).IsSuccess.Should().BeTrue();
            await _d.Db.SaveChangesAsync();
            _d.Db.ChangeTracker.Clear();
        }

        var hechos = await HechosAsync(CupoRotativoOtorgadoV1.Type);
        hechos.Select(h => Leer(h.Contenido)).Should().Equal(
            ("Granted", 2_000_000m, 2_000_000m), ("Increased", 1_000_000m, 3_000_000m), ("Reduced", -500_000m, 2_500_000m), ("Expired", -2_500_000m, 0m));
        hechos.Should().OnlyContain(h => h.Contenido.GetProperty("subject").GetProperty("publicId").GetGuid() == otorgado.PublicId,
            "el sujeto es el cupo (su primera versión), no cada versión");
        hechos.Should().OnlyContain(h => h.Contenido.GetProperty("amounts")[0].GetProperty("reasonCode").GetString() == "RevolvingLimit");
        hechos.Select(h => h.Mensaje.OriginEventKey).Should().OnlyHaveUniqueItems();
        _ = (ampliado, reducido);
    }

    [Fact]
    public async Task Usar_el_cupo_o_pagarlo_no_lo_mueve()
    {
        var cupo = await CupoAsync(5_000_000m, 1, Hoy);
        await Contingentes().CupoAsync(cupo, Hoy, default);
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        var s = await _d.AprobadaAsync();
        await _d.DesembolsadoAsync(_d.Entrada(s));

        (await Contingentes().CupoAsync(cupo, Hoy, default)).Value.Should().BeNull("913005 mide el cupo otorgado, no el usado (C92)");
        (await HechosAsync(CupoRotativoOtorgadoV1.Type)).Should().ContainSingle();
    }

    [Fact]
    public async Task Los_cupos_consolidado_y_por_linea_no_son_contingentes()
    {
        var consolidado = CreditLimit.NuevaVersion(_d.O.Ana.Id, CreditLimitKind.Consolidated, null, 1_000_000m, CreditLimitOrigin.Assigned, Hoy, 1,
            DateTime.UtcNow, "consolidado", _d.Cajera.Id);

        (await Contingentes().CupoAsync(consolidado, Hoy, default)).Value.Should().BeNull();
    }

    // --------------------------------------------------------------------------------------------------------- catálogo --

    [Fact]
    public void Admiten_proceso_solo_los_tres_contingentes_y_los_de_T11()
    {
        var conProceso = CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Where(t => t.AdmiteProceso).Select(t => t.Type).ToList();

        conProceso.Should().Contain(MovimientoContingenteDeCartera.Tipos);
        conProceso.Except(MovimientoContingenteDeCartera.Tipos).Should().BeEquivalentTo(
            InteresCausadoV1.Type, MoraCausadaV1.Type, SeguroCausadoV1.Type, AjusteDeCausacionPorFechaValorV1.Type, CausacionSuspendidaV1.Type,
            CausacionReanudadaV1.Type, AnticipoAplicadoV1.Type, SaldoAFavorReconocidoV1.Type, DimensionContableCambiadaV1.Type, DeterioroCalculadoV1.Type,
            CarteraReclasificadaPorCalificacionV1.Type, ReversoDeCierreV1.Type, CierreDeCarteraRealizadoV1.Type, CierreDeCarteraReabiertoV1.Type,
            // K6b (T1056): la corrección monetaria UVR la emite el ciclo diario, como la causación.
            CorreccionMonetariaUvrCausadaV1.Type,
            // K6b (decisión del dueño del 2026-10-17): el reparto de un pago corregido por la UVR lo emite la corrida de corrección.
            PagoRedistribuidoPorValorNacionalV1.Type);
        ClavesDeEvento.EsValida(CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Single(t => t.Type == CupoRotativoOtorgadoV1.Type).FormaDeClave,
            ClavesDeEvento.ConfirmacionPor(Guid.NewGuid())).Should().BeTrue();
    }
}
