using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Qué confirmar (nuevo). <paramref name="GrupoEsperado"/> nulo = no se compara (la última aprobación y la anulación ya
/// lo resolvieron); <paramref name="PorAprobacion"/> = la reentrada de la última aprobación, en la transacción del
/// aprobador: parte de <c>PendingApproval</c> y no vuelve a pedir aprobación.
/// </summary>
public sealed record PedidoDeConfirmacion(Guid DocumentPublicId, DocumentClassGroup? GrupoEsperado, byte[]? RowVersion = null, bool PorAprobacion = false);

/// <summary>
/// El flujo canónico de confirmación de todas las clases (feature 012, T146; decisiones-transversales §1.3; contracts/api.md
/// §9.3). Lo usan <c>ConfirmInventoryDocumentCommand</c>, <c>VoidInventoryDocumentCommand</c> (para confirmar la
/// anulación) y la fuente de aprobación del documento (la última aprobación reentra aquí): es un servicio y no un envío
/// por <c>ISender</c>, porque un comando reintentable anidado vaciaría el <c>ChangeTracker</c> de afuera.
/// <list type="number">
/// <item>relectura; clase ↔ ruta; alcance; reglas comunes (líneas, campos del tipo, fecha, corte, período, bodega) y de la clase;</item>
/// <item>aprobaciones (<see cref="IMotorDeAprobaciones.EvaluarAsync"/>): con niveles queda <c>PendingApproval</c>, sin
/// número y sin tocar cerrojo ni numerador;</item>
/// <item>guardia fiscal y validación previa, si hay implementación registrada (antes de I2/I4 se omiten);</item>
/// <item>cerrojo en orden canónico, efecto de la clase (o su reversión en una anulación), numeración, sellado del modo
/// de paso, confirmación, mensajes, copia fiscal de la contraparte y <b>un</b> <c>SaveChanges</c>;</item>
/// <item>el aviso de reposición de las salidas (US17, <see cref="Replenishment.AvisoDeReposicionAlConfirmar"/>), que no bloquea.</item>
/// </list>
/// No abre transacción: la pone quien lo llama (<c>TransaccionExplicita</c>, o la de <c>IdempotencyBehavior</c>). (nuevo)
/// </summary>
public sealed class ConfirmacionDeDocumento(
    IApplicationDbContext db,
    IMaestrosDelDocumento maestros,
    IActorActual actorActual,
    IDateTimeService reloj,
    EfectosDeClase efectos,
    IMotorDeAprobaciones motor,
    ICerrojoDeInventario cerrojo,
    Numerador numerador,
    EmisorDeMensajes emisor,
    ILectorDeParametros parametros,
    VistaDeDocumentos vista,
    IEnumerable<IPasoFiscalDeConfirmacion> pasosFiscales,
    IEnumerable<IPasoDeValidacionPrevia> validacionesPrevias,
    Counts.BloqueoPorConteo? bloqueoPorConteo = null,
    Replenishment.AvisoDeReposicionAlConfirmar? avisoDeReposicion = null,
    MensajesDelDocumento? mensajesDelDocumento = null,
    IEnumerable<IAvisoAlConfirmar>? avisosAlConfirmar = null)
{
    private readonly MensajesDelDocumento _mensajes = mensajesDelDocumento ?? new MensajesDelDocumento(db, parametros);

    public async Task<Result<ConfirmationResultDto>> ConfirmarAsync(PedidoDeConfirmacion pedido, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        // ------------------------------------------------------------------------------------------ 1. relectura --
        var documento = pedido.PorAprobacion
            ? await db.InventoryDocuments.Include(d => d.Lines).Include(d => d.DocumentType!).ThenInclude(t => t.Warehouses)
                .FirstOrDefaultAsync(d => d.PublicId == pedido.DocumentPublicId, ct)
            : await vista.BuscarAsync(pedido.DocumentPublicId, pedido.GrupoEsperado, seguir: true, ct);
        if (documento is null) return Falla(InventoryErrors.DocumentNotFound());

        var estadoEsperado = pedido.PorAprobacion ? DocumentStatus.PendingApproval : DocumentStatus.Draft;
        if (documento.Status != estadoEsperado) return Falla(InventoryErrors.NotDraft(documento.Status));
        if (pedido.RowVersion is { Length: > 0 } leida && documento.RowVersion is { Length: > 0 } actual && !leida.AsSpan().SequenceEqual(actual))
            return Falla(Error.StaleRowVersion);

        var tipo = documento.DocumentType ?? await db.InventoryDocumentTypes.Include(t => t.Warehouses).FirstAsync(t => t.Id == documento.DocumentTypeId, ct);
        var clase = ClasesDeDocumento.De(documento.Class);

        InventoryDocument? original = null;
        if (documento.Class == DocumentClass.Voiding)
        {
            original = await db.InventoryDocuments.Include(d => d.Lines).Include(d => d.DocumentType)
                .FirstOrDefaultAsync(d => d.Id == documento.VoidsDocumentId, ct);
            if (original is null) return Falla(InventoryErrors.DocumentNotFound());
        }

        var estrategia = efectos.Para(original?.Class ?? documento.Class);
        if (estrategia.IsFailure) return Falla(estrategia.Error);
        var efecto = estrategia.Value;
        var contexto = new ContextoDeEfecto(documento, tipo, clase, original);

        var hoy = reloj.HoyLocal;
        var bodega = documento.WarehouseId is int b ? (await maestros.BodegasPorIdAsync([b], ct)).FirstOrDefault() : null;
        var corte = await maestros.CorteAsync(ct);
        var comunes = ReglasDelDocumento.Evaluar(documento, tipo, bodega, corte, hoy, original?.Class);
        if (comunes.Count > 0) return Falla(comunes[0]);

        // US11 (T393): un producto de un conteo abierto que bloquea movimientos no se mueve en esa bodega.
        if (bloqueoPorConteo is not null && await bloqueoPorConteo.EvaluarAsync(documento, original?.Class, ct) is { } bloqueado)
            return Falla(bloqueado);

        var reglas = await efecto.ValidarAsync(contexto, ct);
        if (reglas.IsFailure) return Falla(reglas.Error);

        // --------------------------------------------------------------------------------------- 2. aprobación --
        // I3 (T618): la diferencia de arqueo dentro de la tolerancia se confirma sin aprobación y el cajero nunca la aprueba.
        if (!pedido.PorAprobacion && !await efecto.OmiteAprobacionAsync(contexto, ct))
        {
            var excluidos = await efecto.ExcluidosDeLaAprobacionAsync(contexto, ct);
            var grupo = VistaDeDocumentos.GrupoDe(documento.Class, original?.Class);
            var permisoLimitado = PermisosDeGrupo.De(grupo).PermisoLimitado;
            var monto = efecto.MontoParaAprobar(contexto);
            var evaluacion = await motor.EvaluarAsync(ApprovalSubjects.DocumentConfirmation, tipo.PublicId, documento.OperationDate, monto, permisoLimitado, ct);
            if (evaluacion.IsFailure) return Falla(evaluacion.Error);

            if (evaluacion.Value.Evaluacion.RequiereAprobacion)
            {
                var solicitud = await motor.SolicitarAsync(new SolicitudDeAprobacion(
                    ApprovalSubjects.DocumentConfirmation,
                    ApprovalSourceTypes.InventoryDocument,
                    documento.PublicId,
                    $"{tipo.Code} {tipo.Name}",
                    tipo.PublicId,
                    bodega?.PublicId,
                    null,
                    monto,
                    documento.OperationDate,
                    documento.CreatedByUserId,
                    [documento.CreatedByUserId, .. excluidos.Where(u => u != documento.CreatedByUserId)],
                    Huella(documento),
                    permisoLimitado), ct);
                if (solicitud.IsFailure) return Falla(solicitud.Error);

                if (solicitud.Value is { } pendiente)
                {
                    documento.EnviarAAprobacion();
                    await db.SaveChangesAsync(ct);
                    var niveles = pendiente.NivelesRequeridos()
                        .Select(n => new NivelPedidoDto(n.Order, n.Threshold, n.PermissionCode, n.Order == pendiente.CurrentLevel ? "Pending" : "Waiting"))
                        .ToList();
                    return Result.Success(new ConfirmationResultDto(
                        documento.PublicId, documento.Status, null, null, documento.OperationDate, null, null,
                        new AprobacionPedidaDto(pendiente.PublicId, evaluacion.Value.Evaluacion.NivelForzado ? "AmountLimit" : "Policy", niveles),
                        null, null, []));
                }
            }
        }

        // I3 (T655): la aprobación propia de la clase (el crédito provisional), también en la reentrada de la última aprobación del tipo.
        if (original is null)
        {
            var propia = await efecto.AprobacionPropiaAsync(contexto, ct);
            if (propia.IsFailure) return Falla(propia.Error);
            if (propia.Value is { } pendientePropia)
            {
                if (documento.Status == DocumentStatus.Draft) documento.EnviarAAprobacion();
                await db.SaveChangesAsync(ct);
                var nivelesPropios = pendientePropia.NivelesRequeridos()
                    .Select(n => new NivelPedidoDto(n.Order, n.Threshold, n.PermissionCode, n.Order == pendientePropia.CurrentLevel ? "Pending" : "Waiting"))
                    .ToList();
                return Result.Success(new ConfirmationResultDto(
                    documento.PublicId, documento.Status, null, null, documento.OperationDate, null, null,
                    new AprobacionPedidaDto(pendientePropia.PublicId, "Policy", nivelesPropios), null, null, []));
            }
        }

        // ------------------------------------------------------------------ 3. guardia fiscal y validación previa --
        if (clase.IsFiscal)
        {
            foreach (var paso in pasosFiscales)
            {
                var fiscal = await paso.EvaluarAsync(contexto, ct);
                if (fiscal.IsFailure) return Falla(fiscal.Error);
            }
        }
        if (clase.NumberedBy == NumberedBy.DianResolution && !pasosFiscales.Any())
            return Falla(InventoryErrors.DocumentClassNotAvailable(documento.Class));

        // El modo que se sellará: el del original en una anulación, el del origen en un derivado, si no el vigente del tipo.
        var modo = await _mensajes.ModoAsync(contexto, efecto, hoy, ct);
        if (modo.IsFailure) return Falla(modo.Error);
        var origenes = modo.Value.Origenes;

        // T520 (FR-074, T30): con modo distinto de NotPosted, los mensajes se arman una vez con los costos provisionales y se le
        // pregunta a Contabilidad si es contabilizable, fuera del cerrojo. Lo evaluado son los mismos sobres que se emitirán.
        var validacion = new ValidacionPreviaDto(PrevalidationOutcome.NotApplicable, []);
        if (modo.Value.Modo is { } m && m != PostingMode.NotPosted && validacionesPrevias.FirstOrDefault() is { } validador)
        {
            var provisionales = await efecto.MensajesProvisionalesAsync(contexto, ct);
            if (provisionales.Count > 0)
            {
                var origenDeEmision = await _mensajes.OrigenAsync(documento, tipo, bodega?.Code, ct);
                var solicitudes = MensajesDelDocumento.Solicitudes(origenDeEmision, original, origenes, provisionales,
                    new ModoDeEntrega.Sellado(DeliveryMode.Online), PrevalidationOutcome.NotApplicable);
                var sobres = MensajesDelDocumento.Sobres(solicitudes, actor.CentralUserId, actor.Name, new DateTimeOffset(reloj.UtcNow, TimeSpan.Zero));
                var previa = await validador.EvaluarAsync(contexto, sobres, ct);
                if (previa.IsFailure) return Falla(previa.Error);
                validacion = new ValidacionPreviaDto(previa.Value.Outcome, previa.Value.Warnings);
            }
        }

        // ------------------------------------------------------------------ 4. cerrojo, efecto, número, mensajes --
        await cerrojo.BloquearAsync(efecto.Cerrojo(contexto), ct);

        var aplicado = original is null ? await efecto.AplicarAsync(contexto, ct) : await efecto.RevertirAsync(contexto, ct);
        if (aplicado.IsFailure) return Falla(aplicado.Error);

        if (clase.NumberedBy == NumberedBy.Sequence)
        {
            var numerado = await numerador.NumerarAsync(documento, tipo.Code, ct);
            if (numerado.IsFailure) return Falla(numerado.Error);
        }

        documento.PostingMode = modo.Value.Modo;
        documento.Confirmar(usuario, reloj.UtcNow);
        original?.MarcarAnulado(documento.Id);

        var contenidos = original is null ? await efecto.MensajesAsync(contexto, ct) : await efecto.MensajesDeAnulacionAsync(contexto, ct);
        if (contenidos.Count > 0)
        {
            var origenDeEmision = await _mensajes.OrigenAsync(documento, tipo, bodega?.Code, ct);
            var propio = original is null && origenes.Count == 0 ? await _mensajes.ModoDeEntregaAsync(documento, tipo, hoy, ct) : new ModoDeEntrega.Sellado(DeliveryMode.Online);
            foreach (var solicitud in MensajesDelDocumento.Solicitudes(origenDeEmision, original, origenes, contenidos, propio, validacion.Outcome))
                await emisor.EmitirAsync(solicitud, ct);
        }

        if (documento.CounterpartyPersonId is int personaId && !await db.DocumentPartySnapshots.AnyAsync(s => s.DocumentId == documento.Id, ct))
        {
            var persona = await db.People.AsNoTracking().FirstAsync(p => p.Id == personaId, ct);
            db.DocumentPartySnapshots.Add(FotoDeLaContraparte.De(documento, persona));
        }

        await db.SaveChangesAsync(ct);

        // US17 (T953): con el kardex ya escrito, las salidas que dejaron la posición en o bajo el punto de reorden avisan en
        // warnings[] y levantan Inventario.Reorden / Inventario.Quiebre en esta misma transacción. Nunca bloquea.
        IReadOnlyList<AvisoDto> avisos = avisoDeReposicion is null ? [] : await avisoDeReposicion.AvisarAsync(documento, ct);
        // I3 (T611): los avisos de la clase después del guardado (la venta bajo costo con «Alertar»); nunca bloquean.
        foreach (var aviso in avisosAlConfirmar ?? []) avisos = [.. avisos, .. await aviso.AvisarAsync(documento, ct)];
        if (validacion.Outcome == PrevalidationOutcome.NoResponse) avisos = [.. validacion.Warnings, .. avisos];

        var mensajes = await vista.TieneAsync(PermisosDeGrupo.VerMensajes, ct)
            ? (await vista.MensajesAsync(documento.PublicId, ct)).Select(x => new MensajeEmitidoDto(x.MessagePublicId, x.Type, x.Destination, x.DeliveryStatus)).ToList()
            : null;
        return Result.Success(new ConfirmationResultDto(
            documento.PublicId, documento.Status, documento.Number, VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number),
            documento.OperationDate, documento.ConfirmedAt, documento.PostingMode, null, validacion, mensajes, avisos));
    }

    /// <summary>
    /// La huella de lo que se aprueba (<c>ContentSha256</c>): si cambia, la aprobación ya no vale. Tipo, fecha, bodegas,
    /// contraparte y líneas vivas.
    /// </summary>
    public static string Huella(InventoryDocument documento) => HuellaDeOperacion.Calcular("InventoryDocument", new
    {
        documento.PublicId,
        documento.DocumentTypeId,
        documento.OperationDate,
        documento.WarehouseId,
        documento.DestinationWarehouseId,
        documento.CounterpartyPersonId,
        documento.CostCenterId,
        documento.Total,
        documento.CostTotal,
        Lineas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber)
            .Select(l => new { l.LineNumber, l.ProductId, l.UnitId, l.Quantity, l.UnitPrice, l.UnitCost, l.LocationId, l.ToLocationId })
            .ToList(),
    });

    // --------------------------------------------------------------------------------------- modo de paso --

    /// <summary>¿La clase emite algún mensaje de negocio a Contabilidad? (el saldo inicial sólo emite informativos).</summary>
    public static bool EmiteNegocioAContabilidad(DescripcionDeClase clase) =>
        clase.Messages.Any(tipoDeMensaje => CatalogoDeMensajesV1.Todos.Any(t =>
            t.Type == tipoDeMensaje && t.Destination == IntegrationDestinations.Accounting && t.Kind == IntegrationMessageKind.Business));

    private static Result<ConfirmationResultDto> Falla(Error error) => Result.Failure<ConfirmationResultDto>(error);
}

/// <summary>
/// La copia fiscal de la contraparte al confirmar (<c>INV_DocumentPartySnapshots</c> versión 1; T52, FR-011). El tipo de
/// identificación DIAN lo traduce <see cref="CatalogoDian"/> (T180) a la fecha del documento; sin traducción queda vacío y
/// el canónico lo reporta como dato faltante. El perfil tributario es el de <c>COR_People</c> (T172). (nuevo)
/// </summary>
public static class FotoDeLaContraparte
{
    public static DocumentPartySnapshot De(InventoryDocument documento, Person persona)
    {
        var juridica = persona.PersonType == "02" || !string.IsNullOrWhiteSpace(persona.BusinessName);
        var nombre = juridica && !string.IsNullOrWhiteSpace(persona.BusinessName)
            ? persona.BusinessName!
            : string.Join(' ', new[] { persona.FirstName, persona.OtherNames, persona.LastName, persona.SecondLastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return new DocumentPartySnapshot
        {
            DocumentId = documento.Id,
            Version = 1,
            PersonId = persona.Id,
            DianOrganizationType = juridica ? "1" : "2",
            DianIdTypeCode = CatalogoDian.Embebido.TipoDeIdentificacionDe(persona.IdType, documento.OperationDate) ?? string.Empty,
            TaxId = persona.TaxId,
            CheckDigit = persona.TaxIdCheckDigit,
            LegalName = nombre,
            FirstName = juridica ? null : persona.FirstName,
            LastName = juridica ? null : persona.LastName,
            Address = persona.Address,
            MunicipalityDaneCode = persona.DaneCityCode,
            Email = persona.Email,
            Phone = persona.Mobile ?? persona.Phone1,
            IsVatResponsible = persona.IsVatResponsible,
            IsLargeContributor = persona.IsLargeContributor,
            IsSelfWithholder = persona.IsSelfWithholder,
            IsVatWithholdingAgent = persona.IsVatWithholdingAgent,
            IsSimpleTaxRegime = persona.IsSimpleTaxRegime,
            IsIncomeTaxFiler = persona.IsIncomeTaxFiler,
            WithholdingExempt = persona.WithholdingExempt,
            IcaWithholdingExempt = persona.IcaWithholdingExempt,
            CiiuCode = persona.CiiuCode,
        };
    }
}
