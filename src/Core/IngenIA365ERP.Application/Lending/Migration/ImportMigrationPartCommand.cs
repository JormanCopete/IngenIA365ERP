using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>
/// Importa un archivo de una parte de la carga de migración (feature 013, US8, T0740; plantillas.md §5.1, §5.3; C64; api.md §19.1,
/// <c>POST /api/lending/migration/loads/{id}/parts/{part}/import?mode=review|apply[&amp;replace=true]</c>, <c>Lending.Migration.Load</c>). (nuevo)
/// </summary>
public sealed record ImportMigrationPartCommand(
    Guid MigrationLoadPublicId,
    string Part,
    ModoDeImportacion? Mode,
    ArchivoDeImportacion File,
    bool Replace = false,
    string Reason = "") : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportMigrationPartCommandValidator : AbstractValidator<ImportMigrationPartCommand>
{
    public ImportMigrationPartCommandValidator()
    {
        RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
        RuleFor(x => x.Part).NotEmpty().MaximumLength(40);
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportMigrationPartCommand>.LargoMaximo);
    }
}

/// <summary>
/// Una tanda por archivo (C64): <see cref="EjecutorDeImportacion"/> lee la hoja <c>Datos</c> (o el <c>.csv</c>) con la definición de la parte
/// (<see cref="DefinicionesDeMigracion"/>), y esta plantilla:
/// <list type="bullet">
/// <item>carga en bloque las personas vivas por documento, las sucursales vinculadas a una oficina, las líneas con su versión vigente al corte, los
/// números de crédito de la carga (tandas vivas de <c>loans</c> y <c>charged-off</c>) y los de Cartera: nunca una consulta por fila;</item>
/// <item>valida cada fila: formato y obligatoriedad de §5.3, el maestro (<c>Import.Cell.NotFound</c>; ninguna plantilla crea personas), el alcance de
/// oficina y línea de quien importa (fuera, el mismo <c>Import.Cell.NotFound</c>, T62), vivienda (<c>Lending.Migration.HousingNotAvailable</c>,
/// D18), los cuadres de un archivo (<see cref="CuadresDeMigracion"/>: fechas crecientes, interés fuera de la cuota en curso, la ecuación de la
/// venta), y como <b>aviso</b> la tasa pactada sobre la usura del corte (<c>Lending.Usury.Exceeded</c>) y las referencias a créditos que la carga
/// todavía no trae;</item>
/// <item>la llave de la parte es única en el archivo y entre <b>todas</b> las tandas vivas de la parte (<c>Import.Row.Duplicate</c> nombrando la
/// tanda); el mismo archivo (mismo SHA-256) a la misma parte responde «sin cambio»; <c>replace=true</c> pasa las tandas vivas de la parte a
/// <c>Superseded</c> antes de agregar la nueva.</item>
/// </list>
/// <c>review</c> corre las mismas reglas y no guarda; <c>apply</c> es todo o nada para el archivo: una fila de <c>LND_MigrationLoadParts</c>
/// (<c>BatchNumber</c> consecutivo, nombre, SHA-256, filas, quién y cuándo) y sus filas de preparación, más la intervención <c>CargarMigracion</c>
/// sobre la carga (<see cref="RegistroDeIntervenciones"/>). La auditoría <c>Import.Reviewed</c>/<c>Import.Applied</c> con el SHA-256 la escribe el
/// ejecutor en el módulo <c>Lending</c>.
/// </summary>
public sealed class ImportMigrationPartCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    RegistroDeIntervenciones intervenciones,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IActorActual actorActual,
    IDateTimeService reloj,
    ReglasDeLineaDeCredito? reglasDeLinea = null) : IRequestHandler<ImportMigrationPartCommand, Result<ImportResultDto>>
{
    public const string CodigoReferenciaPendiente = "Lending.Migration.LoanNotInLoad";

    private static readonly Regex PatronDeNumero = new("^[A-Za-z0-9-]+$", RegexOptions.Compiled);
    private static readonly Regex PatronDeMes = new(@"^(\d{4})-(\d{2})$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ImportResultDto>> Handle(ImportMigrationPartCommand request, CancellationToken ct)
    {
        if (DefinicionesDeMigracion.Buscar(request.Part) is not { } parte)
            return Result.Failure<ImportResultDto>(ErroresDeGobierno.PlantillaInexistente());

        var setup = await db.LendingSetups.AsNoTracking().FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct);
        if (setup?.ActivationDate is { } activada) return Result.Failure<ImportResultDto>(ErroresDeActivacion.YaActivada(activada));
        var carga = await db.MigrationLoads.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
        if (carga is null) return Result.Failure<ImportResultDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
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
        var lectura = new LecturaDeParte(db, parte, carga, request.Replace ? [] : vivas, alcance, reglasDeLinea, ct);
        var pendientes = new List<Action<int>>();
        MigrationLoadPart? nueva = null;

        return await ejecutor.EjecutarAsync(parte.Definicion, request, async (ctx, c) =>
        {
            if (sinCambio)
            {
                // El mismo archivo a la misma parte: «sin cambio», sin otra tanda (C64).
                foreach (var fila in ctx.Datos.Filas) ctx.Registrar(fila, fila.Numero.ToString(CultureInfo.InvariantCulture), AccionDeImportacion.Unchanged);
                ctx.Extra["unchanged"] = true;
                return;
            }

            await lectura.CargarAsync(ctx);
            foreach (var fila in ctx.Datos.Filas) lectura.Leer(ctx, fila, pendientes);
            await lectura.AvisarAsync(ctx);
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

    // =================================================================================================================== lectura --

    /// <summary>Lo que una parte necesita para leer sus filas: los catálogos en bloque y las llaves de las tandas vivas.</summary>
    private sealed class LecturaDeParte(
        IApplicationDbContext db,
        ParteDeMigracion parte,
        MigrationLoad carga,
        IReadOnlyList<MigrationLoadPart> vivas,
        AlcanceDeCartera alcance,
        ReglasDeLineaDeCredito? reglas,
        CancellationToken ct)
    {
        private Dictionary<string, int> _personas = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (int Id, Guid PublicId, bool Vinculada)> _sucursales = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (int Id, CreditLineVersion? Version)> _lineas = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _llavesPrevias = new(StringComparer.Ordinal);
        private readonly Dictionary<int, int> _tandaDe = vivas.ToDictionary(v => v.Id, v => v.BatchNumber);
        private HashSet<string> _numerosDeLaCarga = new(StringComparer.Ordinal);
        private HashSet<string> _numerosEnCartera = new(StringComparer.Ordinal);
        private readonly HashSet<string> _numerosDelArchivo = new(StringComparer.Ordinal);
        private readonly List<(FilaDeImportacion Fila, string Columna, string Numero)> _referencias = [];
        private readonly List<(FilaDeImportacion Fila, string Linea, decimal Tasa)> _tasas = [];
        private readonly Dictionary<string, List<CuotaMigrada>> _cuotas = new(StringComparer.Ordinal);
        private readonly HashSet<string> _conPrincipal = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FilaDeImportacion> _primeraFilaDeSaldos = new(StringComparer.Ordinal);
        private readonly List<string> _avisos = [];

        private DateOnly Corte => carga.CutoffDate;

        public async Task CargarAsync(ContextoDeImportacion ctx)
        {
            var definicion = parte.Definicion.Hojas[0];
            var columnasDePersona = definicion.Columnas.Where(c => c.Tipo == TipoDeValor.Persona).Select(c => c.Nombre).ToList();
            var documentos = ctx.Datos.Filas.SelectMany(f => columnasDePersona.Select(f.Crudo)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (documentos.Count > 0)
                _personas = (await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId) && !p.IsDeleted).Select(p => new { p.TaxId, p.Id })
                        .ToListAsync(ct))
                    .GroupBy(p => p.TaxId!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

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
                var codigos = ctx.Datos.Filas.Select(f => f.Crudo(C.Linea)?.ToUpperInvariant()).OfType<string>().Distinct().ToList();
                var lineas = await db.CreditLines.AsNoTracking().Where(l => codigos.Contains(l.Code) && !l.IsDeleted && l.Nature == LineNature.Placement).Select(l => new { l.Id, l.Code })
                    .ToListAsync(ct);
                var versiones = await new VersionVigenteDeLinea(db).LeerVariasAsync(lineas.Select(l => l.Id).ToList(), Corte, ct: ct);
                foreach (var l in lineas) _lineas[l.Code] = (l.Id, versiones.TryGetValue(l.Id, out var v) ? v.Version : null);
            }

            var partesVivas = db.MigrationLoadParts.AsNoTracking()
                .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted);
            var deLoans = partesVivas.Where(p => p.PartKey == P.Loans).Select(p => p.Id);
            var deCastigos = partesVivas.Where(p => p.PartKey == P.ChargedOff).Select(p => p.Id);
            _numerosDeLaCarga = (await db.MigrationLoanRows.AsNoTracking().Where(r => deLoans.Contains(r.MigrationLoadPartId)).Select(r => r.SourceLoanNumber)
                    .ToListAsync(ct))
                .Concat(await db.MigrationChargedOffRows.AsNoTracking().Where(r => deCastigos.Contains(r.MigrationLoadPartId)).Select(r => r.SourceLoanNumber)
                    .ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);
            if (parte.Parte is P.Loans or P.ChargedOff)
            {
                var numeros = ctx.Datos.Filas.Select(f => f.Crudo(C.Numero)?.ToUpperInvariant()).OfType<string>().Distinct().ToList();
                _numerosEnCartera = (await db.Loans.AsNoTracking().Where(l => numeros.Contains(l.Number)).Select(l => l.Number).ToListAsync(ct))
                    .ToHashSet(StringComparer.Ordinal);
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
                P.Loans => (await db.MigrationLoanRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber))),
                P.Installments => (await db.MigrationInstallmentRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber, r.InstallmentNumber))),
                P.Balances => (await db.MigrationBalanceRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber, r.Concept))),
                P.RiskData => (await db.MigrationRiskDataRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber))),
                P.Restructurings => (await db.MigrationRestructuringRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber))),
                P.CoDebtors => (await db.MigrationCoDebtorRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber, r.CoDebtorPersonId))),
                P.Collaterals => (await db.MigrationCollateralRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber, r.CollateralKind, r.Reference.ToUpperInvariant()))),
                P.ChargedOff => (await db.MigrationChargedOffRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber))),
                P.Sales => (await db.MigrationSaleRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.InvoiceNumber.ToUpperInvariant()))),
                P.CreditLimits => (await db.MigrationCreditLimitRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.PersonId, r.LimitKind, r.CreditLineCode, r.ValidFrom))),
                P.Blocks => (await db.MigrationBlockRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.PersonId, r.BlockReason, r.Since))),
                P.Authorizations => (await db.MigrationAuthorizationRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.PersonId, r.Purpose, r.ContactChannel))),
                P.PriorNotices => (await db.MigrationPriorNoticeRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber, r.EpisodeStartDate))),
                P.BureauReports => (await db.MigrationBureauReportRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber, r.BureauCode.ToUpperInvariant()))),
                P.Claims => (await db.MigrationClaimRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber, r.BureauCode.ToUpperInvariant(), r.ReceivedOn))),
                P.RelatedParties => (await db.MigrationRelatedPartyRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.PersonId, r.Basis, r.RelatedToPersonId, r.ValidFrom))),
                P.ArrearsHistory => (await db.MigrationArrearsHistoryRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
                    .Select(r => (r.MigrationLoadPartId, Llave(r.SourceLoanNumber, r.PeriodYear, r.PeriodMonth))),
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

        /// <summary>La llave es única en el archivo y entre las tandas vivas de la parte.</summary>
        private bool LlaveNueva(ContextoDeImportacion ctx, FilaDeImportacion fila, string llave, string columna)
        {
            if (!ctx.Datos.LlaveUnica(fila, llave, columna)) return false;
            if (!_llavesPrevias.TryGetValue(llave, out var tanda)) return true;
            fila.Error(columna, ImportErrors.RowDuplicate,
                string.Create(CultureInfo.InvariantCulture, $"Esta fila ya viene en la tanda {tanda} de la parte {parte.Parte}. Use reemplazar la parte para corregirla."));
            return false;
        }

        // -------------------------------------------------------------------------------------------------- celdas --

        private string? Numero(FilaDeImportacion fila, string columna)
        {
            var texto = fila.Texto(columna, 20);
            if (texto is null) return null;
            if (PatronDeNumero.IsMatch(texto)) return texto.ToUpperInvariant();
            fila.Error(columna, ImportErrors.CellFormat, "El número de crédito admite letras, dígitos y guion, hasta 20.");
            return null;
        }

        /// <summary>Un número de crédito que cita a <c>loans</c> (o <c>charged-off</c>): si la carga todavía no lo trae, aviso (§5.1).</summary>
        private string? Referencia(FilaDeImportacion fila, string columna)
        {
            var numero = Numero(fila, columna);
            if (numero is not null) _referencias.Add((fila, columna, numero));
            return numero;
        }

        private int? Persona(FilaDeImportacion fila, string columna)
        {
            var documento = fila.Texto(columna, 20);
            if (documento is null) return null;
            if (_personas.TryGetValue(documento, out var id)) return id;
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

        private (int Id, CreditLineVersion Version, string Codigo)? Linea(FilaDeImportacion fila, string columna)
        {
            var codigo = fila.Codigo(columna, 10);
            if (codigo is null) return null;
            if (_lineas.TryGetValue(codigo, out var l) && l.Version is { } version && alcance.IncluyeLinea(l.Id))
            {
                // D18: hasta K6b la vivienda no se migra; desde K6b (T1055) entra a la carga y al cuadre como las demás.
                if (string.Equals(version.ModalityCode, "VIVIENDA", StringComparison.OrdinalIgnoreCase) && !Housing.LectorDeVivienda.Disponible())
                {
                    fila.Error(columna, ErroresDeMigracion.CodigoViviendaNoDisponible, ErroresDeMigracion.ViviendaNoDisponible(fila.Numero));
                    return null;
                }
                return (l.Id, version, codigo);
            }
            fila.Error(columna, ImportErrors.CellNotFound,
                string.Create(CultureInfo.InvariantCulture,
                    $"No hay una línea «{codigo}» con versión vigente al corte ({Corte:yyyy-MM-dd}) en su alcance. Créela o versiónela en Cartera › Líneas."));
            return null;
        }

        private static T? Enumeracion<T>(FilaDeImportacion fila, string columna, IReadOnlyDictionary<string, T>? etiquetas = null) where T : struct, Enum
        {
            // La celda admite el número del enum (data-model §9, C65) además del nombre y la etiqueta.
            if (fila.Crudo(columna) is { } crudo && int.TryParse(crudo, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                && Enum.IsDefined(typeof(T), Enum.ToObject(typeof(T), n)))
                return (T)Enum.ToObject(typeof(T), n);
            return fila.Enumeracion(columna, etiquetas);
        }

        private static (short Anio, byte Mes)? Mes(FilaDeImportacion fila, string columna)
        {
            var texto = fila.Texto(columna, 7);
            if (texto is null) return null;
            var m = PatronDeMes.Match(texto);
            if (m.Success && int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) is >= 1 and <= 12 and var mes)
                return ((short)int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), (byte)mes);
            fila.Error(columna, ImportErrors.CellFormat, "El mes se escribe AAAA-MM.");
            return null;
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

        // ---------------------------------------------------------------------------------------------------- filas --

        public void Leer(ContextoDeImportacion ctx, FilaDeImportacion fila, List<Action<int>> pendientes)
        {
            Action<int>? agregar = parte.Parte switch
            {
                P.Loans => Credito(ctx, fila),
                P.Installments => Cuota(ctx, fila),
                P.Balances => Saldo(ctx, fila),
                P.RiskData => Riesgo(ctx, fila),
                P.Restructurings => Reestructuracion(ctx, fila),
                P.CoDebtors => Codeudor(ctx, fila),
                P.Collaterals => Garantia(ctx, fila),
                P.ChargedOff => Castigado(ctx, fila),
                P.Sales => Venta(ctx, fila),
                P.CreditLimits => Cupo(ctx, fila),
                P.Blocks => Bloqueo(ctx, fila),
                P.Authorizations => Autorizacion(ctx, fila),
                P.PriorNotices => AvisoPrevio(ctx, fila),
                P.BureauReports => Reporte(ctx, fila),
                P.Claims => Reclamo(ctx, fila),
                P.RelatedParties => Vinculado(ctx, fila),
                P.ArrearsHistory => Historial(ctx, fila),
                _ => null,
            };
            if (agregar is not null && !fila.TieneErrores) pendientes.Add(agregar);
        }

        private Action<int>? Registrar(ContextoDeImportacion ctx, FilaDeImportacion fila, string llave, Action<int> agregar)
        {
            if (fila.TieneErrores) return null;
            ctx.Registrar(fila, llave, AccionDeImportacion.Create);
            return agregar;
        }

        private Action<int>? Credito(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Numero(fila, C.Numero);
            var deudor = Persona(fila, C.Deudor);
            var oficina = Sucursal(fila, C.Oficina);
            var linea = Linea(fila, C.Linea);
            var asesor = Persona(fila, C.Asesor);
            var factura = fila.Texto(C.Factura, 40);
            var desembolso = fila.Fecha(C.FechaDeDesembolso);
            HastaElCorte(fila, C.FechaDeDesembolso, desembolso);
            var monto = NoNegativo(fila, C.MontoDesembolsado, fila.Monto(C.MontoDesembolsado), positivo: true);
            var tipoDeTasa = Enumeracion<RateKind>(fila, C.TipoDeTasa);
            var tasa = fila.Porcentaje(C.TasaCorrienteEA);
            if (tipoDeTasa == RateKind.Fixed) fila.Requerido(C.TasaCorrienteEA);
            var indice = fila.Codigo(C.Indice, 20);
            var claveDelIndice = fila.Texto(C.ClaveDelIndice, 20);
            var margen = fila.Porcentaje(C.Margen);
            if (tipoDeTasa == RateKind.Variable)
            {
                fila.Requerido(C.Indice);
                fila.Requerido(C.Margen);
            }
            var piso = fila.Porcentaje(C.TasaPiso);
            var techo = fila.Porcentaje(C.TasaTecho);
            var reajuste = fila.Entero(C.MesesDeReajuste);
            var tipoDeMora = Enumeracion<DefaultRateKind>(fila, C.TipoDeTasaDeMora);
            var tasaDeMora = fila.Porcentaje(C.TasaDeMoraEA);
            var factor = fila.Costo(C.FactorDeMora);
            if (tipoDeMora == DefaultRateKind.Fixed) fila.Requerido(C.TasaDeMoraEA);
            if (tipoDeMora == DefaultRateKind.RelativeToCurrent) fila.Requerido(C.FactorDeMora);
            var convencion = Enumeracion<DayCountConvention>(fila, C.ConvencionDeDias, new Dictionary<string, DayCountConvention>
            {
                ["30/360"] = DayCountConvention.Thirty360, ["real/365"] = DayCountConvention.Actual365, ["real/360"] = DayCountConvention.Actual360,
            });
            var equivalencia = Enumeracion<RateEquivalence>(fila, C.Equivalencia);
            var unidad = fila.Monto(C.RedondeoUnidad);
            var modo = Enumeracion<RoundingMode>(fila, C.RedondeoModo);
            var momento = Enumeracion<RoundingMoment>(fila, C.RedondeoMomento);
            var sistema = Enumeracion<AmortizationSystem>(fila, C.SistemaDeAmortizacion);
            var prelacion = Enumeracion<AllocationOrder>(fila, C.Prelacion);
            var periodicidad = Enumeracion<RepaymentPeriodicity>(fila, C.Periodicidad);
            var plazo = fila.Entero(C.Plazo);
            if (plazo is < 1) fila.Error(C.Plazo, ImportErrors.CellFormat, "El plazo son las cuotas totales pactadas, desde 1.");
            var preferencia = Enumeracion<PrepaymentChoice>(fila, C.PreferenciaDeAbono);
            var forma = Enumeracion<RepaymentChannel>(fila, C.FormaDePago);
            var pagaduria = Persona(fila, C.Pagaduria);
            var idEnPagaduria = fila.Texto(C.IdEmpleadoEnPagaduria, 30);
            var libranza = fila.Fecha(C.FechaDeLibranza);
            if (forma == RepaymentChannel.PaymasterDeduction)
            {
                fila.Requerido(C.Pagaduria);
                fila.Requerido(C.IdEmpleadoEnPagaduria);
                fila.Requerido(C.FechaDeLibranza);
            }
            var etapa = fila.Texto(C.EtapaDeCobranza, 30);
            var juridico = fila.SiNo(C.EnJuridico);
            var abogado = Persona(fila, C.Abogado);
            var notas = fila.Texto(C.Notas, 400);

            if (numero is null) return null;
            if (_numerosEnCartera.Contains(numero))
                fila.Error(C.Numero, ImportErrors.RowDuplicate, $"Ya hay un crédito de Cartera con el número {numero}.");
            if (!LlaveNueva(ctx, fila, Llave(numero), C.Numero)) return null;
            _numerosDelArchivo.Add(numero);
            if (tipoDeTasa == RateKind.Fixed && tasa is { } t && linea is { } l) _tasas.Add((fila, l.Version.CreditLineId.ToString(CultureInfo.InvariantCulture), t));

            var condiciones = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["rateKind"] = tipoDeTasa?.ToString(), ["annualRate"] = tasa, ["index"] = indice, ["indexKey"] = claveDelIndice, ["margin"] = margen,
                ["floorRate"] = piso, ["ceilingRate"] = techo, ["resetMonths"] = reajuste, ["defaultRateKind"] = tipoDeMora?.ToString(),
                ["defaultRate"] = tasaDeMora, ["defaultFactor"] = factor, ["dayCount"] = convencion?.ToString(), ["equivalence"] = equivalencia?.ToString(),
                ["roundingUnit"] = unidad, ["roundingMode"] = modo?.ToString(), ["roundingMoment"] = momento?.ToString(),
                ["amortizationSystem"] = sistema?.ToString(), ["allocationOrder"] = prelacion?.ToString(), ["periodicity"] = periodicidad?.ToString(),
                ["term"] = plazo,
            }.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value), Json);

            return Registrar(ctx, fila, numero, id => db.MigrationLoanRows.Add(new MigrationLoanRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, PersonId = deudor!.Value,
                CreditLineCode = linea!.Value.Codigo, CreditLineId = linea.Value.Id, OfficePublicId = oficina!.Value,
                InvoiceNumber = factura, DisbursementDate = desembolso!.Value, OriginalAmount = monto, TermsJson = condiciones, PrepaymentChoice = preferencia,
                RepaymentChannel = forma!.Value, PaymasterPersonId = pagaduria, EmployeeIdAtPaymaster = idEnPagaduria, LibranzaDate = libranza,
                CollectionStageCode = etapa, AdvisorPersonId = asesor, InLegalCollection = juridico, LawyerPersonId = abogado, Notes = notas,
            }));
        }

        private Action<int>? Cuota(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var cuota = fila.Entero(C.Cuota);
            if (cuota is < 1) fila.Error(C.Cuota, ImportErrors.CellFormat, "La cuota va desde 1.");
            var vence = fila.Fecha(C.FechaDeVencimiento);
            var capital = NoNegativo(fila, C.Capital, fila.Monto(C.Capital));
            var corriente = NoNegativo(fila, C.InteresCorriente, fila.Monto(C.InteresCorriente));
            var mora = NoNegativo(fila, C.InteresDeMora, fila.Monto(C.InteresDeMora));
            var seguro = NoNegativo(fila, C.Seguro, fila.Monto(C.Seguro));
            var otros = NoNegativo(fila, C.Otros, fila.Monto(C.Otros));
            var proyectado = fila.Monto(C.InteresProyectado);
            if (numero is null || cuota is null || vence is null) return null;
            if (!LlaveNueva(ctx, fila, Llave(numero, cuota.Value), C.Cuota)) return null;
            if (!_cuotas.TryGetValue(numero, out var lista)) _cuotas[numero] = lista = [];
            lista.Add(new CuotaMigrada(fila.Numero, cuota.Value, vence.Value, capital, corriente, mora, seguro, otros));
            return Registrar(ctx, fila, Llave(numero, cuota.Value), id => db.MigrationInstallmentRows.Add(new MigrationInstallmentRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, InstallmentNumber = cuota.Value, DueDate = vence.Value,
                Principal = capital, CurrentInterest = corriente, DefaultInterest = mora, Insurance = seguro, OtherCharges = otros, ProjectedInterest = proyectado,
            }));
        }

        private Action<int>? Saldo(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var concepto = Enumeracion<LoanConcept>(fila, C.Concepto);
            var valor = NoNegativo(fila, C.Valor, fila.Monto(C.Valor));
            if (numero is null || concepto is null) return null;
            _primeraFilaDeSaldos.TryAdd(numero, fila);
            if (concepto == LoanConcept.Principal) _conPrincipal.Add(numero);
            if (!LlaveNueva(ctx, fila, Llave(numero, concepto.Value), C.Concepto)) return null;
            return Registrar(ctx, fila, Llave(numero, concepto.Value), id => db.MigrationBalanceRows.Add(new MigrationBalanceRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, Concept = concepto.Value, Amount = valor,
            }));
        }

        private Action<int>? Riesgo(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var dias = fila.Entero(C.DiasDeMora);
            if (dias is < 0) fila.Error(C.DiasDeMora, ImportErrors.CellFormat, "Los días de mora no pueden ser negativos.");
            var categoria = Enumeracion<RatingCategory>(fila, C.Calificacion);
            var subcategoria = Enumeracion<RatingSubcategory>(fila, C.Subcategoria) ?? RatingSubcategory.None;
            var causa = Enumeracion<RatingCause>(fila, C.Causa);
            var capital = NoNegativo(fila, C.DeterioroCapital, fila.Monto(C.DeterioroCapital));
            var intereses = NoNegativo(fila, C.DeterioroIntereses, fila.Monto(C.DeterioroIntereses));
            var otros = NoNegativo(fila, C.DeterioroOtros, fila.Monto(C.DeterioroOtros));
            var general = NoNegativo(fila, C.DeterioroGeneral, fila.Monto(C.DeterioroGeneral));
            var enE = fila.Fecha(C.FechaDeIngresoAE);
            if (categoria == RatingCategory.E) fila.Requerido(C.FechaDeIngresoAE);
            HastaElCorte(fila, C.FechaDeIngresoAE, enE);
            if (subcategoria != RatingSubcategory.None && categoria != RatingCategory.E)
                fila.Error(C.Subcategoria, ImportErrors.CellFormat, "La subcategoría sólo va con la calificación E.");
            var suspenso = fila.SiNo(C.EnSuspenso);
            if (numero is null || dias is null || categoria is null) return null;
            if (!LlaveNueva(ctx, fila, Llave(numero), C.NumeroDeCredito)) return null;
            return Registrar(ctx, fila, numero, id => db.MigrationRiskDataRows.Add(new MigrationRiskDataRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, DaysPastDue = dias.Value, Rating = categoria.Value,
                Subcategory = subcategoria, Cause = causa, PrincipalImpairment = capital, InterestImpairment = intereses, OtherImpairment = otros,
                GeneralImpairment = general, EnteredEOn = enE, IsSuspended = suspenso,
            }));
        }

        private Action<int>? Reestructuracion(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var clase = Enumeracion<TermsVersionCause>(fila, C.Clase);
            if (clase is not (null or TermsVersionCause.Restructuring or TermsVersionCause.Modification))
                fila.Error(C.Clase, ImportErrors.CellFormat, "Admite Restructuring (5) o Modification (6).");
            var veces = fila.Entero(C.VecesReestructurado);
            if (veces is < 1) fila.Error(C.VecesReestructurado, ImportErrors.CellFormat, "Va desde 1.");
            var ultima = fila.Fecha(C.FechaUltima);
            HastaElCorte(fila, C.FechaUltima, ultima);
            var calificacion = Enumeracion<RatingCategory>(fila, C.CalificacionAlReestructurar);
            var pagadas = fila.Entero(C.CuotasConsecutivasPagadas);
            if (pagadas is < 0) fila.Error(C.CuotasConsecutivasPagadas, ImportErrors.CellFormat, "No puede ser negativo.");
            var anterior = Numero(fila, C.CreditoAnterior);
            if (numero is null || clase is null || veces is null || ultima is null || calificacion is null || pagadas is null) return null;
            if (!LlaveNueva(ctx, fila, Llave(numero), C.NumeroDeCredito)) return null;
            return Registrar(ctx, fila, numero, id => db.MigrationRestructuringRows.Add(new MigrationRestructuringRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, Cause = clase.Value, RestructuringCount = veces.Value,
                LastRestructuredOn = ultima.Value, RatingAtRestructuring = calificacion.Value, ConsecutivePaidInstallments = pagadas.Value,
                PredecessorLoanNumber = anterior,
            }));
        }

        private Action<int>? Codeudor(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var codeudor = Persona(fila, C.Codeudor);
            var rol = Enumeracion<CreditPartyRole>(fila, C.Rol) ?? CreditPartyRole.CoDebtor;
            if (rol == CreditPartyRole.Applicant) fila.Error(C.Rol, ImportErrors.CellFormat, "Admite CoDebtor (2) o Guarantor (3).");
            var desde = fila.Fecha(C.Desde);
            if (numero is null || codeudor is null) return null;
            if (!LlaveNueva(ctx, fila, Llave(numero, codeudor.Value), C.Codeudor)) return null;
            return Registrar(ctx, fila, Llave(numero, codeudor.Value), id => db.MigrationCoDebtorRows.Add(new MigrationCoDebtorRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, CoDebtorPersonId = codeudor.Value, Role = rol, Since = desde,
            }));
        }

        private Action<int>? Garantia(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var tipo = Enumeracion<CollateralKind>(fila, C.Tipo);
            if (tipo == CollateralKind.CoDebtor) fila.Error(C.Tipo, ImportErrors.CellFormat, "El codeudor va en la parte de codeudores.");
            var referencia = fila.Texto(C.Referencia, 40);
            var valor = NoNegativo(fila, C.Valor, fila.Monto(C.Valor));
            var constitucion = fila.Fecha(C.FechaDeConstitucion);
            var hasta = fila.Fecha(C.VigenteHasta);
            var estado = Enumeracion<CollateralStatus>(fila, C.Estado) ?? CollateralStatus.Valid;
            var descripcion = fila.Texto(C.Descripcion, 400);
            var avaluo = fila.Monto(C.ValorAvaluo);
            var fechaAvaluo = fila.Fecha(C.FechaAvaluo);
            var poliza = fila.Texto(C.Poliza, 40);
            var vence = fila.Fecha(C.PolizaVence);
            var aseguradora = Persona(fila, C.Aseguradora);
            var clasePagare = Enumeracion<PromissoryNoteKind>(fila, C.ClasePagare);
            var canal = fila.Texto(C.CanalDeCustodia, 30);
            var externo = fila.Texto(C.IdExternoPagare, 60);
            var fondo = Persona(fila, C.FondoDeGarantias);
            var cobertura = fila.Porcentaje(C.Cobertura);
            var comision = fila.Porcentaje(C.Comision);
            if (tipo == CollateralKind.PromissoryNote) fila.Requerido(C.ClasePagare);
            if (clasePagare == PromissoryNoteKind.Dematerialized)
            {
                fila.Requerido(C.CanalDeCustodia);
                fila.Requerido(C.IdExternoPagare);
            }
            if (tipo == CollateralKind.GuaranteeFund) fila.Requerido(C.FondoDeGarantias);
            if (numero is null || tipo is null || referencia is null || constitucion is null) return null;
            var llave = Llave(numero, tipo.Value, referencia.ToUpperInvariant());
            if (!LlaveNueva(ctx, fila, llave, C.Referencia)) return null;
            return Registrar(ctx, fila, llave, id => db.MigrationCollateralRows.Add(new MigrationCollateralRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, CollateralKind = tipo.Value, Reference = referencia, Value = valor,
                ConstitutedOn = constitucion.Value, ValidUntil = hasta, Status = estado, Description = descripcion, AppraisalValue = avaluo,
                AppraisalDate = fechaAvaluo, PolicyNumber = poliza, PolicyExpiresOn = vence, InsurerPersonId = aseguradora, PromissoryNoteKind = clasePagare,
                CustodianChannelCode = canal, ExternalNoteId = externo, GuaranteeFundPersonId = fondo, CoveragePercentage = cobertura, FeePercentage = comision,
            }));
        }

        private Action<int>? Castigado(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Numero(fila, C.Numero);
            var deudor = Persona(fila, C.Deudor);
            var oficina = Sucursal(fila, C.Oficina);
            var linea = Linea(fila, C.Linea);
            var desembolso = fila.Fecha(C.FechaDeDesembolso);
            var castigo = fila.Fecha(C.FechaDeCastigo);
            HastaElCorte(fila, C.FechaDeCastigo, castigo);
            var capital = NoNegativo(fila, C.CapitalCastigado, fila.Monto(C.CapitalCastigado), positivo: true);
            var intereses = NoNegativo(fila, C.InteresesCastigados, fila.Monto(C.InteresesCastigados));
            var otros = NoNegativo(fila, C.OtrosCastigados, fila.Monto(C.OtrosCastigados));
            var soporte = fila.Texto(C.SoporteDelCastigo, 200);
            if (numero is null || castigo is null) return null;
            if (_numerosEnCartera.Contains(numero))
                fila.Error(C.Numero, ImportErrors.RowDuplicate, $"Ya hay un crédito de Cartera con el número {numero}.");
            if (!LlaveNueva(ctx, fila, Llave(numero), C.Numero)) return null;
            _numerosDelArchivo.Add(numero);
            return Registrar(ctx, fila, numero, id => db.MigrationChargedOffRows.Add(new MigrationChargedOffRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, PersonId = deudor!.Value, CreditLineCode = linea!.Value.Codigo,
                OfficePublicId = oficina!.Value, DisbursementDate = desembolso, WrittenOffOn = castigo.Value, PrincipalBalance = capital,
                InterestBalance = intereses, OtherBalance = otros, HistoricalApprovalReference = soporte!,
            }));
        }

        private Action<int>? Venta(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var factura = fila.Texto(C.Factura, 40);
            var fecha = fila.Fecha(C.FechaDeVenta);
            HastaElCorte(fila, C.FechaDeVenta, fecha);
            var deudor = Persona(fila, C.Deudor);
            var original = NoNegativo(fila, C.ValorOriginal, fila.Monto(C.ValorOriginal), positivo: true);
            var cobros = NoNegativo(fila, C.CobrosExternos, fila.Monto(C.CobrosExternos));
            var ajustes = fila.Monto(C.NotasYAjustes) ?? 0m;
            var saldo = NoNegativo(fila, C.SaldoAlCorte, fila.Monto(C.SaldoAlCorte));
            var resultado = Enumeracion<SaleReconciliationOutcome>(fila, C.Resultado);
            var numero = Numero(fila, C.NumeroDeCredito);
            var linea = fila.Crudo(C.Linea) is null ? null : Linea(fila, C.Linea);
            var referencia = fila.Texto(C.ReferenciaDeLosCobros, 400);
            if (!fila.TieneErrores && !CuadresDeMigracion.VentaCuadra(original, cobros, ajustes, saldo))
                fila.Error(C.SaldoAlCorte, ErroresDeMigracion.CodigoSaldoDescuadrado,
                    ErroresDeMigracion.VentaDescuadrada(factura ?? string.Empty, saldo, CuadresDeMigracion.SaldoDeVenta(original, cobros, ajustes)));
            switch (resultado)
            {
                case SaleReconciliationOutcome.Migrated:
                    if (saldo <= 0m) fila.Error(C.SaldoAlCorte, ImportErrors.CellFormat, "Una venta migrada tiene saldo al corte mayor que cero.");
                    fila.Requerido(C.NumeroDeCredito);
                    if (numero is not null) _referencias.Add((fila, C.NumeroDeCredito, numero));
                    break;
                case SaleReconciliationOutcome.CancelledBeforeCutoff:
                    if (saldo != 0m) fila.Error(C.SaldoAlCorte, ImportErrors.CellFormat, "Una venta cancelada antes del corte tiene saldo cero.");
                    break;
                case null:
                    break;
                default:
                    fila.Error(C.Resultado, ImportErrors.CellFormat, "Admite Migrated o CancelledBeforeCutoff.");
                    break;
            }
            if (factura is null || fecha is null || deudor is null || resultado is null) return null;
            var llave = Llave(factura.ToUpperInvariant());
            if (!LlaveNueva(ctx, fila, llave, C.Factura)) return null;
            var codigoDeLinea = linea?.Codigo;
            return Registrar(ctx, fila, factura, id => db.MigrationSaleRows.Add(new MigrationSaleRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, InvoiceNumber = factura, PersonId = deudor.Value, SaleDate = fecha.Value,
                OriginalAmount = original, ExternalCollections = cobros, AdjustmentsAmount = ajustes, BalanceAtCutoff = saldo, Outcome = resultado.Value,
                SourceLoanNumber = resultado == SaleReconciliationOutcome.Migrated ? numero : null, CreditLineCode = codigoDeLinea,
                CollectionsReference = referencia,
            }));
        }

        private Action<int>? Cupo(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var persona = Persona(fila, C.Persona);
            var tipo = Enumeracion<CreditLimitKind>(fila, C.Tipo);
            var linea = fila.Crudo(C.Linea) is null ? null : Linea(fila, C.Linea);
            if (tipo is CreditLimitKind.ByLine or CreditLimitKind.Revolving) fila.Requerido(C.Linea);
            var valor = NoNegativo(fila, C.Valor, fila.Monto(C.Valor), positivo: true);
            var desde = fila.Fecha(C.VigenteDesde);
            var hasta = fila.Fecha(C.VigenteHasta);
            if (hasta < desde) fila.Error(C.VigenteHasta, ImportErrors.CellFormat, "La vigencia no puede terminar antes de empezar.");
            var motivo = fila.Texto(C.Motivo, 200);
            var preferencia = Enumeracion<PrepaymentChoice>(fila, C.PreferenciaDeAbono);
            if (persona is null || tipo is null || desde is null || motivo is null) return null;
            var codigo = linea?.Codigo;
            var llave = Llave(persona.Value, tipo.Value, codigo, desde.Value);
            if (!LlaveNueva(ctx, fila, llave, C.Persona)) return null;
            return Registrar(ctx, fila, llave, id => db.MigrationCreditLimitRows.Add(new MigrationCreditLimitRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, PersonId = persona.Value, LimitKind = tipo.Value, CreditLineCode = codigo, Amount = valor,
                ValidFrom = desde.Value, ValidTo = hasta, Reason = motivo, PrepaymentChoice = preferencia,
            }));
        }

        private static readonly DebtorBlockReason[] BloqueosMigrables =
            [DebtorBlockReason.Manual, DebtorBlockReason.AssociateWithdrawal, DebtorBlockReason.Deceased, DebtorBlockReason.Insolvency, DebtorBlockReason.Garnishment];

        private Action<int>? Bloqueo(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var persona = Persona(fila, C.Persona);
            var motivo = Enumeracion<DebtorBlockReason>(fila, C.Motivo);
            if (motivo is { } m && !BloqueosMigrables.Contains(m))
                fila.Error(C.Motivo, ImportErrors.CellFormat, "Ese bloqueo es automático y no se migra: Cartera lo deriva después de activar.");
            var numero = fila.Crudo(C.NumeroDeCredito) is null ? null : Referencia(fila, C.NumeroDeCredito);
            var desde = fila.Fecha(C.Desde);
            var hasta = fila.Fecha(C.Hasta);
            var soporte = fila.Texto(C.Soporte, 200);
            var aseguradora = Persona(fila, C.Aseguradora);
            var reclamacion = Enumeracion<InsuranceClaimStatus>(fila, C.EstadoDeLaReclamacion);
            var insolvencia = Enumeracion<InsolvencyProceedingStatus>(fila, C.EstadoDeLaInsolvencia);
            if (motivo == DebtorBlockReason.Deceased) fila.Requerido(C.Aseguradora);
            if (persona is null || motivo is null || desde is null || soporte is null) return null;
            var llave = Llave(persona.Value, motivo.Value, desde.Value);
            if (!LlaveNueva(ctx, fila, llave, C.Persona)) return null;
            return Registrar(ctx, fila, llave, id => db.MigrationBlockRows.Add(new MigrationBlockRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, PersonId = persona.Value, BlockReason = motivo.Value, SourceLoanNumber = numero,
                Since = desde.Value, Until = hasta, SupportReference = soporte, InsurerPersonId = aseguradora,
                InsuranceClaimStatus = motivo == DebtorBlockReason.Deceased ? reclamacion ?? InsuranceClaimStatus.InProcess : null,
                InsolvencyStatus = motivo == DebtorBlockReason.Insolvency ? insolvencia ?? InsolvencyProceedingStatus.Negotiating : null,
            }));
        }

        private static readonly IReadOnlyDictionary<string, string> EstadosDeAutorizacion =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["accepted"] = "Accepted", ["aceptada"] = "Accepted", ["revoked"] = "Revoked", ["revocada"] = "Revoked" };

        private Action<int>? Autorizacion(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var persona = Persona(fila, C.Persona);
            var finalidad = Enumeracion<DataAuthorizationPurpose>(fila, C.Finalidad);
            var canal = Enumeracion<DataAuthorizationChannel>(fila, C.Canal);
            if (finalidad == DataAuthorizationPurpose.ContactChannel) fila.Requerido(C.Canal);
            var estadoCrudo = fila.Texto(C.Estado, 20);
            string estado = "Accepted";
            if (estadoCrudo is not null)
            {
                if (EstadosDeAutorizacion.TryGetValue(estadoCrudo, out var e)) estado = e;
                else fila.Error(C.Estado, ImportErrors.CellFormat, "Admite Accepted o Revoked.");
            }
            var fecha = fila.Fecha(C.Fecha);
            var version = fila.Texto(C.VersionDelTexto, 30);
            var evidencia = fila.Texto(C.Evidencia, 200);
            var obtencion = fila.Texto(C.CanalDeObtencion, 60);
            if (persona is null || finalidad is null || fecha is null) return null;
            var llave = Llave(persona.Value, finalidad.Value.ToString(), finalidad == DataAuthorizationPurpose.ContactChannel ? canal?.ToString() : null);
            if (!LlaveNueva(ctx, fila, llave, C.Finalidad)) return null;
            return Registrar(ctx, fila, llave, id => db.MigrationAuthorizationRows.Add(new MigrationAuthorizationRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, PersonId = persona.Value, Purpose = finalidad.Value.ToString(),
                ContactChannel = finalidad == DataAuthorizationPurpose.ContactChannel ? canal?.ToString() : null, AuthorizedOn = fecha.Value, Status = estado,
                PolicyVersionText = version, EvidenceReference = evidencia, HasEvidence = evidencia is not null, ObtainedThrough = obtencion,
            }));
        }

        private Action<int>? AvisoPrevio(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var episodio = fila.Fecha(C.Episodio);
            var exigidas = fila.Entero(C.ComunicacionesExigidas);
            if (exigidas is not (null or 1 or 2)) fila.Error(C.ComunicacionesExigidas, ImportErrors.CellFormat, "Admite 1 o 2.");
            var envio1 = fila.Fecha(C.FechaDeEnvio1);
            var canal1 = Enumeracion<MessageChannel>(fila, C.Canal1);
            var referencia1 = fila.Texto(C.Referencia1, 100);
            var envio2 = fila.Fecha(C.FechaDeEnvio2);
            var canal2 = Enumeracion<MessageChannel>(fila, C.Canal2);
            var referencia2 = fila.Texto(C.Referencia2, 100);
            if (exigidas == 2)
            {
                fila.Requerido(C.FechaDeEnvio2);
                fila.Requerido(C.Canal2);
                if (envio2 is not null && envio2 == envio1)
                    fila.Error(C.FechaDeEnvio2, ImportErrors.CellFormat, "La segunda comunicación va en un día distinto de la primera.");
            }
            if (numero is null || episodio is null || exigidas is null || envio1 is null || canal1 is null) return null;
            var llave = Llave(numero, episodio.Value);
            if (!LlaveNueva(ctx, fila, llave, C.Episodio)) return null;
            return Registrar(ctx, fila, llave, id => db.MigrationPriorNoticeRows.Add(new MigrationPriorNoticeRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, EpisodeStartDate = episodio.Value,
                RequiredCommunications = exigidas.Value, FirstSentOn = envio1.Value, FirstChannel = canal1.Value, FirstReference = referencia1,
                SecondSentOn = exigidas == 2 ? envio2 : null, SecondChannel = exigidas == 2 ? canal2 : null, SecondReference = exigidas == 2 ? referencia2 : null,
            }));
        }

        private Action<int>? Reporte(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var central = fila.Texto(C.Central, 20);
            var mes = Mes(fila, C.UltimoPeriodoReportado);
            var novedad = fila.Texto(C.NovedadReportada, 20);
            var negativo = fila.SiNo(C.DatoNegativo);
            var episodio = fila.Fecha(C.Episodio);
            var conAviso = fila.SiNoIndiferente(C.ConAvisoPrevio);
            if (negativo)
            {
                fila.Requerido(C.Episodio);
                fila.Requerido(C.ConAvisoPrevio);
            }
            if (numero is null || central is null || mes is null || novedad is null) return null;
            var llave = Llave(numero, central.ToUpperInvariant());
            if (!LlaveNueva(ctx, fila, llave, C.Central)) return null;
            return Registrar(ctx, fila, llave, id => db.MigrationBureauReportRows.Add(new MigrationBureauReportRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, BureauCode = central.ToUpperInvariant(),
                LastReportedYear = mes.Value.Anio, LastReportedMonth = mes.Value.Mes, LastReportedStatus = novedad, IsNegative = negativo,
                EpisodeStartDate = negativo ? episodio : null, WithPriorNotice = negativo ? conAviso : null,
            }));
        }

        private Action<int>? Reclamo(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var central = fila.Texto(C.Central, 20);
            var tipo = Enumeracion<CreditReportClaimKind>(fila, C.Tipo);
            var radicacion = fila.Fecha(C.FechaDeRadicacion);
            HastaElCorte(fila, C.FechaDeRadicacion, radicacion);
            var prorrogado = fila.SiNo(C.Prorrogado);
            var descripcion = fila.Texto(C.Descripcion, 400);
            if (numero is null || central is null || tipo is null || radicacion is null) return null;
            var llave = Llave(numero, central.ToUpperInvariant(), radicacion.Value);
            if (!LlaveNueva(ctx, fila, llave, C.FechaDeRadicacion)) return null;
            return Registrar(ctx, fila, llave, id => db.MigrationClaimRows.Add(new MigrationClaimRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, BureauCode = central.ToUpperInvariant(), Kind = tipo.Value,
                ReceivedOn = radicacion.Value, IsExtended = prorrogado, Description = descripcion,
            }));
        }

        private static readonly IReadOnlyDictionary<string, RelatedPartyBasis> EtiquetasDeFundamento = new Dictionary<string, RelatedPartyBasis>
        {
            ["consejero"] = RelatedPartyBasis.BoardMember, ["junta de vigilancia"] = RelatedPartyBasis.OversightBoardMember,
            ["representante legal"] = RelatedPartyBasis.LegalRepresentative, ["aportante significativo"] = RelatedPartyBasis.SignificantContributor,
            ["conyuge"] = RelatedPartyBasis.Spouse, ["pariente"] = RelatedPartyBasis.Relative,
        };

        private Action<int>? Vinculado(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var V = CatalogoDePlantillasDeCartera.ColumnasDeVinculados.Persona;
            var persona = Persona(fila, V);
            var fundamento = Enumeracion(fila, CatalogoDePlantillasDeCartera.ColumnasDeVinculados.Fundamento, EtiquetasDeFundamento);
            var relacionada = Persona(fila, CatalogoDePlantillasDeCartera.ColumnasDeVinculados.PersonaRelacionada);
            if (fundamento is RelatedPartyBasis.Spouse or RelatedPartyBasis.Relative) fila.Requerido(CatalogoDePlantillasDeCartera.ColumnasDeVinculados.PersonaRelacionada);
            var clase = Enumeracion<KinshipKind>(fila, CatalogoDePlantillasDeCartera.ColumnasDeVinculados.ClaseDeParentesco);
            var grado = fila.Entero(CatalogoDePlantillasDeCartera.ColumnasDeVinculados.Grado);
            if (fundamento == RelatedPartyBasis.Relative)
            {
                fila.Requerido(CatalogoDePlantillasDeCartera.ColumnasDeVinculados.ClaseDeParentesco);
                fila.Requerido(CatalogoDePlantillasDeCartera.ColumnasDeVinculados.Grado);
            }
            var porcentaje = fila.Porcentaje(CatalogoDePlantillasDeCartera.ColumnasDeVinculados.PorcentajeDeAportes);
            if (fundamento == RelatedPartyBasis.SignificantContributor) fila.Requerido(CatalogoDePlantillasDeCartera.ColumnasDeVinculados.PorcentajeDeAportes);
            var desde = fila.Fecha(CatalogoDePlantillasDeCartera.ColumnasDeVinculados.VigenteDesde);
            var hasta = fila.Fecha(CatalogoDePlantillasDeCartera.ColumnasDeVinculados.VigenteHasta);
            var soporte = fila.Texto(CatalogoDePlantillasDeCartera.ColumnasDeVinculados.Soporte, 200);
            if (persona is null || fundamento is null || desde is null || soporte is null) return null;
            var llave = Llave(persona.Value, fundamento.Value, relacionada, desde.Value);
            if (!LlaveNueva(ctx, fila, llave, V)) return null;
            return Registrar(ctx, fila, llave, id => db.MigrationRelatedPartyRows.Add(new MigrationRelatedPartyRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, PersonId = persona.Value, Basis = fundamento.Value, RelatedToPersonId = relacionada,
                KinshipKind = clase, KinshipDegree = grado, ContributionPercentage = porcentaje, ValidFrom = desde.Value, ValidTo = hasta, SupportReference = soporte,
            }));
        }

        private Action<int>? Historial(ContextoDeImportacion ctx, FilaDeImportacion fila)
        {
            var numero = Referencia(fila, C.NumeroDeCredito);
            var mes = Mes(fila, C.Mes);
            if (mes is { } m && new DateOnly(m.Anio, m.Mes, 1) >= new DateOnly(Corte.Year, Corte.Month, 1))
                fila.Error(C.Mes, ImportErrors.CellFormat, "El mes del historial es anterior al mes del corte.");
            var maxima = fila.Entero(C.MoraMaximaDias);
            var alCierre = fila.Entero(C.MoraAlCierreDias);
            if (maxima is < 0) fila.Error(C.MoraMaximaDias, ImportErrors.CellFormat, "No puede ser negativo.");
            if (alCierre is < 0) fila.Error(C.MoraAlCierreDias, ImportErrors.CellFormat, "No puede ser negativo.");
            if (numero is null || mes is null || maxima is null) return null;
            var llave = Llave(numero, mes.Value.Anio, mes.Value.Mes);
            if (!LlaveNueva(ctx, fila, llave, C.Mes)) return null;
            return Registrar(ctx, fila, llave, id => db.MigrationArrearsHistoryRows.Add(new MigrationArrearsHistoryRow
            {
                MigrationLoadPartId = id, RowNumber = fila.Numero, SourceLoanNumber = numero, PeriodYear = mes.Value.Anio, PeriodMonth = mes.Value.Mes,
                MaxDaysPastDue = maxima.Value, DaysPastDueAtMonthEnd = alCierre,
            }));
        }

        // ------------------------------------------------------------------------------------------- dentro del archivo --

        /// <summary>
        /// Lo que se mira con el archivo entero: los cuadres de un archivo (cuotas: fechas crecientes e interés sólo en las vencidas o en curso;
        /// saldos: el capital en cada crédito), los avisos de usura y las referencias a créditos que la carga todavía no trae.
        /// </summary>
        public async Task AvisarAsync(ContextoDeImportacion ctx)
        {
            foreach (var (numero, cuotas) in _cuotas)
            {
                foreach (var mala in CuadresDeMigracion.FechasNoCrecientes(cuotas))
                    FilaDe(ctx, mala.Fila)?.Error(C.FechaDeVencimiento, ImportErrors.CellFormat,
                        $"El vencimiento de la cuota {mala.Numero} del crédito {numero} no crece con la cuota.");
                foreach (var mala in CuadresDeMigracion.InteresFueraDeLaCuotaEnCurso(cuotas, Corte))
                    FilaDe(ctx, mala.Fila)?.Error(C.InteresCorriente, ImportErrors.CellFormat,
                        $"La cuota {mala.Numero} del crédito {numero} no está vencida ni en curso: no lleva interés corriente causado.");
            }

            foreach (var (numero, fila) in _primeraFilaDeSaldos.Where(kv => !_conPrincipal.Contains(kv.Key)))
                fila.Error(C.Concepto, ImportErrors.CellRequired, $"El crédito {numero} no trae su saldo de capital (Principal).");

            if (reglas is not null)
                foreach (var grupo in _tasas.GroupBy(t => t.Linea))
                {
                    var lineaId = int.Parse(grupo.Key, CultureInfo.InvariantCulture);
                    var version = _lineas.Values.First(l => l.Id == lineaId).Version!;
                    var combinacion = await reglas.CombinacionAsync(version.ModalityCode, version.SubsegmentCode, Corte, ct);
                    if (combinacion.IsFailure || combinacion.Value.UsuryKey is null) continue;
                    var usura = await reglas.UsuraAsync(combinacion.Value, Corte, ct);
                    if (usura.IsFailure) continue;
                    foreach (var (fila, _, tasa) in grupo.Where(t => t.Tasa > usura.Value.Tasa))
                        fila.Aviso(C.TasaCorrienteEA, Domain.Lending.Rates.ControlDeUsura.CodigoExcedida,
                            $"La tasa pactada ({tasa:P4}) supera la usura vigente al corte ({usura.Value.Tasa:P4}): el crédito se migra y el motor topa la tasa del día.");
                }

            foreach (var (fila, columna, numero) in _referencias)
                if (!_numerosDeLaCarga.Contains(numero) && !_numerosDelArchivo.Contains(numero))
                    fila.Aviso(columna, CodigoReferenciaPendiente,
                        $"El crédito {numero} todavía no está en la carga (partes de créditos o castigados): cárguelo antes de aplicar.");
        }

        private static FilaDeImportacion? FilaDe(ContextoDeImportacion ctx, int numero) => ctx.Datos.Filas.FirstOrDefault(f => f.Numero == numero);
    }
}
