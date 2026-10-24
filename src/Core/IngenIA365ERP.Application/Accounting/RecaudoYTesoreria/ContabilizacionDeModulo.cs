using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Accounting.Integration;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;

/// <summary>Un error al armar el comprobante, con el hecho, el rol y la combinación de donde salió. (nuevo)</summary>
public sealed record FalloDeModulo(Error Error, string FactType, string? Role, string? Combination);

/// <summary>Una línea del comprobante con lo que la explica: hecho, rol, nombre de la cuenta y si conserva detalle en un resumido. (nuevo)</summary>
public sealed record LineaConstruida(PostingLine Linea, string Hecho, string Rol, string AccountName, bool ConservaDetalle);

/// <summary>El comprobante de una unidad, o sus fallos, o por qué no lleva comprobante. (nuevo)</summary>
public sealed record ConstruccionDeModulo(
    PostingRequest? Request,
    IReadOnlyList<LineaConstruida> Mapa,
    IReadOnlyList<FalloDeModulo> Fallos,
    DateOnly Fecha,
    string? SinComprobante)
{
    public bool EsValida => Fallos.Count == 0;

    public bool SinLineas => EsValida && Request is null;
}

/// <summary>Lo que el constructor necesita, cargado una vez por unidad o por grupo: la matriz, los mapeos, las personas y las sucursales. (nuevo)</summary>
public sealed record CatalogosDeModulo(
    MatrizVigente Matriz,
    IReadOnlyDictionary<string, (string Comprobante, string? Cruce)> Mapeos,
    IReadOnlyDictionary<Guid, int> Personas,
    IReadOnlyDictionary<Guid, int> Sucursales);

/// <summary>
/// El constructor de comprobantes de Recaudo y Tesorería (feature 015, T0257; contabilidad.md §2 a §7): por cada línea de cada hecho, la cuenta que resuelve la matriz
/// (o el vínculo de la cuenta propia); el tercero y el cruce sólo donde la cuenta los exige; la sucursal del sobre; el centro de costo de la regla si la cuenta lo
/// maneja. El tipo de comprobante lo da el hecho principal de la unidad. Puro: lo cargado viene en <see cref="CatalogosDeModulo"/>. (nuevo)
/// </summary>
public static class ConstructorDeComprobanteDeModulo
{
    /// <summary>El tipo de documento cruce por defecto cuando el mapeo no trae uno y la cuenta lo exige.</summary>
    public const string CruceOtro = "OT";

    public static ConstruccionDeModulo Construir(ModuloDeLaMatriz modulo, IReadOnlyList<HechoContableDeModulo> unidad, CatalogosDeModulo c)
    {
        var principal = unidad[0];
        var fecha = principal.Fecha;
        var fallos = new List<FalloDeModulo>();
        if (!c.Mapeos.TryGetValue(principal.Tipo, out var mapeo))
            fallos.Add(new FalloDeModulo(ErroresDeLaMatriz.SinComprobante(modulo, principal.Tipo), principal.Tipo, null, null));

        var sucursal = c.Sucursales.TryGetValue(principal.Mensaje.Envelope.BranchPublicId, out var s) ? s : (int?)null;
        var lineas = new List<LineaConstruida>();
        foreach (var hecho in unidad)
        {
            foreach (var l in hecho.Lineas)
            {
                if (l.Valor == 0m) continue;
                var resuelta = c.Matriz.Resolver(hecho.Tipo, l, hecho.Fecha);
                if (resuelta.IsFailure)
                {
                    fallos.Add(new FalloDeModulo(resuelta.Error, hecho.Tipo, l.Rol, l.Combinacion));
                    continue;
                }
                var cuenta = resuelta.Value!;
                int? persona = null;
                if (cuenta.Reglas.RequiresThirdParty && (l.Tercero ?? hecho.Mensaje.Envelope.PersonPublicId) is { } tercero && c.Personas.TryGetValue(tercero, out var id))
                    persona = id;
                string? tipoDeCruce = null, numeroDeCruce = null;
                if (cuenta.Reglas.RequiresCrossDocument)
                {
                    tipoDeCruce = mapeo.Cruce ?? CruceOtro;
                    numeroDeCruce = string.IsNullOrWhiteSpace(l.Cruce) ? hecho.Mensaje.Envelope.Origin.Number : l.Cruce;
                }
                var valor = Math.Abs(l.Valor);
                var debito = l.Valor > 0m == l.Debito;
                lineas.Add(new LineaConstruida(new PostingLine
                {
                    AccountCode = cuenta.Cuenta.Code,
                    Debit = debito ? valor : 0m,
                    Credit = debito ? 0m : valor,
                    Detail = Detalle(hecho, l),
                    PersonId = persona,
                    BranchId = sucursal,
                    CostCenterId = cuenta.Reglas.RequiresCostCenter ? cuenta.CostCenterId : null,
                    CrossDocumentType = tipoDeCruce,
                    CrossDocumentNumber = numeroDeCruce,
                    TaxBase = cuenta.Reglas.RequiresTaxBase ? valor : null,
                }, hecho.Tipo, l.Rol, cuenta.Cuenta.Name, cuenta.Reglas.RequiresThirdParty || cuenta.Reglas.RequiresCrossDocument || cuenta.Reglas.RequiresTaxBase));
            }
        }
        if (fallos.Count > 0) return new ConstruccionDeModulo(null, [], fallos, fecha, null);
        if (lineas.Count == 0) return new ConstruccionDeModulo(null, [], [], fecha, RecepcionContableDeModulo.SinComprobanteValorCero);

        // Las dos puntas en la misma cuenta, sucursal, tercero y cruce se anulan: recibo sin comprobante (como C21 de la 013).
        var netas = lineas.GroupBy(x => (x.Linea.AccountCode, x.Linea.BranchId, x.Linea.PersonId, x.Linea.CrossDocumentType, x.Linea.CrossDocumentNumber))
            .All(g => g.Sum(x => x.Linea.Debit - x.Linea.Credit) == 0m);
        if (netas) return new ConstruccionDeModulo(null, [], [], fecha, RecepcionContableDeModulo.SinComprobanteMismasCuentas);

        var nombre = modulo.Hecho(principal.Tipo)?.Nombre ?? principal.Tipo;
        var origen = principal.Mensaje.Envelope.Origin;
        var descripcion = Recortar($"{modulo.Nombre} · {nombre} {origen.Number}".Trim());
        var request = new PostingRequest(mapeo.Comprobante, fecha, descripcion,
            new AccountingOrigin(modulo.Codigo, principal.Mensaje.OriginDocumentClassText ?? principal.Tipo, origen.PublicId),
            lineas.Select(x => x.Linea).ToList(), DocumentKind.Regular,
            new UsuarioDeOrigen(principal.Mensaje.Envelope.OriginUser.CentralUserId, principal.Mensaje.Envelope.OriginUser.Name ?? string.Empty));
        return new ConstruccionDeModulo(request, lineas, [], fecha, null);
    }

    private static string Detalle(HechoContableDeModulo hecho, LineaDeHecho l) =>
        Recortar(string.IsNullOrWhiteSpace(l.Cruce) || l.Cruce == hecho.Mensaje.Envelope.Origin.Number
            ? $"{hecho.Mensaje.Envelope.Origin.Number} · {l.Rol}"
            : $"{hecho.Mensaje.Envelope.Origin.Number} · {l.Rol} · {l.Cruce}");

    private static string Recortar(string texto) => texto.Length <= 200 ? texto : texto[..200];

    /// <summary>
    /// El comprobante resumido (contabilidad.md §7): suma por cuenta, lado, sucursal y centro (débitos y créditos por separado, sin netear); las cuentas que exigen
    /// tercero, cruce o base conservan su detalle. Fechado en la fecha del grupo, con origen el lote.
    /// </summary>
    public static PostingRequest Resumen(ModuloDeLaMatriz modulo, IReadOnlyList<ConstruccionDeModulo> construcciones, Guid lote, long? numeroDeLote)
    {
        var lineas = new List<PostingLine>();
        var indice = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in construcciones)
        {
            foreach (var x in c.Mapa)
            {
                var l = x.Linea;
                var lado = l.Debit > 0m ? "D" : "C";
                var clave = x.ConservaDetalle
                    ? string.Join('|', "detalle", l.AccountCode, lado, l.BranchId, l.CostCenterId, l.PersonId, l.CrossDocumentType, l.CrossDocumentNumber)
                    : string.Join('|', l.AccountCode, lado, l.BranchId, l.CostCenterId);
                if (indice.TryGetValue(clave, out var j))
                {
                    lineas[j] = lineas[j] with
                    {
                        Debit = lineas[j].Debit + l.Debit, Credit = lineas[j].Credit + l.Credit,
                        TaxBase = lineas[j].TaxBase is null && l.TaxBase is null ? null : (lineas[j].TaxBase ?? 0m) + (l.TaxBase ?? 0m),
                    };
                    continue;
                }
                indice[clave] = lineas.Count;
                lineas.Add(x.ConservaDetalle ? l : l with { PersonId = null, CrossDocumentType = null, CrossDocumentNumber = null, Detail = $"Lote {numeroDeLote} · {x.AccountName}" });
            }
        }
        var primero = construcciones[0];
        var tipo = primero.Request!.VoucherTypeCode;
        var descripcion = string.Create(CultureInfo.InvariantCulture,
            $"Lote {numeroDeLote} · {modulo.Nombre} · {tipo} · {primero.Fecha:yyyy-MM-dd} · {construcciones.Count} {(construcciones.Count == 1 ? "documento" : "documentos")}");
        return new PostingRequest(tipo, primero.Fecha, descripcion, new AccountingOrigin(modulo.Codigo, modulo.OrigenDelLote, lote), lineas, DocumentKind.Regular);
    }
}

/// <summary>
/// Lo común del consumo de los hechos de Recaudo y de Tesorería (feature 015, T0257; molde <c>ConsumoDeCartera</c>): versión y moneda, recibo existente, original
/// pendiente, los recibos de <c>ACC_ReceiptsPostings</c> o <c>ACC_TreasuryPostings</c>, los rechazos con su hecho y su rol, la colisión del recibo único y la
/// auditoría. No guarda. Nunca lee tablas <c>RCD_</c> ni <c>TES_</c>. (nuevo)
/// </summary>
public sealed class ConsumoDeModulo(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj, AccountingAuditEmitter auditoria)
{
    public const string IndiceDeRecaudo = "UK_ACC_ReceiptsPostings_MessagePublicId";
    public const string IndiceDeTesoreria = "UK_ACC_TreasuryPostings_MessagePublicId";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool EsColision(DbUpdateException ex) =>
        RecibosDeContabilidad.EsColision(ex, IndiceDeRecaudo) || RecibosDeContabilidad.EsColision(ex, IndiceDeTesoreria);

    /// <summary>Lo que carga el constructor para estas unidades.</summary>
    public async Task<CatalogosDeModulo> CatalogosAsync(ModuloDeLaMatriz modulo, ResolutorDeLaMatriz resolutor, IReadOnlyList<HechoContableDeModulo> hechos,
        CancellationToken ct)
    {
        var desde = hechos.Count == 0 ? reloj.TodayUtc : hechos.Min(h => h.Fecha);
        var hasta = hechos.Count == 0 ? reloj.TodayUtc : hechos.Max(h => h.Fecha);
        var matriz = await resolutor.CargarAsync(modulo, desde, hasta, ct);
        var mapeos = await MapeosAsync(modulo, ct);
        var guids = hechos.SelectMany(h => h.Lineas.Select(l => l.Tercero).Append(h.Mensaje.Envelope.PersonPublicId))
            .Where(g => g is not null).Select(g => g!.Value).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => guids.Contains(p.PublicId)).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        var oficinas = hechos.Select(h => h.Mensaje.Envelope.BranchPublicId).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().Where(b => oficinas.Contains(b.PublicId)).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
        return new CatalogosDeModulo(matriz, mapeos, personas, sucursales);
    }

    /// <summary>El tipo de comprobante y el cruce de cada hecho del módulo.</summary>
    public async Task<IReadOnlyDictionary<string, (string Comprobante, string? Cruce)>> MapeosAsync(ModuloDeLaMatriz modulo, CancellationToken ct)
    {
        var filas = modulo.Codigo == ModuloContable.Recaudo
            ? await db.ReceiptsVoucherMappings.AsNoTracking()
                .Select(m => new { m.FactType, Tipo = m.VoucherType!.Code, Cruce = m.CrossDocumentType == null ? null : m.CrossDocumentType.Code }).ToListAsync(ct)
            : await db.TreasuryVoucherMappings.AsNoTracking()
                .Select(m => new { m.FactType, Tipo = m.VoucherType!.Code, Cruce = m.CrossDocumentType == null ? null : m.CrossDocumentType.Code }).ToListAsync(ct);
        return filas.ToDictionary(f => f.FactType, f => (f.Tipo, f.Cruce), StringComparer.Ordinal);
    }

    /// <summary>Versión y moneda (rechazo), recibo existente (ya procesado), original pendiente (reintento), o nulo si la unidad sigue.</summary>
    public async Task<ResultadoDeConsumo?> RevisarAsync(ModuloDeLaMatriz modulo, IReadOnlyList<MensajeEntrante> unidad, CancellationToken ct)
    {
        if (RecibosDeContabilidad.RevisarVersionYMoneda(unidad, modulo.Codigo, (moneda, tasa) => new Error("Accounting.ModuleFact.CurrencyNotSupported",
                $"Contabilidad sólo recibe de {modulo.Nombre} pesos colombianos con tasa 1; el hecho viene en {moneda} con tasa {tasa:0.######}.")) is { } rechazo)
            return rechazo;
        if (await YaProcesadoAsync(modulo, unidad.Select(m => m.MessageId).ToList(), ct) is { } ya) return ya;

        var originales = unidad.Select(m => m.Envelope.Related?.PublicId).Where(g => g is { } x && x != Guid.Empty).Select(g => g!.Value).Distinct().ToList();
        if (originales.Count > 0)
        {
            var contabilizados = modulo.Codigo == ModuloContable.Recaudo
                ? await db.ReceiptsPostings.AsNoTracking().Where(p => originales.Contains(p.SourcePublicId)).Select(p => p.SourcePublicId).Distinct().ToListAsync(ct)
                : await db.TreasuryPostings.AsNoTracking().Where(p => originales.Contains(p.SourcePublicId)).Select(p => p.SourcePublicId).Distinct().ToListAsync(ct);
            var faltan = originales.Except(contabilizados).ToList();
            if (faltan.Count > 0 && await RecibosDeContabilidad.OriginalPendienteAsync(db, faltan, ct) is { } original)
                return new ResultadoDeConsumo.Retry($"El hecho original {original} de {modulo.Nombre} todavía no está contabilizado.", "Accounting.ModuleFact.WaitingForOriginal");
        }
        return null;
    }

    public async Task<ResultadoDeConsumo?> YaProcesadoAsync(ModuloDeLaMatriz modulo, IReadOnlyList<Guid> mensajes, CancellationToken ct)
    {
        var recibo = modulo.Codigo == ModuloContable.Recaudo
            ? await db.ReceiptsPostings.AsNoTracking().Where(p => mensajes.Contains(p.MessagePublicId)).OrderBy(p => p.Id)
                .Select(p => new { p.AccountingDocumentId, p.NoVoucherReason }).FirstOrDefaultAsync(ct)
            : await db.TreasuryPostings.AsNoTracking().Where(p => mensajes.Contains(p.MessagePublicId)).OrderBy(p => p.Id)
                .Select(p => new { p.AccountingDocumentId, p.NoVoucherReason }).FirstOrDefaultAsync(ct);
        if (recibo is null) return null;
        if (recibo.AccountingDocumentId is not { } documentoId) return new ResultadoDeConsumo.AlreadyProcessed(null, null, null, MotivoDe(recibo.NoVoucherReason));
        var documento = await db.AccountingDocuments.AsNoTracking().Where(d => d.Id == documentoId)
            .Select(d => new { d.PublicId, Codigo = d.VoucherType!.Code, d.Number }).FirstAsync(ct);
        return new ResultadoDeConsumo.AlreadyProcessed(documento.PublicId, documento.Codigo, documento.Number?.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Un recibo por hecho, con el comprobante o el motivo sin comprobante. No guarda.</summary>
    public async Task AgregarRecibosAsync(ModuloDeLaMatriz modulo, IReadOnlyList<MensajeEntrante> unidad, AccountingDocument? comprobante, string? sinComprobante,
        Guid? lote, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        foreach (var m in unidad)
        {
            var sobre = m.Envelope;
            var proceso = sobre.OriginUser.CentralUserId is null;
            RecepcionContableDeModulo recibo = modulo.Codigo == ModuloContable.Recaudo ? new ReceiptsPosting
            {
                MessagePublicId = sobre.MessageId, MessageType = sobre.Type, MessageVersion = (short)sobre.Version, MessageKind = sobre.Kind, SourceModule = modulo.Codigo,
                SourcePublicId = sobre.Origin.PublicId, SourceDocumentClass = m.OriginDocumentClassText, SourceDocumentTypeCode = sobre.Origin.DocumentTypeCode,
                SourceDocumentNumber = Texto(sobre.Origin.Number), RelatedMessagePublicId = sobre.Related?.PublicId, OperationDate = sobre.Origin.OperationDate,
                AccountingDocument = comprobante, NoVoucherReason = comprobante is null ? sinComprobante : null, BatchPublicId = lote ?? m.Entrega.BatchPublicId,
                OriginActorKind = proceso ? ActorKind.Process : ActorKind.Person, OriginUserCentralId = sobre.OriginUser.CentralUserId,
                OriginUserName = string.IsNullOrWhiteSpace(sobre.OriginUser.Name) ? Actor.NombreDelProceso : sobre.OriginUser.Name, ActorKind = actor.Kind,
                ActorUserId = actor.UserId, ActorName = actor.Name, ProcessedAt = ahora, CreatedAt = ahora, CreatedBy = actor.Name,
            } : new TreasuryPosting
            {
                MessagePublicId = sobre.MessageId, MessageType = sobre.Type, MessageVersion = (short)sobre.Version, MessageKind = sobre.Kind, SourceModule = modulo.Codigo,
                SourcePublicId = sobre.Origin.PublicId, SourceDocumentClass = m.OriginDocumentClassText, SourceDocumentTypeCode = sobre.Origin.DocumentTypeCode,
                SourceDocumentNumber = Texto(sobre.Origin.Number), RelatedMessagePublicId = sobre.Related?.PublicId, OperationDate = sobre.Origin.OperationDate,
                AccountingDocument = comprobante, NoVoucherReason = comprobante is null ? sinComprobante : null, BatchPublicId = lote ?? m.Entrega.BatchPublicId,
                OriginActorKind = proceso ? ActorKind.Process : ActorKind.Person, OriginUserCentralId = sobre.OriginUser.CentralUserId,
                OriginUserName = string.IsNullOrWhiteSpace(sobre.OriginUser.Name) ? Actor.NombreDelProceso : sobre.OriginUser.Name, ActorKind = actor.Kind,
                ActorUserId = actor.UserId, ActorName = actor.Name, ProcessedAt = ahora, CreatedAt = ahora, CreatedBy = actor.Name,
            };
            if (recibo is ReceiptsPosting r) db.ReceiptsPostings.Add(r);
            else db.TreasuryPostings.Add((TreasuryPosting)recibo);
        }
        await Task.CompletedTask;
    }

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Length <= 30 ? valor : valor[..30];

    /// <summary>El rechazo de una unidad que no se pudo armar: el primer error y todos con su hecho, rol y combinación.</summary>
    public static ResultadoDeConsumo.Rejected RechazoDe(ConstruccionDeModulo construccion)
    {
        var primero = construccion.Fallos[0];
        var datos = new
        {
            errors = construccion.Fallos.Select(f => new
            {
                factType = f.FactType, role = f.Role, combination = f.Combination, rule = f.Error.Code, message = f.Error.Message, data = (f.Error as ErrorConDatos)?.Data,
            }).ToList(),
        };
        return new ResultadoDeConsumo.Rejected(primero.Error.Code, primero.Error.Message, JsonSerializer.Serialize(datos, Json));
    }

    /// <summary>El rechazo del contrato de la 009 con los errores de línea traducidos al hecho y al rol de cada línea.</summary>
    public static ResultadoDeConsumo.Rejected RechazoDe(ValidacionDeComprobante validacion, IReadOnlyList<LineaConstruida> mapa, Error? error = null)
    {
        var errores = validacion.Errores;
        var codigo = errores.Count == 1 ? errores[0].Code : error?.Code ?? "Accounting.Document.Invalid";
        var mensaje = errores.Count == 1 ? errores[0].Message : error?.Message ?? string.Join(" ", errores.Select(e => e.Message));
        var datos = new
        {
            errors = errores.Select(e =>
            {
                var linea = e.LineNumber >= 1 && e.LineNumber <= mapa.Count ? mapa[e.LineNumber - 1] : null;
                return new { voucherLine = e.LineNumber, factType = linea?.Hecho, role = linea?.Rol, accountCode = e.AccountCode ?? linea?.Linea.AccountCode, field = e.Field, rule = e.Code, message = e.Message };
            }).ToList(),
        };
        return new ResultadoDeConsumo.Rejected(codigo, mensaje, JsonSerializer.Serialize(datos, Json));
    }

    public Task AuditarContabilizacionAsync(ModuloDeLaMatriz modulo, AccountingDocument comprobante, IReadOnlyList<MensajeEntrante> hechos, Guid? lote, CancellationToken ct) =>
        auditoria.EmitAsync(modulo.Codigo == ModuloContable.Recaudo ? AuditEventTypes.AccountingReceiptsPosted : AuditEventTypes.AccountingTreasuryPosted,
            "AccountingDocument", comprobante.PublicId, null, new
            {
                voucherTypeCode = comprobante.VoucherType?.Code, number = comprobante.Number, date = comprobante.Date,
                facts = hechos.Select(m => m.MessageId).ToList(), origins = hechos.Select(m => m.Envelope.Origin.PublicId).Distinct().ToList(), batchPublicId = lote,
            }, ct);

    public async Task AuditarRechazoAsync(ModuloDeLaMatriz modulo, IReadOnlyList<MensajeEntrante> hechos, ResultadoDeConsumo.Rejected rechazo, CancellationToken ct)
    {
        foreach (var m in hechos)
            await auditoria.EmitAsync(modulo.Codigo == ModuloContable.Recaudo ? AuditEventTypes.AccountingReceiptsRejected : AuditEventTypes.AccountingTreasuryRejected,
                "IntegrationMessage", m.MessageId, null, new { code = rechazo.Code, reason = rechazo.Reason, type = m.Type, origin = m.Envelope.Origin.PublicId }, ct);
    }

    public static MotivoSinComprobante? MotivoDe(string? motivo) => motivo switch
    {
        RecepcionContableDeModulo.SinComprobanteValorCero => MotivoSinComprobante.ZeroValue,
        RecepcionContableDeModulo.SinComprobanteInformativo => MotivoSinComprobante.Informational,
        RecepcionContableDeModulo.SinComprobanteMismasCuentas => MotivoSinComprobante.SameAccounts,
        _ => null,
    };
}

/// <summary>
/// La contabilización de una unidad o de un grupo resumido de hechos de Recaudo o de Tesorería (feature 015, T0257; contabilidad.md §3, §4, §7). Siempre por
/// <see cref="AccountingPoster"/> (un solo camino al libro); el comprobante y sus recibos en un solo guardado; la colisión del recibo es «ya procesado». Lo usan
/// <c>PostReceiptsFactsCommand</c>, <c>PostReceiptsSummaryGroupCommand</c> y <c>PostTreasuryFactsCommand</c>. (nuevo)
/// </summary>
public sealed class ContabilizacionDeModulo(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ConsumoDeModulo consumo,
    AccountingPoster poster,
    ResolutorDeLaMatriz resolutor)
{
    /// <summary>El código que el poster da a una fecha sin período: es reintento, no rechazo.</summary>
    public const string CodigoPeriodoInexistente = "Accounting.Period.NotFound";

    public async Task<Result<ResultadoDeConsumo>> UnidadAsync(ModuloDeLaMatriz modulo, IReadOnlyList<Guid> ids, Guid? lote, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(ids, IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Success<ResultadoDeConsumo>(RecibosDeContabilidad.Rechazo(leidos.Error));
        var unidad = leidos.Value;
        var alto = await consumo.RevisarAsync(modulo, unidad, ct);
        if (alto is not null)
        {
            if (alto is ResultadoDeConsumo.Rejected r) await consumo.AuditarRechazoAsync(modulo, unidad, r, ct);
            return Result.Success(alto);
        }

        var hechos = unidad.Select(HechoContableDeModulo.De).ToList();
        var catalogos = await consumo.CatalogosAsync(modulo, resolutor, hechos, ct);
        var construccion = ConstructorDeComprobanteDeModulo.Construir(modulo, hechos, catalogos);
        if (!construccion.EsValida)
        {
            var rechazo = ConsumoDeModulo.RechazoDe(construccion);
            await consumo.AuditarRechazoAsync(modulo, unidad, rechazo, ct);
            return Result.Success<ResultadoDeConsumo>(rechazo);
        }
        if (construccion.SinLineas)
        {
            await consumo.AgregarRecibosAsync(modulo, unidad, null, construccion.SinComprobante, lote, ct);
            if (!await GuardarAsync(ct))
                return Result.Success(await consumo.YaProcesadoAsync(modulo, ids, ct) ?? new ResultadoDeConsumo.Retry("Otra réplica procesó la unidad."));
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, ConsumoDeModulo.MotivoDe(construccion.SinComprobante)));
        }

        var preparado = await poster.PrepareAsync(construccion.Request!, ct);
        if (preparado.IsFailure)
        {
            db.DescartarCambios();
            if (preparado.Error.Code == CodigoPeriodoInexistente) return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Retry(preparado.Error.Message, preparado.Error.Code));
            var validacion = await poster.ValidarAsync(construccion.Request!, ct);
            var rechazo = validacion.Errores.Count == 0 ? RecibosDeContabilidad.Rechazo(preparado.Error) : ConsumoDeModulo.RechazoDe(validacion, construccion.Mapa, preparado.Error);
            await consumo.AuditarRechazoAsync(modulo, unidad, rechazo, ct);
            return Result.Success<ResultadoDeConsumo>(rechazo);
        }
        var comprobante = preparado.Value;
        await consumo.AgregarRecibosAsync(modulo, unidad, comprobante, null, lote, ct);
        if (!await GuardarAsync(ct))
            return Result.Success(await consumo.YaProcesadoAsync(modulo, ids, ct) ?? new ResultadoDeConsumo.Retry("Otra réplica procesó la unidad."));
        await consumo.AuditarContabilizacionAsync(modulo, comprobante, unidad, lote, ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(comprobante.PublicId, comprobante.VoucherType?.Code,
            comprobante.Number?.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Un grupo resumido (contabilidad.md §7): cada unidad se arma como si fuera por hecho; las que fallan quedan rechazadas y no detienen a las demás; las válidas
    /// se suman en un comprobante por tipo (normalmente uno) fechado en la fecha del grupo.
    /// </summary>
    public async Task<Result<IReadOnlyList<ResultadoDeUnidad>>> GrupoAsync(ModuloDeLaMatriz modulo, Guid lote, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(ids, IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Failure<IReadOnlyList<ResultadoDeUnidad>>(leidos.Error);
        var unidades = AgrupadorDeRecaudo.Unidades(leidos.Value);
        var resultados = new Dictionary<UnidadDeConsumo, ResultadoDeConsumo>();

        var vivas = new List<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)>();
        foreach (var (unidad, mensajes) in unidades)
        {
            var alto = await consumo.RevisarAsync(modulo, mensajes, ct);
            if (alto is not null) resultados[unidad] = alto;
            else vivas.Add((unidad, mensajes));
        }
        var hechos = vivas.Select(v => (IReadOnlyList<HechoContableDeModulo>)v.Mensajes.Select(HechoContableDeModulo.De).ToList()).ToList();
        var catalogos = await consumo.CatalogosAsync(modulo, resolutor, hechos.SelectMany(h => h).ToList(), ct);
        var construidas = vivas.Select((v, i) => (v.Unidad, v.Mensajes, Construccion: ConstructorDeComprobanteDeModulo.Construir(modulo, hechos[i], catalogos))).ToList();

        foreach (var x in construidas.Where(x => !x.Construccion.EsValida))
            resultados[x.Unidad] = ConsumoDeModulo.RechazoDe(x.Construccion);
        var conLineas = construidas.Where(x => x.Construccion.EsValida && !x.Construccion.SinLineas).ToList();
        var validaciones = await poster.ValidarVariosAsync(conLineas.Select(x => x.Construccion.Request!).ToList(), ct);
        for (var i = 0; i < conLineas.Count; i++)
        {
            if (validaciones[i].EsValido) continue;
            var periodo = validaciones[i].Errores.FirstOrDefault(e => e.Code == CodigoPeriodoInexistente);
            resultados[conLineas[i].Unidad] = periodo is not null
                ? new ResultadoDeConsumo.Retry(periodo.Message, periodo.Code)
                : ConsumoDeModulo.RechazoDe(validaciones[i], conLineas[i].Construccion.Mapa);
        }
        foreach (var x in construidas.Where(x => resultados.GetValueOrDefault(x.Unidad) is ResultadoDeConsumo.Rejected))
            await consumo.AuditarRechazoAsync(modulo, x.Mensajes, (ResultadoDeConsumo.Rejected)resultados[x.Unidad], ct);

        foreach (var x in construidas.Where(x => !resultados.ContainsKey(x.Unidad) && x.Construccion.SinLineas).ToList())
        {
            await consumo.AgregarRecibosAsync(modulo, x.Mensajes, null, x.Construccion.SinComprobante, lote, ct);
            resultados[x.Unidad] = new ResultadoDeConsumo.Processed(null, null, null, ConsumoDeModulo.MotivoDe(x.Construccion.SinComprobante));
        }

        var incluidas = conLineas.Where(x => !resultados.ContainsKey(x.Unidad)).ToList();
        var comprobantes = new List<(AccountingDocument Documento, List<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)> Unidades)>();
        if (incluidas.Count > 0)
        {
            var numeroDeLote = await db.IntegrationBatches.AsNoTracking().Where(b => b.PublicId == lote).Select(b => (long?)b.Number).FirstOrDefaultAsync(ct);
            foreach (var porTipo in incluidas.GroupBy(x => x.Construccion.Request!.VoucherTypeCode, StringComparer.Ordinal))
            {
                var deEste = porTipo.ToList();
                var resumen = ConstructorDeComprobanteDeModulo.Resumen(modulo, deEste.Select(x => x.Construccion).ToList(), lote, numeroDeLote);
                var preparado = await poster.PrepareAsync(resumen, ct);
                if (preparado.IsFailure)
                {
                    db.DescartarCambios();
                    ResultadoDeConsumo fallo = preparado.Error.Code == CodigoPeriodoInexistente
                        ? new ResultadoDeConsumo.Retry(preparado.Error.Message, preparado.Error.Code)
                        : RecibosDeContabilidad.Rechazo(preparado.Error);
                    foreach (var x in incluidas)
                    {
                        resultados[x.Unidad] = fallo;
                        if (fallo is ResultadoDeConsumo.Rejected r) await consumo.AuditarRechazoAsync(modulo, x.Mensajes, r, ct);
                    }
                    comprobantes.Clear();
                    break;
                }
                foreach (var x in deEste) await consumo.AgregarRecibosAsync(modulo, x.Mensajes, preparado.Value, null, lote, ct);
                comprobantes.Add((preparado.Value, deEste.Select(x => (x.Unidad, x.Mensajes)).ToList()));
            }
        }

        if (!await GuardarAsync(ct))
            return Result.Success<IReadOnlyList<ResultadoDeUnidad>>(unidades
                .Select(u => new ResultadoDeUnidad(u.Unidad, new ResultadoDeConsumo.Retry("Otra réplica procesó parte del grupo."))).ToList());

        foreach (var (documento, deEste) in comprobantes)
        {
            await consumo.AuditarContabilizacionAsync(modulo, documento, deEste.SelectMany(x => x.Mensajes).ToList(), lote, ct);
            var procesado = new ResultadoDeConsumo.Processed(documento.PublicId, documento.VoucherType?.Code, documento.Number?.ToString(CultureInfo.InvariantCulture));
            foreach (var x in deEste) resultados[x.Unidad] = procesado;
        }
        return Result.Success<IReadOnlyList<ResultadoDeUnidad>>(unidades.Select(u => new ResultadoDeUnidad(u.Unidad, resultados[u.Unidad])).ToList());
    }

    private async Task<bool> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ConsumoDeModulo.EsColision(ex))
        {
            db.DescartarCambios();
            return false;
        }
    }
}

/// <summary>
/// Los débitos y créditos de los comprobantes de Recaudo y Tesorería que dejó un lote (feature 015, T0257): la suma de los comprobantes distintos que nombran sus
/// recibos. <c>DestinoContabilidad.TotalesDelLoteAsync</c> la suma con las de Inventario y Cartera. Sin ruta. (nuevo)
/// </summary>
public sealed record GetReceiptsAndTreasuryBatchTotalsQuery(Guid BatchPublicId) : MediatR.IRequest<Result<TotalesDeLoteEnDestino>>;

public sealed class GetReceiptsAndTreasuryBatchTotalsQueryHandler(IApplicationDbContext db)
    : MediatR.IRequestHandler<GetReceiptsAndTreasuryBatchTotalsQuery, Result<TotalesDeLoteEnDestino>>
{
    public async Task<Result<TotalesDeLoteEnDestino>> Handle(GetReceiptsAndTreasuryBatchTotalsQuery request, CancellationToken ct)
    {
        var deRecaudo = await db.ReceiptsPostings.AsNoTracking().Where(p => p.BatchPublicId == request.BatchPublicId && p.AccountingDocumentId != null)
            .Select(p => p.AccountingDocumentId!.Value).Distinct().ToListAsync(ct);
        var deTesoreria = await db.TreasuryPostings.AsNoTracking().Where(p => p.BatchPublicId == request.BatchPublicId && p.AccountingDocumentId != null)
            .Select(p => p.AccountingDocumentId!.Value).Distinct().ToListAsync(ct);
        var comprobantes = deRecaudo.Concat(deTesoreria).Distinct().ToList();
        if (comprobantes.Count == 0) return Result.Success(TotalesDeLoteEnDestino.Cero);
        var totales = await db.AccountingDocuments.AsNoTracking().Where(d => comprobantes.Contains(d.Id)).Select(d => new { d.TotalDebit, d.TotalCredit }).ToListAsync(ct);
        return Result.Success(new TotalesDeLoteEnDestino(totales.Sum(t => t.TotalDebit), totales.Sum(t => t.TotalCredit)));
    }
}
