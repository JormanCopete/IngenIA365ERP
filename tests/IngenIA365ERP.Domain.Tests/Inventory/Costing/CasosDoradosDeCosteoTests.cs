using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Units;

namespace IngenIA365ERP.Domain.Tests.Inventory.Costing;

/// <summary>
/// Feature 012, T268 (SC-007; research R10; quickstart §3.8): el motor de costeo coincide al peso con los cálculos hechos
/// a mano de <c>Casos/</c>. Cada movimiento pasa por <see cref="MotorDeCosteo"/> (o por <see cref="Retroactivo"/> si es
/// retroactivo) sobre el estado de su ámbito; se comparan las líneas del kardex una a una, el estado después de cada
/// movimiento y, al final, cada ámbito, la existencia por bodega y los invariantes del kardex (Σ cantidades = estado,
/// Σ costos = valor, valor 0 con cantidad 0).
/// </summary>
public class CasosDoradosDeCosteoTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoDoradoDeCosteo.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_I1()
    {
        var nombres = CasoDoradoDeCosteo.Archivos().Select(Path.GetFileName).ToList();
        foreach (var prefijo in new[] { "01-", "02-", "03-", "04-", "05-", "06-", "07-", "08-", "12-", "13-", "14-", "15-", "17-" })
            nombres.Should().Contain(n => n!.StartsWith(prefijo, StringComparison.Ordinal), $"falta el caso {prefijo}* de I1 (research R10)");
    }

    [Fact]
    public void Esta_el_caso_10_de_I5()
    {
        CasoDoradoDeCosteo.Archivos().Select(Path.GetFileName).Should()
            .Contain("10-prorrateo-con-parte-vendida.json", "falta el caso 10 «prorrateo con parte vendida» (T768, research R10)");
    }

    [Fact]
    public void Estan_los_casos_de_I5_de_costeo_avanzado()
    {
        var nombres = CasoDoradoDeCosteo.Archivos().Select(Path.GetFileName).ToList();
        foreach (var archivo in new[]
                 {
                     "09-compra-retroactiva-antes-de-tres-ventas.json", "11-peps-dos-capas.json", "16-valorizado-por-dos-metodos.json",
                     "peps-devolucion-de-cliente.json", "peps-devolucion-a-proveedor.json", "peps-anulacion-de-salida.json",
                     "peps-traslado-ambito-bodega.json", "peps-negativo-y-regularizacion.json", "peps-cambio-de-metodo.json",
                     "peps-prorrateo.json",
                 })
            nombres.Should().Contain(archivo, "los casos dorados de US16 (T818–T821, research R10)");
    }

    /// <summary>
    /// I6, T863 (SC-007, FR-044, FR-052): el caso 20 de la remisión existe y ninguno de sus movimientos es de la factura desde remisiones ni de
    /// una nota crédito: esas clases no producen kardex (lo facturado ya salió por la remisión, y la nota de esa factura no devuelve mercancía).
    /// </summary>
    [Fact]
    public void Esta_el_caso_20_de_I6_y_la_factura_desde_remisiones_no_mueve_kardex()
    {
        var archivo = CasoDoradoDeCosteo.Archivos().SingleOrDefault(f => Path.GetFileName(f) == "20-remision-y-factura-desde-remisiones.json");
        archivo.Should().NotBeNull("falta el caso 20 «remisión y factura desde remisiones» (T863)");
        var caso = CasoDoradoDeCosteo.Cargar(archivo!);
        caso.Movimientos.Should().Contain(m => m.Clase == DocumentClass.Shipment);
        caso.Movimientos.Should().NotContain(m => m.Clase == DocumentClass.SalesInvoiceFromShipments || m.Clase == DocumentClass.CreditNote);
        caso.Movimientos.Should().Contain(m => m.Anulacion && m.Valoracion == ValoracionDelMovimiento.AlCostoDeOrigen,
            "la anulación de la remisión no facturada entra al costo con que salió");
    }

    /// <summary>
    /// I6, T904 (SC-007, US15-2, US15-3): los casos 18 (ensamble de kits) y 19 (venta de combo) existen y recorren el camino compuesto:
    /// el ensamble lleva clase <c>Assembly</c> y componentes; el combo, componentes en una clase de venta.
    /// </summary>
    [Fact]
    public void Estan_los_casos_18_y_19_de_I6()
    {
        var nombres = CasoDoradoDeCosteo.Archivos().Select(Path.GetFileName).ToList();
        nombres.Should().Contain("18-ensamble-de-kits.json", "falta el caso 18 «ensamble de kits» (T904)");
        nombres.Should().Contain("19-venta-de-combo.json", "falta el caso 19 «venta de combo» (T904)");

        var ensamble = CasoDoradoDeCosteo.Cargar(Path.Combine(CasoDoradoDeCosteo.DirectorioDeCasos, "18-ensamble-de-kits.json"));
        ensamble.Movimientos.Should().Contain(m => m.Clase == DocumentClass.Assembly && m.Componentes.Count > 0);
        var combo = CasoDoradoDeCosteo.Cargar(Path.Combine(CasoDoradoDeCosteo.DirectorioDeCasos, "19-venta-de-combo.json"));
        combo.Movimientos.Should().Contain(m => m.Clase == DocumentClass.SalesInvoice && m.Componentes.Count > 0);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_al_peso_con_el_calculo_manual(string archivo)
    {
        var caso = CasoDoradoDeCosteo.Cargar(Path.Combine(CasoDoradoDeCosteo.DirectorioDeCasos, archivo));
        caso.Movimientos.Should().NotBeEmpty($"{archivo} no tiene movimientos");
        new Ejecucion(caso, archivo).Correr();
    }

    /// <summary>Recorre un caso: un estado y una historia por ámbito, y los Ids que el registro le daría a cada línea.</summary>
    private sealed class Ejecucion(CasoDoradoDeCosteo caso, string archivo)
    {
        private readonly Dictionary<string, EstadoDeCosto> _estados = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<MovimientoRegistrado>> _historias = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<LineaDeKardexPropuesta>> _kardexPorAmbito = new(StringComparer.Ordinal);
        private readonly Dictionary<string, decimal> _existenciaPorBodega = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LineaDeKardexPropuesta> _lineas = new(StringComparer.Ordinal);
        private readonly Dictionary<long, string> _nombres = [];
        private readonly Dictionary<string, long> _documentos = new(StringComparer.Ordinal);
        private readonly ParametrosDeCosteo _parametros = caso.ParametrosDelMotor();
        private readonly Dictionary<string, CostMethod> _metodos = new(StringComparer.Ordinal);
        private readonly List<ConsumoDeCapa> _consumos = [];
        private readonly List<(string Ambito, string? Producto, DateOnly Fecha, LineaDeKardexPropuesta Linea, bool Primera, CostMethod Metodo)> _registro = [];
        private long _siguienteId;

        /// <summary>El método vigente del ámbito: el del caso hasta que un movimiento lo cambia (I5, T821).</summary>
        private ParametrosDeCosteo Parametros(string ambito) =>
            _parametros with { Metodo = _metodos.GetValueOrDefault(ambito, _parametros.Metodo) };

        public void Correr()
        {
            foreach (var m in caso.Movimientos)
            {
                using var _ = new AssertionScope($"{archivo} — {caso.Nombre} — movimiento {m.Id}");
                if (m.Componentes.Count > 0)
                {
                    Compuesto(m);
                    continue;
                }

                var ambito = caso.AmbitoDe(m);
                var estado = _estados.GetValueOrDefault(ambito, EstadoDeCosto.Vacio);
                var cantidad = CantidadBase(m);
                var movimiento = Movimiento(m, cantidad, estado);

                if (m.CambioDeMetodo is { } metodo) CambioDeMetodo(m, metodo, ambito, estado);
                else if (m.CostoAdicional is { } costo) CostoAdicional(m, costo, ambito, estado);
                else if (m.Retroactivo) Retroactivo_(m, ambito, estado, movimiento);
                else Normal(m, ambito, estado, movimiento);
            }

            using var final = new AssertionScope($"{archivo} — {caso.Nombre} — final");
            foreach (var e in caso.Final)
            {
                var estado = _estados.GetValueOrDefault(e.Ambito, EstadoDeCosto.Vacio);
                estado.Quantity.Should().Be(e.Cantidad, $"cantidad final del ámbito {e.Ambito}");
                estado.Value.Should().Be(e.Valor, $"valor final del ámbito {e.Ambito}");
                estado.AverageCost.Should().Be(e.Promedio, $"promedio final del ámbito {e.Ambito}");
                if (e.UltimoCosto is { } u) estado.LastUnitCost.Should().Be(u, $"último costo final del ámbito {e.Ambito}");
            }

            foreach (var e in caso.Existencias)
                _existenciaPorBodega.GetValueOrDefault(e.Bodega).Should().Be(e.Cantidad, $"existencia de la bodega {e.Bodega}");

            foreach (var (ambito, estado) in _estados)
            {
                var lineas = _kardexPorAmbito.GetValueOrDefault(ambito, []);
                lineas.Sum(l => l.QuantityBase).Should().Be(estado.Quantity, $"Σ QuantityBase del kardex = cantidad del ámbito {ambito}");
                lineas.Sum(l => l.TotalCost).Should().Be(estado.Value, $"Σ TotalCost del kardex = valor del ámbito {ambito}");
                if (estado.Quantity == 0m) estado.Value.Should().Be(0m, $"con cantidad 0 el valor del ámbito {ambito} es 0");
                foreach (var l in lineas.Where(l => l.Kind != KardexEntryKind.CostAdjustment && l.Reason == KardexReason.Normal))
                    l.TotalCost.Should().Be(Redondeo.Monto(l.QuantityBase * l.UnitCost, _parametros.Montos),
                        $"TotalCost = round(QuantityBase × UnitCost) en la línea {_nombres.GetValueOrDefault(l.Id ?? 0)}");

                // PEPS (FR-043, data-model §3.5): Σ valor de las capas vivas = valor del ámbito; 0 ≤ restante ≤ original y
                // restante = original − Σ consumos de la capa.
                if (Parametros(ambito).Metodo != CostMethod.Fifo) continue;
                if (estado.Quantity >= 0m)
                    estado.Capas.Sum(c => c.Valor(_parametros.Montos)).Should().Be(estado.Value, $"Σ valor de las capas = valor del ámbito {ambito}");
                foreach (var capa in estado.Capas)
                {
                    capa.RemainingQuantity.Should().BeInRange(0m, capa.OriginalQuantity, $"restante de la capa {Nombre(capa.Entrada)}");
                    var consumido = _consumos.Where(c => c.Capa.Entrada.Id == capa.Entrada.Id).Sum(c => c.Quantity);
                    capa.RemainingQuantity.Should().Be(capa.OriginalQuantity - consumido, $"restante = original − Σ consumos en la capa {Nombre(capa.Entrada)}");
                }
            }

            if (caso.Valorizacion is { } valorizacion) Valorizar(valorizacion);
        }

        private void Normal(CasoDoradoDeCosteo.MovimientoJson m, string ambito, EstadoDeCosto estado, MovimientoDeCosto movimiento)
        {
            var resultado = MotorDeCosteo.Aplicar(estado, movimiento, Parametros(ambito));
            if (Rechazado(m, resultado.Rechazo)) return;

            Registrar(m, ambito, resultado.Lineas);
            _consumos.AddRange(resultado.Consumos);
            CompararConsumos(m.Esperado.Consumos, resultado.Consumos);
            CompararCapas(m.Esperado.Capas, resultado.Estado);
            Historia(ambito).Add(new MovimientoRegistrado(resultado.Principal!.Id!.Value, Documento(m), m.Fecha, movimiento,
                resultado.Valor, resultado.Principal.UnitCost, m.DejaElInventario));
            _estados[ambito] = resultado.Estado;

            CompararLineas(m.Esperado.Lineas, resultado.Lineas, "línea");
            CompararEstado(m.Esperado.Estado, resultado.Estado);
            CompararExplicacion(m.Esperado.Explicacion, resultado.Explicacion);
        }

        private void Retroactivo_(CasoDoradoDeCosteo.MovimientoJson m, string ambito, EstadoDeCosto estado, MovimientoDeCosto movimiento)
        {
            var historia = Historia(ambito);
            var pedido = new PedidoRetroactivo(EstadoDeCosto.Vacio, historia,
                [new MovimientoRetroactivo(m.Fecha, Documento(m), movimiento)], Parametros(ambito));

            // T830: el impacto que se muestra antes de confirmar es el mismo cálculo que la confirmación (FR-045).
            var impacto = MotorDeCosteo.SimularImpacto(pedido);
            var resultado = Retroactivo.Insertar(pedido);
            if (m.Esperado.Impacto is { } esperado)
            {
                impacto.EsRetroactivo.Should().Be(esperado.Retroactivo, "SimularImpacto dice si el documento es retroactivo");
                impacto.Total.Should().Be(esperado.Total, "SimularImpacto da el total de la diferencia");
            }
            impacto.Afectados.Select(a => (a.DocumentId, a.EnExistencia, a.Vendida))
                .Should().Equal(resultado.PorDocumento.Select(a => (a.DocumentId, a.EnExistencia, a.Vendida)), "el impacto es lo que escribe la confirmación");
            impacto.Resultado.Ajustes.Select(a => (a.TotalCost, a.AffectsEntry?.Id, a.OperationDate, a.Porcion))
                .Should().Equal(resultado.Ajustes.Select(a => (a.TotalCost, a.AffectsEntry?.Id, a.OperationDate, a.Porcion)));
            if (Rechazado(m, resultado.Rechazo)) return;

            var nuevo = resultado.Nuevos.Single();
            Registrar(m, ambito, nuevo.Lineas);
            foreach (var ajuste in resultado.Ajustes)
            {
                ajuste.Id = ++_siguienteId;
                Kardex(ambito).Add(ajuste);
                var i = historia.FindIndex(h => h.EntryId == ajuste.AffectsEntry!.Id);
                historia[i] = historia[i] with { ValorRegistrado = historia[i].ValorRegistrado + ajuste.TotalCost };
            }
            historia.Add(new MovimientoRegistrado(nuevo.Principal!.Id!.Value, Documento(m), m.Fecha, movimiento,
                nuevo.Valor, nuevo.Principal.UnitCost, m.DejaElInventario));
            _estados[ambito] = resultado.EstadoFinal;

            CompararLineas(m.Esperado.Lineas, nuevo.Lineas, "línea");
            CompararLineas(m.Esperado.Ajustes, resultado.Ajustes, "ajuste retroactivo");
            resultado.PorDocumento.Select(d => (Doc: NombreDeDocumento(d.DocumentId), d.EnExistencia, d.Vendida))
                .Should().BeEquivalentTo(m.Esperado.AjustesPorDocumento.Select(d => (Doc: d.Documento, d.EnExistencia, d.Vendida)),
                    "un AjusteDeCostoReconocido por documento afectado, separado en existencia y vendido");
            CompararEstado(m.Esperado.Estado, resultado.EstadoFinal);
            CompararExplicacion(m.Esperado.Explicacion, resultado.Explicacion);
        }

        /// <summary>
        /// I6 (T904): un combo vendido o devuelto (<see cref="MotorDeCosteo.MoverCombo{TClave}"/>) o un ensamble de kits
        /// (<see cref="MotorDeCosteo.Ensamblar{TClave}"/>). Cada componente mueve su propio ámbito —«componente@ámbito»—; el combo no
        /// tiene kardex propio y el kit entra al costo de lo consumido. Si un componente no alcanza, no se mueve ninguno.
        /// </summary>
        private void Compuesto(CasoDoradoDeCosteo.MovimientoJson m)
        {
            var ensamble = m.Clase == DocumentClass.Assembly;
            var movimientos = m.Componentes.Select(c =>
            {
                var ambito = caso.AmbitoDe(c.Producto, m.Bodega);
                var cantidad = ensamble ? -Math.Abs(c.Cantidad * m.Cantidad) : c.Cantidad * m.Cantidad;
                var origen = cantidad > 0m && m.Origen is { } o ? Linea($"{o}:{c.Producto}") : null;
                var movimiento = origen is null
                    ? new MovimientoDeCosto(cantidad, ValoracionDelMovimiento.AlCostoVigente) { OperationDate = m.Fecha }
                    : new MovimientoDeCosto(cantidad, ValoracionDelMovimiento.AlCostoDeOrigen, origen.UnitCost, ReferenciaDeKardex.A(origen.Id!.Value))
                    {
                        OperationDate = m.Fecha,
                    };
                return new MovimientoDeComponente<string>(c.Producto, _estados.GetValueOrDefault(ambito, EstadoDeCosto.Vacio), movimiento);
            }).ToList();

            ResultadoDeCompuesto<string> componentes;
            ResultadoDeCosteo? kit = null;
            ExplicacionDeCosto explicacion;
            var ambitoDelKit = caso.AmbitoDe(m);
            if (ensamble)
            {
                var r = MotorDeCosteo.Ensamblar(movimientos, _estados.GetValueOrDefault(ambitoDelKit, EstadoDeCosto.Vacio), m.Cantidad,
                    Parametros(ambitoDelKit), m.Fecha);
                componentes = r.Componentes;
                kit = r.Kit;
                explicacion = r.Explicacion;
                if (r.Admitido) r.CostoConsumido.Should().Be(componentes.Costo, "el ensamble consume lo que salió de sus componentes");
            }
            else
            {
                componentes = MotorDeCosteo.MoverCombo(movimientos, _parametros);
                explicacion = componentes.Explicacion;
            }

            if (m.Esperado.ComponenteRechazado is { } rechazado) componentes.ComponenteRechazado.Should().Be(rechazado);
            if (Rechazado(m, componentes.Rechazo))
            {
                componentes.Componentes.Should().BeEmpty("un combo o ensamble rechazado no mueve ningún componente");
                kit.Should().BeNull();
                return;
            }

            foreach (var c in componentes.Componentes)
            {
                var ambito = caso.AmbitoDe(c.Componente, m.Bodega);
                Registrar($"{m.Id}:{c.Componente}", ambito, c.Componente, m.Bodega, m.Fecha, c.Resultado.Lineas);
                var mov = movimientos.Single(x => x.Componente == c.Componente).Movimiento;
                Historia(ambito).Add(new MovimientoRegistrado(c.Resultado.Principal!.Id!.Value, Documento(m), m.Fecha, mov,
                    c.Resultado.Valor, c.Resultado.Principal.UnitCost, true));
                _estados[ambito] = c.Resultado.Estado;

                var esperado = m.Esperado.Componentes.SingleOrDefault(e => e.Producto == c.Componente);
                if (esperado is null) continue;
                using var _ = new AssertionScope($"componente {c.Componente}");
                CompararLineas(esperado.Lineas, c.Resultado.Lineas, "línea");
                CompararEstado(esperado.Estado, c.Resultado.Estado);
            }

            if (m.Esperado.CostoCompuesto is { } costo)
                componentes.Costo.Should().Be(costo, ensamble ? "costo consumido del ensamble" : "costo de venta del combo = Σ de sus componentes");

            if (kit is not null)
            {
                Registrar(m.Id, ambitoDelKit, m.Producto, m.Bodega, m.Fecha, kit.Lineas);
                Historia(ambitoDelKit).Add(new MovimientoRegistrado(kit.Principal!.Id!.Value, Documento(m), m.Fecha,
                    new MovimientoDeCosto(m.Cantidad, ValoracionDelMovimiento.AlCostoIndicado, kit.Principal.UnitCost) { OperationDate = m.Fecha },
                    kit.Valor, kit.Principal.UnitCost, false));
                _estados[ambitoDelKit] = kit.Estado;
                CompararLineas(m.Esperado.Lineas, kit.Lineas, "línea del kit");
                CompararEstado(m.Esperado.Estado, kit.Estado);
            }
            else
            {
                m.Esperado.Lineas.Should().BeNull("un combo no tiene kardex propio: sólo sus componentes");
            }

            CompararExplicacion(m.Esperado.Explicacion, explicacion);
        }

        private bool Rechazado(CasoDoradoDeCosteo.MovimientoJson m, RechazoDeCosteo? rechazo)
        {
            if (m.Esperado.Rechazo is { } codigo)
            {
                rechazo.Should().NotBeNull("el movimiento debía rechazarse");
                rechazo?.Codigo.Should().Be(codigo);
                return true;
            }
            rechazo.Should().BeNull($"el movimiento no debía rechazarse: {rechazo?.Mensaje}");
            return rechazo is not null;
        }

        /// <summary>
        /// I5 (T768): el costo adicional se reparte sobre la entrada con <see cref="Prorrateo"/> —una sola línea, con la existencia
        /// del ámbito para la regla D5— y entra al kardex por <see cref="MotorDeCosteo.CostoAdicional"/>. No mueve cantidad ni
        /// entra a la historia del ámbito (sus líneas afectan la entrada, no son un movimiento propio).
        /// </summary>
        private void CostoAdicional(CasoDoradoDeCosteo.MovimientoJson m, CasoDoradoDeCosteo.CostoAdicionalJson costo, string ambito, EstadoDeCosto estado)
        {
            var entrada = Linea(costo.Entrada);
            // T843: con PEPS la existencia de la regla D5 es lo que queda de la capa de esa entrada, no la del ámbito.
            var metodo = Parametros(ambito).Metodo;
            var existencia = metodo == CostMethod.Fifo
                ? Peps.CapaDe(estado, ReferenciaDeKardex.A(entrada.Id!.Value))?.RemainingQuantity ?? 0m
                : estado.Quantity;
            var reparto = Prorrateo.Repartir(new PedidoDeProrrateo(costo.Monto, costo.Metodo,
                [new LineaAProrratear(1, 1, entrada.QuantityBase, entrada.TotalCost, null, null, costo.Metodo == LandedCostAllocationMethod.Manual ? costo.Monto : null, existencia)],
                _parametros.Montos, ResiduoDeRedondeo.MayorValor));
            if (Rechazado(m, reparto.Rechazo is { } r ? new RechazoDeCosteo(r.Codigo, r.Mensaje, 0m, costo.Monto) : null)) return;

            var resultado = MotorDeCosteo.CostoAdicional(estado, ReferenciaDeKardex.A(entrada.Id!.Value), reparto.Lineas.Single(), metodo, _parametros.Montos);
            Registrar(m, ambito, resultado.Lineas);
            _estados[ambito] = resultado.Estado;

            CompararLineas(m.Esperado.Lineas, resultado.Lineas, "línea");
            CompararCapas(m.Esperado.Capas, resultado.Estado);
            if (m.Esperado.AjustesPorDocumento.Count > 0)
                new[] { (Doc: Nombre(ReferenciaDeKardex.A(entrada.Id!.Value)), EnExistencia: Prorrateo.EnExistencia(resultado), Vendida: Prorrateo.Vendida(resultado)) }
                    .Should().BeEquivalentTo(m.Esperado.AjustesPorDocumento.Select(d => (Doc: d.Documento, d.EnExistencia, d.Vendida)),
                        "un AjusteDeCostoReconocido por recepción afectada, separado en existencia y vendido");
            CompararEstado(m.Esperado.Estado, resultado.Estado);
            CompararExplicacion(m.Esperado.Explicacion, resultado.Explicacion.Agregar(reparto.Explicacion));
        }

        /// <summary>
        /// I5 (T821): el cambio de método del ámbito por <see cref="MotorDeCosteo.CambiarMetodo"/>: una línea <c>MethodChange</c> y, a
        /// PEPS, una sola capa con la existencia al promedio. No entra a la historia (no es un movimiento de cantidad).
        /// </summary>
        private void CambioDeMetodo(CasoDoradoDeCosteo.MovimientoJson m, CostMethod metodo, string ambito, EstadoDeCosto estado)
        {
            var resultado = MotorDeCosteo.CambiarMetodo(estado, metodo, _parametros.Montos, m.Fecha);
            Registrar(m, ambito, resultado.Lineas);
            _estados[ambito] = resultado.Estado;
            _metodos[ambito] = metodo;

            CompararLineas(m.Esperado.Lineas, resultado.Lineas, "línea");
            CompararCapas(m.Esperado.Capas, resultado.Estado);
            CompararEstado(m.Esperado.Estado, resultado.Estado);
            CompararExplicacion(m.Esperado.Explicacion, resultado.Explicacion);
        }

        private void CompararConsumos(List<CasoDoradoDeCosteo.ConsumoJson>? esperados, IReadOnlyList<ConsumoDeCapa> reales)
        {
            if (esperados is null) return;
            reales.Select(c => (Salida: Nombre(c.Salida), Capa: Nombre(c.Capa.Entrada), c.Quantity, c.UnitCost))
                .Should().Equal(esperados.Select(e => (e.Salida, e.Capa, Quantity: e.Cantidad, UnitCost: e.CostoUnitario)),
                    "los consumos de capa del movimiento, en orden (data-model §3.5)");
        }

        private void CompararCapas(List<CasoDoradoDeCosteo.CapaJson>? esperadas, EstadoDeCosto estado)
        {
            if (esperadas is null) return;
            estado.Capas.Select(c => (Capa: Nombre(c.Entrada), c.OriginalQuantity, c.RemainingQuantity, c.UnitCost))
                .Should().Equal(esperadas.Select(e => (e.Capa, OriginalQuantity: e.Original, RemainingQuantity: e.Restante, UnitCost: e.CostoUnitario)),
                    "las capas vivas del ámbito en orden PEPS (OperationDate, EntryKardexEntryId)");
        }

        /// <summary>
        /// I5 (T820): del kardex que dejó el caso, una historia por producto y ámbito —entradas, salidas, el ajuste completo sobre
        /// una entrada (la primera línea <c>PriceDifference</c>/<c>LandedCost</c> del movimiento) y los demás ajustes— para
        /// <see cref="ValorizacionPorDosMetodos"/>; se compara por grupo y fecha.
        /// </summary>
        private void Valorizar(CasoDoradoDeCosteo.ValorizacionJson valorizacion)
        {
            using var _ = new AssertionScope($"{archivo} — {caso.Nombre} — valorizado por los dos métodos");
            var productos = _registro.Select(r => r.Producto ?? string.Empty).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
            var grupos = valorizacion.Grupos.Values.Distinct().OrderBy(g => g, StringComparer.Ordinal).ToList();

            var historias = _registro.GroupBy(r => (r.Ambito, Producto: r.Producto ?? string.Empty)).Select(g =>
            {
                var producto = g.Key.Producto;
                var movimientos = g.Select(r => new MovimientoAValorizar(
                    r.Linea.Id!.Value, r.Fecha, Clase(r.Linea, r.Primera), r.Linea.QuantityBase, r.Linea.UnitCost, r.Linea.TotalCost,
                    r.Metodo, r.Linea.AffectsEntry?.Id)).ToList();
                return new HistoriaParaValorizar(productos.IndexOf(producto) + 1, 0, grupos.IndexOf(valorizacion.Grupos[producto]) + 1,
                    valorizacion.Cortes.TryGetValue(producto, out var corte) ? corte : null, movimientos);
            }).ToList();

            var resultado = ValorizacionPorDosMetodos.Calcular(historias, valorizacion.Fechas, _parametros.Montos);

            resultado.Grupos.Select(g => (Grupo: grupos[g.AccountingGroupId - 1], g.Fecha, g.PromedioPonderado, g.Peps, g.Diferencia,
                    SinCalcular: string.Join(",", g.SinCalcular.Select(p => productos[p.ProductId - 1]))))
                .Should().BeEquivalentTo(valorizacion.Esperado.Select(e => (e.Grupo, e.Fecha, e.PromedioPonderado, e.Peps, e.Diferencia,
                    SinCalcular: string.Join(",", e.SinCalcular))), "el valorizado por grupo contable y fecha (FR-043)");
            foreach (var p in resultado.Grupos.SelectMany(g => g.SinCalcular))
            {
                p.Nota.Should().NotBeNullOrWhiteSpace("todo producto sin calcular dice por qué");
                if (valorizacion.Nota is { } nota) p.Nota.Should().Contain(nota);
            }
        }

        private static ClaseAValorizar Clase(LineaDeKardexPropuesta l, bool primera) => l.Kind switch
        {
            KardexEntryKind.Entry => ClaseAValorizar.Entrada,
            KardexEntryKind.Exit => ClaseAValorizar.Salida,
            _ when primera && l.Reason is KardexReason.PriceDifference or KardexReason.LandedCost => ClaseAValorizar.AjusteSobreEntrada,
            _ => ClaseAValorizar.OtroAjuste,
        };

        private decimal CantidadBase(CasoDoradoDeCosteo.MovimientoJson m)
        {
            if (m.Unidad is not { } u) return m.Cantidad;
            var conversion = ConversionDeUnidades.Convertir(new PedidoDeConversion(1, u.Codigo, Math.Abs(m.Cantidad), u.Factor, u.Decimales, u.Base, u.DecimalesBase));
            conversion.Admitida.Should().BeTrue($"la conversión de {m.Id} debía admitirse");
            var cantidad = Math.Sign(m.Cantidad) * conversion.QuantityBase;
            if (m.Esperado.CantidadBase is { } b) cantidad.Should().Be(b, "cantidad en unidad base");
            if (m.Esperado.CantidadDeRedondeo is { } r) conversion.RoundingQuantity.Should().Be(r, "cantidad de redondeo visible de la línea");
            return cantidad;
        }

        private MovimientoDeCosto Movimiento(CasoDoradoDeCosteo.MovimientoJson m, decimal cantidad, EstadoDeCosto estado)
        {
            var origen = m.Origen is { } o ? ReferenciaDeKardex.A(Linea(o).Id!.Value) : null;

            if (m.Compra is { } c)
            {
                var costo = CostoDeEntrada.DeCompra(new PedidoDeCostoDeCompra(cantidad, c.ValorBruto, c.Descuentos,
                    c.Impuestos.Select(i => new ImpuestoDeCompra(i.Clase, i.Valor, i.Codigo)).ToList(),
                    c.CooperativaResponsableIva, c.TipoIvaNoDescontable, c.TratamientoDeVenta), _parametros.Montos);
                if (m.Esperado.CostoDeEntrada is { } e)
                {
                    costo.CostoUnitario.Should().Be(e.CostoUnitario, $"costo de entrada de la compra. Explicación: {costo.Explicacion.Texto()}");
                    costo.ImpuestosAlCosto.Should().Be(e.ImpuestosAlCosto, "impuestos que van al costo");
                }
                return costo.Movimiento(cantidad) with { OperationDate = m.Fecha };
            }

            var valoracion = m.Valoracion ?? (cantidad > 0m && m.CostoUnitario is not null
                ? ValoracionDelMovimiento.AlCostoIndicado
                : ValoracionDelMovimiento.AlCostoVigente);
            var costoUnitario = m.CostoUnitario
                ?? (valoracion is ValoracionDelMovimiento.AlCostoDeOrigen or ValoracionDelMovimiento.DevolucionDeEntrada && m.Origen is { } nombre
                    ? Linea(nombre).UnitCost
                    : null);
            return new MovimientoDeCosto(cantidad, valoracion, costoUnitario, origen, m.Anulacion)
            {
                OperationDate = m.Fecha,
                // I5 (PEPS): la anulación de una salida devuelve los consumos de esa salida.
                ConsumosDelOrigen = m.Anulacion && m.Origen is { } anulada
                    ? _consumos.Where(c => Nombre(c.Salida) == anulada && c.Quantity > 0m).ToList()
                    : [],
            };
        }

        private void Registrar(CasoDoradoDeCosteo.MovimientoJson m, string ambito, IReadOnlyList<LineaDeKardexPropuesta> lineas) =>
            Registrar(m.Id, ambito, m.Producto, m.Bodega, m.Fecha, lineas);

        private void Registrar(string id, string ambito, string? producto, string bodega, DateOnly fecha, IReadOnlyList<LineaDeKardexPropuesta> lineas)
        {
            for (var i = 0; i < lineas.Count; i++)
            {
                var linea = lineas[i];
                linea.Id = ++_siguienteId;
                var nombre = i == 0 ? id : $"{id}#{i + 1}";
                _lineas[nombre] = linea;
                _nombres[linea.Id.Value] = nombre;
                Kardex(ambito).Add(linea);
                _registro.Add((ambito, producto, linea.OperationDate ?? fecha, linea, i == 0, Parametros(ambito).Metodo));
                _existenciaPorBodega[bodega] = _existenciaPorBodega.GetValueOrDefault(bodega) + linea.QuantityBase;
            }
        }

        private void CompararLineas(List<CasoDoradoDeCosteo.LineaJson>? esperadas, IReadOnlyList<LineaDeKardexPropuesta> reales, string que)
        {
            if (esperadas is null) return;
            reales.Should().HaveCount(esperadas.Count, $"cantidad de {que}s: {string.Join(" | ", reales.Select(Describir))}");
            for (var i = 0; i < Math.Min(esperadas.Count, reales.Count); i++)
            {
                var e = esperadas[i];
                var r = reales[i];
                var donde = $"{que} {i + 1} ({Describir(r)})";
                r.Kind.Should().Be(e.Kind, donde);
                r.Reason.Should().Be(e.Reason, donde);
                r.QuantityBase.Should().Be(e.Cantidad, $"cantidad de la {donde}");
                r.UnitCost.Should().Be(e.CostoUnitario, $"costo unitario de la {donde}");
                r.TotalCost.Should().Be(e.CostoTotal, $"costo total de la {donde}");
                if (e.Afecta is { } afecta) Nombre(r.AffectsEntry).Should().Be(afecta, $"línea afectada por la {donde}");
                if (e.Revierte is { } revierte) Nombre(r.ReversesEntry).Should().Be(revierte, $"línea revertida por la {donde}");
                if (e.Fecha is { } fecha) r.OperationDate.Should().Be(fecha, $"fecha de la {donde}");
                if (e.Porcion is { } porcion) r.Porcion.Should().Be(porcion, $"porción de la {donde}");
            }
        }

        private static void CompararEstado(CasoDoradoDeCosteo.EstadoJson? esperado, EstadoDeCosto real)
        {
            if (esperado is null) return;
            real.Quantity.Should().Be(esperado.Cantidad, "cantidad del ámbito");
            real.Value.Should().Be(esperado.Valor, "valor del ámbito");
            real.AverageCost.Should().Be(esperado.Promedio, "promedio del ámbito");
            if (esperado.UltimoCosto is { } u) real.LastUnitCost.Should().Be(u, "último costo del ámbito");
        }

        private static void CompararExplicacion(List<string> fragmentos, ExplicacionDeCosto explicacion)
        {
            explicacion.Pasos.Should().NotBeEmpty("todo movimiento lleva explicación (T19)");
            explicacion.Resumen.Should().NotBeNullOrWhiteSpace();
            var texto = explicacion.Texto();
            foreach (var f in fragmentos) texto.Should().Contain(f, "la explicación debe decirlo");
        }

        private string Nombre(ReferenciaDeKardex? referencia) =>
            referencia?.Id is { } id ? _nombres.GetValueOrDefault(id, $"#{id}") : "-";

        private string Describir(LineaDeKardexPropuesta l) =>
            $"{l.Kind}/{l.Reason} q={l.QuantityBase} u={l.UnitCost} t={l.TotalCost} afecta={Nombre(l.AffectsEntry)}";

        private LineaDeKardexPropuesta Linea(string nombre) =>
            _lineas.TryGetValue(nombre, out var linea) ? linea : throw new InvalidOperationException($"{archivo}: no existe la línea «{nombre}».");

        private long Documento(CasoDoradoDeCosteo.MovimientoJson m)
        {
            if (!_documentos.TryGetValue(m.DocumentoOId, out var id)) _documentos[m.DocumentoOId] = id = _documentos.Count + 1;
            return id;
        }

        private string NombreDeDocumento(long id) => _documentos.Single(d => d.Value == id).Key;

        private List<MovimientoRegistrado> Historia(string ambito) =>
            _historias.TryGetValue(ambito, out var h) ? h : _historias[ambito] = [];

        private List<LineaDeKardexPropuesta> Kardex(string ambito) =>
            _kardexPorAmbito.TryGetValue(ambito, out var k) ? k : _kardexPorAmbito[ambito] = [];
    }
}
