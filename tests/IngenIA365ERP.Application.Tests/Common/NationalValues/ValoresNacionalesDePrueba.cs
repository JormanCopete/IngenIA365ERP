using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.NationalValues;

/// <summary>
/// Una central de mentiras para la réplica (feature 013, T0024–T0026): guarda filas con su <c>Sequence</c> y responde como
/// <c>RegistroCentralDeValoresNacionales</c> (lo de <c>Sequence</c> mayor que la marca, en orden), anota con qué marca le
/// preguntaron y puede fallar. (nuevo)
/// </summary>
public sealed class CentralDePrueba : IRegistroCentralDeValoresNacionales
{
    private long _siguiente = 1;

    public List<ValorCentral> Valores { get; } = [];
    public List<DocumentoCentral> Documentos { get; } = [];
    public List<long> MarcasPedidas { get; } = [];
    public Exception? Falla { get; set; }

    /// <summary>Si es verdadero, devuelve también lo que ya está bajo la marca (una central que se equivoca): la guarda lo salta.</summary>
    public bool DevolverTodo { get; set; }

    public DateTime RegistradoEn { get; set; } = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public ValorCentral Valor(string codigo, string clave, DateOnly desde, decimal? valor, string? unidad = null, DateOnly? hasta = null,
        Guid? corrige = null, string? etiqueta = null, string fuente = "Norma de prueba")
    {
        var v = new ValorCentral(Guid.NewGuid(), _siguiente++, codigo, clave, desde, hasta, null, valor, unidad, etiqueta, fuente,
            corrige is null ? null : "Corrección", RegistradoEn, corrige);
        Valores.Add(v);
        return v;
    }

    public ValorCentral Festivo(DateOnly fecha, string nombre) =>
        Valor(CodigosDeValorNacional.Festivo, fecha.ToString("yyyy-MM-dd"), fecha, null, hasta: fecha, etiqueta: nombre);

    public DocumentoCentral Documento(string clase, string codigo, DateOnly desde, string contenido, string sha)
    {
        var d = new DocumentoCentral(Guid.NewGuid(), _siguiente++, clase, codigo, desde, null, contenido, sha, 1, false, "Fuente", null, RegistradoEn, null);
        Documentos.Add(d);
        return d;
    }

    public Task<LecturaDeLaCentral> LeerDesdeAsync(long despuesDe, int maximo, CancellationToken ct)
    {
        MarcasPedidas.Add(despuesDe);
        if (Falla is not null) throw Falla;
        var valores = Valores.Where(v => DevolverTodo || v.Sequence > despuesDe).OrderBy(v => v.Sequence).ToList();
        var documentos = Documentos.Where(d => DevolverTodo || d.Sequence > despuesDe).OrderBy(d => d.Sequence).ToList();
        return Task.FromResult(new LecturaDeLaCentral(valores, documentos));
    }
}

/// <summary>Ayudas comunes: el reloj y las copias armadas a mano en la base de la cooperativa. (nuevo)</summary>
public static class CopiasDePrueba
{
    public static IDateTimeService Reloj(DateTime ahora)
    {
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(ahora);
        reloj.TodayUtc.Returns(DateOnly.FromDateTime(ahora));
        reloj.HoyLocal.Returns(DateOnly.FromDateTime(ahora));
        return reloj;
    }

    /// <summary>Una réplica y sus copias, guardadas, como las dejaría <c>ReplicacionDeValoresNacionales</c>.</summary>
    public static async Task<NationalValueReplication> SembrarAsync(IApplicationDbContext db, DateTime conocidaEn, params NationalValue[] copias)
    {
        var replica = new NationalValueReplication
        {
            FromSequence = copias.Min(c => c.SourceSequence), ToSequence = copias.Max(c => c.SourceSequence), ValueCount = copias.Length,
            Origin = NationalValueReplicationOrigin.ScheduledTask, StartedAt = conocidaEn, FinishedAt = conocidaEn, ActorName = "prueba",
        };
        replica.Values.AddRange(copias);
        db.NationalValueReplications.Add(replica);
        await db.SaveChangesAsync();
        return replica;
    }

    private static long _secuencia = 1000;

    public static NationalValue Copia(string codigo, string clave, DateOnly desde, decimal? valor, DateTime conocidaEn,
        DateOnly? hasta = null, Guid? corrige = null, Guid? origen = null, string? unidad = null) => new()
    {
        Code = codigo, Key = clave, ValidFrom = desde, ValidTo = hasta, Value = valor, Unit = unidad, Source = "Norma",
        SourcePublicId = origen ?? Guid.NewGuid(), SourceSequence = Interlocked.Increment(ref _secuencia), SupersedesSourcePublicId = corrige,
        CentralRegisteredAt = conocidaEn, KnownAt = conocidaEn,
    };
}
