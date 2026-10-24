using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Entities.Treasury.Remittances;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Imports;

/// <summary>
/// Las plantillas de puesta en marcha de Tesorería (feature 015, US13, T0287; contracts/archivos-y-plantillas.md §5; FR-157, FR-210): cuentas bancarias con su vínculo
/// contable (E-04) y su saldo al corte, chequeras, cheques girados en tránsito, remesas y consignaciones en tránsito, y las cifras de referencia. Sin hechos. (nuevo)
/// </summary>
public static class PlantillasDePuestaEnMarchaDeTesoreria
{
    public const string CuentasBancarias = "tesoreria.cuentas-bancarias";
    public const string Chequeras = "tesoreria.chequeras";
    public const string ChequesEnTransito = "tesoreria.cheques-en-transito";
    public const string RemesasEnTransito = "tesoreria.remesas-y-consignaciones-en-transito";
    public const string CifrasDeReferencia = "tesoreria.cifras-de-referencia";

    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Banco = "banco";
    public const string Numero = "numero";
    public const string Tipo = "tipo";
    public const string Oficina = "oficina";
    public const string Propositos = "propositos";
    public const string CuentaContable = "cuentaContable";
    public const string ExentaGmf = "exentaGmf";
    public const string SaldoAlCorte = "saldoAlCorte";
    public const string FechaDeCorte = "fechaDeCorte";
    public const string Cuenta = "cuenta";
    public const string Desde = "desde";
    public const string Hasta = "hasta";
    public const string Siguiente = "siguiente";
    public const string Beneficiario = "beneficiario";
    public const string Valor = "valor";
    public const string Fecha = "fecha";
    public const string Entregado = "entregado";
    public const string Concepto = "concepto";
    public const string Dimension = "dimension";

    /// <summary>Los propósitos por su nombre en la plantilla.</summary>
    public static readonly IReadOnlyDictionary<string, BankAccountPurpose> NombresDePropositos = new Dictionary<string, BankAccountPurpose>(StringComparer.OrdinalIgnoreCase)
    {
        ["Recaudo"] = BankAccountPurpose.Collection, ["Pagos"] = BankAccountPurpose.Payments, ["Nomina"] = BankAccountPurpose.Payroll,
        ["Convenio"] = BankAccountPurpose.Agreement, ["FondoDeLiquidez"] = BankAccountPurpose.LiquidityFund,
    };

    private static DefinicionDePlantilla Una(string clave, string nombre, params ColumnaDePlantilla[] columnas) =>
        new(clave, nombre, ModuloDeAuditoria.Treasury, [new HojaDePlantilla("Datos", columnas)]);

    public static IReadOnlyDictionary<string, DefinicionDePlantilla> Todas { get; } = new Dictionary<string, DefinicionDePlantilla>(StringComparer.OrdinalIgnoreCase)
    {
        [CuentasBancarias] = Una(CuentasBancarias, "Cuentas bancarias propias",
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "código de la cuenta; si ya existe, sólo se actualiza su saldo al corte", Ejemplo: "BCO01"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Bancolombia recaudo"),
            new(Banco, TipoDeValor.Banco, Obligatoria: true, Reglas: "banco con código de transferencia"),
            new(Numero, TipoDeValor.Texto, Obligatoria: true, Largo: 30),
            new(Tipo, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Savings (ahorros) o Checking (corriente)", Ejemplo: "Savings"),
            new(Oficina, TipoDeValor.Sucursal, Obligatoria: true),
            new(Propositos, TipoDeValor.Lista, Obligatoria: true, Reglas: "Recaudo, Pagos, Nomina, Convenio o FondoDeLiquidez; el fondo de liquidez va solo", Ejemplo: "Recaudo,Pagos"),
            new(CuentaContable, TipoDeValor.CuentaContable, Reglas: "código de la auxiliar bancaria del plan (vínculo contable, E-04)"),
            new(ExentaGmf, TipoDeValor.SiNo, Reglas: "vacío = no"),
            new(SaldoAlCorte, TipoDeValor.Monto, Reglas: "saldo en libros al corte; el cuadre lo compara con Contabilidad"),
            new(FechaDeCorte, TipoDeValor.Fecha, Reglas: "obligatoria si hay saldo al corte")),
        [Chequeras] = Una(Chequeras, "Chequeras",
            new(Cuenta, TipoDeValor.Codigo, Obligatoria: true, Largo: 10),
            new(Desde, TipoDeValor.Entero, Obligatoria: true),
            new(Hasta, TipoDeValor.Entero, Obligatoria: true),
            new(Siguiente, TipoDeValor.Entero, Obligatoria: true, Reglas: "el próximo número sin usar, dentro del rango")),
        [ChequesEnTransito] = Una(ChequesEnTransito, "Cheques girados en tránsito",
            new(Cuenta, TipoDeValor.Codigo, Obligatoria: true, Largo: 10),
            new(Numero, TipoDeValor.Entero, Obligatoria: true, Reglas: "dentro de una chequera registrada antes de la cuenta"),
            new(Beneficiario, TipoDeValor.Persona, Obligatoria: true, Largo: 20),
            new(Valor, TipoDeValor.Monto, Obligatoria: true),
            new(Fecha, TipoDeValor.Fecha, Obligatoria: true),
            new(Entregado, TipoDeValor.SiNo, Reglas: "vacío = no")),
        [RemesasEnTransito] = Una(RemesasEnTransito, "Remesas y consignaciones en tránsito",
            new(Cuenta, TipoDeValor.Codigo, Obligatoria: true, Largo: 10),
            new(Oficina, TipoDeValor.Sucursal, Obligatoria: true),
            new(Valor, TipoDeValor.Monto, Obligatoria: true),
            new(Fecha, TipoDeValor.Fecha, Obligatoria: true),
            new(Tipo, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Remesa o Consignacion")),
        [CifrasDeReferencia] = Una(CifrasDeReferencia, "Cifras de referencia de Tesorería",
            new(FechaDeCorte, TipoDeValor.Fecha, Obligatoria: true),
            new(Concepto, TipoDeValor.Codigo, Obligatoria: true, Largo: 40, Reglas: "Banco, ChequesGiradosEnTransito o EfectivoEnTransito (o un concepto propio)"),
            new(Dimension, TipoDeValor.Codigo, Obligatoria: true, Largo: 40, Reglas: "el código de la cuenta propia (o de la oficina, en efectivo en tránsito)"),
            new(Valor, TipoDeValor.Monto, Obligatoria: true)),
    };

    public static DefinicionDePlantilla? Por(string codigo) => Todas.GetValueOrDefault(codigo);
}

/// <summary>
/// <c>POST /api/treasury/imports/{code}/import?mode=review|apply</c> (<c>Treasury.Imports.Run</c>, con clave; US13, T0287): las cuatro plantillas de Tesorería y la de
/// cifras de referencia, un solo comando por código. Sin hechos contables (FR-158). (nuevo)
/// </summary>
public sealed record ImportTreasuryGoLiveTemplateCommand(string Code, ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportTreasuryGoLiveTemplateCommandValidator : AbstractValidator<ImportTreasuryGoLiveTemplateCommand>
{
    public ImportTreasuryGoLiveTemplateCommandValidator()
    {
        RuleFor(x => x.Code).Must(c => PlantillasDePuestaEnMarchaDeTesoreria.Por(c) is not null).WithMessage("No hay una plantilla de puesta en marcha de Tesorería con ese código.");
        RuleFor(x => x.File).NotNull();
    }
}

public sealed class ImportTreasuryGoLiveTemplateCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    IContabilidadParaTesoreria contabilidad,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ImportTreasuryGoLiveTemplateCommand, Result<ImportResultDto>>
{
    private enum TipoDeTransito { Remesa = 1, Consignacion = 2 }

    public const string CodigoRangoCruzado = "Treasury.Imports.CheckbookRangeOverlaps";
    public const string CodigoSinChequera = "Treasury.Imports.CheckbookNotFound";

    public Task<Result<ImportResultDto>> Handle(ImportTreasuryGoLiveTemplateCommand request, CancellationToken ct)
    {
        var plantilla = PlantillasDePuestaEnMarchaDeTesoreria.Por(request.Code)!;
        return ejecutor.EjecutarAsync(plantilla, request, (ctx, c) => plantilla.Clave switch
        {
            PlantillasDePuestaEnMarchaDeTesoreria.CuentasBancarias => CuentasAsync(ctx, c),
            PlantillasDePuestaEnMarchaDeTesoreria.Chequeras => ChequerasAsync(ctx, c),
            PlantillasDePuestaEnMarchaDeTesoreria.ChequesEnTransito => ChequesAsync(ctx, c),
            PlantillasDePuestaEnMarchaDeTesoreria.RemesasEnTransito => RemesasAsync(ctx, c),
            _ => CifrasAsync(ctx, request.File.NombreArchivo, c),
        }, ct);
    }

    private static string Nuevo(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}"[..Math.Min(30, prefijo.Length + 17)];

    private async Task<CatalogoCitado<OwnBankAccount>> CuentasPropiasAsync(CancellationToken ct) =>
        CatalogoCitado<OwnBankAccount>.Desde(await db.OwnBankAccounts.AsNoTracking().ToListAsync(ct), c => c, c => c.Code);

    // ----------------------------------------------------------------------------------------------------------------- cuentas bancarias --
    private async Task CuentasAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        const string T = PlantillasDePuestaEnMarchaDeTesoreria.Codigo;
        var hoy = reloj.HoyLocal;
        var oficinas = CatalogoCitado<Branch>.Desde(await db.Branches.AsNoTracking().ToListAsync(ct), b => b, b => b.LegacyCode, b => b.Name);
        var bancos = CatalogoCitado<Bank>.Desde(await db.Banks.AsNoTracking().Where(b => !b.IsDeleted).ToListAsync(ct), b => b, b => b.Name, b => b.LegacyCode, b => b.TransferCode);
        var existentes = await db.OwnBankAccounts.ToListAsync(ct);
        var plan = await contabilidad.CuentasBancariasDelPlanAsync(null, ct);
        foreach (var fila in ctx.Datos.Filas)
        {
            var codigo = CodigoDeCatalogo.Normalizar(fila.Codigo(T, 10));
            var nombre = fila.Texto(PlantillasDePuestaEnMarchaDeTesoreria.Nombre, 120);
            var banco = fila.Referencia(PlantillasDePuestaEnMarchaDeTesoreria.Banco, bancos, "un banco", "en Maestros → Bancos");
            var numero = fila.Texto(PlantillasDePuestaEnMarchaDeTesoreria.Numero, 30);
            var tipo = fila.Enumeracion<OwnBankAccountType>(PlantillasDePuestaEnMarchaDeTesoreria.Tipo);
            var oficina = fila.Referencia(PlantillasDePuestaEnMarchaDeTesoreria.Oficina, oficinas, "una oficina", "en Maestros → Agencias");
            var lista = fila.Lista(PlantillasDePuestaEnMarchaDeTesoreria.Propositos) ?? [];
            var contable = fila.Codigo(PlantillasDePuestaEnMarchaDeTesoreria.CuentaContable, 20);
            var exenta = fila.SiNo(PlantillasDePuestaEnMarchaDeTesoreria.ExentaGmf);
            var saldo = fila.Monto(PlantillasDePuestaEnMarchaDeTesoreria.SaldoAlCorte);
            var corte = fila.Fecha(PlantillasDePuestaEnMarchaDeTesoreria.FechaDeCorte);
            var propositos = BankAccountPurpose.None;
            foreach (var p in lista)
                if (PlantillasDePuestaEnMarchaDeTesoreria.NombresDePropositos.TryGetValue(p, out var x)) propositos |= x;
                else fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Propositos, ImportErrors.CellFormat, $"«{p}» no es un propósito: Recaudo, Pagos, Nomina, Convenio o FondoDeLiquidez.");
            var regla = CuentasPropias.Propositos(propositos);
            if (lista.Count > 0 && regla.IsFailure) fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Propositos, regla.Error.Code, regla.Error.Message);
            if (banco is not null && string.IsNullOrWhiteSpace(banco.TransferCode))
                fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Banco, ImportErrors.CellFormat, $"El banco {banco.Name} no tiene código de transferencia (ACH).");
            if (saldo is not null && corte is null) fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.FechaDeCorte, ImportErrors.CellRequired, "Con saldo al corte va su fecha de corte.");
            var auxiliar = contable is null ? null : plan.FirstOrDefault(c => string.Equals(c.Code, contable, StringComparison.OrdinalIgnoreCase));
            if (contable is not null && auxiliar is null)
                fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.CuentaContable, ImportErrors.CellNotFound, $"No hay una cuenta bancaria de movimiento «{contable}» en el plan de cuentas.");
            if (codigo is not null) ctx.Datos.LlaveUnica(fila, codigo, T);
            if (banco is not null && numero is not null) ctx.Datos.LlaveUnica(fila, $"{banco.Id}|{numero}", PlantillasDePuestaEnMarchaDeTesoreria.Numero);
            if (fila.TieneErrores || codigo is null || nombre is null || banco is null || numero is null || tipo is null || oficina is null) continue;

            var existente = existentes.FirstOrDefault(c => c.Code == codigo);
            if (existente is not null)
            {
                if (existente.BankId != banco.Id || existente.AccountNumber != numero.Trim())
                {
                    fila.Error(T, ImportErrors.RowDuplicate, $"Ya existe la cuenta «{codigo}» con otro banco o número: el banco y el número de una cuenta no cambian.");
                    continue;
                }
                existente.OpeningBalance = saldo;
                existente.OpeningBalanceOn = corte;
                ctx.Registrar(fila, codigo, AccionDeImportacion.Update, [new CampoCambiadoDto(PlantillasDePuestaEnMarchaDeTesoreria.SaldoAlCorte, null, saldo?.ToString())]);
                continue;
            }
            if (existentes.Any(c => c.BankId == banco.Id && c.AccountNumber == numero.Trim()))
            {
                fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Numero, ImportErrors.RowDuplicate, $"La cuenta {numero} de ese banco ya está registrada con otro código.");
                continue;
            }
            var cuenta = new OwnBankAccount
            {
                Code = codigo, Name = nombre, BankId = banco.Id, AccountNumber = numero.Trim(), AccountType = tipo.Value, BranchId = oficina.Id, Purposes = propositos,
                IsGmfExempt = exenta, ValidFrom = corte?.AddDays(1) ?? hoy, OpeningBalance = saldo, OpeningBalanceOn = corte,
            };
            db.OwnBankAccounts.Add(cuenta);
            if (auxiliar is not null)
            {
                var vinculo = await contabilidad.VincularCuentaBancariaAsync(cuenta.PublicId, auxiliar.ChartAccountPublicId, cuenta.ValidFrom, ct);
                if (vinculo.IsFailure) fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.CuentaContable, vinculo.Error.Code, vinculo.Error.Message);
            }
            ctx.Registrar(fila, codigo, AccionDeImportacion.Create);
        }
    }

    // --------------------------------------------------------------------------------------------------------------------------- chequeras --
    private async Task ChequerasAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var cuentas = await CuentasPropiasAsync(ct);
        var existentes = await db.Checkbooks.AsNoTracking().Where(k => k.Status != CheckbookState.Voided).ToListAsync(ct);
        var nuevas = new List<Checkbook>();
        foreach (var fila in ctx.Datos.Filas)
        {
            var cuenta = fila.Referencia(PlantillasDePuestaEnMarchaDeTesoreria.Cuenta, cuentas, "una cuenta bancaria propia", "con la plantilla tesoreria.cuentas-bancarias");
            var desde = fila.Entero(PlantillasDePuestaEnMarchaDeTesoreria.Desde);
            var hasta = fila.Entero(PlantillasDePuestaEnMarchaDeTesoreria.Hasta);
            var siguiente = fila.Entero(PlantillasDePuestaEnMarchaDeTesoreria.Siguiente);
            if (desde is { } d && hasta is { } h && (h < d || d <= 0))
                fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Hasta, ImportErrors.CellFormat, "El rango no es válido: «hasta» no puede ser menor que «desde».");
            if (desde is { } d2 && hasta is { } h2 && siguiente is { } s && (s < d2 || s > h2 + 1))
                fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Siguiente, ImportErrors.CellFormat, "El siguiente número cae fuera del rango de la chequera.");
            if (cuenta is not null && desde is { } d3 && hasta is { } h3
                && existentes.Concat(nuevas).Any(k => k.BankAccountId == cuenta.Id && k.FromNumber <= h3 && d3 <= k.ToNumber))
                fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Desde, CodigoRangoCruzado, "El rango se cruza con otra chequera de la cuenta.");
            if (fila.TieneErrores || cuenta is null || desde is null || hasta is null || siguiente is null || actor.UserId is not { } usuario) continue;
            var chequera = new Checkbook
            {
                BankAccountId = cuenta.Id, FromNumber = desde.Value, ToNumber = hasta.Value, NextNumber = siguiente.Value,
                Status = siguiente.Value > hasta.Value ? CheckbookState.Exhausted : CheckbookState.Active, CustodianUserId = usuario, RegisteredByUserId = usuario,
                RegisteredAt = reloj.UtcNow,
            };
            nuevas.Add(chequera);
            db.Checkbooks.Add(chequera);
            ctx.Registrar(fila, $"{cuenta.Code}|{desde}-{hasta}", AccionDeImportacion.Create);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------- cheques en tránsito --
    private async Task ChequesAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var cuentas = await CuentasPropiasAsync(ct);
        var chequeras = await db.Checkbooks.AsNoTracking().Where(k => k.Status != CheckbookState.Voided).ToListAsync(ct);
        var documentos = ctx.Datos.Filas.Select(f => f.Crudo(PlantillasDePuestaEnMarchaDeTesoreria.Beneficiario)).OfType<string>().Distinct().ToList();
        var personas = CatalogoCitado<int>.Desde(await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId)).Select(p => new { p.Id, p.TaxId }).ToListAsync(ct),
            p => p.Id, p => p.TaxId);
        var usados = await db.IssuedChecks.AsNoTracking().Select(c => new { c.CheckbookId, c.CheckNumber }).ToListAsync(ct);
        foreach (var fila in ctx.Datos.Filas)
        {
            var cuenta = fila.Referencia(PlantillasDePuestaEnMarchaDeTesoreria.Cuenta, cuentas, "una cuenta bancaria propia", "con la plantilla tesoreria.cuentas-bancarias");
            var numero = fila.Entero(PlantillasDePuestaEnMarchaDeTesoreria.Numero);
            var beneficiario = fila.Referencia(PlantillasDePuestaEnMarchaDeTesoreria.Beneficiario, personas, "una persona con documento", "en Personas");
            var valor = fila.Monto(PlantillasDePuestaEnMarchaDeTesoreria.Valor);
            var fecha = fila.Fecha(PlantillasDePuestaEnMarchaDeTesoreria.Fecha);
            var entregado = fila.SiNo(PlantillasDePuestaEnMarchaDeTesoreria.Entregado);
            var chequera = cuenta is null || numero is null ? null : chequeras.FirstOrDefault(k => k.BankAccountId == cuenta.Id && k.FromNumber <= numero && numero <= k.ToNumber);
            if (cuenta is not null && numero is not null && chequera is null)
                fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Numero, CodigoSinChequera,
                    "El número no está en ninguna chequera registrada de la cuenta: cargue antes la chequera (tesoreria.chequeras).");
            if (chequera is not null && usados.Any(u => u.CheckbookId == chequera.Id && u.CheckNumber == numero))
                fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Numero, ImportErrors.RowDuplicate, "Ese cheque ya está registrado.");
            if (valor is <= 0m) fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Valor, ImportErrors.CellFormat, "El valor tiene que ser mayor que cero.");
            if (cuenta is not null && numero is not null) ctx.Datos.LlaveUnica(fila, $"{cuenta.Code}|{numero}", PlantillasDePuestaEnMarchaDeTesoreria.Numero);
            if (fila.TieneErrores || chequera is null || numero is null || beneficiario == 0 || valor is null || fecha is null) continue;
            db.IssuedChecks.Add(new IssuedCheck
            {
                CheckbookId = chequera.Id, CheckNumber = numero.Value, BeneficiaryPersonId = beneficiario, Amount = valor.Value, IssuedOn = fecha.Value,
                Status = entregado ? IssuedCheckState.Delivered : IssuedCheckState.Issued, DeliveredOn = entregado ? fecha : null, IsOpeningLoad = true,
            });
            ctx.Registrar(fila, $"{cuenta!.Code}|{numero}", AccionDeImportacion.Create);
        }
    }

    // ------------------------------------------------------------------------------------------------------ remesas y consignaciones --
    private async Task RemesasAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var cuentas = await CuentasPropiasAsync(ct);
        var oficinas = CatalogoCitado<Branch>.Desde(await db.Branches.AsNoTracking().ToListAsync(ct), b => b, b => b.LegacyCode, b => b.Name);
        foreach (var fila in ctx.Datos.Filas)
        {
            var cuenta = fila.Referencia(PlantillasDePuestaEnMarchaDeTesoreria.Cuenta, cuentas, "una cuenta bancaria propia", "con la plantilla tesoreria.cuentas-bancarias");
            var oficina = fila.Referencia(PlantillasDePuestaEnMarchaDeTesoreria.Oficina, oficinas, "una oficina", "en Maestros → Agencias");
            var valor = fila.Monto(PlantillasDePuestaEnMarchaDeTesoreria.Valor);
            var fecha = fila.Fecha(PlantillasDePuestaEnMarchaDeTesoreria.Fecha);
            var tipo = fila.Enumeracion<TipoDeTransito>(PlantillasDePuestaEnMarchaDeTesoreria.Tipo);
            if (valor is <= 0m) fila.Error(PlantillasDePuestaEnMarchaDeTesoreria.Valor, ImportErrors.CellFormat, "El valor tiene que ser mayor que cero.");
            if (fila.TieneErrores || cuenta is null || oficina is null || valor is null || fecha is null || tipo is null) continue;
            db.CashRemittances.Add(new CashRemittance
            {
                Number = Nuevo("RM-PM"), Direction = RemittanceDirection.VaultToBank, BranchId = oficina.Id, VaultPublicId = Guid.Empty, BankAccountId = cuenta.Id,
                Amount = valor.Value, IdempotencyKey = Guid.NewGuid(), DispatchDate = fecha.Value, DispatchedAt = fecha.Value.ToDateTime(new TimeOnly(12, 0)),
                Status = RemittanceState.InTransit, IsOpeningLoad = true, SourceDocumentNumber = tipo == TipoDeTransito.Consignacion ? "Consignación al corte" : "Remesa al corte",
            });
            ctx.Registrar(fila, $"{cuenta.Code}|{fecha:yyyy-MM-dd}|{valor}", AccionDeImportacion.Create);
        }
    }

    // ------------------------------------------------------------------------------------------------------------- cifras de referencia --
    private async Task CifrasAsync(ContextoDeImportacion ctx, string archivo, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var lote = Guid.NewGuid();
        var leidas = new List<(FilaDeImportacion Fila, DateOnly Corte, string Concepto, string Dimension, decimal Valor)>();
        foreach (var fila in ctx.Datos.Filas)
        {
            var corte = fila.Fecha(PlantillasDePuestaEnMarchaDeTesoreria.FechaDeCorte);
            var concepto = fila.Codigo(PlantillasDePuestaEnMarchaDeTesoreria.Concepto, 40);
            var dimension = fila.Codigo(PlantillasDePuestaEnMarchaDeTesoreria.Dimension, 40);
            var valor = fila.Monto(PlantillasDePuestaEnMarchaDeTesoreria.Valor);
            if (corte is not null && concepto is not null && dimension is not null)
                ctx.Datos.LlaveUnica(fila, $"{corte:yyyy-MM-dd}|{concepto}|{dimension}", PlantillasDePuestaEnMarchaDeTesoreria.Dimension);
            if (fila.TieneErrores || corte is null || concepto is null || dimension is null || valor is null) continue;
            leidas.Add((fila, corte.Value, concepto, dimension, valor.Value));
        }
        if (ctx.HayErrores || actor.UserId is not { } usuario) return;
        var cortes = leidas.Select(l => l.Corte).Distinct().ToList();
        var anteriores = await db.TreasuryReferenceFigures.Where(c => cortes.Contains(c.CutoffDate)).ToListAsync(ct);
        foreach (var l in leidas)
        {
            foreach (var a in anteriores.Where(a => a.CutoffDate == l.Corte && string.Equals(a.Concept, l.Concepto, StringComparison.OrdinalIgnoreCase)
                                                    && string.Equals(a.Dimension, l.Dimension, StringComparison.OrdinalIgnoreCase)))
            {
                a.IsDeleted = true;
                a.DeletedAt = reloj.UtcNow;
                a.DeletedBy = actor.Name;
            }
            db.TreasuryReferenceFigures.Add(new TreasuryReferenceFigure
            {
                CutoffDate = l.Corte, Concept = l.Concepto, Dimension = l.Dimension, Amount = l.Valor, LoadedByUserId = usuario, LoadedAt = reloj.UtcNow, ImportBatchPublicId = lote,
                SourceFileName = archivo.Length <= 200 ? archivo : archivo[..200],
            });
            ctx.Registrar(l.Fila, $"{l.Corte:yyyy-MM-dd}|{l.Concepto}|{l.Dimension}", AccionDeImportacion.Create);
        }
        ctx.Extra["batchPublicId"] = lote;
    }
}
