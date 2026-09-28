using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Settings;

/// <summary>Quién arregla lo que falta: la pantalla y el permiso (api.md §24.3). (nuevo)</summary>
public sealed record QuienLoArreglaDto(string? Page, string? Permission);

/// <summary>Un faltante de la preparación; los códigos son los de <c>data.missing[]</c> de <c>ElectronicInvoicing.NotReady</c>. (nuevo)</summary>
public sealed record FaltanteDeEmisionDto(string Code, string Message, QuienLoArreglaDto WhoFixes);

/// <summary>El canal de la configuración vigente. (nuevo)</summary>
public sealed record CanalDeLaPreparacionDto(string ChannelCode, EmissionMode Mode, Domain.Enums.Dian.DianEnvironment Environment);

/// <summary>Una resolución con su estado calculado a la fecha. (nuevo)</summary>
public sealed record ResolucionDeLaPreparacionDto(ResolutionKind Kind, string Prefix, string Status, decimal ConsumedFraction, int DaysToExpire);

/// <summary>La contingencia abierta del canal. (nuevo)</summary>
public sealed record ContingenciaAbiertaDto(Guid ContingencyPublicId, ContingencyType Type);

/// <summary>La respuesta de <c>GET /api/electronic-invoicing/readiness</c> (api.md §24.3). (nuevo)</summary>
public sealed record DianReadinessDto(
    DateOnly AsOf,
    bool Obligated,
    VeredictoFiscal Verdict,
    CanalDeLaPreparacionDto? Channel,
    IReadOnlyList<ResolucionDeLaPreparacionDto> Resolutions,
    ContingenciaAbiertaDto? OpenContingency,
    IReadOnlyList<FaltanteDeEmisionDto> Missing);

/// <summary>
/// La preparación para emitir (feature 012, I4, T707; api.md §24.3; contracts/dian.md §10.4), al estilo de
/// <c>GET /api/payroll/legal-parameters/missing</c>: la <b>misma</b> decisión de <see cref="GuardiaDeEmisionFiscal"/> que usan la
/// confirmación y la apertura de caja, por tipo de documento y por caja. Sin tipo, evalúa los tipos fiscales activos de la caja (sus roles
/// de venta y de nota) o, sin caja, todos los de la cooperativa; el veredicto es el más restrictivo y <c>missing[]</c> reúne los motivos sin
/// repetir. Con caja suma además, como faltante que avisa, el tipo del rol de contingencia que respalda a cada rol de venta
/// (<c>DocumentTypeMissing</c>): la guardia sólo bloquea por él con una contingencia 03 abierta. (nuevo)
/// </summary>
public sealed record GetDianReadinessQuery(DateOnly? AsOf = null, Guid? DocumentType = null, Guid? CashRegister = null)
    : IRequest<Result<DianReadinessDto>>;

public sealed class GetDianReadinessQueryHandler(
    IApplicationDbContext db,
    GuardiaDeEmisionFiscal guardia,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
    : IRequestHandler<GetDianReadinessQuery, Result<DianReadinessDto>>
{
    private static readonly CashRegisterDocumentRole[] RolesDeContingencia =
        [CashRegisterDocumentRole.PosSaleContingency, CashRegisterDocumentRole.InvoiceContingency];

    public async Task<Result<DianReadinessDto>> Handle(GetDianReadinessQuery request, CancellationToken ct)
    {
        var fecha = request.AsOf ?? reloj.HoyLocal;
        var leido = await parametros.LeerComoAsync<bool>(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.ObligadaAFacturar, fecha, ct: ct);
        var obligada = leido.IsFailure || leido.Value;

        CashRegister? caja = null;
        List<CashRegisterDocumentType> roles = [];
        if (request.CashRegister is Guid cajaId)
        {
            caja = await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == cajaId, ct);
            if (caja is null) return Result.Failure<DianReadinessDto>(ErroresDePuntoDeVenta.CashRegisterNotFound());
            roles = await db.CashRegisterDocumentTypes.AsNoTracking().Include(r => r.DocumentType)
                .Where(r => r.CashRegisterId == caja.Id).ToListAsync(ct);
        }

        List<InventoryDocumentType> tipos;
        if (request.DocumentType is Guid tipoId)
        {
            var tipo = await db.InventoryDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == tipoId, ct);
            if (tipo is null) return Result.Failure<DianReadinessDto>(InventoryErrors.DocumentTypeNotFound());
            tipos = [tipo];
        }
        else if (caja is not null)
        {
            tipos = roles.Where(r => !RolesDeContingencia.Contains(r.Role)).Select(r => r.DocumentType).OfType<InventoryDocumentType>()
                .DistinctBy(t => t.Id).ToList();
        }
        else
        {
            tipos = (await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.IsActive && !t.IsContingency).ToListAsync(ct))
                .Where(t => GuardiaDeEmisionFiscal.TipoElectronicoDe(t.Class) is not null || GuardiaDeEmisionFiscal.ClasesNoElectronicas.Contains(t.Class))
                .ToList();
        }
        if (tipos.Count == 0)
            tipos = [new InventoryDocumentType { Code = "FV", Name = "Factura electrónica", Class = DocumentClass.SalesInvoice }];

        var evaluaciones = new List<EvaluacionFiscal>();
        foreach (var tipo in tipos) evaluaciones.Add(await guardia.EvaluarAsync(fecha, tipo, caja, ct));

        var veredicto = evaluaciones.Any(e => e.Veredicto == VeredictoFiscal.Blocked) ? VeredictoFiscal.Blocked
            : evaluaciones.Any(e => e.Veredicto == VeredictoFiscal.Electronic) ? VeredictoFiscal.Electronic
            : VeredictoFiscal.NonElectronic;
        var faltantes = evaluaciones.SelectMany(e => e.Motivos)
            .Select(m => new FaltanteDeEmisionDto(m.Code, m.Message, new QuienLoArreglaDto(m.Page, m.Permission)))
            .ToList();
        if (caja is not null) faltantes.AddRange(ContingenciaDeLaCaja(roles, obligada));

        var configuracion = await db.ElectronicEmissionSettings.AsNoTracking()
            .Where(s => s.ValidFrom <= fecha && (s.ValidTo == null || s.ValidTo >= fecha))
            .OrderByDescending(s => s.ValidFrom).FirstOrDefaultAsync(ct);
        var resoluciones = await db.DianNumberingResolutions.AsNoTracking()
            .Where(r => r.IsActive && (configuracion == null || r.Environment == configuracion.Environment))
            .OrderBy(r => r.Kind).ThenBy(r => r.Prefix).ThenBy(r => r.ValidFrom)
            .ToListAsync(ct);
        var contingencia = configuracion is null ? null : await db.DianContingencyEvents.AsNoTracking()
            .Where(e => e.Status == ContingencyEventStatus.Open && e.ChannelCode == configuracion.ChannelCode)
            .OrderBy(e => e.Type).FirstOrDefaultAsync(ct);

        return Result.Success(new DianReadinessDto(
            fecha,
            obligada,
            veredicto,
            configuracion is null ? null : new CanalDeLaPreparacionDto(configuracion.ChannelCode, configuracion.Mode, configuracion.Environment),
            resoluciones.Select(r => new ResolucionDeLaPreparacionDto(r.Kind, r.Prefix, ReglasDeResolucion.Estado(r, fecha).ToString(),
                ReglasDeResolucion.FraccionConsumida(r), ReglasDeResolucion.DiasParaVencer(r, fecha))).ToList(),
            contingencia is null ? null : new ContingenciaAbiertaDto(contingencia.PublicId, contingencia.Type),
            faltantes.DistinctBy(f => (f.Code, f.Message)).ToList()));
    }

    /// <summary>Cada rol de venta de la caja necesita el tipo del rol de contingencia que lo respalda (api.md §20.1, §24.3).</summary>
    private static IEnumerable<FaltanteDeEmisionDto> ContingenciaDeLaCaja(IReadOnlyList<CashRegisterDocumentType> roles, bool obligada)
    {
        if (!obligada) yield break;
        foreach (var (venta, contingencia, nombre) in new[]
                 {
                     (CashRegisterDocumentRole.PosSale, CashRegisterDocumentRole.PosSaleContingency, "venta POS"),
                     (CashRegisterDocumentRole.InvoiceOnRequest, CashRegisterDocumentRole.InvoiceContingency, "factura a solicitud"),
                 })
        {
            var deVenta = roles.FirstOrDefault(r => r.Role == venta)?.DocumentType;
            if (deVenta is null || GuardiaDeEmisionFiscal.TipoElectronicoDe(deVenta.Class) is null) continue;
            if (roles.Any(r => r.Role == contingencia)) continue;
            yield return new FaltanteDeEmisionDto(GuardiaDeEmisionFiscal.DocumentTypeMissingCode,
                $"La caja no tiene el tipo de documento de contingencia que respalda a la {nombre}.",
                new QuienLoArreglaDto(GuardiaDeEmisionFiscal.PaginaDePuntosDeVenta, GuardiaDeEmisionFiscal.PermisoDePuntosDeVenta));
        }
    }
}
