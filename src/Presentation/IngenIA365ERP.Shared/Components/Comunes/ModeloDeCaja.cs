namespace IngenIA365ERP.Shared.Components.Comunes;

// 2026-11-24 (pedido del dueño): la caja es la misma en Comercial y en Recaudo. Estas piezas no conocen ningún módulo: cada pantalla traduce sus DTO a
// estos modelos y lo digitado de vuelta a sus peticiones. Lo usan AperturaDeCaja, MiCajaAbierta, ArqueoDeCaja, ResultadoDelArqueo y TablaDeCajas. (nuevo)

/// <summary>
/// Una caja (o un punto) que se puede elegir; <see cref="OcupadaPor"/> dice quién la tiene abierta. 2026-11-25: <see cref="SoloPara"/> no nulo, la caja es
/// de otros cajeros (<c>Caja.SoloCajerosAsignados</c>) y dice por qué no («de Leidy Agudelo», «sin cajeros asignados»); tampoco se elige. Lo arma
/// <see cref="Para"/>.
/// </summary>
public sealed record OpcionDeCaja(Guid Id, string Rotulo, string? OcupadaPor = null, string? SoloPara = null)
{
    /// <summary>Si se puede elegir para abrir.</summary>
    public bool Libre => OcupadaPor is null && SoloPara is null;

    /// <summary>El motivo de <see cref="SoloPara"/> a partir de lo que dice el servidor: nulo si quien pregunta la puede abrir (o no se sabe).</summary>
    public static string? Para(bool? abreYo, IReadOnlyList<string>? cajeros) =>
        abreYo != false ? null : cajeros is { Count: > 0 } c ? $"de {string.Join(", ", c)}" : "sin cajeros asignados";
}

/// <summary>Un billete o una moneda vigente.</summary>
public sealed record DenominacionDeConteo(Guid Id, decimal Valor, bool EsMoneda);

/// <summary>Cómo se cuenta un medio en el arqueo.</summary>
public enum FormaDeContar
{
    /// <summary>El efectivo: un valor o billete por billete, según <c>Caja.FormaDeConteoDelEfectivo</c>.</summary>
    Efectivo,

    /// <summary>Un solo total (el arqueo sencillo cuenta así tarjetas, transferencias, consignaciones y cheques).</summary>
    Total,

    /// <summary>Por lote de datáfono (sólo con el arqueo estricto).</summary>
    Lotes,

    /// <summary>Marcando los pagos uno a uno (sólo con el arqueo estricto, y si el módulo los manda).</summary>
    Referencias,
}

public sealed record DatafonoDeConteo(Guid Id, string Rotulo, decimal? Esperado = null, int Pagos = 0);

public sealed record ReferenciaDeConteo(Guid Id, string Documento, string Referencia, decimal Valor);

/// <summary>Un lote de datáfono digitado.</summary>
public sealed class LoteDigitado
{
    public Guid? Datafono { get; set; }
    public string Numero { get; set; } = "";
    public decimal Valor { get; set; }
    public int Cantidad { get; set; }
}

/// <summary>Un medio a contar, con lo esperado (nulo en el arqueo ciego) y lo que digita quien cuenta.</summary>
public sealed class MedioAContar
{
    public required Guid Id { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public required FormaDeContar Forma { get; init; }

    /// <summary>Lo que debería haber; nulo con arqueo ciego.</summary>
    public decimal? Esperado { get; init; }

    public decimal Tolerancia { get; init; }

    /// <summary>De dónde sale lo esperado, en una línea (base, recibos, entradas, salidas…).</summary>
    public string? DetalleDelEsperado { get; init; }

    public IReadOnlyList<DatafonoDeConteo> Datafonos { get; init; } = [];
    public IReadOnlyList<ReferenciaDeConteo> Referencias { get; init; } = [];

    public decimal Contado { get; set; }
    public bool PorBilletes { get; set; }
    public Dictionary<Guid, int> Billetes { get; } = [];
    public List<LoteDigitado> Lotes { get; } = [];
    public HashSet<Guid> Cotejados { get; } = [];
    public string? Motivo { get; set; }

    public decimal? Diferencia => Esperado is { } e ? Contado - e : null;

    public bool FueraDeTolerancia => Diferencia is { } d && Math.Abs(d) > Tolerancia;

    /// <summary>
    /// Si el motivo es obligatorio: con el arqueo estricto, en toda diferencia; con el sencillo, sólo fuera de la tolerancia. En el arqueo ciego no se
    /// sabe: lo dice el servidor al cerrar.
    /// </summary>
    public bool PideMotivo(bool estricto) => Diferencia is { } d && d != 0m && (estricto || FueraDeTolerancia);
}

/// <summary>La base con que se abre: un valor o billete por billete.</summary>
public sealed record BaseDeApertura(Guid Caja, decimal? Valor, IReadOnlyDictionary<Guid, int>? Billetes);

/// <summary>Una línea del resultado del arqueo.</summary>
public sealed record LineaDelResultado(string Medio, decimal Esperado, decimal Contado, decimal Diferencia, string Tratamiento);

/// <summary>Una caja de la consulta «Cajas del día».</summary>
public sealed record FilaDeCaja(Guid Id, DateOnly Dia, string Punto, string Caja, string Cajero, string Estado, bool Abierta, DateTime AbiertaEl,
    DateTime? CerradaEl, decimal Base, string? Movido = null, bool Pendiente = false);

/// <summary>Las formas de contar el efectivo de <c>Caja.FormaDeConteoDelEfectivo</c> y si el arqueo es estricto.</summary>
public sealed record PoliticaDeCaja(string FormaDelEfectivo, bool Estricto)
{
    public static PoliticaDeCaja PorDefecto { get; } = new(IngenIA365ERP.Shared.Services.Recaudo.FormasDeConteo.AElegir, false);
}
