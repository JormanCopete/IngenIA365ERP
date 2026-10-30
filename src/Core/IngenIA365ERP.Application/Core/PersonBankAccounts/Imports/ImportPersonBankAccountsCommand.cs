using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.PersonBankAccounts.Imports;

/// <summary>
/// La plantilla <c>plataforma.cuentas-bancarias-de-personas</c> (feature 015, US13, T0287; archivos-y-plantillas.md §5; FR-157, FR-210): las cuentas destino de las
/// personas al corte. Entran <c>PendingApproval</c> con origen <c>Imported</c> y las aprueba <b>otra persona</b> por la ruta de siempre (<c>…/approve</c>); sin
/// certificación, avisa (la revisa quien aprueba). Una cuenta igual viva de la misma persona no se repite. (nuevo)
/// </summary>
public static class PlantillaDeCuentasBancariasDePersonas
{
    public const string Clave = "plataforma.cuentas-bancarias-de-personas";
    public const string Documento = "documento";
    public const string Banco = "banco";
    public const string Tipo = "tipo";
    public const string Numero = "numero";
    public const string Titular = "titular";
    public const string DocumentoTitular = "documentoTitular";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Cuentas bancarias de personas", ModuloDeAuditoria.Core,
    [
        new HojaDePlantilla("Datos",
        [
            new(Documento, TipoDeValor.Persona, Obligatoria: true, Largo: 20, Reglas: "documento de la persona dueña de la cuenta"),
            new(Banco, TipoDeValor.Banco, Obligatoria: true),
            new(Tipo, TipoDeValor.Texto, Obligatoria: true, Largo: 20, Reglas: "Ahorros o Corriente", Ejemplo: "Ahorros"),
            new(Numero, TipoDeValor.Texto, Obligatoria: true, Largo: 30, Reglas: "lleva dígitos"),
            new(Titular, TipoDeValor.Texto, Obligatoria: true, Largo: 200, Reglas: "como en la certificación bancaria"),
            new(DocumentoTitular, TipoDeValor.Texto, Obligatoria: true, Largo: 20),
        ]),
    ]);
}

/// <summary><c>POST /api/treasury/imports/plataforma.cuentas-bancarias-de-personas/import</c> (<c>Treasury.Imports.Run</c>, con clave). (nuevo)</summary>
public sealed record ImportPersonBankAccountsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportPersonBankAccountsCommandValidator : AbstractValidator<ImportPersonBankAccountsCommand>
{
    public ImportPersonBankAccountsCommandValidator() => RuleFor(x => x.File).NotNull();
}

public sealed class ImportPersonBankAccountsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, IActorActual actorActual)
    : IRequestHandler<ImportPersonBankAccountsCommand, Result<ImportResultDto>>
{
    public const string CodigoSinCertificacion = "Core.PersonBankAccount.ImportedWithoutCertificate";

    public Task<Result<ImportResultDto>> Handle(ImportPersonBankAccountsCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(PlantillaDeCuentasBancariasDePersonas.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        const string D = PlantillaDeCuentasBancariasDePersonas.Documento;
        var actor = await actorActual.ObtenerAsync(ct);
        var documentos = ctx.Datos.Filas.Select(f => f.Crudo(D)).OfType<string>().Distinct().ToList();
        var personas = CatalogoCitado<int>.Desde(await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId)).Select(p => new { p.Id, p.TaxId }).ToListAsync(ct),
            p => p.Id, p => p.TaxId);
        var bancos = CatalogoCitado<Bank>.Desde(await db.Banks.AsNoTracking().Where(b => !b.IsDeleted).ToListAsync(ct), b => b, b => b.Name, b => b.LegacyCode, b => b.TransferCode);
        var vivas = await db.PersonBankAccounts.AsNoTracking()
            .Where(c => c.Status == PersonBankAccountState.Active || c.Status == PersonBankAccountState.PendingApproval)
            .Select(c => new { c.PersonId, c.BankId, c.AccountNumber }).ToListAsync(ct);
        foreach (var fila in ctx.Datos.Filas)
        {
            var persona = fila.Referencia(D, personas, "una persona con documento", "en Personas");
            var banco = fila.Referencia(PlantillaDeCuentasBancariasDePersonas.Banco, bancos, "un banco", "en Maestros → Bancos");
            var tipo = fila.Texto(PlantillaDeCuentasBancariasDePersonas.Tipo, 20);
            var numeroCrudo = fila.Texto(PlantillaDeCuentasBancariasDePersonas.Numero, 30);
            var titular = fila.Texto(PlantillaDeCuentasBancariasDePersonas.Titular, 200);
            var documentoTitular = fila.Texto(PlantillaDeCuentasBancariasDePersonas.DocumentoTitular, 20);
            if (numeroCrudo is not null && !numeroCrudo.Any(char.IsDigit))
                fila.Error(PlantillaDeCuentasBancariasDePersonas.Numero, ImportErrors.CellFormat, "El número de la cuenta lleva dígitos.");
            if (fila.TieneErrores || persona == 0 || banco is null || tipo is null || numeroCrudo is null || titular is null || documentoTitular is null) continue;
            var numero = CuentasBancariasDePersonaComun.Normalizar(numeroCrudo);
            if (!ctx.Datos.LlaveUnica(fila, $"{persona}|{banco.Id}|{numero}", PlantillaDeCuentasBancariasDePersonas.Numero)) continue;
            if (vivas.Any(v => v.PersonId == persona && v.BankId == banco.Id && v.AccountNumber == numero))
            {
                ctx.Registrar(fila, $"{fila.Crudo(D)}|{banco.Name}|{numero}", AccionDeImportacion.Unchanged);
                continue;
            }
            fila.Aviso(PlantillaDeCuentasBancariasDePersonas.Numero, CodigoSinCertificacion,
                "La cuenta entra sin certificación bancaria: quien la aprueba tiene que verificar la titularidad.");
            db.PersonBankAccounts.Add(new PersonBankAccount
            {
                PersonId = persona, BankId = banco.Id, AccountType = tipo, AccountNumber = numero, HolderName = titular, HolderTaxId = documentoTitular,
                Origin = PersonBankAccountOrigin.Imported, Status = PersonBankAccountState.PendingApproval, RequestedByUserId = actor.UserId, RequestedByName = actor.Name,
            });
            ctx.Registrar(fila, $"{fila.Crudo(D)}|{banco.Name}|{numero}", AccionDeImportacion.Create);
        }
    }
}
