using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Treasury.Lotes;

/// <summary>Lo que la verificación de la cuenta destino dijo de una orden (<c>ICuentasBancariasDePersona.VerificarAsync</c>). (nuevo)</summary>
public sealed record VerificacionDeDestino(bool CambiadaReciente, bool Compartida, bool DeUsuarioInterno, bool ImportadaSinCertificacion)
{
    public static VerificacionDeDestino Limpia { get; } = new(false, false, false, false);
}

/// <summary>Una orden candidata a entrar a un lote, como la ve el motor. (nuevo)</summary>
public sealed record OrdenCandidata(
    int OrdenId,
    PaymentOrderState Estado,
    PaymentForm Forma,
    decimal Neto,
    bool PagadaPorArchivoDeCartera,
    bool EnOtroLoteVigente,
    bool AnulacionPedida,
    IReadOnlyList<string> MotivosDePendiente,
    VerificacionDeDestino Destino);

/// <summary>Una orden incluida en el lote con sus marcas. (nuevo)</summary>
public sealed record OrdenIncluida(int OrdenId, decimal Valor, PaymentBatchItemFlags Marcas)
{
    public bool ExigeRevision => Marcas.HasFlag(PaymentBatchItemFlags.RecentAccountChange);
}

/// <summary>Una orden que no entra, con su motivo. (nuevo)</summary>
public sealed record OrdenPendiente(int OrdenId, string Motivo);

/// <summary>El lote armado: lo que entra, lo que queda pendiente y los totales al peso (FR-110, FR-116). (nuevo)</summary>
public sealed record LoteArmado(IReadOnlyList<OrdenIncluida> Incluidas, IReadOnlyList<OrdenPendiente> Pendientes, decimal Total, int Cantidad);

/// <summary>
/// El armado de un lote de pago (feature 015, US10, T0134; FR-110, FR-116, FR-210; SC-011, SC-013), puro: recibe las órdenes pedidas con su estado y la
/// verificación de su cuenta destino, y la forma del lote. Una orden entra sólo si está <see cref="PaymentOrderState.Ready"/>, no la paga un archivo de Cartera
/// (FR-108), no está en otro lote vigente (FR-116), no pidió su anulación el origen, su forma cabe en la del lote (archivo y portal: transferencia y pago a
/// tercero; cheques: cheque) y su neto es positivo. Las que entran llevan sus marcas antifraude —cuenta cambiada hace poco, compartida, de un usuario
/// interno, importada sin certificación— y las cambiadas exigen revisión expresa antes de aprobar (US26-1). Los totales suman el neto exacto (no hay pagos
/// parciales de una orden de giro). (nuevo)
/// </summary>
public static class ArmadoDeLote
{
    public const string MotivoPagadaPorArchivo = "PaidByLendingFile";
    public const string MotivoEnOtroLote = "AlreadyInBatch";
    public const string MotivoNoLista = "NotReady";
    public const string MotivoFormaDistinta = "FormMismatch";
    public const string MotivoSinValor = "InvalidAmount";
    public const string MotivoAnulacionPedida = "VoidRequested";

    public static LoteArmado Armar(IReadOnlyList<OrdenCandidata> ordenes, PaymentBatchForm forma)
    {
        ArgumentNullException.ThrowIfNull(ordenes);
        var incluidas = new List<OrdenIncluida>();
        var pendientes = new List<OrdenPendiente>();
        foreach (var o in ordenes.DistinctBy(o => o.OrdenId))
        {
            var motivo = Motivo(o, forma);
            if (motivo is not null)
            {
                pendientes.Add(new OrdenPendiente(o.OrdenId, motivo));
                continue;
            }
            incluidas.Add(new OrdenIncluida(o.OrdenId, o.Neto, Marcas(o.Destino)));
        }
        return new LoteArmado(incluidas, pendientes, Math.Round(incluidas.Sum(i => i.Valor), 2, MidpointRounding.AwayFromZero), incluidas.Count);
    }

    /// <summary>¿Cabe una forma de orden en la forma del lote?</summary>
    public static bool FormaCabe(PaymentForm orden, PaymentBatchForm lote) => lote switch
    {
        PaymentBatchForm.Checks => orden == PaymentForm.Check,
        _ => orden is PaymentForm.Transfer or PaymentForm.ThirdPartyPayment,
    };

    /// <summary>Las marcas de una cuenta destino.</summary>
    public static PaymentBatchItemFlags Marcas(VerificacionDeDestino d)
    {
        ArgumentNullException.ThrowIfNull(d);
        var m = PaymentBatchItemFlags.None;
        if (d.CambiadaReciente) m |= PaymentBatchItemFlags.RecentAccountChange;
        if (d.Compartida) m |= PaymentBatchItemFlags.SharedDestination;
        if (d.DeUsuarioInterno) m |= PaymentBatchItemFlags.InternalUserAccount;
        if (d.ImportadaSinCertificacion) m |= PaymentBatchItemFlags.ImportedAccountWithoutCertificate;
        return m;
    }

    private static string? Motivo(OrdenCandidata o, PaymentBatchForm forma)
    {
        if (o.PagadaPorArchivoDeCartera) return MotivoPagadaPorArchivo;
        if (o.EnOtroLoteVigente) return MotivoEnOtroLote;
        if (o.AnulacionPedida) return MotivoAnulacionPedida;
        if (o.Estado != PaymentOrderState.Ready) return o.MotivosDePendiente.FirstOrDefault() ?? MotivoNoLista;
        if (!FormaCabe(o.Forma, forma)) return MotivoFormaDistinta;
        if (o.Neto <= 0m) return MotivoSinValor;
        return null;
    }
}
