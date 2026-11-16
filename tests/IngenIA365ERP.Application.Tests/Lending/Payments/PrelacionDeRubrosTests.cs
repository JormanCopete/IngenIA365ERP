using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// 2026-11-16 (pedido del dueño; <c>Pagos.PrelacionDeRubros</c>): el orden de rubros de la empresa por el contrato real. El pago lo lee a su fecha valor
/// (sin vigencia, el de fábrica), reparte con él y lo sella en su decisión; con otro orden de la empresa, el capital puede ir primero; una vigencia
/// registrada después —aunque rija desde antes— no cambia un pago ya aplicado; un pago sin el orden sellado (anterior al parámetro) se reconstruye con
/// la prelación de la línea; y una línea con su propia lista de conceptos manda sobre el parámetro. El escenario es el de <see cref="PagosDePrueba"/> (Ana,
/// 3.000.000 a 24 cuotas desde el 01-12-2026): el 10-02-2027 las cuotas 1 y 2 están vencidas.
/// </summary>
public class PrelacionDeRubrosTests : IAsyncLifetime
{
    private static readonly DateOnly Vencidas = new(2027, 2, 10);
    private PagosDePrueba _p = null!;

    public async Task InitializeAsync()
    {
        _p = await PagosDePrueba.CrearAsync();
        await _p.CausarHastaAsync(Vencidas);
    }

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private static void Orden(TestApplicationDbContext db, string valor, DateOnly desde)
    {
        db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.PagosPrelacionDeRubros, Value = valor, ValidFrom = desde, Reason = "prueba",
            ScopeKind = ParameterScopeKind.None, CreatedBy = "test",
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    /// <summary>La distribución del pago tal como la reconstruye hoy el motor (la del libro).</summary>
    private async Task<DistribucionDePago> ReconstruidoAsync(Guid operacion)
    {
        var op = await _p.OperacionAsync(operacion);
        var cargado = await _p.C.Cargador().CargarAsync(_p.Credito.Id, null, Vencidas, default);
        return MotorDeCartera.Reconstruir(cargado!.Entrada!, Vencidas).Pagos[CargadorDeReconstruccion.Id(op.Id)];
    }

    [Fact]
    public async Task Con_el_parametro_de_fabrica_el_pago_reparte_interes_antes_que_capital_y_sella_el_orden()
    {
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.PastDue, 150_000m, Vencidas));

        c.Distribution.Select(l => (l.InstallmentNumber, l.Concept)).Take(3).Should().Equal(
            (1, ConceptoDeObligacion.DefaultInterest), (1, ConceptoDeObligacion.CurrentInterest), (1, ConceptoDeObligacion.Principal));
        var decision = DecisionDePago.Leer((await _p.OperacionAsync(c.OperationPublicId)).Payload)!;
        decision.AllocationRubrics.Should().Be(OrdenDeRubros.Defecto);
        decision.Contenido().Rubros.Should().Be(OrdenDeRubros.DeFabrica, "la reconstrucción lo repite desde la decisión sellada");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Con_otro_orden_de_la_empresa_el_capital_va_primero_y_una_vigencia_posterior_no_cambia_lo_aplicado()
    {
        Orden(_p.Db, "Capital,Interes,Seguro", new DateOnly(2026, 1, 1));

        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.PastDue, 150_000m, Vencidas));

        c.Distribution.First().Should().Match<LineaDeDistribucionDto>(l => l.InstallmentNumber == 1 && l.Concept == ConceptoDeObligacion.Principal,
            "el capital de la cuota 1 primero");
        DecisionDePago.Leer((await _p.OperacionAsync(c.OperationPublicId)).Payload)!.AllocationRubrics.Should().Be("Capital,Interes,Seguro");
        var antes = await ReconstruidoAsync(c.OperationPublicId);

        // La empresa vuelve al de fábrica con una vigencia que rige desde antes del pago: el pago ya aplicado no cambia.
        _p.Db.ParameterVersions.Single(v => v.Key == ParametrosDeCartera.PagosPrelacionDeRubros).ValidTo = new DateOnly(2026, 12, 31);
        _p.Db.SaveChanges();
        Orden(_p.Db, OrdenDeRubros.Defecto, new DateOnly(2027, 1, 1));
        (await _p.Prelacion().OrdenAsync(Vencidas, default)).Should().Be(OrdenDeRubros.DeFabrica);

        (await ReconstruidoAsync(c.OperationPublicId)).Lineas.Should().Equal(antes.Lineas, "lo que se aplicó con un orden no se vuelve a juzgar al reconstruir");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Un_pago_sin_el_orden_sellado_se_reconstruye_con_la_prelacion_de_la_linea()
    {
        DecisionDePago.Leer("{\"modality\":\"PastDue\",\"received\":100,\"applied\":100,\"valueDate\":\"2027-02-10\"}")!.Contenido().Rubros
            .Should().BeNull("un pago anterior al parámetro no trae el orden: la prelación de la línea");
        DecisionDeResolucion.Leer("{\"concept\":\"CreditBalance\",\"amount\":100,\"form\":\"ApplyAsExtraordinaryPrincipal\",\"choice\":\"ReduceTerm\"}")!
            .Contenido().Should().Match<ContenidoDeResolucion>(r => r.Rubros == null && !r.CuotaCompleta);

        Orden(_p.Db, "Capital,Interes,Seguro", new DateOnly(2026, 1, 1));
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.PastDue, 150_000m, Vencidas));
        var op = await _p.OperacionAsync(c.OperationPublicId);
        var decision = DecisionDePago.Leer(op.Payload)!;
        decision.AllocationRubrics.Should().Be("Capital,Interes,Seguro");
        (decision with { AllocationRubrics = null }).Serializar().Should().NotContain("allocationRubrics", "sin el orden, la decisión se sella como antes");

        // El mismo pago como si fuera de antes del parámetro (su decisión sin el campo), con el parámetro que rige hoy pidiendo el capital primero.
        var cargado = await _p.C.Cargador().CargarAsync(_p.Credito.Id, null, Vencidas, default);
        var entrada = cargado!.Entrada!;
        var id = CargadorDeReconstruccion.Id(op.Id);
        var viejo = entrada with
        {
            Operaciones = [.. entrada.Operaciones.Select(o => o.Id == id ? o with { Contenido = (decision with { AllocationRubrics = null }).Contenido() } : o)],
        };

        var conOrden = MotorDeCartera.Reconstruir(entrada, Vencidas).Pagos[id];
        var sinOrden = MotorDeCartera.Reconstruir(viejo, Vencidas).Pagos[id];

        conOrden.Lineas.First().Concepto.Should().Be(LoanConcept.Principal);
        sinOrden.Lineas.First().Concepto.Should().NotBe(LoanConcept.Principal,
            "sin el orden sellado se reconstruye con la prelación de la línea (intereses antes que capital), no con el parámetro que rige hoy");
        sinOrden.Aplicado.Should().Be(conOrden.Aplicado);
    }

    [Fact]
    public async Task Una_linea_con_su_propia_lista_de_conceptos_manda_y_no_se_sella_nada()
    {
        Orden(_p.Db, "Capital,Interes,Seguro", new DateOnly(2026, 1, 1));
        var propia = Prelacion.DesdeSellada(AllocationOrder.OldestInstallmentFirst, "[\"CurrentInterest\",\"Principal\"]", true, false);

        var deLaLinea = await _p.Prelacion().ParaElPagoAsync(propia, Vencidas, default);
        var deLaEmpresa = await _p.Prelacion().ParaElPagoAsync(Prelacion.PorDefecto, Vencidas, default);

        deLaLinea.Should().Be(PrelacionDelPago.DeLaLinea(propia), "la lista propia de la línea es más específica que el orden de la empresa");
        deLaEmpresa.Rubros.Should().Be(OrdenDeRubros.Leer("Capital,Interes,Seguro"));
        deLaEmpresa.Prelacion.ConceptosEnOrden[1].Should().Be(LoanConcept.Principal);
    }

    [Fact]
    public async Task Un_valor_guardado_que_no_sirve_se_lee_como_el_de_fabrica()
    {
        Orden(_p.Db, "Capital,Capital", new DateOnly(2026, 1, 1));

        (await _p.Prelacion().OrdenAsync(Vencidas, default)).Should().Be(OrdenDeRubros.DeFabrica);
    }
}
