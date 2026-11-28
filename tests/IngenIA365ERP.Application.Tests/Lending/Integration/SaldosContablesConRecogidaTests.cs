using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Application.Tests.Lending.Sustitutos;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 028 (T049; FR-027, H5, E-09): con el contrato de obligaciones real (<see cref="PagosDePrueba"/>), el crédito nuevo de 5.000.000 recoge el de Ana y
/// la contabilidad que sale de los hechos que Cartera emitió —partida por partida, con el primer paso del constructor de Contabilidad, que no necesita la
/// matriz— cuadra con lo que la conciliación de Cartera espera:
/// <list type="bullet">
/// <item>el puente de la recogida propia (<c>RecaudoPorAplicar</c> de <c>OwnLoanPayoff</c>) queda en cero por crédito recogido, tercero y sucursal: el
/// desembolso lo acredita y el recibo de la recogida lo debita con las mismas dimensiones;</item>
/// <item>«desembolso por girar» es el neto de las órdenes abiertas;</item>
/// <item>con la orden rechazada, el reverso devuelve «desembolso por girar» a cero —la conciliación ya no cuenta la orden rechazada como abierta— y el
/// puente espera lo recogido hasta el giro nuevo, que lo vuelve a dejar en cero; si en vez del giro nuevo el crédito se anula, el reverso de la recogida
/// lo cierra.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class SaldosContablesConRecogidaTests
{
    private const decimal Bruto = 5_000_000m;
    private const string Puente = "RecaudoPorAplicar";
    private const string PorGirar = "DesembolsoPorGirar";

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private static async Task<DisbursementResultDto> DesembolsadoConRecogidaAsync(PagosDePrueba p)
    {
        var s = await p.D.AprobadaAsync(p.D.O.Solicitud(monto: Bruto) with { Payoffs = [DesembolsoConDiferenciasTests.Total(p.Credito)] });
        var d = await p.D.DesembolsadoAsync(p.D.Entrada(s) with { AcknowledgedPayoffDifferences = true }, p.Contrato());
        d.Payoffs.Should().ContainSingle();
        p.Db.ChangeTracker.Clear();
        return d;
    }

    private static async Task ResultadoAsync(PagosDePrueba p, Guid orden, string resultado)
    {
        var emitido = await p.D.EmitirResultado().Handle(new EmitTestDisbursementResultCommand(orden, resultado, "TRF-9", p.G.HoyLocal)
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        emitido.IsSuccess.Should().BeTrue(emitido.IsFailure ? $"{emitido.Error.Code}: {emitido.Error.Message}" : string.Empty);
        p.Db.ChangeTracker.Clear();
        var consumido = await p.D.Consumir().Handle(new ConsumeTreasuryResultCommand([emitido.Value.MessagePublicId]), default);
        consumido.IsSuccess.Should().BeTrue(consumido.IsFailure ? consumido.Error.Message : string.Empty);
        p.Db.ChangeTracker.Clear();
    }

    private static async Task<DisbursementOrderDto> GiroNuevoAsync(PagosDePrueba p, Guid orden)
    {
        var r = await p.D.GirarDeNuevo().Handle(new ReissueDisbursementOrderCommand(orden, DisbursementForm.Transfer,
            new BeneficiaryInput(p.D.O.Ana.PublicId, p.D.Banco.PublicId, BeneficiaryInput.Corriente, "777"), "Cuenta corregida") { OperationKey = Guid.NewGuid() }, default);
        p.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>
    /// Las partidas de todos los hechos de Cartera a Contabilidad emitidos hasta ahora, unidad por unidad (mismo origen y misma clave de evento), con el
    /// primer paso del constructor (<see cref="ConstructorDeLineasDeCartera.Desglosar"/>): lo que la contabilidad asienta, sin la matriz.
    /// </summary>
    private static async Task<IReadOnlyList<ConstructorDeLineasDeCartera.Partida>> PartidasAsync(PagosDePrueba p)
    {
        var mensajes = await p.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginModule == ModulosDeOrigen.Cartera).OrderBy(m => m.Id).ToListAsync();
        return mensajes.Where(m => ConstructorDeLineasDeCartera.VaAContabilidad(m.Type))
            .GroupBy(m => (m.OriginPublicId, m.OriginEventKey))
            .SelectMany(g =>
            {
                var unidad = g.Select(m =>
                {
                    var contenido = ConstructorDeLineasDeCartera.ContenidoTipado(m.Type, m.PayloadJson);
                    return new HechoDeCartera(MensajesEntrantes.Sobre(m, contenido), contenido, m.OriginDocumentClass);
                }).ToList();
                var desglose = ConstructorDeLineasDeCartera.Desglosar(unidad);
                desglose.Fallos.Should().BeEmpty($"los hechos de {g.First().Type} {g.First().OriginNumber} cuadran");
                return desglose.Partidas;
            })
            .ToList();
    }

    private static decimal Debito(ConstructorDeLineasDeCartera.Partida x) => x.DebitoSiPositivo ? x.Importe : -x.Importe;

    /// <summary>El puente de la recogida propia, débito − crédito, por crédito recogido, tercero y sucursal (como saldaría con su cuenta, tercero y cruce).</summary>
    private static IReadOnlyDictionary<(string? Credito, Guid? Tercero, Guid? Sucursal), decimal> ElPuente(IEnumerable<ConstructorDeLineasDeCartera.Partida> partidas) =>
        partidas.Where(x => x.Rol == Puente && x.Valores.CollectorClass == ConstructorDeLineasDeCartera.ClaseDeLaRecogidaPropia)
            .GroupBy(x => (x.Credito, x.Tercero ?? x.TerceroDelSobre, x.Valores.BranchPublicId))
            .ToDictionary(g => g.Key, g => g.Sum(Debito));

    /// <summary>«Desembolso por girar», crédito − débito (lo que espera girar Tesorería).</summary>
    private static decimal PorGirarDe(IEnumerable<ConstructorDeLineasDeCartera.Partida> partidas) => -partidas.Where(x => x.Rol == PorGirar).Sum(Debito);

    private static SaldosContablesDeCartera Saldos(PagosDePrueba p) => new(p.Db, p.D.Contabilidad);

    private static async Task<decimal> OrdenesAbiertasAsync(PagosDePrueba p) =>
        (await Saldos(p).OrdenesAbiertasAsync(p.G.HoyLocal, null, default)).Sum(s => s.Saldo);

    private static async Task<decimal> PuenteEsperadoAsync(PagosDePrueba p) =>
        (await Saldos(p).PuentesEnCeroAsync(p.G.HoyLocal, default))
        .Single(s => s.Clave.Rol == Puente && s.Clave.ClaseDeRecaudador == ConstructorDeLineasDeCartera.ClaseDeLaRecogidaPropia).Saldo;

    // -------------------------------------------------------------------------------------------------------- pruebas --

    [Fact]
    public async Task El_puente_de_la_recogida_queda_en_cero_y_desembolso_por_girar_es_el_neto_de_las_ordenes_abiertas()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var abiertasAntes = await OrdenesAbiertasAsync(p);
        var antes = PorGirarDe(await PartidasAsync(p));
        antes.Should().Be(abiertasAntes, "el crédito de Ana tiene su propia orden abierta");

        var d = await DesembolsadoConRecogidaAsync(p);
        var recogida = d.Payoffs.Single();
        var partidas = await PartidasAsync(p);

        ElPuente(partidas).Should().NotBeEmpty().And.OnlyContain(x => x.Value == 0m,
            "el desembolso acredita el puente y el recibo de la recogida lo debita con el mismo crédito, tercero y sucursal");
        var sucursal = await p.Db.Branches.AsNoTracking().Where(b => b.Id == p.Credito.BranchId).Select(b => (Guid?)b.PublicId).SingleAsync();
        ElPuente(partidas).Keys.Should().ContainSingle().Which.Should().Be(((string?)p.Credito.Number, (Guid?)p.D.O.Ana.PublicId, sucursal));
        (PorGirarDe(partidas) - antes).Should().Be(Bruto - recogida.Applied, "«desembolso por girar» lleva sólo el neto");
        (await OrdenesAbiertasAsync(p)).Should().Be(PorGirarDe(partidas), "lo que la conciliación espera es lo que la contabilidad asienta");
        (await PuenteEsperadoAsync(p)).Should().Be(0m);

        // T045: el hecho lleva lo que el comprobante necesita para el puente.
        var hecho = await p.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == CreditoDesembolsadoV1.Type).OrderByDescending(m => m.Id).FirstAsync();
        var deduccion = JsonDocument.Parse(hecho.PayloadJson).RootElement.GetProperty("payoffDeductions").EnumerateArray().Single();
        deduccion.GetProperty("productKind").GetString().Should().Be(PayoffDeductionV1.Credito);
        deduccion.GetProperty("payerPersonPublicId").GetGuid().Should().Be(p.D.O.Ana.PublicId);
        deduccion.GetProperty("branchPublicId").GetGuid().Should().NotBeEmpty();
        deduccion.GetProperty("accountRef").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Con_la_orden_rechazada_el_puente_espera_lo_recogido_hasta_el_giro_nuevo()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var abiertasAntes = await OrdenesAbiertasAsync(p);
        var d = await DesembolsadoConRecogidaAsync(p);
        var recogido = d.Payoffs.Single().Applied;

        await ResultadoAsync(p, d.Order!.PublicId, "Rejected");

        var rechazado = await PartidasAsync(p);
        (PorGirarDe(rechazado) - abiertasAntes).Should().Be(0m, "el reverso le devolvió a «desembolso por girar» todo lo que el desembolso le acreditó");
        (await OrdenesAbiertasAsync(p)).Should().Be(abiertasAntes,
            "una orden rechazada ya tiene su reverso: no está abierta (E-09; hasta la 028 se contaba hasta el giro nuevo y descuadraba por el neto)");
        ElPuente(rechazado).Values.Sum().Should().Be(recogido, "el reverso le devolvió al puente lo recogido, que sigue aplicado hasta el giro nuevo");
        (await PuenteEsperadoAsync(p)).Should().Be(recogido, "la conciliación espera en el puente lo que aguarda el giro nuevo");

        var giro = await GiroNuevoAsync(p, d.Order.PublicId);

        var girado = await PartidasAsync(p);
        ElPuente(girado).Should().OnlyContain(x => x.Value == 0m, "el giro nuevo vuelve a acreditar las mismas recogidas");
        (PorGirarDe(girado) - abiertasAntes).Should().Be(giro.NetAmount);
        (await OrdenesAbiertasAsync(p)).Should().Be(PorGirarDe(girado));
        (await PuenteEsperadoAsync(p)).Should().Be(0m);
    }

    [Fact]
    public async Task Con_la_orden_rechazada_y_el_credito_anulado_el_reverso_de_la_recogida_cierra_el_puente()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var abiertasAntes = await OrdenesAbiertasAsync(p);
        var d = await DesembolsadoConRecogidaAsync(p);
        await ResultadoAsync(p, d.Order!.PublicId, "Rejected");

        var anulado = await new VoidPendingLoanCommandHandler(p.Db, p.Contrato(), p.D.O.Cerrojo, p.G.Alcance, p.G.Auditoria, p.G.Reloj)
            .Handle(new VoidPendingLoanCommand(d.LoanPublicId, "El asociado desistió") { OperationKey = Guid.NewGuid() }, default);
        p.Db.ChangeTracker.Clear();
        anulado.IsSuccess.Should().BeTrue(anulado.IsFailure ? $"{anulado.Error.Code}: {anulado.Error.Message}" : string.Empty);

        var partidas = await PartidasAsync(p);
        ElPuente(partidas).Should().OnlyContain(x => x.Value == 0m, "el reverso de la recogida le quita al puente lo que el reverso del desembolso le devolvió");
        (PorGirarDe(partidas) - abiertasAntes).Should().Be(0m);
        (await OrdenesAbiertasAsync(p)).Should().Be(abiertasAntes);
        (await PuenteEsperadoAsync(p)).Should().Be(0m, "anulado el crédito, nada espera un giro");
    }
}
