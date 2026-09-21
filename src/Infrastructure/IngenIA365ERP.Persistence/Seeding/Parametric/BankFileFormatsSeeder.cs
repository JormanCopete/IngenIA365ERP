using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Formatos de archivo bancario que trae el programa (feature 010, N4; D-15, D-42):
/// <c>CSV-GENERICO</c>, delimitado, genérico (sin banco) y activo —sirve para probar la
/// dispersión y como plantilla para copiar—, y <c>AVVILLAS-1</c>, **sin campos e inactivo**,
/// sólo si la cooperativa tiene un banco «AV Villas» en <c>COR_Banks</c>: la estructura real del
/// archivo de AV Villas Empresas la aporta el dueño y se carga por la pantalla de formatos.
/// Idempotente por <c>Code</c>: una fila que la cooperativa ya editó no se pisa.
/// </summary>
public sealed class BankFileFormatsSeeder : IDataSeeder
{
    public int Order => 76;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public const string CsvGenerico = "CSV-GENERICO";
    public const string AvVillasPendiente = "AVVILLAS-1";

    public static BankFileFormatDefinition DefinicionCsvGenerico() => BankFileFormatDefinition.Parse("""
        { "code": "CSV-GENERICO", "name": "CSV genérico de pagos (cualquier banco)", "scope": "PayrollDisbursement",
          "validFrom": "2026-01-01", "kind": "Delimited", "delimiter": ";", "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF",
          "uppercase": true, "stripAccents": true, "amountFormat": "Integer",
          "fileName": "PAGOS_{PaymentDate:yyyyMMdd}_{Sequence:000}.csv", "contentType": "text/csv",
          "notes": "Formato de referencia: una línea por pago con los campos que todo banco pide. Cópielo y ajústelo al layout que publique el banco.",
          "records": {
            "header": { "enabled": true, "fields": [
              { "order": 1, "name": "Marca", "source": "Constant", "value": "H" },
              { "order": 2, "name": "NIT", "source": "CompanyNit" },
              { "order": 3, "name": "Empresa", "source": "CompanyName" },
              { "order": 4, "name": "Fecha de pago", "source": "PaymentDate", "format": "yyyy-MM-dd" },
              { "order": 5, "name": "Referencia", "source": "BatchReference" },
              { "order": 6, "name": "Registros", "source": "LineCount" },
              { "order": 7, "name": "Total", "source": "TotalAmount" } ] },
            "detail": { "enabled": true, "fields": [
              { "order": 1, "name": "Marca", "source": "Constant", "value": "D" },
              { "order": 2, "name": "Línea", "source": "LineNumber" },
              { "order": 3, "name": "Tipo de documento", "source": "PayeeDocumentType" },
              { "order": 4, "name": "Documento", "source": "PayeeDocument", "required": true },
              { "order": 5, "name": "Nombre", "source": "PayeeFullName", "length": 60 },
              { "order": 6, "name": "Banco destino (ACH)", "source": "PayeeBankCode", "required": true },
              { "order": 7, "name": "Tipo de cuenta", "source": "PayeeAccountType", "map": { "1": "AHORROS", "2": "CORRIENTE" } },
              { "order": 8, "name": "Cuenta", "source": "PayeeAccountNumber", "required": true },
              { "order": 9, "name": "Valor", "source": "Amount" },
              { "order": 10, "name": "Concepto", "source": "Concept", "length": 40 },
              { "order": 11, "name": "Correo", "source": "PayeeEmail" } ] },
            "trailer": { "enabled": false, "fields": [] } } }
        """)!;

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, ct);

    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var existentes = await db.BankFileFormats.IgnoreQueryFilters().Select(f => f.Code).ToListAsync(ct);
        var insertados = 0;
        var ahora = DateTime.UtcNow;

        if (!existentes.Contains(CsvGenerico, StringComparer.OrdinalIgnoreCase))
        {
            var formato = new BankFileFormat { IsSeeded = true, CreatedAt = ahora, CreatedBy = SeedContext.ParametricCreatedBy };
            DefinicionCsvGenerico().AplicarA(formato, bankId: null);
            foreach (var f in formato.Fields) { f.CreatedAt = ahora; f.CreatedBy = SeedContext.ParametricCreatedBy; }
            db.BankFileFormats.Add(formato);
            insertados++;
        }

        if (!existentes.Contains(AvVillasPendiente, StringComparer.OrdinalIgnoreCase))
        {
            var avVillas = await db.Banks.AsNoTracking().Where(b => !b.IsDeleted && b.Name.ToUpper().Contains("VILLAS")).OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
            if (avVillas is not null)
            {
                db.BankFileFormats.Add(new BankFileFormat
                {
                    BankId = avVillas.Id, Code = AvVillasPendiente, Name = "Banco AV Villas — pagos a terceros (Empresas)",
                    ValidFrom = new DateOnly(2026, 12, 1), IsActive = false, IsSeeded = true, HasHeader = true, HasTrailer = true,
                    FileNamePattern = "PAGOS{PaymentDate:yyyyMMdd}.txt",
                    Notes = "Estructura pendiente del dueño (D-15): cargue los campos del archivo plano de AV Villas Empresas y active el formato, o cree uno nuevo con código AVVILLAS-PAGOS.",
                    CreatedAt = ahora, CreatedBy = SeedContext.ParametricCreatedBy,
                });
                insertados++;
            }
        }

        if (insertados > 0) await db.SaveChangesAsync(ct);
        return insertados;
    }
}
