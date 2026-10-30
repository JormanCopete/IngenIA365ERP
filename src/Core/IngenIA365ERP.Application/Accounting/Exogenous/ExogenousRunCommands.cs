using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Exogenous;

// Generación y exportación de la exógena (feature 009 E4, US11, FR-072..FR-075). Lee el libro por MovimientosContables, sin alcance de
// sucursal (la exógena es de la empresa): el movimiento del año para los selectores de movimiento (sin el cierre del ejercicio, que
// cancela los resultados) y el saldo al 31 de diciembre para «Balance». Suma por tercero y concepto, agrupa bajo la identificación de
// menores cuantías a quien no llega a la cuantía mínima (y lo que no trae tercero), toma una instantánea de la identificación de cada
// persona y marca las inconsistencias: el documento inválido bloquea el XML; dirección y municipio faltantes avisan. Regenerar da de
// baja la versión de trabajo; las exportadas se conservan con su archivo.

internal static class GeneracionDeExogena
{
    public const string TipoDeMenoresCuantias = "43";
    public const string NombreDeMenoresCuantias = "CUANTÍAS MENORES";
    public const string TipoNit = "31";

    public sealed record Inconsistencia(string Code, string Message, bool Blocking);

    public sealed record Valores(int? PersonId, int ConceptId, decimal[] Columnas);

    /// <summary>El valor de una suma de cuenta según el selector y la naturaleza de la cuenta.</summary>
    public static decimal Valor(string selector, AccountNature naturaleza, decimal debitos, decimal creditos, decimal baseDebitos, decimal baseCreditos) => selector switch
    {
        SelectoresDeExogena.Debito => debitos,
        SelectoresDeExogena.Credito => creditos,
        SelectoresDeExogena.BaseDebito => baseDebitos,
        SelectoresDeExogena.BaseCredito => baseCreditos,
        SelectoresDeExogena.Base => naturaleza == AccountNature.Credit ? baseCreditos - baseDebitos : baseDebitos - baseCreditos,
        _ => naturaleza == AccountNature.Credit ? creditos - debitos : debitos - creditos,
    };

    /// <summary>El dígito de verificación del NIT (módulo 11 con los pesos primos de la DIAN), para cuando la persona no lo tiene guardado.</summary>
    public static string? DigitoDeVerificacion(string nit)
    {
        if (nit.Length == 0 || nit.Length > 15 || !nit.All(char.IsDigit)) return null;
        int[] pesos = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];
        var suma = 0;
        for (var i = 0; i < nit.Length; i++) suma += (nit[nit.Length - 1 - i] - '0') * pesos[i];
        var residuo = suma % 11;
        return (residuo > 1 ? 11 - residuo : residuo).ToString(CultureInfo.InvariantCulture);
    }

    public static string Recortar(string? texto, int largo) => string.IsNullOrEmpty(texto) ? string.Empty : texto.Length <= largo ? texto : texto[..largo];
}

/// <summary>Genera la versión de trabajo de un formato del año.</summary>
public sealed record GenerateExogenousRunCommand(int Year, string FormatCode) : IRequest<Result<GeneracionDeExogenaDto>>;

public sealed class GenerateExogenousRunCommandValidator : AbstractValidator<GenerateExogenousRunCommand>
{
    public GenerateExogenousRunCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.FormatCode).NotEmpty().MaximumLength(10);
    }
}

public sealed class GenerateExogenousRunCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<GenerateExogenousRunCommand, Result<GeneracionDeExogenaDto>>
{
    public async Task<Result<GeneracionDeExogenaDto>> Handle(GenerateExogenousRunCommand request, CancellationToken ct)
    {
        var formato = await db.ExogenousFormats.AsNoTracking().Include(f => f.Concepts).ThenInclude(c => c.Accounts)
            .FirstOrDefaultAsync(f => f.TaxYear == request.Year && f.FormatCode == request.FormatCode && !f.IsDeleted, ct);
        if (formato is null) return Result.Failure<GeneracionDeExogenaDto>(ExogenousErrors.FormatNotFound);
        if (!formato.Applies) return Result.Failure<GeneracionDeExogenaDto>(ExogenousErrors.NotApplicable);
        var disposicion = DisposicionDeExogena.Leer(formato.LayoutJson);
        if (disposicion is null || disposicion.Columns.Count == 0) return Result.Failure<GeneracionDeExogenaDto>(ExogenousErrors.LayoutMissing);

        var conceptos = formato.Concepts.Where(c => !c.IsDeleted).ToList();
        var filas = conceptos.SelectMany(c => c.Accounts.Where(a => !a.IsDeleted).Select(a => (Concepto: c, Cuenta: a))).ToList();
        var plan = await db.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted).Select(a => new { a.Id, a.Code, a.Nature }).ToListAsync(ct);
        var cuentasDe = filas.ToDictionary(f => f.Cuenta, f => plan.Where(a => a.Code.StartsWith(f.Cuenta.AccountPrefix, StringComparison.Ordinal)).ToList());
        var naturaleza = plan.ToDictionary(a => a.Id, a => a.Nature);
        var deMovimiento = filas.Where(f => f.Cuenta.Selector != SelectoresDeExogena.Saldo).SelectMany(f => cuentasDe[f.Cuenta]).Select(a => a.Id).Distinct().ToList();
        var deSaldo = filas.Where(f => f.Cuenta.Selector == SelectoresDeExogena.Saldo).SelectMany(f => cuentasDe[f.Cuenta]).Select(a => a.Id).Distinct().ToList();

        var desde = new DateOnly(request.Year, 1, 1);
        var hasta = new DateOnly(request.Year, 12, 31);
        var movimiento = await MovimientosContables.PrepararAsync(db, AlcanceDeSucursales.SinRestriccion, clock, new FiltrosDeInforme { From = desde, To = hasta }, ct);
        if (movimiento.IsFailure) return Result.Failure<GeneracionDeExogenaDto>(movimiento.Error);
        var saldos = await MovimientosContables.PrepararAsync(db, AlcanceDeSucursales.SinRestriccion, clock, new FiltrosDeInforme { From = desde, To = hasta, IncludeClosing = true }, ct);
        if (saldos.IsFailure) return Result.Failure<GeneracionDeExogenaDto>(saldos.Error);

        var sumasDeMovimiento = deMovimiento.Count == 0 ? [] : await MovimientosContables.DelRango(MovimientosContables.Base(db, movimiento.Value), movimiento.Value)
            .Where(e => deMovimiento.Contains(e.AccountId))
            .GroupBy(e => new { e.AccountId, e.PersonId })
            .Select(g => new Suma(g.Key.AccountId, g.Key.PersonId, g.Sum(e => e.Debit), g.Sum(e => e.Credit),
                g.Sum(e => e.Debit > 0 ? e.TaxBase ?? 0m : 0m), g.Sum(e => e.Credit > 0 ? e.TaxBase ?? 0m : 0m)))
            .ToListAsync(ct);
        var sumasDeSaldo = deSaldo.Count == 0 ? [] : await MovimientosContables.HastaInclusive(MovimientosContables.Base(db, saldos.Value), hasta)
            .Where(e => deSaldo.Contains(e.AccountId))
            .GroupBy(e => new { e.AccountId, e.PersonId })
            .Select(g => new Suma(g.Key.AccountId, g.Key.PersonId, g.Sum(e => e.Debit), g.Sum(e => e.Credit), 0m, 0m))
            .ToListAsync(ct);

        // Valores por (tercero, concepto): cada fila de cuenta del concepto suma en su columna.
        var valores = new Dictionary<(int?, int), decimal[]>();
        foreach (var (concepto, cuenta) in filas)
        {
            var indice = disposicion.Indice(cuenta.ValueField);
            if (indice < 0) continue;
            var ids = cuentasDe[cuenta].Select(a => a.Id).ToHashSet();
            var fuente = cuenta.Selector == SelectoresDeExogena.Saldo ? sumasDeSaldo : sumasDeMovimiento;
            foreach (var s in fuente.Where(s => ids.Contains(s.AccountId)))
            {
                var v = GeneracionDeExogena.Valor(cuenta.Selector, naturaleza[s.AccountId], s.Debitos, s.Creditos, s.BaseDebitos, s.BaseCreditos);
                if (v == 0m) continue;
                if (!valores.TryGetValue((s.PersonId, concepto.Id), out var columnas)) valores[(s.PersonId, concepto.Id)] = columnas = new decimal[DisposicionDeExogena.MaximoDeColumnas];
                columnas[indice] += v;
            }
        }

        var idsDePersona = valores.Keys.Where(k => k.Item1 is not null).Select(k => k.Item1!.Value).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Include(p => p.City).ThenInclude(c => c!.Department)
            .Where(p => idsDePersona.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var version = (await db.ExogenousRuns.Where(r => r.FormatId == formato.Id).MaxAsync(r => (int?)r.Version, ct) ?? 0) + 1;
        var corrida = new ExogenousRun { TaxYear = request.Year, FormatId = formato.Id, Version = version, Status = ExogenousRunStatus.Working, GeneratedAt = ahora, GeneratedBy = quien, CreatedAt = ahora, CreatedBy = quien };

        var usaDireccion = disposicion.Identity.Contains("dir");
        foreach (var concepto in conceptos.OrderBy(c => c.ConceptCode))
        {
            var menores = new decimal[DisposicionDeExogena.MaximoDeColumnas];
            var hayMenores = false;
            var sinTercero = false;
            foreach (var ((personaId, conceptoId), columnas) in valores.Where(v => v.Key.Item2 == concepto.Id).OrderBy(v => v.Key.Item1))
            {
                var total = columnas.Sum(Math.Abs);
                if (personaId is null || (formato.MinorAmountsRule && total < formato.MinAmount))
                {
                    for (var i = 0; i < columnas.Length; i++) menores[i] += columnas[i];
                    hayMenores = true;
                    sinTercero |= personaId is null;
                    continue;
                }
                var p = personas[personaId.Value];
                var tipo = TiposDeDocumento.PorCodigoDePersona(p.IdType)?.CodigoDian;
                var problemas = new List<GeneracionDeExogena.Inconsistencia>();
                var numero = (p.TaxId ?? string.Empty).Trim();
                if (tipo is null) problemas.Add(new("Accounting.Exogenous.IdTypeUnknown", $"El tipo de documento «{p.IdType}» no tiene código DIAN.", true));
                if (numero.Length == 0 || (tipo is not "41" && !numero.All(char.IsDigit)))
                    problemas.Add(new("Accounting.Exogenous.InvalidDocument", "El número de documento está vacío o tiene caracteres no permitidos.", true));
                string? dv = null;
                if (tipo == GeneracionDeExogena.TipoNit)
                {
                    dv = string.IsNullOrWhiteSpace(p.TaxIdCheckDigit) ? GeneracionDeExogena.DigitoDeVerificacion(numero) : p.TaxIdCheckDigit!.Trim();
                    if (string.IsNullOrWhiteSpace(p.TaxIdCheckDigit)) problemas.Add(new("Accounting.Exogenous.CheckDigitComputed", "El NIT no tiene dígito de verificación guardado: se calculó.", false));
                }
                if (usaDireccion && string.IsNullOrWhiteSpace(p.Address)) problemas.Add(new("Accounting.Exogenous.AddressMissing", "Falta la dirección.", false));
                if (usaDireccion && p.City?.DaneCode is null) problemas.Add(new("Accounting.Exogenous.MunicipalityMissing", "Falta el municipio (código DIVIPOLA).", false));

                var juridica = tipo == GeneracionDeExogena.TipoNit || p.PersonType == "02";
                corrida.Lines.Add(Linea(corrida, concepto.Id, columnas, ahora, quien, p.Id, tipo, numero, dv,
                    juridica ? null : p.LastName, juridica ? null : p.SecondLastName, juridica ? null : p.FirstName, juridica ? null : p.OtherNames,
                    juridica ? (string.IsNullOrWhiteSpace(p.BusinessName) ? NombreDePersonaCompleto(p) : p.BusinessName) : null,
                    p.Address, p.City?.DaneCode, p.City?.Department?.Code, problemas));
            }
            if (hayMenores)
            {
                var avisos = sinTercero
                    ? new List<GeneracionDeExogena.Inconsistencia> { new("Accounting.Exogenous.WithoutThirdParty", "Incluye movimientos sin tercero: revise si esas cuentas deberían exigirlo.", false) }
                    : [];
                corrida.Lines.Add(Linea(corrida, concepto.Id, menores, ahora, quien, null, GeneracionDeExogena.TipoDeMenoresCuantias, formato.MinorAmountsTaxId, null,
                    null, null, null, null, GeneracionDeExogena.NombreDeMenoresCuantias, null, null, null, avisos));
            }
        }
        corrida.IssueCount = corrida.Lines.Sum(l => Problemas(l).Count);
        corrida.BlockingIssueCount = corrida.Lines.Sum(l => Problemas(l).Count(p => p.Blocking));

        // La versión de trabajo anterior se da de baja; las exportadas quedan.
        foreach (var anterior in await db.ExogenousRuns.Where(r => r.FormatId == formato.Id && !r.IsDeleted && r.Status == ExogenousRunStatus.Working).ToListAsync(ct))
        {
            anterior.IsDeleted = true;
            anterior.DeletedAt = ahora;
            anterior.DeletedBy = quien;
        }
        db.ExogenousRuns.Add(corrida);
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Exogenous.Generated", nameof(ExogenousRun), corrida.PublicId, null,
            new { request.Year, request.FormatCode, version, lines = corrida.Lines.Count, issues = corrida.IssueCount, blocking = corrida.BlockingIssueCount }, ct);
        return Result.Success(ArmadoDeGeneraciones.Dto(corrida, formato, corrida.Lines.Count));
    }

    private sealed record Suma(int AccountId, int? PersonId, decimal Debitos, decimal Creditos, decimal BaseDebitos, decimal BaseCreditos);

    private static string NombreDePersonaCompleto(Domain.Entities.Core.Person p) => Domain.Entities.Core.NombreDePersona.Completo(p);

    private static ExogenousRunLine Linea(ExogenousRun corrida, int conceptId, decimal[] v, DateTime ahora, string quien, int? personId, string? tipo, string numero, string? dv,
        string? apellido1, string? apellido2, string? nombre1, string? nombre2, string? razon, string? direccion, string? municipio, string? departamento,
        IReadOnlyList<GeneracionDeExogena.Inconsistencia> problemas) => new()
    {
        Run = corrida, ConceptId = conceptId, PersonId = personId, IdType = tipo, IdNumber = GeneracionDeExogena.Recortar(numero, 20), CheckDigit = dv,
        Surname1 = GeneracionDeExogena.Recortar(apellido1, 60), Surname2 = GeneracionDeExogena.Recortar(apellido2, 60),
        Name1 = GeneracionDeExogena.Recortar(nombre1, 60), Name2 = GeneracionDeExogena.Recortar(nombre2, 60),
        BusinessName = GeneracionDeExogena.Recortar(razon, 150), Address = GeneracionDeExogena.Recortar(direccion, 200),
        MunicipalityCode = GeneracionDeExogena.Recortar(municipio, 5), DepartmentCode = GeneracionDeExogena.Recortar(departamento, 2),
        Value1 = v[0], Value2 = v[1], Value3 = v[2], Value4 = v[3], Value5 = v[4], Value6 = v[5], Value7 = v[6], Value8 = v[7], Value9 = v[8], Value10 = v[9],
        IssuesJson = problemas.Count == 0 ? null : JsonSerializer.Serialize(problemas.Select(p => new { code = p.Code, message = p.Message, blocking = p.Blocking })),
        CreatedAt = ahora, CreatedBy = quien,
    };

    internal static IReadOnlyList<GeneracionDeExogena.Inconsistencia> Problemas(ExogenousRunLine l) =>
        string.IsNullOrEmpty(l.IssuesJson) ? [] :
        JsonSerializer.Deserialize<List<GeneracionDeExogena.Inconsistencia>>(l.IssuesJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
}

internal static class ArmadoDeGeneraciones
{
    public static GeneracionDeExogenaDto Dto(ExogenousRun r, ExogenousFormat f, int lineas) => new(r.PublicId, r.TaxYear, f.FormatCode, f.Name, r.Version, r.Status.ToString(),
        r.GeneratedAt, r.GeneratedBy, r.ExportedAt, r.ExportedBy, r.XmlAttachmentPublicId, lineas, r.IssueCount, r.BlockingIssueCount);

    public static decimal[] Valores(ExogenousRunLine l) => [l.Value1, l.Value2, l.Value3, l.Value4, l.Value5, l.Value6, l.Value7, l.Value8, l.Value9, l.Value10];
}

/// <summary>Las generaciones del año (la de trabajo y las exportadas), de la más reciente a la más antigua.</summary>
public sealed record ListExogenousRunsQuery(int Year) : IRequest<Result<IReadOnlyList<GeneracionDeExogenaDto>>>;

public sealed class ListExogenousRunsQueryValidator : AbstractValidator<ListExogenousRunsQuery>
{
    public ListExogenousRunsQueryValidator() => RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
}

public sealed class ListExogenousRunsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListExogenousRunsQuery, Result<IReadOnlyList<GeneracionDeExogenaDto>>>
{
    public async Task<Result<IReadOnlyList<GeneracionDeExogenaDto>>> Handle(ListExogenousRunsQuery request, CancellationToken ct)
    {
        var corridas = await db.ExogenousRuns.AsNoTracking().Include(r => r.Format)
            .Where(r => r.TaxYear == request.Year && (!r.IsDeleted || r.Status == ExogenousRunStatus.Exported))
            .OrderBy(r => r.Format!.FormatCode).ThenByDescending(r => r.Version).ToListAsync(ct);
        var ids = corridas.Select(r => r.Id).ToList();
        var lineas = await db.ExogenousRunLines.AsNoTracking().Where(l => ids.Contains(l.RunId)).GroupBy(l => l.RunId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return Result.Success<IReadOnlyList<GeneracionDeExogenaDto>>(corridas.Select(r => ArmadoDeGeneraciones.Dto(r, r.Format!, lineas.GetValueOrDefault(r.Id))).ToList());
    }
}

/// <summary>Las inconsistencias de una generación, con el tercero para abrirlo en Personas.</summary>
public sealed record ListExogenousIssuesQuery(Guid RunPublicId) : IRequest<Result<IReadOnlyList<InconsistenciaDeExogenaDto>>>;

public sealed class ListExogenousIssuesQueryValidator : AbstractValidator<ListExogenousIssuesQuery>
{
    public ListExogenousIssuesQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ListExogenousIssuesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListExogenousIssuesQuery, Result<IReadOnlyList<InconsistenciaDeExogenaDto>>>
{
    public async Task<Result<IReadOnlyList<InconsistenciaDeExogenaDto>>> Handle(ListExogenousIssuesQuery request, CancellationToken ct)
    {
        var corrida = await db.ExogenousRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (corrida is null) return Result.Failure<IReadOnlyList<InconsistenciaDeExogenaDto>>(ExogenousErrors.RunNotFound);
        var lineas = await db.ExogenousRunLines.AsNoTracking().Include(l => l.Concept).Include(l => l.Person)
            .Where(l => l.RunId == corrida.Id && l.IssuesJson != null).ToListAsync(ct);
        var lista = lineas.SelectMany(l => GenerateExogenousRunCommandHandler.Problemas(l).Select(p => new InconsistenciaDeExogenaDto(l.Person?.PublicId, l.IdNumber,
                l.BusinessName ?? string.Join(' ', new[] { l.Name1, l.Name2, l.Surname1, l.Surname2 }.Where(x => !string.IsNullOrWhiteSpace(x))),
                l.Concept!.ConceptCode, p.Code, p.Message, p.Blocking)))
            .OrderByDescending(i => i.Blocking).ThenBy(i => i.IdNumber).ToList();
        return Result.Success<IReadOnlyList<InconsistenciaDeExogenaDto>>(lista);
    }
}

/// <summary>Una generación como tabla (para la pantalla y el Excel de revisión): identificación, concepto, columnas de valor e inconsistencias.</summary>
public sealed record ExogenousRunTableQuery(Guid RunPublicId, string? Format) : IRequest<Result<TablaExportable>>;

public sealed class ExogenousRunTableQueryValidator : AbstractValidator<ExogenousRunTableQuery>
{
    public ExogenousRunTableQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ExogenousRunTableQueryHandler(IApplicationDbContext db, AccountingAuditEmitter audit) : IRequestHandler<ExogenousRunTableQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(ExogenousRunTableQuery request, CancellationToken ct)
    {
        var corrida = await db.ExogenousRuns.AsNoTracking().Include(r => r.Format).FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (corrida is null) return Result.Failure<TablaExportable>(ExogenousErrors.RunNotFound);
        var disposicion = DisposicionDeExogena.Leer(corrida.Format!.LayoutJson);
        var etiquetas = disposicion?.Columns.Select(c => c.Label).ToList() ?? [];
        var columnas = new List<ColumnaExportable>
        {
            new("Concepto"), new("Tipo doc."), new("Número"), new("DV"), new("Primer apellido"), new("Segundo apellido"), new("Primer nombre"), new("Otros nombres"),
            new("Razón social"), new("Dirección"), new("Dpto."), new("Municipio"),
        };
        columnas.AddRange(etiquetas.Select(e => new ColumnaExportable(e, TipoDeColumna.Moneda)));
        columnas.Add(new ColumnaExportable("Inconsistencias"));
        columnas.Add(new ColumnaExportable("Persona", Clave: "_persona"));

        var lineas = await db.ExogenousRunLines.AsNoTracking().Include(l => l.Concept).Include(l => l.Person)
            .Where(l => l.RunId == corrida.Id).OrderBy(l => l.Concept!.ConceptCode).ThenBy(l => l.IdNumber).ToListAsync(ct);
        var filas = lineas.Select(l =>
        {
            var valores = new List<object?>
            {
                l.Concept!.ConceptCode, l.IdType, l.IdNumber, l.CheckDigit, l.Surname1, l.Surname2, l.Name1, l.Name2, l.BusinessName, l.Address, l.DepartmentCode, l.MunicipalityCode,
            };
            var v = ArmadoDeGeneraciones.Valores(l);
            for (var i = 0; i < etiquetas.Count; i++) valores.Add(v[i]);
            valores.Add(string.Join(" ", GenerateExogenousRunCommandHandler.Problemas(l).Select(p => (p.Blocking ? "[bloquea] " : "") + p.Message)));
            valores.Add(l.Person?.PublicId);
            return new FilaExportable(valores, Resaltada: GenerateExogenousRunCommandHandler.Problemas(l).Any(p => p.Blocking));
        }).ToList();
        var formato = FormatosDeInforme.Normalizar(request.Format);
        var tabla = new TablaExportable($"Exógena {corrida.Format.FormatCode} · {corrida.Format.Name}", $"Año gravable {corrida.TaxYear} · versión {corrida.Version}",
            columnas, filas, null,
            [
                $"Generada el {corrida.GeneratedAt:dd/MM/yyyy HH:mm} por {corrida.GeneratedBy}. {corrida.BlockingIssueCount} inconsistencia(s) bloqueante(s) de {corrida.IssueCount}.",
                "Los formatos de la semilla están pendientes de validar con el contador y la resolución vigente.",
            ]);
        if (FormatosDeInforme.EsExportacion(request.Format))
            await audit.EmitirExportacionAsync($"exogenous/{corrida.Format.FormatCode}", new { corrida.TaxYear, corrida.Version }, formato, filas.Count, ct);
        return Result.Success(tabla);
    }
}

/// <summary>
/// El archivo XML del prevalidador (FR-074): rechaza con bloqueantes; la primera vez guarda el archivo como adjunto del módulo y deja la
/// versión <c>Exported</c> con fecha y usuario; las siguientes devuelven el mismo archivo.
/// </summary>
public sealed record ExportExogenousXmlCommand(Guid RunPublicId) : IRequest<Result<ArchivoDeExogena>>;

public sealed class ExportExogenousXmlCommandValidator : AbstractValidator<ExportExogenousXmlCommand>
{
    public ExportExogenousXmlCommandValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ExportExogenousXmlCommandHandler(IApplicationDbContext db, ISender sender, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<ExportExogenousXmlCommand, Result<ArchivoDeExogena>>
{
    public const string OwnerEntityType = nameof(ExogenousRun);

    public async Task<Result<ArchivoDeExogena>> Handle(ExportExogenousXmlCommand request, CancellationToken ct)
    {
        var corrida = await db.ExogenousRuns.Include(r => r.Format).FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (corrida is null) return Result.Failure<ArchivoDeExogena>(ExogenousErrors.RunNotFound);
        if (corrida.IsDeleted && corrida.Status != ExogenousRunStatus.Exported) return Result.Failure<ArchivoDeExogena>(ExogenousErrors.RunNotWorking);
        if (corrida.BlockingIssueCount > 0) return Result.Failure<ArchivoDeExogena>(ExogenousErrors.BlockingIssues);
        var disposicion = DisposicionDeExogena.Leer(corrida.Format!.LayoutJson);
        if (disposicion is null) return Result.Failure<ArchivoDeExogena>(ExogenousErrors.LayoutMissing);

        var primera = corrida.Status != ExogenousRunStatus.Exported;
        var envio = primera
            ? await db.ExogenousRuns.CountAsync(r => r.FormatId == corrida.FormatId && r.Status == ExogenousRunStatus.Exported, ct) + 1
            : await db.ExogenousRuns.CountAsync(r => r.FormatId == corrida.FormatId && r.Status == ExogenousRunStatus.Exported && r.ExportedAt <= corrida.ExportedAt, ct);
        var fecha = primera ? clock.UtcNow : corrida.ExportedAt!.Value;
        var lineas = await db.ExogenousRunLines.AsNoTracking().Include(l => l.Concept).Where(l => l.RunId == corrida.Id).OrderBy(l => l.Concept!.ConceptCode).ThenBy(l => l.IdNumber).ToListAsync(ct);
        var contenido = XmlDeExogena.Escribir(corrida.Format, disposicion, lineas, envio, fecha);
        var nombre = XmlDeExogena.NombreDeArchivo(corrida.Format, envio);

        if (primera)
        {
            var subida = await sender.Send(new UploadAttachmentCommand(OwnerEntityType, corrida.PublicId, nombre, "application/xml", contenido), ct);
            if (subida.IsFailure) return Result.Failure<ArchivoDeExogena>(subida.Error);
            corrida.XmlAttachmentPublicId = subida.Value;
            corrida.Status = ExogenousRunStatus.Exported;
            corrida.ExportedAt = fecha;
            corrida.ExportedBy = user.UserName ?? "system";
            await db.SaveChangesAsync(ct);
            await audit.EmitAsync("Accounting.Exogenous.Exported", nameof(ExogenousRun), corrida.PublicId, null,
                new { corrida.TaxYear, corrida.Format.FormatCode, corrida.Version, envio, file = nombre, lines = lineas.Count }, ct);
        }
        return Result.Success(new ArchivoDeExogena(contenido, nombre));
    }
}

/// <summary>
/// El XML del prevalidador de la DIAN: encabezado <c>Cab</c> (año, concepto de envío, formato, versión, número de envío, fechas, valor total
/// y cantidad de registros) y una fila por línea con los atributos que dice la disposición, en ISO-8859-1. Los valores van en pesos
/// enteros. El nombre es <c>Dmuisca_</c> + concepto (2) + formato (5) + versión (2) + año (4) + envío (8), 33 caracteres con la extensión.
/// </summary>
public static class XmlDeExogena
{
    private const int ConceptoDeInsercion = 1;

    public static string NombreDeArchivo(ExogenousFormat f, int envio) =>
        $"Dmuisca_{ConceptoDeInsercion:00}{int.Parse(f.FormatCode, CultureInfo.InvariantCulture):00000}{f.FormatVersion:00}{f.TaxYear:0000}{envio:00000000}.xml";

    public static byte[] Escribir(ExogenousFormat f, DisposicionDeExogena d, IReadOnlyList<ExogenousRunLine> lineas, int envio, DateTime fecha)
    {
        var total = lineas.Sum(l => ArmadoDeGeneraciones.Valores(l).Take(d.Columns.Count).Sum(v => Math.Round(v, 0, MidpointRounding.AwayFromZero)));
        var ajustes = new XmlWriterSettings { Encoding = Encoding.Latin1, Indent = true };
        using var memoria = new MemoryStream();
        using (var xml = XmlWriter.Create(memoria, ajustes))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("mas");
            xml.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
            xml.WriteAttributeString("xsi", "noNamespaceSchemaLocation", "http://www.w3.org/2001/XMLSchema-instance", $"../xsd/{f.FormatCode}.xsd");
            xml.WriteStartElement("Cab");
            xml.WriteElementString("Ano", f.TaxYear.ToString(CultureInfo.InvariantCulture));
            xml.WriteElementString("CodCpt", ConceptoDeInsercion.ToString(CultureInfo.InvariantCulture));
            xml.WriteElementString("Formato", f.FormatCode);
            xml.WriteElementString("Version", f.FormatVersion.ToString(CultureInfo.InvariantCulture));
            xml.WriteElementString("NumEnvio", envio.ToString(CultureInfo.InvariantCulture));
            xml.WriteElementString("FecEnvio", fecha.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
            xml.WriteElementString("FecInicial", $"{f.TaxYear:0000}-01-01");
            xml.WriteElementString("FecFinal", $"{f.TaxYear:0000}-12-31");
            xml.WriteElementString("ValorTotal", total.ToString("0", CultureInfo.InvariantCulture));
            xml.WriteElementString("CantReg", lineas.Count.ToString(CultureInfo.InvariantCulture));
            xml.WriteEndElement();
            foreach (var l in lineas)
            {
                xml.WriteStartElement(d.Element);
                if (!string.IsNullOrWhiteSpace(d.ConceptAttribute)) xml.WriteAttributeString(d.ConceptAttribute, l.Concept!.ConceptCode);
                foreach (var atributo in d.Identity)
                    if (Identidad(l, atributo) is { Length: > 0 } valor) xml.WriteAttributeString(atributo, valor);
                var v = ArmadoDeGeneraciones.Valores(l);
                for (var i = 0; i < d.Columns.Count; i++)
                    xml.WriteAttributeString(d.Columns[i].Xml, Math.Round(v[i], 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture));
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
            xml.WriteEndDocument();
        }
        return memoria.ToArray();
    }

    /// <summary>El valor de un atributo de identificación de la línea; el municipio va con sus tres dígitos, como lo pide la DIAN.</summary>
    private static string? Identidad(ExogenousRunLine l, string atributo) => atributo switch
    {
        "tdoc" => l.IdType,
        "nid" => l.IdNumber,
        "dv" => l.CheckDigit,
        "apl1" => l.Surname1,
        "apl2" => l.Surname2,
        "nom1" => l.Name1,
        "nom2" => l.Name2,
        "raz" => l.BusinessName,
        "dir" => l.Address,
        "dpto" => l.DepartmentCode,
        "mun" => l.MunicipalityCode is { Length: 5 } m ? m[2..] : l.MunicipalityCode,
        // Colombia en la tabla de países de la DIAN; un tercero del exterior se corrige a mano en el Excel de revisión hasta que Personas guarde el país.
        "pais" => "169",
        _ => null,
    };
}
