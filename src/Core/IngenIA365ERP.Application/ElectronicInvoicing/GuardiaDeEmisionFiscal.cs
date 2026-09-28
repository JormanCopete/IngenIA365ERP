using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing;

/// <summary>Los tres veredictos de <see cref="GuardiaDeEmisionFiscal"/> (contracts/dian.md §10.4). (nuevo)</summary>
public enum VeredictoFiscal
{
    /// <summary>Se emite electrónicamente por el canal y con la resolución del veredicto (I4, US8).</summary>
    Electronic = 1,

    /// <summary>La cooperativa no está obligada a la fecha: comprobante no electrónico y su nota.</summary>
    NonElectronic = 2,

    /// <summary>No se confirma ninguna venta fiscal; los motivos dicen qué falta, dónde y con qué permiso.</summary>
    Blocked = 3,
}

/// <summary>
/// Un motivo de <see cref="VeredictoFiscal.Blocked"/>: qué falta, dónde se completa y con qué permiso (nunca sin salida). <see cref="Page"/>
/// es la pantalla que lo arregla (<c>whoFixes.page</c> de api.md §24.3; I4). (nuevo)
/// </summary>
public sealed record MotivoDeBloqueoFiscal(string Code, string Message, string Where, string? Permission, string? Page = null);

/// <summary>
/// El veredicto con lo que permite: las clases que confirman y, si está bloqueado, los motivos (<c>data.missing[]</c> de
/// <c>ElectronicInvoicing.NotReady</c>). Con <c>Electronic</c> lleva el canal y la resolución (su número) con que se emitirá. (nuevo)
/// </summary>
public sealed record EvaluacionFiscal(
    VeredictoFiscal Veredicto,
    IReadOnlyList<DocumentClass> ClasesAdmitidas,
    IReadOnlyList<MotivoDeBloqueoFiscal> Motivos,
    string? Canal = null,
    string? Resolucion = null)
{
    /// <summary>¿Admite confirmar un documento de <paramref name="clase"/>?</summary>
    public bool Admite(DocumentClass clase) => Veredicto != VeredictoFiscal.Blocked && ClasesAdmitidas.Contains(clase);
}

/// <summary>
/// Lo que la guardia mira para decidir (T706): la configuración vigente, el registro de canales, las resoluciones del ambiente con sus
/// asociaciones, la contingencia 03 abierta del canal y, si el documento sale de una caja con 03 abierta, el prefijo del tipo de su rol de
/// contingencia. Se arma en <see cref="GuardiaDeEmisionFiscal.EvaluarAsync"/> y lo decide la regla pura
/// <see cref="GuardiaDeEmisionFiscal.Decidir"/>. (nuevo)
/// </summary>
public sealed record FotoDeEmision(
    DateOnly Fecha,
    InventoryDocumentType Tipo,
    ElectronicDocumentKind TipoElectronico,
    ElectronicEmissionSetting? Configuracion,
    bool CanalRegistrado,
    CapacidadesDelCanal? Capacidades,
    IReadOnlyList<DianNumberingResolution> Resoluciones,
    DianContingencyEvent? Contingencia03,
    bool CajaConsultada,
    string? PrefijoDeContingenciaDeLaCaja);

/// <summary>
/// La decisión única de emitir (feature 012; I3, T609; I4, T706; contracts/dian.md §10.4; patrón de <c>GuardiaDeMetodos.Evaluar</c>): la
/// consultan la confirmación de las ventas (<c>ReglasDeConfirmacionDeVenta</c>), la apertura de la sesión de caja (aviso temprano, sin
/// bloquear) y <c>GET /api/electronic-invoicing/readiness</c> (<c>GetDianReadinessQuery</c>). La firma no cambió desde I3:
/// <c>EvaluarAsync(fecha, tipoDeDocumento, caja?)</c>.
///
/// <para>
/// Mientras la entrega vigente del comercio sea anterior a I4 (<see cref="CatalogoDeParametros.EntregaVigente"/>) responde como en I3:
/// <c>NonElectronic</c> si la cooperativa no está obligada y la clase es de venta, y <c>Blocked</c> con
/// <c>ElectronicInvoicing.I4NotActive</c> en otro caso. Desde I4 decide con la configuración de emisión vigente, el canal y sus
/// capacidades, la credencial verificada, las resoluciones asociadas al canal, el set de pruebas y la contingencia 03 abierta, y nunca
/// bloquea sin decir qué falta, dónde y con qué permiso (<c>ElectronicInvoicing.Readiness.*</c>). El documento soporte no depende de
/// <c>Dian.ObligadaAFacturar</c> (Res. 167): exige canal configurado y resolución <c>SupportDocument</c>.
/// </para>
/// </summary>
public sealed class GuardiaDeEmisionFiscal(
    ILectorDeParametros parametros,
    IApplicationDbContext db,
    ICanalesDeEmision? canales = null,
    EntregaDelComercio entregaVigente = CatalogoDeParametros.EntregaVigente)
{
    public const string ObligadaSinI4Code = "ElectronicInvoicing.I4NotActive";

    // Motivos de api.md §24.3.
    public const string NoSettingsCode = "ElectronicInvoicing.Readiness.NoSettings";
    public const string DisabledCode = "ElectronicInvoicing.Readiness.Disabled";
    public const string CredentialNotVerifiedCode = "ElectronicInvoicing.Readiness.CredentialNotVerified";
    public const string NoResolutionCode = "ElectronicInvoicing.Readiness.NoResolution";
    public const string ResolutionNotLinkedCode = "ElectronicInvoicing.Readiness.ResolutionNotLinked";
    public const string TestSetPendingCode = "ElectronicInvoicing.Readiness.TestSetPending";
    public const string NoContingencyResolutionCode = "ElectronicInvoicing.Readiness.NoContingencyResolution";
    public const string DocumentTypeMissingCode = "ElectronicInvoicing.Readiness.DocumentTypeMissing";
    public const string CompanyDataIncompleteCode = "ElectronicInvoicing.Readiness.CompanyDataIncomplete";

    // Motivos de §10.4 sin código en la tabla de §24.3 (nuevos, registrados en decisiones-transversales §2.17).
    public const string ChannelUnknownCode = "ElectronicInvoicing.Readiness.ChannelUnknown";
    public const string DocumentTypeNotSupportedCode = "ElectronicInvoicing.Readiness.DocumentTypeNotSupported";
    public const string SimulatedInProductionCode = "ElectronicInvoicing.Readiness.SimulatedInProduction";
    public const string ClassNotAllowedCode = "ElectronicInvoicing.Readiness.ClassNotAllowed";

    /// <summary>El canal simulado: sólo ambiente de pruebas (contracts/dian.md §3.4).</summary>
    public const string CanalSimulado = "SIMULADO";

    public const string PaginaDeConfiguracion = "/admin/facturacion-electronica";
    public const string PaginaDeResoluciones = "/maestros/resoluciones-dian";
    public const string PaginaDeTiposDeDocumento = "/inventario/tipos-de-documento";
    public const string PaginaDePuntosDeVenta = "/ventas/puntos-de-venta";
    public const string PaginaDeParametros = "/inventario/parametros";

    public const string PermisoDeConfiguracion = ParametrosDeFacturacionElectronica.Permiso;
    public const string PermisoDeResoluciones = "ElectronicInvoicing.Resolutions.Manage";
    public const string PermisoDeTiposDeDocumento = "Inventory.DocumentTypes.Manage";
    public const string PermisoDePuntosDeVenta = "Inventory.PointsOfSale.Manage";

    /// <summary>Las clases del comprobante no electrónico: las únicas que confirma una cooperativa no obligada.</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesNoElectronicas = [DocumentClass.NonElectronicSalesReceipt, DocumentClass.NonElectronicSalesNote];

    /// <summary>El tipo de documento electrónico de una clase fiscal electrónica; nulo si la clase no emite (contracts/dian.md §4.3).</summary>
    public static ElectronicDocumentKind? TipoElectronicoDe(DocumentClass clase) => clase switch
    {
        DocumentClass.SalesInvoice or DocumentClass.SalesInvoiceFromShipments => ElectronicDocumentKind.Invoice,
        DocumentClass.CreditNote => ElectronicDocumentKind.CreditNote,
        DocumentClass.DebitNote => ElectronicDocumentKind.DebitNote,
        DocumentClass.PosEquivalentDocument => ElectronicDocumentKind.PosEquivalent,
        DocumentClass.PosAdjustmentNote => ElectronicDocumentKind.PosAdjustmentNote,
        DocumentClass.SupportDocument => ElectronicDocumentKind.SupportDocument,
        DocumentClass.SupportDocumentAdjustmentNote => ElectronicDocumentKind.SupportDocumentAdjustmentNote,
        _ => null,
    };

    /// <summary>Las clases fiscales electrónicas: las que confirma el veredicto <c>Electronic</c>.</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesElectronicas = Enum.GetValues<DocumentClass>().Where(c => TipoElectronicoDe(c) is not null).ToList();

    /// <summary>El veredicto para <paramref name="tipo"/> a <paramref name="fecha"/> y, si el documento sale de una caja, en <paramref name="caja"/>.</summary>
    public async Task<EvaluacionFiscal> EvaluarAsync(DateOnly fecha, InventoryDocumentType tipo, CashRegister? caja, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        var leido = await parametros.LeerComoAsync<bool>(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.ObligadaAFacturar, fecha, ct: ct);
        var obligada = leido.IsFailure || leido.Value;
        if (entregaVigente < EntregaDelComercio.I4) return Evaluar(obligada, tipo.Class);

        var deVenta = ClasesDeDocumento.De(tipo.Class).Group == DocumentClassGroup.Sales;
        var electronico = TipoElectronicoDe(tipo.Class);
        if (electronico is null)
        {
            if (!deVenta) return Evaluar(obligada, tipo.Class);
            return obligada ? Bloqueada(ClaseNoAdmitida(tipo)) : new EvaluacionFiscal(VeredictoFiscal.NonElectronic, ClasesNoElectronicas, []);
        }

        var esSoporte = electronico is ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote;
        if (!esSoporte && !obligada) return new EvaluacionFiscal(VeredictoFiscal.NonElectronic, ClasesNoElectronicas, []);

        return Decidir(await FotografiarAsync(fecha, tipo, electronico.Value, caja, ct));
    }

    /// <summary>Arma la <see cref="FotoDeEmision"/> leyendo la configuración, las resoluciones y la contingencia a la fecha.</summary>
    public async Task<FotoDeEmision> FotografiarAsync(DateOnly fecha, InventoryDocumentType tipo, ElectronicDocumentKind electronico, CashRegister? caja, CancellationToken ct)
    {
        var configuracion = await db.ElectronicEmissionSettings.AsNoTracking()
            .Where(s => s.ValidFrom <= fecha && (s.ValidTo == null || s.ValidTo >= fecha))
            .OrderByDescending(s => s.ValidFrom)
            .FirstOrDefaultAsync(ct);
        if (configuracion is null) return new FotoDeEmision(fecha, tipo, electronico, null, false, null, [], null, false, null);

        var codigo = ReglasDeResolucion.Canal(configuracion.ChannelCode);
        var registrado = canales is not null && canales.Codigos.Contains(codigo, StringComparer.OrdinalIgnoreCase);
        var capacidades = registrado ? canales!.Resolver(codigo).Capacidades : null;
        var resoluciones = await db.DianNumberingResolutions.AsNoTracking().Include(r => r.Channels)
            .Where(r => r.IsActive && r.Environment == configuracion.Environment)
            .ToListAsync(ct);
        var contingencia = await db.DianContingencyEvents.AsNoTracking()
            .Where(e => e.Type == ContingencyType.Issuer03 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == codigo)
            .FirstOrDefaultAsync(ct);

        string? prefijoDeContingencia = null;
        var cajaConsultada = false;
        if (caja is not null && contingencia is not null)
        {
            var roles = await db.CashRegisterDocumentTypes.AsNoTracking()
                .Where(r => r.CashRegisterId == caja.Id)
                .Select(r => new { r.Role, r.DocumentTypeId, r.DocumentType!.FiscalPrefix })
                .ToListAsync(ct);
            var rolDeVenta = roles.FirstOrDefault(r => r.DocumentTypeId == tipo.Id && r.Role is CashRegisterDocumentRole.PosSale or CashRegisterDocumentRole.InvoiceOnRequest);
            if (rolDeVenta is not null)
            {
                cajaConsultada = true;
                var rolDeContingencia = rolDeVenta.Role == CashRegisterDocumentRole.PosSale
                    ? CashRegisterDocumentRole.PosSaleContingency
                    : CashRegisterDocumentRole.InvoiceContingency;
                prefijoDeContingencia = roles.FirstOrDefault(r => r.Role == rolDeContingencia)?.FiscalPrefix;
                prefijoDeContingencia ??= roles.Any(r => r.Role == rolDeContingencia) ? string.Empty : null;
            }
        }

        return new FotoDeEmision(fecha, tipo, electronico, configuracion, registrado, capacidades, resoluciones, contingencia,
            cajaConsultada, prefijoDeContingencia);
    }

    /// <summary>
    /// La regla pura de I4 (§10.4): todo lo que falta, en orden; sin motivos → <c>Electronic(canal, resolución)</c>. Sin configuración vigente
    /// no se sigue mirando: todo lo demás depende de ella.
    /// </summary>
    public static EvaluacionFiscal Decidir(FotoDeEmision foto)
    {
        ArgumentNullException.ThrowIfNull(foto);
        var motivos = new List<MotivoDeBloqueoFiscal>();
        var cfg = foto.Configuracion;
        var nombreDelTipo = ReglasDeResolucion.Nombre(foto.TipoElectronico);
        if (cfg is null)
        {
            motivos.Add(Motivo(NoSettingsCode, $"No hay configuración de emisión electrónica vigente el {foto.Fecha:dd/MM/yyyy}.",
                "Administración › Facturación electrónica", PermisoDeConfiguracion, PaginaDeConfiguracion));
            return Bloqueada([.. motivos]);
        }

        var canal = ReglasDeResolucion.Canal(cfg.ChannelCode);
        if (!cfg.IsEnabled)
            motivos.Add(Motivo(DisabledCode, "La emisión electrónica está desactivada en la configuración vigente.",
                "Administración › Facturación electrónica", PermisoDeConfiguracion, PaginaDeConfiguracion));
        if (cfg.Environment == DianEnvironment.Production && string.Equals(canal, CanalSimulado, StringComparison.OrdinalIgnoreCase))
            motivos.Add(Motivo(SimulatedInProductionCode, "La configuración vigente usa el canal simulado en producción: no tiene validez fiscal.",
                "Administración › Facturación electrónica", PermisoDeConfiguracion, PaginaDeConfiguracion));
        if (!foto.CanalRegistrado)
            motivos.Add(Motivo(ChannelUnknownCode, $"El canal «{canal}» de la configuración vigente no está disponible en esta instalación.",
                "Administración › Facturación electrónica", PermisoDeConfiguracion, PaginaDeConfiguracion));
        if (cfg.CredentialVerifiedAt is null)
            motivos.Add(Motivo(CredentialNotVerifiedCode, $"La credencial del canal {canal} no está verificada.",
                "Administración › Facturación electrónica › Verificar credencial", PermisoDeConfiguracion, PaginaDeConfiguracion));
        if (foto.Capacidades is { } capacidades && !capacidades.Emite(foto.TipoElectronico))
            motivos.Add(Motivo(DocumentTypeNotSupportedCode, $"El canal {canal} no emite {nombreDelTipo}.",
                "Administración › Facturación electrónica", PermisoDeConfiguracion, PaginaDeConfiguracion));
        if (cfg.Environment == DianEnvironment.Production && cfg.Mode == EmissionMode.OwnSoftware && cfg.TestSetAcceptedAt is null)
            motivos.Add(Motivo(TestSetPendingCode, "En producción con software propio falta que la DIAN acepte el set de pruebas.",
                "Administración › Facturación electrónica", PermisoDeConfiguracion, PaginaDeConfiguracion));
        if (string.IsNullOrWhiteSpace(cfg.IssuerTaxId) || string.IsNullOrWhiteSpace(cfg.IssuerBusinessName)
            || string.IsNullOrWhiteSpace(cfg.IssuerMunicipalityDaneCode) || string.IsNullOrWhiteSpace(cfg.IssuerEmail))
            motivos.Add(Motivo(CompanyDataIncompleteCode, "Faltan datos del emisor: NIT, razón social, municipio (DIVIPOLA) o correo.",
                "Administración › Facturación electrónica", PermisoDeConfiguracion, PaginaDeConfiguracion));

        string? resolucion = null;
        var tipoDeResolucion = ReglasDeResolucion.TipoDeResolucion(foto.TipoElectronico);
        if (tipoDeResolucion is { } tipoBase)
        {
            var kind = foto.Tipo.IsContingency ? ResolutionKind.Contingency : tipoBase;
            var (motivo, elegida) = ResolucionDelTipo(foto, kind, tipoBase, ReglasDeResolucion.Prefijo(foto.Tipo.FiscalPrefix), canal, cfg.SoftwareId);
            if (motivo is not null) motivos.Add(motivo);
            resolucion = elegida?.ResolutionNumber;

            if (foto.Contingencia03 is not null && !foto.Tipo.IsContingency)
                motivos.AddRange(ContingenciaDelTipo(foto, tipoBase, canal, cfg.SoftwareId));
        }

        return motivos.Count > 0
            ? Bloqueada([.. motivos])
            : new EvaluacionFiscal(VeredictoFiscal.Electronic, ClasesElectronicas, [], canal, resolucion);
    }

    /// <summary>
    /// La regla pura de I3 (T609), vigente mientras la entrega del comercio sea anterior a I4: sin obligación y clase de venta →
    /// <see cref="VeredictoFiscal.NonElectronic"/>; en otro caso → <see cref="VeredictoFiscal.Blocked"/> (la emisión llega con I4).
    /// </summary>
    public static EvaluacionFiscal Evaluar(bool obligada, DocumentClass clase)
    {
        var deVenta = ClasesDeDocumento.De(clase).Group == DocumentClassGroup.Sales;
        if (!obligada && deVenta) return new EvaluacionFiscal(VeredictoFiscal.NonElectronic, ClasesNoElectronicas, []);
        return Bloqueada(
            new MotivoDeBloqueoFiscal(ObligadaSinI4Code,
                "La cooperativa está obligada a facturar electrónicamente y la entrega de documentos electrónicos (I4) no está activa.",
                "Parámetros › Facturación electrónica (Dian.ObligadaAFacturar)", ParametrosDeFacturacionElectronica.Permiso, PaginaDeParametros));
    }

    private static (MotivoDeBloqueoFiscal? Motivo, DianNumberingResolution? Elegida) ResolucionDelTipo(FotoDeEmision foto, ResolutionKind kind,
        ResolutionKind tipoBase, string prefijo, string canal, string? softwareId)
    {
        var delTipo = foto.Resoluciones
            .Where(r => r.Kind == kind && r.Prefix == prefijo && (kind != ResolutionKind.Contingency || r.BacksUpKind == tipoBase))
            .ToList();
        var nombre = ReglasDeResolucion.Nombre(kind);
        var vigentes = delTipo.Where(r => r.VigenteEn(foto.Fecha)).ToList();
        if (vigentes.Count == 0)
        {
            var mensaje = delTipo.Any(r => r.ValidTo < foto.Fecha)
                ? $"La resolución de {nombre} con prefijo «{prefijo}» está vencida."
                : $"No hay resolución de {nombre} con prefijo «{prefijo}» vigente el {foto.Fecha:dd/MM/yyyy}.";
            return (Motivo(NoResolutionCode, mensaje, "Maestros › Resoluciones DIAN", PermisoDeResoluciones, PaginaDeResoluciones), null);
        }

        var asociadas = vigentes.Where(r => ReglasDeResolucion.AsociacionVigente(r, canal, softwareId, foto.Fecha) is not null).ToList();
        if (asociadas.Count == 0)
            return (Motivo(ResolutionNotLinkedCode, $"La resolución de {nombre} con prefijo «{prefijo}» no está asociada al canal {canal}.",
                "Maestros › Resoluciones DIAN › Asociar al canal", PermisoDeResoluciones, PaginaDeResoluciones), null);

        var disponible = asociadas.FirstOrDefault(r => !r.Agotada);
        if (disponible is null)
            return (Motivo(NoResolutionCode, $"La resolución de {nombre} con prefijo «{prefijo}» agotó su rango.",
                "Maestros › Resoluciones DIAN", PermisoDeResoluciones, PaginaDeResoluciones), null);
        return (null, disponible);
    }

    private static IEnumerable<MotivoDeBloqueoFiscal> ContingenciaDelTipo(FotoDeEmision foto, ResolutionKind tipoBase, string canal, string? softwareId)
    {
        if (foto.CajaConsultada && foto.PrefijoDeContingenciaDeLaCaja is null)
        {
            yield return Motivo(DocumentTypeMissingCode,
                "Hay contingencia abierta y la caja no tiene el tipo de documento del rol de contingencia que respalda a esta venta.",
                "Ventas › Puntos de venta › Cajas", PermisoDePuntosDeVenta, PaginaDePuntosDeVenta);
            yield break;
        }

        var nombre = ReglasDeResolucion.Nombre(tipoBase);
        var candidatas = foto.Resoluciones
            .Where(r => r.Kind == ResolutionKind.Contingency && r.BacksUpKind == tipoBase && !r.Agotada && r.VigenteEn(foto.Fecha))
            .Where(r => foto.PrefijoDeContingenciaDeLaCaja is null || r.Prefix == ReglasDeResolucion.Prefijo(foto.PrefijoDeContingenciaDeLaCaja))
            .Where(r => ReglasDeResolucion.AsociacionVigente(r, canal, softwareId, foto.Fecha) is not null);
        if (!candidatas.Any())
            yield return Motivo(NoContingencyResolutionCode,
                $"Hay contingencia abierta y no hay resolución de contingencia vigente, asociada al canal {canal}, que respalde la {nombre}.",
                "Maestros › Resoluciones DIAN", PermisoDeResoluciones, PaginaDeResoluciones);
    }

    private static MotivoDeBloqueoFiscal ClaseNoAdmitida(InventoryDocumentType tipo) =>
        Motivo(ClassNotAllowedCode,
            $"La cooperativa está obligada a facturar electrónicamente y el tipo {tipo.Code} es de un comprobante no electrónico.",
            "Inventario › Tipos de documento", PermisoDeTiposDeDocumento, PaginaDeTiposDeDocumento);

    private static MotivoDeBloqueoFiscal Motivo(string code, string message, string where, string permission, string page) =>
        new(code, message, where, permission, page);

    private static EvaluacionFiscal Bloqueada(params MotivoDeBloqueoFiscal[] motivos) => new(VeredictoFiscal.Blocked, [], motivos);
}
