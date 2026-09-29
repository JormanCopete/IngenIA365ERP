using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

// ------------------------------------------------------------------------------------------------ entrada --

/// <summary>
/// Un descuento pedido (contracts/api.md §18.2 <c>discount</c> y <c>documentDiscount</c>): <see cref="Percent"/> es <b>fracción</b>
/// (0,05 = 5 %), como en el POS; <see cref="Amount"/> va en pesos, en la base de la lista. (nuevo)
/// </summary>
public sealed record SalesDiscountInput(decimal? Percent = null, decimal? Amount = null);

/// <summary>
/// Una línea del borrador de venta (§18.2). Sin <see cref="UnitPrice"/>, el de la lista; con él, un precio digitado bajo la lista es un
/// descuento que respeta el tope (§19.3). <see cref="LinePublicId"/> en un <c>PUT</c> conserva la línea. (nuevo)
/// </summary>
public sealed record SalesLineInput(
    Guid ProductPublicId,
    Guid UnitPublicId,
    decimal Quantity,
    decimal? UnitPrice = null,
    SalesDiscountInput? Discount = null,
    string? Notes = null,
    Guid? LinePublicId = null,
    int? LineNumber = null);

/// <summary>
/// <c>SalesDraftInput</c> (contracts/api.md §18.2, feature 012, I3, T608): el cuerpo de <c>POST</c> y <c>PUT
/// /api/inventory/sales/invoices</c>. Canal y sucursal no se escriben: salen del tipo y de la bodega. Sin contraparte, el consumidor
/// final. <c>originPublicIds</c> (remisiones) llega con I6. Se guarda por el ciclo común: <see cref="ComoBorrador"/> lo convierte en el
/// <see cref="SaveInventoryDraftRequest"/> de <c>SaveInventoryDraftCommand(…, ExpectedGroup = Sales, …)</c>, y lo propio de la venta
/// lo aplica <see cref="BorradorDeVenta"/>. (nuevo)
/// </summary>
public sealed record SalesDraftInput(
    Guid DocumentTypePublicId,
    Guid WarehousePublicId,
    IReadOnlyList<SalesLineInput> Lines,
    IReadOnlyList<DocumentPaymentInput> Payments,
    DateOnly? OperationDate = null,
    Guid? CounterpartyPersonPublicId = null,
    Guid? SalespersonPublicId = null,
    Guid? CostCenterPublicId = null,
    string? ExternalReference = null,
    DateOnly? DueDate = null,
    string? Notes = null,
    SalesDiscountInput? DocumentDiscount = null,
    IReadOnlyList<Guid>? OriginPublicIds = null,
    byte[]? RowVersion = null)
{
    /// <summary>El borrador del ciclo común con los datos de la venta (<see cref="DatosDeVentaDelBorrador"/>).</summary>
    public SaveInventoryDraftRequest ComoBorrador() => new(
        DocumentTypePublicId,
        OperationDate,
        WarehousePublicId,
        null,
        CostCenterPublicId,
        CounterpartyPersonPublicId,
        ExternalReference,
        null,
        null,
        Notes,
        null,
        null,
        RowVersion,
        (Lines ?? []).Select(l => new SaveInventoryDraftLine(l.LinePublicId, l.ProductPublicId, l.UnitPublicId, l.Quantity,
            UnitPrice: l.UnitPrice, DiscountPercent: l.Discount?.Percent, DiscountAmount: l.Discount?.Amount, Notes: l.Notes)).ToList(),
        Sales: new DatosDeVentaDelBorrador(SalespersonPublicId, DueDate, DocumentDiscount, Payments ?? []));
}

/// <summary>
/// Lo que el borrador de venta agrega al del ciclo común (nuevo): vendedor, vencimiento, descuento por total y pagos. Viaja en
/// <see cref="SaveInventoryDraftRequest.Sales"/>; sólo lo admite el grupo <c>Sales</c>.
/// </summary>
public sealed record DatosDeVentaDelBorrador(
    Guid? SalespersonPublicId,
    DateOnly? DueDate,
    SalesDiscountInput? DocumentDiscount,
    IReadOnlyList<DocumentPaymentInput> Payments)
{
    public static DatosDeVentaDelBorrador Vacio { get; } = new(null, null, null, []);
}

// ---------------------------------------------------------------------------------------------- el grupo --

/// <summary>
/// El borrador de venta de oficina (feature 012, I3, T608; contracts/api.md §18.2; FR-053 a FR-057, T26, T50): lo que el grupo
/// <c>Sales</c> agrega al guardado del ciclo común (<see cref="SaveInventoryDraftCommand"/>). En orden:
/// <list type="bullet">
/// <item>la ruta de facturas admite <c>SalesInvoice</c> y <c>NonElectronicSalesReceipt</c> (otra clase de venta →
/// <c>Inventory.Document.TypeNotForRoute</c>); una venta del POS no se edita por aquí (404);</item>
/// <item>sin contraparte, el consumidor final (<c>ConsumidorFinalSeeder</c>); canal del tipo y sucursal de la bodega los pone el ciclo
/// común;</item>
/// <item>vendedor opcional con la regla de FR-057 —persona con rol vivo en <c>INV_Salespeople</c>—: si no lo es, el borrador se guarda
/// y el <c>issue</c> es <c>Inventory.Sales.SalespersonInvalid</c> (la confirmación lo rechaza);</item>
/// <item>precios, descuentos, impuestos y totales por <see cref="PrecificacionDeVenta"/> (el único que precifica una línea de venta);
/// un descuento sobre el tope pide su aprobación (<see cref="AprobacionDeDescuentos"/>);</item>
/// <item>los pagos (<see cref="DocumentPaymentInput"/>) se guardan como <c>INV_DocumentPayments</c> del borrador con las copias del medio;
/// lo que el validador de pagos diría al confirmar vuelve como <c>issue</c>, salvo un número de tarjeta, que nunca se guarda
/// (<c>Payments.CardNumberNotAllowed</c>);</item>
/// <item>persona inactiva de contado con <c>Ventas.PersonaInactivaDeContado = Bloquear</c> y bajo costo con «Alertar», como
/// <c>issues</c>.</item>
/// </list>
/// Ninguno guarda: el ciclo común hace el <c>SaveChanges</c>. Sólo si el documento es nuevo y lleva descuentos se guarda antes su
/// cabecera, porque la fila del descuento necesita el Id del documento. (nuevo)
/// </summary>
public sealed class BorradorDeVenta(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    PrecificacionDeVenta precificacion,
    AprobacionDeDescuentos aprobaciones,
    IPermissionChecker permisos,
    ILectorDeParametros parametros) : IBorradorDeGrupo
{
    /// <summary>Las clases que admite la ruta de facturas en I3 (§18.2).</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesDeFactura = [DocumentClass.SalesInvoice, DocumentClass.NonElectronicSalesReceipt];

    public DocumentClassGroup Grupo => DocumentClassGroup.Sales;

    public async Task<Result<SaveInventoryDraftRequest>> PrepararAsync(InventoryDocumentType tipo, SaveInventoryDraftRequest pedido, InventoryDocument? existente, CancellationToken ct)
    {
        if (existente?.PointOfSaleId is not null) return Result.Failure<SaveInventoryDraftRequest>(InventoryErrors.DocumentNotFound());
        if (!ClasesDeFactura.Contains(tipo.Class)) return Result.Failure<SaveInventoryDraftRequest>(InventoryErrors.TypeNotForRoute(tipo.Class, DocumentClassGroup.Sales));
        if (pedido.Contraparte is not null) return Result.Success(pedido);

        var fecha = pedido.OperationDate ?? existente?.OperationDate ?? reloj.HoyLocal;
        var final = await ConsumidorFinalAsync(db, fecha, ct);
        return Result.Success(final is { } c ? pedido with { CounterpartyPersonPublicId = c } : pedido);
    }

    public async Task<Result<ResultadoDelBorrador>> AplicarAsync(BorradorEnCurso borrador, CancellationToken ct)
    {
        var documento = borrador.Documento;
        var pedido = borrador.Pedido;
        var datos = pedido.Sales ?? DatosDeVentaDelBorrador.Vacio;
        var actor = await actorActual.ObtenerAsync(ct);
        var avisos = new List<Error>();

        // Vendedor (FR-057): se guarda aunque no esté vivo; la confirmación lo rechaza.
        documento.SalespersonId = null;
        if (datos.SalespersonPublicId is { } vendedor)
        {
            var fila = await db.Salespeople.IgnoreQueryFilters().AsNoTracking().Where(s => s.PublicId == vendedor)
                .Select(s => new { s.Id, Vivo = !s.IsDeleted && !s.Person.IsDeleted }).FirstOrDefaultAsync(ct);
            if (fila is not null) documento.SalespersonId = fila.Id;
            if (fila is not { Vivo: true }) avisos.Add(ErroresDelPos.SalespersonInvalid());
        }
        documento.DueDate = datos.DueDate;

        // Precios, descuentos, impuestos y totales.
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        IReadOnlyList<DocumentTaxLineDto> previstos = [];
        if (vivas.Count == 0)
        {
            CalculoTributarioDeVenta.AplicarTotales(documento, new TotalesDeVenta(0m, 0m, 0m, 0m, 0m, 0m));
        }
        else
        {
            var municipio = await db.Branches.AsNoTracking().Where(b => b.Id == documento.BranchId).Select(b => b.MunicipalityDaneCode).FirstOrDefaultAsync(ct);
            var lineas = vivas.Select(l =>
            {
                var p = l.LineNumber - 1 < pedido.Lines.Count ? pedido.Lines[l.LineNumber - 1] : null;
                return new LineaAPrecificar(l.LineNumber, l.ProductId, l.UnitId, l.Quantity, l.QuantityBase, p?.UnitPrice, p?.DiscountPercent, p?.DiscountAmount);
            }).ToList();
            var pedidoDePrecio = new PedidoDePrecificacion(documento.OperationDate, documento.CounterpartyPersonId, documento.SalesChannelId,
                documento.BranchId, documento.WarehouseId, actor.UserId, lineas, datos.DocumentDiscount?.Amount, municipio);
            if (datos.DocumentDiscount?.Percent is { } pct && pct > 0m)
            {
                var previa = await precificacion.PrecificarAsync(pedidoDePrecio, ct);
                if (previa.IsFailure) return Result.Failure<ResultadoDelBorrador>(previa.Error);
                // Sobre las líneas sin promoción: sólo a ellas se prorratea el descuento por total (I6, F9).
                pedidoDePrecio = pedidoDePrecio with
                {
                    DescuentoPorTotal = Math.Round(previa.Value.Lineas.Where(l => l.Descuentos.All(d => d.Source != DiscountSource.Promotion)).Sum(l => l.NetAmount) * pct,
                        2, MidpointRounding.AwayFromZero),
                };
            }
            if (pedidoDePrecio.DescuentoPorTotal is <= 0m) pedidoDePrecio = pedidoDePrecio with { DescuentoPorTotal = null };
            var precificada = await precificacion.PrecificarAsync(pedidoDePrecio, ct);
            if (precificada.IsFailure) return Result.Failure<ResultadoDelBorrador>(precificada.Error);

            // La fila del descuento no tiene navegación al documento: uno nuevo se guarda antes para tener su Id.
            if (documento.Id == 0 && precificada.Value.Lineas.Any(l => l.Descuentos.Count > 0)) await db.SaveChangesAsync(ct);

            var ids = vivas.Where(l => l.Id != 0).Select(l => l.Id).Concat(documento.Lines.Where(l => l.IsDeleted && l.Id != 0).Select(l => l.Id)).ToList();
            var filas = ids.Count == 0 ? [] : await db.DocumentLineDiscounts.Where(d => ids.Contains(d.DocumentLineId) && !d.IsDeleted).ToListAsync(ct);
            var pares = new List<(InventoryDocumentLine, DocumentLineDiscount)>();
            foreach (var l in vivas)
            {
                var viejas = l.Id == 0 ? [] : filas.Where(f => f.DocumentLineId == l.Id).ToList();
                var nuevas = PrecificacionDeVenta.AplicarALinea(documento, l, precificada.Value.Lineas.Single(x => x.LineNumber == l.LineNumber), viejas);
                foreach (var vieja in viejas)
                {
                    vieja.DeletedAt = reloj.UtcNow;
                    pares.Add((l, vieja));
                }
                foreach (var nueva in nuevas)
                {
                    db.DocumentLineDiscounts.Add(nueva);
                    pares.Add((l, nueva));
                }
                if (precificada.Value.Lineas.Single(x => x.LineNumber == l.LineNumber).BelowCost)
                    avisos.Add(new ErrorConDatos(ErroresDePrecios.BelowCostCode, $"La línea {l.LineNumber} se vende por debajo de su costo promedio.", new { lineNumber = l.LineNumber }));
            }
            foreach (var huerfana in filas.Where(f => documento.Lines.Any(l => l.IsDeleted && l.Id == f.DocumentLineId)))
            {
                huerfana.IsDeleted = true;
                huerfana.DeletedAt = reloj.UtcNow;
            }
            CalculoTributarioDeVenta.AplicarTotales(documento, precificada.Value.Totales);
            previstos = precificada.Value.Renglones.Select(r => new DocumentTaxLineDto(r.Linea, r.Kind, r.TaxRateCode, r.Rate, r.AmountPerUnit,
                r.Base, r.Amount, r.Treatment, r.MunicipalityDaneCode, JsonSerializer.Serialize(r.Explicacion))).ToList();

            if (pares.Any(p => p.Item2.RequiresApproval))
            {
                var pedidas = await aprobaciones.SolicitarAsync(documento, pares, null, ct);
                if (pedidas.IsFailure) return Result.Failure<ResultadoDelBorrador>(pedidas.Error);
            }
        }

        // Pagos del borrador.
        var pagos = await GuardarPagosAsync(documento, datos.Payments, actor.UserId, ct);
        if (pagos.IsFailure) return Result.Failure<ResultadoDelBorrador>(pagos.Error);
        avisos.AddRange(pagos.Value);

        // Persona inactiva de contado.
        if (await PersonaInactivaBloqueadaAsync(db, parametros, documento, ct) is { } inactiva)
            avisos.Add(inactiva);

        return Result.Success(new ResultadoDelBorrador(avisos, previstos));
    }

    public Task<Error?> TraducirColisionAsync(DbUpdateException ex, BorradorEnCurso borrador, CancellationToken ct) => Task.FromResult<Error?>(null);

    /// <summary>
    /// Reemplaza los pagos del borrador: los anteriores quedan de baja lógica y los nuevos llevan las copias del medio. Devuelve lo que
    /// el validador diría hoy; falla sólo si el medio no existe o si algún campo parece un número de tarjeta (nunca se guarda).
    /// </summary>
    private async Task<Result<IReadOnlyList<Error>>> GuardarPagosAsync(InventoryDocument documento, IReadOnlyList<DocumentPaymentInput> entradas, int? usuario,
        CancellationToken ct)
    {
        if (documento.Id != 0)
        {
            foreach (var previo in await db.DocumentPayments.Where(p => p.DocumentId == documento.Id && !p.IsDeleted).ToListAsync(ct))
            {
                previo.IsDeleted = true;
                previo.DeletedAt = reloj.UtcNow;
            }
        }
        if (entradas.Count == 0) return Result.Success<IReadOnlyList<Error>>([]);

        var ids = entradas.Select(e => e.PaymentMeansPublicId).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().Include(m => m.CardNetwork).Include(m => m.CardAcquirer)
            .Where(m => ids.Contains(m.PublicId)).ToDictionaryAsync(m => m.PublicId, ct);
        for (var i = 0; i < entradas.Count; i++)
        {
            if (!medios.ContainsKey(entradas[i].PaymentMeansPublicId))
                return Result.Failure<IReadOnlyList<Error>>(RegistroDePagos.ErrorDePago(new ErrorDePago(DisponibilidadDeMedio.MeansNotAvailable, i,
                    new Dictionary<string, object?> { ["paymentMeansCode"] = null })));
            var e = entradas[i];
            if (new[] { e.Reference, e.AuthorizationCode, e.BatchNumber, e.Last4 }.Any(ValidadorDePagos.PareceNumeroDeTarjeta))
                return Result.Failure<IReadOnlyList<Error>>(RegistroDePagos.ErrorDePago(new ErrorDePago(ValidadorDePagos.CardNumberNotAllowed, i,
                    new Dictionary<string, object?> { ["paymentMeansCode"] = medios[e.PaymentMeansPublicId].Code })));
        }

        var abiertas = usuario is int u
            ? await db.CashSessions.AsNoTracking().Where(s => s.CashierUserId == u && s.Status == CashSessionStatus.Open)
                .Select(s => new { s.Id, s.PublicId }).ToListAsync(ct)
            : [];
        var terminalIds = entradas.Select(e => e.CardTerminalPublicId).OfType<Guid>().Distinct().ToList();
        var terminales = await db.CardTerminals.AsNoTracking().Where(t => terminalIds.Contains(t.PublicId)).ToDictionaryAsync(t => t.PublicId, t => t.Id, ct);

        var propuestos = new List<PagoPropuesto>(entradas.Count);
        for (var i = 0; i < entradas.Count; i++)
        {
            var e = entradas[i];
            var m = medios[e.PaymentMeansPublicId];
            int? sesion = null;
            if (m.CountMethod != CashCountMethod.None)
                sesion = e.CashSessionPublicId is { } s ? abiertas.FirstOrDefault(a => a.PublicId == s)?.Id : abiertas.Count == 1 ? abiertas[0].Id : null;
            var pago = new DocumentPayment
            {
                Document = documento,
                DocumentId = documento.Id,
                LineNumber = (short)(i + 1),
                Direction = PaymentDirection.Received,
                Amount = e.Amount,
                AmountTendered = e.Tendered,
                ChangeGiven = e.Tendered is { } entregado && entregado > e.Amount ? entregado - e.Amount : null,
                Reference = string.IsNullOrWhiteSpace(e.Reference) ? null : e.Reference.Trim(),
                NormalizedReference = m.UniqueReference ? ValidadorDePagos.NormalizarReferencia(e.Reference) : null,
                AuthorizationCode = string.IsNullOrWhiteSpace(e.AuthorizationCode) ? null : e.AuthorizationCode.Trim(),
                CardTerminalId = e.CardTerminalPublicId is { } t && terminales.TryGetValue(t, out var tid) ? tid : null,
                TerminalBatchNumber = string.IsNullOrWhiteSpace(e.BatchNumber) ? null : e.BatchNumber.Trim(),
                Last4 = e.Last4,
                CashSessionId = sesion,
            };
            pago.CopiarDelMedio(m);
            if (ClasesDeMedio.EsCredito(m.Class) && e.Credit is { } c)
            {
                pago.InstallmentCount = c.Installments;
                pago.CreditTermDays = c.TermDays;
                pago.InstallmentPeriodDays = c.PeriodicityDays;
                pago.FirstDueDate = c.FirstDueDate;
                pago.FinalDueDate = documento.OperationDate.AddDays(c.TermDays);
                if (!string.IsNullOrWhiteSpace(c.SuggestedLineCode)) pago.SuggestedCreditLineCode = c.SuggestedLineCode.Trim();
            }
            db.DocumentPayments.Add(pago);
            propuestos.Add(ReglasDeConfirmacionDeVenta.Propuesto(pago, m));
        }

        // Lo que diría la confirmación: el validador puro y la disponibilidad de cada medio.
        var validado = ValidadorDePagos.Validar(new PedidoDeCobro(documento.AmountDue, propuestos, abiertas.Select(a => a.Id).ToList()));
        var issues = validado.Errors.Select(RegistroDePagos.ErrorDePago).ToList();
        var disponibilidad = await ReglasDeConfirmacionDeVenta.MedioNoDisponibleAsync(db, permisos, documento, medios.Values.ToList(),
            await EsConsumidorFinalAsync(db, documento, ct), ct);
        if (disponibilidad is not null) issues.Add(disponibilidad);
        return Result.Success<IReadOnlyList<Error>>(issues);
    }

    // ------------------------------------------------------------------------------------------------ apoyo --

    /// <summary>El <c>PublicId</c> del consumidor final del maestro a la fecha (Res. 202/2025).</summary>
    public static async Task<Guid?> ConsumidorFinalAsync(IApplicationDbContext db, DateOnly fecha, CancellationToken ct)
    {
        var numero = CatalogoDian.Embebido.ConsumidorFinal(fecha)?.Numero;
        if (numero is null) return null;
        return await db.People.AsNoTracking().Where(p => p.TaxId == numero && !p.IsDeleted).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct);
    }

    /// <summary>¿El comprador es el consumidor final (sin contraparte o la genérica)?</summary>
    public static async Task<bool> EsConsumidorFinalAsync(IApplicationDbContext db, InventoryDocument documento, CancellationToken ct)
    {
        if (documento.CounterpartyPersonId is not int persona) return true;
        var numero = CatalogoDian.Embebido.ConsumidorFinal(documento.OperationDate)?.Numero;
        return numero is not null && await db.People.AsNoTracking().AnyAsync(p => p.Id == persona && p.TaxId == numero, ct);
    }

    /// <summary>
    /// <c>Inventory.Sales.PersonInactive</c> si la contraparte está inactiva (<c>Status = I</c>), la venta es de contado (ningún pago de
    /// clase crédito) y <c>Ventas.PersonaInactivaDeContado = Bloquear</c>; nulo en otro caso. A crédito lo decide el crédito (§23).
    /// </summary>
    public static async Task<Error?> PersonaInactivaBloqueadaAsync(IApplicationDbContext db, ILectorDeParametros parametros, InventoryDocument documento,
        CancellationToken ct)
    {
        if (documento.CounterpartyPersonId is not int personaId) return null;
        var persona = await db.People.AsNoTracking().Where(p => p.Id == personaId).Select(p => new { p.Status, p.BusinessName, p.FirstName, p.LastName }).FirstOrDefaultAsync(ct);
        if (persona is null || !string.Equals(persona.Status, "I", StringComparison.OrdinalIgnoreCase)) return null;
        var aCredito = db.DocumentPayments.Local.Any(p => p.Document == documento && !p.IsDeleted && ClasesDeMedio.EsCredito(p.MeansClass))
            || (documento.Id != 0 && await db.DocumentPayments.AsNoTracking().AnyAsync(p => p.DocumentId == documento.Id && !p.IsDeleted
                && (p.MeansClass == PaymentMeansClass.AssociateCredit || p.MeansClass == PaymentMeansClass.CustomerCredit), ct));
        if (aCredito) return null;
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.VentasPersonaInactivaDeContado, documento.OperationDate, ct: ct);
        var politica = leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto : "Permitir";
        if (!string.Equals(politica, "Bloquear", StringComparison.OrdinalIgnoreCase)) return null;
        var nombre = !string.IsNullOrWhiteSpace(persona.BusinessName) ? persona.BusinessName! : $"{persona.FirstName} {persona.LastName}".Trim();
        return ErroresDeVentas.PersonInactive(nombre);
    }
}
