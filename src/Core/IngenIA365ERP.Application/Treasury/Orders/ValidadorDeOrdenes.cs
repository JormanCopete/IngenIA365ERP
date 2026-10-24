using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.PersonBankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Lotes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Orders;

/// <summary>Lo que dejó la validación de una orden: si quedó lista, sus motivos y sus marcas. (nuevo)</summary>
public sealed record ValidacionDeOrden(bool Lista, IReadOnlyList<string> Motivos, PaymentBatchItemFlags Marcas, VerificacionDeDestino Destino);

/// <summary>
/// La validación de una orden de pago (feature 015, US10 y US26, T0142, T0167; FR-109, FR-121, FR-203, FR-210; mensajes.md §5.3), en el consumo, al volverla a
/// validar y en cada armado de lote. <b>Sólo escribe la orden</b> (estado, motivos, marcas, la cuenta registrada que casa y el banco destino), en la unidad de
/// trabajo de quien llama, sin guardar:
/// <list type="number">
/// <item>el beneficiario existe en el maestro;</item>
/// <item>una transferencia o un pago a tercero trae cuenta destino (<c>DestinationMissing</c>) de un banco con código de transferencia
/// (<c>BankWithoutTransferCode</c>) y la cuenta está <b>registrada activa</b> en las cuentas de la persona (<c>DestinationNotRegistered</c>); con su
/// <c>PayableFrom</c> futuro, <c>DestinationWaiting</c>;</item>
/// <item>listas: el beneficiario contra <see cref="IListasDeTesoreria"/> (contexto <c>Disbursement</c>); una coincidencia vinculante deja la orden pendiente con
/// <c>BindingListMatch</c> y alerta al oficial de inmediato (<c>Tesoreria.ListaVinculante</c>);</item>
/// <item>marcas: cuenta cambiada en los últimos <c>CuentasDestino.DiasDeMarca</c>, compartida con otro beneficiario, de un usuario interno, importada sin
/// certificación (<see cref="ICuentasBancariasDePersona.VerificarAsync"/>); la compartida y la de un usuario interno alertan
/// <c>Plataforma.CuentaDestinoCompartida</c>, la cambiada <c>Tesoreria.CuentaDestinoCambiada</c>.</item>
/// </list>
/// Un cheque no necesita cuenta destino. Sin motivos, la orden queda <see cref="PaymentOrderState.Ready"/>; con alguno, <see cref="PaymentOrderState.Pending"/>.
/// Una orden en lote, enviada o con resultado no se toca. (nuevo)
/// </summary>
public sealed class ValidadorDeOrdenes(
    IApplicationDbContext db,
    ICuentasBancariasDePersona cuentasDePersona,
    IListasDeTesoreria listas,
    IAlertas alertas,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Los estados que la validación puede mover (mensajes.md §5.3).</summary>
    public static bool Revalidable(PaymentOrderState estado) => estado is PaymentOrderState.Received or PaymentOrderState.Pending or PaymentOrderState.Ready;

    public async Task<ValidacionDeOrden> ValidarAsync(PaymentOrder orden, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orden);
        var motivos = new List<string>();
        var beneficiario = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == orden.BeneficiaryPersonId)
            .Select(p => new { p.PublicId, p.IsDeleted }).FirstOrDefaultAsync(ct);
        if (beneficiario is null || beneficiario.IsDeleted) motivos.Add(ErroresDeTesoreria.CodigoBeneficiarioInexistente);

        var destino = VerificacionDeDestino.Limpia;
        if (orden.Form != PaymentForm.Check && beneficiario is not null)
        {
            if (string.IsNullOrWhiteSpace(orden.DestinationBankCode) || string.IsNullOrWhiteSpace(orden.DestinationAccountNumber))
                motivos.Add(ErroresDeTesoreria.CodigoSinCuentaDestino);
            else
            {
                var codigo = orden.DestinationBankCode.Trim();
                var banco = await db.Banks.AsNoTracking().Where(b => b.TransferCode == codigo).Select(b => new { b.Id, b.TransferCode }).FirstOrDefaultAsync(ct);
                orden.DestinationBankId = banco?.Id;
                if (banco is null || string.IsNullOrWhiteSpace(banco.TransferCode)) motivos.Add(ErroresDeTesoreria.CodigoBancoSinCodigo);
                else
                {
                    var v = await cuentasDePersona.VerificarAsync(beneficiario.PublicId, codigo, orden.DestinationAccountNumber, ct);
                    if (!v.Registered) motivos.Add(ErroresDeTesoreria.CodigoDestinoNoRegistrado);
                    else if (v.Waiting) motivos.Add(ErroresDeTesoreria.CodigoDestinoEnEspera);
                    orden.PersonBankAccountId = v.AccountPublicId is { } c
                        ? await db.PersonBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
                        : null;
                    destino = new VerificacionDeDestino(v.ChangedRecently, v.SharedWithOtherBeneficiary, v.SharedWithInternalUser, v.ImportedWithoutCertificate);
                }
            }
        }

        // T1 (fase 34; FR-211): una orden manual sin sus dos aprobaciones no queda lista.
        if (orden.EsManualSinAprobar) motivos.Add(ErroresDeTesoreria.CodigoOrdenManualSinAprobar);

        var antes = orden.Status;
        var resultado = await listas.EvaluarAsync([orden.BeneficiaryPersonId], ct);
        if (resultado.HayVinculante) motivos.Add(ErroresDeTesoreria.CodigoListaVinculante);

        var marcas = ArmadoDeLote.Marcas(destino);
        orden.Flags = marcas;
        orden.AccountChangedRecently = destino.CambiadaReciente;
        orden.PendingReasonsJson = JsonSerializer.Serialize(motivos, Json);
        orden.Status = motivos.Count == 0 ? PaymentOrderState.Ready : PaymentOrderState.Pending;
        orden.ValidatedAt = reloj.UtcNow;

        await AlertarAsync(orden, resultado.HayVinculante && antes != PaymentOrderState.Pending, destino, ct);
        return new ValidacionDeOrden(motivos.Count == 0, motivos, marcas, destino);
    }

    /// <summary>Los motivos de pendiente guardados en la orden.</summary>
    public static IReadOnlyList<string> Motivos(PaymentOrder orden) =>
        string.IsNullOrWhiteSpace(orden.PendingReasonsJson) ? [] : JsonSerializer.Deserialize<List<string>>(orden.PendingReasonsJson, Json) ?? [];

    private async Task AlertarAsync(PaymentOrder orden, bool listaVinculante, VerificacionDeDestino destino, CancellationToken ct)
    {
        if (listaVinculante)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaListaVinculante, "Beneficiario en lista vinculante",
                string.Create(CultureInfo.InvariantCulture,
                    $"El beneficiario de la orden {orden.Number} ({orden.BeneficiaryName}) coincidió en una lista vinculante: la orden quedó pendiente y no se paga."),
                "PaymentOrder", orden.PublicId, DedupKey: $"Tesoreria.ListaVinculante:{orden.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
        if (destino.Compartida || destino.DeUsuarioInterno)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.PlataformaCuentaDestinoCompartida, "Cuenta destino compartida",
                string.Create(CultureInfo.InvariantCulture,
                    $"La cuenta destino de la orden {orden.Number} está registrada también para {(destino.DeUsuarioInterno ? "un usuario interno" : "otro beneficiario")}."),
                "PaymentOrder", orden.PublicId, DedupKey: $"Plataforma.CuentaDestinoCompartida:{orden.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
        if (destino.CambiadaReciente)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaCuentaDestinoCambiada, "Cuenta destino cambiada",
                string.Create(CultureInfo.InvariantCulture,
                    $"La cuenta destino de la orden {orden.Number} cambió hace pocos días: el lote exige su revisión expresa."),
                "PaymentOrder", orden.PublicId, DedupKey: $"Tesoreria.CuentaDestinoCambiada:{orden.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
    }
}
