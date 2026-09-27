using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeTopesDeDescuento;

namespace IngenIA365ERP.Application.Inventory.Imports;

/// <summary>
/// Plantilla 13 — topes de descuento (feature 012, I3, T600; contracts/plantillas.md §13). Escribe <c>INV_DiscountCaps</c>. Llave:
/// rol + <c>vigenteDesde</c>. Los porcentajes se escriben en puntos (5 = 5 %) y se guardan como fracción. (nuevo)
/// </summary>
public static class PlantillaDeTopesDeDescuento
{
    public const string Clave = CatalogoDePlantillas.TopesDeDescuentoClave;
    public const string Hoja = "Datos";

    public const string Rol = "rol";
    public const string TopeLinea = "topeLineaPorcentaje";
    public const string TopeDocumento = "topeDocumentoPorcentaje";
    public const string VigenteDesde = "vigenteDesde";
    public const string VigenteHasta = "vigenteHasta";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Topes de descuento", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla(Hoja,
        [
            new(Rol, TipoDeValor.Codigo, Obligatoria: true, Largo: 50, Reglas: "código del rol de la cooperativa", Ejemplo: "CAJERO"),
            new(TopeLinea, TipoDeValor.Porcentaje, Obligatoria: true, Reglas: "0 a 100, en puntos", Ejemplo: "5"),
            new(TopeDocumento, TipoDeValor.Porcentaje, Obligatoria: true, Reglas: "0 a 100, en puntos", Ejemplo: "3"),
            new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "una vigencia nueva cierra la anterior del rol la víspera; no se cruzan", Ejemplo: "AAAA-MM-01"),
            new(VigenteHasta, TipoDeValor.Fecha, Reglas: "vacío = sin cierre", Ejemplo: ""),
        ]),
    ]);
}

/// <summary>
/// La plantilla 13 (feature 012, I3, T600; contracts/plantillas.md §13, §0.5; <c>POST /api/inventory/discount-caps/import</c>): con
/// <b>la misma regla que el alta</b> (<see cref="ReglasDeTopesDeDescuento"/>), todo o nada y siempre con motivo cuando crea o cambia
/// una vigencia. Un rol + fecha que ya existe actualiza sus topes y su cierre; si queda igual, «sin cambio». (nuevo)
/// </summary>
public sealed record ImportDiscountCapsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportDiscountCapsCommandValidator : AbstractValidator<ImportDiscountCapsCommand>
{
    public ImportDiscountCapsCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportDiscountCapsCommand>.LargoMaximo);
    }
}

public sealed class ImportDiscountCapsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, ICerrojoPorClave cerrojo)
    : IRequestHandler<ImportDiscountCapsCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportDiscountCapsCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var roles = CatalogoCitado<Role>.Desde(await db.Roles.AsNoTracking().Where(r => !r.IsDeleted).ToListAsync(ct), r => r, r => r.Code);
        var hoja = ctx.Datos;
        // Las filas de un rol se aplican en orden de fecha: así un archivo con dos vigencias del mismo rol cierra la primera la víspera de la segunda.
        var filas = hoja.Filas.Select(f => (Fila: f, Desde: f.Fecha(P.VigenteDesde))).OrderBy(x => x.Desde ?? DateOnly.MaxValue).ToList();
        foreach (var (fila, desde) in filas)
        {
            var rol = fila.Referencia(P.Rol, roles, "un rol", "en Seguridad → Roles");
            var linea = fila.Porcentaje(P.TopeLinea);
            var documento = fila.Porcentaje(P.TopeDocumento);
            var hasta = fila.Fecha(P.VigenteHasta);
            if (rol is null || linea is null || documento is null || desde is null) continue;
            var clave = $"{rol.Code}|{desde.Value:yyyy-MM-dd}";
            if (!hoja.LlaveUnica(fila, clave, P.VigenteDesde) || fila.TieneErrores) continue;
            if (linea is < 0m or > 1m) fila.Error(P.TopeLinea, ImportErrors.CellFormat, "El tope va de 0 a 100.");
            if (documento is < 0m or > 1m) fila.Error(P.TopeDocumento, ImportErrors.CellFormat, "El tope va de 0 a 100.");
            if (hasta is { } h && h < desde) fila.Error(P.VigenteHasta, ImportErrors.CellFormat, "La vigencia termina antes de empezar.");
            if (fila.TieneErrores) continue;

            var existente = await db.DiscountCaps.FirstOrDefaultAsync(t => t.RoleId == rol.Id && t.ValidFrom == desde && !t.IsDeleted, ct);
            if (existente is not null)
            {
                var campos = new List<CampoCambiadoDto>();
                Diferencia(campos, P.TopeLinea, Puntos(existente.MaxLineRate), Puntos(linea.Value));
                Diferencia(campos, P.TopeDocumento, Puntos(existente.MaxDocumentRate), Puntos(documento.Value));
                Diferencia(campos, P.VigenteHasta, existente.ValidTo?.ToString("yyyy-MM-dd"), hasta?.ToString("yyyy-MM-dd"));
                if (campos.Count > 0)
                {
                    existente.MaxLineRate = linea.Value;
                    existente.MaxDocumentRate = documento.Value;
                    existente.ValidTo = hasta;
                    if (!string.IsNullOrWhiteSpace(ctx.Motivo)) existente.Reason = ctx.Motivo.Trim();
                    ctx.PedirMotivo();
                }
                ctx.Registrar(fila, clave, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
                continue;
            }

            var alta = await ReglasDeTopesDeDescuento.AltaAsync(db, cerrojo,
                new DatosDeTope(rol.Id, linea.Value, documento.Value, desde.Value, hasta, string.IsNullOrWhiteSpace(ctx.Motivo) ? "Importación de topes" : ctx.Motivo), ct);
            if (alta.IsFailure)
            {
                fila.Error(P.VigenteDesde, alta.Error.Code, alta.Error.Message);
                continue;
            }
            ctx.PedirMotivo();
            ctx.Registrar(fila, clave, AccionDeImportacion.Create,
                [new(P.TopeLinea, null, Puntos(linea.Value)), new(P.TopeDocumento, null, Puntos(documento.Value))]);
        }
    }

    private static string Puntos(decimal fraccion) => (fraccion * 100m).ToString("0.####", CultureInfo.InvariantCulture);

    private static void Diferencia(List<CampoCambiadoDto> campos, string columna, string? antes, string? despues)
    {
        if (!string.Equals(antes ?? string.Empty, despues ?? string.Empty, StringComparison.Ordinal)) campos.Add(new(columna, antes, despues));
    }
}
