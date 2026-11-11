namespace IngenIA365ERP.Shared.Services.Pagos;

/// <summary>Una opción elegible de un medio (un datáfono, una cuenta propia): su PublicId y cómo se muestra.</summary>
public sealed record OpcionDeMedio(Guid PublicId, string Texto);

/// <summary>Las condiciones propuestas de un medio de crédito (sólo ventas).</summary>
public sealed record CreditoDelMedio(short? Cuotas, short? MaximoDeCuotas, short? PlazoDias, short? MaximoPlazoDias, short? PeriodicidadDias, string? LineaSugerida);

/// <summary>
/// Feature 022 (T062; FR-040): un medio de pago tal como lo ofrece el panel único, venga de una venta (POS, oficina) o de la caja
/// de Recaudo. Cada módulo arma la lista con lo que trae su servidor; el panel no sabe de dónde viene.
/// </summary>
public sealed record MedioDisponible(
    Guid PublicId,
    string Codigo,
    string Nombre,
    int Clase,
    string? Tecla,
    bool DaVueltas,
    bool PideReferencia,
    int? TipoDeReferencia,
    IReadOnlyList<OpcionDeMedio> Datafonos,
    Guid? DatafonoPorDefecto,
    IReadOnlyList<OpcionDeMedio> CuentasPropias,
    CreditoDelMedio? Credito = null)
{
    public bool EsEfectivo => Clase == ClasesDeMedio.Efectivo;
    public bool EsTarjeta => Clase is ClasesDeMedio.TarjetaCredito or ClasesDeMedio.TarjetaDebito;
    public bool EsCredito => Clase is ClasesDeMedio.CreditoAsociado or ClasesDeMedio.CreditoCliente;
    public bool EsBono => Clase == ClasesDeMedio.Bono;
    public bool EsCheque => Clase == ClasesDeMedio.Cheque;

    /// <summary>Transferencia o consignación con cuentas propias habilitadas: la cuenta es obligatoria (Q16).</summary>
    public bool PideCuentaPropia => Clase is ClasesDeMedio.Consignacion or ClasesDeMedio.Transferencia && CuentasPropias.Count > 0;
}

/// <summary>Las clases de medio (<c>PaymentMeansClass</c>) que el panel distingue.</summary>
public static class ClasesDeMedio
{
    public const int Efectivo = 1, TarjetaCredito = 2, TarjetaDebito = 3, CreditoAsociado = 4, CreditoCliente = 5, Consignacion = 6, Transferencia = 7,
        Bono = 8, Cheque = 9;
}

/// <summary>Los datos de un cheque recibido, iguales en ventas y en Recaudo (Q17).</summary>
public sealed class ChequeEnCobro
{
    public Guid? Banco { get; set; }
    public string? Numero { get; set; }
    public string? Cuenta { get; set; }
    public DateOnly? Fecha { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public bool MismaPlaza { get; set; } = true;
    public string? Girador { get; set; }
}

/// <summary>Un pago que se está armando en el panel. Tiene el superconjunto de datos de los dos módulos.</summary>
public sealed class PagoMultiple(MedioDisponible medio)
{
    /// <summary>Identificador estable de la fila (para la pantalla y para el MeansId de Recaudo).</summary>
    public Guid Id { get; } = Guid.NewGuid();
    public MedioDisponible Medio { get; } = medio;

    /// <summary>Lo que se aplica a la deuda.</summary>
    public decimal Monto { get; set; }

    /// <summary>Lo que entregó quien paga en efectivo; de ahí salen las vueltas.</summary>
    public decimal? Recibido { get; set; }

    public string? Referencia { get; set; }
    public string? Aprobacion { get; set; }
    public Guid? Datafono { get; set; }
    public string? Lote { get; set; }

    /// <summary>Los últimos cuatro dígitos de la tarjeta, nunca el número (<c>LosPagosNoGuardanElNumeroDeTarjeta</c>).</summary>
    public string? Last4 { get; set; }

    public string? NumeroDeBono { get; set; }
    public Guid? CuentaPropia { get; set; }
    public ChequeEnCobro? Cheque { get; set; }

    public short? Cuotas { get; set; }
    public short? PlazoDias { get; set; }
    public short? PeriodicidadDias { get; set; }
    public DateOnly? PrimerVencimiento { get; set; }
    public string? Linea { get; set; }

    /// <summary>Las vueltas de este pago: lo recibido de más en un medio que da vueltas.</summary>
    public decimal Vueltas => Medio.DaVueltas && Recibido is { } r && r > Monto ? r - Monto : 0m;
}

/// <summary>
/// Feature 022 (T062; FR-040 a FR-048): el cobro con uno o varios medios, igual en el POS, la factura, las notas, los documentos
/// del ciclo y la caja de Recaudo (reemplaza a <c>CobroDelPos</c>). Agregar un medio lo agrega por lo que falta; el efectivo
/// recibido de más son vueltas, nunca un pago de más. Aquí sólo se evita mandar lo que seguro se rechaza: las reglas definitivas
/// (cuenta habilitada, bono usado, crédito, cheque) son del servidor de cada módulo.
/// </summary>
public sealed class CobroMultiple(decimal aPagar)
{
    private readonly List<PagoMultiple> _pagos = [];

    public decimal APagar { get; private set; } = aPagar;
    public IReadOnlyList<PagoMultiple> Pagos => _pagos;
    public decimal Pagado => _pagos.Sum(p => p.Monto);
    public decimal Falta => Math.Max(0m, APagar - Pagado);

    /// <summary>Lo pagado de más (en valor, no en lo recibido): no deja confirmar.</summary>
    public decimal Sobra => Math.Max(0m, Pagado - APagar);

    public decimal Vueltas => _pagos.Sum(p => p.Vueltas);

    /// <summary>Cambia lo que hay que pagar (Recaudo simula de nuevo; la venta recalcula). Los pagos quedan como estaban.</summary>
    public void CambiarAPagar(decimal aPagar) => APagar = aPagar;

    /// <summary>Agrega un pago del medio por lo que falta; sin nada que falte no agrega. Propone datáfono, cuenta y crédito.</summary>
    public PagoMultiple? Agregar(MedioDisponible medio)
    {
        if (Falta <= 0m) return null;
        var pago = Nuevo(medio, Falta);
        _pagos.Add(pago);
        return pago;
    }

    /// <summary>
    /// 2026-11-09 (pedido del dueño): agrega un abono de <paramref name="valor"/> con el medio. Un abono menor que lo que falta deja
    /// el resto para otro medio; uno mayor sólo cabe en un medio que da vueltas (lo de más queda en «Recibido» y sale en vueltas): en
    /// los demás se registra por lo que falta y <paramref name="aviso"/> lo dice. Sin valor, por lo que falta.
    /// </summary>
    public PagoMultiple? Agregar(MedioDisponible medio, decimal? valor, out string? aviso)
    {
        aviso = null;
        var falta = Falta;
        var pago = Agregar(medio);
        if (pago is null || valor is not { } v || v <= 0m) return pago;
        aviso = AjustarMonto(pago, v, falta);
        return pago;
    }

    /// <summary>
    /// Cambia el valor de un pago ya registrado con la misma regla del abono: lo que pase de lo que le cabe (lo que falta más lo que
    /// ya pagaba) va a vueltas si el medio las da, y si no, se ajusta. Devuelve el aviso del ajuste o nulo.
    /// </summary>
    public string? FijarMonto(PagoMultiple pago, decimal valor)
    {
        if (!_pagos.Contains(pago)) return null;
        return AjustarMonto(pago, Math.Max(0m, valor), CabeEn(pago));
    }

    /// <summary>Cambia el medio de un pago ya registrado conservando su lugar y su valor; los datos propios del medio anterior se pierden.</summary>
    public PagoMultiple? CambiarMedio(PagoMultiple pago, MedioDisponible medio, out string? aviso)
    {
        aviso = null;
        var i = _pagos.IndexOf(pago);
        if (i < 0 || pago.Medio.Codigo == medio.Codigo) return null;
        var valor = pago.Recibido is { } r && r > pago.Monto ? r : pago.Monto;
        var cabe = CabeEn(pago);
        var nuevo = Nuevo(medio, cabe);
        _pagos[i] = nuevo;
        aviso = AjustarMonto(nuevo, valor, cabe);
        return nuevo;
    }

    /// <summary>Lo máximo que un pago ya registrado puede pagar: lo que falta más lo que él mismo paga.</summary>
    public decimal CabeEn(PagoMultiple pago) => Math.Max(0m, APagar - (Pagado - pago.Monto));

    /// <summary>
    /// El valor escrito es lo que entregó quien paga por ese medio: si cabe, es el pago; si pasa de lo que cabe, el pago es lo que cabe
    /// y, en un medio que da vueltas, lo de más queda en «Recibido».
    /// </summary>
    private static string? AjustarMonto(PagoMultiple pago, decimal valor, decimal cabe)
    {
        if (valor <= cabe)
        {
            pago.Monto = valor;
            pago.Recibido = null;
            return null;
        }
        pago.Monto = cabe;
        if (pago.Medio.DaVueltas)
        {
            pago.Recibido = valor;
            return null;
        }
        pago.Recibido = null;
        return $"{pago.Medio.Nombre} no da vueltas: quedó por {cabe:N0}, lo que faltaba.";
    }

    private static PagoMultiple Nuevo(MedioDisponible medio, decimal monto) => new(medio)
    {
        Monto = monto,
        Datafono = medio.EsTarjeta ? medio.DatafonoPorDefecto ?? (medio.Datafonos.Count == 1 ? medio.Datafonos[0].PublicId : null) : null,
        CuentaPropia = medio.CuentasPropias.Count == 1 ? medio.CuentasPropias[0].PublicId : null,
        Cheque = medio.EsCheque ? new ChequeEnCobro() : null,
        Cuotas = medio.Credito?.Cuotas,
        PlazoDias = medio.Credito?.PlazoDias,
        PeriodicidadDias = medio.Credito?.PeriodicidadDias,
        Linea = medio.Credito?.LineaSugerida,
    };

    public void Quitar(PagoMultiple pago) => _pagos.Remove(pago);

    public void Limpiar() => _pagos.Clear();

    /// <summary>Lo que impide confirmar, o nulo. <paramref name="clienteIdentificado"/>: el cheque y el crédito lo exigen.</summary>
    public string? Problema(bool clienteIdentificado = true)
    {
        if (_pagos.Count == 0 || Falta > 0m) return $"Faltan {Falta:N0} por pagar.";
        if (Sobra > 0m) return $"Sobran {Sobra:N0}: ajuste los valores (lo recibido de más en efectivo va en «Recibido»).";
        foreach (var p in _pagos)
        {
            var m = p.Medio;
            if (p.Monto <= 0m) return $"El pago con {m.Nombre} no tiene valor.";
            if (p.Recibido is { } r && r < p.Monto) return $"Lo recibido en {m.Nombre} es menor que el pago.";
            if (m.PideReferencia && !m.EsBono && !m.EsCheque && string.IsNullOrWhiteSpace(p.Referencia)) return $"{m.Nombre} exige la referencia.";
            if (m.EsTarjeta && p.Datafono is null && m.Datafonos.Count > 0) return $"Elija el datáfono de {m.Nombre}.";
            if (!string.IsNullOrEmpty(p.Last4) && !Last4Valido(p.Last4)) return "De la tarjeta sólo se digitan los últimos cuatro dígitos.";
            if (m.EsBono && string.IsNullOrWhiteSpace(p.NumeroDeBono)) return $"Escriba el número del bono de {m.Nombre}.";
            if (m.PideCuentaPropia && p.CuentaPropia is null) return $"Elija la cuenta propia donde entró {m.Nombre}.";
            if (m.EsCheque)
            {
                if (!clienteIdentificado) return "El cheque exige un cliente identificado.";
                var c = p.Cheque;
                if (c?.Banco is null || string.IsNullOrWhiteSpace(c.Numero) || string.IsNullOrWhiteSpace(c.Cuenta) || c.Fecha is null)
                    return "Del cheque se necesitan banco, número, cuenta y fecha.";
            }
            if (m.EsCredito)
            {
                if (!clienteIdentificado) return "Al consumidor final no se le vende a crédito.";
                if (p.Cuotas is null or <= 0 || p.PlazoDias is null or <= 0 || p.PeriodicidadDias is null or <= 0)
                    return $"El crédito con {m.Nombre} necesita cuotas, plazo y periodicidad.";
            }
        }
        return null;
    }

    public static bool Last4Valido(string? s) => s is { Length: 4 } && s.All(char.IsAsciiDigit);

    internal static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
