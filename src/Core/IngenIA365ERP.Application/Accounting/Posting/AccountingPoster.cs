using IngenIA365ERP.Application.Accounting.Rules;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Posting;

/// <summary>
/// El único camino por el que un movimiento entra al libro (feature 009, FR-036, FR-039;
/// contracts/contabilizacion.md). Valida las doce comprobaciones del contrato, construye el
/// documento con sus líneas ya contabilizadas, toma el consecutivo del tipo y lo AGREGA al
/// contexto SIN guardar: quien llama guarda todo —su operación y el comprobante— en un solo
/// <c>SaveChangesAsync</c>, o nada. La numeración vive en <see cref="VoucherType.NextNumber"/>
/// (se incrementa en memoria y su <c>RowVersion</c> convierte una carrera en
/// <see cref="Common.Behaviors.IReintentableAnteConcurrencia">reintento</see>, R3).
///
/// <para>
/// No abre transacciones, no decide cuentas (las trae el módulo de su parametrización), no
/// escribe saldos (no existen, R4) y no edita ni borra nada contabilizado: la corrección es
/// <see cref="PrepareReversalAsync"/> (Principio XI).
/// </para>
/// </summary>
public sealed class AccountingPoster(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IUserBranchScope scope)
{
    public const int LargoDeDescripcion = 200;
    /// <summary>Largo de <c>RegisteredBy</c> en <c>ACC_Documents</c>.</summary>
    private const int LargoDeUsuario = 100;
    private const string PersonaInactiva = "I";

    /// <summary>Valida, construye y agrega documento + líneas contabilizadas, con número; no guarda.</summary>
    public async Task<Result<AccountingDocument>> PrepareAsync(PostingRequest request, CancellationToken ct)
    {
        var analisis = await AnalizarAsync(request, await CargarAsync([request], seguimiento: true, ct), ct);
        if (analisis.IsFailure) return Result.Failure<AccountingDocument>(analisis.Error);

        var a = analisis.Value;
        if (a.Errores.Count > 0)
            return Result.Failure<AccountingDocument>(a.Errores.Count == 1 ? a.Errores[0].ComoError() : AccountingErrors.DocumentInvalid(a.Errores));

        var documento = Construir(a, request);
        db.AccountingDocuments.Add(documento);
        return Result.Success(documento);
    }

    /// <summary>
    /// Un borrador manual nuevo, agregado al contexto sin guardar y sin número: no es un
    /// movimiento (sus líneas nacen <c>IsPosted = false</c>), pero se crea aquí porque el contrato
    /// es el único sitio que instancia documentos y líneas (lo vigila
    /// <c>NingunModuloEscribeMovimientosFueraDelContrato</c>).
    /// </summary>
    public AccountingDocument NuevoBorrador()
    {
        var documento = new AccountingDocument
        {
            Status = DocumentStatus.Draft,
            Kind = DocumentKind.Regular,
            OriginModule = AccountingDocument.ModuloContabilidad,
            SourceType = nameof(AccountingDocument),
            RegisteredByUserId = user.UserId ?? 0,
            RegisteredBy = Quien(),
            CreatedAt = clock.UtcNow,
            CreatedBy = Quien(),
        };
        documento.SourcePublicId = documento.PublicId;
        db.AccountingDocuments.Add(documento);
        return documento;
    }

    /// <summary>Una línea de borrador, sin contabilizar; quien llama la agrega al documento y la llena.</summary>
    public JournalEntry NuevaLineaDeBorrador() => new() { IsPosted = false, CreatedAt = clock.UtcNow, CreatedBy = Quien() };

    /// <summary>
    /// Contabiliza un borrador EN SU SITIO (mismo <c>PublicId</c>, mismos adjuntos): las mismas
    /// comprobaciones que <see cref="PrepareAsync"/> sobre <paramref name="request"/> —que quien
    /// llama arma desde las líneas vivas del borrador, en su orden—, y si pasan, el documento
    /// recibe número, período, totales, quién contabilizó y sus líneas quedan resueltas y
    /// <c>IsPosted</c>. No guarda.
    /// </summary>
    public async Task<Result<AccountingDocument>> ContabilizarBorradorAsync(AccountingDocument borrador, PostingRequest request, CancellationToken ct)
    {
        if (borrador.Status != DocumentStatus.Draft) return Fallo(AccountingErrors.DocumentNotDraft);
        var analisis = await AnalizarAsync(request, await CargarAsync([request], seguimiento: true, ct), ct);
        if (analisis.IsFailure) return Fallo(analisis.Error);
        var a = analisis.Value;
        if (a.Errores.Count > 0)
            return Fallo(a.Errores.Count == 1 ? a.Errores[0].ComoError() : AccountingErrors.DocumentInvalid(a.Errores));

        var ahora = clock.UtcNow;
        var quien = Quien();
        borrador.VoucherTypeId = a.Voucher.Id;
        borrador.VoucherType = a.Voucher;
        borrador.Number = TomarNumero(a.Voucher);
        borrador.Date = request.Date;
        borrador.Description = Recortar(request.Description);
        borrador.Status = DocumentStatus.Posted;
        borrador.Kind = request.Kind;
        borrador.PeriodId = a.Periodo?.Id;
        borrador.TotalDebit = a.TotalDebit;
        borrador.TotalCredit = a.TotalCredit;
        borrador.PostedByUserId = user.UserId;
        borrador.PostedBy = quien;
        borrador.PostedAt = ahora;
        borrador.UpdatedAt = ahora;
        borrador.UpdatedBy = quien;

        var vivas = borrador.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        if (vivas.Count != a.Lineas.Count) return Fallo(AccountingErrors.DocumentTooFewLines);
        for (var i = 0; i < vivas.Count; i++)
        {
            var linea = vivas[i];
            var r = a.Lineas[i];
            var cuenta = r.Cuenta!;
            linea.LineNumber = r.LineNumber;
            linea.AccountId = cuenta.Id;
            linea.Account = cuenta;
            linea.BranchId = r.BranchId!.Value;
            linea.CostCenterId = r.CostCenterId;
            linea.PersonId = r.Origen.PersonId;
            linea.CrossDocumentTypeId = r.CrossDocumentTypeId;
            linea.CrossDocumentNumber = string.IsNullOrWhiteSpace(r.Origen.CrossDocumentNumber) ? null : r.Origen.CrossDocumentNumber.Trim();
            linea.Debit = r.Origen.Debit;
            linea.Credit = r.Origen.Credit;
            linea.Description = Recortar(r.Origen.Detail);
            linea.TaxBase = r.Origen.TaxBase;
            linea.Date = request.Date;
            linea.IsPosted = true;
            linea.UpdatedAt = ahora;
            linea.UpdatedBy = quien;
            cuenta.FirstMovementAt ??= request.Date;
        }
        return Result.Success(borrador);
    }

    /// <summary>
    /// Las mismas comprobaciones, sin agregar nada: errores y avisos por línea con su campo, para
    /// que la pantalla los señale antes de guardar (<c>POST /documents/validate</c>). Un fallo de
    /// encabezado (contabilidad sin iniciar, tipo, fecha) llega como infracción de la línea 0.
    /// </summary>
    public async Task<ValidacionDeComprobante> ValidarAsync(PostingRequest request, CancellationToken ct) =>
        (await ValidarVariosAsync([request], ct))[0];

    /// <summary>
    /// <see cref="ValidarAsync"/> para varios comprobantes a la vez (feature 012, T488; contracts/contabilidad.md §4 y
    /// §5.5): el mismo análisis —reglas 1 a 11, en el mismo orden— con la configuración, los tipos, los períodos, las
    /// cuentas con sus tarifas, las sucursales, los terceros, los centros y los cruces cargados <b>una vez para todos</b>
    /// y sin seguimiento. No numera, no agrega y no deja nada en el <c>ChangeTracker</c>: lo usan la validación previa
    /// de Inventario y la vista previa de un lote, que no pueden tocar el contexto de quien las llama. Devuelve un
    /// resultado por comprobante, en el orden recibido; <see cref="ValidarAsync"/> es este mismo análisis con uno solo,
    /// así los dos no divergen.
    /// </summary>
    public async Task<IReadOnlyList<ValidacionDeComprobante>> ValidarVariosAsync(IReadOnlyList<PostingRequest> requests, CancellationToken ct)
    {
        if (requests.Count == 0) return [];
        var referencias = await CargarAsync(requests, seguimiento: false, ct);
        var resultados = new List<ValidacionDeComprobante>(requests.Count);
        foreach (var request in requests)
        {
            var analisis = await AnalizarAsync(request, referencias, ct);
            if (analisis.IsFailure)
            {
                var campo = analisis.Error.Code.Contains("Period", StringComparison.Ordinal) || analisis.Error.Code.Contains("Date", StringComparison.Ordinal)
                    ? "Date"
                    : analisis.Error.Code.Contains("VoucherType", StringComparison.Ordinal) ? "VoucherType" : "Header";
                resultados.Add(new ValidacionDeComprobante([ErrorDeLinea.DeEncabezado(analisis.Error, campo)], [],
                    request.Lines.Sum(l => l.Debit), request.Lines.Sum(l => l.Credit)));
                continue;
            }
            var a = analisis.Value;
            resultados.Add(new ValidacionDeComprobante(a.Errores, a.Avisos, a.TotalDebit, a.TotalCredit));
        }
        return resultados;
    }

    /// <summary>
    /// Documento <c>Reversal</c> del mismo tipo, con las líneas invertidas y la referencia en ambos
    /// sentidos; marca el original <c>Reversed</c>. Sólo el módulo dueño reversa lo suyo. Si la
    /// fecha cae en un período cerrado, se fecha en el primer período abierto y lo dice en la
    /// descripción (caso borde de la especificación). No guarda.
    /// </summary>
    public async Task<Result<AccountingDocument>> PrepareReversalAsync(
        AccountingDocument original, DateOnly date, string reason, AccountingOrigin origin, CancellationToken ct)
    {
        // Feature 012 (T29, contracts/contabilidad.md §6): lo de Inventario no se reversa nunca. Cada anulación, nota o
        // ajuste llega como un comprobante nuevo de su propio mensaje; el original no se marca ni se toca. La guarda mira
        // las dos puntas: un original de Inventario, lo pida quien lo pida, y una petición de Inventario sobre cualquier
        // comprobante. Lo vigila LoDeInventarioNoSeReversa.
        if (string.Equals(original.OriginModule, ModuloContable.Inventario, StringComparison.Ordinal)
            || string.Equals(origin.Module, ModuloContable.Inventario, StringComparison.Ordinal))
            return Fallo(AccountingErrors.DocumentInventoryCorrectsWithNewVoucher(original.OriginModule, original.SourceType, original.SourcePublicId));
        if (string.IsNullOrWhiteSpace(reason)) return Fallo(AccountingErrors.ReasonRequired);
        if (original.Kind == DocumentKind.Reversal || original.ReversesDocumentId is not null) return Fallo(AccountingErrors.DocumentIsReversal);
        if (original.Status == DocumentStatus.Reversed || original.ReversedByDocumentId is not null) return Fallo(AccountingErrors.DocumentAlreadyReversed);
        if (original.Status != DocumentStatus.Posted) return Fallo(AccountingErrors.DocumentNotPosted);
        if (!string.Equals(original.OriginModule, origin.Module, StringComparison.Ordinal)) return Fallo(AccountingErrors.DocumentModuleOwned(original.OriginModule));

        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct)) return Fallo(AccountingErrors.NotInitialized);

        // Se carga con seguimiento aunque el original traiga la navegación: el consecutivo se
        // incrementa aquí y tiene que llegar al SaveChanges de quien llama.
        var voucher = await db.VoucherTypes.FirstOrDefaultAsync(v => v.Id == original.VoucherTypeId && !v.IsDeleted, ct);
        if (voucher is null) return Fallo(AccountingErrors.VoucherTypeNotFound(original.VoucherType?.Code ?? original.VoucherTypeId.ToString()));
        if (!voucher.IsActive) return Fallo(AccountingErrors.VoucherTypeInactive(voucher.Code));
        original.VoucherType ??= voucher;

        var hoy = clock.TodayUtc;
        if (origin.EsContabilidad && date > hoy && original.Kind is not (DocumentKind.Closing or DocumentKind.Opening)) return Fallo(AccountingErrors.DateInFuture(date));

        var fecha = date;
        var nota = string.Empty;
        var periodo = await PeriodoDeAsync(date, ct);
        if (original.Kind == DocumentKind.Closing)
        {
            // Reabrir el ejercicio (US6) deshace el cierre en su misma fecha y su mismo período, aunque
            // esté cerrado: una reversión fechada en el ejercicio siguiente dejaría el 31/12 con los
            // resultados cancelados y el 1/1 con ellos de vuelta, y los dos años mentirían. El reverso
            // es también de clase «Cierre»: las consultas lo tratan igual que al cierre que deshace
            // (fuera salvo IncludeClosing), así apertura y cierre siguen neteando a cero.
            fecha = original.Date;
            periodo = await PeriodoDeAsync(original.Date, ct);
            if (periodo is null) return Fallo(AccountingErrors.PeriodNotFound(original.Date));
        }
        else if (original.Kind == DocumentKind.Opening)
        {
            // La apertura (US13) se deshace donde está: la víspera del primer período, sin período y de
            // clase «Apertura», para que siga siendo saldo inicial (en cero) y no un movimiento del mes
            // en que alguien la reversó; la que se cargue después vuelve a ser la única vigente (FR-087).
            fecha = original.Date;
            periodo = null;
        }
        else if (periodo is null || periodo.Status != PeriodStatus.Open)
        {
            var abierto = await db.AccountingPeriods.AsNoTracking()
                .Where(p => !p.IsDeleted && p.Status == PeriodStatus.Open && p.EndDate >= date && (!origin.EsContabilidad || p.StartDate <= hoy))
                .OrderBy(p => p.StartDate)
                .FirstOrDefaultAsync(ct);
            if (abierto is null) return Fallo(periodo is null ? AccountingErrors.PeriodNotFound(date) : AccountingErrors.PeriodClosed(date));
            periodo = abierto;
            fecha = abierto.StartDate > date ? abierto.StartDate : date;
            nota = $" (fechada el {fecha:yyyy-MM-dd}: el período {date:yyyy-MM} está cerrado)";
        }

        var lineas = original.Lines.Count > 0
            ? original.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList()
            : await db.JournalEntries.AsNoTracking().Where(j => j.DocumentId == original.Id && !j.IsDeleted).OrderBy(j => j.LineNumber).ToListAsync(ct);
        if (lineas.Count == 0) return Fallo(AccountingErrors.DocumentTooFewLines);

        var ahora = clock.UtcNow;
        var quien = Quien();
        var referencia = $"{voucher.Code}-{original.Number}";
        var motivo = reason.Trim();

        var reverso = new AccountingDocument
        {
            VoucherTypeId = voucher.Id,
            VoucherType = voucher,
            Number = TomarNumero(voucher),
            Date = fecha,
            Description = Recortar($"Reversión del comprobante {referencia}: {motivo}{nota}"),
            Status = DocumentStatus.Posted,
            Kind = original.Kind is DocumentKind.Closing or DocumentKind.Opening ? original.Kind : DocumentKind.Reversal,
            OriginModule = original.OriginModule,
            SourceType = original.SourceType,
            SourcePublicId = original.SourcePublicId,
            PeriodId = periodo?.Id,
            TotalDebit = original.TotalCredit,
            TotalCredit = original.TotalDebit,
            RegisteredByUserId = user.UserId ?? 0,
            RegisteredBy = quien,
            PostedByUserId = user.UserId,
            PostedBy = quien,
            PostedAt = ahora,
            ReversesDocumentId = original.Id,
            ReversesDocument = original,
            ReversalReason = Recortar(motivo),
            CreatedAt = ahora,
            CreatedBy = quien,
        };
        foreach (var l in lineas)
        {
            reverso.Lines.Add(new JournalEntry
            {
                LineNumber = l.LineNumber,
                AccountId = l.AccountId,
                BranchId = l.BranchId,
                CostCenterId = l.CostCenterId,
                PersonId = l.PersonId,
                CrossDocumentTypeId = l.CrossDocumentTypeId,
                CrossDocumentNumber = l.CrossDocumentNumber,
                Debit = l.Credit,
                Credit = l.Debit,
                Description = Recortar($"Reversión {referencia}: {l.Description}"),
                TaxBase = l.TaxBase,
                Date = fecha,
                IsPosted = true,
                CreatedAt = ahora,
                CreatedBy = quien,
            });
        }

        original.Status = DocumentStatus.Reversed;
        original.ReversedByDocument = reverso;
        original.ReversalReason = Recortar(motivo);
        original.UpdatedAt = ahora;
        original.UpdatedBy = quien;

        db.AccountingDocuments.Add(reverso);
        return Result.Success(reverso);
    }

    // ------------------------------------------------------------------------------------------

    private sealed record LineaResuelta(PostingLine Origen, int LineNumber, ChartOfAccount? Cuenta, int? BranchId, int? CostCenterId, int? CrossDocumentTypeId, string? CrossDocumentType);

    private sealed record Analisis(
        VoucherType Voucher,
        AccountingPeriod? Periodo,
        IReadOnlyList<LineaResuelta> Lineas,
        IReadOnlyList<ErrorDeLinea> Errores,
        IReadOnlyList<ErrorDeLinea> Avisos,
        decimal TotalDebit,
        decimal TotalCredit);

    /// <summary>
    /// Lo que el análisis consulta, cargado una vez para uno o varios comprobantes: configuración, tipos, períodos,
    /// cuentas con sus tarifas y las referencias vigentes (sucursales, terceros, centros, cruces). Con
    /// <c>seguimiento</c> los tipos y las cuentas quedan rastreados —<see cref="PrepareAsync"/> incrementa el
    /// consecutivo y fija <c>FirstMovementAt</c>—; sin él nada queda en el contexto (<see cref="ValidarVariosAsync"/>).
    /// Un conjunto más grande que el de un solo comprobante no cambia ninguna respuesta: las reglas preguntan por
    /// pertenencia de un Id o un código concreto.
    /// </summary>
    private sealed record Referencias(
        AccountingSetup? Setup,
        IReadOnlyDictionary<string, VoucherType> Tipos,
        IReadOnlyList<AccountingPeriod> Periodos,
        IReadOnlyDictionary<int, ChartOfAccount> CuentasPorId,
        IReadOnlyDictionary<string, ChartOfAccount> CuentasPorCodigo,
        AlcanceDeSucursales? AlcanceDelUsuario,
        HashSet<int> Sucursales,
        HashSet<int> Terceros,
        HashSet<int> Centros,
        IReadOnlyDictionary<string, int> TiposDeCruce)
    {
        public AccountingPeriod? PeriodoDe(DateOnly fecha) => Periodos.FirstOrDefault(p => p.StartDate <= fecha && p.EndDate >= fecha);
    }

    /// <summary>El alcance de sucursal sólo se aplica al digitar (FR-035); el cierre cancela las cuentas de resultado de todas.</summary>
    private static bool UsaAlcanceDelUsuario(PostingRequest request) => request.Origin.EsContabilidad && request.Kind != DocumentKind.Closing;

    private async Task<Referencias> CargarAsync(IReadOnlyList<PostingRequest> requests, bool seguimiento, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null)
            return new Referencias(null, new Dictionary<string, VoucherType>(), [], new Dictionary<int, ChartOfAccount>(),
                new Dictionary<string, ChartOfAccount>(), null, [], [], [], new Dictionary<string, int>());

        var codigosDeTipo = requests.Select(r => r.VoucherTypeCode.Trim().ToUpperInvariant()).Distinct().ToList();
        var tiposQuery = db.VoucherTypes.Where(v => codigosDeTipo.Contains(v.Code) && !v.IsDeleted);
        var tipos = await (seguimiento ? tiposQuery : tiposQuery.AsNoTracking()).ToListAsync(ct);

        var periodos = await db.AccountingPeriods.AsNoTracking().Where(p => !p.IsDeleted).ToListAsync(ct);

        var lineas = requests.SelectMany(r => r.Lines).ToList();
        var codigos = lineas.Where(l => l.AccountId is null && !string.IsNullOrWhiteSpace(l.AccountCode)).Select(l => l.AccountCode!.Trim()).Distinct().ToList();
        var ids = lineas.Where(l => l.AccountId is not null).Select(l => l.AccountId!.Value).Distinct().ToList();
        var cuentasQuery = db.ChartOfAccounts.Include(c => c.TaxRates).Where(c => !c.IsDeleted && (ids.Contains(c.Id) || codigos.Contains(c.Code)));
        var cuentas = await (seguimiento ? cuentasQuery : cuentasQuery.AsNoTracking()).ToListAsync(ct);

        // sucursal propuesta: la del usuario al digitar, si no la principal (R7)
        var alcanceDelUsuario = requests.Any(UsaAlcanceDelUsuario) ? await scope.ObtenerAsync(ct) : null;
        var sucursalesRef = lineas.Where(l => l.BranchId is not null).Select(l => l.BranchId!.Value).ToList();
        sucursalesRef.Add(setup.MainBranchId);
        if (alcanceDelUsuario?.SucursalPorDefecto is { } porDefecto) sucursalesRef.Add(porDefecto);
        sucursalesRef = sucursalesRef.Distinct().ToList();
        var sucursales = (await db.Branches.AsNoTracking().Where(b => !b.IsDeleted && sucursalesRef.Contains(b.Id)).Select(b => b.Id).ToListAsync(ct)).ToHashSet();

        var tercerosRef = lineas.Where(l => l.PersonId is not null).Select(l => l.PersonId!.Value).Distinct().ToList();
        var terceros = tercerosRef.Count == 0
            ? new HashSet<int>()
            : (await db.People.AsNoTracking().Where(p => !p.IsDeleted && (p.Status == null || p.Status != PersonaInactiva) && tercerosRef.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct)).ToHashSet();

        var centrosRef = lineas.Where(l => l.CostCenterId is not null).Select(l => l.CostCenterId!.Value).Distinct().ToList();
        var centros = centrosRef.Count == 0
            ? new HashSet<int>()
            : (await db.CostCenters.AsNoTracking().Where(c => !c.IsDeleted && centrosRef.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct)).ToHashSet();

        var tiposRef = lineas.Where(l => !string.IsNullOrWhiteSpace(l.CrossDocumentType)).Select(l => l.CrossDocumentType!.Trim().ToUpperInvariant()).Distinct().ToList();
        var tiposDeCruce = tiposRef.Count == 0
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : await db.CrossDocumentTypes.AsNoTracking().Where(t => !t.IsDeleted && t.IsActive && tiposRef.Contains(t.Code))
                .ToDictionaryAsync(t => t.Code, t => t.Id, StringComparer.OrdinalIgnoreCase, ct);

        return new Referencias(
            setup,
            tipos.ToDictionary(v => v.Code, StringComparer.Ordinal),
            periodos,
            cuentas.ToDictionary(c => c.Id),
            cuentas.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase),
            alcanceDelUsuario,
            sucursales,
            terceros,
            centros,
            tiposDeCruce);
    }

    /// <summary>Comprobaciones 1 a 11 del contrato, en ese orden; las de encabezado cortan, las de línea se acumulan.</summary>
    private async Task<Result<Analisis>> AnalizarAsync(PostingRequest request, Referencias r, CancellationToken ct)
    {
        // 1. contabilidad iniciada
        var setup = r.Setup;
        if (setup is null) return Result.Failure<Analisis>(AccountingErrors.NotInitialized);

        // 2. tipo de comprobante coherente con el origen
        var codigoTipo = request.VoucherTypeCode.Trim().ToUpperInvariant();
        var voucher = r.Tipos.GetValueOrDefault(codigoTipo);
        if (voucher is null) return Result.Failure<Analisis>(AccountingErrors.VoucherTypeNotFound(codigoTipo));
        if (!voucher.IsActive) return Result.Failure<Analisis>(AccountingErrors.VoucherTypeInactive(codigoTipo));
        var permitido = voucher.Usage switch
        {
            VoucherUsage.Manual => request.Origin.EsContabilidad && request.Kind is DocumentKind.Regular or DocumentKind.Reversal,
            VoucherUsage.Module => string.Equals(voucher.ModuleCode, request.Origin.Module, StringComparison.Ordinal) && request.Kind is DocumentKind.Regular or DocumentKind.Reversal,
            VoucherUsage.Opening => request.Kind == DocumentKind.Opening,
            VoucherUsage.Closing => request.Kind == DocumentKind.Closing,
            VoucherUsage.Assets => request.Origin.Module == ModuloContable.Activos,
            _ => false,
        };
        if (!permitido) return Result.Failure<Analisis>(AccountingErrors.VoucherTypeNotAllowedForModule(codigoTipo, request.Origin.Module));

        // 3. fecha y período
        AccountingPeriod? periodo = null;
        if (request.Kind == DocumentKind.Opening)
        {
            // La apertura la fecha quien implanta (E2, 2026-09-22: el dueño la necesitó al corte real
            // del sistema anterior, que casi nunca cae la víspera del primer período). Dos límites, y nada más:
            // no puede ser posterior al último día del primer ejercicio —después de eso ya no es un
            // saldo inicial sino un movimiento— ni caer en un período CERRADO. Su período queda nulo
            // aunque la fecha caiga dentro de uno: en las consultas es saldo inicial, no movimiento
            // del mes (MovimientosContables), y por eso tampoco estorba al cierre mensual.
            var reparo = await FechaDeAperturaInvalidaAsync(db, request.Date, ct);
            if (reparo is not null) return Result.Failure<Analisis>(reparo);
        }
        else if (request.Kind == DocumentKind.Closing)
        {
            // El cierre se fecha el último día del ejercicio, que a esa altura está cerrado —cerrar los
            // doce meses es requisito, no impedimento (FR-023)— y puede estar en el futuro del reloj si
            // el año se cierra por anticipado en pruebas; la fecha no la elige nadie, la fija el ejercicio.
            periodo = r.PeriodoDe(request.Date);
            if (periodo is null) return Result.Failure<Analisis>(AccountingErrors.PeriodNotFound(request.Date));
            if (request.Date.Month != 12 || request.Date != periodo.EndDate) return Result.Failure<Analisis>(AccountingErrors.ClosingDateInvalid(new DateOnly(request.Date.Year, 12, 31)));
        }
        else
        {
            // «Posterior a hoy» sólo se rechaza al digitar: un módulo fecha según su operación (la
            // nómina, al último día del período, que se aprueba unos días antes), y el período tiene
            // que existir y estar abierto de todos modos.
            if (request.Origin.EsContabilidad && request.Date > clock.TodayUtc) return Result.Failure<Analisis>(AccountingErrors.DateInFuture(request.Date));
            periodo = r.PeriodoDe(request.Date);
            if (periodo is null) return Result.Failure<Analisis>(AccountingErrors.PeriodNotFound(request.Date));
            if (periodo.Status != PeriodStatus.Open) return Result.Failure<Analisis>(AccountingErrors.PeriodClosed(request.Date));
        }

        // 4. al menos dos líneas (el importe de cada una lo revisan las reglas)
        if (request.Lines.Count < 2) return Result.Failure<Analisis>(AccountingErrors.DocumentTooFewLines);

        // sucursal propuesta: la del usuario al digitar, si no la principal (R7); alcance sólo en CNT (FR-035)
        var alcance = UsaAlcanceDelUsuario(request) ? r.AlcanceDelUsuario ?? AlcanceDeSucursales.SinRestriccion : AlcanceDeSucursales.SinRestriccion;
        var propuesta = alcance.SucursalPorDefecto ?? setup.MainBranchId;

        var contexto = new ContextoDeReglas(request.Origin.Module, setup.TaxTolerance, alcance,
            r.Sucursales, r.Terceros, r.Centros, new HashSet<string>(r.TiposDeCruce.Keys, StringComparer.OrdinalIgnoreCase), request.Kind);

        // 5 a 10. reglas de cada línea
        var errores = new List<ErrorDeLinea>();
        var avisos = new List<ErrorDeLinea>();
        var resueltas = new List<LineaResuelta>(request.Lines.Count);
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var l = request.Lines[i];
            var numero = i + 1;
            var cuenta = l.AccountId is { } id
                ? r.CuentasPorId.GetValueOrDefault(id)
                : string.IsNullOrWhiteSpace(l.AccountCode) ? null : r.CuentasPorCodigo.GetValueOrDefault(l.AccountCode.Trim());
            var explicita = l.BranchId is not null;
            var sucursal = l.BranchId ?? propuesta;

            // Un módulo manda el centro de costo de la operación; la cuenta decide si lo conserva.
            // Al digitar, en cambio, ponerlo en una cuenta que no lo maneja es un error (regla 9).
            var centro = l.CostCenterId;
            if (centro is not null && cuenta is { RequiresCostCenter: false } && !request.Origin.EsContabilidad) centro = null;

            var tipo = string.IsNullOrWhiteSpace(l.CrossDocumentType) ? null : l.CrossDocumentType.Trim().ToUpperInvariant();
            var reglas = AccountLineRules.Evaluar(
                cuenta is null ? null : CuentaParaReglas.De(cuenta, request.Date),
                new LineaParaReglas(numero, l.AccountRef, l.Debit, l.Credit, sucursal, explicita, l.PersonId, centro, tipo, l.CrossDocumentNumber?.Trim(), l.TaxBase),
                contexto);
            errores.AddRange(reglas.Where(x => x.Bloquea));
            avisos.AddRange(reglas.Where(x => !x.Bloquea));
            resueltas.Add(new LineaResuelta(l, numero, cuenta, sucursal, centro, tipo is null ? null : r.TiposDeCruce.GetValueOrDefault(tipo), tipo));
        }

        // 11. cuadre
        var totalDebito = request.Lines.Sum(l => l.Debit);
        var totalCredito = request.Lines.Sum(l => l.Credit);
        if (totalDebito != totalCredito)
            errores.Add(ErrorDeLinea.DeEncabezado(AccountingErrors.DocumentUnbalanced(totalDebito - totalCredito), "Total"));

        return Result.Success(new Analisis(voucher, periodo, resueltas, errores, avisos, totalDebito, totalCredito));
    }

    /// <summary>12. número y construcción: documento + líneas con fecha y estado desnormalizados; primer movimiento de cada cuenta.</summary>
    private AccountingDocument Construir(Analisis a, PostingRequest request)
    {
        var ahora = clock.UtcNow;
        var quien = Quien();
        var documento = new AccountingDocument
        {
            VoucherTypeId = a.Voucher.Id,
            VoucherType = a.Voucher,
            Number = TomarNumero(a.Voucher),
            Date = request.Date,
            Description = Recortar(request.Description),
            Status = DocumentStatus.Posted,
            Kind = request.Kind,
            OriginModule = request.Origin.Module,
            SourceType = request.Origin.SourceType,
            SourcePublicId = request.Origin.SourcePublicId,
            PeriodId = a.Periodo?.Id,
            TotalDebit = a.TotalDebit,
            TotalCredit = a.TotalCredit,
            RegisteredByUserId = user.UserId ?? 0,
            // Feature 012 (T6): el usuario de origen de un documento de otro módulo queda como dato; el actor sigue siendo quien contabiliza.
            RegisteredBy = request.RegistradoPor is { Name: { } nombre } && !string.IsNullOrWhiteSpace(nombre) ? Recortar(nombre.Trim(), LargoDeUsuario) : quien,
            PostedByUserId = user.UserId,
            PostedBy = quien,
            PostedAt = ahora,
            CreatedAt = ahora,
            CreatedBy = quien,
        };
        foreach (var l in a.Lineas)
        {
            var cuenta = l.Cuenta!;
            documento.Lines.Add(new JournalEntry
            {
                LineNumber = l.LineNumber,
                AccountId = cuenta.Id,
                Account = cuenta,
                BranchId = l.BranchId!.Value,
                CostCenterId = l.CostCenterId,
                PersonId = l.Origen.PersonId,
                CrossDocumentTypeId = l.CrossDocumentTypeId,
                CrossDocumentNumber = string.IsNullOrWhiteSpace(l.Origen.CrossDocumentNumber) ? null : l.Origen.CrossDocumentNumber.Trim(),
                Debit = l.Origen.Debit,
                Credit = l.Origen.Credit,
                Description = Recortar(l.Origen.Detail),
                TaxBase = l.Origen.TaxBase,
                Date = request.Date,
                IsPosted = true,
                CreatedAt = ahora,
                CreatedBy = quien,
            });
            cuenta.FirstMovementAt ??= request.Date;
        }
        return documento;
    }

    /// <summary>
    /// Por qué esa fecha no sirve para la apertura, o null si sirve: tiene que existir un ejercicio,
    /// no pasar del fin del primero y no caer en un período cerrado (FR-084, ampliado en E2).
    /// Lo usan el contrato y quien guarda el borrador, para que los dos digan lo mismo.
    /// </summary>
    public static async Task<Error?> FechaDeAperturaInvalidaAsync(IApplicationDbContext db, DateOnly fecha, CancellationToken ct)
    {
        var primero = await db.AccountingPeriods.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.StartDate)
            .Select(p => new { p.StartDate, p.FiscalYearId }).FirstOrDefaultAsync(ct);
        if (primero is null) return AccountingErrors.PeriodNotFound(fecha);
        var finDelPrimerEjercicio = await db.AccountingPeriods.AsNoTracking().Where(p => !p.IsDeleted && p.FiscalYearId == primero.FiscalYearId)
            .MaxAsync(p => p.EndDate, ct);
        if (fecha > finDelPrimerEjercicio) return AccountingErrors.OpeningDateOutOfRange(primero.StartDate.AddDays(-1), finDelPrimerEjercicio);
        var periodo = await db.AccountingPeriods.AsNoTracking().FirstOrDefaultAsync(p => !p.IsDeleted && p.StartDate <= fecha && p.EndDate >= fecha, ct);
        if (periodo is { Status: PeriodStatus.Closed }) return AccountingErrors.OpeningDateClosed(fecha);
        return null;
    }

    private Task<AccountingPeriod?> PeriodoDeAsync(DateOnly fecha, CancellationToken ct) =>
        db.AccountingPeriods.AsNoTracking().FirstOrDefaultAsync(p => !p.IsDeleted && p.StartDate <= fecha && p.EndDate >= fecha, ct);

    private static long TomarNumero(VoucherType voucher)
    {
        var numero = voucher.NextNumber;
        voucher.NextNumber = numero + 1;
        return numero;
    }

    private string Quien() => string.IsNullOrWhiteSpace(user.UserName) ? "system" : user.UserName;

    private static Result<AccountingDocument> Fallo(Error error) => Result.Failure<AccountingDocument>(error);

    private static string Recortar(string? texto, int largo = LargoDeDescripcion) =>
        texto is null ? string.Empty : texto.Length <= largo ? texto : texto[..largo];
}
