using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Imputacion;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

/// <summary>
/// Lo común de la operación de caja (feature 015, T0114, T0115; R-07, R-11; FR-038 a FR-041, FR-188, FR-189): leer y escribir lo que guarda en JSON (ítems,
/// medios, imputación), la operación visible por el alcance de su sesión, el nombre de una persona, y la imputación con <see cref="ImputacionDeMedios"/> sobre
/// los medios sellados en la sesión. (nuevo)
/// </summary>
public static class OperacionesDeCaja
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ItemDeCajaDto> Items(CashOperation o) => JsonSerializer.Deserialize<List<ItemDeCajaDto>>(o.ItemsJson, Opciones) ?? [];

    public static IReadOnlyList<MedioDeCajaDto> Medios(CashOperation o) => JsonSerializer.Deserialize<List<MedioDeCajaDto>>(o.MeansJson, Opciones) ?? [];

    public static IReadOnlyList<PorcionDeCajaDto> Imputacion(CashOperation o) =>
        JsonSerializer.Deserialize<List<PorcionDeCajaDto>>(o.ImputationJson, Opciones) ?? [];

    public static string Json<T>(IReadOnlyList<T> valores) => JsonSerializer.Serialize(valores, Opciones);

    /// <summary>La operación y su sesión, si la caja de la sesión está al alcance.</summary>
    public static async Task<(CashOperation Operacion, CashSession Sesion)?> VisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, Guid publicId,
        bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.CashOperations : db.CashOperations.AsNoTracking();
        var operacion = await consulta.FirstOrDefaultAsync(o => o.PublicId == publicId, ct);
        if (operacion is null) return null;
        var sesionPublicId = await db.ReceiptsCashSessions.AsNoTracking().Where(s => s.Id == operacion.CashSessionId).Select(s => s.PublicId).FirstAsync(ct);
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, sesionPublicId, seguir, ct);
        return sesion is null ? null : (operacion, sesion);
    }

    public static string Nombre(Person p) =>
        !string.IsNullOrWhiteSpace(p.BusinessName)
            ? p.BusinessName!
            : string.Join(' ', new[] { p.FirstName, p.OtherNames, p.LastName, p.SecondLastName }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>
    /// La imputación de los medios a los ítems: la propuesta por el orden de <c>Recibo.OrdenDeImputacion</c>, o la fijada por el cajero (sólo se valida).
    /// Los medios se identifican por su <see cref="MedioDeCajaDto.MeansId"/> y su clase sellada en la sesión.
    /// </summary>
    public static Result<ResultadoDeImputacion> Imputar(IReadOnlyList<ItemDeCajaDto> items, IReadOnlyList<MedioDeCajaDto> medios,
        ValoresSelladosDeLaSesion sellados, OrdenDeImputacion orden, IReadOnlyList<PorcionDeCajaDto>? fijada)
    {
        var aImputar = new List<MedioAImputar>();
        foreach (var m in medios)
        {
            var sellado = sellados.Means.FirstOrDefault(x => x.PublicId == m.PaymentMeansPublicId);
            if (sellado is null) return Result.Failure<ResultadoDeImputacion>(ErroresDeRecaudo.MedioNoOfrecido());
            aImputar.Add(new MedioAImputar(m.MeansId!.Value.ToString("N"), sellado.Class, m.Amount));
        }
        var pedido = new PedidoDeImputacion(
            items.Select(i => new AplicacionAImputar(i.ItemId.ToString("N"), i.Amount)).ToList(),
            aImputar,
            orden,
            fijada?.Select(p => new PorcionImputada(p.ItemId.ToString("N"), p.MeansId.ToString("N"), p.Amount)).ToList());
        var resultado = ImputacionDeMedios.Imputar(pedido);
        return resultado.Error switch
        {
            null => Result.Success(resultado),
            ImputacionDeMedios.CodigoMediosNoCuadran => Result.Failure<ResultadoDeImputacion>(ErroresDeRecaudo.MediosNoCuadran()),
            ImputacionDeMedios.CodigoVueltasSoloEnEfectivo => Result.Failure<ResultadoDeImputacion>(ErroresDeRecaudo.VueltasSoloEnEfectivo()),
            _ => Result.Failure<ResultadoDeImputacion>(ErroresDeRecaudo.ImputacionInvalida()),
        };
    }

    public static IReadOnlyList<PorcionDeCajaDto> Porciones(ResultadoDeImputacion r) =>
        r.Porciones.Select(p => new PorcionDeCajaDto(Guid.ParseExact(p.Aplicacion, "N"), Guid.ParseExact(p.Medio, "N"), p.Valor)).ToList();

    /// <summary>El efectivo de los medios de la operación.</summary>
    public static decimal Efectivo(IReadOnlyList<MedioDeCajaDto> medios, ValoresSelladosDeLaSesion sellados) =>
        medios.Where(m => sellados.Means.FirstOrDefault(x => x.PublicId == m.PaymentMeansPublicId)?.Class == PaymentMeansClass.Cash).Sum(m => m.Amount);

    public static async Task<CashOperationDto> DtoAsync(IApplicationDbContext db, CashOperation o, CancellationToken ct)
    {
        var personas = new[] { o.PayerPersonId, o.BeneficiaryPersonId ?? 0, o.DeliveredByPersonId ?? 0 }.Where(x => x != 0).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var sesion = await db.ReceiptsCashSessions.AsNoTracking().Where(s => s.Id == o.CashSessionId).Select(s => s.PublicId).FirstAsync(ct);
        var recibo = o.ReceiptId is { } r
            ? await db.IssuedReceipts.AsNoTracking().Where(x => x.Id == r).Select(x => new { x.PublicId, x.Number }).FirstOrDefaultAsync(ct)
            : null;
        var items = Items(o);
        var medios = Medios(o);
        var totalItems = items.Sum(i => i.Amount);
        var totalMedios = medios.Sum(m => m.Amount);
        return new CashOperationDto(o.PublicId, o.Status, sesion, publicos[o.PayerPersonId],
            o.BeneficiaryPersonId is { } b ? publicos[b] : null, o.DeliveredByPersonId is { } d ? publicos[d] : null, o.ValueDate, items, medios, Imputacion(o),
            o.ImputationFixed, totalItems, totalMedios, Math.Max(0m, totalMedios - totalItems), o.SourceOfFundsDeclarationId is not null, recibo?.PublicId,
            recibo?.Number);
    }
}
