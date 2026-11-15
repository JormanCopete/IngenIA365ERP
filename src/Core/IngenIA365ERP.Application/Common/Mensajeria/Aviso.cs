namespace IngenIA365ERP.Application.Common.Mensajeria;

/// <summary>
/// Un aviso de trabajo pendiente (feature 023; contracts/mensajeria.md §2). <b>No es fuente de verdad de nada</b>: dice «mire esta fila
/// de esta cooperativa»; quien lo consume relee la base y actúa sólo si la fila sigue elegible. No lleva datos de negocio —ni montos, ni
/// números fiscales, ni personas—: sólo el <c>PublicId</c> de la cooperativa y de la entidad (Principio IV, Complexity Tracking del plan).
/// </summary>
/// <param name="V">Versión del formato (1).</param>
/// <param name="Tipo">Uno de <see cref="TiposDeAviso"/>.</param>
/// <param name="Cooperativa">PublicId de la cooperativa.</param>
/// <param name="Entidad">PublicId del documento electrónico, o el código del destino para <see cref="TiposDeAviso.IntegracionEntregar"/>.</param>
/// <param name="EmitidoEn">Cuándo se emitió (UTC).</param>
/// <param name="Origen"><c>cobro</c>, <c>confirmacion</c>, <c>resultado</c>, <c>barrido</c> o <c>reproceso</c>.</param>
public sealed record Aviso(int V, string Tipo, Guid Cooperativa, string Entidad, DateTime EmitidoEn, string Origen)
{
    public const int VersionActual = 1;

    /// <summary>Un aviso nuevo de la versión vigente.</summary>
    public static Aviso De(string tipo, Guid cooperativa, string entidad, DateTime ahora, string origen) =>
        new(VersionActual, tipo, cooperativa, entidad, ahora, origen);

    /// <summary>Un aviso sobre un documento electrónico.</summary>
    public static Aviso De(string tipo, Guid cooperativa, Guid entidad, DateTime ahora, string origen) =>
        De(tipo, cooperativa, entidad.ToString("D"), ahora, origen);

    /// <summary>La entidad como Guid (documentos); nulo si no lo es (destinos de integración).</summary>
    public Guid? EntidadComoGuid => Guid.TryParse(Entidad, out var g) ? g : null;

    /// <summary>El <c>message-id</c> AMQP: trazabilidad, no deduplicación (la idempotencia está en la base).</summary>
    public string IdDeMensaje => $"{Tipo}:{Cooperativa:N}:{Entidad}:{EmitidoEn.Ticks}";
}

/// <summary>Los tipos de aviso y la cola de cada uno (contracts/mensajeria.md §1). (nuevo)</summary>
public static class TiposDeAviso
{
    public const string FeEmitir = "fe.emitir";
    public const string FeSeguimiento = "fe.seguimiento";
    public const string IntegracionEntregar = "integracion.entregar";

    public const string ColaEmision = "erp.fe.emision";
    public const string ColaSeguimiento = "erp.fe.seguimiento";
    public const string ColaIntegracion = "erp.integracion";
    public const string ColaMuertos = "erp.muertos";

    public static IReadOnlyList<string> Todos { get; } = [FeEmitir, FeSeguimiento, IntegracionEntregar];

    public static IReadOnlyList<string> Colas { get; } = [ColaEmision, ColaSeguimiento, ColaIntegracion];

    /// <summary>La cola de un tipo; lanza ante uno desconocido (es un defecto de quien publica).</summary>
    public static string ColaDe(string tipo) => tipo switch
    {
        FeEmitir => ColaEmision,
        FeSeguimiento => ColaSeguimiento,
        IntegracionEntregar => ColaIntegracion,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de aviso desconocido."),
    };
}
