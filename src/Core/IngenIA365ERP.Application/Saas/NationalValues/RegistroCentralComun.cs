using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Saas.NationalValues;

/// <summary>
/// La numeración común de <c>ADM_NationalValues</c> y <c>ADM_RegulatoryDocuments</c> (feature 013, C44; data-model parte 3
/// §3.3). Toma el número del contador en el <b>mismo</b> guardado que inserta la fila (molde de <c>COR_IntegrationBatchCounters</c>):
/// si dos registros chocan, uno pierde por <c>RowVersion</c> o por <c>UK_…_Sequence</c> y lo repite <see cref="ConReintentoAsync"/>. (nuevo)
/// </summary>
public static class NumeracionCentral
{
    public const int MaximoDeIntentos = 5;

    /// <summary>Índices que delatan la carrera por el número.</summary>
    public static readonly IReadOnlyList<string> IndicesDeLaNumeracion =
        ["UK_ADM_NationalValues_Sequence", "UK_ADM_RegulatoryDocuments_Sequence"];

    /// <summary>La <c>Sequence</c> de la fila que se registra; crea el contador en el primer uso.</summary>
    public static async Task<long> TomarAsync(IAdminDbContext db, CancellationToken ct)
    {
        var contador = db.PlatformNationalValueCounters.Local.OrderBy(c => c.Id).FirstOrDefault()
                       ?? await db.PlatformNationalValueCounters.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (contador is null)
        {
            contador = new PlatformNationalValueCounter { CreatedBy = "system" };
            db.PlatformNationalValueCounters.Add(contador);
        }
        return contador.TomarSiguiente();
    }

    /// <summary>
    /// Corre <paramref name="registrar"/> (lee, valida, numera, agrega y guarda) y lo repite desde cero, hasta
    /// <see cref="MaximoDeIntentos"/> veces, si pierde la carrera por el contador.
    /// </summary>
    public static async Task<Result<T>> ConReintentoAsync<T>(IAdminDbContext db, Func<Task<Result<T>>> registrar, ILogger? logger, CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await registrar();
            }
            catch (DbUpdateException ex) when (intento < MaximoDeIntentos && EsCarrera(ex))
            {
                logger?.LogWarning(ex, "[ValoresNacionales.CarreraPorElNumero] Intento {Intento} de {Maximo}: se relee y se repite.", intento, MaximoDeIntentos);
                db.DescartarCambios();
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(20, 100) * intento), ct);
            }
        }
    }

    private static bool EsCarrera(DbUpdateException ex)
    {
        if (ex is DbUpdateConcurrencyException) return true;
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
            if (IndicesDeLaNumeracion.Any(i => actual.Message.Contains(i, StringComparison.OrdinalIgnoreCase)))
                return true;
        return false;
    }
}

/// <summary>
/// La forma canónica de un documento normativo y su huella (feature 013, T45; data-model parte 3 §3.2): el JSON sin espacios ni
/// comentarios, con las propiedades en el orden en que vienen, y el SHA-256 de sus bytes UTF-8 en hexadecimal minúscula. Dos
/// contenidos que sólo difieren en espacios dan la misma huella. (nuevo)
/// </summary>
public static class ContenidoCanonico
{
    private static readonly JsonDocumentOptions Lectura = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static readonly JsonWriterOptions Escritura = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>El JSON canónico; lanza <see cref="JsonException"/> si no es JSON (quien llama ya lo validó).</summary>
    public static string Canonizar(string contenido)
    {
        using var doc = JsonDocument.Parse(contenido, Lectura);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, Escritura)) doc.RootElement.WriteTo(w);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static string Sha256(string canonico) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));
}

/// <summary>El maestro que registra: su identidad central y el nombre que queda en la fila. (nuevo)</summary>
public sealed record MaestroQueRegistra(Guid CentralUserId, string Nombre)
{
    /// <summary>La semilla de la central (Order 6) registra a nombre del sistema.</summary>
    public static MaestroQueRegistra Semilla { get; } = new(Guid.Empty, "system:seed");

    /// <summary>
    /// El maestro de la petición, o el rechazo. La ruta ya lo exige (<c>RequireMasterAdminAttribute</c>); el comando lo
    /// comprueba otra vez porque es lo que firma la fila.
    /// </summary>
    public static Result<MaestroQueRegistra> De(ICurrentCentralUserContext usuario)
    {
        if (!usuario.IsAuthenticated || usuario.CentralUserId is null)
            return Result.Failure<MaestroQueRegistra>("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (!usuario.IsGlobalMasterAdmin)
            return Result.Failure<MaestroQueRegistra>("Saas.MasterOnly", "Sólo el administrador maestro registra valores nacionales.");
        var nombre = string.IsNullOrWhiteSpace(usuario.Email) ? usuario.CentralUserId.Value.ToString("N") : usuario.Email!;
        return Result.Success(new MaestroQueRegistra(usuario.CentralUserId.Value, nombre.Length > 150 ? nombre[..150] : nombre));
    }
}

/// <summary>
/// La auditoría de plataforma de los comandos <c>/api/saas/*</c> de valores nacionales (feature 013, T37; data-model parte 3 §3):
/// módulo <c>NationalValues</c>, sin cooperativa ni cadena (la réplica en cada cooperativa sí va encadenada). Nunca hace fallar
/// el registro. (nuevo)
/// </summary>
public static class AuditoriaDelRegistroCentral
{
    public const string Modulo = "NationalValues";
    public const string ValorRegistrado = "NationalValues.Value.Registered";
    public const string ValorCorregido = "NationalValues.Value.Corrected";
    public const string DocumentoRegistrado = "NationalValues.Document.Registered";
    public const string Importados = "NationalValues.Imported";

    public static async Task RegistrarAsync(
        IAuditAppendOnlyWriter? escritor, MaestroQueRegistra maestro, string accion, string entidad, Guid? publicId,
        object? datos, DateTime cuando, ILogger? logger, CancellationToken ct)
    {
        if (escritor is null) return;
        try
        {
            await escritor.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: maestro.CentralUserId.ToString("N"),
                UserName: maestro.Nombre,
                Action: accion,
                EntityType: entidad,
                EntityPublicId: publicId?.ToString("N"),
                Module: Modulo,
                OldValuesJson: null,
                NewValuesJson: datos is null ? null : JsonSerializer.Serialize(datos, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                ChangedFields: null,
                IpAddress: null, UserAgent: null, Endpoint: null, HttpMethod: null,
                HttpStatusCode: 201, DurationMs: null,
                OccurredAt: cuando), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "[ValoresNacionales.AuditoriaFallida] {Accion}", accion);
        }
    }
}

/// <summary>Un valor nacional validado y normalizado, listo para numerar. (nuevo)</summary>
public sealed record ValorValidado(string Codigo, string Clave, DateOnly ValidFrom, DateOnly? ValidTo, decimal? Valor, string? Unidad);

/// <summary>
/// Las reglas de un valor nacional del registro central (feature 013, T37, T38; data-model parte 3 §3.1, §12.3, §12.5), las
/// mismas para el alta, la corrección, la plantilla y la semilla: el catálogo de códigos (<see cref="CodigosDeValorNacional"/>),
/// la clave de <c>USURA</c> contra el catálogo de modalidades vigente, el <c>FESTIVO</c> con <c>ValidFrom = ValidTo = Key</c>,
/// la unidad del límite (<see cref="CatalogoDeLimitesLegales"/>) y el duplicado vigente. (nuevo)
/// </summary>
public static class ReglasDelRegistroCentral
{
    /// <summary>Valida y normaliza (código en mayúsculas, unidad como texto del enum, fin del festivo).</summary>
    public static async Task<Result<ValorValidado>> ValidarAsync(
        IAdminDbContext db, string? codigo, string? clave, DateOnly validFrom, DateOnly? validTo, decimal? valor, string? unidad, CancellationToken ct)
    {
        var code = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        var key = (clave ?? string.Empty).Trim();

        UnidadDeValorNacional? unidadLeida = null;
        if (!string.IsNullOrWhiteSpace(unidad))
        {
            if (!Enum.TryParse<UnidadDeValorNacional>(unidad.Trim(), ignoreCase: true, out var u) || !Enum.IsDefined(u) || int.TryParse(unidad, out _))
                return Result.Failure<ValorValidado>(ErroresDeValoresNacionales.UnidadInvalida($"«{unidad}» no es una unidad de valor nacional."));
            unidadLeida = u;
        }

        // El festivo no lleva valor ni unidad: la plantilla de ejemplo trae «1 Dias» y se acepta como marca.
        if (code == CodigosDeValorNacional.Festivo)
        {
            valor = null;
            unidadLeida = null;
        }

        var rechazos = CodigosDeValorNacional.Validar(code, key, unidadLeida, valor);
        if (rechazos.Count > 0)
        {
            var r = rechazos[0];
            return Result.Failure<ValorValidado>(r.Motivo switch
            {
                MotivoDeRechazoDeValorNacional.CodigoDesconocido => ErroresDeValoresNacionales.CodigoDesconocido(codigo),
                MotivoDeRechazoDeValorNacional.CodigoNoDisponible => ErroresDeValoresNacionales.CodigoNoDisponible(code),
                MotivoDeRechazoDeValorNacional.ClaveInvalida => ErroresDeValoresNacionales.ClaveInvalida(code, r.Mensaje,
                    code == CodigosDeValorNacional.Limite ? CatalogoDeLimitesLegales.Todos.Select(l => l.Codigo).ToList() : null),
                MotivoDeRechazoDeValorNacional.UnidadInvalida => ErroresDeValoresNacionales.UnidadInvalida(r.Mensaje),
                _ => ErroresDeValoresNacionales.ValorInvalido(r.Mensaje),
            });
        }

        if (code == CodigosDeValorNacional.Festivo)
        {
            var fecha = DateOnly.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (validFrom != fecha || (validTo is { } hasta && hasta != fecha))
                return Result.Failure<ValorValidado>(ErroresDeValoresNacionales.ClaveInvalida(code,
                    $"Un festivo rige sólo su fecha: vigente desde y hasta deben ser {fecha:yyyy-MM-dd}, la clave."));
            validTo = fecha;
        }

        if (validTo is { } fin && fin < validFrom)
            return Result.Failure<ValorValidado>(ErroresDeValoresNacionales.ValorInvalido(
                $"La vigencia termina ({fin:yyyy-MM-dd}) antes de empezar ({validFrom:yyyy-MM-dd})."));

        if (code == CodigosDeValorNacional.Usura)
        {
            var modalidades = await ClavesDeUsuraAsync(db, validFrom, ct);
            if (!modalidades.Contains(key, StringComparer.Ordinal))
                return Result.Failure<ValorValidado>(ErroresDeValoresNacionales.ClaveInvalida(code,
                    modalidades.Count == 0
                        ? $"No hay catálogo de modalidades vigente el {validFrom:yyyy-MM-dd}: regístrelo antes que la usura."
                        : $"«{key}» no es una modalidad certificada del catálogo vigente el {validFrom:yyyy-MM-dd}.",
                    modalidades));
        }

        return Result.Success(new ValorValidado(code, key, validFrom, validTo, valor, unidadLeida?.ToString()));
    }

    /// <summary>Las claves de usura (modalidades certificadas) del catálogo de modalidades vigente en la central a la fecha.</summary>
    public static async Task<IReadOnlyList<string>> ClavesDeUsuraAsync(IAdminDbContext db, DateOnly fecha, CancellationToken ct)
    {
        var clase = nameof(RegulatoryDocumentKind.CatalogoDeModalidades);
        var versiones = await db.PlatformRegulatoryDocuments.AsNoTracking()
            .Where(d => d.Kind == clase && d.ValidFrom <= fecha)
            .ToListAsync(ct);
        versiones.AddRange(db.PlatformRegulatoryDocuments.Local.Where(d => d.Kind == clase && d.ValidFrom <= fecha && versiones.All(v => v.PublicId != d.PublicId)));
        var vigente = VigenciaBitemporal.Vigente(versiones, fecha, DateTime.MaxValue);
        if (vigente is null) return [];
        var leido = CatalogoDeModalidades.Leer(vigente.Content, vigente.SchemaVersion);
        return leido.EsValido ? leido.Documento!.ClavesDeUsura : [];
    }

    /// <summary>
    /// La fila vigente sin reemplazar con el mismo código, clave y vigencia desde (para <c>NationalValues.Value.AlreadyRegistered</c>
    /// y la plantilla), o nula.
    /// </summary>
    public static async Task<PlatformNationalValue?> VigenteConLaMismaLlaveAsync(IAdminDbContext db, string codigo, string clave, DateOnly desde, CancellationToken ct)
    {
        var mismas = await db.PlatformNationalValues.AsNoTracking()
            .Where(v => v.Code == codigo && v.Key == clave && v.ValidFrom == desde)
            .ToListAsync(ct);
        mismas.AddRange(db.PlatformNationalValues.Local.Where(v => v.Code == codigo && v.Key == clave && v.ValidFrom == desde && mismas.All(m => m.PublicId != v.PublicId)));
        if (mismas.Count == 0) return null;
        var ids = mismas.Select(m => m.PublicId).ToList();
        var reemplazadas = (await db.PlatformNationalValues.AsNoTracking()
                .Where(v => v.SupersedesPublicId != null && ids.Contains(v.SupersedesPublicId.Value))
                .Select(v => v.SupersedesPublicId!.Value)
                .ToListAsync(ct))
            .Concat(db.PlatformNationalValues.Local.Where(v => v.SupersedesPublicId is not null).Select(v => v.SupersedesPublicId!.Value))
            .ToHashSet();
        return mismas.Where(m => !reemplazadas.Contains(m.PublicId)).OrderByDescending(m => m.Sequence).FirstOrDefault();
    }

    /// <summary>La corrección que ya reemplazó a una fila, o nula.</summary>
    public static async Task<Guid?> ReemplazadaPorAsync(IAdminDbContext db, Guid publicId, CancellationToken ct)
    {
        var local = db.PlatformNationalValues.Local.FirstOrDefault(v => v.SupersedesPublicId == publicId)?.PublicId;
        if (local is not null) return local;
        return await db.PlatformNationalValues.AsNoTracking()
            .Where(v => v.SupersedesPublicId == publicId)
            .Select(v => (Guid?)v.PublicId)
            .FirstOrDefaultAsync(ct);
    }
}
