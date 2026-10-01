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

    // ------------------------------------------------------------------------------------------------ I5 --

    public static IEnumerable<object[]> SemillasPeps() =>
        from ambito in new[] { CostScope.Cooperative, CostScope.Warehouse }
        from negativo in new[] { false, true }
        from montos in new[] { RedondeoDeMontos.Centavo, RedondeoDeMontos.Peso }
        select new object[] { 20261001, ambito, negativo, montos };

    /// <summary>
    /// T822 (FR-002, FR-043, SC-006; data-model §3.5): secuencias aleatorias en PEPS —compras, ventas, devoluciones de cliente y
    /// a proveedor, anulaciones de ventas y traslados por tránsito—. Además de lo de arriba, en cada ámbito y después de cada
    /// movimiento: <c>0 ≤ RemainingQuantity = OriginalQuantity − Σ consumos</c> de cada capa; Σ consumos de una salida =
    /// |<c>QuantityBase</c>| (menos lo que sigue pendiente en negativo); Σ consumos de una anulación = −<c>QuantityBase</c>; y, con
    /// existencia no negativa, Σ valor de las capas vivas = <c>CostState.Value</c> y Σ restante = <c>CostState.Quantity</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(SemillasPeps))]
    public void En_PEPS_las_capas_y_los_consumos_siempre_cuadran(int semilla, CostScope ambito, bool negativoPermitido, RedondeoDeMontos montos)
    {
        for (var s = 0; s < Secuencias; s++)
            new SecuenciaPeps(semilla + s, ambito, new ParametrosDeCosteo(CostMethod.Fifo, montos, negativoPermitido)).Correr(MovimientosPorSecuencia);
    }

    /// <summary>
    /// T822 (FR-002, FR-045): un retroactivo general sobre una historia aleatoria en promedio ponderado nunca modifica lo
    /// registrado —la historia que recibe queda igual y ningún ajuste cae sobre una entrada al costo indicado—; sólo agrega
    /// líneas <c>CostAdjustment</c> <c>Retroactive</c> con <c>AffectsEntryId</c>, fechadas en el movimiento que afectan, y el
    /// estado final es la suma de todo lo registrado más lo nuevo.
    /// </summary>
    [Theory]
    [MemberData(nameof(Semillas))]
    public void Un_retroactivo_general_nunca_modifica_una_entrada_existente(int semilla, CostScope ambito, bool negativoPermitido, RedondeoDeMontos montos)
    {
        _ = ambito;
        var parametros = new ParametrosDeCosteo(CostMethod.WeightedAverage, montos, negativoPermitido);
        for (var s = 0; s < Secuencias; s++)
        {
            var azar = new Random(semilla + s);
            var estado = EstadoDeCosto.Vacio;
            var historia = new List<MovimientoRegistrado>();
            var id = 0L;
            var inicio = new DateOnly(2026, 1, 1);

            for (var i = 0; i < 40; i++)
            {
                var fecha = inicio.AddDays(i);
                MovimientoDeCosto mov;
                if (azar.Next(3) == 0 || estado.Quantity <= 0m && !negativoPermitido)
                    mov = new MovimientoDeCosto(azar.Next(1, 30), ValoracionDelMovimiento.AlCostoIndicado, Math.Round((decimal)azar.Next(100, 500_000) / 100m, 2));
                else
                    mov = new MovimientoDeCosto(-(negativoPermitido ? azar.Next(1, 30) : Math.Min(azar.Next(1, 30), estado.Quantity)), ValoracionDelMovimiento.AlCostoVigente);
                if (mov.QuantityBase == 0m) continue;

                var r = MotorDeCosteo.Aplicar(estado, mov, parametros);
                if (!r.Admitido) continue;
                foreach (var l in r.Lineas) l.Id = ++id;
                historia.Add(new MovimientoRegistrado(r.Principal!.Id!.Value, i + 1, fecha, mov, r.Valor, r.Principal.UnitCost, true));
                estado = r.Estado;
            }

            var copia = historia.ToList();
            var fechaRetro = inicio.AddDays(azar.Next(0, 40));
            var nuevo = new MovimientoRetroactivo(fechaRetro, 999, new MovimientoDeCosto(azar.Next(1, 30), ValoracionDelMovimiento.AlCostoIndicado, Math.Round((decimal)azar.Next(100, 500_000) / 100m, 2)));
            var resultado = Retroactivo.Insertar(new PedidoRetroactivo(EstadoDeCosto.Vacio, historia, [nuevo], parametros));

            var donde = $"Semilla {semilla + s}, negativo {negativoPermitido}, redondeo {montos}";
            Assert.True(historia.SequenceEqual(copia), $"{donde}: el retroactivo cambió la historia que recibió");
            if (!resultado.Admitido) continue;

            var porId = historia.ToDictionary(h => h.EntryId);
            foreach (var a in resultado.Ajustes)
            {
                Assert.True(a is { Kind: KardexEntryKind.CostAdjustment, Reason: KardexReason.Retroactive, QuantityBase: 0m }, $"{donde}: un ajuste retroactivo es CostAdjustment/Retroactive sin cantidad");
                Assert.True(a.AffectsEntry?.Id is { } afectada && porId.ContainsKey(afectada), $"{donde}: el ajuste nombra un movimiento registrado");
                var afectado = porId[a.AffectsEntry!.Id!.Value];
                Assert.True(a.OperationDate == afectado.OperationDate && afectado.OperationDate > fechaRetro, $"{donde}: el ajuste se fecha en un movimiento posterior");
                // Una compra vale lo que costó: el retroactivo no la toca. Sólo con el negativo permitido puede corregir la
                // regularización que esa compra hizo de un negativo que, con la compra anterior insertada, ya no existía.
                var plana = afectado.Movimiento is { EsEntrada: true, Valoracion: ValoracionDelMovimiento.AlCostoIndicado }
                    && afectado.ValorRegistrado == Redondeo.Monto(afectado.Movimiento.QuantityBase * afectado.CostoUnitarioRegistrado, montos);
                Assert.False(plana, $"{donde}: ningún ajuste cae sobre una entrada al costo indicado que no regularizó un negativo");
            }

            var nuevoResultado = resultado.Nuevos.Single();
            Assert.Equal(historia.Sum(h => h.ValorRegistrado) + nuevoResultado.Valor + resultado.Ajustes.Sum(a => a.TotalCost), resultado.EstadoFinal.Value);
            Assert.Equal(historia.Sum(h => h.Movimiento.QuantityBase) + nuevo.Movimiento.QuantityBase, resultado.EstadoFinal.Quantity);
        }
    }

    private sealed class SecuenciaPeps(int semilla, CostScope ambito, ParametrosDeCosteo parametros)
    {
        private static readonly string[] Bodegas = ["A", "B"];
        private const string Transito = "TRANSITO";

        private readonly Random _azar = new(semilla);
        private readonly Dictionary<string, EstadoDeCosto> _estados = [];
        private readonly Dictionary<string, List<LineaDeKardexPropuesta>> _kardex = [];
        private readonly Dictionary<string, decimal> _fisico = [];
        private readonly List<ConsumoDeCapa> _consumos = [];
        private readonly List<(string Bodega, LineaDeKardexPropuesta Linea, decimal Pendiente)> _compras = [];
        private readonly List<(string Bodega, LineaDeKardexPropuesta Linea, decimal Pendiente, bool Tocada)> _ventas = [];
        private readonly List<(string Destino, LineaDeKardexPropuesta Despacho, decimal Cantidad)> _enTransito = [];
        private DateOnly _fecha = new(2026, 1, 1);
        private long _id;
        private int _paso;

        public void Correr(int movimientos)
        {
            for (_paso = 0; _paso < movimientos; _paso++)
            {
                _fecha = _fecha.AddDays(1);
                switch (_azar.Next(8))
                {
                    case 0: case 1: Comprar(); break;
                    case 2: case 3: Vender(); break;
                    case 4: DevolverVenta(); break;
                    case 5: DevolverAProveedor(); break;
                    case 6: AnularVenta(); break;
                    default: if (_azar.Next(2) == 0) Despachar(); else Recibir(); break;
                }
            }

            while (_enTransito.Count > 0) Recibir();
            if (ambito == CostScope.Warehouse)
            {
                var transito = Estado(Transito);
                Afirmar(transito.Quantity == 0m && transito.Value == 0m, $"el tránsito queda en 0 por 0, quedó {transito.Quantity} por {transito.Value}");
            }
        }

        private void Comprar()
        {
            var bodega = Bodegas[_azar.Next(Bodegas.Length)];
            var costo = Math.Round((decimal)_azar.Next(100, 500_000) / 100m, 2);
            var r = Aplicar(bodega, new MovimientoDeCosto(Cantidad(), ValoracionDelMovimiento.AlCostoIndicado, costo));
            if (r is not null) _compras.Add((bodega, r.Principal!, r.Principal!.QuantityBase));
        }

        private void Vender()
        {
            var bodega = Bodegas[_azar.Next(Bodegas.Length)];
            var cantidad = parametros.NegativoPermitido ? Cantidad() : Math.Min(Cantidad(), _fisico.GetValueOrDefault(bodega));
            if (cantidad <= 0m) return;
            var r = Aplicar(bodega, new MovimientoDeCosto(-cantidad, ValoracionDelMovimiento.AlCostoVigente));
            if (r is null) return;
            var salidas = r.Lineas.Where(l => l.Kind == KardexEntryKind.Exit).ToList();
            if (salidas.Count == 1) _ventas.Add((bodega, salidas[0], cantidad, false));
        }

        private void DevolverVenta()
        {
            if (_ventas.Count == 0) return;
            var i = _azar.Next(_ventas.Count);
            var (bodega, linea, pendiente, _) = _ventas[i];
            if (pendiente <= 0m) return;
            var cantidad = Math.Min(pendiente, Cantidad());
            if (Aplicar(bodega, new MovimientoDeCosto(cantidad, ValoracionDelMovimiento.AlCostoDeOrigen, linea.UnitCost, ReferenciaDeKardex.A(linea))) is not null)
                _ventas[i] = (bodega, linea, pendiente - cantidad, true);
        }

        private void AnularVenta()
        {
            var candidatas = Enumerable.Range(0, _ventas.Count).Where(i => !_ventas[i].Tocada && _ventas[i].Pendiente > 0m).ToList();
            if (candidatas.Count == 0) return;
            var i = candidatas[_azar.Next(candidatas.Count)];
            var (bodega, linea, pendiente, _) = _ventas[i];
            var consumos = _consumos.Where(c => c.Salida.Id == linea.Id && c.Quantity > 0m).ToList();
            var anulacion = new MovimientoDeCosto(pendiente, ValoracionDelMovimiento.AlCostoDeOrigen, linea.UnitCost, ReferenciaDeKardex.A(linea), EsAnulacion: true)
            {
                OperationDate = _fecha,
                ConsumosDelOrigen = consumos,
            };
            if (Aplicar(bodega, anulacion) is not null) _ventas[i] = (bodega, linea, 0m, true);
        }

        private void DevolverAProveedor()
        {
            if (_compras.Count == 0) return;
            var i = _azar.Next(_compras.Count);
            var (bodega, linea, pendiente) = _compras[i];
            var cantidad = Math.Min(pendiente, Cantidad());
            if (!parametros.NegativoPermitido) cantidad = Math.Min(cantidad, _fisico.GetValueOrDefault(bodega));
            if (cantidad <= 0m) return;
            if (Aplicar(bodega, new MovimientoDeCosto(-cantidad, ValoracionDelMovimiento.DevolucionDeEntrada, linea.UnitCost, ReferenciaDeKardex.A(linea))) is not null)
                _compras[i] = (bodega, linea, pendiente - cantidad);
        }

        private void Despachar()
        {
            var origen = Bodegas[_azar.Next(Bodegas.Length)];
            var destino = Bodegas.First(b => b != origen);
            var cantidad = Math.Min(Cantidad(), _fisico.GetValueOrDefault(origen));
            cantidad = Math.Min(cantidad, Math.Max(0m, Estado(Clave(origen)).Quantity));
            if (cantidad <= 0m) return;
            var salida = Aplicar(origen, new MovimientoDeCosto(-cantidad, ValoracionDelMovimiento.AlCostoVigente));
            if (salida is null) return;
            var despacho = salida.Principal!;
            Aplicar(Transito, new MovimientoDeCosto(cantidad, ValoracionDelMovimiento.AlCostoDeOrigen, despacho.UnitCost, ReferenciaDeKardex.A(despacho)), obligatorio: true);
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
            var clave = Clave(bodega);
            movimiento = movimiento with { OperationDate = _fecha };
            var resultado = MotorDeCosteo.Aplicar(Estado(clave), movimiento, parametros);
            if (!resultado.Admitido)
            {
                Afirmar(!obligatorio, $"el movimiento obligatorio se rechazó: {resultado.Rechazo!.Mensaje}");
                Afirmar(!parametros.NegativoPermitido, "con el negativo permitido no se rechaza nada");
                return null;
            }

            foreach (var l in resultado.Lineas) l.Id = ++_id;
            Kardex(clave).AddRange(resultado.Lineas);
            _consumos.AddRange(resultado.Consumos);
            _estados[clave] = resultado.Estado;
            _fisico[bodega] = _fisico.GetValueOrDefault(bodega) + movimiento.QuantityBase;

            Verificar(clave, resultado);
            return resultado;
        }

        private void Verificar(string clave, ResultadoDeCosteo resultado)
        {
            var lineas = Kardex(clave);
            var estado = resultado.Estado;
            Afirmar(lineas.Sum(l => l.QuantityBase) == estado.Quantity, $"Σ QuantityBase = Quantity en {clave}");
            Afirmar(lineas.Sum(l => l.TotalCost) == estado.Value, $"Σ TotalCost ({lineas.Sum(l => l.TotalCost)}) = Value ({estado.Value}) en {clave}");
            Afirmar(estado.Quantity != 0m || estado.Value == 0m, $"con cantidad 0 el valor es 0 en {clave}, quedó {estado.Value}");
            Afirmar(parametros.NegativoPermitido || estado.Quantity >= 0m, $"con el negativo prohibido {clave} no queda en {estado.Quantity}");
            Afirmar(resultado.Lineas.All(l => l.UnitCost >= 0m), "todo costo unitario es ≥ 0");
            Afirmar(resultado.Lineas.All(l => (l.Kind == KardexEntryKind.CostAdjustment) == (l.QuantityBase == 0m)), "QuantityBase = 0 ⇔ CostAdjustment");

            if (estado.Quantity >= 0m)
            {
                var valorCapas = estado.Capas.Sum(c => c.Valor(parametros.Montos));
                Afirmar(valorCapas == estado.Value, $"Σ valor de las capas ({valorCapas}) = Value ({estado.Value}) en {clave}");
                Afirmar(estado.Capas.Sum(c => c.RemainingQuantity) == estado.Quantity, $"Σ restante de las capas = Quantity en {clave}");
            }
            else
            {
                Afirmar(estado.Capas.Count == 0, $"en negativo no quedan capas vivas en {clave}");
            }

            foreach (var capa in estado.Capas)
            {
                var consumido = _consumos.Where(c => c.Capa.Entrada.Id == capa.Entrada.Id).Sum(c => c.Quantity);
                Afirmar(capa.RemainingQuantity > 0m && capa.RemainingQuantity <= capa.OriginalQuantity, $"0 < restante ≤ original en la capa {capa.Entrada.Id}");
                Afirmar(capa.RemainingQuantity == capa.OriginalQuantity - consumido,
                    $"restante ({capa.RemainingQuantity}) = original ({capa.OriginalQuantity}) − Σ consumos ({consumido}) en la capa {capa.Entrada.Id}");
            }

            // Σ consumos de cada línea del ámbito: salida = |q| menos lo pendiente en negativo; anulación = −q.
            var pendientes = estado.SalidasEnNegativo.GroupBy(p => p.Salida.Id).ToDictionary(g => g.Key!.Value, g => g.Sum(p => p.Cantidad));
            foreach (var l in lineas.Where(l => l.Kind == KardexEntryKind.Exit))
            {
                var consumido = _consumos.Where(c => c.Salida.Id == l.Id).Sum(c => c.Quantity);
                Afirmar(consumido + pendientes.GetValueOrDefault(l.Id!.Value) == -l.QuantityBase,
                    $"Σ consumos ({consumido}) + pendiente de la salida {l.Id} = {-l.QuantityBase} en {clave}");
            }
            foreach (var l in lineas.Where(l => l.Kind == KardexEntryKind.Entry && l.ReversesEntry is not null))
            {
                var consumido = _consumos.Where(c => c.Salida.Id == l.Id).Sum(c => c.Quantity);
                Afirmar(consumido == -l.QuantityBase || consumido == 0m, $"Σ consumos de la anulación {l.Id} = {-l.QuantityBase}, quedó {consumido}");
            }
        }

        private string Clave(string bodega) => ambito == CostScope.Cooperative ? "COOPERATIVA" : bodega;

        private decimal Cantidad() =>
            _azar.Next(4) == 0 ? Math.Round((decimal)_azar.Next(1, 500_000) / 10_000m, 4) : _azar.Next(1, 60);

        private EstadoDeCosto Estado(string clave) => _estados.GetValueOrDefault(clave, EstadoDeCosto.Vacio);

        private List<LineaDeKardexPropuesta> Kardex(string clave) =>
            _kardex.TryGetValue(clave, out var k) ? k : _kardex[clave] = [];

        private void Afirmar(bool condicion, string mensaje)
        {
            if (!condicion)
                Assert.Fail($"PEPS. Semilla {semilla}, ámbito {ambito}, negativo {parametros.NegativoPermitido}, redondeo {parametros.Montos}, paso {_paso}: {mensaje}");
        }
    }
}
