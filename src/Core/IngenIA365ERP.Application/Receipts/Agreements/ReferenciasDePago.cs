using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Convenios;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Agreements;

// Feature 015, R2 (fase 38, T0434, T0436; FR-090; pedido del dueño para R2): la referencia de pago estable de cada producto (y, si el convenio lo pide, de cada persona),
// con su dígito de verificación. La base es el número del producto (o el documento de la persona) y el dígito sale del algoritmo del parámetro
// Referencias.DigitoDeVerificacion: así la referencia es la misma cada vez que se imprime. RCD_PaymentReferences registra las emitidas y las encontradas en un archivo,
// para resolverlas sin consultar a Cartera y para retirarlas: una referencia nunca se reutiliza.

/// <summary>Una referencia de pago registrada. (nuevo)</summary>
public sealed record PaymentReferenceDto(Guid PublicId, string Reference, string BaseNumber, string CheckDigit, CheckDigitAlgorithm Algorithm, PaymentReferenceMode Mode,
    Guid PersonPublicId, string PersonName, Guid? ProductPublicId, ApplicationTarget? Target, string? ProductNumber, PaymentReferenceState Status, DateTime IssuedAt);

/// <summary>Lo que se resolvió de una referencia: la fila registrada (si la hay), la persona y el producto. (nuevo)</summary>
public sealed record ReferenciaResuelta(PaymentReference? Fila, Guid PersonPublicId, int PersonId, Guid? ProductPublicId, ApplicationTarget? Target, string? ProductNumber);

/// <summary>
/// La referencia de pago de Recaudo (feature 015, R2, fase 38): la calcula, la registra y la resuelve. Implementa <see cref="IReferenciasDePago"/> para Cartera
/// (sólo lectura). (nuevo)
/// </summary>
public sealed class ReferenciasDePagoDeRecaudo(IApplicationDbContext db, ParametrosDeCaja parametros, IBusquedaDeObligaciones busqueda, IDateTimeService reloj)
    : IReferenciasDePago
{
    /// <summary>El algoritmo vigente del dígito (parámetro <c>Referencias.DigitoDeVerificacion</c>).</summary>
    public async Task<CheckDigitAlgorithm> AlgoritmoAsync(CancellationToken ct) =>
        Enum.TryParse<CheckDigitAlgorithm>(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.ReferenciasDigitoDeVerificacion, reloj.HoyLocal, ct), out var a)
            ? a
            : CheckDigitAlgorithm.Modulo10;

    public async Task<ReferenciaDePagoDelProductoDto?> DelProductoAsync(Guid productPublicId, string productNumber, CancellationToken ct)
    {
        if (await db.PaymentReferences.AsNoTracking().Where(r => r.ProductPublicId == productPublicId && r.Status == PaymentReferenceState.Active)
                .OrderBy(r => r.Id).FirstOrDefaultAsync(ct) is { } fila)
            return new ReferenciaDePagoDelProductoDto(productPublicId, fila.Reference, fila.BaseNumber, fila.CheckDigitValue, fila.Algorithm.ToString());
        var algoritmo = await AlgoritmoAsync(ct);
        var baseDeLaReferencia = DigitoDeVerificacion.Normalizar(productNumber);
        if (baseDeLaReferencia.Length == 0) return null;
        var digito = DigitoDeVerificacion.Calcular(baseDeLaReferencia, algoritmo);
        return new ReferenciaDePagoDelProductoDto(productPublicId, baseDeLaReferencia + digito, baseDeLaReferencia, digito, algoritmo.ToString());
    }

    /// <summary>Registra (o devuelve la ya registrada) la referencia de un producto o de una persona. Sin guardar.</summary>
    public async Task<PaymentReference> RegistrarAsync(int personaId, string baseDeLaReferencia, PaymentReferenceMode modo, Guid? producto, ApplicationTarget? destino,
        string? numeroDelProducto, CheckDigitAlgorithm algoritmo, CancellationToken ct)
    {
        var b = DigitoDeVerificacion.Normalizar(baseDeLaReferencia);
        var referencia = DigitoDeVerificacion.Componer(b, algoritmo);
        var ya = db.PaymentReferences.Local.FirstOrDefault(r => r.Reference == referencia)
                 ?? await db.PaymentReferences.FirstOrDefaultAsync(r => r.Reference == referencia, ct);
        if (ya is not null) return ya;
        var fila = new PaymentReference
        {
            Reference = referencia, BaseNumber = b, CheckDigitValue = DigitoDeVerificacion.Calcular(b, algoritmo), Algorithm = algoritmo, Mode = modo, PersonId = personaId,
            ProductPublicId = producto, Target = destino, ProductNumber = numeroDelProducto, IssuedAt = reloj.UtcNow,
        };
        db.PaymentReferences.Add(fila);
        return fila;
    }

    /// <summary>
    /// Resuelve una referencia leída de un archivo con la estructura del convenio: primero la registrada (exacta); si no, por producto busca el número de la base en
    /// Cartera y Captaciones (exacto, una sola coincidencia) y, por persona, el documento. Nula si no se encuentra.
    /// </summary>
    public async Task<ReferenciaResuelta?> ResolverAsync(LecturaDeReferencia lectura, PaymentReferenceMode modo, CheckDigitAlgorithm algoritmo, CancellationToken ct)
    {
        if (!lectura.Valida) return null;
        var completa = lectura.Base + lectura.Digito;
        var fila = await db.PaymentReferences.AsNoTracking().FirstOrDefaultAsync(r => r.Reference == completa && r.Status == PaymentReferenceState.Active, ct);
        if (fila is not null)
        {
            var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == fila.PersonId).Select(p => p.PublicId).FirstAsync(ct);
            return new ReferenciaResuelta(fila, persona, fila.PersonId, fila.ProductPublicId, fila.Target, fila.ProductNumber);
        }
        var baseSinCeros = lectura.Base.TrimStart('0');
        if (modo == PaymentReferenceMode.ByPerson)
        {
            var personas = await db.People.AsNoTracking().Where(p => p.TaxId == lectura.Base || p.TaxId == baseSinCeros).Select(p => new { p.Id, p.PublicId }).Take(2).ToListAsync(ct);
            return personas.Count == 1 ? new ReferenciaResuelta(null, personas[0].PublicId, personas[0].Id, null, null, null) : null;
        }
        var encontrados = new List<ObligacionEncontradaDto>();
        foreach (var candidata in new[] { lectura.Base, baseSinCeros }.Where(x => x.Length > 0).Distinct())
            encontrados.AddRange((await busqueda.PorNumeroAsync(new BusquedaPorNumero(candidata, SinSeparadores: true), ct))
                .Where(o => string.Equals(DigitoDeVerificacion.Normalizar(o.Number).TrimStart('0'), baseSinCeros, StringComparison.Ordinal)));
        var unicos = encontrados.DistinctBy(o => o.ObligationPublicId).ToList();
        if (unicos.Count != 1) return null;
        var o = unicos[0];
        var id = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == o.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return id is null
            ? null
            : new ReferenciaResuelta(null, o.PersonPublicId, id.Value, o.ObligationPublicId,
                o.Kind == ClaseDeObligacionEncontrada.Loan ? ApplicationTarget.Loan : ApplicationTarget.Account, o.Number);
    }

    public static async Task<PaymentReferenceDto> DtoAsync(IApplicationDbContext db, PaymentReference r, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == r.PersonId, ct);
        return new PaymentReferenceDto(r.PublicId, r.Reference, r.BaseNumber, r.CheckDigitValue, r.Algorithm, r.Mode, persona.PublicId, CashDesk.OperacionesDeCaja.Nombre(persona),
            r.ProductPublicId, r.Target, r.ProductNumber, r.Status, r.IssuedAt);
    }
}

/// <summary>
/// <c>POST /api/receipts/payment-references</c> (<c>Receipts.Agreements.Manage</c>, con clave; FR-090): emite —registra— la referencia de pago estable de un producto de
/// la persona (por su número, que se busca en Cartera y Captaciones) o, sin producto, la de la persona por su documento. Repetirla devuelve la misma. (nuevo)
/// </summary>
public sealed record IssuePaymentReferenceCommand(Guid PersonPublicId, Guid? ProductPublicId = null, string? ProductNumber = null)
    : IRequest<Result<PaymentReferenceDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class IssuePaymentReferenceCommandValidator : AbstractValidator<IssuePaymentReferenceCommand>
{
    public IssuePaymentReferenceCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.ProductNumber).NotEmpty().When(x => x.ProductPublicId is not null).WithMessage("Indique el número del producto.");
        RuleFor(x => x.ProductNumber).MaximumLength(40);
    }
}

public sealed class IssuePaymentReferenceCommandHandler(
    IApplicationDbContext db,
    ReferenciasDePagoDeRecaudo referencias,
    IBusquedaDeObligaciones busqueda,
    AuditoriaDeRecaudo auditoria) : IRequestHandler<IssuePaymentReferenceCommand, Result<PaymentReferenceDto>>
{
    public async Task<Result<PaymentReferenceDto>> Handle(IssuePaymentReferenceCommand request, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<PaymentReferenceDto>(ErroresDeRecaudo.NoEncontrado("La persona"));
        var algoritmo = await referencias.AlgoritmoAsync(ct);
        PaymentReference fila;
        if (request.ProductPublicId is { } producto)
        {
            var encontrado = (await busqueda.PorNumeroAsync(new BusquedaPorNumero(request.ProductNumber!.Trim()), ct))
                .FirstOrDefault(o => o.ObligationPublicId == producto && o.PersonPublicId == persona.PublicId);
            if (encontrado is null) return Result.Failure<PaymentReferenceDto>(ErroresDeConvenios.ReferenciaSinProducto());
            fila = await referencias.RegistrarAsync(persona.Id, encontrado.Number, PaymentReferenceMode.ByProduct, producto,
                encontrado.Kind == ClaseDeObligacionEncontrada.Loan ? ApplicationTarget.Loan : ApplicationTarget.Account, encontrado.Number, algoritmo, ct);
        }
        else
        {
            fila = await referencias.RegistrarAsync(persona.Id, persona.TaxId, PaymentReferenceMode.ByPerson, null, null, null, algoritmo, ct);
        }
        await auditoria.AgregarAsync("Receipts.PaymentReference.Issued", "PaymentReference", fila.PublicId, null,
            new { reference = fila.Reference, mode = fila.Mode.ToString(), product = fila.ProductPublicId, algorithm = fila.Algorithm.ToString() }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ReferenciasDePagoDeRecaudo.DtoAsync(db, fila, ct));
    }
}

/// <summary><c>GET /api/receipts/payment-references?person=&amp;reference=</c> (<c>Receipts.Agreements.View</c>): las referencias registradas. (nuevo)</summary>
public sealed record ListPaymentReferencesQuery(Guid? PersonPublicId = null, string? Reference = null) : IRequest<Result<IReadOnlyList<PaymentReferenceDto>>>;

public sealed class ListPaymentReferencesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListPaymentReferencesQuery, Result<IReadOnlyList<PaymentReferenceDto>>>
{
    public async Task<Result<IReadOnlyList<PaymentReferenceDto>>> Handle(ListPaymentReferencesQuery request, CancellationToken ct)
    {
        var consulta = db.PaymentReferences.AsNoTracking();
        if (request.PersonPublicId is { } p)
        {
            var id = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(r => r.PersonId == id);
        }
        if (!string.IsNullOrWhiteSpace(request.Reference))
        {
            var r = DigitoDeVerificacion.Normalizar(request.Reference);
            consulta = consulta.Where(x => x.Reference == r || x.BaseNumber == r);
        }
        var filas = await consulta.OrderByDescending(r => r.Id).Take(200).ToListAsync(ct);
        var lista = new List<PaymentReferenceDto>(filas.Count);
        foreach (var f in filas) lista.Add(await ReferenciasDePagoDeRecaudo.DtoAsync(db, f, ct));
        return Result.Success<IReadOnlyList<PaymentReferenceDto>>(lista);
    }
}
