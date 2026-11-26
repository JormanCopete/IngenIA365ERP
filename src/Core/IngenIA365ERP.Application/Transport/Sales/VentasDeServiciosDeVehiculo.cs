using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Platform.Modules;
using IngenIA365ERP.Application.Transport.Notifications;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Platform;
using IngenIA365ERP.Domain.Transport;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Sales;

/// <summary>
/// Los servicios de vehículo vendidos en Comercial con la placa o el lateral en la línea (2026-11-18, pedido del dueño: en COOFLOPAL la radio
/// y los demás servicios se venden en el POS y en la factura de oficina, con precio digitado). Implementa
/// <see cref="IReferenciaDeLineaDeVenta"/>, así que Comercial sigue sin conocer Transporte:
/// <list type="bullet">
/// <item>piden la referencia los productos de los servicios activos, con Transporte encendido en la cooperativa;</item>
/// <item>lo digitado se busca como placa (normalizada), lateral (el código radio-móvil, el que se envía al proveedor) o número interno, entre los
/// vehículos no retirados; si dos vehículos distintos responden, es ambiguo; se guarda la placa;</item>
/// <item>si no es de ningún vehículo del parque y es un número de 1 a 10 dígitos, es el <b>lateral de un vehículo externo</b> (2026-11-25, pedido
/// del dueño: los servicios también se venden a quien no pertenece a la cooperativa); se guarda tal cual y el cargo nace sin vehículo, con
/// <see cref="ServiceCharge.VehicleReference"/>, y avisa al sistema externo con ese lateral. Lo que no es número y no está en el parque se
/// rechaza: los externos se identifican por el lateral. Desde el 2026-11-26 el externo pide además su <b>placa</b> (el sistema externo recibe
/// las dos): el lateral solo responde que falta el complemento, y la línea guarda «lateral/placa»; la placa es válida
/// (<see cref="Placa.EsValida"/>) y no es de un vehículo del parque (ése se vende por su placa, no como externo);</item>
/// <item>al confirmar la venta nace un cargo por línea (<see cref="ChargeOrigin.Sale"/>, sin corrida, el mes de la venta como período) por el
/// neto más impuestos de la línea. Lo pagado de contado en la venta se reparte entre los cargos por prioridad del servicio; el cargo cubierto
/// queda pagado y avisa al sistema externo (si el aviso está encendido). Lo que falta va a la primera forma de pago a crédito: por Cartera si
/// está activa (y si esa obligación cubre más que el servicio, el cargo espera a que quede saldada) o como cuenta por cobrar comercial.</item>
/// </list>
/// El deudor del cargo es el cliente de la venta (sin cliente, el propietario del vehículo). (nuevo)
/// </summary>
public sealed class VentasDeServiciosDeVehiculo(
    IApplicationDbContext db,
    FacturasDeServicios facturas,
    EmisionDeSolicitudDeConector emision,
    IDateTimeService reloj,
    IModulosHabilitados? modulos = null,
    IConsultaDeObligaciones? cartera = null) : IReferenciaDeLineaDeVenta
{
    public const string Rotulo = "Placa o lateral";
    public const string RotuloDeLaPlacaExterna = "Placa del vehículo externo";

    public async Task<IReadOnlyList<ProductoConReferencia>> ProductosQueLaPidenAsync(CancellationToken ct)
    {
        if (!await EncendidoAsync(ct)) return [];
        return await db.TransportServices.AsNoTracking().Where(s => s.IsActive)
            .Select(s => new ProductoConReferencia(s.ProductPublicId, Rotulo, RotuloDeLaPlacaExterna)).ToListAsync(ct);
    }

    public async Task<Result<ReferenciaDeLineaResuelta>> ResolverAsync(Guid productoPublicId, string texto, CancellationToken ct)
    {
        var (parte, complemento) = Partir(texto);
        var buscado = Placa.Normalizar(parte);
        if (buscado.Length == 0) return Result.Failure<ReferenciaDeLineaResuelta>(Errores.SinReferencia());

        var candidatos = await db.Vehicles.AsNoTracking()
            .Where(v => v.Status != VehicleStatus.Retired && (v.Plate == buscado || v.RadioCode == buscado || v.InternalNumber == buscado))
            .Select(v => new { v.Id, v.Plate, v.RadioCode, v.InternalNumber, v.OwnerPersonId, v.Status }).ToListAsync(ct);
        if (candidatos.Count == 0)
        {
            if (!LateralExterno.Es(buscado)) return Result.Failure<ReferenciaDeLineaResuelta>(Errores.NoExiste(parte.Trim()));
            if (complemento is null)
                return Result.Success(new ReferenciaDeLineaResuelta(buscado, $"lateral {buscado} · fuera del parque", RotuloDeLaPlacaExterna));

            var placa = Placa.Normalizar(complemento);
            if (!Placa.EsValida(placa)) return Result.Failure<ReferenciaDeLineaResuelta>(Errores.PlacaExternaInvalida(complemento.Trim()));
            var delParque = await db.Vehicles.AsNoTracking().Where(v => v.Status != VehicleStatus.Retired && v.Plate == placa)
                .Select(v => v.InternalNumber).FirstOrDefaultAsync(ct);
            if (delParque is not null) return Result.Failure<ReferenciaDeLineaResuelta>(Errores.PlacaDelParque(placa, delParque));
            return Result.Success(new ReferenciaDeLineaResuelta(Guardada(buscado, placa), $"{placa} · lateral {buscado} · fuera del parque"));
        }

        // La placa manda: «ABC123» es la placa de uno aunque sea el lateral de otro. Entre lateral y número interno de vehículos distintos, ambiguo.
        var elegido = candidatos.Where(v => v.Plate == buscado).ToList() is { Count: 1 } porPlaca ? porPlaca[0]
            : candidatos.Where(v => v.RadioCode == buscado).ToList() is { Count: 1 } porLateral ? porLateral[0]
            : candidatos.Count == 1 ? candidatos[0]
            : null;
        if (elegido is null) return Result.Failure<ReferenciaDeLineaResuelta>(Errores.Ambigua(parte.Trim()));
        // Un vehículo del parque se conoce por su ficha: no lleva placa aparte.
        if (complemento is not null) return Result.Failure<ReferenciaDeLineaResuelta>(Errores.DelParqueSinPlacaAparte(parte.Trim(), elegido.Plate));

        var dueño = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == elegido.OwnerPersonId).FirstOrDefaultAsync(ct);
        var etiqueta = string.Join(" · ", new[]
        {
            elegido.Plate,
            elegido.RadioCode is { Length: > 0 } l ? $"lateral {l}" : null,
            elegido.Status == VehicleStatus.Inactive ? "inactivo" : null,
            dueño is null ? null : NombreVisible.De(dueño),
        }.Where(x => x is not null));
        return Result.Success(new ReferenciaDeLineaResuelta(elegido.Plate, etiqueta));
    }

    public async Task<Result<bool>> AlConfirmarVentaAsync(Guid documentoPublicId, CancellationToken ct)
    {
        var servicios = await db.TransportServices.Where(s => s.IsActive).ToListAsync(ct);
        if (servicios.Count == 0) return Result.Success(false);

        var factura = await facturas.PorPublicIdAsync(documentoPublicId, ct);
        // La factura mensual de servicios registra sus propios cargos (FacturadorDeVehiculo), uno por vehículo, servicio y período.
        if (factura is null || factura.ExternalReference?.StartsWith(Billing.FacturadorDeVehiculo.PrefijoDeReferencia, StringComparison.Ordinal) == true)
            return Result.Success(false);
        var lineas = factura.Lines
            .Where(l => l.ItemReference is { Length: > 0 } && servicios.Any(s => s.ProductPublicId == l.ProductPublicId) && l.Weight > 0m)
            .OrderBy(l => l.LineNumber).ToList();
        if (lineas.Count == 0) return Result.Success(false);

        var ya = await db.ServiceCharges.AsNoTracking().Where(c => c.InvoicePublicId == factura.PublicId && c.Origin == ChargeOrigin.Sale)
            .Select(c => c.InvoiceLinePublicId).ToListAsync(ct);
        lineas = lineas.Where(l => !ya.Contains(l.PublicId)).ToList();
        if (lineas.Count == 0) return Result.Success(false);

        var placas = lineas.Select(l => l.ItemReference!).Distinct().ToList();
        var vehiculos = await db.Vehicles.Where(v => placas.Contains(v.Plate) && v.Status != VehicleStatus.Retired).ToListAsync(ct);
        int? cliente = factura.CounterpartyPersonPublicId is { } c
            ? await db.People.AsNoTracking().Where(p => p.PublicId == c).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
            : null;

        var ahora = reloj.UtcNow;
        var numero = factura.DisplayNumber ?? string.Empty;
        var periodo = factura.OperationDate.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
        var cargos = new List<(ServiceCharge Cargo, TransportService Servicio, int Orden)>();
        foreach (var l in lineas)
        {
            var vehiculo = vehiculos.FirstOrDefault(v => v.Plate == l.ItemReference);
            // Sin vehículo del parque, lo guardado es el lateral de un vehículo externo y su placa («lateral/placa»).
            var (lateral, placaExterna) = Partir(l.ItemReference!);
            if (vehiculo is null && !LateralExterno.Es(lateral)) return Result.Failure<bool>(Errores.NoExiste(l.ItemReference!));
            var servicio = servicios.First(s => s.ProductPublicId == l.ProductPublicId);
            var cargo = new ServiceCharge
            {
                VehicleId = vehiculo?.Id, Vehicle = vehiculo, VehicleReference = vehiculo is null ? lateral : null,
                ExternalPlate = vehiculo is null && placaExterna is not null ? Placa.Normalizar(placaExterna) : null,
                ServiceId = servicio.Id, Service = servicio, Period = periodo, Origin = ChargeOrigin.Sale,
                OwnerPersonId = cliente ?? vehiculo?.OwnerPersonId, InvoicePublicId = factura.PublicId, InvoiceNumber = numero,
                InvoiceLinePublicId = l.PublicId, Amount = l.Weight, Route = ChargeRoute.Cash, Status = ChargeStatus.Open, CreatedAt = ahora,
            };
            db.ServiceCharges.Add(cargo);
            cargos.Add((cargo, servicio, l.LineNumber));
        }

        // Lo de contado (todo lo que no es crédito) se reparte entre los servicios por prioridad; lo que falta, a la primera forma a crédito.
        var contado = factura.Payments.Where(p => !p.IsCredit).Sum(p => p.Amount);
        var aCredito = factura.Payments.Where(p => p.IsCredit).OrderBy(p => p.LineNumber).ToList();
        var (partes, _) = RepartoPorPrioridad.Repartir(contado,
            cargos.Select((x, i) => new CargoPorRepartir(i, x.Servicio.Priority, x.Orden, x.Cargo.Amount)));
        var deContado = partes.ToDictionary(p => p.CargoId, p => p.Importe);

        var conCartera = aCredito.Count > 0 && cartera is not null
                         && (await cartera.ObligacionesAsync(new ConsultaDeObligaciones([], factura.OperationDate, false), ct)).LendingStatus == EstadoDeCartera.Active;
        var credito = aCredito.FirstOrDefault();
        var faltantes = cargos.Select((x, i) => x.Cargo.Amount - deContado.GetValueOrDefault(i)).Where(f => f > 0m).ToList();
        // Si la obligación cubre más que lo que le falta a los servicios, o a más de uno, un abono no es de un servicio: se espera el saldo.
        var esperaSaldo = credito is not null && (faltantes.Count > 1 || credito.Amount > faltantes.Sum() + 0.01m);

        for (var i = 0; i < cargos.Count; i++)
        {
            var (cargo, _, _) = cargos[i];
            var pagado = deContado.GetValueOrDefault(i);
            if (pagado > 0m)
            {
                db.ChargeAllocations.Add(new ChargeAllocation
                {
                    Charge = cargo, Source = AllocationSource.CashAtInvoice, ReceiptNumber = numero, Amount = pagado, AppliedAt = ahora,
                    ValueDate = factura.OperationDate, CreatedAt = ahora,
                });
            }
            if (pagado >= cargo.Amount)
            {
                cargo.Route = ChargeRoute.Cash;
                cargo.PaymentPublicId = factura.Payments.Where(p => !p.IsCredit).OrderBy(p => p.LineNumber).Select(p => (Guid?)p.PublicId).FirstOrDefault();
                cargo.Status = ChargeStatus.Paid;
                cargo.PaidAt = ahora;
                await emision.EmitirAsync(cargo, new PagoQueSalda(numero, factura.OperationDate), ct);
            }
            else if (credito is not null)
            {
                cargo.Route = conCartera ? ChargeRoute.Lending : ChargeRoute.CommercialReceivable;
                cargo.PaymentPublicId = credito.PublicId;
                cargo.RequiresPaidOff = conCartera && esperaSaldo;
            }
            else
            {
                // Sin crédito y sin cubrir (un saldo que queda por cobrar en Comercial): lo salda el recibo que lo aplique.
                cargo.Route = ChargeRoute.CommercialReceivable;
            }
        }
        return Result.Success(true);
    }

    /// <summary>El lateral de un vehículo que no está en el parque: de 1 a 10 dígitos.</summary>
    public static class LateralExterno
    {
        public const int LargoMaximo = 10;

        public static bool Es(string texto) => texto.Length is > 0 and <= LargoMaximo && texto.All(char.IsAsciiDigit);
    }

    /// <summary>Lo digitado o guardado partido en la referencia y su complemento («618/ABC123»); sin separador, el complemento es nulo.</summary>
    private static (string Parte, string? Complemento) Partir(string texto)
    {
        var i = texto.IndexOf(ReferenciaDeLinea.Separador);
        if (i < 0) return (texto, null);
        var complemento = texto[(i + 1)..].Trim();
        return (texto[..i], complemento.Length == 0 ? null : complemento);
    }

    /// <summary>Lo que guarda la línea de un vehículo externo: el lateral y su placa.</summary>
    private static string Guardada(string lateral, string placa) => $"{lateral}{ReferenciaDeLinea.Separador}{placa}";

    private async Task<bool> EncendidoAsync(CancellationToken ct)
    {
        if (modulos is null) return true;
        return (await modulos.DeLaCooperativaActualAsync(ct)).Contains(ModulosHabilitables.Transporte);
    }

    private static class Errores
    {
        public static Error SinReferencia() => new("Transport.Sale.VehicleRequired", "Digite la placa o el lateral del vehículo.");

        public static Error NoExiste(string texto) => new ErrorConDatos("Transport.Sale.VehicleNotFound",
            $"No hay un vehículo vinculado con placa o lateral «{texto}». Si el vehículo no es de la cooperativa, digite su lateral (sólo números).",
            new { reference = texto });

        public static Error PlacaExternaInvalida(string texto) => new ErrorConDatos("Transport.Sale.ExternalPlateInvalid",
            $"«{texto}» no es una placa válida: de {Placa.LargoMinimo} a {Placa.LargoMaximo} letras o números.", new { plate = texto });

        public static Error PlacaDelParque(string placa, string interno) => new ErrorConDatos("Transport.Sale.ExternalPlateInFleet",
            $"La placa {placa} es del vehículo {interno} de la cooperativa: digite esa placa en vez del lateral.", new { plate = placa, internalNumber = interno });

        public static Error DelParqueSinPlacaAparte(string texto, string placa) => new ErrorConDatos("Transport.Sale.FleetVehicleNoExternalPlate",
            $"«{texto}» es el vehículo {placa} de la cooperativa: no lleva placa aparte.", new { reference = texto, plate = placa });

        public static Error Ambigua(string texto) => new ErrorConDatos("Transport.Sale.VehicleAmbiguous",
            $"«{texto}» es el lateral o el número interno de más de un vehículo: digite la placa.", new { reference = texto });
    }
}
