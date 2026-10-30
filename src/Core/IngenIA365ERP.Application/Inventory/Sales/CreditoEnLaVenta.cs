using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Common.Identidad;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// La elegibilidad provisional de una persona para un medio de crédito (T32; contracts/api.md §23.1) (nuevo): identificada (no el
/// consumidor final), activa en el maestro; con <c>AssociateCredit</c>, asociado sin retiro; con <c>CustomerCredit</c>, cliente.
/// <see cref="Motivos"/> vacío = elegible; si no, en el orden de §23.1 y con los códigos del cobro.
/// </summary>
public sealed record ElegibilidadDeCredito(Person? Persona, string Nombre, bool IsAssociate, bool? AssociateActive, bool IsCustomer, IReadOnlyList<Error> Motivos)
{
    public bool Elegible => Motivos.Count == 0;
}

/// <summary>
/// El crédito dentro del pago de una venta (feature 012, I3, T654; contracts/api.md §23.2; T32; FR-061, FR-062; feature 013, T0176, T95;
/// contracts/interfaces.md §5.3 de la 013) (nuevo). Lo llama <see cref="ReglasDeConfirmacionDeVenta"/> antes de la aprobación, así rige igual
/// en la factura de oficina y en el cobro del POS (<c>CheckoutPosDraftCommand</c> confirma por el mismo flujo canónico). Por cada pago de
/// clase <c>AssociateCredit</c> o <c>CustomerCredit</c>:
/// <list type="number">
/// <item>la elegibilidad provisional (<see cref="ElegibilidadAsync"/>); no elegible → 422 con el primer código de <c>reasons</c>;</item>
/// <item>completa las condiciones con las propuestas del medio y las valida contra sus máximos → <c>Inventory.Credit.TermsOutOfRange</c>;
/// calcula el primer y el último vencimiento y deja en <c>INV_Documents.DueDate</c> el mayor;</item>
/// <item>con <c>CreditWithoutLending</c> (A6, D29) no consulta ni reserva: crédito provisional sellado en <c>Contabilidad</c>;</item>
/// <item>si no, consulta <see cref="IConsultasDeCartera"/> con el plazo de <c>Cartera.ConsultaSegundos</c> y aplica su respuesta (§5.3):
/// <c>NotEnabled</c> → crédito provisional como en la 012, sellado en <c>Contabilidad</c>; bloqueada o sin admitir la venta →
/// <c>Inventory.Credit.LendingBlocked</c>; más que el cupo → <c>Inventory.Credit.QuotaExceeded</c>; sin respuesta →
/// <c>Cartera.PoliticaSinRespuesta</c> (<c>Bloquear</c> → <c>Inventory.Credit.LendingNoResponse</c>; <c>PermitirConAprobacion</c> →
/// origen <c>LendingNoResponse</c> con aprobación provisional); válida → origen <c>Validated</c>, sin pendiente ni aprobación
/// provisional, con la evidencia de la consulta y la línea elegida.</item>
/// </list>
/// <see cref="ReservarAsync"/> reserva el número en Cartera dentro del cerrojo de la confirmación (orden C24) y bajo un punto de guardado
/// propio (C25); <see cref="LiberarAsync"/> lo libera al descartar el borrador (T22). Nunca guarda: los pagos ya están seguidos por la
/// unidad de trabajo de la confirmación.
/// </summary>
public sealed class CreditoEnLaVenta(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    IConsultasDeCartera cartera,
    IReservasDeCartera? reservas = null)
{
    public const string Contabilidad = "Contabilidad";
    public const string Cartera = "Cartera";

    /// <summary>Valores de <c>Cartera.PoliticaSinRespuesta</c>.</summary>
    public const string Bloquear = "Bloquear";

    public const string PermitirConAprobacion = "PermitirConAprobacion";

    /// <summary>El plazo de la consulta cuando el parámetro no está disponible (su defecto seguro).</summary>
    public const int SegundosPorDefecto = 5;

    /// <summary>El punto de guardado propio de la reserva (C25).</summary>
    public const string PuntoDeGuardadoDeLaReserva = "reserva_cartera";

    /// <summary>El código de Cartera para una reserva que supera el cupo (interfaces.md §5.2).</summary>
    public const string ReservaSinCupo = "Lending.Reservation.QuotaExceeded";

    /// <summary>Periodicidad por defecto (mensual) cuando ni el pago ni el medio la dicen.</summary>
    public const short PeriodicidadPorDefecto = 30;

    private readonly IReservasDeCartera _reservas = reservas ?? new ReservasDeCarteraNoHabilitada();

    public async Task<Result> ValidarAsync(InventoryDocument documento, IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var creditos = pagos.Select((p, i) => (Pago: p, Indice: i)).Where(x => x.Pago.EsCredito).ToList();
        if (creditos.Count == 0) return Result.Success();

        var fecha = documento.OperationDate;
        var persona = documento.CounterpartyPersonId is int id ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) : null;
        var ids = creditos.Select(c => c.Pago.PaymentMeansId).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var canal = documento.SalesChannelId is int c ? await db.SalesChannels.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;
        var sello = await SelloAsync(fecha, ct);

        DateOnly? mayor = null;
        foreach (var (pago, indice) in creditos)
        {
            var elegibilidad = await ElegibilidadAsync(db, persona, fecha, pago.MeansClass, ct);
            if (!elegibilidad.Elegible) return Result.Failure(elegibilidad.Motivos[0]);

            if (!medios.TryGetValue(pago.PaymentMeansId, out var medio)) return Result.Failure(ErroresDeCredito.MeansNotCredit(pago.MeansCode));
            var condiciones = Condiciones(medio, pago.InstallmentCount, pago.CreditTermDays, pago.InstallmentPeriodDays, pago.FirstDueDate, fecha);
            if (condiciones is null) return Result.Failure(ErroresDeCredito.TermsOutOfRange(medio.Code, indice, medio.MaxInstallments, MaximoDePlazo(medio)));

            pago.InstallmentCount = condiciones.Cuotas;
            pago.CreditTermDays = condiciones.PlazoDias;
            pago.InstallmentPeriodDays = condiciones.PeriodicidadDias;
            pago.FirstDueDate = condiciones.PrimerVencimiento;
            pago.FinalDueDate = condiciones.UltimoVencimiento;
            pago.SuggestedCreditLineCode ??= medio.SuggestedCreditLineCode;
            pago.CreditWithoutLending = medio.CreditWithoutLending;
            mayor = mayor is { } m && m >= condiciones.UltimoVencimiento ? m : condiciones.UltimoVencimiento;

            // A6 (D29): el crédito que no pasa a Cartera no le pregunta nada; queda como el provisional, en Contabilidad.
            if (pago.CreditWithoutLending)
            {
                Provisional(pago, Contabilidad);
                continue;
            }

            var respuesta = await ConsultarAsync(new ConsultaCrediticia(persona!.PublicId, pago.MeansClass, pago.Amount, fecha, canal), fecha, ct);
            if (respuesta.Estado is not { } estado)
            {
                var politica = await PoliticaSinRespuestaAsync(fecha, pago.MeansClass, ct);
                if (!string.Equals(politica, PermitirConAprobacion, StringComparison.OrdinalIgnoreCase))
                    return Result.Failure(ErroresDeCredito.LendingNoResponse(respuesta.Segundos));
                Provisional(pago, sello);
                pago.CreditOrigin = CreditOrigin.LendingNoResponse;
                continue;
            }
            if (!estado.LendingEnabled)
            {
                // Antes de activar Cartera (o sin destino): el crédito provisional de la 012.
                Provisional(pago, Contabilidad);
                continue;
            }
            if (estado.Status == EstadosCrediticios.Blocked || !estado.AllowsSale)
                return Result.Failure(ErroresDeCredito.LendingBlocked(elegibilidad.Nombre, estado.BlockReason));
            if (estado.AvailableQuota is { } cupo && pago.Amount > cupo)
                return Result.Failure(ErroresDeCredito.QuotaExceeded(pago.Amount, cupo));

            pago.PendingValidation = false;
            pago.CreditOrigin = CreditOrigin.Validated;
            pago.ConsultationEvidence = estado.EvidenceId;
            pago.CreditLineCode ??= pago.SuggestedCreditLineCode ?? estado.Lines.FirstOrDefault()?.Code;
            pago.AccountsReceivableRecordedBy = sello;
        }
        documento.DueDate = mayor;
        return Result.Success();
    }

    /// <summary>El crédito provisional de la 012: pendiente de validar, origen provisional (con su aprobación) y el sello dado.</summary>
    private static void Provisional(DocumentPayment pago, string sello)
    {
        pago.PendingValidation = true;
        pago.CreditOrigin = CreditOrigin.ProvisionalCredit;
        pago.ConsultationEvidence = null;
        pago.AccountsReceivableRecordedBy = sello;
    }

    /// <summary>Lo que respondió la consulta: el estado, o nulo si no respondió en <see cref="Segundos"/> (o falló).</summary>
    private sealed record RespuestaDeConsulta(EstadoCrediticioDto? Estado, int Segundos);

    /// <summary>
    /// La consulta a Cartera con el plazo de <c>Cartera.ConsultaSegundos</c> (token de cancelación enlazado, §5.1): una respuesta tardía no
    /// cambia la venta. El tiempo agotado o una falla técnica son «sin respuesta»; la cancelación de quien llama sube.
    /// </summary>
    private async Task<RespuestaDeConsulta> ConsultarAsync(ConsultaCrediticia consulta, DateOnly fecha, CancellationToken ct)
    {
        var segundos = await EnteroAsync(ParametrosDeInventario.CarteraConsultaSegundos, fecha, SegundosPorDefecto, ct);
        return new RespuestaDeConsulta(await ConsultarConPlazoAsync(cartera, consulta, segundos, ct), segundos);
    }

    /// <summary>
    /// <see cref="IConsultasDeCartera.EstadoCrediticioAsync"/> con un plazo: nulo si no respondió a tiempo o falló («sin respuesta»); la
    /// cancelación de quien llama sube. La usan la confirmación y la evaluación de la pantalla (<see cref="EvaluateSaleCreditQuery"/>).
    /// </summary>
    public static async Task<EstadoCrediticioDto?> ConsultarConPlazoAsync(IConsultasDeCartera cartera, ConsultaCrediticia consulta, int segundos,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cartera);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(segundos));
        try
        {
            return await cartera.EstadoCrediticioAsync(consulta, limite.Token).WaitAsync(TimeSpan.FromSeconds(segundos), ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary><c>Cartera.PoliticaSinRespuesta</c> por tipo de tercero (la clase del medio); <c>Bloquear</c> si no está disponible.</summary>
    public async Task<string> PoliticaSinRespuestaAsync(DateOnly fecha, PaymentMeansClass clase, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CarteraPoliticaSinRespuesta, fecha,
            ParameterScopeKind.ThirdPartyKind, (int)clase, ct);
        return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto! : Bloquear;
    }

    private async Task<int> EnteroAsync(string clave, DateOnly fecha, int porDefecto, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, fecha, ct: ct);
        return leido.IsSuccess && int.TryParse(leido.Value.Texto, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n)
            && n > 0
            ? n
            : porDefecto;
    }

    // ------------------------------------------------------------------------------------------------ reserva --

    /// <summary>La clave de la reserva: <c>INV:{paymentPublicId:N}:{monto}</c>, con dos decimales y punto (cultura invariante; G9).</summary>
    public static string ClaveDeReserva(Guid pagoPublicId, decimal monto) =>
        $"INV:{pagoPublicId:N}:{monto.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Reserva en Cartera el número de cada pago de crédito validado que aún no lo tiene (FR-057, T22; interfaces.md §5.2, §5.3), dentro de
    /// la unidad de trabajo de la confirmación y bajo un punto de guardado propio (C25): ante una falla técnica vuelve a él y la venta sigue
    /// sin número (D26). <c>Reserved</c> → número y reserva en el pago; <c>Rejected</c> → la confirmación no procede
    /// (<c>Inventory.Credit.QuotaExceeded</c> o <c>.LendingBlocked</c>); <c>NotEnabled</c> o <c>NoResponse</c> → nada. Quien llama respeta
    /// el orden de cerrojos (C24): filas de Inventario → reserva → numerador de Inventario.
    /// </summary>
    public async Task<Result> ReservarAsync(InventoryDocument documento, IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var aReservar = pagos.Where(p => !p.IsDeleted && p.Direction == PaymentDirection.Received && p.PasaACartera
            && p.CreditOrigin == CreditOrigin.Validated && p.ReservationPublicId is null).ToList();
        if (aReservar.Count == 0) return Result.Success();

        var persona = documento.CounterpartyPersonId is int id ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) : null;
        if (persona is null) return Result.Success();
        var nombre = Pos.BorradorDelPos.Nombre(persona);
        var canal = documento.SalesChannelId is int c ? await db.SalesChannels.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;

        foreach (var pago in aReservar)
        {
            var solicitud = new SolicitudDeReserva(ClaveDeReserva(pago.PublicId, pago.Amount), pago.PublicId, documento.PublicId, persona.PublicId,
                pago.MeansClass, pago.Amount, documento.OperationDate, pago.CreditLineCode ?? pago.SuggestedCreditLineCode ?? string.Empty, canal)
            {
                // Feature 013 (T0599, C31): la evidencia de la consulta que precedió a la reserva viaja con ella.
                ConsultationEvidence = pago.ConsultationEvidence,
            };
            var r = await ReservarBajoPuntoDeGuardadoAsync(solicitud, ct);
            switch (r.Status)
            {
                case EstadosDeReserva.Reserved or EstadosDeReserva.Used:
                    pago.ReservedLoanNumber = r.LoanNumber;
                    pago.ReservationPublicId = r.ReservationPublicId;
                    break;
                case EstadosDeReserva.Rejected:
                    return Result.Failure(string.Equals(r.Code, ReservaSinCupo, StringComparison.Ordinal)
                        ? ErroresDeCredito.QuotaExceeded(pago.Amount, null)
                        : ErroresDeCredito.LendingBlocked(nombre, r.Message));
            }
        }
        return Result.Success();
    }

    /// <summary>
    /// La reserva bajo su punto de guardado (C25; feature 013, T0599: por <see cref="PuntoDeGuardado"/>, el mismo que usa Cartera): una falla técnica
    /// vuelve a él —en PostgreSQL, además, deja la transacción utilizable—, saca del seguimiento lo que la reserva dejó agregado y responde
    /// <c>NoResponse</c>. Sin transacción en curso (InMemory, dobles) sólo captura la falla.
    /// </summary>
    private Task<ResultadoDeReservaDto> ReservarBajoPuntoDeGuardadoAsync(SolicitudDeReserva solicitud, CancellationToken ct) =>
        PuntoDeGuardado.EjecutarAsync(db, PuntoDeGuardadoDeLaReserva, () => _reservas.ReservarAsync(solicitud, ct),
            e => new ResultadoDeReservaDto(EstadosDeReserva.NoResponse, null, null, null, EstadosDeReserva.NoResponse, e.Message), ct);

    /// <summary>
    /// Libera la reserva de cada pago que la tenga, por su clave (T22: descartar un borrador de oficina o del POS). Una liberación que
    /// falla no detiene el descarte: la reserva vence sola (<c>cartera.reservas-vencidas</c>) y su número nunca se reasigna.
    /// </summary>
    public async Task LiberarAsync(IEnumerable<DocumentPayment> pagos, string motivo, CancellationToken ct)
    {
        foreach (var pago in pagos.Where(p => p.ReservationPublicId is not null))
        {
            try
            {
                await _reservas.LiberarAsync(new SolicitudDeLiberacion(ClaveDeReserva(pago.PublicId, pago.Amount), pago.PublicId, motivo), ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested && e is not OutOfMemoryException)
            {
                // Sin respuesta: la reserva queda Reserved hasta vencer.
            }
        }
    }

    /// <summary>
    /// <c>Cartera.CuentaPorCobrarRegistradaPor</c> a la fecha, forzado a <c>Contabilidad</c> mientras <c>Cartera.IntegracionHabilitadaDesde</c>
    /// esté vacío (o sea posterior a la fecha, o aún no disponible): lo que se sella en el pago (FR-062).
    /// </summary>
    public async Task<string> SelloAsync(DateOnly fecha, CancellationToken ct)
    {
        var desde = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CarteraIntegracionHabilitadaDesde, fecha, ct: ct);
        var habilitada = desde.IsSuccess && DateOnly.TryParse(desde.Value.Texto, System.Globalization.CultureInfo.InvariantCulture, out var d) && d <= fecha;
        if (!habilitada) return Contabilidad;
        var quien = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CarteraCuentaPorCobrarRegistradaPor, fecha, ct: ct);
        return quien.IsSuccess && string.Equals(quien.Value.Texto, Cartera, StringComparison.OrdinalIgnoreCase) ? Cartera : Contabilidad;
    }

    /// <summary>Las condiciones del crédito, ya completas y validadas. (nuevo)</summary>
    public sealed record CondicionesDeCredito(short Cuotas, short PlazoDias, short PeriodicidadDias, DateOnly PrimerVencimiento, DateOnly UltimoVencimiento);

    /// <summary>El plazo máximo del medio: el máximo, o el propuesto si no tiene máximo.</summary>
    public static short? MaximoDePlazo(PaymentMeans medio) => medio.MaxTermDays ?? medio.DefaultTermDays;

    /// <summary>
    /// Completa las condiciones pedidas con las propuestas del medio y las valida contra sus máximos (§23.2): cuotas de 1 al máximo,
    /// plazo de 1 al máximo (o al propuesto), periodicidad positiva, primer vencimiento después de la fecha y no después del último, que
    /// es la fecha más el plazo. Con una cuota, el primer vencimiento es el último. Nulo si algo queda fuera.
    /// </summary>
    public static CondicionesDeCredito? Condiciones(PaymentMeans medio, short? cuotas, short? plazo, short? periodicidad, DateOnly? primer, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(medio);
        var n = (short)(cuotas ?? medio.DefaultInstallments ?? medio.MaxInstallments ?? 1);
        var dias = (short)(plazo ?? medio.DefaultTermDays ?? medio.MaxTermDays ?? 0);
        var cada = (short)(periodicidad ?? medio.InstallmentPeriodDays ?? PeriodicidadPorDefecto);
        if (n < 1 || medio.MaxInstallments is { } maxN && n > maxN) return null;
        if (dias < 1 || MaximoDePlazo(medio) is { } maxD && dias > maxD) return null;
        if (cada < 1) return null;
        var ultimo = fecha.AddDays(dias);
        var primero = primer ?? (n == 1 ? ultimo : fecha.AddDays(Math.Min(cada, dias)));
        if (primero <= fecha || primero > ultimo) return null;
        return new CondicionesDeCredito(n, dias, cada, primero, ultimo);
    }

    /// <summary>
    /// ¿La persona es la genérica del consumidor final del catálogo vigente? Por tipo y número (feature 017: una CE con el mismo
    /// número es otra persona); sin traducción del tipo, por el número como antes.
    /// </summary>
    private static bool EsElConsumidorFinal(Person persona, DateOnly fecha)
    {
        var catalogo = CatalogoDian.Embebido;
        var consumidor = catalogo.ConsumidorFinal(fecha);
        if (consumidor is null) return false;
        var idType = catalogo.IdTypeDe(consumidor.TipoDeIdentificacion, fecha);
        return idType is null
            ? UsuarioDeDocumento.NormalizarNumero(persona.TaxId) == UsuarioDeDocumento.NormalizarNumero(consumidor.Numero)
            : persona.Username == UsuarioDeDocumento.DerivarParaPersona(idType, consumidor.Numero);
    }

    /// <summary>La elegibilidad provisional de la persona para la clase del medio (T32, §23.1).</summary>
    public static async Task<ElegibilidadDeCredito> ElegibilidadAsync(IApplicationDbContext db, Person? persona, DateOnly fecha, PaymentMeansClass clase,
        CancellationToken ct)
    {
        if (persona is null || persona.IsDeleted || EsElConsumidorFinal(persona, fecha))
            return new ElegibilidadDeCredito(persona, persona is null ? string.Empty : Pos.BorradorDelPos.Nombre(persona), persona?.IsAssociate ?? false, null,
                persona?.IsCustomer ?? false, [ErroresDeCredito.PersonNotIdentified()]);

        var nombre = Pos.BorradorDelPos.Nombre(persona);
        bool? asociadoVigente = null;
        if (persona.IsAssociate)
        {
            var asociado = await db.Associates.AsNoTracking().Where(a => a.PersonId == persona.Id && !a.IsDeleted)
                .Select(a => new { a.Status, a.WithdrawalDate, a.RejoinDate }).FirstOrDefaultAsync(ct);
            asociadoVigente = asociado is not null && !Retirado(asociado.Status, asociado.WithdrawalDate, asociado.RejoinDate, fecha);
        }

        var motivos = new List<Error>();
        if (string.Equals(persona.Status, "I", StringComparison.OrdinalIgnoreCase)) motivos.Add(ErroresDeCredito.PersonInactive(nombre));
        if (clase == PaymentMeansClass.AssociateCredit && asociadoVigente != true) motivos.Add(ErroresDeCredito.NotAssociate(nombre));
        if (clase == PaymentMeansClass.CustomerCredit && !persona.IsCustomer) motivos.Add(ErroresDeCredito.NotCustomer(nombre));
        return new ElegibilidadDeCredito(persona, nombre, persona.IsAssociate, asociadoVigente, persona.IsCustomer, motivos);
    }

    /// <summary>
    /// ¿El asociado tiene retiro a la fecha? Estado <c>R</c> o fecha de retiro cumplida, salvo un reingreso posterior al retiro que ya
    /// rige.
    /// </summary>
    public static bool Retirado(string? estado, DateOnly? retiro, DateOnly? reingreso, DateOnly fecha)
    {
        var reingresado = reingreso is { } r && r <= fecha && (retiro is null || r >= retiro);
        if (reingresado) return false;
        return string.Equals(estado, "R", StringComparison.OrdinalIgnoreCase) || retiro is { } w && w <= fecha;
    }
}
