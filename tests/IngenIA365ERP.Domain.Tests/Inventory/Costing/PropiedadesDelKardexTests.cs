using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Domain.Tests.Inventory.Costing;

/// <summary>
/// Feature 012, T272 (nombre fijo, decisiones-transversales §2.18; T18; research R10): secuencias aleatorias pero
/// reproducibles —la semilla va en cada mensaje— de compras, ventas, devoluciones de cliente, anulaciones de compras y
/// traslados por tránsito, en los dos ámbitos. Después de cada movimiento, en cada ámbito: Σ <c>QuantityBase</c> del
/// kardex = <c>CostState.Quantity</c>; Σ <c>TotalCost</c> = <c>CostState.Value</c>; con cantidad 0 el valor es 0; y con el
/// negativo prohibido ningún saldo queda bajo cero. Al resolver todos los traslados en ámbito bodega, el tránsito queda
/// en 0 por 0 exacto.
/// </summary>
public class PropiedadesDelKardexTests
{
    private const int Secuencias = 150;
    private const int MovimientosPorSecuencia = 80;

    public static IEnumerable<object[]> Semillas() =>
        from ambito in new[] { CostScope.Cooperative, CostScope.Warehouse }
        from negativo in new[] { false, true }
        from montos in new[] { RedondeoDeMontos.Centavo, RedondeoDeMontos.Peso }
        select new object[] { 20260925, ambito, negativo, montos };

    [Theory]
    [MemberData(nameof(Semillas))]
    public void El_kardex_y_el_estado_siempre_cuadran(int semilla, CostScope ambito, bool negativoPermitido, RedondeoDeMontos montos)
    {
        for (var s = 0; s < Secuencias; s++)
            new Secuencia(semilla + s, ambito, new ParametrosDeCosteo(CostMethod.WeightedAverage, montos, negativoPermitido)).Correr(MovimientosPorSecuencia);
    }

    private sealed class Secuencia(int semilla, CostScope ambito, ParametrosDeCosteo parametros)
    {
        private static readonly string[] Bodegas = ["A", "B"];
        private const string Transito = "TRANSITO";

        private readonly Random _azar = new(semilla);
        private readonly Dictionary<string, EstadoDeCosto> _estados = [];
        private readonly Dictionary<string, List<LineaDeKardexPropuesta>> _kardex = [];
        private readonly Dictionary<string, decimal> _fisico = [];
        private readonly List<(string Bodega, LineaDeKardexPropuesta Linea, decimal Pendiente)> _compras = [];
        private readonly List<(string Bodega, LineaDeKardexPropuesta Linea, decimal Pendiente)> _ventas = [];
        private readonly List<(string Destino, LineaDeKardexPropuesta Despacho, decimal Cantidad)> _enTransito = [];
        private long _id;
        private int _paso;

        public void Correr(int movimientos)
        {
            for (_paso = 0; _paso < movimientos; _paso++)
            {
                switch (_azar.Next(6))
                {
                    case 0: case 1: Comprar(); break;
                    case 2: Vender(); break;
                    case 3: DevolverVenta(); break;
                    case 4: AnularCompra(); break;
                    default: if (_azar.Next(2) == 0) Despachar(); else Recibir(); break;
                }
            }

            while (_enTransito.Count > 0) Recibir();
            if (ambito == CostScope.Warehouse)
            {
                var transito = Estado(Transito);
                Afirmar(transito.Quantity == 0m && transito.Value == 0m, $"el tránsito queda en 0 por 0 al resolverse, quedó {transito.Quantity} por {transito.Value}");
            }
        }

        private void Comprar()
        {
            var bodega = Bodegas[_azar.Next(Bodegas.Length)];
            var cantidad = Cantidad();
            var costo = Math.Round((decimal)_azar.Next(100, 500_000) / 100m, 2);
            var r = Aplicar(bodega, new MovimientoDeCosto(cantidad, ValoracionDelMovimiento.AlCostoIndicado, costo));
            if (r is not null) _compras.Add((bodega, r.Principal!, cantidad));
        }

        private void Vender()
        {
            var bodega = Bodegas[_azar.Next(Bodegas.Length)];
            var cantidad = parametros.NegativoPermitido ? Cantidad() : Math.Min(Cantidad(), _fisico.GetValueOrDefault(bodega));
            if (cantidad <= 0m) return;
            var r = Aplicar(bodega, new MovimientoDeCosto(-cantidad, ValoracionDelMovimiento.AlCostoVigente));
            if (r is not null) _ventas.Add((bodega, r.Principal!, cantidad));
        }

        private void DevolverVenta()
        {
            if (_ventas.Count == 0) return;
            var i = _azar.Next(_ventas.Count);
            var (bodega, linea, pendiente) = _ventas[i];
            if (pendiente <= 0m) return;
            var cantidad = Math.Min(pendiente, Cantidad());
            if (Aplicar(bodega, new MovimientoDeCosto(cantidad, ValoracionDelMovimiento.AlCostoDeOrigen, linea.UnitCost, ReferenciaDeKardex.A(linea))) is not null)
                _ventas[i] = (bodega, linea, pendiente - cantidad);
        }

        private void AnularCompra()
        {
            if (_compras.Count == 0) return;
            var i = _azar.Next(_compras.Count);
            var (bodega, linea, pendiente) = _compras[i];
            if (pendiente <= 0m || (!parametros.NegativoPermitido && _fisico.GetValueOrDefault(bodega) < pendiente)) return;
            if (Aplicar(bodega, new MovimientoDeCosto(-pendiente, ValoracionDelMovimiento.DevolucionDeEntrada, linea.UnitCost, ReferenciaDeKardex.A(linea), EsAnulacion: true)) is not null)
                _compras[i] = (bodega, linea, 0m);
        }

        private void Despachar()
        {
            var origen = Bodegas[_azar.Next(Bodegas.Length)];
            var destino = Bodegas.First(b => b != origen);
            var cantidad = Math.Min(Cantidad(), _fisico.GetValueOrDefault(origen));
            if (cantidad <= 0m) return;
            var salida = Aplicar(origen, new MovimientoDeCosto(-cantidad, ValoracionDelMovimiento.AlCostoVigente));
            if (salida is null) return;
            var despacho = salida.Principal!;
            Aplicar(Transito, new MovimientoDeCosto(cantidad, ValoracionDelMovimiento.AlCostoDeOrigen, despacho.UnitCost, ReferenciaDeKardex.A(despacho)));
            _enTransito.Add((destino, despacho, cantidad));
        }

        private void Recibir()
        {
            if (_enTransito.Count == 0) return;
            var i = _azar.Next(_enTransito.Count);
            var (destino, despacho, cantidad) = _enTransito[i];
            _enTransito.RemoveAt(i);
            var origen = ReferenciaDeKardex.A(despacho);
            Aplicar(Transito, new MovimientoDeCosto(-cantidad, ValoracionDelMovimiento.AlCostoDeOrigen, despacho.UnitCost, origen), obligatorio: true);
            Aplicar(destino, new MovimientoDeCosto(cantidad, ValoracionDelMovimiento.AlCostoDeOrigen, despacho.UnitCost, origen), obligatorio: true);
        }

        private ResultadoDeCosteo? Aplicar(string bodega, MovimientoDeCosto movimiento, bool obligatorio = false)
        {
            var clave = ambito == CostScope.Cooperative ? "COOPERATIVA" : bodega;
            var resultado = MotorDeCosteo.Aplicar(Estado(clave), movimiento, parametros);
            if (!resultado.Admitido)
            {
                Afirmar(!obligatorio, $"el movimiento obligatorio se rechazó: {resultado.Rechazo!.Mensaje}");
                Afirmar(!parametros.NegativoPermitido, "con el negativo permitido no se rechaza nada");
                return null;
            }

            foreach (var l in resultado.Lineas) l.Id = ++_id;
            Kardex(clave).AddRange(resultado.Lineas);
            _estados[clave] = resultado.Estado;
            _fisico[bodega] = _fisico.GetValueOrDefault(bodega) + movimiento.QuantityBase;

            var lineas = Kardex(clave);
            var estado = resultado.Estado;
            Afirmar(lineas.Sum(l => l.QuantityBase) == estado.Quantity, $"Σ QuantityBase ({lineas.Sum(l => l.QuantityBase)}) = Quantity ({estado.Quantity}) en {clave}");
            Afirmar(lineas.Sum(l => l.TotalCost) == estado.Value, $"Σ TotalCost ({lineas.Sum(l => l.TotalCost)}) = Value ({estado.Value}) en {clave}");
            Afirmar(estado.Quantity != 0m || estado.Value == 0m, $"con cantidad 0 el valor es 0 en {clave}, quedó {estado.Value}");
            Afirmar(parametros.NegativoPermitido || estado.Quantity >= 0m, $"con el negativo prohibido {clave} no queda en {estado.Quantity}");
            Afirmar(resultado.Lineas.All(l => l.UnitCost >= 0m), "todo costo unitario es ≥ 0");
            Afirmar(resultado.Lineas.All(l => (l.Kind == KardexEntryKind.CostAdjustment) == (l.QuantityBase == 0m)), "QuantityBase = 0 ⇔ CostAdjustment");
            return resultado;
        }

        private decimal Cantidad() =>
            _azar.Next(4) == 0 ? Math.Round((decimal)_azar.Next(1, 500_000) / 10_000m, 4) : _azar.Next(1, 60);

        private EstadoDeCosto Estado(string clave) => _estados.GetValueOrDefault(clave, EstadoDeCosto.Vacio);

        private List<LineaDeKardexPropuesta> Kardex(string clave) =>
            _kardex.TryGetValue(clave, out var k) ? k : _kardex[clave] = [];

        private void Afirmar(bool condicion, string mensaje)
        {
            if (!condicion)
                Assert.Fail($"Semilla {semilla}, ámbito {ambito}, negativo {parametros.NegativoPermitido}, redondeo {parametros.Montos}, paso {_paso}: {mensaje}");
        }
    }
}
