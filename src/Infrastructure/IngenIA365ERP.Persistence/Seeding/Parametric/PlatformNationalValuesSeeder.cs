using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// La semilla del registro central de valores nacionales (feature 013, T0055; DT §2.21; data-model parte 3 §12.3–§12.5): en la
/// base administrativa, Order 6. Siembra desde JSON incrustados (<c>Data/NationalValues/</c>) el catálogo de modalidades y los
/// tipos de organización en su versión inicial (<c>Verified = 0</c>, preguntas E4 y E10), el <c>SMMLV</c> y la <c>UVT</c> 2026
/// con su norma, los límites legales con su valor semilla «pendiente de validar» y los <c>FESTIVO</c> 2026–2028 calculados con
/// <see cref="FestivosLey51"/> (que dejó la semilla de Nómina, T93 regla 2).
///
/// <para>Idempotente por (<c>Code</c>, <c>Key</c>, <c>ValidFrom</c>) y por <c>Code</c> de documento, numerando con el contador
/// común; <b>nunca actualiza</b> una fila (son hechos). Los documentos <c>SarcAnexo1</c> (Anexo 1 del 2025-01-31) y
/// <c>ReglaDePerdidaEsperada</c> (CE 100 del 2026-05-12) entran desde la fase 3 de Cartera (T0204) con sus validadores, sin verificar
/// (pregunta E8: el dueño los coteja; mientras tanto la cooperativa ve la alerta <c>Cartera.NormativaPendienteDeValidar</c>). No hace nada hasta que la base administrativa tenga la migración
/// <see cref="MigracionQueCreaLasTablas"/>. Cada cooperativa recibe su copia por <see cref="NationalValuesReplicaSeeder"/>
/// (Order 88) y por la tarea <c>valores-nacionales.replicar</c>.</para>
/// </summary>
public sealed class PlatformNationalValuesSeeder : IDataSeeder
{
    public const string MigracionQueCreaLasTablas = "ValoresNacionales";

    public const string Recurso = "NationalValues.valores-nacionales.json";

    public int Order => 6;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Admin;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var admin = context.Admin!;
        if (!await TieneLaMigracionAsync(admin, context.Logger, ct)) return 0;
        return await AplicarAsync(admin, [new ValidadorDeCatalogoDeModalidades(), new ValidadorDeTiposDeOrganizacion(), .. Application.Lending.Risk.ValidadorDeDocumentoNormativoDeCartera.Todos], DateTime.UtcNow, context.Logger, ct);
    }

    internal static async Task<bool> TieneLaMigracionAsync(AdminDbContext admin, ILogger logger, CancellationToken ct)
    {
        var aplicadas = await admin.Database.GetAppliedMigrationsAsync(ct);
        if (aplicadas.Any(m => m.EndsWith("_" + MigracionQueCreaLasTablas, StringComparison.Ordinal))) return true;
        logger.LogInformation("[ValoresNacionales.SinTablas] La base administrativa no tiene todavía la migración {Migracion}: no se siembra.", MigracionQueCreaLasTablas);
        return false;
    }

    // ------------------------------------------------------------------------------------------------- catálogo --

    /// <summary>El JSON de la semilla.</summary>
    public sealed record Semilla(int Version, DateOnly ValidFrom, IReadOnlyList<DocumentoSembrado> Documents, IReadOnlyList<ValorSembrado> Values, FestivosSembrados Holidays);

    public sealed record DocumentoSembrado(string Kind, string Code, DateOnly ValidFrom, short SchemaVersion, bool Verified, string Source, string File);

    public sealed record ValorSembrado(string Code, string? Key, DateOnly ValidFrom, decimal? Value, string? Unit, string? Label, string Source);

    public sealed record FestivosSembrados(IReadOnlyList<int> Years, string Source);

    public static Semilla Catalogo() => RecursoJson.Leer<Semilla>(Recurso);

    /// <summary>Los valores escalares sembrados, festivos incluidos, en el orden en que se numeran.</summary>
    public static IReadOnlyList<ValorSembrado> Valores()
    {
        var semilla = Catalogo();
        return semilla.Values
            .Concat(semilla.Holidays.Years.SelectMany(año => FestivosLey51.DelAño(año)).Select(f =>
                new ValorSembrado(CodigosDeValorNacional.Festivo, f.Fecha.ToString("yyyy-MM-dd"), f.Fecha, null, null, f.Nombre, semilla.Holidays.Source)))
            .ToList();
    }

    /// <summary>El contenido de un documento sembrado.</summary>
    public static string Contenido(string archivo)
    {
        var ensamblado = typeof(PlatformNationalValuesSeeder).Assembly;
        var nombre = ensamblado.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(".NationalValues." + archivo, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException($"No existe el documento sembrado {archivo}; revise el EmbeddedResource de Persistence.");
        using var flujo = ensamblado.GetManifestResourceStream(nombre)!;
        using var lector = new StreamReader(flujo);
        return lector.ReadToEnd();
    }

    // ------------------------------------------------------------------------------------------------ aplicar --

    /// <summary>La semilla sobre cualquier contexto administrativo (probable con InMemory). Guarda una vez.</summary>
    public static async Task<int> AplicarAsync(
        IAdminDbContext db, IEnumerable<IValidadorDeDocumentoNormativo> validadores, DateTime ahora, ILogger logger, CancellationToken ct)
    {
        var semilla = Catalogo();
        var maestro = MaestroQueRegistra.Semilla;
        var insertadas = 0;

        var documentos = await db.PlatformRegulatoryDocuments.AsNoTracking().Select(d => new { d.Kind, d.Code }).ToListAsync(ct);
        foreach (var d in semilla.Documents)
        {
            if (documentos.Any(x => x.Kind == d.Kind && x.Code == d.Code)) continue;
            var r = await RegisterRegulatoryDocumentCommandHandler.RegistrarAsync(db, validadores,
                new RegisterRegulatoryDocumentCommand(d.Kind, d.Code, d.ValidFrom, null, d.SchemaVersion, Contenido(d.File), d.Verified, d.Source),
                maestro, ahora, ct, guardar: false);
            if (r.IsFailure)
                throw new InvalidOperationException($"La semilla central del documento {d.Kind} {d.Code} no es válida: {r.Error.Message} "
                    + JsonSerializer.Serialize((r.Error as Application.Common.Models.ErrorConDatos)?.Data));
            insertadas++;
        }

        var existentes = (await db.PlatformNationalValues.AsNoTracking().Select(v => new { v.Code, v.Key, v.ValidFrom }).ToListAsync(ct))
            .Select(v => (v.Code, v.Key, v.ValidFrom)).ToHashSet();
        foreach (var v in Valores())
        {
            var clave = v.Key ?? string.Empty;
            if (existentes.Contains((v.Code, clave, v.ValidFrom))) continue;
            // Feature 014 (T016): un código de una entrega posterior a la vigente espera, sin error, a que su entrega salga; la semilla
            // lo inserta en el primer arranque con esa entrega (es idempotente).
            if (Domain.Common.NationalValues.CodigosDeValorNacional.Buscar(v.Code) is { } definicion && !definicion.DisponibleEn())
            {
                logger.LogDebug("[ValoresNacionales.SemillaEnEspera] {Codigo} {Clave} {Desde}: llega con {Entrega}", v.Code, clave, v.ValidFrom,
                    definicion.DisponibleDesde);
                continue;
            }
            var validado = await ReglasDelRegistroCentral.ValidarAsync(db, v.Code, clave, v.ValidFrom, null, v.Value, v.Unit, ct);
            if (validado.IsFailure)
            {
                logger.LogError("[ValoresNacionales.SemillaInvalida] {Codigo} {Clave} {Desde}: {Mensaje}", v.Code, clave, v.ValidFrom, validado.Error.Message);
                continue;
            }
            db.PlatformNationalValues.Add(RegisterNationalValueCommandHandler.Nueva(validado.Value, null, v.Label, v.Source, null, null,
                await NumeracionCentral.TomarAsync(db, ct), ahora, maestro));
            existentes.Add((v.Code, clave, v.ValidFrom));
            insertadas++;
        }

        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }
}
