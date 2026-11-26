using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Personas;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.BulkCharges.PlantillaDeCargosMasivos.C;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

/// <summary>
/// La plantilla de los cargos masivos (2026-11-25, pedido del dueño; feature 026, HU4): un archivo por corrida en borrador con origen «plano». Cada fila es
/// un asociado por su documento y, si se quiere, su valor, cuotas, primer pago y forma de pago propios; lo que no trae sale de los datos de la corrida.
/// Aplicar reemplaza las filas del borrador y recalcula su vista previa. (nuevo)
/// </summary>
public static class PlantillaDeCargosMasivos
{
    public const string Clave = "lending.bulk-charges";

    public static class C
    {
        public const string Corrida = "corrida";
        public const string Persona = "persona";
        public const string Valor = "valor";
        public const string Cuotas = "cuotas";
        public const string PrimerPago = "primerPago";
        public const string FormaDePago = "formaDePago";
    }

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Cargos masivos", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla("Datos",
        [
            new(C.Corrida, TipoDeValor.Texto, Obligatoria: true, Largo: 20,
                Reglas: "número de la corrida de cargos masivos (CGM-…) en borrador y con origen «plano»; todo el archivo es de una sola corrida",
                Ejemplo: "CGM-000001"),
            new(C.Persona, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "llave; documento del asociado sin puntos ni dígito de verificación; debe existir en Maestros › Personas y ser asociado"),
            new(C.Valor, TipoDeValor.Monto, Reglas: "vacío = el de la corrida; mayor que cero y dentro de los montos de la línea", Ejemplo: "13557,00"),
            new(C.Cuotas, TipoDeValor.Entero, Reglas: "vacío = las de la corrida; al menos una", Ejemplo: ""),
            new(C.PrimerPago, TipoDeValor.Fecha, Reglas: "vacío = el de la corrida; posterior a la fecha del cargo", Ejemplo: ""),
            new(C.FormaDePago, TipoDeValor.Texto, Largo: 20,
                Reglas: "vacío = la de la corrida; Caja, NominaPropia, Pagaduria o DebitoAutomatico", Ejemplo: ""),
        ]),
    ]);

    /// <summary>La forma de pago escrita en el plano.</summary>
    public static RepaymentChannel? Forma(string? texto) => texto?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "CAJA" or "CASHIER" => RepaymentChannel.Cashier,
        "NOMINAPROPIA" or "NOMINA" or "OWNPAYROLL" => RepaymentChannel.OwnPayroll,
        "PAGADURIA" or "LIBRANZA" or "PAYMASTERDEDUCTION" => RepaymentChannel.PaymasterDeduction,
        "DEBITOAUTOMATICO" or "DIRECTDEBIT" => RepaymentChannel.DirectDebit,
        _ => (RepaymentChannel)(-1),
    };
}

public sealed record ImportBulkChargeLinesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportBulkChargeLinesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    CorridasDeCargoMasivo corridas,
    IDateTimeService reloj) : IRequestHandler<ImportBulkChargeLinesCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportBulkChargeLinesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(PlantillaDeCargosMasivos.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;
        if (filas.Count == 0) return;

        var numeros = filas.Select(f => f.Crudo(C.Corrida)).OfType<string>().Select(n => n.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToList();
        if (numeros.Count > 1)
        {
            foreach (var f in filas) f.Error(C.Corrida, ImportErrors.CellFormat, $"El archivo mezcla corridas ({string.Join(", ", numeros)}): suba un archivo por corrida.");
            return;
        }
        var numero = numeros.FirstOrDefault();
        var run = numero is null ? null : await db.BulkChargeRuns.FirstOrDefaultAsync(r => r.Number == numero && !r.IsDeleted, ct);
        string? problema = run switch
        {
            null => $"No hay una corrida de cargos masivos «{numero}».",
            { Status: not BulkChargeRunStatus.Draft } r => $"La corrida {numero} está «{r.Status}»: el plano sólo se sube mientras está en borrador.",
            { Source: not BulkChargeSource.File } => $"La corrida {numero} toma a los asociados de los filtros: cámbiela a origen «plano».",
            _ => null,
        };
        if (problema is not null || run is null)
        {
            foreach (var f in filas) f.Error(C.Corrida, ImportErrors.CellNotFound, problema ?? "La corrida no existe.");
            return;
        }

        var documentos = filas.Select(f => f.Crudo(C.Persona)).OfType<string>().Select(d => d.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        documentos = documentos.Concat(documentos.Select(d => UsuarioDeDocumento.NormalizarNumero(d))).Distinct().ToList();
        var encontradas = await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId) && !p.IsDeleted)
            .Select(p => new { p.Id, p.TaxId, p.Username, p.IsAssociate }).ToListAsync(ct);
        var personas = ResolutorDePersonaPorDocumento.Indexar(encontradas, p => p.Username, p => p.TaxId);

        var nuevas = new List<FilaDeCargoMasivo>();
        foreach (var fila in filas)
        {
            var documento = fila.Texto(C.Persona, 20);
            var valor = fila.Monto(C.Valor);
            var cuotas = fila.Entero(C.Cuotas);
            var primerPago = fila.Fecha(C.PrimerPago);
            var formaTexto = fila.Texto(C.FormaDePago, 20);
            fila.Requerido(C.Persona);
            if (!ctx.Datos.LlaveUnica(fila, documento, C.Persona) || fila.TieneErrores || documento is null) continue;
            if (valor is <= 0m) { fila.Error(C.Valor, ImportErrors.CellFormat, "El valor debe ser mayor que cero."); continue; }
            if (cuotas is < 1) { fila.Error(C.Cuotas, ImportErrors.CellFormat, "Las cuotas son al menos una."); continue; }
            if (primerPago is { } pp && pp <= run.ChargeDate)
            {
                fila.Error(C.PrimerPago, ImportErrors.CellFormat, $"El primer pago debe ser posterior a la fecha del cargo ({run.ChargeDate:dd/MM/yyyy}).");
                continue;
            }
            var forma = PlantillaDeCargosMasivos.Forma(formaTexto);
            if (forma is { } f0 && !Enum.IsDefined(f0))
            {
                fila.Error(C.FormaDePago, ImportErrors.CellFormat, "La forma de pago es Caja, NominaPropia, Pagaduria o DebitoAutomatico.");
                continue;
            }
            if (!personas.TryGet(null, documento, out var encontrada, out var ambigua))
            {
                fila.Error(C.Persona, ambigua ? ResolutorDePersonaPorDocumento.CodigoAmbiguo : ImportErrors.CellNotFound, ambigua
                    ? $"Hay varias personas con el documento «{documento}» y esta plantilla no trae el tipo de documento: no se puede saber a cuál corresponde."
                    : $"No hay una persona viva con documento «{documento}». Créela en Maestros › Personas: esta plantilla no crea personas.");
                continue;
            }
            if (!encontrada.IsAssociate)
            {
                fila.Error(C.Persona, ImportErrors.CellNotFound, $"La persona «{documento}» no es asociada: los cargos masivos son para asociados.");
                continue;
            }
            nuevas.Add(new FilaDeCargoMasivo(fila.Numero, encontrada.Id, valor, cuotas, primerPago, forma));
            ctx.Registrar(fila, documento, AccionDeImportacion.Create);
        }
        if (ctx.HayErrores || nuevas.Count == 0) return;

        // Las filas reemplazan las del borrador y la vista previa se recalcula con ellas.
        run.FiltersJson = new OrigenDeCargoMasivo(null, nuevas).Escribir();
        var calculo = await corridas.CalcularAsync(run, ct);
        if (calculo.IsFailure)
        {
            ctx.ErrorSinFila(null, C.Corrida, calculo.Error.Code, calculo.Error.Message);
            return;
        }
        await corridas.AplicarAsync(run, calculo.Value, reloj.UtcNow, ct);
    }
}
