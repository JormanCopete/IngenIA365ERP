using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// El único que escribe la bandeja de salida (feature 012, T7, T9, T078; FR-071; contracts/mensajes.md §9–§11).
/// Agrega a la unidad de trabajo del documento el mensaje, sus entregas por destino y sus dependencias, y
/// <b>nunca</b> llama a <c>SaveChanges</c>: los mensajes nacen en el mismo guardado que confirma el documento (paso 9
/// del flujo canónico, molde <c>AccountingPoster</c>). No hay documento confirmado sin sus mensajes ni mensaje sin
/// documento.
///
/// <para>Lo que hace con cada solicitud:</para>
/// <list type="number">
///   <item>busca cada contenido en el catálogo agregado (<see cref="CatalogoDeMensajes"/>: tipo, versión, módulo, destinos y
///         <c>Kind</c>), comprueba que la <c>originEventKey</c> tenga la forma que el tipo admite (§10.1), que todos los contenidos
///         sean del mismo módulo y que el destino sea uno de los que el tipo admite (feature 013, T10). El módulo de origen lo
///         da el tipo, nunca la solicitud;</item>
///   <item>rechaza la doble emisión de <c>(OriginPublicId, Type, OriginEventKey)</c>, en la unidad de trabajo y en la
///         base; el índice único de <c>COR_IntegrationMessages</c> es la última defensa (§10.2);</item>
///   <item>resuelve el usuario de origen en el orden de T11 (feature 013): la persona que actúa (<see cref="IActorActual"/>:
///         quien confirmó u ordenó; con aprobación, el último aprobador); si actúa el proceso, el
///         <see cref="SolicitudDeEmision.UsuarioDeOrigen"/> (el del mensaje consumido o el de quien ordenó la corrida); si
///         tampoco, el proceso mismo (<c>OriginActorKind = Process</c>, sin identidad central, «Proceso de integración»),
///         <b>sólo</b> si todos los tipos lo admiten (<c>AdmiteProceso</c>). Si no, lanza y no agrega nada: para Inventario
///         el mensaje lo origina la persona. Es un <b>dato</b>, nunca un actor;</item>
///   <item>resuelve el modo: el sellado, o el de la entrega a Contabilidad del original, del que un relacionado copia
///         <c>Mode</c> y <c>ScheduleKey</c> <b>sin leer el parámetro</b> (FR-075, FR-079);</item>
///   <item>serializa cada contenido con <see cref="OpcionesDeMensajes"/> y calcula <c>PayloadSha256</c> sobre esos
///         mismos bytes;</item>
///   <item>crea una entrega por destino con el estado inicial de §11 y las aristas hacia el último mensaje de cada
///         cadena de la que depende (§9).</item>
/// </list>
///
/// <para>
/// <b>Qué es «el último mensaje de una cadena».</b> La cadena de un documento son los mensajes cuyo origen es él. El
/// nuevo apunta al último de cada cadena y, además, al último de esa cadena que tiene entrega al mismo destino que el
/// nuevo: la elegibilidad sólo mira dependencias del mismo destino (§9), y sin esa segunda arista un
/// <c>AjusteDeVentaACredito</c> no esperaría a su <c>VentaACreditoRegistrada</c> si el último del original fuera un
/// mensaje a Contabilidad. Los contenidos de una misma solicitud no dependen entre sí: son una unidad.
/// </para>
///
/// <para>
/// Es <c>Scoped</c> y recuerda lo que emitió en su ámbito: un documento que emite dos eventos en el mismo guardado
/// (una venta y su crédito) o un relacionado confirmado en la misma transacción que su original los ve aunque todavía
/// no estén en la base. El <c>Id</c> que la base les asigne sigue el orden en que se emitieron, así que siempre
/// <c>DependsOnMessageId &lt; MessageId</c>.
/// </para>
/// </summary>
public sealed class EmisorDeMensajes(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj)
{
    public const string Moneda = "COP";

    private readonly List<Emitido> _emitidos = [];

    private sealed record Emitido(IntegrationMessage Mensaje, IReadOnlyList<IntegrationMessageDelivery> Entregas);

    /// <summary>
    /// Agrega los mensajes de un evento a la unidad de trabajo y los devuelve en el orden en que se emitieron. No
    /// guarda. Una solicitud mal formada es un defecto del emisor, no del usuario: responde con excepción.
    /// </summary>
    /// <exception cref="ArgumentException">Contenido fuera del catálogo, clave de evento inválida, modo incompleto.</exception>
    /// <exception cref="InvalidOperationException">
    /// Doble emisión, proceso en un tipo que no lo admite, persona sin identidad central, u original sin entrega a Contabilidad
    /// que heredar.
    /// </exception>
    public async Task<IReadOnlyList<IntegrationMessage>> EmitirAsync(SolicitudDeEmision solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var (modulo, contenidos) = Clasificar(solicitud);
        var usuario = await ResolverUsuarioDeOrigenAsync(solicitud, contenidos, ct);

        await RechazarDobleEmisionAsync(solicitud, contenidos, ct);

        var necesitaModo = contenidos.Any(c => LlevaModo(c.Destino, c.Kind));
        var (raiz, modo) = await ResolverModoAsync(solicitud, necesitaModo, ct);
        if (solicitud.CadenaDeOrden is { } cadenaDeOrden && cadenaDeOrden != Guid.Empty) raiz = cadenaDeOrden;
        var objetivos = await ObjetivosAsync(solicitud, contenidos.Select(c => c.Destino).Distinct().ToList(), ct);

        var ahora = reloj.UtcNow;
        var origen = solicitud.Origen;
        var resultado = new List<IntegrationMessage>(contenidos.Count);

        foreach (var contenido in contenidos)
        {
            var bytes = OpcionesDeMensajes.Serializar(contenido.Record);
            var mensaje = new IntegrationMessage
            {
                Type = contenido.Tipo.Type,
                Version = (short)contenido.Tipo.Version,
                Kind = contenido.Kind,
                OriginModule = modulo,
                OriginKind = origen.Kind,
                OriginPublicId = origen.PublicId,
                OriginDocumentClass = origen.DocumentClass,
                OriginDocumentTypeCode = origen.DocumentTypeCode,
                OriginNumber = origen.Number,
                OriginEventKey = solicitud.OriginEventKey,
                FiscalUniqueCode = origen.FiscalUniqueCode,
                RelatedPublicId = solicitud.Relacionado?.PublicId,
                RelatedDocumentClass = solicitud.Relacionado?.DocumentClass,
                RelatedNumber = solicitud.Relacionado?.Number,
                ChainRootPublicId = raiz,
                OperationDate = origen.OperationDate,
                BranchPublicId = origen.BranchPublicId,
                CostCenterPublicId = origen.CostCenterPublicId,
                WarehouseCode = origen.WarehouseCode,
                PersonPublicId = origen.PersonPublicId,
                Currency = Moneda,
                ExchangeRate = 1m,
                PayloadJson = Encoding.UTF8.GetString(bytes),
                PayloadSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                PrevalidationOutcome = solicitud.ValidacionPrevia,
                OriginActorKind = usuario.Kind,
                OriginUserCentralId = usuario.CentralUserId,
                OriginUserName = usuario.Nombre,
                EmittedAt = ahora,
            };
            db.IntegrationMessages.Add(mensaje);

            // Feature 013 (T0653, C01, C06): en una unidad de Cartera cada contenido sella la clave del principal con su propio código corto.
            var modoDelContenido = modo is { ScheduleKey: { } clave } && contenido.Tipo.CodigoCorto is { Length: > 0 } corto
                ? modo with { ScheduleKey = ClavesDeLote.ConCodigoCortoDeCartera(clave, corto) }
                : modo;
            var entrega = NuevaEntrega(mensaje, contenido.Destino, contenido.Kind, modoDelContenido, ahora);
            db.IntegrationMessageDeliveries.Add(entrega);

            foreach (var objetivo in objetivos)
                db.IntegrationMessageDependencies.Add(new IntegrationMessageDependency { Message = mensaje, DependsOnMessage = objetivo });

            _emitidos.Add(new Emitido(mensaje, [entrega]));
            resultado.Add(mensaje);
        }

        return resultado;
    }

    // ------------------------------------------------------------------------------------ validación --

    private sealed record Contenido(object Record, TipoDeMensaje Tipo, string Destino, IntegrationMessageKind Kind);

    private static (string Modulo, List<Contenido> Contenidos) Clasificar(SolicitudDeEmision solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud.Origen);
        ArgumentNullException.ThrowIfNull(solicitud.Modo);
        if (solicitud.Contenidos is not { Count: > 0 })
            throw new ArgumentException("Una emisión lleva al menos un contenido.", nameof(solicitud));

        var origen = solicitud.Origen;
        if (origen.PublicId == Guid.Empty || origen.BranchPublicId == Guid.Empty)
            throw new ArgumentException("El origen necesita su PublicId y la sucursal contable.", nameof(solicitud));
        if (origen.Kind == MessageOriginKind.Document &&
            (string.IsNullOrWhiteSpace(origen.DocumentClass) || string.IsNullOrWhiteSpace(origen.DocumentTypeCode)))
        {
            throw new ArgumentException("Un origen Document lleva su clase y su tipo de documento.", nameof(solicitud));
        }

        var lista = new List<Contenido>(solicitud.Contenidos.Count);
        foreach (var record in solicitud.Contenidos)
        {
            ArgumentNullException.ThrowIfNull(record);
            var tipo = CatalogoDeMensajes.Buscar(record.GetType())
                ?? throw new ArgumentException($"«{record.GetType().Name}» no es un mensaje del catálogo (CatalogoDeMensajes).", nameof(solicitud));

            if (!ClavesDeEvento.EsValida(tipo.FormaDeClave, solicitud.OriginEventKey))
            {
                throw new ArgumentException(
                    $"«{solicitud.OriginEventKey}» no es una originEventKey válida para {tipo.Type} (contracts/mensajes.md §10.1).",
                    nameof(solicitud));
            }

            var kind = tipo.Kind ?? solicitud.KindDelOriginal
                ?? throw new ArgumentException($"{tipo.Type} hereda el Kind de su original: falta KindDelOriginal.", nameof(solicitud));
            lista.Add(new Contenido(record, tipo, ResolverDestino(tipo, solicitud.Destino), kind));
        }

        var modulos = lista.Select(c => c.Tipo.OriginModule).Distinct(StringComparer.Ordinal).ToList();
        if (modulos.Count > 1)
        {
            throw new ArgumentException(
                $"Una solicitud de emisión trae contenidos de un solo módulo; ésta mezcla {string.Join(", ", modulos)} (T10).", nameof(solicitud));
        }

        if (lista.GroupBy(c => c.Tipo.Type).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Doble emisión: una solicitud repite un tipo de mensaje con la misma originEventKey.");

        return (modulos[0], lista);
    }

    /// <summary>
    /// El destino de un contenido (feature 013, T10): el único que admite el tipo, o el que diga la solicitud si admite varios. Un
    /// destino que el tipo no admite, o un tipo con varios sin <see cref="SolicitudDeEmision.Destino"/>, es un defecto del emisor.
    /// </summary>
    /// <exception cref="ArgumentException">Sin destino en un tipo con varios, o con uno que el tipo no admite.</exception>
    public static string ResolverDestino(TipoDeMensaje tipo, string? destino)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        if (destino is null)
        {
            if (tipo.Destinos.Count == 1) return tipo.Destinos.First();
            throw new ArgumentException(
                $"{tipo.Type} admite los destinos {string.Join(", ", tipo.Destinos.Order(StringComparer.Ordinal))}: la solicitud dice cuál (Destino).",
                nameof(destino));
        }

        if (!tipo.AdmiteDestino(destino))
        {
            throw new ArgumentException(
                $"{tipo.Type} no admite el destino {destino}; admite {string.Join(", ", tipo.Destinos.Order(StringComparer.Ordinal))}.", nameof(destino));
        }

        return destino;
    }

    // ----------------------------------------------------------------------------------- usuario de origen --

    /// <summary>Quién origina el mensaje como <b>dato</b>: persona (con su identidad central) o proceso (sin ella).</summary>
    private sealed record UsuarioDeOrigenResuelto(ActorKind Kind, Guid? CentralUserId, string Nombre);

    /// <summary>
    /// El usuario de origen en el orden de T11: la persona que actúa; si actúa el proceso, el usuario de origen de la solicitud; si
    /// tampoco, el proceso, sólo si todos los tipos lo admiten. Corre antes de agregar nada: si lanza, la operación falla entera.
    /// </summary>
    private async Task<UsuarioDeOrigenResuelto> ResolverUsuarioDeOrigenAsync(SolicitudDeEmision solicitud, List<Contenido> contenidos, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (!actor.EsProceso)
        {
            if (actor.CentralUserId is { } central) return new UsuarioDeOrigenResuelto(ActorKind.Person, central, actor.Name);
            throw new InvalidOperationException("No se puede emitir un mensaje sin la identidad central de quien confirma.");
        }

        if (solicitud.UsuarioDeOrigen is { CentralUserId: { } delOrigen } usuario)
            return new UsuarioDeOrigenResuelto(ActorKind.Person, delOrigen, Recortar(usuario.Name, 150));

        var sinProceso = contenidos.Where(c => !c.Tipo.AdmiteProcesoPara(c.Record)).Select(c => c.Tipo.Type).Distinct(StringComparer.Ordinal).ToList();
        if (sinProceso.Count > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", sinProceso)} lo origina la persona que confirmó u ordenó la operación: el proceso automático no lo emite (T11).");
        }

        return new UsuarioDeOrigenResuelto(ActorKind.Process, null, Execution.Actor.NombreDelProceso);
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];

    private async Task RechazarDobleEmisionAsync(SolicitudDeEmision solicitud, List<Contenido> contenidos, CancellationToken ct)
    {
        var origen = solicitud.Origen.PublicId;
        var clave = solicitud.OriginEventKey;
        foreach (var contenido in contenidos)
        {
            var tipo = contenido.Tipo.Type;
            var repetido = _emitidos.Any(e => e.Mensaje.OriginPublicId == origen && e.Mensaje.Type == tipo && e.Mensaje.OriginEventKey == clave)
                || await db.IntegrationMessages.AnyAsync(m => m.OriginPublicId == origen && m.Type == tipo && m.OriginEventKey == clave, ct);
            if (repetido)
            {
                throw new InvalidOperationException(
                    $"Doble emisión de {tipo} con originEventKey «{clave}» para el origen {origen:D}: es un defecto del emisor (contracts/mensajes.md §10.2).");
            }
        }
    }

    // ---------------------------------------------------------------------------------- modo y cadena --

    private sealed record ModoResuelto(DeliveryMode Modo, string? ScheduleKey, string? BatchScopeKey);

    private static bool LlevaModo(string destino, IntegrationMessageKind kind) =>
        destino == IntegrationDestinations.Accounting && kind == IntegrationMessageKind.Business;

    private async Task<(Guid Raiz, ModoResuelto? Modo)> ResolverModoAsync(SolicitudDeEmision solicitud, bool necesitaModo, CancellationToken ct)
    {
        switch (solicitud.Modo)
        {
            case ModoDeEntrega.Sellado sellado:
                if (!necesitaModo) return (solicitud.Origen.PublicId, null);
                return (solicitud.Origen.PublicId, ValidarSellado(sellado));

            case ModoDeEntrega.Heredado heredado:
                var original = await UltimoDeCadenaAsync(heredado.OriginalPublicId, destino: null, ct);
                var raiz = original?.ChainRootPublicId ?? heredado.OriginalPublicId;
                if (!necesitaModo) return (raiz, null);

                var delOriginal = await UltimoDeCadenaAsync(heredado.OriginalPublicId, IntegrationDestinations.Accounting, ct);
                var entrega = delOriginal is null ? null : await EntregaAsync(delOriginal, IntegrationDestinations.Accounting, ct);
                if (entrega is null)
                {
                    throw new InvalidOperationException(
                        $"El original {heredado.OriginalPublicId:D} no tiene entrega a Contabilidad de la que heredar el modo de paso.");
                }

                return (delOriginal!.ChainRootPublicId, new ModoResuelto(entrega.Mode, entrega.ScheduleKey, heredado.BatchScopeKey));

            default:
                throw new ArgumentException("Modo de entrega desconocido.", nameof(solicitud));
        }
    }

    private static ModoResuelto ValidarSellado(ModoDeEntrega.Sellado sellado)
    {
        switch (sellado.Modo)
        {
            case DeliveryMode.Online:
            case DeliveryMode.NotPosted:
                return new ModoResuelto(sellado.Modo, null, null);
            case DeliveryMode.Batch:
                if (string.IsNullOrWhiteSpace(sellado.ScheduleKey))
                    throw new ArgumentException("Un documento por lotes sella su ScheduleKey (ClavesDeLote.Horario).", nameof(sellado));
                return new ModoResuelto(DeliveryMode.Batch, sellado.ScheduleKey, sellado.BatchScopeKey);
            default:
                throw new ArgumentException(
                    $"El modo sellado de un documento es Online, Batch o NotPosted; llegó {sellado.Modo}.", nameof(sellado));
        }
    }

    /// <summary>Las cadenas de las que depende: su origen, el relacionado, el original del modo y las que pase el emisor.</summary>
    private async Task<List<IntegrationMessage>> ObjetivosAsync(SolicitudDeEmision solicitud, List<string> destinos, CancellationToken ct)
    {
        var cadenas = new List<Guid> { solicitud.Origen.PublicId };
        if (solicitud.Relacionado is { } relacionado) cadenas.Add(relacionado.PublicId);
        if (solicitud.Modo is ModoDeEntrega.Heredado heredado) cadenas.Add(heredado.OriginalPublicId);
        if (solicitud.CadenasDeLasQueDepende is { } otras) cadenas.AddRange(otras);

        var objetivos = new List<IntegrationMessage>();
        foreach (var cadena in cadenas.Where(c => c != Guid.Empty).Distinct())
        {
            Agregar(await UltimoDeCadenaAsync(cadena, destino: null, ct));
            foreach (var destino in destinos)
                Agregar(await UltimoDeCadenaAsync(cadena, destino, ct));
        }

        // Feature 013 (T12): las respuestas a Comercial de una venta llegan en orden: cada una espera a la última ya emitida
        // con la venta como raíz y entrega a su mismo destino, aunque su origen (el recibo) sea otro.
        if (solicitud.CadenaDeOrden is { } raiz && raiz != Guid.Empty)
        {
            foreach (var destino in destinos)
                Agregar(await UltimoDeRaizAsync(raiz, destino, ct));
        }

        return objetivos;

        void Agregar(IntegrationMessage? mensaje)
        {
            if (mensaje is not null && !objetivos.Any(o => ReferenceEquals(o, mensaje)))
                objetivos.Add(mensaje);
        }
    }

    /// <summary>
    /// El último mensaje ya emitido de la cadena (con entrega a <paramref name="destino"/>, si se pide): primero lo
    /// emitido en este ámbito y todavía sin guardar, que es más nuevo que todo lo de la base; si no, el de mayor
    /// <c>Id</c> en la base.
    /// </summary>
    private async Task<IntegrationMessage?> UltimoDeCadenaAsync(Guid cadena, string? destino, CancellationToken ct)
    {
        for (var i = _emitidos.Count - 1; i >= 0; i--)
        {
            var emitido = _emitidos[i];
            if (emitido.Mensaje.OriginPublicId == cadena && (destino is null || emitido.Entregas.Any(e => e.Destination == destino)))
                return emitido.Mensaje;
        }

        var consulta = db.IntegrationMessages.Where(m => m.OriginPublicId == cadena);
        if (destino is not null)
            consulta = consulta.Where(m => db.IntegrationMessageDeliveries.Any(d => d.MessageId == m.Id && d.Destination == destino));
        return await consulta.OrderByDescending(m => m.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>El último mensaje ya emitido con esa raíz de cadena y entrega a <paramref name="destino"/> (feature 013, T12).</summary>
    private async Task<IntegrationMessage?> UltimoDeRaizAsync(Guid raiz, string destino, CancellationToken ct)
    {
        for (var i = _emitidos.Count - 1; i >= 0; i--)
        {
            var emitido = _emitidos[i];
            if (emitido.Mensaje.ChainRootPublicId == raiz && emitido.Entregas.Any(e => e.Destination == destino))
                return emitido.Mensaje;
        }

        return await db.IntegrationMessages
            .Where(m => m.ChainRootPublicId == raiz && db.IntegrationMessageDeliveries.Any(d => d.MessageId == m.Id && d.Destination == destino))
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IntegrationMessageDelivery?> EntregaAsync(IntegrationMessage mensaje, string destino, CancellationToken ct)
    {
        var propia = _emitidos.FirstOrDefault(e => ReferenceEquals(e.Mensaje, mensaje));
        if (propia is not null) return propia.Entregas.FirstOrDefault(e => e.Destination == destino);

        return await db.IntegrationMessageDeliveries.FirstOrDefaultAsync(d => d.MessageId == mensaje.Id && d.Destination == destino, ct);
    }

    // -------------------------------------------------------------------------------------- entregas --

    /// <summary>Estado inicial de contracts/mensajes.md §11.</summary>
    private static IntegrationMessageDelivery NuevaEntrega(
        IntegrationMessage mensaje, string destino, IntegrationMessageKind kind, ModoResuelto? modo, DateTime ahora)
    {
        var entrega = new IntegrationMessageDelivery { Message = mensaje, Destination = destino, Attempts = 0 };

        if (LlevaModo(destino, kind))
        {
            var resuelto = modo ?? throw new InvalidOperationException("Falta el modo de paso de un mensaje de negocio a Contabilidad.");
            entrega.Mode = resuelto.Modo;
            entrega.Status = resuelto.Modo switch
            {
                DeliveryMode.Batch => DeliveryStatus.InBatch,
                DeliveryMode.NotPosted => DeliveryStatus.NotApplicable,
                _ => DeliveryStatus.Pending,
            };
            if (resuelto.Modo == DeliveryMode.Batch)
            {
                entrega.ScheduleKey = resuelto.ScheduleKey;
                entrega.BatchScopeKey = resuelto.BatchScopeKey;
            }
        }
        else
        {
            // Informativos a Contabilidad y todo lo de Cartera: se entregan siempre, sin modo de paso.
            entrega.Mode = DeliveryMode.Always;
            entrega.Status = DeliveryStatus.Pending;
        }

        if (entrega.Status == DeliveryStatus.Pending)
            entrega.NextAttemptAt = ahora;
        return entrega;
    }
}
