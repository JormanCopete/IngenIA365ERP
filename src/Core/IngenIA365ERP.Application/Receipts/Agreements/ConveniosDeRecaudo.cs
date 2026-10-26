using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Agreements;

// Feature 015, R2 (fase 38, T0434; FR-088, FR-091, FR-092; US16): los convenios de recaudo con bancos, corresponsales y pasarelas. El convenio se configura antes de
// activar Recaudo (no es una escritura de negocio); lo que cambia con el tiempo es una versión nueva con su vigencia, nunca retroactiva. Sólo por archivo (C2 = A).

/// <summary>Una versión de un convenio. (nuevo)</summary>
public sealed record CollectionAgreementVersionDto(Guid PublicId, DateOnly ValidFrom, DateOnly? ValidTo, PaymentReferenceMode ReferenceMode, int ReferenceLength,
    CheckDigitAlgorithm CheckDigit, Guid FormatPublicId, string FormatCode, decimal? CommissionPerPayment, decimal? CommissionRate, string? Note);

/// <summary>Un convenio de recaudo con su versión vigente y su historia. (nuevo)</summary>
public sealed record CollectionAgreementDto(Guid PublicId, string Code, string Name, CollectionAgreementKind Kind, Guid OwnBankAccountPublicId, string? OwnBankAccountCode,
    Guid? PartnerPersonPublicId, string? PartnerName, string? PartnerAgreementCode, bool IsActive, CollectionAgreementVersionDto? Current,
    IReadOnlyList<CollectionAgreementVersionDto> Versions);

/// <summary>Lo común de los convenios: el DTO y la versión vigente. (nuevo)</summary>
public static class ConveniosDeRecaudo
{
    public static async Task<CollectionAgreementVersion?> VersionVigenteAsync(IApplicationDbContext db, int convenio, DateOnly fecha, CancellationToken ct) =>
        (await db.CollectionAgreementVersions.AsNoTracking().Where(v => v.CollectionAgreementId == convenio).OrderByDescending(v => v.ValidFrom).ToListAsync(ct))
        .FirstOrDefault(v => v.VigenteEn(fecha));

    public static async Task<CollectionAgreementDto> DtoAsync(IApplicationDbContext db, ICuentasBancariasPropias cuentas, CollectionAgreement c, DateOnly hoy,
        CancellationToken ct)
    {
        var versiones = await (from v in db.CollectionAgreementVersions.AsNoTracking()
                               join f in db.BankFileFormats.AsNoTracking().IgnoreQueryFilters() on v.BankFileFormatId equals f.Id
                               where v.CollectionAgreementId == c.Id
                               orderby v.ValidFrom descending
                               select new { v, f.PublicId, f.Code }).ToListAsync(ct);
        var dtos = versiones.Select(x => new CollectionAgreementVersionDto(x.v.PublicId, x.v.ValidFrom, x.v.ValidTo, x.v.ReferenceMode, x.v.ReferenceLength, x.v.CheckDigit,
            x.PublicId, x.Code, x.v.CommissionPerPayment, x.v.CommissionRate, x.v.Note)).ToList();
        var vigente = versiones.FirstOrDefault(x => x.v.VigenteEn(hoy));
        var aliado = c.PartnerPersonId is { } p
            ? await db.People.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == p, ct)
            : null;
        var cuenta = await cuentas.PorPublicIdAsync(c.OwnBankAccountPublicId, ct);
        return new CollectionAgreementDto(c.PublicId, c.Code, c.Name, c.Kind, c.OwnBankAccountPublicId, cuenta?.Code, aliado?.PublicId, aliado is null ? null : CashDesk.OperacionesDeCaja.Nombre(aliado),
            c.PartnerAgreementCode, c.IsActive, vigente is null ? null : dtos.First(d => d.PublicId == vigente.v.PublicId), dtos);
    }

    /// <summary>El ámbito del formato del archivo de un convenio por su clase.</summary>
    public static BankFileScope AmbitoDe(CollectionAgreementKind clase) =>
        clase == CollectionAgreementKind.Gateway ? BankFileScope.PaymentGatewayFile : BankFileScope.CollectionAgreementFile;
}

/// <summary>
/// <c>POST /api/receipts/collection-agreements</c> (crear) y <c>PUT /{id}</c> (<c>Receipts.Agreements.Manage</c>, con clave; FR-088): un convenio de banco, corresponsal o
/// pasarela con su cuenta recaudadora y la primera versión; al editarlo, si cambia la estructura de la referencia, el formato o la comisión, nace una versión nueva desde
/// <see cref="ValidFrom"/> (no antes de mañana si la vigente ya cargó archivos) y la anterior cierra la víspera. (nuevo)
/// </summary>
public sealed record SaveCollectionAgreementCommand(
    Guid? PublicId,
    string Code,
    string Name,
    CollectionAgreementKind Kind,
    Guid OwnBankAccountPublicId,
    Guid FormatPublicId,
    PaymentReferenceMode ReferenceMode,
    int ReferenceLength,
    CheckDigitAlgorithm CheckDigit,
    DateOnly? ValidFrom = null,
    Guid? PartnerPersonPublicId = null,
    string? PartnerAgreementCode = null,
    decimal? CommissionPerPayment = null,
    decimal? CommissionRate = null,
    bool IsActive = true,
    string? Note = null) : IRequest<Result<CollectionAgreementDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveCollectionAgreementCommandValidator : AbstractValidator<SaveCollectionAgreementCommand>
{
    public SaveCollectionAgreementCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.ReferenceMode).IsInEnum();
        RuleFor(x => x.CheckDigit).IsInEnum();
        RuleFor(x => x.OwnBankAccountPublicId).NotEmpty().WithMessage("Elija la cuenta recaudadora del convenio.");
        RuleFor(x => x.FormatPublicId).NotEmpty().WithMessage("Elija el formato del archivo diario del convenio.");
        RuleFor(x => x.ReferenceLength).InclusiveBetween(0, 40).WithMessage("La longitud de la referencia va de 0 (la que traiga) a 40.");
        RuleFor(x => x.CommissionPerPayment).GreaterThanOrEqualTo(0m).When(x => x.CommissionPerPayment is not null);
        RuleFor(x => x.CommissionRate).InclusiveBetween(0m, 1m).When(x => x.CommissionRate is not null).WithMessage("La comisión por porcentaje va como fracción (0 a 1).");
        RuleFor(x => x.PartnerAgreementCode).MaximumLength(40);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class SaveCollectionAgreementCommandHandler(
    IApplicationDbContext db,
    ICuentasBancariasPropias cuentas,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<SaveCollectionAgreementCommand, Result<CollectionAgreementDto>>
{
    public async Task<Result<CollectionAgreementDto>> Handle(SaveCollectionAgreementCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var cuenta = await cuentas.PorPublicIdAsync(request.OwnBankAccountPublicId, ct);
        if (cuenta is null || !cuenta.IsCollection || !cuenta.IsActive || cuenta.BranchPublicId is null) return Result.Failure<CollectionAgreementDto>(ErroresDeConvenios.CuentaNoRecaudadora());
        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == cuenta.BranchPublicId).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (oficina is null) return Result.Failure<CollectionAgreementDto>(ErroresDeConvenios.CuentaNoRecaudadora());
        var ambito = ConveniosDeRecaudo.AmbitoDe(request.Kind);
        var formato = await db.BankFileFormats.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == request.FormatPublicId, ct);
        if (formato is null || formato.Scope != ambito || !formato.IsActive) return Result.Failure<CollectionAgreementDto>(ErroresDeConvenios.FormatoInvalido(ambito));
        int? aliado = null;
        if (request.PartnerPersonPublicId is { } pp)
        {
            aliado = await db.People.AsNoTracking().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (aliado is null) return Result.Failure<CollectionAgreementDto>(ErroresDeRecaudo.NoEncontrado("El aliado del convenio"));
        }
        if (await db.CollectionAgreements.AsNoTracking().FirstOrDefaultAsync(c => c.Code == codigo && c.PublicId != request.PublicId, ct) is { } otro)
            return Result.Failure<CollectionAgreementDto>(CodigoDeCatalogo.Duplicado("un convenio", codigo, otro.Name, otro.PublicId));

        CollectionAgreement convenio;
        var nuevo = request.PublicId is null;
        if (nuevo)
        {
            convenio = new CollectionAgreement();
            db.CollectionAgreements.Add(convenio);
        }
        else
        {
            convenio = await db.CollectionAgreements.FirstOrDefaultAsync(c => c.PublicId == request.PublicId, ct)
                       ?? throw new InvalidOperationException("unreachable");
            if (convenio.Kind != request.Kind) return Result.Failure<CollectionAgreementDto>(ErroresDeConvenios.ClaseNoCambia());
        }
        (convenio.Code, convenio.Name, convenio.Kind, convenio.OwnBankAccountPublicId, convenio.BranchId, convenio.PartnerPersonId, convenio.PartnerAgreementCode,
            convenio.IsActive) = (codigo, request.Name.Trim(), request.Kind, cuenta.PublicId, oficina.Value, aliado,
            string.IsNullOrWhiteSpace(request.PartnerAgreementCode) ? null : request.PartnerAgreementCode.Trim(), request.IsActive);
        await db.SaveChangesAsync(ct);

        // La versión: la primera desde la fecha pedida (o hoy); después, una nueva sólo si cambia lo versionado.
        var versiones = await db.CollectionAgreementVersions.Where(v => v.CollectionAgreementId == convenio.Id).OrderByDescending(v => v.ValidFrom).ToListAsync(ct);
        var vigente = versiones.FirstOrDefault(v => v.VigenteEn(hoy)) ?? versiones.FirstOrDefault();
        var cambia = vigente is null || vigente.ReferenceMode != request.ReferenceMode || vigente.ReferenceLength != request.ReferenceLength
                     || vigente.CheckDigit != request.CheckDigit || vigente.BankFileFormatId != formato.Id || vigente.CommissionPerPayment != request.CommissionPerPayment
                     || vigente.CommissionRate != request.CommissionRate;
        if (cambia)
        {
            var desde = request.ValidFrom ?? (vigente is null ? hoy : hoy.AddDays(1));
            if (vigente is not null)
            {
                if (desde <= vigente.ValidFrom) return Result.Failure<CollectionAgreementDto>(ErroresDeConvenios.VersionRetroactiva(vigente.ValidFrom));
                var usada = await db.AgreementFiles.AnyAsync(f => f.CollectionAgreementVersionId == vigente.Id, ct)
                            || await db.GatewayFiles.AnyAsync(f => f.CollectionAgreementVersionId == vigente.Id, ct);
                if (usada && desde <= hoy) return Result.Failure<CollectionAgreementDto>(ErroresDeConvenios.VersionRetroactiva(hoy));
                vigente.ValidTo = desde.AddDays(-1);
            }
            db.CollectionAgreementVersions.Add(new CollectionAgreementVersion
            {
                CollectionAgreementId = convenio.Id, ValidFrom = desde, ReferenceMode = request.ReferenceMode, ReferenceLength = request.ReferenceLength,
                CheckDigit = request.CheckDigit, BankFileFormatId = formato.Id, CommissionPerPayment = request.CommissionPerPayment, CommissionRate = request.CommissionRate,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            });
        }
        await auditoria.AgregarAsync(nuevo ? "Receipts.Agreement.Created" : "Receipts.Agreement.Updated", "CollectionAgreement", convenio.PublicId, null,
            new { code = codigo, kind = request.Kind.ToString(), account = cuenta.Code, format = formato.Code, newVersion = cambia, request.ReferenceMode, request.ReferenceLength,
                checkDigit = request.CheckDigit.ToString() }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConveniosDeRecaudo.DtoAsync(db, cuentas, convenio, hoy, ct));
    }
}

/// <summary><c>GET /api/receipts/collection-agreements?onlyActive=</c> (<c>Receipts.Agreements.View</c>): los convenios con su versión vigente. (nuevo)</summary>
public sealed record ListCollectionAgreementsQuery(bool OnlyActive = false) : IRequest<Result<IReadOnlyList<CollectionAgreementDto>>>;

public sealed class ListCollectionAgreementsQueryHandler(IApplicationDbContext db, ICuentasBancariasPropias cuentas, IDateTimeService reloj)
    : IRequestHandler<ListCollectionAgreementsQuery, Result<IReadOnlyList<CollectionAgreementDto>>>
{
    public async Task<Result<IReadOnlyList<CollectionAgreementDto>>> Handle(ListCollectionAgreementsQuery request, CancellationToken ct)
    {
        var consulta = db.CollectionAgreements.AsNoTracking();
        if (request.OnlyActive) consulta = consulta.Where(c => c.IsActive);
        var filas = await consulta.OrderBy(c => c.Code).ToListAsync(ct);
        var lista = new List<CollectionAgreementDto>(filas.Count);
        foreach (var c in filas) lista.Add(await ConveniosDeRecaudo.DtoAsync(db, cuentas, c, reloj.HoyLocal, ct));
        return Result.Success<IReadOnlyList<CollectionAgreementDto>>(lista);
    }
}

/// <summary>Los errores de los convenios, sus archivos y su conciliación (feature 015, R2, fase 38; nuevos, DT §2.7). (nuevo)</summary>
public static class ErroresDeConvenios
{
    public static Error CuentaNoRecaudadora() => new("Receipts.Agreement.AccountNotCollection",
        "La cuenta recaudadora de un convenio es una cuenta propia de recaudo, activa y con oficina: elíjala en Tesorería → Cuentas bancarias.");

    public static Error FormatoInvalido(BankFileScope ambito) => new ErrorConDatos("Receipts.Agreement.FormatInvalid",
        "El formato del archivo tiene que estar activo y ser del ámbito del convenio (archivo de convenio o de pasarela): créelo en Maestros → Formatos bancarios.",
        new { scope = ambito.ToString() });

    public static Error ClaseNoCambia() => new("Receipts.Agreement.KindLocked", "La clase de un convenio no cambia: cree otro convenio.");

    public static Error VersionRetroactiva(DateOnly desde) => new ErrorConDatos("Receipts.Agreement.VersionRetroactive",
        string.Create(CultureInfo.GetCultureInfo("es-CO"), $"La versión nueva del convenio rige después del {desde:dd/MM/yyyy}: una versión nunca es retroactiva."),
        new { after = desde });

    public static Error SinVersion() => new("Receipts.Agreement.NoCurrentVersion", "El convenio no tiene una versión vigente hoy: revise su vigencia.");

    public static Error Inactivo() => new("Receipts.Agreement.Inactive", "El convenio está inactivo: actívelo para cargar sus archivos.");

    public static Error ArchivoYaCargado(string numero) => new ErrorConDatos("Receipts.AgreementFile.AlreadyLoaded",
        $"Ese archivo ya se cargó como {numero}: un archivo no se carga dos veces.", new { number = numero });

    public static Error ArchivoIlegible(IReadOnlyList<object> lineas) => new ErrorConDatos("Receipts.AgreementFile.IllegibleLines",
        "El archivo tiene líneas que no se pueden leer: revíselas o cárguelo aceptando que esas líneas no se carguen.", new { lines = lineas });

    public static Error ArchivoVacio() => new("Receipts.AgreementFile.Empty", "El archivo no trae pagos.");

    public static Error TotalesNoCuadran(int lineasDeclaradas, int lineas, decimal totalDeclarado, decimal total) => new ErrorConDatos("Receipts.AgreementFile.TotalsMismatch",
        string.Create(CultureInfo.GetCultureInfo("es-CO"),
            $"El archivo trae {lineas} pagos por {total:N2} y el aliado informó {lineasDeclaradas} por {totalDeclarado:N2}: el archivo no se carga."),
        new { declaredLines = lineasDeclaradas, lines = lineas, declaredTotal = totalDeclarado, total });

    public static Error AbonoDeOtraCuenta() => new("Receipts.AgreementReconciliation.StatementLineMismatch",
        "El abono no es de la cuenta recaudadora del convenio: elija un abono del extracto de esa cuenta.");

    public static Error ArchivoYaConciliado() => new("Receipts.AgreementReconciliation.FileAlreadyReconciled", "El archivo ya está conciliado con un abono.");

    public static Error NoPorAprobar() => new("Receipts.AgreementReconciliation.NotPendingApproval", "La conciliación no está por aprobar.");

    public static Error AutoAprobacion() => new("Receipts.AgreementReconciliation.SelfApproval",
        "La diferencia sin explicar la aprueba una persona distinta de quien registró la conciliación.");

    public static Error ReferenciaSinProducto() => new("Receipts.PaymentReference.ProductNotFound",
        "El producto no es de la persona o no está vigente: elíjalo entre sus créditos y cuentas.");
}
