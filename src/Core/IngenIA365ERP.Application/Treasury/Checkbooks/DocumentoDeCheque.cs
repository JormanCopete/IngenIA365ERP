using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Checkbooks;

/// <summary>Lo que se imprime de un cheque girado sobre el formato de la chequera (feature 015, US19, T0229; archivos-y-plantillas.md §7). (nuevo)</summary>
public sealed record ChequeModel(string Company, string BankName, string AccountNumber, long CheckNumber, DateOnly IssuedOn, string Beneficiary, decimal Amount,
    string AmountInWords, bool Crossed, bool FirstBeneficiaryOnly, string OrderNumber, string Concept);

/// <summary>El documento del cheque en PDF: lo implementa la API con QuestPDF. (nuevo)</summary>
public interface IDocumentosDeTesoreriaEnPdf
{
    byte[] Cheque(ChequeModel modelo);
}

/// <summary><c>GET /api/treasury/issued-checks/{id}/document</c> (<c>Treasury.Checks.Print</c>): el modelo del cheque emitido para imprimirlo. (nuevo)</summary>
public sealed record GetIssuedCheckDocumentQuery(Guid IssuedCheckPublicId) : IRequest<Result<ChequeModel>>;

public sealed class GetIssuedCheckDocumentQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance) : IRequestHandler<GetIssuedCheckDocumentQuery, Result<ChequeModel>>
{
    public async Task<Result<ChequeModel>> Handle(GetIssuedCheckDocumentQuery request, CancellationToken ct)
    {
        var c = await ChequerasDeTesoreria.ChequeVisibleAsync(db, alcance, request.IssuedCheckPublicId, seguir: false, ct);
        if (c is null || c.PaymentOrderId is not { } ordenId || c.Status is IssuedCheckState.SpoiledBlank or IssuedCheckState.Voided)
            return Result.Failure<ChequeModel>(ErroresDeTesoreria.NoEncontrado("El cheque"));
        var cuenta = await (from k in db.Checkbooks.AsNoTracking()
                            join a in db.OwnBankAccounts.AsNoTracking() on k.BankAccountId equals a.Id
                            join b in db.Banks.AsNoTracking().IgnoreQueryFilters() on a.BankId equals b.Id
                            where k.Id == c.CheckbookId
                            select new { a.AccountNumber, Banco = b.Name }).FirstAsync(ct);
        var orden = await db.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == ordenId, ct);
        var empresa = await db.Companies.AsNoTracking().Select(e => e.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
        return Result.Success(new ChequeModel(empresa, cuenta.Banco, cuenta.AccountNumber, c.CheckNumber, c.IssuedOn, orden.BeneficiaryName, c.Amount,
            ValorEnLetras.Pesos(c.Amount), c.Crossed, c.FirstBeneficiaryOnly, orden.Number, orden.Concept));
    }
}

/// <summary>El valor en letras de un cheque (pesos y centavos), en español. (nuevo)</summary>
public static class ValorEnLetras
{
    private static readonly string[] Unidades = ["", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez", "once", "doce", "trece", "catorce",
        "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve", "veinte", "veintiuno", "veintidós", "veintitrés", "veinticuatro", "veinticinco", "veintiséis",
        "veintisiete", "veintiocho", "veintinueve"];

    private static readonly string[] Decenas = ["", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa"];

    private static readonly string[] Centenas = ["", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos", "seiscientos", "setecientos", "ochocientos",
        "novecientos"];

    public static string Pesos(decimal valor)
    {
        var entero = (long)Math.Floor(Math.Abs(valor));
        var centavos = (int)Math.Round((Math.Abs(valor) - entero) * 100m);
        var texto = entero == 0 ? "cero" : Numero(entero);
        var pesos = texto + (entero % 1_000_000 == 0 && entero > 0 ? " de pesos" : " pesos");
        return (centavos > 0 ? $"{pesos} con {Numero(centavos)} centavos" : pesos + " m/cte").Trim();
    }

    private static string Numero(long n)
    {
        if (n >= 1_000_000)
        {
            var millones = n / 1_000_000;
            var resto = n % 1_000_000;
            var m = millones == 1 ? "un millón" : Numero(millones).Replace("uno", "un", StringComparison.Ordinal) + " millones";
            return resto == 0 ? m : $"{m} {Numero(resto)}";
        }
        if (n >= 1000)
        {
            var miles = n / 1000;
            var resto = n % 1000;
            var m = miles == 1 ? "mil" : Numero(miles).Replace("uno", "un", StringComparison.Ordinal) + " mil";
            return resto == 0 ? m : $"{m} {Numero(resto)}";
        }
        if (n >= 100)
        {
            if (n == 100) return "cien";
            var resto = n % 100;
            return resto == 0 ? Centenas[n / 100] : $"{Centenas[n / 100]} {Numero(resto)}";
        }
        if (n < 30) return Unidades[n];
        var u = n % 10;
        return u == 0 ? Decenas[n / 10] : $"{Decenas[n / 10]} y {Unidades[u]}";
    }
}
