using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Los tipos de hecho de Cartera (<c>LND_FactTypes</c>; feature 013, T0223; data-model §1.11; DC-11, T43, T56, C06, C110, C117;
/// Order 90, CH36): una fila por cada tipo <c>CAR → Accounting</c> del catálogo de mensajes de Cartera
/// (<see cref="CatalogoDeMensajesDeCarteraV1.HechosAContabilidad"/>, <b>36</b>, con los tres contingentes de A3 y, desde K6b, la corrección
/// monetaria UVR); los 12 tipos
/// restantes del catálogo no son hechos contables y no tienen fila. El <c>ShortCode</c> se lee de
/// <see cref="TipoDeMensaje.CodigoCorto"/> (C117): un tipo sin código corto hace fallar el arranque. El resto —nombre, comprobante
/// (contabilidad.md §2.1, §5), modo y disparador por defecto (D8, T56) y entrega— es la tabla <see cref="Datos"/>.
///
/// <para>
/// Idempotente por <c>Code</c>; pone al día nombre, código corto y los defectos de lo sembrado; <b>nunca borra</b> (un tipo retirado
/// del catálogo queda). El modo por defecto de los acompañantes y de los reversos que heredan el de su original (C01, C08) es el de
/// su principal; el de los informativos se guarda <c>EnLinea</c> pero rige <c>Always</c> sin comprobante (lo decide
/// <c>ModoDePasoDeCartera</c> por <c>IsInformational</c>). La tabla llega con <c>CarteraNucleo</c>: hasta que esa migración esté
/// aplicada en la base, la semilla no hace nada. (nuevo)
/// </para>
/// </summary>
public sealed class LendingFactTypesSeeder : IDataSeeder
{
    public const string MigracionQueCreaLasTablas = "CarteraNucleo";

    public const string EnLinea = "EnLinea";
    public const string PorLotes = "PorLotes";
    public const string NoPasa = "NoPasa";
    public const string FinDeCausacion = "FinDeCausacion";
    public const string CierreDeCartera = "CierreDeCartera";

    public int Order => 90;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    /// <summary>Lo que el catálogo de mensajes no dice de un tipo de hecho.</summary>
    public sealed record DatosDelHecho(string Nombre, string? Comprobante, string Modo, string? Disparador, EntregaDeCartera Entrega);

    /// <summary>Por <c>Type</c>, en el orden de contracts/mensajes.md §2.1.</summary>
    public static IReadOnlyDictionary<string, DatosDelHecho> Datos { get; } = new Dictionary<string, DatosDelHecho>(StringComparer.Ordinal)
    {
        [CreditoDesembolsadoV1.Type] = new("Crédito desembolsado", "DS", EnLinea, null, EntregaDeCartera.K1),
        [DesembolsoReversadoV1.Type] = new("Desembolso reversado", "DS", EnLinea, null, EntregaDeCartera.K1),
        [CreditoAjustadoDesdeComercialV1.Type] = new("Crédito ajustado desde Comercial", "DS", EnLinea, null, EntregaDeCartera.K1),
        [InteresCausadoV1.Type] = new("Interés causado", "CA", PorLotes, FinDeCausacion, EntregaDeCartera.K1),
        [MoraCausadaV1.Type] = new("Mora causada", "CA", PorLotes, FinDeCausacion, EntregaDeCartera.K1),
        [SeguroCausadoV1.Type] = new("Seguro causado", "CA", PorLotes, FinDeCausacion, EntregaDeCartera.K1),
        [AjusteDeCausacionPorFechaValorV1.Type] = new("Ajuste de causación por fecha valor", "CA", PorLotes, FinDeCausacion, EntregaDeCartera.K1),
        [CausacionSuspendidaV1.Type] = new("Causación suspendida", null, EnLinea, null, EntregaDeCartera.K1),
        [CausacionReanudadaV1.Type] = new("Causación reanudada", null, EnLinea, null, EntregaDeCartera.K1),
        [PagoAplicadoV1.Type] = new("Pago aplicado", "RC", EnLinea, null, EntregaDeCartera.K1),
        [PagoReversadoV1.Type] = new("Pago reversado", "RC", EnLinea, null, EntregaDeCartera.K1),
        [AnticipoRecibidoV1.Type] = new("Anticipo recibido", "RC", EnLinea, null, EntregaDeCartera.K1),
        [AnticipoAplicadoV1.Type] = new("Anticipo aplicado", "RC", EnLinea, null, EntregaDeCartera.K1),
        [CobroDeInteresEnSuspensoV1.Type] = new("Cobro de interés en suspenso", "RC", EnLinea, null, EntregaDeCartera.K1),
        [SaldoAFavorReconocidoV1.Type] = new("Saldo a favor reconocido", "RC", EnLinea, null, EntregaDeCartera.K1),
        [SaldoAFavorResueltoV1.Type] = new("Saldo a favor resuelto", "RC", EnLinea, null, EntregaDeCartera.K1),
        [RecuperacionDeCastigadaV1.Type] = new("Recuperación de cartera castigada", "RC", EnLinea, null, EntregaDeCartera.K1),
        // En línea si la emite una operación; por lotes al fin de la causación si la emite el proceso (T56).
        [DimensionContableCambiadaV1.Type] = new("Dimensión contable cambiada", "CN", EnLinea, FinDeCausacion, EntregaDeCartera.K1),
        [CobroProvisionalIncorporadoV1.Type] = new("Cobro provisional incorporado", "RC", EnLinea, null, EntregaDeCartera.K1),
        [DeterioroCalculadoV1.Type] = new("Deterioro calculado", "PV", PorLotes, CierreDeCartera, EntregaDeCartera.K1),
        [CarteraReclasificadaPorCalificacionV1.Type] = new("Cartera reclasificada por calificación", "PV", PorLotes, CierreDeCartera, EntregaDeCartera.K1),
        [ReversoDeCierreV1.Type] = new("Reverso de cierre de cartera", "PV", PorLotes, CierreDeCartera, EntregaDeCartera.K1),
        [GastoDeCobranzaCausadoV1.Type] = new("Gasto de cobranza causado", "CA", EnLinea, null, EntregaDeCartera.K4),
        [CreditoReestructuradoV1.Type] = new("Crédito reestructurado", "CN", EnLinea, null, EntregaDeCartera.K3),
        [CreditoModificadoV1.Type] = new("Crédito modificado", "CN", EnLinea, null, EntregaDeCartera.K3),
        [CreditoNovadoV1.Type] = new("Crédito novado", "CN", EnLinea, null, EntregaDeCartera.K3),
        [CondonacionAprobadaV1.Type] = new("Condonación aprobada", "CN", EnLinea, null, EntregaDeCartera.K3),
        [CreditoCastigadoV1.Type] = new("Crédito castigado", "PV", EnLinea, null, EntregaDeCartera.K3),
        [CarteraActivadaV1.Type] = new("Cartera activada", null, EnLinea, null, EntregaDeCartera.K1),
        [CargaDeCarteraAplicadaV1.Type] = new("Carga de cartera aplicada", null, EnLinea, null, EntregaDeCartera.K1),
        [CierreDeCarteraRealizadoV1.Type] = new("Cierre de cartera realizado", null, EnLinea, null, EntregaDeCartera.K1),
        [CierreDeCarteraReabiertoV1.Type] = new("Cierre de cartera reabierto", null, EnLinea, null, EntregaDeCartera.K1),
        // A3 (C15, C91): cuentas contingentes; sin pasar hasta que la contadora decida. Salen si el dueño rechaza A3.
        [CreditoAprobadoSinDesembolsarV1.Type] = new("Crédito aprobado sin desembolsar", "CN", NoPasa, null, EntregaDeCartera.K1),
        [CupoRotativoOtorgadoV1.Type] = new("Cupo rotativo otorgado", "CN", NoPasa, null, EntregaDeCartera.K1),
        [GarantiaConstituidaV1.Type] = new("Garantía constituida", "CN", NoPasa, null, EntregaDeCartera.K1),
        // K6b (T1056; pregunta E12, propuesta por defecto): la corrección monetaria de los créditos en UVR, por lotes como la causación.
        [CorreccionMonetariaUvrCausadaV1.Type] = new("Corrección monetaria UVR causada", "CA", PorLotes, FinDeCausacion, EntregaDeCartera.K6b),
    };

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb!;
        var aplicadas = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!aplicadas.Any(m => m.EndsWith("_" + MigracionQueCreaLasTablas, StringComparison.Ordinal)))
        {
            context.Logger.LogInformation("[Cartera.TiposDeHechoSinTabla] La base no tiene todavía la migración {Migracion}: no se siembran los tipos de hecho.",
                MigracionQueCreaLasTablas);
            return 0;
        }
        return await AplicarAsync(db, context.Logger, ct);
    }

    /// <summary>Las filas que debe haber, armadas del catálogo de mensajes (lanza si a un tipo le falta código corto o datos).</summary>
    public static IReadOnlyList<LendingFactType> Catalogo() => CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Select(tipo =>
    {
        if (string.IsNullOrWhiteSpace(tipo.CodigoCorto))
            throw new InvalidOperationException($"El tipo de hecho {tipo.Type} no tiene código corto en el catálogo de mensajes de Cartera (C117).");
        if (!Datos.TryGetValue(tipo.Type, out var datos))
            throw new InvalidOperationException($"El tipo de hecho {tipo.Type} no tiene nombre, comprobante ni modo en LendingFactTypesSeeder.Datos.");
        var informativo = tipo.Kind == IntegrationMessageKind.Informational;
        return new LendingFactType
        {
            Code = tipo.Type,
            ShortCode = tipo.CodigoCorto,
            Name = datos.Nombre,
            IsInformational = informativo,
            VoucherTypeCode = informativo ? null : datos.Comprobante,
            DefaultMode = datos.Modo,
            DefaultBatchTrigger = datos.Disparador,
            DeliveredIn = datos.Entrega,
            IsSeeded = true,
            CreatedBy = SeedContext.ParametricCreatedBy,
        };
    }).ToList();

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory). Inserta lo que falta y pone al día lo sembrado.</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, ILogger logger, CancellationToken ct)
    {
        var deseados = Catalogo();
        var existentes = (await db.LendingFactTypes.IgnoreQueryFilters().ToListAsync(ct)).ToDictionary(t => t.Code, StringComparer.Ordinal);

        var cambios = 0;
        foreach (var d in deseados)
        {
            if (!existentes.TryGetValue(d.Code, out var e))
            {
                db.LendingFactTypes.Add(d);
                cambios++;
                continue;
            }
            if (e.Name == d.Name && e.ShortCode == d.ShortCode && e.IsInformational == d.IsInformational && e.VoucherTypeCode == d.VoucherTypeCode
                && e.DefaultMode == d.DefaultMode && e.DefaultBatchTrigger == d.DefaultBatchTrigger && e.DeliveredIn == d.DeliveredIn)
                continue;
            e.Name = d.Name;
            e.ShortCode = d.ShortCode;
            e.IsInformational = d.IsInformational;
            e.VoucherTypeCode = d.VoucherTypeCode;
            e.DefaultMode = d.DefaultMode;
            e.DefaultBatchTrigger = d.DefaultBatchTrigger;
            e.DeliveredIn = d.DeliveredIn;
            e.UpdatedBy = SeedContext.ParametricCreatedBy;
            cambios++;
        }

        if (cambios > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Tipos de hecho de Cartera: {Cambios} insertados o puestos al día de {Total}.", cambios, deseados.Count);
        }
        return cambios;
    }
}
