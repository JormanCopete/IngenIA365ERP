using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Columnas;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>
/// Importa un archivo de una parte de la carga de migración de aportes o captaciones (feature 014, US9, T152, T153; plantillas-y-archivos.md §1.1 y §4;
/// api.md §16, <c>POST /api/lending/deposits/migration/loads/{id}/parts/{part}/import?mode=review|apply[&amp;replace=true]</c>,
/// <c>Lending.Deposits.Migration</c>). (nuevo)
/// </summary>
public sealed record ImportDepositMigrationPartCommand(
    Guid MigrationLoadPublicId,
    string Part,
    ModoDeImportacion? Mode,
    ArchivoDeImportacion File,
    bool Replace = false,
    string Reason = "") : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportDepositMigrationPartCommandValidator : AbstractValidator<ImportDepositMigrationPartCommand>
{
    public ImportDepositMigrationPartCommandValidator()
    {
        RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
        RuleFor(x => x.Part).NotEmpty().MaximumLength(40);
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportDepositMigrationPartCommand>.LargoMaximo);
    }
}

/// <summary>
/// Una tanda por archivo, con la mecánica de la 013 (C64): <see cref="EjecutorDeImportacion"/> lee la hoja <c>Datos</c> (o el <c>.csv</c>) con la
/// definición de la parte (<see cref="DefinicionesDeMigracionDeCaptaciones"/>), y esta plantilla:
/// <list type="bullet">
/// <item>exige una carga en borrador de aportes o captaciones (las de créditos tienen su propia ruta: <c>Lending.MigrationLoad.NotFound</c>), con la
/// naturaleza sin activar, una parte de su naturaleza (<c>Lending.Migration.PartNotForNature</c>) y construida y desplegada
/// (<c>Lending.Migration.PartNotAvailable</c>);</item>
/// <item>carga en bloque las personas vivas por documento (con su afiliación al corte), las sucursales vinculadas a una oficina, las líneas de la
/// naturaleza con su versión al corte, las equivalencias de las tandas vivas, los números de cuenta de la carga y los de la Cartera: nunca una consulta
/// por fila;</item>
/// <item>valida cada fila: formato y obligatoriedad de §4, el maestro (<c>Import.Cell.NotFound</c>; <b>ninguna plantilla crea personas</b>), el alcance
/// de oficina y línea de quien importa (fuera, el mismo <c>Import.Cell.NotFound</c>), la línea por su código o por el código heredado
/// (<c>Lending.Migration.LineEquivalenceMissing</c>), la clase de la línea, el asociado vigente al corte (<c>Lending.Savings.MembersOnly</c>), el número
/// que no se repite en la carga ni en la Cartera (<c>Lending.Migration.AccountNumberDuplicate</c>), lo que llega con una entrega posterior
/// (<c>Import.Cell.NotYetAvailable</c>) y como aviso las referencias a cuentas que la carga todavía no trae;</item>
/// <item>la llave de la parte es única en el archivo y entre todas las tandas vivas de la parte (<c>Import.Row.Duplicate</c> nombrando la tanda); el mismo
/// archivo a la misma parte responde «sin cambio»; <c>replace=true</c> pasa las tandas vivas de la parte a <c>Superseded</c>.</item>
/// </list>
/// <c>review</c> no guarda; <c>apply</c> es todo o nada para el archivo: una fila de <c>LND_MigrationLoadParts</c> y sus filas de preparación, con la
/// intervención <c>CargarMigracion</c> sobre la carga. La auditoría <c>Import.Reviewed</c>/<c>.Applied</c> con el SHA-256 la escribe el ejecutor.
/// </summary>
public sealed class ImportDepositMigrationPartCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    RegistroDeIntervenciones intervenciones,
    IAlcanceDeCartera alcanceDeLaPeticion,
    RequisitosDeActivacionDeCaptaciones requisitos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ImportDepositMigrationPartCommand, Result<ImportResultDto>>
{
    public const string CodigoCuentaPendiente = "Lending.Migration.AccountNotInLoad";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ImportResultDto>> Handle(ImportDepositMigrationPartCommand request, CancellationToken ct)
    {
        if (DefinicionesDeMigracionDeCaptaciones.Buscar(request.Part) is not { } parte)
            return Result.Failure<ImportResultDto>(ErroresDeGobierno.PlantillaInexistente());
        var carga = await ConsultasDeCargasDeCaptaciones.BuscarAsync(db, request.MigrationLoadPublicId, seguimiento: false, ct);
        if (carga is null) return Result.Failure<ImportResultDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
        if (!parte.Admite(carga.Nature)) return Result.Failure<ImportResultDto>(ErroresDeMigracionDeCaptaciones.ParteDeOtraNaturaleza(parte.Parte, carga.Nature));
        if (!parte.Disponible(requisitos.Entrega) || parte.Definicion is not { } definicion)
            return Result.Failure<ImportResultDto>(ErroresDeMigracionDeCaptaciones.ParteNoDisponible(parte));
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        if (fechas.De(carga.Nature) is { } activada) return Result.Failure<ImportResultDto>(ErroresDeActivacionDeCaptaciones.YaActivada(carga.Nature, activada));
        if (!carga.EsBorrador) return Result.Failure<ImportResultDto>(ErroresDeActivacion.CargaNoBorrador());

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ImportResultDto>(ErroresDeActivacion.SinUsuario());

        IntervencionVerificada? intervencion = null;
        if (request.Mode == ModoDeImportacion.Apply)
        {
            var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(AsuntoDeDiferenciasDeActivacion.ClaseDeExpediente, carga.PublicId),
                AccionesDeSegregacion.CargarMigracion, reloj.HoyLocal, ct);
            if (verificada.IsFailure) return Result.Failure<ImportResultDto>(verificada.Error);
            intervencion = verificada.Value;
        }

        var tandas = await db.MigrationLoadParts.Where(p => p.MigrationLoadId == carga.Id && p.PartKey == parte.Parte && !p.IsDeleted)
            .OrderBy(p => p.BatchNumber).ToListAsync(ct);
        var vivas = tandas.Where(t => t.Viva).ToList();
        var huella = request.File?.Sha256 ?? string.Empty;
        var sinCambio = !request.Replace && vivas.Any(t => string.Equals(t.FileSha256, huella, StringComparison.OrdinalIgnoreCase));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var lectura = new LecturaDeParteDeCaptaciones(db, parte, carga, request.Replace ? [] : vivas, alcance, ct);
        var pendientes = new List<Action<int>>();
        MigrationLoadPart? nueva = null;

        return await ejecutor.EjecutarAsync(definicion, request, async (ctx, c) =>
        {
            if (sinCambio)
            {
                foreach (var fila in ctx.Datos.Filas) ctx.Registrar(fila, fila.Numero.ToString(CultureInfo.InvariantCulture), AccionDeImportacion.Unchanged);
                ctx.Extra["unchanged"] = true;
                return;
            }

            await lectura.CargarAsync(ctx);
            foreach (var fila in ctx.Datos.Filas) lectura.Leer(ctx, fila, pendientes);
            lectura.Avisar();
            ctx.Extra["part"] = parte.Parte;
            ctx.Extra["batch"] = (request.Replace ? 0 : tandas.Count) + 1;

            if (ctx.Modo != ModoDeImportacion.Apply || ctx.HayErrores) return;
            if (request.Replace)
                foreach (var t in vivas) t.Reemplazar();
            nueva = MigrationLoadPart.Nueva(carga.Id, parte.Parte, tandas.Count, request.File!.NombreArchivo, huella, ctx.Datos.Filas.Count, usuario,
                reloj.UtcNow, ctx.Extra.TryGetValue("warnings", out var avisos) ? JsonSerializer.Serialize(avisos, Json) : null);
            db.MigrationLoadParts.Add(nueva);
            if (intervencion is not null) intervenciones.Registrar(intervencion, AsuntoDeDiferenciasDeActivacion.ClaseDeExpediente, carga.PublicId);
        }, ct, despuesDeGuardar: (_, _) =>
        {
            if (nueva is not null)
                foreach (var agregar in pendientes) agregar(nueva.Id);
            return Task.CompletedTask;
        });
    }
}

/// <summary>Lo que una parte necesita para leer sus filas: los catálogos en bloque y las llaves de las tandas vivas. (nuevo)</summary>
internal sealed class LecturaDeParteDeCaptaciones(
    IApplicationDbContext db,
    ParteDeMigracionDeCaptaciones parte,
    MigrationLoad carga,
    IReadOnlyList<MigrationLoadPart> vivas,
    AlcanceDeCartera alcance,
    CancellationToken ct)
{
    private static readonly Regex PatronDeNumero = new("^[A-Za-z0-9-]+$", RegexOptions.Compiled);
    private static readonly Regex PatronDePeriodo = new(@"^(\d{4})-(\d{2})(?:-[Qq]?(\d))?$", RegexOptions.Compiled);

    private sealed record PersonaLeida(int Id, bool AsociadaAlCorte, bool Fallecida);

    private Dictionary<string, PersonaLeida> _personas = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Id, Guid PublicId, bool Vinculada)> _sucursales = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, LineaAlCorte> _lineas = new Dictionary<string, LineaAlCorte>();
    private Dictionary<string, int> _equivalencias = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<int, LineaAlCorte> _lineasPorId = [];
    private readonly Dictionary<string, int> _llavesPrevias = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _tandaDe = vivas.ToDictionary(v => v.Id, v => v.BatchNumber);
    private HashSet<string> _numerosDeLaCarga = new(StringComparer.Ordinal);
    private HashSet<string> _numerosEnCartera = new(StringComparer.Ordinal);
    private HashSet<string> _numerosDeOtraParte = new(StringComparer.Ordinal);
    private HashSet<int> _conAportesVivos = [];
    private readonly HashSet<string> _numerosDelArchivo = new(StringComparer.Ordinal);
    private readonly List<(FilaDeImportacion Fila, string Columna, string Numero)> _referencias = [];

    private DateOnly Corte => carga.CutoffDate;

    private bool EsDeAportes => carga.Nature == LineNature.Contribution;

    public async Task CargarAsync(ContextoDeImportacion ctx)
    {
        var definicion = parte.Definicion!.Hojas[0];
        var columnasDePersona = definicion.Columnas.Where(c => c.Tipo == TipoDeValor.Persona).Select(c => c.Nombre).ToList();
        var documentos = ctx.Datos.Filas.SelectMany(f => columnasDePersona.Select(f.Crudo)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (documentos.Count > 0)
        {
            var personas = await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId) && !p.IsDeleted)
                .Select(p => new { p.TaxId, p.Id, p.IsAssociate, p.IsDeceased }).ToListAsync(ct);
            var asociadas = personas.Where(p => p.IsAssociate).Select(p => p.Id).ToList();
            var afiliaciones = (await db.Associates.AsNoTracking().Where(a => asociadas.Contains(a.PersonId) && !a.IsDeleted)
                    .Select(a => new { a.PersonId, a.Status, a.WithdrawalDate, a.RejoinDate, a.JoinDate }).ToListAsync(ct))
                .GroupBy(a => a.PersonId).ToDictionary(g => g.Key, g => g.First());
            _personas = personas.GroupBy(p => p.TaxId!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g =>
            {
                var p = g.First();
                var vigente = p.IsAssociate && afiliaciones.TryGetValue(p.Id, out var a) && (a.JoinDate is null || a.JoinDate <= Corte)
                              && !PersonaDeOriginacion.Retirado(a.Status, a.WithdrawalDate, a.RejoinDate, Corte);
                return new PersonaLeida(p.Id, vigente, p.IsDeceased);
            }, StringComparer.OrdinalIgnoreCase);
        }

        if (definicion.Columnas.Any(c => c.Tipo == TipoDeValor.Sucursal))
            foreach (var b in await db.Branches.AsNoTracking().Where(b => !b.IsDeleted)
                         .Select(b => new { b.Id, b.PublicId, b.LegacyCode, b.Name, b.TenantBranchPublicId }).ToListAsync(ct))
            {
                var valor = (b.Id, b.PublicId, b.TenantBranchPublicId is not null);
                if (!string.IsNullOrWhiteSpace(b.LegacyCode)) _sucursales.TryAdd(b.LegacyCode.Trim(), valor);
                if (!string.IsNullOrWhiteSpace(b.Name)) _sucursales.TryAdd(b.Name.Trim(), valor);
            }

        if (definicion.Columna(C.Linea) is not null)
        {
            _lineas = await LineasDeLaCarga.LeerAsync(db, carga.Nature, Corte, null, ct);
            _lineasPorId = _lineas.Values.ToDictionary(l => l.Id);
        }
        if (definicion.Columna(C.CodigoHeredado) is not null && parte.Parte != P.LineEquivalences)
        {
            var tandas = ConsultasDeCargasDeCaptaciones.PartesVivas(db, carga.Id, P.LineEquivalences);
            _equivalencias = (await db.MigrationLineEquivalenceRows.AsNoTracking().Where(r => tandas.Contains(r.MigrationLoadPartId))
                    .Select(r => new { r.SourceProductCode, r.CreditLineId }).ToListAsync(ct))
                .GroupBy(r => r.SourceProductCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().CreditLineId,
                    StringComparer.OrdinalIgnoreCase);
        }

        var cuentas = ConsultasDeCargasDeCaptaciones.PartesVivas(db, carga.Id, P.MemberAccounts);
        var aportes = ConsultasDeCargasDeCaptaciones.PartesVivas(db, carga.Id, P.ContributionAccounts);
        var cdat = ConsultasDeCargasDeCaptaciones.PartesVivas(db, carga.Id, P.TermDeposits);
        var deAhorro = await db.MigrationAccountRows.AsNoTracking().Where(r => cuentas.Contains(r.MigrationLoadPartId)).Select(r => r.SourceAccountNumber)
            .ToListAsync(ct);
        // US6 (pendiente de la fase 10): los CDAT de la carga también tienen número (los citan las partes de la cuenta).
        var deCdat = await db.MigrationTermDepositRows.AsNoTracking().Where(r => cdat.Contains(r.MigrationLoadPartId)).Select(r => r.SourceAccountNumber)
            .ToListAsync(ct);
        var deAportes = await db.MigrationContributionAccountRows.AsNoTracking()
            .Where(r => aportes.Contains(r.MigrationLoadPartId) && r.SourceAccountNumber != null).Select(r => r.SourceAccountNumber!).ToListAsync(ct);
        _numerosDeLaCarga = deAhorro.Concat(deAportes).Concat(deCdat).ToHashSet(StringComparer.Ordinal);
        // Un número no se repite entre las partes de cuentas; dentro de la misma parte lo cuida la llave (y con «reemplazar» no cuentan sus tandas).
        _numerosDeOtraParte = (parte.Parte switch
            {
                P.MemberAccounts => deAportes.Concat(deCdat),
                P.ContributionAccounts => deAhorro.Concat(deCdat),
                P.TermDeposits => deAhorro.Concat(deAportes),
                _ => [],
            })
            .ToHashSet(StringComparer.Ordinal);
        if (parte.Parte is P.MemberAccounts or P.ContributionAccounts or P.TermDeposits)
        {
            var numeros = ctx.Datos.Filas.Select(f => f.Crudo(C.Numero)?.ToUpperInvariant()).OfType<string>().Distinct().ToList();
            _numerosEnCartera = (await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => numeros.Contains(a.Number)).Select(a => a.Number)
                    .ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);
        }
        if (parte.Parte == P.ContributionAccounts)
        {
            var ids = _personas.Values.Select(p => p.Id).ToList();
            _conAportesVivos = (await db.MemberAccounts.AsNoTracking()
                    .Where(a => ids.Contains(a.HolderPersonId) && a.Nature == LineNature.Contribution && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
                    .Select(a => a.HolderPersonId).ToListAsync(ct))
                .ToHashSet();
        }
        await LlavesPreviasAsync();
    }

    /// <summary>Las llaves de las tandas vivas de la parte (sin ellas con <c>replace=true</c>), para que una llave no se repita entre tandas.</summary>
    private async Task LlavesPreviasAsync()
    {
        if (vivas.Count == 0) return;
        var ids = vivas.Select(v => v.Id).ToList();
        IEnumerable<(int Parte, string Llave)> llaves = parte.Parte switch
        {
            P.LineEquivalences => (await db.MigrationLineEquivalenceRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                .Select(r => (r.MigrationLoadPartId, Llave(r.SourceProductCode.ToUpperInvariant()))),
            P.MemberAccounts => (await db.MigrationAccountRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                .Select(r => (r.MigrationLoadPartId, Llave(r.SourceAccountNumber))),
            P.ContributionAccounts => (await db.MigrationContributionAccountRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                .Select(r => (r.MigrationLoadPartId, Llave(r.PersonId))),
            P.MemberAccountParties => (await db.MigrationAccountPartyRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                .Select(r => (r.MigrationLoadPartId, Llave(r.SourceAccountNumber, r.PersonId, r.Role))),
            P.AccountInstallments => (await db.MigrationAccountInstallmentRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                .Select(r => (r.MigrationLoadPartId, Llave(r.SourceAccountNumber, r.PeriodYear, r.PeriodMonth, r.SubPeriodNumber))),
            P.GmfExemptions => (await db.MigrationGmfExemptionRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                .Select(r => (r.MigrationLoadPartId, Llave(r.PersonId))),
            P.TaxYearToDate => (await db.MigrationTaxYearRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                .Select(r => (r.MigrationLoadPartId, Llave(r.PersonId, r.SourceAccountNumber, r.TaxYear, r.Concept))),
            P.TermDeposits => (await db.MigrationTermDepositRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                .Select(r => (r.MigrationLoadPartId, Llave(r.SourceAccountNumber))),
            P.HoldsAndGarnishments => (await db.MigrationHoldRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                .Select(r => (r.MigrationLoadPartId, Llave(r.SourceAccountNumber, r.Kind, r.Reference.ToUpperInvariant()))),
            _ => [],
        };
        foreach (var (id, llave) in llaves) _llavesPrevias.TryAdd(llave, _tandaDe[id]);
    }

    private static string Llave(params object?[] partes) =>
        string.Join('|', partes.Select(p => p switch
        {
            null => string.Empty,
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Enum e => e.ToString(),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => p.ToString()!,
        }));

    private bool LlaveNueva(ContextoDeImportacion ctx, FilaDeImportacion fila, string llave, string columna)
    {
        if (!ctx.Datos.LlaveUnica(fila, llave, columna)) return false;
        if (!_llavesPrevias.TryGetValue(llave, out var tanda)) return true;
        fila.Error(columna, ImportErrors.RowDuplicate,
            string.Create(CultureInfo.InvariantCulture, $"Esta fila ya viene en la tanda {tanda} de la parte {parte.Parte}. Use reemplazar la parte para corregirla."));
        return false;
    }

    // -------------------------------------------------------------------------------------------------- celdas --

    private string? NumeroDeCuenta(FilaDeImportacion fila, string columna)
    {
        var texto = fila.Texto(columna, 20);
        if (texto is null) return null;
        if (PatronDeNumero.IsMatch(texto)) return texto.ToUpperInvariant();
        fila.Error(columna, ImportErrors.CellFormat, "El número de cuenta admite letras, dígitos y guion, hasta 20.");
        return null;
    }

    /// <summary>Una cuenta que otra parte de la carga trae: si la carga todavía no la tiene, aviso (al aplicar es error).</summary>
    private string? Referencia(FilaDeImportacion fila, string columna)
    {
        var numero = NumeroDeCuenta(fila, columna);
        if (numero is not null) _referencias.Add((fila, columna, numero));
        return numero;
    }

    private PersonaLeida? Persona(FilaDeImportacion fila, string columna)
    {
        var documento = fila.Texto(columna, 20);
        if (documento is null) return null;
        if (_personas.TryGetValue(documento, out var p)) return p;
        fila.Error(columna, ImportErrors.CellNotFound,
            $"No hay una persona viva con documento «{documento}». Créela en Maestros › Personas: esta plantilla no crea personas.");
        return null;
    }

    private Guid? Sucursal(FilaDeImportacion fila, string columna)
    {
        var codigo = fila.Texto(columna, 120);
        if (codigo is null) return null;
        if (_sucursales.TryGetValue(codigo, out var s) && s.Vinculada && alcance.IncluyeOficina(s.Id)) return s.PublicId;
        fila.Error(columna, ImportErrors.CellNotFound,
            $"No hay una sucursal «{codigo}» vinculada a una oficina en su alcance. Vincúlela en Maestros › Agencias o pida el alcance.");
        return null;
    }

    /// <summary>La línea de la cuenta: por <c>linea</c> o por <c>codigoHeredado</c> (con su equivalencia en la carga), con versión al corte y en el alcance.</summary>
    private (LineaAlCorte Linea, string? Heredado)? LineaDeLaCuenta(FilaDeImportacion fila)
    {
        var codigo = fila.Codigo(C.Linea, 10);
        var heredado = fila.Texto(C.CodigoHeredado, 20)?.ToUpperInvariant();
        LineaAlCorte? porEquivalencia = null;
        if (heredado is not null)
        {
            if (!_equivalencias.TryGetValue(heredado, out var id) || !_lineasPorId.TryGetValue(id, out porEquivalencia))
            {
                fila.Error(C.CodigoHeredado, ErroresDeMigracionDeCaptaciones.CodigoSinEquivalencia,
                    $"El producto heredado «{heredado}» no tiene equivalencia en la carga: cárguelo antes en la parte line-equivalences.");
                return null;
            }
        }
        if (codigo is null && porEquivalencia is null)
        {
            if (heredado is null) fila.Error(C.Linea, ImportErrors.CellRequired, "Indique la línea o el código heredado de la cuenta.");
            return null;
        }
        LineaAlCorte? linea = porEquivalencia;
        if (codigo is not null)
        {
            if (!_lineas.TryGetValue(codigo, out var directa))
            {
                fila.Error(C.Linea, ImportErrors.CellNotFound,
                    $"No hay una línea de {ErroresDeMigracionDeCaptaciones.Nombre(carga.Nature)} «{codigo}». Créela en Cartera › Líneas.");
                return null;
            }
            if (porEquivalencia is not null && porEquivalencia.Id != directa.Id)
            {
                fila.Error(C.Linea, ImportErrors.CellFormat,
                    $"La línea «{codigo}» no es la que la equivalencia del código heredado «{heredado}» nombra ({porEquivalencia.Code}).");
                return null;
            }
            linea = directa;
        }
        var columna = codigo is not null ? C.Linea : C.CodigoHeredado;
        if (!linea!.TieneVersion || !alcance.IncluyeLinea(linea.Id))
        {
            fila.Error(columna, ImportErrors.CellNotFound,
                string.Create(CultureInfo.InvariantCulture,
                    $"La línea «{linea.Code}» no tiene versión vigente al corte ({Corte:yyyy-MM-dd}) o no está en su alcance. Versiónela en Cartera › Líneas."));
            return null;
        }
        return (linea, heredado);
    }

    private static T? Enumeracion<T>(FilaDeImportacion fila, string columna, IReadOnlyDictionary<string, T>? etiquetas = null) where T : struct, Enum
    {
        if (fila.Crudo(columna) is { } crudo && int.TryParse(crudo, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            && Enum.IsDefined(typeof(T), Enum.ToObject(typeof(T), n)))
            return (T)Enum.ToObject(typeof(T), n);
        return fila.Enumeracion(columna, etiquetas);
    }

    private static decimal NoNegativo(FilaDeImportacion fila, string columna, decimal? valor, bool positivo = false)
    {
        if (valor is null) return 0m;
        if (positivo ? valor > 0m : valor >= 0m) return valor.Value;
        fila.Error(columna, ImportErrors.CellFormat, positivo ? "Debe ser mayor que cero." : "No puede ser negativo.");
        return 0m;
    }

    private void HastaElCorte(FilaDeImportacion fila, string columna, DateOnly? fecha)
    {
        if (fecha > Corte)
            fila.Error(columna, ImportErrors.CellFormat,
                string.Create(CultureInfo.InvariantCulture, $"Debe ser anterior o igual al corte de la carga ({Corte:yyyy-MM-dd})."));
    }

    /// <summary>Una columna que existe en la plantilla pero llega con una entrega posterior: con valor, <c>Import.Cell.NotYetAvailable</c>.</summary>
    private static void NoTodavia(FilaDeImportacion fila, string entrega, params string[] columnas)
    {
        foreach (var columna in columnas)
            if (fila.Crudo(columna) is not null) fila.TodaviaNoDisponible(columna, entrega);
    }

    private void NumeroNuevo(FilaDeImportacion fila, string numero)
    {
        if (_numerosEnCartera.Contains(numero))
            fila.Error(C.Numero, ErroresDeMigracionDeCaptaciones.CodigoNumeroDuplicado, $"Ya hay una cuenta de la Cartera con el número {numero}.");
        else if (_numerosDeOtraParte.Contains(numero))
            fila.Error(C.Numero, ErroresDeMigracionDeCaptaciones.CodigoNumeroDuplicado,
                $"El número {numero} ya lo tiene otra cuenta de la carga (en otra parte de cuentas: ahorro, aportes o CDAT).");
    }

    // ---------------------------------------------------------------------------------------------------- filas --

    public void Leer(ContextoDeImportacion ctx, FilaDeImportacion fila, List<Action<int>> pendientes)
    {
        Action<int>? agregar = parte.Parte switch
        {
            P.LineEquivalences => Equivalencia(ctx, fila),
            P.MemberAccounts => CuentaDeCaptacion(ctx, fila),
            P.ContributionAccounts => CuentaDeAportes(ctx, fila),
            P.MemberAccountParties => ParteDeLaCuenta(ctx, fila),
            P.AccountInstallments => Cuota(ctx, fila),
            P.GmfExemptions => Exencion(ctx, fila),
            P.TaxYearToDate => Tributario(ctx, fila),
            P.TermDeposits => Cdat(ctx, fila),
            P.HoldsAndGarnishments => BloqueoOEmbargo(ctx, fila),
            _ => null,
        };
        if (agregar is not null && !fila.TieneErrores) pendientes.Add(agregar);
    }

    private static Action<int>? Registrar(ContextoDeImportacion ctx, FilaDeImportacion fila, string llave, Action<int> agregar)
    {
        if (fila.TieneErrores) return null;
        ctx.Registrar(fila, llave, AccionDeImportacion.Create);
        return agregar;
    }

    private Action<int>? Equivalencia(ContextoDeImportacion ctx, FilaDeImportacion fila)
    {
        var heredado = fila.Texto(C.CodigoHeredado, 20)?.ToUpperInvariant();
        var descripcion = fila.Texto(C.DescripcionHeredada, 120);
        var codigo = fila.Codigo(C.Linea, 10);
        var clase = Enumeracion<DepositClass>(fila, C.Clase);
        LineaAlCorte? linea = null;
        if (codigo is not null)
        {
            if (!_lineas.TryGetValue(codigo, out linea) || !linea.TieneVersion || !alcance.IncluyeLinea(linea.Id))
            {
                fila.Error(C.Linea, ImportErrors.CellNotFound,
                    string.Create(CultureInfo.InvariantCulture,
                        $"No hay una línea de {ErroresDeMigracionDeCaptaciones.Nombre(carga.Nature)} «{codigo}» con versión vigente al corte ({Corte:yyyy-MM-dd}) en su alcance."));
                linea = null;
            }
        }
        if (EsDeAportes && clase is not null)
            fila.Error(C.Clase, ImportErrors.CellFormat, "Una línea de aportes no tiene clase de captación: deje la columna vacía.");
        if (!EsDeAportes)
        {
            fila.Requerido(C.Clase);
            if (linea is not null && clase is not null && linea.Clase != clase)
                fila.Error(C.Clase, ImportErrors.CellFormat, $"La línea {linea.Code} es de la clase {linea.Clase}, no {clase}.");
        }
        if (heredado is null || linea is null) return null;
        if (!LlaveNueva(ctx, fila, Llave(heredado), C.CodigoHeredado)) return null;
        var l = linea;
        return Registrar(ctx, fila, heredado, id => db.MigrationLineEquivalenceRows.Add(new MigrationLineEquivalenceRow
        {
            MigrationLoadPartId = id, RowNumber = fila.Numero, SourceProductCode = heredado, SourceDescription = descripcion, CreditLineCode = l.Code,
            CreditLineId = l.Id, Nature = carga.Nature, DepositClass = l.Clase,
        }));
    }

    private Action<int>? CuentaDeCaptacion(ContextoDeImportacion ctx, FilaDeImportacion fila)
    {
        var numero = NumeroDeCuenta(fila, C.Numero);
        var titular = Persona(fila, C.Titular);
        var oficina = Sucursal(fila, C.Oficina);
        var linea = LineaDeLaCuenta(fila);
        var clase = Enumeracion<DepositClass>(fila, C.Clase);
        if (clase == DepositClass.TermDeposit)
            fila.Error(C.Clase, ImportErrors.CellNotYetAvailable, "Un CDAT se migra en la parte term-deposits, que llega con el CDAT (K9).");
        else if (clase is not null && linea is { } l0 && l0.Linea.Clase != clase)
            fila.Error(C.Clase, ImportErrors.CellFormat, $"La línea {l0.Linea.Code} es de la clase {l0.Linea.Clase}, no {clase}.");
        var apertura = fila.Fecha(C.FechaDeApertura);
        HastaElCorte(fila, C.FechaDeApertura, apertura);
        var estado = Enumeracion<MemberAccountStatus>(fila, C.Estado);
        if (estado is not (null or MemberAccountStatus.Active or MemberAccountStatus.Inactive))
            fila.Error(C.Estado, ImportErrors.CellFormat, "Admite Active o Inactive.");
        var ultimoMovimiento = fila.Fecha(C.FechaUltimoMovimientoDelTitular);
        HastaElCorte(fila, C.FechaUltimoMovimientoDelTitular, ultimoMovimiento);
        if (estado == MemberAccountStatus.Inactive) fila.Requerido(C.FechaUltimoMovimientoDelTitular);
        var manejo = Enumeracion<HandlingMode>(fila, C.Manejo) ?? HandlingMode.Indistinct;
        var capital = NoNegativo(fila, C.SaldoCapital, fila.Monto(C.SaldoCapital));
        var interes = NoNegativo(fila, C.InteresCausadoNoLiquidado, fila.Costo(C.InteresCausadoNoLiquidado));
        var liquidadoHasta = fila.Fecha(C.UltimaLiquidacionHasta);
        HastaElCorte(fila, C.UltimaLiquidacionHasta, liquidadoHasta);
        if (interes > 0m) fila.Requerido(C.UltimaLiquidacionHasta);
        if (liquidadoHasta is { } lq && apertura is { } ap && lq < ap)
            fila.Error(C.UltimaLiquidacionHasta, ImportErrors.CellFormat, "La última liquidación no puede ser anterior a la apertura de la cuenta.");
        var cargos = NoNegativo(fila, C.CargosPendientes, fila.Monto(C.CargosPendientes));
        // US7 (T182; research §19): el contrato del ahorro contractual; las demás clases no llevan sus columnas. La tasa negociada y la pactada de la vista
        // siguen sin entrega.
        var contrato = clase == DepositClass.Contractual ? ContratoMigrado(fila, linea?.Linea, apertura) : null;
        if (clase != DepositClass.Contractual)
        {
            foreach (var columna in new[] { C.CuotaContractual, C.CuotasPagadas, C.CuotasAtrasadas, C.FechaDeVencimientoContractual, C.TasaBonificadaEA,
                         C.EstadoContractual })
                if (fila.Crudo(columna) is not null)
                    fila.Error(columna, ImportErrors.CellFormat, "Las condiciones del ahorro contractual sólo van en una cuenta de clase Contractual.");
            NoTodavia(fila, nameof(Domain.Lending.EntregaDeCartera.K9), C.TasaPactadaEA);
        }
        NoTodavia(fila, nameof(Domain.Lending.EntregaDeCartera.K9), C.TasaNegociadaMargenEA);
        var canje = NoNegativo(fila, C.CanjePendiente, fila.Monto(C.CanjePendiente));
        if (canje > capital) fila.Error(C.CanjePendiente, ImportErrors.CellFormat, "Lo que está en canje es parte del saldo de capital: no puede superarlo.");
        var soporte = fila.Texto(C.SoporteDeOrigenDeFondos, 200);

        if (titular is not null && linea is { } l1 && l1.Linea.DeCaptacion is { RequiresAssociate: true } && !titular.AsociadaAlCorte)
            fila.Error(C.Titular, "Lending.Savings.MembersOnly",
                $"La línea {l1.Linea.Code} exige asociado y el titular no estaba afiliado al corte.");
        if (titular is { Fallecida: true })
            fila.Aviso(C.Titular, "Lending.Savings.HolderDeceased", "El titular figura fallecido en el maestro: la cuenta se migra y espera su trámite de fallecimiento.");

        if (numero is null) return null;
        NumeroNuevo(fila, numero);
        if (!LlaveNueva(ctx, fila, Llave(numero), C.Numero)) return null;
        _numerosDelArchivo.Add(numero);
        if (titular is null || oficina is null || linea is null || clase is null || apertura is null || estado is null) return null;
        var (lineaDeLaCuenta, heredado) = linea.Value;
        return Registrar(ctx, fila, numero, id => db.MigrationAccountRows.Add(new MigrationAccountRow
        {
            MigrationLoadPartId = id, RowNumber = fila.Numero, SourceAccountNumber = numero, PersonId = titular.Id, SourceProductCode = heredado,
            CreditLineCode = lineaDeLaCuenta.Code, CreditLineId = lineaDeLaCuenta.Id, OfficePublicId = oficina.Value, DepositClass = clase.Value,
            OpenedOn = apertura.Value, Status = estado.Value, LastHolderActivityDate = ultimoMovimiento, HandlingMode = manejo, Principal = capital,
            InterestPayable = interes, LastSettledThrough = liquidadoHasta, ChargeReceivable = cargos, ClearingAmount = canje, SourceOfFundsReference = soporte,
            ContractualInstallment = contrato?.Cuota, ContractualPaidInstallments = contrato?.Pagadas, ContractualLateInstallments = contrato?.Atrasadas,
            ContractualMaturityDate = contrato?.Vencimiento, AgreedAnnualRate = contrato?.TasaBase, ContractualBonusAnnualRate = contrato?.TasaBonificada,
            ContractualStatus = contrato?.Estado,
        }));
    }

    /// <summary>El contrato de un ahorro contractual migrado como lo trae la fila (research §19). (nuevo)</summary>
    private sealed record ContratoDeLaFila(decimal Cuota, int Pagadas, int? Atrasadas, DateOnly Vencimiento, decimal? TasaBase, decimal? TasaBonificada,
        ContractualStatus Estado);

    /// <summary>
    /// US7 (T182): las columnas del contrato de un ahorro contractual —la cuota, las cuotas pagadas y atrasadas al corte, el vencimiento (posterior al corte:
    /// el vencido se liquida antes de migrar), la tasa base y la bonificada pactadas (vacías = las de la línea) y si ya estaba incumplido—, y el aviso si las
    /// pagadas y las atrasadas no suman las cuotas del plan vencidas al corte (el plan se arma con la periodicidad de la línea desde el vencimiento hacia
    /// atrás, por meses enteros desde la apertura).
    /// </summary>
    private ContratoDeLaFila? ContratoMigrado(FilaDeImportacion fila, LineaAlCorte? linea, DateOnly? apertura)
    {
        fila.Requerido(C.CuotaContractual);
        fila.Requerido(C.CuotasPagadas);
        fila.Requerido(C.FechaDeVencimientoContractual);
        var cuota = NoNegativo(fila, C.CuotaContractual, fila.Monto(C.CuotaContractual), positivo: true);
        var pagadas = fila.Entero(C.CuotasPagadas);
        if (pagadas is < 0) fila.Error(C.CuotasPagadas, ImportErrors.CellFormat, "Las cuotas pagadas no son negativas.");
        var atrasadas = fila.Entero(C.CuotasAtrasadas);
        if (atrasadas is < 0) fila.Error(C.CuotasAtrasadas, ImportErrors.CellFormat, "Las cuotas atrasadas no son negativas.");
        var vence = fila.Fecha(C.FechaDeVencimientoContractual);
        if (vence is { } v && v <= Corte)
            fila.Error(C.FechaDeVencimientoContractual, ImportErrors.CellFormat,
                "Un contrato vencido al corte se liquida y se entrega en el sistema anterior antes de migrar: el vencimiento es posterior al corte.");
        var tasa = fila.Porcentaje(C.TasaPactadaEA);
        var bonificada = fila.Porcentaje(C.TasaBonificadaEA);
        var estado = Enumeracion<ContractualStatus>(fila, C.EstadoContractual) ?? ContractualStatus.Current;
        if (estado == ContractualStatus.Fulfilled)
            fila.Error(C.EstadoContractual, ImportErrors.CellFormat, "Un contrato cumplido ya venció: el estado es Current o Breached.");
        if (fila.TieneErrores || vence is null || pagadas is null || apertura is null) return null;
        if (linea?.DeCaptacion is { DepositClass: DepositClass.Contractual } version)
        {
            var meses = Math.Max(1, (vence.Value.Year - apertura.Value.Year) * 12 + vence.Value.Month - apertura.Value.Month);
            var inicio = vence.Value.AddMonths(-meses);
            var periodicidad = version.InstallmentPeriodicity ?? RepaymentPeriodicity.Monthly;
            var cuotas = AhorroContractual.Cuotas(inicio, periodicidad, vence.Value);
            var alCorte = cuotas.Count(q => q.Vencimiento <= Corte);
            if (pagadas > cuotas.Count)
                fila.Error(C.CuotasPagadas, ImportErrors.CellFormat,
                    $"El contrato tiene {cuotas.Count} cuotas {periodicidad} entre {inicio:yyyy-MM-dd} y {vence:yyyy-MM-dd}: no pudo pagar {pagadas}.");
            else if (pagadas + (atrasadas ?? 0) != alCorte)
                fila.Aviso(C.CuotasPagadas, "Lending.Migration.ContractualInstallmentsDiffer",
                    $"Al corte vencieron {alCorte} cuotas del plan ({inicio:yyyy-MM-dd} a {vence:yyyy-MM-dd}, {periodicidad}); la fila trae {pagadas} pagadas y "
                    + $"{atrasadas ?? 0} atrasadas. Las atrasadas que sigan exigibles se cargan en account-installments.");
            if (version.MinInstallmentAmount is { } minima && cuota < minima)
                fila.Aviso(C.CuotaContractual, AhorroContractual.CodigoCuotaBajoElMinimo,
                    $"La cuota de {cuota:N0} está bajo la mínima de la línea ({minima:N0}): se conserva la pactada en el sistema anterior.");
        }
        if (fila.TieneErrores) return null;
        return new ContratoDeLaFila(cuota, pagadas.Value, atrasadas, vence.Value, tasa, bonificada, estado);
    }

    private Action<int>? CuentaDeAportes(ContextoDeImportacion ctx, FilaDeImportacion fila)
    {
        var asociado = Persona(fila, C.Asociado);
        var numero = fila.Crudo(C.Numero) is null ? null : NumeroDeCuenta(fila, C.Numero);
        var oficina = Sucursal(fila, C.Oficina);
        var linea = LineaDeLaCuenta(fila);
        var vinculacion = fila.Fecha(C.FechaDeVinculacion);
        HastaElCorte(fila, C.FechaDeVinculacion, vinculacion);
        var minimos = NoNegativo(fila, C.AportesMinimos, fila.Monto(C.AportesMinimos));
        var ordinarios = NoNegativo(fila, C.AportesOrdinarios, fila.Monto(C.AportesOrdinarios));
        var extraordinarios = NoNegativo(fila, C.AportesExtraordinarios, fila.Monto(C.AportesExtraordinarios));
        var revalorizados = NoNegativo(fila, C.AportesRevalorizados, fila.Monto(C.AportesRevalorizados));
        var minimoNoReducible = fila.Monto(C.MinimoNoReducible);
        if (minimoNoReducible is { } mnr) NoNegativo(fila, C.MinimoNoReducible, mnr);
        var ingreso = fila.Monto(C.IngresoRegistrado);
        if (ingreso is { } ing) NoNegativo(fila, C.IngresoRegistrado, ing);
        var cuota = fila.Monto(C.CuotaPactada);
        if (cuota is { } cp) NoNegativo(fila, C.CuotaPactada, cp);
        if (linea is { Linea.DeAportes: { InstallmentMode: ContributionAmountMode.IncomeFraction } } && ingreso is null)
            fila.Aviso(C.IngresoRegistrado, "Lending.Contributions.IncomeMissing",
                "La cuota de la línea es un porcentaje del ingreso y la fila no trae el ingreso registrado: revise el ingreso del asociado en Asociados.");
        if (cuota is not null)
            fila.Aviso(C.CuotaPactada, "Lending.Migration.InstallmentDiffers",
                "La cuota pactada se conserva como dato de la carga: la cuota exigible después de activar la calcula la línea.");

        if (asociado is { AsociadaAlCorte: false })
            fila.Error(C.Asociado, "Lending.Savings.MembersOnly", "Una cuenta de aportes es de un asociado vigente al corte, y esta persona no lo estaba.");
        if (asociado is not null && _conAportesVivos.Contains(asociado.Id))
            fila.Error(C.Asociado, "Lending.Contributions.AccountAlreadyOpen", "La persona ya tiene una cuenta de aportes viva en la Cartera.");

        if (asociado is null) return null;
        if (numero is not null)
        {
            NumeroNuevo(fila, numero);
            if (!ctx.Datos.LlaveUnica(fila, numero, C.Numero)) return null;
            _numerosDelArchivo.Add(numero);
        }
        if (!LlaveNueva(ctx, fila, Llave(asociado.Id), C.Asociado)) return null;
        if (oficina is null || linea is null || vinculacion is null) return null;
        var (lineaDeLaCuenta, heredado) = linea.Value;
        return Registrar(ctx, fila, numero ?? fila.Crudo(C.Asociado)!, id => db.MigrationContributionAccountRows.Add(new MigrationContributionAccountRow
        {
            MigrationLoadPartId = id, RowNumber = fila.Numero, PersonId = asociado.Id, SourceAccountNumber = numero, SourceProductCode = heredado,
            CreditLineCode = lineaDeLaCuenta.Code, CreditLineId = lineaDeLaCuenta.Id, OfficePublicId = oficina.Value, OpenedOn = vinculacion.Value,
            MinimumBalance = minimos, OrdinaryBalance = ordinarios, ExtraordinaryBalance = extraordinarios, RevaluedBalance = revalorizados,
            MinimumNonReducible = minimoNoReducible, RegisteredIncome = ingreso, InstallmentAmount = cuota,
        }));
    }

    private Action<int>? ParteDeLaCuenta(ContextoDeImportacion ctx, FilaDeImportacion fila)
    {
        var cuenta = Referencia(fila, C.Cuenta);
        var persona = Persona(fila, C.Persona);
        var rol = Enumeracion<MemberAccountPartyRole>(fila, C.Rol);
        if (rol == MemberAccountPartyRole.Holder)
            fila.Error(C.Rol, ImportErrors.CellFormat, "El titular va en la cuenta: aquí van cotitulares, firmantes, representantes y beneficiarios.");
        if (EsDeAportes && rol is not (null or MemberAccountPartyRole.Beneficiary))
            fila.Error(C.Rol, ReglasDeTitularidad.CodigoSoloTitularYBeneficiarios, "Una cuenta de aportes sólo tiene titular y beneficiarios.");
        var proporcion = fila.Porcentaje(C.Proporcion);
        var limite = fila.Monto(C.LimiteDeFirma);
        var beneficiario = fila.Porcentaje(C.PorcentajeBeneficiario);
        var fundamento = fila.Texto(C.Fundamento, 40);
        if (proporcion is not null && rol != MemberAccountPartyRole.CoHolder)
            fila.Error(C.Proporcion, ImportErrors.CellFormat, "Sólo un cotitular tiene proporción del saldo.");
        if (proporcion is { } pr && (pr <= 0m || pr >= 1m))
            fila.Error(C.Proporcion, ImportErrors.CellFormat, "La proporción de un cotitular va entre 0 y 100 puntos, sin incluirlos.");
        if (limite is not null && rol != MemberAccountPartyRole.Signatory)
            fila.Error(C.LimiteDeFirma, ImportErrors.CellFormat, "Sólo un firmante tiene límite de firma.");
        if (limite is { } lf) NoNegativo(fila, C.LimiteDeFirma, lf);
        if (rol == MemberAccountPartyRole.Beneficiary) fila.Requerido(C.PorcentajeBeneficiario);
        else if (beneficiario is not null) fila.Error(C.PorcentajeBeneficiario, ImportErrors.CellFormat, "Sólo un beneficiario tiene porcentaje de beneficiario.");
        if (beneficiario is { } bp && (bp <= 0m || bp > 1m))
            fila.Error(C.PorcentajeBeneficiario, ImportErrors.CellFormat, "El porcentaje de un beneficiario va entre 0 (excluido) y 100.");
        if (rol == MemberAccountPartyRole.LegalRepresentative)
        {
            fila.Requerido(C.Fundamento);
            fila.Requerido(C.Soporte);
            if (fundamento is not null && !ReglasDeTitularidad.Fundamentos.Contains(fundamento, StringComparer.OrdinalIgnoreCase))
                fila.Error(C.Fundamento, ImportErrors.CellFormat, $"Admite: {string.Join(", ", ReglasDeTitularidad.Fundamentos)}.");
        }
        else if (fundamento is not null)
            fila.Error(C.Fundamento, ImportErrors.CellFormat, "Sólo un representante legal tiene fundamento de la representación.");
        var desde = fila.Fecha(C.VigenteDesde);
        HastaElCorte(fila, C.VigenteDesde, desde);
        var hasta = fila.Fecha(C.VigenteHasta);
        if (hasta is { } h && h <= Corte)
            fila.Error(C.VigenteHasta, ImportErrors.CellFormat, "Sólo se migran las partes vigentes al corte: la fecha de fin va después del corte o vacía.");
        var soporte = fila.Texto(C.Soporte, 200);

        if (cuenta is null || persona is null || rol is null || desde is null) return null;
        var llave = Llave(cuenta, persona.Id, rol.Value);
        if (!LlaveNueva(ctx, fila, llave, C.Persona)) return null;
        var normalizado = fundamento is null ? null : ReglasDeTitularidad.Fundamentos.First(f => string.Equals(f, fundamento, StringComparison.OrdinalIgnoreCase));
        return Registrar(ctx, fila, llave, id => db.MigrationAccountPartyRows.Add(new MigrationAccountPartyRow
        {
            MigrationLoadPartId = id, RowNumber = fila.Numero, SourceAccountNumber = cuenta, PersonId = persona.Id, Role = rol.Value,
            Share = rol == MemberAccountPartyRole.Beneficiary ? beneficiario : proporcion, SignatureLimit = limite, RepresentationBasis = normalizado,
            ValidFrom = desde.Value, ValidTo = hasta, SupportReference = soporte,
        }));
    }

    private Action<int>? Cuota(ContextoDeImportacion ctx, FilaDeImportacion fila)
    {
        var cuenta = Referencia(fila, C.Cuenta);
        var periodo = Periodo(fila);
        var vence = fila.Fecha(C.FechaDeVencimiento);
        HastaElCorte(fila, C.FechaDeVencimiento, vence);
        var valor = NoNegativo(fila, C.Valor, fila.Monto(C.Valor), positivo: true);
        var pagado = NoNegativo(fila, C.Pagado, fila.Monto(C.Pagado));
        if (pagado > 0m && pagado >= valor) fila.Error(C.Pagado, ImportErrors.CellFormat, "Una cuota pagada entera no está atrasada: lo pagado es menor que el valor.");
        var motivo = Enumeracion<NonDeductionReason>(fila, C.MotivoNoDescontado);
        if (cuenta is null || periodo is null || vence is null) return null;
        var (anio, mes, sub) = periodo.Value;
        var llave = Llave(cuenta, anio, mes, sub);
        if (!LlaveNueva(ctx, fila, llave, C.Periodo)) return null;
        return Registrar(ctx, fila, llave, id => db.MigrationAccountInstallmentRows.Add(new MigrationAccountInstallmentRow
        {
            MigrationLoadPartId = id, RowNumber = fila.Numero, SourceAccountNumber = cuenta, PeriodYear = anio, PeriodMonth = mes, SubPeriodNumber = sub,
            DueDate = vence.Value, Amount = valor, PaidAmount = pagado, NonDeductionReason = motivo,
        }));
    }

    private static (short Anio, byte Mes, byte Sub)? Periodo(FilaDeImportacion fila)
    {
        var texto = fila.Texto(C.Periodo, 10);
        if (texto is null) return null;
        var m = PatronDePeriodo.Match(texto);
        if (m.Success && int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) is >= 1 and <= 12 and var mes)
        {
            var sub = m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 1;
            if (sub is >= 1 and <= 5) return ((short)int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), (byte)mes, (byte)sub);
        }
        fila.Error(C.Periodo, ImportErrors.CellFormat, "El período se escribe AAAA-MM o AAAA-MM-n (subperíodo 1 a 5).");
        return null;
    }

    private Action<int>? Exencion(ContextoDeImportacion ctx, FilaDeImportacion fila)
    {
        var persona = Persona(fila, C.Persona);
        var cuenta = Referencia(fila, C.CuentaExenta);
        var desde = fila.Fecha(C.VigenteDesde);
        HastaElCorte(fila, C.VigenteDesde, desde);
        var usado = NoNegativo(fila, C.ExentoUsadoDelMes, fila.Monto(C.ExentoUsadoDelMes));
        if (persona is null || cuenta is null || desde is null) return null;
        if (!LlaveNueva(ctx, fila, Llave(persona.Id), C.Persona)) return null;
        return Registrar(ctx, fila, Llave(persona.Id), id => db.MigrationGmfExemptionRows.Add(new MigrationGmfExemptionRow
        {
            MigrationLoadPartId = id, RowNumber = fila.Numero, PersonId = persona.Id, SourceAccountNumber = cuenta, MarkedSince = desde.Value, MonthExemptUsed = usado,
        }));
    }

    /// <summary>
    /// US11 (T189; §4.6, research E166): un bloqueo o un embargo vigente al corte. El bloqueo lleva su motivo (no los de proceso), valor o total; el de garantía,
    /// el crédito. El embargo lleva el juzgado (persona viva), el proceso, el demandante, la fecha del oficio y lo retenido en la cuenta al corte. Que la cuenta
    /// esté en la carga, que un embargo no recaiga sobre aportes, que el crédito viva y que lo retenido quepa en el saldo lo valida la aplicación.
    /// </summary>
    private Action<int>? BloqueoOEmbargo(ContextoDeImportacion ctx, FilaDeImportacion fila)
    {
        var cuenta = Referencia(fila, C.Cuenta);
        var tipo = fila.Texto(C.Tipo, 20);
        var esEmbargo = string.Equals(tipo, "Garnishment", StringComparison.OrdinalIgnoreCase);
        if (tipo is not null && !esEmbargo && !string.Equals(tipo, "Hold", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(tipo, "Deceased", StringComparison.OrdinalIgnoreCase)) fila.TodaviaNoDisponible(C.Tipo, "K10");
            else fila.Error(C.Tipo, ImportErrors.CellFormat, "El tipo es Hold (bloqueo) o Garnishment (embargo).");
        }
        var referencia = fila.Texto(C.Referencia, 40);
        var desde = fila.Fecha(C.Desde);
        HastaElCorte(fila, C.Desde, desde);
        HoldReason? motivo = null;
        decimal? valor = null;
        var total = false;
        string? credito = null;
        PersonaLeida? juzgado = null;
        GarnishmentAuthority? autoridad = null;
        DateOnly? oficio = null;
        decimal? retenido = null;
        decimal? medida = null;
        if (esEmbargo)
        {
            juzgado = Persona(fila, C.Juzgado);
            if (fila.Crudo(C.Juzgado) is null) fila.Error(C.Juzgado, ImportErrors.CellRequired, "Un embargo lleva el juzgado o la entidad que lo ordenó.");
            autoridad = fila.Crudo(C.Autoridad) is null ? GarnishmentAuthority.Court : Enumeracion<GarnishmentAuthority>(fila, C.Autoridad);
            if (fila.Texto(C.Proceso, 40) is null) fila.Error(C.Proceso, ImportErrors.CellRequired, "Un embargo lleva el radicado del proceso.");
            if (fila.Texto(C.Demandante, 200) is null) fila.Error(C.Demandante, ImportErrors.CellRequired, "Un embargo lleva el demandante.");
            oficio = fila.Fecha(C.FechaDelOficio);
            if (oficio is null && fila.Crudo(C.FechaDelOficio) is null) fila.Error(C.FechaDelOficio, ImportErrors.CellRequired, "Un embargo lleva la fecha del oficio.");
            HastaElCorte(fila, C.FechaDelOficio, oficio);
            retenido = NoNegativo(fila, C.Retenido, fila.Monto(C.Retenido));
            medida = fila.Monto(C.ValorDeLaMedida);
            if (medida is { } m) NoNegativo(fila, C.ValorDeLaMedida, m, positivo: true);
            if (fila.Crudo(C.Motivo) is not null || fila.Crudo(C.Valor) is not null)
                fila.Error(C.Motivo, ImportErrors.CellFormat, "El motivo y el valor son del bloqueo; el embargo lleva lo retenido.");
        }
        else
        {
            motivo = Enumeracion<HoldReason>(fila, C.Motivo);
            if (motivo is null && fila.Crudo(C.Motivo) is null) fila.Error(C.Motivo, ImportErrors.CellRequired, "Un bloqueo lleva su motivo.");
            if (motivo is { } r && !Holds.PlaceAccountHoldCommand.Manuales.Contains(r))
                fila.Error(C.Motivo, ImportErrors.CellFormat, "El motivo es Internal, LoanCollateral, SignaturePending u Other (los demás los pone su proceso).");
            total = fila.SiNo(C.Total);
            valor = fila.Monto(C.Valor);
            if (valor is { } v) NoNegativo(fila, C.Valor, v, positivo: true);
            if (total == (valor is not null)) fila.Error(C.Valor, ImportErrors.CellFormat, "Un bloqueo es por valor o total, una de las dos.");
            credito = fila.Texto(C.Credito, 30)?.ToUpperInvariant();
            if (motivo == HoldReason.LoanCollateral && credito is null)
                fila.Error(C.Credito, ImportErrors.CellRequired, "El bloqueo de garantía lleva el número del crédito que garantiza.");
        }
        if (cuenta is null || tipo is null || referencia is null || desde is null) return null;
        var clase = esEmbargo ? "Garnishment" : "Hold";
        var llave = Llave(cuenta, clase, referencia.ToUpperInvariant());
        if (!LlaveNueva(ctx, fila, llave, C.Referencia)) return null;
        return Registrar(ctx, fila, llave, id => db.MigrationHoldRows.Add(new MigrationHoldRow
        {
            MigrationLoadPartId = id, RowNumber = fila.Numero, SourceAccountNumber = cuenta, Kind = clase, Reference = referencia, Reason = motivo,
            LoanNumber = credito, Amount = valor, IsTotal = total, ValidFrom = desde.Value, CourtPersonId = juzgado?.Id, Authority = autoridad,
            ProcessNumber = fila.Texto(C.Proceso, 40), ClaimantName = fila.Texto(C.Demandante, 200), MeasureAmount = medida,
            IsChildSupport = esEmbargo && fila.SiNo(C.EsPorAlimentos), OrderDate = oficio, RetainedAmount = retenido,
        }));
    }

    private Action<int>? Tributario(ContextoDeImportacion ctx, FilaDeImportacion fila)
    {
        var persona = Persona(fila, C.Titular);
        var cuenta = Referencia(fila, C.Cuenta);
        var anio = fila.Entero(C.Anio);
        if (anio is { } a && a != Corte.Year)
            fila.Error(C.Anio, ImportErrors.CellFormat, string.Create(CultureInfo.InvariantCulture, $"El acumulado es del año del corte ({Corte.Year})."));
        var concepto = Enumeracion<MigratedTaxConcept>(fila, C.Concepto);
        var valor = NoNegativo(fila, C.Valor, fila.Monto(C.Valor));
        var baseDeRetencion = fila.Monto(C.BaseDeRetencion);
        if (baseDeRetencion is { } b) NoNegativo(fila, C.BaseDeRetencion, b);
        if (baseDeRetencion is not null && concepto is not (null or MigratedTaxConcept.Withholding))
            fila.Error(C.BaseDeRetencion, ImportErrors.CellFormat, "La base de retención va sólo en la fila de la retención (Withholding).");
        if (persona is null || cuenta is null || anio is null || concepto is null) return null;
        var llave = Llave(persona.Id, cuenta, anio.Value, concepto.Value);
        if (!LlaveNueva(ctx, fila, llave, C.Concepto)) return null;
        return Registrar(ctx, fila, llave, id => db.MigrationTaxYearRows.Add(new MigrationTaxYearRow
        {
            MigrationLoadPartId = id, RowNumber = fila.Numero, PersonId = persona.Id, SourceAccountNumber = cuenta, TaxYear = (short)anio.Value,
            Concept = concepto.Value, Amount = valor, WithholdingBase = baseDeRetencion,
        }));
    }

    /// <summary>
    /// US6 (pendiente de la fase 10; plantillas-y-archivos.md §4.3): un CDAT vigente al corte. La línea debe ser de clase CDAT; el inicio vigente entre la
    /// constitución y el corte; el vencimiento después del corte; la tasa pactada es la tabla más el margen si se informan; el plazo de emisión se calcula del
    /// plazo y, si se informa distinto, es error; el plazo fuera del rango de la línea sólo avisa (lo pactado no se reescribe). Lo que cruza partes —el plan
    /// regenerado contra el interés causado, la cuenta destino— lo valida la aplicación de la carga.
    /// </summary>
    private Action<int>? Cdat(ContextoDeImportacion ctx, FilaDeImportacion fila)
    {
        var numero = NumeroDeCuenta(fila, C.Numero);
        var titular = Persona(fila, C.Titular);
        var oficina = Sucursal(fila, C.Oficina);
        var linea = LineaDeLaCuenta(fila);
        if (linea is { } l0 && l0.Linea.Clase != DepositClass.TermDeposit)
            fila.Error(l0.Heredado is null ? C.Linea : C.CodigoHeredado, ImportErrors.CellFormat,
                $"La línea {l0.Linea.Code} es de la clase {l0.Linea.Clase}: un CDAT va en una línea de CDAT.");
        var titulo = fila.Texto(C.NumeroDeTitulo, 20);
        var capital = NoNegativo(fila, C.Capital, fila.Monto(C.Capital), positivo: true);
        var constitucion = fila.Fecha(C.FechaDeConstitucion);
        HastaElCorte(fila, C.FechaDeConstitucion, constitucion);
        var inicio = fila.Fecha(C.FechaDeInicioVigente);
        HastaElCorte(fila, C.FechaDeInicioVigente, inicio);
        if (inicio is { } i0 && constitucion is { } c0 && i0 < c0)
            fila.Error(C.FechaDeInicioVigente, ImportErrors.CellFormat, "El inicio vigente no puede ser anterior a la constitución del título.");
        var plazo = fila.Entero(C.PlazoDias);
        if (plazo is { } pl && pl < 1) fila.Error(C.PlazoDias, ImportErrors.CellFormat, "El plazo es de al menos un día.");
        if (plazo is { } p1 && p1 >= 1 && linea is { Linea.DeCaptacion: { } v } && PlanDeCdat.ValidarPlazo(p1, v.MinTermDays, v.MaxTermDays) is { } fuera)
            fila.Aviso(C.PlazoDias, fuera.Codigo, $"{fuera.Mensaje} Lo pactado se conserva.");
        var vencimiento = fila.Fecha(C.FechaDeVencimiento);
        if (vencimiento is { } ve && ve <= Corte)
            fila.Error(C.FechaDeVencimiento, ImportErrors.CellFormat,
                string.Create(CultureInfo.InvariantCulture, $"Un CDAT vencido al corte ({Corte:yyyy-MM-dd}) no se migra como vigente: migre su versión prorrogada o cancélelo antes."));
        var tasa = fila.Porcentaje(C.TasaPactadaEA);
        var tabla = fila.Porcentaje(C.TasaDeTablaEA);
        var margen = fila.Porcentaje(C.MargenNegociadoEA);
        if (tasa is { } t0 && t0 <= 0m) fila.Error(C.TasaPactadaEA, ImportErrors.CellFormat, "La tasa pactada es mayor que cero.");
        if ((tabla is null) != (margen is null))
            fila.Error(tabla is null ? C.TasaDeTablaEA : C.MargenNegociadoEA, ImportErrors.CellFormat, "Con tasa negociada van la tasa de la tabla y el margen.");
        else if (tabla is { } tb && margen is { } mg && tasa is { } tp && tb + mg != tp)
            fila.Error(C.TasaPactadaEA, ImportErrors.CellFormat, "La tasa pactada es la de la tabla más el margen negociado.");
        var periodicidad = Enumeracion<RepaymentPeriodicity>(fila, C.PeriodicidadDeIntereses);
        var modalidad = Enumeracion<InterestTiming>(fila, C.Modalidad);
        if (periodicidad is { } pe && modalidad is { } mo && plazo is { } p2 && p2 >= 1
            && PlanDeCdat.ValidarPeriodicidad(pe, mo, p2, [], []) is { } periodicidadInvalida)
            fila.Error(C.PeriodicidadDeIntereses, periodicidadInvalida.Codigo, periodicidadInvalida.Mensaje);
        var pagados = fila.Entero(C.PeriodosPagados);
        if (pagados is { } pg && pg < 0) fila.Error(C.PeriodosPagados, ImportErrors.CellFormat, "No puede ser negativo.");
        var causado = NoNegativo(fila, C.InteresCausadoNoPagado, fila.Costo(C.InteresCausadoNoPagado));
        var destino = Enumeracion<CdatMigradoDestino>(fila, C.DestinoDeIntereses);
        var cuentaDestino = fila.Crudo(C.CuentaDestino) is null ? null : NumeroDeCuenta(fila, C.CuentaDestino);
        if (destino == CdatMigradoDestino.Account && cuentaDestino is null) fila.Requerido(C.CuentaDestino);
        if (destino == CdatMigradoDestino.DisbursementOrder && cuentaDestino is not null)
            fila.Error(C.CuentaDestino, ImportErrors.CellFormat,
                "Con giro (DisbursementOrder) no va cuenta destino: el giro va a la cuenta bancaria registrada del titular.");
        var bancoDelGiro = fila.Codigo(C.BancoDelGiro, 20);
        var tipoDelGiro = fila.Entero(C.TipoDeCuentaDelGiro);
        var numeroDelGiro = fila.Texto(C.NumeroDeCuentaDelGiro, 30);
        if (destino == CdatMigradoDestino.Account && (bancoDelGiro is not null || tipoDelGiro is not null || numeroDelGiro is not null))
            fila.Error(C.BancoDelGiro, ImportErrors.CellFormat, "Con abono en cuenta (Account) no van datos bancarios del giro.");
        if (bancoDelGiro is not null || tipoDelGiro is not null || numeroDelGiro is not null)
        {
            if (bancoDelGiro is null) fila.Requerido(C.BancoDelGiro);
            if (tipoDelGiro is null) fila.Requerido(C.TipoDeCuentaDelGiro);
            else if (tipoDelGiro is < 0 or > 255) fila.Error(C.TipoDeCuentaDelGiro, ImportErrors.CellFormat, "El tipo de cuenta no es válido.");
            if (numeroDelGiro is null) fila.Requerido(C.NumeroDeCuentaDelGiro);
        }
        var instruccion = Enumeracion(fila, C.InstruccionAlVencimiento,
            new Dictionary<string, MaturityInstruction>(StringComparer.OrdinalIgnoreCase) { ["RenewWithConditions"] = MaturityInstruction.RenewWithNewTerms });
        var prorrogas = fila.Entero(C.ProrrogasHechas) ?? 0;
        if (prorrogas < 0) fila.Error(C.ProrrogasHechas, ImportErrors.CellFormat, "No puede ser negativo.");
        var plazoDeEmision = Enumeracion<IssueTermBand>(fila, C.PlazoDeEmision);
        IssueTermBand? calculado = plazo is { } p3 && p3 >= 1 ? PlanDeCdat.PlazoDeEmision(p3) : null;
        if (plazoDeEmision is not null && calculado is not null && plazoDeEmision != calculado)
            fila.Error(C.PlazoDeEmision, ImportErrors.CellFormat, $"El plazo de {plazo} días es del plazo de emisión {calculado}, no {plazoDeEmision}.");
        var embargado = fila.SiNo(C.Embargado);
        if (embargado)
            fila.Aviso(C.Embargado, "Lending.Migration.GarnishmentPending",
                "El embargo llega con los bloqueos y embargos: el CDAT nace marcado embargado y no se paga al vencer hasta registrar la orden.");
        var fallecido = fila.SiNo(C.TitularFallecido);
        if (titular is not null && fila.Crudo(C.TitularFallecido) is not null && fallecido != titular.Fallecida)
            fila.Aviso(C.TitularFallecido, "Lending.Savings.HolderDeceased", "El fallecimiento informado no coincide con el maestro de personas: rige el maestro.");
        if (titular is not null && linea is { } l1 && l1.Linea.DeCaptacion is { RequiresAssociate: true } && !titular.AsociadaAlCorte)
            fila.Error(C.Titular, "Lending.Savings.MembersOnly", $"La línea {l1.Linea.Code} exige asociado y el titular no estaba afiliado al corte.");

        if (numero is null) return null;
        NumeroNuevo(fila, numero);
        if (!LlaveNueva(ctx, fila, Llave(numero), C.Numero)) return null;
        _numerosDelArchivo.Add(numero);
        if (titular is null || oficina is null || linea is null || titulo is null || constitucion is null || inicio is null || plazo is null
            || vencimiento is null || tasa is null || periodicidad is null || modalidad is null || pagados is null || destino is null || instruccion is null
            || calculado is null)
            return null;
        var (lineaDelCdat, heredado) = linea.Value;
        return Registrar(ctx, fila, numero, id => db.MigrationTermDepositRows.Add(new MigrationTermDepositRow
        {
            MigrationLoadPartId = id, RowNumber = fila.Numero, SourceAccountNumber = numero, PersonId = titular.Id, SourceProductCode = heredado,
            CreditLineCode = lineaDelCdat.Code, CreditLineId = lineaDelCdat.Id, OfficePublicId = oficina.Value, SourceTitleNumber = titulo, PrincipalAmount = capital,
            ConstitutedOn = constitucion.Value, StartDate = inicio.Value, TermDays = plazo.Value, MaturityDate = vencimiento.Value, AppliedAnnualRate = tasa.Value,
            TableAnnualRate = tabla, NegotiatedSpread = margen, InterestPeriodicity = periodicidad.Value, InterestTiming = modalidad.Value,
            PaidPeriods = pagados.Value, InterestPayable = causado,
            InterestDestination = destino == CdatMigradoDestino.Account ? InterestDestination.ToAccount : InterestDestination.PaymentOrder,
            DestinationAccountNumber = cuentaDestino, DestinationBankTransferCode = bancoDelGiro,
            DestinationBankAccountType = tipoDelGiro is { } tg ? (byte)tg : null, DestinationBankAccountNumber = numeroDelGiro, MaturityInstruction = instruccion.Value, RolloverCount = prorrogas, IssueTermBand = calculado.Value,
            IsGarnished = embargado, HolderDeceased = titular.Fallecida,
        }));
    }

    /// <summary>El destino de los intereses de un CDAT migrado como lo escribe la plantilla (§4.3).</summary>
    private enum CdatMigradoDestino
    {
        Account = 1,
        DisbursementOrder = 2,
    }

    /// <summary>Las referencias a cuentas que la carga todavía no trae: aviso (al aplicar, error entre partes).</summary>
    public void Avisar()
    {
        foreach (var (fila, columna, numero) in _referencias)
            if (!_numerosDeLaCarga.Contains(numero) && !_numerosDelArchivo.Contains(numero))
                fila.Aviso(columna, ImportDepositMigrationPartCommandHandler.CodigoCuentaPendiente,
                    $"La cuenta {numero} todavía no está en la carga (cuentas de ahorro o de aportes): cárguela antes de aplicar.");
    }
}
