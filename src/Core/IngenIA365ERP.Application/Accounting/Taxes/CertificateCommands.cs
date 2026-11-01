using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Taxes;

// Certificados de retención (feature 009 E4, US10, FR-067..FR-069). Salen del libro por MovimientosContables, sin alcance de sucursal
// (el certificado es de la empresa): líneas contabilizadas, con tercero, en cuentas de la clase certificada (fuente, IVA o ICA), del año o
// de los meses pedidos. Las de los módulos que expiden sus propios certificados quedan fuera (AccountingSetup.CertificatesExcludedModules,
// por defecto CAR: Captaciones y Cartera, decisión del dueño del 2026-10-30). Cada certificado guarda la huella de las líneas que lo
// alimentaron; si el libro cambia (una reversión), la lista lo muestra desactualizado y se reexpide con número nuevo, anulando el
// anterior. Reimprimir no cambia el número. El consecutivo es por clase.

/// <summary>Lo común de los certificados: qué líneas entran, sus totales por tercero y cuenta, la huella y el DTO.</summary>
public static class LibroDeCertificados
{
    public sealed record Alcance(TaxKind Kind, int Year, byte? PeriodFrom, byte? PeriodTo)
    {
        public DateOnly Desde => new(Year, PeriodFrom ?? 1, 1);
        public DateOnly Hasta => new DateOnly(Year, PeriodTo ?? 12, 1).AddMonths(1).AddDays(-1);
    }

    public sealed record LineaDelLibro(long Id, int PersonId, int AccountId, decimal Debit, decimal Credit, decimal? TaxBase);

    public sealed record Totales(int AccountId, decimal Base, decimal Amount);

    public static async Task<Result<IReadOnlyList<LineaDelLibro>>> LineasAsync(IApplicationDbContext db, IDateTimeService clock, Alcance a, int? personId, CancellationToken ct)
    {
        var excluidos = (await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted).Select(s => s.CertificatesExcludedModules).FirstOrDefaultAsync(ct) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(m => m.ToUpperInvariant()).ToList();
        var contexto = await MovimientosContables.PrepararAsync(db, AlcanceDeSucursales.SinRestriccion, clock, new FiltrosDeInforme { From = a.Desde, To = a.Hasta }, ct);
        if (contexto.IsFailure) return Result.Failure<IReadOnlyList<LineaDelLibro>>(contexto.Error);
        var q = MovimientosContables.DelRango(MovimientosContables.Base(db, contexto.Value), contexto.Value)
            .Where(e => e.PersonId != null && e.Account!.TaxKind == a.Kind && !excluidos.Contains(e.Document!.OriginModule));
        if (personId is { } p) q = q.Where(e => e.PersonId == p);
        var lineas = await q.Select(e => new LineaDelLibro(e.Id, e.PersonId!.Value, e.AccountId, e.Debit, e.Credit, e.TaxBase)).ToListAsync(ct);
        return Result.Success<IReadOnlyList<LineaDelLibro>>(lineas);
    }

    /// <summary>Por cuenta: lo retenido (créditos menos débitos: la retención es un pasivo) y su base con el mismo signo.</summary>
    public static IReadOnlyList<Totales> PorCuenta(IEnumerable<LineaDelLibro> lineas) =>
        lineas.GroupBy(l => l.AccountId)
            .Select(g => new Totales(g.Key,
                g.Sum(l => l.Credit > 0 ? l.TaxBase ?? 0m : -(l.TaxBase ?? 0m)),
                g.Sum(l => l.Credit - l.Debit)))
            .Where(t => t.Amount != 0m)
            .ToList();

    /// <summary>SHA-256 de las líneas (id, débito, crédito, base) en orden: cambia si una línea entra, sale o cambia.</summary>
    public static string Huella(IEnumerable<LineaDelLibro> lineas)
    {
        var texto = new StringBuilder();
        foreach (var l in lineas.OrderBy(l => l.Id))
            texto.Append(CultureInfo.InvariantCulture, $"{l.Id}:{l.Debit}:{l.Credit}:{l.TaxBase};");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }

    public static async Task<long> SiguienteNumeroAsync(IApplicationDbContext db, TaxKind kind, CancellationToken ct) =>
        (await db.WithholdingCertificates.Where(c => c.TaxKind == kind).MaxAsync(c => (long?)c.Number, ct) ?? 0L) + 1L;

    /// <summary>Arma el certificado (sin guardarlo) con sus líneas por cuenta; la tarifa es la vigente de la cuenta al final del período o, sin ella, la efectiva.</summary>
    public static WithholdingCertificate Nuevo(Alcance a, int personId, long numero, IReadOnlyList<Totales> totales, IReadOnlyDictionary<int, ChartOfAccount> cuentas,
        string huella, DateTime ahora, string quien)
    {
        var certificado = new WithholdingCertificate
        {
            PersonId = personId, TaxKind = a.Kind, Year = a.Year, PeriodFrom = a.PeriodFrom, PeriodTo = a.PeriodTo, Number = numero,
            IssuedAt = ahora, IssuedBy = quien, Status = CertificateStatus.Current, LedgerFingerprint = huella,
            TotalBase = totales.Sum(t => t.Base), TotalAmount = totales.Sum(t => t.Amount), CreatedAt = ahora, CreatedBy = quien,
        };
        foreach (var t in totales.OrderBy(t => cuentas[t.AccountId].Code))
        {
            var cuenta = cuentas[t.AccountId];
            var tarifa = TaxReportQueryHandler.TarifaA(cuenta, a.Hasta) ?? (t.Base != 0m ? Math.Round(t.Amount / t.Base, 6) : 0m);
            certificado.Lines.Add(new WithholdingCertificateLine
            {
                AccountId = cuenta.Id, ConceptCode = string.IsNullOrWhiteSpace(cuenta.TaxConceptCode) ? cuenta.Code : cuenta.TaxConceptCode!,
                Base = t.Base, Rate = tarifa, Amount = t.Amount, CreatedAt = ahora, CreatedBy = quien,
            });
        }
        return certificado;
    }

    public static async Task<IReadOnlyList<CertificadoDto>> DtosAsync(IApplicationDbContext db, IDateTimeService clock, IReadOnlyList<WithholdingCertificate> certificados, CancellationToken ct)
    {
        var ids = certificados.Select(c => c.PersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, Nombre = PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName), p.TaxId, p.Email })
            .ToDictionaryAsync(p => p.Id, ct);
        var reemplazos = certificados.Where(c => c.VoidedByCertificateId is not null).Select(c => c.VoidedByCertificateId!.Value).ToList();
        var publicos = await db.WithholdingCertificates.AsNoTracking().Where(c => reemplazos.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.PublicId, ct);

        // La huella vigente se recalcula una vez por alcance (clase, año y meses) y se compara por tercero.
        var vigentes = new Dictionary<(int, Alcance), string>();
        foreach (var grupo in certificados.Where(c => c.Status == CertificateStatus.Current).GroupBy(c => new Alcance(c.TaxKind, c.Year, c.PeriodFrom, c.PeriodTo)))
        {
            var lineas = await LineasAsync(db, clock, grupo.Key, null, ct);
            if (lineas.IsFailure) continue;
            foreach (var porPersona in lineas.Value.GroupBy(l => l.PersonId)) vigentes[(porPersona.Key, grupo.Key)] = Huella(porPersona);
        }

        return certificados.Select(c =>
        {
            var p = personas.GetValueOrDefault(c.PersonId);
            var alcance = new Alcance(c.TaxKind, c.Year, c.PeriodFrom, c.PeriodTo);
            var estado = c.Status == CertificateStatus.Current && vigentes.GetValueOrDefault((c.PersonId, alcance), Huella([])) != c.LedgerFingerprint
                ? CertificateStatus.Outdated : c.Status;
            return new CertificadoDto(c.PublicId, c.TaxKind.ToString(), c.Number, c.Year, c.PeriodFrom, c.PeriodTo, p?.PublicId ?? Guid.Empty, p?.Nombre ?? string.Empty,
                p?.TaxId ?? string.Empty, p?.Email, c.IssuedAt, c.IssuedBy, estado.ToString(), c.TotalBase, c.TotalAmount, c.LastSentAt, c.LastSentTo,
                c.VoidedByCertificateId is { } v ? publicos.GetValueOrDefault(v) : null);
        }).ToList();
    }

    public static Result<Alcance> Validar(string kind, int year, byte? desde, byte? hasta)
    {
        if (!Enum.TryParse<TaxKind>(kind, ignoreCase: true, out var clase) || !ClasesDeImpuesto.Certificables.Contains(clase)) return Result.Failure<Alcance>(TaxErrors.KindNotCertifiable);
        if ((desde is null) != (hasta is null) || desde is < 1 or > 12 || hasta is < 1 or > 12 || desde > hasta) return Result.Failure<Alcance>(TaxErrors.PeriodInvalid);
        return Result.Success(new Alcance(clase, year, desde, hasta));
    }
}

// ----------------------------------------------------------------------------------- generar --

/// <summary>
/// Genera los certificados de una clase y un año (o de unos meses del año): uno por tercero con retenciones, si no tiene ya uno vigente
/// del mismo alcance. Con <paramref name="PersonPublicId"/>, sólo el de ese tercero. Los que tienen uno vigente pero el libro cambió se
/// cuentan como desactualizados: se reexpiden uno a uno.
/// </summary>
public sealed record GenerateWithholdingCertificatesCommand(string Kind, int Year, byte? PeriodFrom = null, byte? PeriodTo = null, Guid? PersonPublicId = null)
    : IRequest<Result<GeneracionDeCertificadosDto>>;

public sealed class GenerateWithholdingCertificatesCommandValidator : AbstractValidator<GenerateWithholdingCertificatesCommand>
{
    public GenerateWithholdingCertificatesCommandValidator()
    {
        RuleFor(x => x.Kind).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
    }
}

public sealed class GenerateWithholdingCertificatesCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<GenerateWithholdingCertificatesCommand, Result<GeneracionDeCertificadosDto>>
{
    public async Task<Result<GeneracionDeCertificadosDto>> Handle(GenerateWithholdingCertificatesCommand request, CancellationToken ct)
    {
        var alcance = LibroDeCertificados.Validar(request.Kind, request.Year, request.PeriodFrom, request.PeriodTo);
        if (alcance.IsFailure) return Result.Failure<GeneracionDeCertificadosDto>(alcance.Error);
        var a = alcance.Value;
        int? persona = null;
        if (request.PersonPublicId is { } pid)
        {
            persona = await db.People.AsNoTracking().Where(p => p.PublicId == pid).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (persona is null) return Result.Failure<GeneracionDeCertificadosDto>(TaxErrors.PersonNotFound);
        }

        var lineas = await LibroDeCertificados.LineasAsync(db, clock, a, persona, ct);
        if (lineas.IsFailure) return Result.Failure<GeneracionDeCertificadosDto>(lineas.Error);
        var existentes = await db.WithholdingCertificates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status != CertificateStatus.Voided && c.TaxKind == a.Kind && c.Year == a.Year && c.PeriodFrom == a.PeriodFrom && c.PeriodTo == a.PeriodTo)
            .ToDictionaryAsync(c => c.PersonId, c => c.LedgerFingerprint, ct);
        var idsDeCuenta = lineas.Value.Select(l => l.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Include(c => c.TaxRates).Where(c => idsDeCuenta.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var numero = await LibroDeCertificados.SiguienteNumeroAsync(db, a.Kind, ct);
        var nuevos = new List<WithholdingCertificate>();
        int vigentes = 0, desactualizados = 0;
        foreach (var porPersona in lineas.Value.GroupBy(l => l.PersonId).OrderBy(g => g.Key))
        {
            var totales = LibroDeCertificados.PorCuenta(porPersona);
            if (totales.Sum(t => t.Amount) <= 0m) continue;
            var huella = LibroDeCertificados.Huella(porPersona);
            if (existentes.TryGetValue(porPersona.Key, out var anterior))
            {
                if (anterior == huella) vigentes++; else desactualizados++;
                continue;
            }
            var certificado = LibroDeCertificados.Nuevo(a, porPersona.Key, numero++, totales, cuentas, huella, ahora, quien);
            db.WithholdingCertificates.Add(certificado);
            nuevos.Add(certificado);
        }
        if (nuevos.Count > 0) await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Certificate.Generated", nameof(WithholdingCertificate), null, null,
            new { kind = a.Kind.ToString(), a.Year, a.PeriodFrom, a.PeriodTo, issued = nuevos.Count, alreadyCurrent = vigentes, outdated = desactualizados }, ct);
        return Result.Success(new GeneracionDeCertificadosDto(nuevos.Count, vigentes, desactualizados, await LibroDeCertificados.DtosAsync(db, clock, nuevos, ct)));
    }
}

// ------------------------------------------------------------------------------------ listar --

/// <summary>Los certificados expedidos, del más reciente al más antiguo; <c>Status</c> calcula si están desactualizados.</summary>
public sealed record ListCertificatesQuery(string? Kind = null, int? Year = null, Guid? PersonPublicId = null) : IRequest<Result<IReadOnlyList<CertificadoDto>>>;

public sealed class ListCertificatesQueryValidator : AbstractValidator<ListCertificatesQuery>;

public sealed class ListCertificatesQueryHandler(IApplicationDbContext db, IDateTimeService clock) : IRequestHandler<ListCertificatesQuery, Result<IReadOnlyList<CertificadoDto>>>
{
    public async Task<Result<IReadOnlyList<CertificadoDto>>> Handle(ListCertificatesQuery request, CancellationToken ct)
    {
        var q = db.WithholdingCertificates.AsNoTracking().Where(c => !c.IsDeleted);
        if (Enum.TryParse<TaxKind>(request.Kind, ignoreCase: true, out var clase)) q = q.Where(c => c.TaxKind == clase);
        if (request.Year is { } year) q = q.Where(c => c.Year == year);
        if (request.PersonPublicId is { } pid) q = q.Where(c => c.Person!.PublicId == pid);
        var certificados = await q.OrderByDescending(c => c.Year).ThenBy(c => c.TaxKind).ThenByDescending(c => c.Number).Take(5000).ToListAsync(ct);
        return Result.Success(await LibroDeCertificados.DtosAsync(db, clock, certificados, ct));
    }
}

/// <summary>Un certificado con sus líneas.</summary>
public sealed record GetCertificateQuery(Guid PublicId) : IRequest<Result<CertificadoDetalleDto>>;

public sealed class GetCertificateQueryValidator : AbstractValidator<GetCertificateQuery>
{
    public GetCertificateQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class GetCertificateQueryHandler(IApplicationDbContext db, IDateTimeService clock) : IRequestHandler<GetCertificateQuery, Result<CertificadoDetalleDto>>
{
    public async Task<Result<CertificadoDetalleDto>> Handle(GetCertificateQuery request, CancellationToken ct)
    {
        var c = await db.WithholdingCertificates.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(x => x.PublicId == request.PublicId && !x.IsDeleted, ct);
        if (c is null) return Result.Failure<CertificadoDetalleDto>(TaxErrors.NotFound);
        var dto = (await LibroDeCertificados.DtosAsync(db, clock, [c], ct))[0];
        var lineas = c.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.Account!.Code)
            .Select(l => new LineaDeCertificadoDto(l.Account!.Code, l.ConceptCode, l.Account.Name, l.Base, l.Rate, l.Amount)).ToList();
        return Result.Success(new CertificadoDetalleDto(dto, lineas));
    }
}

// --------------------------------------------------------------------------------- reexpedir --

/// <summary>Reexpide un certificado desde el libro de hoy: número nuevo, el anterior anulado y apuntando al nuevo.</summary>
public sealed record ReissueCertificateCommand(Guid PublicId) : IRequest<Result<CertificadoDto>>;

public sealed class ReissueCertificateCommandValidator : AbstractValidator<ReissueCertificateCommand>
{
    public ReissueCertificateCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class ReissueCertificateCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<ReissueCertificateCommand, Result<CertificadoDto>>
{
    public async Task<Result<CertificadoDto>> Handle(ReissueCertificateCommand request, CancellationToken ct)
    {
        var anterior = await db.WithholdingCertificates.FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);
        if (anterior is null) return Result.Failure<CertificadoDto>(TaxErrors.NotFound);
        if (anterior.Status == CertificateStatus.Voided) return Result.Failure<CertificadoDto>(TaxErrors.Voided);

        var a = new LibroDeCertificados.Alcance(anterior.TaxKind, anterior.Year, anterior.PeriodFrom, anterior.PeriodTo);
        var lineas = await LibroDeCertificados.LineasAsync(db, clock, a, anterior.PersonId, ct);
        if (lineas.IsFailure) return Result.Failure<CertificadoDto>(lineas.Error);
        var idsDeCuenta = lineas.Value.Select(l => l.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Include(c => c.TaxRates).Where(c => idsDeCuenta.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        // Sin retenciones en el libro de hoy el certificado nuevo sale en cero: el tercero tiene derecho a saber que ya no hay retención que certificar.
        var nuevo = LibroDeCertificados.Nuevo(a, anterior.PersonId, await LibroDeCertificados.SiguienteNumeroAsync(db, a.Kind, ct),
            LibroDeCertificados.PorCuenta(lineas.Value), cuentas, LibroDeCertificados.Huella(lineas.Value), ahora, quien);
        db.WithholdingCertificates.Add(nuevo);
        anterior.Status = CertificateStatus.Voided;
        anterior.UpdatedAt = ahora;
        anterior.UpdatedBy = quien;
        await db.SaveChangesAsync(ct);
        // El enlace al reemplazo necesita el Id del nuevo: segundo guardado (el anterior ya quedó anulado en el primero).
        anterior.VoidedByCertificateId = nuevo.Id;
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Certificate.Reissued", nameof(WithholdingCertificate), nuevo.PublicId,
            new { previous = anterior.PublicId, previousNumber = anterior.Number }, new { kind = a.Kind.ToString(), number = nuevo.Number, nuevo.TotalAmount }, ct);
        return Result.Success((await LibroDeCertificados.DtosAsync(db, clock, [nuevo], ct))[0]);
    }
}

// ------------------------------------------------------------------------------- PDF y correo --

/// <summary>Lo que el PDF necesita: empresa, tercero, clase, alcance, número, fecha y líneas. Lo dibuja la API (QuestPDF).</summary>
public sealed record CertificadoParaImprimir(
    string CompanyName, string CompanyTaxId, string? CompanyAddress, string PersonName, string PersonTaxId, string? PersonAddress,
    string Title, string KindName, int Year, byte? PeriodFrom, byte? PeriodTo, long Number, DateTime IssuedAt, bool Voided,
    IReadOnlyList<LineaDeCertificadoDto> Lines, decimal TotalBase, decimal TotalAmount);

public interface IWithholdingCertificatePdfRenderer
{
    byte[] Render(CertificadoParaImprimir certificado);
}

public sealed record CertificadoImpreso(byte[] Content, string FileName);

/// <summary>El PDF de un certificado; reimprimirlo no cambia el número. Uno anulado sale con la marca.</summary>
public sealed record GetCertificatePdfQuery(Guid PublicId) : IRequest<Result<CertificadoImpreso>>;

public sealed class GetCertificatePdfQueryValidator : AbstractValidator<GetCertificatePdfQuery>
{
    public GetCertificatePdfQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class GetCertificatePdfQueryHandler(IApplicationDbContext db, IWithholdingCertificatePdfRenderer renderer) : IRequestHandler<GetCertificatePdfQuery, Result<CertificadoImpreso>>
{
    public async Task<Result<CertificadoImpreso>> Handle(GetCertificatePdfQuery request, CancellationToken ct)
    {
        var datos = await ImpresionDeCertificado.ArmarAsync(db, request.PublicId, ct);
        if (datos is null) return Result.Failure<CertificadoImpreso>(TaxErrors.NotFound);
        return Result.Success(new CertificadoImpreso(renderer.Render(datos), ImpresionDeCertificado.NombreDeArchivo(datos)));
    }
}

/// <summary>Envía el certificado en PDF al correo del tercero registrado en Personas y anota el envío.</summary>
public sealed record SendCertificateCommand(Guid PublicId) : IRequest<Result<CertificadoDto>>;

public sealed class SendCertificateCommandValidator : AbstractValidator<SendCertificateCommand>
{
    public SendCertificateCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class SendCertificateCommandHandler(IApplicationDbContext db, IWithholdingCertificatePdfRenderer renderer, IEmailSender correo, IOutboundEmailStatus estadoDelCorreo,
    IDateTimeService clock, AccountingAuditEmitter audit) : IRequestHandler<SendCertificateCommand, Result<CertificadoDto>>
{
    public async Task<Result<CertificadoDto>> Handle(SendCertificateCommand request, CancellationToken ct)
    {
        if (!estadoDelCorreo.IsConfigured) return Result.Failure<CertificadoDto>(TaxErrors.EmailNotConfigured(estadoDelCorreo.Description));
        var certificado = await db.WithholdingCertificates.Include(c => c.Person).FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);
        if (certificado is null) return Result.Failure<CertificadoDto>(TaxErrors.NotFound);
        if (certificado.Status == CertificateStatus.Voided) return Result.Failure<CertificadoDto>(TaxErrors.Voided);
        var destino = certificado.Person?.Email?.Trim();
        if (string.IsNullOrWhiteSpace(destino)) return Result.Failure<CertificadoDto>(TaxErrors.PersonWithoutEmail);

        var datos = (await ImpresionDeCertificado.ArmarAsync(db, request.PublicId, ct))!;
        var pdf = renderer.Render(datos);
        var asunto = $"{datos.KindName}: certificado {datos.Number} del año gravable {datos.Year}";
        await correo.SendAsync(new EmailMessage(destino, asunto,
            $"<p>Señor(a) {System.Net.WebUtility.HtmlEncode(datos.PersonName)}:</p><p>Adjuntamos el certificado de {System.Net.WebUtility.HtmlEncode(datos.KindName.ToLowerInvariant())} número {datos.Number} del año gravable {datos.Year}, expedido por {System.Net.WebUtility.HtmlEncode(datos.CompanyName)}.</p>",
            null, null, [new EmailAttachment(ImpresionDeCertificado.NombreDeArchivo(datos), "application/pdf", pdf)]), ct);

        certificado.LastSentAt = clock.UtcNow;
        certificado.LastSentTo = destino;
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Certificate.Sent", nameof(WithholdingCertificate), certificado.PublicId, null,
            new { kind = certificado.TaxKind.ToString(), certificado.Number, to = destino }, ct);
        return Result.Success((await LibroDeCertificados.DtosAsync(db, clock, [certificado], ct))[0]);
    }
}

internal static class ImpresionDeCertificado
{
    public static async Task<CertificadoParaImprimir?> ArmarAsync(IApplicationDbContext db, Guid publicId, CancellationToken ct)
    {
        var c = await db.WithholdingCertificates.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.Account).Include(x => x.Person)
            .FirstOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted, ct);
        if (c is null) return null;
        var empresa = await db.Companies.AsNoTracking().Where(e => !e.IsDeleted).OrderBy(e => e.Id)
            .Select(e => new { e.Name, NIT = e.TaxIdCheckDigit == null ? e.TaxId : e.TaxId + "-" + e.TaxIdCheckDigit, e.Address }).FirstOrDefaultAsync(ct);
        var p = c.Person!;
        var lineas = c.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.Account!.Code)
            .Select(l => new LineaDeCertificadoDto(l.Account!.Code, l.ConceptCode, l.Account.Name, l.Base, l.Rate, l.Amount)).ToList();
        return new CertificadoParaImprimir(empresa?.Name ?? string.Empty, empresa?.NIT ?? string.Empty, empresa?.Address,
            PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName), p.TaxId, p.Address,
            ClasesDeImpuesto.Titulo(c.TaxKind), ClasesDeImpuesto.Nombre(c.TaxKind), c.Year, c.PeriodFrom, c.PeriodTo, c.Number, c.IssuedAt,
            c.Status == CertificateStatus.Voided, lineas, c.TotalBase, c.TotalAmount);
    }

    public static string NombreDeArchivo(CertificadoParaImprimir c) =>
        $"certificado-{(c.Title.Contains("IVA") ? "reteiva" : c.Title.Contains("INDUSTRIA") ? "reteica" : "retefuente")}-{c.Year}-{c.Number}.pdf";
}
