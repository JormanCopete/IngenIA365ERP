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
/// de paso, confirmación, mensajes, copia fiscal de la contraparte y <b>un</b> <c>SaveChanges</c>.</item>
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
    IEnumerable<IPasoDeValidacionPrevia> validacionesPrevias)
{
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
        var comunes = ReglasDelDocumento.Evaluar(documento, tipo, bodega, corte, hoy);
        if (comunes.Count > 0) return Falla(comunes[0]);

        var reglas = await efecto.ValidarAsync(contexto, ct);
        if (reglas.IsFailure) return Falla(reglas.Error);

        // --------------------------------------------------------------------------------------- 2. aprobación --
        if (!pedido.PorAprobacion)
        {
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
                    [documento.CreatedByUserId],
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

        // Los relacionados (la anulación) no leen el parámetro: copian el modo de su original (FR-079); los derivados (la
        // factura o la devolución contra sus recepciones, US9) copian el de su origen (data-model §5.3).
        var origenes = original is null ? await efecto.OrigenesDelModoAsync(contexto, ct) : [];
        var modo = original is not null
            ? Result.Success(original.PostingMode)
            : origenes.Count > 0
                ? Result.Success(origenes[0].PostingMode)
                : await ModoASellarAsync(documento, tipo, clase, hoy, ct);
        if (modo.IsFailure) return Falla(modo.Error);

        var validacion = new ValidacionPreviaDto(PrevalidationOutcome.NotApplicable, []);
        if (modo.Value is { } m && m != PostingMode.NotPosted && validacionesPrevias.FirstOrDefault() is { } validador)
        {
            var previos = original is null ? await efecto.MensajesAsync(contexto, ct) : await efecto.MensajesDeAnulacionAsync(contexto, ct);
            var previa = await validador.EvaluarAsync(contexto, previos, ct);
            if (previa.IsFailure) return Falla(previa.Error);
            validacion = new ValidacionPreviaDto(previa.Value.Outcome, previa.Value.Warnings);
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

        documento.PostingMode = modo.Value;
        documento.Confirmar(usuario, reloj.UtcNow);
        original?.MarcarAnulado(documento.Id);

        var contenidos = original is null ? await efecto.MensajesAsync(contexto, ct) : await efecto.MensajesDeAnulacionAsync(contexto, ct);
        if (contenidos.Count > 0)
            await EmitirAsync(documento, tipo, bodega, original, contenidos, validacion.Outcome, hoy, ct, origenes);

        if (documento.CounterpartyPersonId is int personaId && !await db.DocumentPartySnapshots.AnyAsync(s => s.DocumentId == documento.Id, ct))
        {
            var persona = await db.People.AsNoTracking().FirstAsync(p => p.Id == personaId, ct);
            db.DocumentPartySnapshots.Add(FotoDeLaContraparte.De(documento, persona));
        }

        await db.SaveChangesAsync(ct);

        var mensajes = await vista.TieneAsync(PermisosDeGrupo.VerMensajes, ct)
            ? (await vista.MensajesAsync(documento.PublicId, ct)).Select(x => new MensajeEmitidoDto(x.MessagePublicId, x.Type, x.Destination, x.DeliveryStatus)).ToList()
            : null;
        return Result.Success(new ConfirmationResultDto(
            documento.PublicId, documento.Status, documento.Number, VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number),
            documento.OperationDate, documento.ConfirmedAt, documento.PostingMode, null, validacion, mensajes, []));
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

    /// <summary>
    /// El modo a sellar (data-model §5.3): <c>Contabilidad.ModoDePaso</c> vigente a la fecha de confirmación, del tipo si
    /// tiene excepción o el general, en toda clase que emite mensajes de negocio a Contabilidad; nulo en las demás.
    /// </summary>
    private async Task<Result<PostingMode?>> ModoASellarAsync(InventoryDocument documento, InventoryDocumentType tipo, DescripcionDeClase clase, DateOnly hoy, CancellationToken ct)
    {
        if (!EmiteNegocioAContabilidad(clase)) return Result.Success<PostingMode?>(null);

        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, hoy,
            ParameterScopeKind.DocumentType, tipo.Id, ct);
        if (leido.IsFailure) return Result.Failure<PostingMode?>(leido.Error);
        return Result.Success<PostingMode?>(leido.Value.Texto switch
        {
            "PorLotes" => PostingMode.Batch,
            "NoPasa" => PostingMode.NotPosted,
            _ => PostingMode.Online,
        });
    }

    /// <summary>¿La clase emite algún mensaje de negocio a Contabilidad? (el saldo inicial sólo emite informativos).</summary>
    public static bool EmiteNegocioAContabilidad(DescripcionDeClase clase) =>
        clase.Messages.Any(tipoDeMensaje => CatalogoDeMensajesV1.Todos.Any(t =>
            t.Type == tipoDeMensaje && t.Destination == IntegrationDestinations.Accounting && t.Kind == IntegrationMessageKind.Business));

    private async Task EmitirAsync(
        InventoryDocument documento,
        InventoryDocumentType tipo,
        BodegaDelDocumento? bodega,
        InventoryDocument? original,
        IReadOnlyList<object> contenidos,
        PrevalidationOutcome validacion,
        DateOnly hoy,
        CancellationToken ct,
        IReadOnlyList<InventoryDocument>? origenes = null)
    {
        var sucursal = await db.Branches.AsNoTracking().Where(s => s.Id == documento.BranchId).Select(s => s.PublicId).FirstAsync(ct);
        Guid? centro = documento.CostCenterId is int cc ? await db.CostCenters.AsNoTracking().Where(c => c.Id == cc).Select(c => c.PublicId).FirstAsync(ct) : null;
        Guid? persona = documento.CounterpartyPersonId is int p ? await db.People.AsNoTracking().Where(x => x.Id == p).Select(x => x.PublicId).FirstAsync(ct) : null;

        var origen = new OrigenDeEmision(
            MessageOriginKind.Document, documento.PublicId, documento.Class.ToString(), tipo.Code,
            VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number) ?? string.Empty,
            documento.OperationDate, sucursal, centro, bodega?.Code, persona);

        // Cada parte de AjusteDeCostoReconocido es su propia unidad (contracts/mensajes.md §9, §10.1): su clave es
        // Confirmation:{afectado:N}, su relacionado es el documento afectado y sigue el destino del mensaje de ése (US2, T252).
        var ajustesDeCosto = contenidos.OfType<AjusteDeCostoReconocidoV1>().ToList();
        var delEvento = contenidos.Where(c => c is not AjusteDeCostoReconocidoV1).ToList();

        if (delEvento.Count > 0)
        {
            SolicitudDeEmision solicitud;
            if (original is null && origenes is { Count: > 0 })
            {
                // Derivado: sigue el destino del mensaje de su origen (FR-075) y depende de las cadenas de todos sus orígenes.
                var raiz = origenes[0];
                solicitud = new SolicitudDeEmision(origen, ClavesDeEvento.Confirmacion, delEvento, new ModoDeEntrega.Heredado(raiz.PublicId),
                    CadenasDeLasQueDepende: origenes.Select(o => o.PublicId).ToList(),
                    Relacionado: new DocumentoRelacionado(raiz.PublicId, raiz.Class.ToString(), VistaDeDocumentos.NumeroVisible(raiz.Prefix, raiz.Number) ?? string.Empty),
                    ValidacionPrevia: validacion);
            }
            else if (original is null)
            {
                solicitud = new SolicitudDeEmision(origen, ClavesDeEvento.Confirmacion, delEvento, await ModoDeEntregaAsync(documento, tipo, hoy, ct),
                    ValidacionPrevia: validacion);
            }
            else
            {
                var informativo = !EmiteNegocioAContabilidad(ClasesDeDocumento.De(original.Class));
                solicitud = new SolicitudDeEmision(origen, ClavesDeEvento.Confirmacion, delEvento,
                    new ModoDeEntrega.Heredado(original.PublicId),
                    Relacionado: new DocumentoRelacionado(original.PublicId, original.Class.ToString(),
                        VistaDeDocumentos.NumeroVisible(original.Prefix, original.Number) ?? string.Empty),
                    ValidacionPrevia: validacion,
                    KindDelOriginal: informativo ? IntegrationMessageKind.Informational : IntegrationMessageKind.Business);
            }
            await emisor.EmitirAsync(solicitud, ct);
        }

        foreach (var ajuste in ajustesDeCosto)
        {
            var afectado = ajuste.AffectedDocument;
            await emisor.EmitirAsync(new SolicitudDeEmision(origen, ClavesDeEvento.ConfirmacionPor(afectado.PublicId), [ajuste],
                new ModoDeEntrega.Heredado(afectado.PublicId),
                CadenasDeLasQueDepende: [afectado.PublicId],
                Relacionado: new DocumentoRelacionado(afectado.PublicId, afectado.DocumentClass.ToString(), afectado.Number),
                ValidacionPrevia: validacion), ct);
        }
    }

    /// <summary>El modo sellado como entrega: por lotes, con su horario (<see cref="ClavesDeLote.Horario"/>).</summary>
    private async Task<ModoDeEntrega> ModoDeEntregaAsync(InventoryDocument documento, InventoryDocumentType tipo, DateOnly hoy, CancellationToken ct)
    {
        switch (documento.PostingMode)
        {
            case PostingMode.Batch:
                var disparador = await TextoAsync(ParametrosDeInventario.ContabilidadDisparadorDeLote, "HoraDiaria");
                var granularidad = await TextoAsync(ParametrosDeInventario.ContabilidadGranularidad, "PorDocumento");
                var hora = await TextoAsync(ParametrosDeInventario.ContabilidadHoraDeLote, string.Empty);
                TimeOnly? horaDeLote = disparador == "HoraDiaria" && TimeOnly.TryParse(hora, System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : null;
                return new ModoDeEntrega.Sellado(DeliveryMode.Batch, ClavesDeLote.Horario(tipo.Code, disparador, horaDeLote, granularidad));
            case PostingMode.NotPosted:
                return new ModoDeEntrega.Sellado(DeliveryMode.NotPosted);
            default:
                return new ModoDeEntrega.Sellado(DeliveryMode.Online);
        }

        async Task<string> TextoAsync(string clave, string defecto)
        {
            var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, hoy, ParameterScopeKind.DocumentType, tipo.Id, ct);
            return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto : defecto;
        }
    }

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
