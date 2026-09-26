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
        private long _siguienteId;

        public void Correr()
        {
            foreach (var m in caso.Movimientos)
            {
                using var _ = new AssertionScope($"{archivo} — {caso.Nombre} — movimiento {m.Id}");
                var ambito = caso.AmbitoDe(m);
                var estado = _estados.GetValueOrDefault(ambito, EstadoDeCosto.Vacio);
                var cantidad = CantidadBase(m);
                var movimiento = Movimiento(m, cantidad, estado);

                if (m.Retroactivo) Retroactivo_(m, ambito, estado, movimiento);
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
            }
        }

        private void Normal(CasoDoradoDeCosteo.MovimientoJson m, string ambito, EstadoDeCosto estado, MovimientoDeCosto movimiento)
        {
            var resultado = MotorDeCosteo.Aplicar(estado, movimiento, _parametros);
            if (Rechazado(m, resultado.Rechazo)) return;

            Registrar(m, ambito, resultado.Lineas);
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
            var resultado = Retroactivo.Insertar(new PedidoRetroactivo(EstadoDeCosto.Vacio, historia,
                [new MovimientoRetroactivo(m.Fecha, Documento(m), movimiento)], _parametros));
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
                return costo.Movimiento(cantidad);
            }

            var valoracion = m.Valoracion ?? (cantidad > 0m && m.CostoUnitario is not null
                ? ValoracionDelMovimiento.AlCostoIndicado
                : ValoracionDelMovimiento.AlCostoVigente);
            var costoUnitario = m.CostoUnitario
                ?? (valoracion is ValoracionDelMovimiento.AlCostoDeOrigen or ValoracionDelMovimiento.DevolucionDeEntrada && m.Origen is { } nombre
                    ? Linea(nombre).UnitCost
                    : null);
            return new MovimientoDeCosto(cantidad, valoracion, costoUnitario, origen, m.Anulacion);
        }

        private void Registrar(CasoDoradoDeCosteo.MovimientoJson m, string ambito, IReadOnlyList<LineaDeKardexPropuesta> lineas)
        {
            for (var i = 0; i < lineas.Count; i++)
            {
                var linea = lineas[i];
                linea.Id = ++_siguienteId;
                var nombre = i == 0 ? m.Id : $"{m.Id}#{i + 1}";
                _lineas[nombre] = linea;
                _nombres[linea.Id.Value] = nombre;
                Kardex(ambito).Add(linea);
                _existenciaPorBodega[m.Bodega] = _existenciaPorBodega.GetValueOrDefault(m.Bodega) + linea.QuantityBase;
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
