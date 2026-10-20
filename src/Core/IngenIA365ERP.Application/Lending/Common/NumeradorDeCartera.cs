using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// El único que numera en Cartera (feature 013, C48, T0207; data-model §0.3, §1.10): el único que toca <c>LND_NumberSequences</c>
/// y el que arma el <c>Number</c> de créditos, operaciones, solicitudes, órdenes, archivos, actas y documentos. Lo llama la operación
/// <b>al final</b> del orden de cerrojos (T34) y dentro de su transacción:
/// <list type="number">
/// <item>busca la fila de (<see cref="LendingNumberKind"/>, ámbito); si falta, la agrega con el prefijo pedido (la inserta el
/// <c>SaveChanges</c> de la operación);</item>
/// <item>si existe, la bloquea con <see cref="ICerrojoDeCartera.BloquearNumeracionAsync"/> y la relee ya bloqueada;</item>
/// <item>arma <c>Prefix + NextValue</c> con ceros a la izquierda hasta <c>MinDigits</c>, <b>salta</b> los números que ya existen en
/// su tabla (migrados o reservados: no tienen que ser continuos, FR-057) e incrementa <c>NextValue</c> por EF; el guardado de la
/// operación lo escribe todo junto.</item>
/// </list>
/// Un borrador no consume número: la solicitud se numera al radicar (<see cref="NumerarSolicitudAsync"/> rechaza un borrador) y el
/// acta al cerrar. Nadie más escribe <c>NextValue</c> ni asigna <c>Number</c> en <c>Entities/Lending</c> (<c>SoloElNumeradorNumera</c>).
/// (nuevo)
///
/// <para>Feature 014 (T032; data-model §0.4, §1.3 con los valores de research E03): numera también las cuentas de captación y aportes
/// (<c>MemberAccount</c> 16, un consecutivo por línea con <c>ScopeKey</c> = código de la línea), sus operaciones (<c>MemberAccountOperation</c> 17,
/// ámbito = la clase), el radicado del retiro del asociado (18), la distribución de la asamblea (19) y el caso de fallecimiento (20). Las órdenes de
/// giro de cuentas comparten el consecutivo de las de desembolso (<c>DisbursementOrder</c>): un número no se repite entre las dos tablas.</para>
/// </summary>
public sealed class NumeradorDeCartera(IApplicationDbContext db, ICerrojoDeCartera cerrojo)
{
    /// <summary>Dígitos mínimos de una fila nueva (con ceros a la izquierda).</summary>
    public const int DigitosPorDefecto = 6;

    /// <summary>Largo máximo de un número de Cartera (<c>nvarchar(20)</c>, data-model §0.3).</summary>
    public const int LargoMaximo = 20;

    /// <summary>
    /// El siguiente número de la clase en su ámbito. <paramref name="prefijo"/> sólo se usa al crear la fila (después rige el de
    /// la fila); <paramref name="ambito"/> es <c>''</c> o la clave del ámbito (clase de operación, <c>PublicId:N</c> del órgano…).
    /// </summary>
    public async Task<string> AsignarAsync(LendingNumberKind clase, string ambito, string prefijo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ambito);
        ArgumentNullException.ThrowIfNull(prefijo);
        var secuencia = await SecuenciaAsync(clase, ambito, prefijo, ct);

        while (true)
        {
            var numero = Formatear(secuencia.Prefix, secuencia.NextValue, secuencia.MinDigits);
            secuencia.NextValue = secuencia.NextValue + 1;
            if (numero.Length > LargoMaximo)
                throw new InvalidOperationException($"El número {numero} de {clase} pasa de {LargoMaximo} caracteres.");
            if (!await ExisteAsync(clase, numero, ct)) return numero;
        }
    }

    /// <summary>
    /// Feature 013, US8 (T0750): <paramref name="cantidad"/> comprobantes seguidos de una clase de operación para una tanda de la activación, con
    /// una consulta de existencia por tanda (no una por número): los que ya existen se saltan como en <see cref="AsignarAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<string>> NumerosDeOperacionAsync(LoanOperationKind clase, string prefijo, int cantidad, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cantidad);
        var numeros = new List<string>(cantidad);
        if (cantidad == 0) return numeros;
        var secuencia = await SecuenciaAsync(LendingNumberKind.Operation, clase.ToString(), prefijo, ct);
        while (numeros.Count < cantidad)
        {
            var faltan = cantidad - numeros.Count;
            var candidatos = new List<string>(faltan);
            for (var i = 0; i < faltan; i++)
            {
                var numero = Formatear(secuencia.Prefix, secuencia.NextValue, secuencia.MinDigits);
                secuencia.NextValue = secuencia.NextValue + 1;
                if (numero.Length > LargoMaximo)
                    throw new InvalidOperationException($"El número {numero} de {LendingNumberKind.Operation} pasa de {LargoMaximo} caracteres.");
                candidatos.Add(numero);
            }
            var existentes = (await db.LoanOperations.IgnoreQueryFilters().Where(o => candidatos.Contains(o.Number)).Select(o => o.Number).ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);
            numeros.AddRange(candidatos.Where(c => !existentes.Contains(c)));
        }
        return numeros;
    }

    /// <summary>
    /// Feature 013, US8 (T0752; FR-057): después de la activación con cartera previa, el consecutivo de créditos arranca por encima del mayor número
    /// <b>numérico</b> migrado (sin el prefijo de la fila, si lo trae): así un crédito nuevo nunca choca con uno migrado. Un número con letras no
    /// cuenta (el numerador igual lo salta si existe). No baja nunca el consecutivo.
    /// </summary>
    public async Task LlevarCreditosPorEncimaDeAsync(IEnumerable<string> numerosMigrados, string prefijo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(numerosMigrados);
        var secuencia = await SecuenciaAsync(LendingNumberKind.Loan, string.Empty, prefijo, ct);
        long mayor = 0;
        foreach (var numero in numerosMigrados)
        {
            var sinPrefijo = secuencia.Prefix.Length > 0 && numero.StartsWith(secuencia.Prefix, StringComparison.OrdinalIgnoreCase)
                ? numero[secuencia.Prefix.Length..]
                : numero;
            if (sinPrefijo.Length > 0 && sinPrefijo.All(char.IsAsciiDigit)
                && long.TryParse(sinPrefijo, NumberStyles.None, CultureInfo.InvariantCulture, out var valor) && valor > mayor)
                mayor = valor;
        }
        if (mayor >= secuencia.NextValue) secuencia.NextValue = mayor + 1;
    }

    /// <summary>La fila de numeración de (clase, ámbito), bloqueada si viene de la base; agregada con el prefijo pedido si no existe.</summary>
    private async Task<LendingNumberSequence> SecuenciaAsync(LendingNumberKind clase, string ambito, string prefijo, CancellationToken ct)
    {
        // Una fila ya seguida en esta unidad de trabajo (agregada por otra operación de la tanda, o ya bloqueada antes): se sigue
        // con ella, que lleva el NextValue al día y, si venía de la base, el candado tomado.
        var secuencia = db.LendingNumberSequences.Local.FirstOrDefault(s => s.Kind == clase && s.ScopeKey == ambito);
        if (secuencia is null)
        {
            var id = await ExistenteAsync(clase, ambito, ct);
            if (id is null)
            {
                // La primera vez en este ámbito: la fila la crea el cerrojo (si hay transacción) para que dos operaciones simultáneas no agreguen cada
                // una la suya con NextValue = 1 y numeren lo mismo (E238); sin transacción, o sin cerrojo real, se agrega abajo como siempre.
                await cerrojo.AsegurarNumeracionAsync(clase, ambito, prefijo, DigitosPorDefecto, ct);
                id = await ExistenteAsync(clase, ambito, ct);
            }
            if (id is null)
            {
                secuencia = new LendingNumberSequence { Kind = clase, ScopeKey = ambito, Prefix = prefijo, NextValue = 1, MinDigits = DigitosPorDefecto };
                db.LendingNumberSequences.Add(secuencia);
            }
            else
            {
                // Bloquea primero y lee después: así se lee el NextValue que dejó el último que numeró.
                await cerrojo.BloquearNumeracionAsync(id.Value, ct);
                secuencia = await db.LendingNumberSequences.FirstAsync(s => s.Id == id.Value, ct);
            }
        }
        return secuencia;
    }

    private Task<int?> ExistenteAsync(LendingNumberKind clase, string ambito, CancellationToken ct) =>
        db.LendingNumberSequences.AsNoTracking()
            .Where(s => s.Kind == clase && s.ScopeKey == ambito)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>El radicado de una solicitud que se envía a análisis (nunca un borrador, data-model §2.1).</summary>
    public async Task NumerarSolicitudAsync(CreditApplication solicitud, string prefijo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (solicitud.Status == CreditApplicationStatus.Draft)
            throw new InvalidOperationException("Un borrador no consume número: la solicitud se numera al radicarla.");
        if (solicitud.Number is not null)
            throw new InvalidOperationException($"La solicitud {solicitud.PublicId} ya tiene número.");
        solicitud.Number = await AsignarAsync(LendingNumberKind.Application, string.Empty, prefijo, ct);
    }

    /// <summary>El número del acta de una sesión que se cierra: ámbito = el órgano (<c>PublicId:N</c>), prefijo = el del órgano (C48).</summary>
    public async Task NumerarActaAsync(Session sesion, GoverningBody organo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        ArgumentNullException.ThrowIfNull(organo);
        if (sesion.Status == SessionStatus.Draft)
            throw new InvalidOperationException("Un acta en borrador no consume número: se numera al cerrar la sesión.");
        if (sesion.Number is not null)
            throw new InvalidOperationException($"La sesión {sesion.PublicId} ya tiene número.");
        sesion.Number = await AsignarAsync(LendingNumberKind.SessionMinutes, AmbitoDeOrgano(organo), organo.MinutesPrefix, ct);
    }

    /// <summary>El número del crédito que nace con su primer tramo (feature 013, US3, T0444): clase <c>Loan</c>, ámbito único.</summary>
    public async Task NumerarCreditoAsync(Domain.Entities.Lending.Loans.Loan credito, string prefijo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credito);
        if (!string.IsNullOrEmpty(credito.Number))
            throw new InvalidOperationException($"El crédito {credito.PublicId} ya tiene número.");
        credito.Number = await AsignarAsync(LendingNumberKind.Loan, string.Empty, prefijo, ct);
    }

    /// <summary>
    /// US6 (T0590; FR-057, C20): el crédito que nace de una venta toma el número que el numerador asignó a su reserva (<c>LND_LoanNumberReservations</c>,
    /// clase <c>Loan</c>): el número nunca se reasigna y la reserva sólo se usa una vez.
    /// </summary>
    public static void NumerarCreditoConSuReserva(Domain.Entities.Lending.Loans.Loan credito, Domain.Entities.Lending.Origination.LoanNumberReservation reserva)
    {
        ArgumentNullException.ThrowIfNull(credito);
        ArgumentNullException.ThrowIfNull(reserva);
        if (!string.IsNullOrEmpty(credito.Number))
            throw new InvalidOperationException($"El crédito {credito.PublicId} ya tiene número.");
        credito.Number = reserva.Number;
        credito.ReservationId = reserva.Id;
    }

    /// <summary>El número de una orden de giro (feature 013, US3, T0444, T0446): clase <c>DisbursementOrder</c>, ámbito único.</summary>
    public async Task NumerarOrdenAsync(Domain.Entities.Lending.Operations.DisbursementOrder orden, string prefijo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orden);
        if (!string.IsNullOrEmpty(orden.Number))
            throw new InvalidOperationException($"La orden {orden.PublicId} ya tiene número.");
        orden.Number = await AsignarAsync(LendingNumberKind.DisbursementOrder, string.Empty, prefijo, ct);
    }

    /// <summary>
    /// El comprobante de una operación (feature 013, US3): clase <c>Operation</c> con ámbito = la clase de la operación. La operación es un
    /// hecho de sólo inserción: el número se pide antes de armarla y nace en su inicializador.
    /// </summary>
    public Task<string> NumeroDeOperacionAsync(LoanOperationKind clase, string prefijo, CancellationToken ct = default) =>
        AsignarAsync(LendingNumberKind.Operation, clase.ToString(), prefijo, ct);

    /// <summary>
    /// El número de un documento emitido (feature 013, US13, T0779, T0784): clase <c>IssuedDocument</c> con ámbito = la clase del documento y el
    /// prefijo de <see cref="Documents.PrefijosDeDocumentosDeCartera"/> (<c>PP-</c>, <c>PS-</c>, <c>PSG-</c>, <c>CS-</c>…). El documento es un hecho de
    /// sólo inserción: el número se pide al final, antes de armarlo, y nace en su inicializador.
    /// </summary>
    public Task<string> NumeroDeDocumentoAsync(IssuedDocumentKind clase, CancellationToken ct = default) =>
        AsignarAsync(LendingNumberKind.IssuedDocument, Documents.PrefijosDeDocumentosDeCartera.Ambito(clase),
            Documents.PrefijosDeDocumentosDeCartera.De(clase), ct);

    /// <summary>
    /// El consecutivo de una carga de migración (feature 013, T0232; data-model §29.1: <c>Number</c> entero, único). No tiene fila en
    /// <c>LND_NumberSequences</c> (<c>LendingNumberKind</c> no la nombra): es el mayor de <c>LND_MigrationLoads</c>, borradas incluidas, más
    /// uno; el índice único es la defensa ante dos cargas a la vez.
    /// </summary>
    public async Task NumerarCargaAsync(MigrationLoad carga, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(carga);
        if (carga.Number != 0)
            throw new InvalidOperationException($"La carga {carga.PublicId} ya tiene número.");
        var ultimo = await db.MigrationLoads.IgnoreQueryFilters().MaxAsync(l => (int?)l.Number, ct) ?? 0;
        var enEsteAmbito = db.MigrationLoads.Local.Select(l => l.Number).DefaultIfEmpty(0).Max();
        carga.Number = Math.Max(ultimo, enEsteAmbito) + 1;
    }

    // ----------------------------------------------------------------------------------------- feature 014 (T032) --

    /// <summary>
    /// El número de una cuenta de captación o de aportes (data-model §0.4): clase <c>MemberAccount</c>, un consecutivo <b>por línea</b>
    /// (<c>ScopeKey</c> = <paramref name="codigoDeLinea"/>). Se pide al final del orden de cerrojos; un borrador de apertura no consume número y el
    /// número no se cambia nunca (tampoco al cumplir la mayoría de edad ni al renovar un CDAT).
    /// </summary>
    public async Task NumerarCuentaAsync(Domain.Entities.Lending.Deposits.Accounts.MemberAccount cuenta, string codigoDeLinea, string prefijo,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDeLinea);
        if (!string.IsNullOrEmpty(cuenta.Number))
            throw new InvalidOperationException($"La cuenta {cuenta.PublicId} ya tiene número.");
        cuenta.Number = await AsignarAsync(LendingNumberKind.MemberAccount, codigoDeLinea, prefijo, ct);
    }

    /// <summary>
    /// El comprobante de una operación de cuenta (data-model §4.1): clase <c>MemberAccountOperation</c> con ámbito = la clase de la operación. La
    /// operación es un hecho de sólo inserción: el número se pide antes de armarla y nace en su inicializador.
    /// </summary>
    public Task<string> NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind clase, string prefijo, CancellationToken ct = default) =>
        AsignarAsync(LendingNumberKind.MemberAccountOperation, clase.ToString(), prefijo, ct);

    /// <summary>
    /// <paramref name="cantidad"/> comprobantes seguidos de una clase de operación de cuenta para una tanda (la activación, la causación por lotes),
    /// con una consulta de existencia por tanda: los que ya existen se saltan como en <see cref="AsignarAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<string>> NumerosDeOperacionDeCuentaAsync(MemberAccountOperationKind clase, string prefijo, int cantidad,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cantidad);
        var numeros = new List<string>(cantidad);
        if (cantidad == 0) return numeros;
        var secuencia = await SecuenciaAsync(LendingNumberKind.MemberAccountOperation, clase.ToString(), prefijo, ct);
        while (numeros.Count < cantidad)
        {
            var faltan = cantidad - numeros.Count;
            var candidatos = new List<string>(faltan);
            for (var i = 0; i < faltan; i++)
            {
                var numero = Formatear(secuencia.Prefix, secuencia.NextValue, secuencia.MinDigits);
                secuencia.NextValue = secuencia.NextValue + 1;
                if (numero.Length > LargoMaximo)
                    throw new InvalidOperationException($"El número {numero} de {LendingNumberKind.MemberAccountOperation} pasa de {LargoMaximo} caracteres.");
                candidatos.Add(numero);
            }
            var existentes = (await db.MemberAccountOperations.IgnoreQueryFilters().Where(o => candidatos.Contains(o.Number)).Select(o => o.Number)
                    .ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);
            numeros.AddRange(candidatos.Where(c => !existentes.Contains(c)));
        }
        return numeros;
    }

    /// <summary>
    /// El radicado del retiro del asociado (18), el número de una distribución de la asamblea (19) o el de un caso de fallecimiento (20), ámbito único.
    /// El retiro salta los números de su tabla desde US10 (T096); las de K10 nacen con su entrega: hasta entonces el numerador no sabe en qué tabla buscar
    /// un número ya usado y no salta ninguno.
    /// </summary>
    public Task<string> NumeroDeProcesoDeCaptacionesAsync(LendingNumberKind clase, string prefijo, CancellationToken ct = default)
    {
        if (clase is not (LendingNumberKind.MembershipWithdrawal or LendingNumberKind.AssemblyDistribution or LendingNumberKind.DeceasedHolderCase))
            throw new ArgumentOutOfRangeException(nameof(clase), clase, "Sólo el retiro del asociado, la distribución de la asamblea y el caso de fallecimiento.");
        return AsignarAsync(clase, string.Empty, prefijo, ct);
    }

    /// <summary>
    /// El número de una orden de giro de cuentas (data-model §3.9): el mismo consecutivo de las órdenes de desembolso (<c>DisbursementOrder</c>),
    /// que salta los números de las dos tablas.
    /// </summary>
    public async Task NumerarOrdenDeCuentaAsync(Domain.Entities.Lending.Deposits.Accounts.AccountPayoutOrder orden, string prefijo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orden);
        if (!string.IsNullOrEmpty(orden.Number))
            throw new InvalidOperationException($"La orden {orden.PublicId} ya tiene número.");
        orden.Number = await AsignarAsync(LendingNumberKind.DisbursementOrder, string.Empty, prefijo, ct);
    }

    /// <summary>
    /// Al activar captaciones o aportes con carga (data-model §0.4), el consecutivo de cuentas de una línea arranca por encima del mayor número
    /// <b>numérico</b> migrado de esa línea (sin el prefijo de la fila, si lo trae). Un número con letras no cuenta (el numerador igual lo salta si
    /// existe). No baja nunca el consecutivo.
    /// </summary>
    /// <summary>
    /// Feature 014 (US9, T155; data-model §0.4): una cuenta migrada conserva su número de origen (en mayúsculas, hasta 20). No consume consecutivo: la
    /// activación lleva antes el consecutivo de la línea por encima de los números migrados (<see cref="LlevarCuentasPorEncimaDeAsync"/>).
    /// </summary>
    public static void ConservarNumeroDeCuenta(Domain.Entities.Lending.Deposits.Accounts.MemberAccount cuenta, string numero)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        if (!string.IsNullOrEmpty(cuenta.Number)) throw new InvalidOperationException($"La cuenta {cuenta.PublicId} ya tiene número.");
        var normalizado = numero.Trim().ToUpperInvariant();
        if (normalizado.Length > Domain.Entities.Lending.Deposits.Accounts.MemberAccount.LargoDelNumero)
            throw new ArgumentException($"El número {normalizado} pasa de {Domain.Entities.Lending.Deposits.Accounts.MemberAccount.LargoDelNumero} caracteres.", nameof(numero));
        cuenta.Number = normalizado;
    }

    public async Task LlevarCuentasPorEncimaDeAsync(string codigoDeLinea, IEnumerable<string> numerosMigrados, string prefijo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDeLinea);
        ArgumentNullException.ThrowIfNull(numerosMigrados);
        var secuencia = await SecuenciaAsync(LendingNumberKind.MemberAccount, codigoDeLinea, prefijo, ct);
        var mayor = MayorNumerico(numerosMigrados, secuencia.Prefix);
        if (mayor >= secuencia.NextValue) secuencia.NextValue = mayor + 1;
    }

    /// <summary>El mayor número puramente numérico de <paramref name="numeros"/>, sin el <paramref name="prefijo"/> si lo trae; 0 si no hay.</summary>
    private static long MayorNumerico(IEnumerable<string> numeros, string prefijo)
    {
        long mayor = 0;
        foreach (var numero in numeros)
        {
            var sinPrefijo = prefijo.Length > 0 && numero.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) ? numero[prefijo.Length..] : numero;
            if (sinPrefijo.Length > 0 && sinPrefijo.All(char.IsAsciiDigit)
                && long.TryParse(sinPrefijo, NumberStyles.None, CultureInfo.InvariantCulture, out var valor) && valor > mayor)
                mayor = valor;
        }
        return mayor;
    }

    /// <summary>El ámbito del consecutivo de actas de un órgano.</summary>
    public static string AmbitoDeOrgano(GoverningBody organo) => organo.PublicId.ToString("N", CultureInfo.InvariantCulture);

    /// <summary><c>Prefix</c> + el número con ceros a la izquierda hasta <paramref name="digitos"/>.</summary>
    public static string Formatear(string prefijo, long valor, int digitos) =>
        prefijo + valor.ToString(CultureInfo.InvariantCulture).PadLeft(digitos, '0');

    /// <summary>¿El número ya existe en la tabla de su clase? (migrado, reservado o asignado antes; FR-057.)</summary>
    private Task<bool> ExisteAsync(LendingNumberKind clase, string numero, CancellationToken ct) => clase switch
    {
        LendingNumberKind.Loan => ExisteCreditoAsync(numero, ct),
        LendingNumberKind.Operation => db.LoanOperations.IgnoreQueryFilters().AnyAsync(o => o.Number == numero, ct),
        LendingNumberKind.Application => db.CreditApplications.IgnoreQueryFilters().AnyAsync(a => a.Number == numero, ct),
        // Feature 014 (T032): las órdenes de giro de cuentas comparten el consecutivo de las de desembolso.
        LendingNumberKind.DisbursementOrder => ExisteOrdenAsync(numero, ct),
        LendingNumberKind.DisbursementFile => db.DisbursementFiles.IgnoreQueryFilters().AnyAsync(f => f.Number == numero, ct),
        LendingNumberKind.SessionMinutes => db.LendingSessions.IgnoreQueryFilters().AnyAsync(s => s.Number == numero, ct),
        LendingNumberKind.IssuedDocument => db.IssuedLendingDocuments.IgnoreQueryFilters().AnyAsync(d => d.Number == numero, ct),
        LendingNumberKind.PayrollDeductionFile => db.PayrollDeductionFiles.IgnoreQueryFilters().AnyAsync(f => f.Number == numero, ct),
        // K3 (T0878, T0884): la novedad y la condonación.
        LendingNumberKind.LoanNovelty => db.LoanNovelties.IgnoreQueryFilters().AnyAsync(n => n.Number == numero, ct),
        LendingNumberKind.Waiver => db.Waivers.IgnoreQueryFilters().AnyAsync(w => w.Number == numero, ct),
        LendingNumberKind.WriteOffBatch => db.WriteOffBatches.IgnoreQueryFilters().AnyAsync(b => b.Number == numero, ct),
        LendingNumberKind.PortfolioEvaluation => db.PortfolioEvaluations.IgnoreQueryFilters().AnyAsync(e => e.Number == numero, ct),
        // K4 (T0946, T0947): el acuerdo de pago y el proceso de cobro jurídico.
        LendingNumberKind.PaymentAgreement => db.PaymentAgreements.IgnoreQueryFilters().AnyAsync(a => a.Number == numero, ct),
        LendingNumberKind.LegalProcess => db.LegalProcesses.IgnoreQueryFilters().AnyAsync(p => p.Number == numero, ct),
        // K5 (T1004): el archivo regulatorio no tiene columna de número: el número encabeza su nombre (NUMERO_…).
        LendingNumberKind.RegulatoryFile => db.RegulatoryFiles.IgnoreQueryFilters().AnyAsync(f => f.FileName != null && f.FileName.StartsWith(numero + "_"), ct),
        // Feature 014 (T032): la cuenta y su operación. El retiro del asociado, la distribución y el caso de fallecimiento suman su tabla al nacer.
        LendingNumberKind.MemberAccount => db.MemberAccounts.IgnoreQueryFilters().AnyAsync(a => a.Number == numero, ct),
        LendingNumberKind.MemberAccountOperation => db.MemberAccountOperations.IgnoreQueryFilters().AnyAsync(o => o.Number == numero, ct),
        // US10 (T096): el retiro del asociado.
        LendingNumberKind.MembershipWithdrawal => db.MembershipWithdrawals.IgnoreQueryFilters().AnyAsync(w => w.Number == numero, ct),
        // US12 (T202): la decisión de asamblea.
        LendingNumberKind.AssemblyDistribution => db.AssemblyDistributions.IgnoreQueryFilters().AnyAsync(d => d.Number == numero, ct),
        // US14 (T207): el caso de fallecimiento.
        LendingNumberKind.DeceasedHolderCase => db.DeceasedHolderCases.IgnoreQueryFilters().AnyAsync(d => d.Number == numero, ct),
        _ => Task.FromResult(false),
    };

    private async Task<bool> ExisteOrdenAsync(string numero, CancellationToken ct) =>
        await db.DisbursementOrders.IgnoreQueryFilters().AnyAsync(o => o.Number == numero, ct)
        || await db.AccountPayoutOrders.IgnoreQueryFilters().AnyAsync(o => o.Number == numero, ct);

    private async Task<bool> ExisteCreditoAsync(string numero, CancellationToken ct) =>
        await db.Loans.IgnoreQueryFilters().AnyAsync(l => l.Number == numero, ct)
        || await db.LoanNumberReservations.IgnoreQueryFilters().AnyAsync(r => r.Number == numero, ct);
}
