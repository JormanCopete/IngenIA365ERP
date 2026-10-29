using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Reports;

// Feature 015, R2, US21 (fase 39, T0437; FR-152 parte R2; api.md §17): las vistas de R2 del centro de informes de Recaudo. «Canales» cuadra con los recibos vigentes del
// rango por canal (lo mismo que suman los cierres y el consolidado); «Convenios» explica cada archivo cargado: lo informado, lo aplicado, lo que fue a partidas y su
// conciliación con el abono (comisión, IVA, retenciones y lo sin explicar).
public sealed partial class ReceiptsReportQueryHandler
{
    private async Task<TablaExportable> CanalesAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var recibos = await db.IssuedReceipts.AsNoTracking().Where(r => r.Status != ReceiptState.Voided && r.ValueDate >= desde && r.ValueDate <= hasta)
            .Select(r => new { r.Channel, r.CashBoxId, r.BranchId, r.TotalApplied }).ToListAsync(ct);
        recibos = recibos.Where(r => a.IncluyeRecibo(r.CashBoxId, r.BranchId)).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Canal"), new("Recibos", TipoDeColumna.Entero), new("Valor aplicado", TipoDeColumna.Moneda), new("Participación", TipoDeColumna.Porcentaje),
            new("_canal", Clave: "_canal"),
        };
        var total = recibos.Sum(r => r.TotalApplied);
        var filas = Enum.GetValues<ReceiptChannel>().Select(c =>
        {
            var deCanal = recibos.Where(r => r.Channel == c).ToList();
            var valor = deCanal.Sum(r => r.TotalApplied);
            return new FilaExportable([NombreDelCanal(c), deCanal.Count, valor, total == 0m ? 0m : valor / total, c.ToString()]);
        }).Where(f => (int)f.Valores[1]! > 0).ToList();
        return new TablaExportable("Recaudo por canal", Periodo(desde, hasta), columnas, filas,
            new FilaExportable(["Total", recibos.Count, total, total == 0m ? 0m : 1m, null]),
            ["Los recibos vigentes del rango por su canal; los convenios y las pasarelas sólo entran por archivo (no hay botón de pago en línea)."]);
    }

    internal static string NombreDelCanal(ReceiptChannel c) => c switch
    {
        ReceiptChannel.CashDesk => "Caja",
        ReceiptChannel.Bank => "Banco (extracto)",
        ReceiptChannel.Paymaster => "Pagaduría",
        ReceiptChannel.Agreement => "Convenio",
        ReceiptChannel.Online => "Pasarela o PSE",
        ReceiptChannel.Contingency => "Contingencia",
        _ => c.ToString(),
    };

    private async Task<TablaExportable> ConveniosAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var convenios = (await db.CollectionAgreements.AsNoTracking().ToListAsync(ct))
            .Where(c => a.Alcance.IncluyeOficina(c.BranchId) && (a.Oficina is null || a.Oficina == c.BranchId)).ToDictionary(c => c.Id);
        var ids = convenios.Keys.ToList();
        var archivos = new List<(CollectionFileBase Archivo, bool Pasarela)>();
        archivos.AddRange((await db.AgreementFiles.AsNoTracking().Where(f => ids.Contains(f.CollectionAgreementId) && f.FileDate >= desde && f.FileDate <= hasta).ToListAsync(ct))
            .Select(f => ((CollectionFileBase)f, false)));
        archivos.AddRange((await db.GatewayFiles.AsNoTracking().Where(f => ids.Contains(f.CollectionAgreementId) && f.FileDate >= desde && f.FileDate <= hasta).ToListAsync(ct))
            .Select(f => ((CollectionFileBase)f, true)));
        var conciliaciones = await db.AgreementReconciliations.AsNoTracking().Where(r => ids.Contains(r.CollectionAgreementId)).ToListAsync(ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Convenio"), new("Archivo"), new("Fecha", TipoDeColumna.Fecha), new("Pagos", TipoDeColumna.Entero), new("Aplicados", TipoDeColumna.Entero),
            new("A partidas", TipoDeColumna.Entero), new("Informado", TipoDeColumna.Moneda), new("Abonado", TipoDeColumna.Moneda), new("Comisión", TipoDeColumna.Moneda),
            new("IVA", TipoDeColumna.Moneda), new("Retenciones", TipoDeColumna.Moneda), new("Sin explicar", TipoDeColumna.Moneda), new("Conciliación"),
            new("_archivo", Clave: "_archivo"),
        };
        var filas = archivos.OrderBy(x => x.Archivo.FileDate).ThenBy(x => x.Archivo.Number).Select(x =>
        {
            var f = x.Archivo;
            var c = conciliaciones.FirstOrDefault(r => x.Pasarela ? r.GatewayFileId == f.Id : r.AgreementFileId == f.Id);
            var estado = c is null
                ? "Sin conciliar"
                : c.Status switch
                {
                    AgreementReconciliationState.Explained => "Explicada",
                    AgreementReconciliationState.PendingApproval => "Por aprobar",
                    _ => "Aprobada",
                };
            return new FilaExportable([convenios[f.CollectionAgreementId].Code, f.Number, f.FileDate, f.Lines, f.Applied, f.Unidentified, f.TotalApplied + f.TotalUnidentified,
                c?.CreditedAmount, c?.CommissionAmount, c?.VatAmount, c?.WithholdingsAmount, c?.UnexplainedAmount, estado, f.PublicId],
                Resaltada: c is null || c.Status == AgreementReconciliationState.PendingApproval);
        }).ToList();
        decimal Suma(int i) => filas.Sum(r => r.Valores[i] is decimal d ? d : 0m);
        int Cuenta(int i) => filas.Sum(r => r.Valores[i] is int n ? n : 0);
        return new TablaExportable("Convenios de recaudo", Periodo(desde, hasta), columnas, filas,
            new FilaExportable(["Total", null, null, Cuenta(3), Cuenta(4), Cuenta(5), Suma(6), Suma(7), Suma(8), Suma(9), Suma(10), Suma(11), null, null]),
            ["Informado = lo aplicado más lo que fue a partidas; resaltados, los archivos sin conciliar o con la diferencia por aprobar."]);
    }
}
