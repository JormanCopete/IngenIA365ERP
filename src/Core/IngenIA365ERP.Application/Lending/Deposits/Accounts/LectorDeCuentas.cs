using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

/// <summary>
/// Lo que comparten las consultas y los comandos de las cuentas (feature 014, US2, T073–T075): la cuenta en alcance —la oficina y la línea de la
/// cuenta; fuera, el mismo 404 que si no existiera y <c>Lending.Access.OutOfScope</c>—, el saldo por concepto (Σ del libro, data-model §0.2), las
/// partes, lo que retiene una apertura, los datos mínimos de conocimiento de la persona y los parámetros que las cuentas leen. (nuevo)
/// </summary>
public static class LectorDeCuentas
{
    /// <summary>Tipo de persona jurídica en <c>COR_People.PersonType</c> (01 natural, 02 jurídica).</summary>
    public const string PersonaJuridica = "02";

    /// <summary>Por qué una cuenta sigue en apertura: una coincidencia en listas sin liberar.</summary>
    public const string RetenidaPorListas = "ScreeningMatch";

    /// <summary>Por qué una cuenta sigue en apertura: el depósito mínimo de apertura que todavía no llega por el contrato.</summary>
    public const string EsperaDepositoInicial = "InitialDeposit";

    /// <summary>La cuenta viva por su PublicId, dentro del alcance de oficina y línea; fuera, el 404 de la cuenta y el evento.</summary>
    public static async Task<Result<MemberAccount>> CuentaEnAlcanceAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.MemberAccounts : db.MemberAccounts.AsNoTracking();
        var cuenta = await consulta.FirstOrDefaultAsync(a => a.PublicId == publicId && !a.IsDeleted, ct);
        if (cuenta is null) return Result.Failure<MemberAccount>(ErroresDeCuentas.CuentaInexistente());
        var dentro = await alcance.AsegurarAsync(cuenta.BranchId, cuenta.CreditLineId, ErroresDeCuentas.CuentaInexistente(), auditoria,
            nameof(MemberAccount), cuenta.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<MemberAccount>(dentro.Error) : Result.Success(cuenta);
    }

    /// <summary>¿Es persona jurídica?</summary>
    public static bool EsJuridica(Person p) => string.Equals(p.PersonType?.Trim(), PersonaJuridica, StringComparison.Ordinal);

    /// <summary>
    /// Los datos mínimos de conocimiento que la apertura exige de la persona (FR-032; research E44): documento, nombre o razón social y, si es persona
    /// natural, la fecha de nacimiento —sin ella no se sabe si es menor—. Vacío = completos.
    /// </summary>
    public static IReadOnlyList<string> DatosFaltantes(Person p)
    {
        ArgumentNullException.ThrowIfNull(p);
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(p.TaxId)) faltan.Add("documento");
        if (EsJuridica(p))
        {
            if (string.IsNullOrWhiteSpace(p.BusinessName) && string.IsNullOrWhiteSpace(p.LastName)) faltan.Add("razón social");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(p.FirstName) || string.IsNullOrWhiteSpace(p.LastName)) faltan.Add("nombres y apellidos");
            if (p.DateOfBirth is null) faltan.Add("fecha de nacimiento");
        }
        return faltan;
    }

    public static MemberAccountPersonDto Persona(Person p) => new(p.PublicId, ReferenciasDeGobierno.Nombre(p), p.TaxId);

    /// <summary>Las personas de <paramref name="ids"/> por su Id interno (también eliminadas: lo histórico se sigue nombrando).</summary>
    public static async Task<IReadOnlyDictionary<int, MemberAccountPersonDto>> PersonasAsync(IApplicationDbContext db, IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return new Dictionary<int, MemberAccountPersonDto>();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => lista.Contains(p.Id)).ToListAsync(ct);
        return personas.ToDictionary(p => p.Id, Persona);
    }

    /// <summary>El saldo por concepto de las cuentas (Σ del libro con todo lo registrado; data-model §0.2).</summary>
    public static async Task<Dictionary<int, Dictionary<MemberAccountConcept, decimal>>> SaldosAsync(IApplicationDbContext db, IReadOnlyCollection<int> cuentas,
        CancellationToken ct)
    {
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted)
            .GroupBy(t => new { t.MemberAccountId, t.Concept })
            .Select(g => new { g.Key.MemberAccountId, g.Key.Concept, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        return filas.GroupBy(f => f.MemberAccountId).ToDictionary(g => g.Key, g => g.ToDictionary(f => f.Concept, f => f.Total));
    }

    /// <summary>
    /// El saldo del titular (data-model §0.2): en captación, capital + interés por pagar + bonificación − cargos por cobrar; en aportes, la suma de las
    /// clases de aporte. Lo retenido (retención y GMF) no cuenta.
    /// </summary>
    public static decimal SaldoDelTitular(LineNature naturaleza, IReadOnlyDictionary<MemberAccountConcept, decimal> porConcepto)
    {
        decimal De(MemberAccountConcept c) => porConcepto.TryGetValue(c, out var v) ? v : 0m;
        return naturaleza == LineNature.Contribution
            ? MemberAccountTransaction.ConceptosDeAportes.Sum(De)
            : De(MemberAccountConcept.Principal) + De(MemberAccountConcept.InterestPayable) + De(MemberAccountConcept.Bonus) - De(MemberAccountConcept.ChargeReceivable);
    }

    /// <summary>Las coincidencias en listas sin liberar de las personas (los bloqueos automáticos <c>RestrictiveListMatch</c> vigentes).</summary>
    public static async Task<IReadOnlyList<Guid>> CoincidenciasSinLiberarAsync(IApplicationDbContext db, IReadOnlyCollection<int> personas, CancellationToken ct)
    {
        if (personas.Count == 0) return [];
        var ids = personas.ToArray();
        var disparadores = await db.DebtorBlocks.AsNoTracking()
            .Where(b => ids.Contains(b.PersonId) && b.Reason == DebtorBlockReason.RestrictiveListMatch && b.LiftedOn == null && !b.IsDeleted)
            .Select(b => b.SourceKey).ToListAsync(ct);
        return disparadores.Select(d => Guid.TryParse(d, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
    }

    /// <summary>
    /// Por qué la cuenta sigue en apertura (vacío = nada la retiene): una coincidencia en listas sin liberar del titular o de un cotitular, o el
    /// depósito mínimo de apertura de la línea que el capital todavía no alcanza.
    /// </summary>
    public static async Task<IReadOnlyList<string>> RetencionesDeAperturaAsync(IApplicationDbContext db, MemberAccount cuenta, DateOnly fecha, CancellationToken ct)
    {
        if (cuenta.Status != MemberAccountStatus.Opening) return [];
        var retenciones = new List<string>();
        var personas = await db.MemberAccountParties.AsNoTracking()
            .Where(p => p.MemberAccountId == cuenta.Id && !p.IsDeleted && p.ValidTo == null
                        && (p.Role == MemberAccountPartyRole.Holder || p.Role == MemberAccountPartyRole.CoHolder))
            .Select(p => p.PersonId).ToListAsync(ct);
        if (!personas.Contains(cuenta.HolderPersonId)) personas.Add(cuenta.HolderPersonId);
        if ((await CoincidenciasSinLiberarAsync(db, personas, ct)).Count > 0) retenciones.Add(RetenidaPorListas);
        if (await DepositoInicialPendienteAsync(db, cuenta, fecha, ct)) retenciones.Add(EsperaDepositoInicial);
        return retenciones;
    }

    /// <summary>¿Exige la línea un depósito de apertura que el capital todavía no alcanza? (sólo captación; aportes se pagan por cuotas).</summary>
    public static async Task<bool> DepositoInicialPendienteAsync(IApplicationDbContext db, MemberAccount cuenta, DateOnly fecha, CancellationToken ct)
    {
        if (cuenta.Nature != LineNature.Deposit) return false;
        var version = await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cuenta.CreditLineId, fecha, null, ct);
        if (version is not { MinOpeningAmount: > 0m }) return false;
        var capital = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuenta.Id && t.Concept == MemberAccountConcept.Principal && !t.IsDeleted)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        return capital < version.MinOpeningAmount;
    }

    /// <summary>La cuenta con todo lo suyo a <paramref name="fecha"/>.</summary>
    public static async Task<MemberAccountDto> DtoAsync(IApplicationDbContext db, MemberAccount cuenta, DateOnly fecha, CancellationToken ct,
        IReadOnlyList<MemberAccountWarningDto>? avisos = null)
    {
        var linea = await LectorDeLineasDeCaptacion.ReferenciaAsync(db, cuenta.CreditLineId, ct)
                    ?? new LendingLineRefDto(Guid.Empty, string.Empty, string.Empty);
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BranchId)
            .Select(b => new MemberAccountOfficeDto(b.PublicId, b.Name)).FirstOrDefaultAsync(ct);
        var filas = await db.MemberAccountParties.AsNoTracking().Where(p => p.MemberAccountId == cuenta.Id && !p.IsDeleted).ToListAsync(ct);
        var personas = await PersonasAsync(db, filas.Select(p => p.PersonId).Append(cuenta.HolderPersonId), ct);
        var vigentes = filas.Where(p => p.VigenteEn(fecha)).ToList();
        var saldos = (await SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var marca = await db.GmfExemptAccounts.AsNoTracking()
            .Where(g => g.MemberAccountId == cuenta.Id && g.ValidTo == null && !g.IsDeleted)
            .Select(g => new GmfExemptMarkDto(g.PublicId, g.ValidFrom)).FirstOrDefaultAsync(ct);
        var control = await db.MemberAccountControls.AsNoTracking().Where(c => c.MemberAccountId == cuenta.Id)
            .Select(c => (DateOnly?)c.LastAccruedDate).FirstOrDefaultAsync(ct);
        var titularFallecido = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == cuenta.HolderPersonId).Select(p => p.IsDeceased)
            .FirstOrDefaultAsync(ct);

        return new MemberAccountDto(cuenta.PublicId, cuenta.Number, cuenta.Nature, cuenta.DepositClass, cuenta.Status, linea, oficina,
            personas[cuenta.HolderPersonId], cuenta.HandlingMode,
            vigentes.Where(p => p.Role != MemberAccountPartyRole.Beneficiary).OrderBy(p => p.Role).ThenBy(p => p.Id).Select(p => Parte(p, personas)).ToList(),
            Beneficiarios(vigentes, personas),
            marca,
            new MemberAccountBalancesDto(saldos.ToDictionary(k => k.Key.ToString(), k => k.Value), SaldoDelTitular(cuenta.Nature, saldos)),
            cuenta.OpenedOn, cuenta.ActivatedOn, control,
            await RetencionesDeAperturaAsync(db, cuenta, fecha, ct),
            new MemberAccountMarksDto(cuenta.RequiresDataUpdate, cuenta.Status == MemberAccountStatus.Inactive, cuenta.IsGarnished, titularFallecido,
                cuenta.TechnicalNegativeSince is not null),
            avisos ?? []);
    }

    public static MemberAccountPartyDto Parte(MemberAccountParty p, IReadOnlyDictionary<int, MemberAccountPersonDto> personas) =>
        new(p.PublicId, personas[p.PersonId], p.Role, p.Share, p.SignatureLimit, p.RepresentationBasis, p.RepresentationSupportAttachmentPublicId,
            p.SignatureSpecimenAttachmentPublicId, p.PhotoAttachmentPublicId, p.ValidFrom, p.ValidTo, p.EndReason);

    /// <summary>Los beneficiarios vigentes (el conjunto más reciente que rige), con su porcentaje 0–100.</summary>
    public static IReadOnlyList<MemberAccountBeneficiaryDto> Beneficiarios(IEnumerable<MemberAccountParty> vigentes,
        IReadOnlyDictionary<int, MemberAccountPersonDto> personas) =>
        vigentes.Where(p => p.Role == MemberAccountPartyRole.Beneficiary).OrderBy(p => p.Id)
            .Select(p => new MemberAccountBeneficiaryDto(p.PublicId, personas[p.PersonId], (p.Share ?? 0m) * 100m, p.BeneficiarySetNumber ?? 0, p.ValidFrom,
                p.BeneficiaryRecordAttachmentPublicId))
            .ToList();

    // ---------------------------------------------------------------------------------------------------------- parámetros --

    /// <summary>La mayoría de edad que rige (<c>Captaciones.MayoriaDeEdadAnios</c>, 18 por defecto con fuente).</summary>
    public static async Task<int> MayoriaDeEdadAsync(ReglasDeParametrosDeCartera parametros, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CaptacionesMayoriaDeEdadAnios, fecha, ct: ct);
        if (leido.IsSuccess && Entero(leido.Value) is { } anios and > 0) return anios;
        // El defecto del catálogo, si la lectura falla: nunca se adivina un número distinto del que declara el parámetro.
        return int.Parse(ParametrosDeCartera.Definiciones.First(d => d.Clave == ParametrosDeCartera.CaptacionesMayoriaDeEdadAnios).DefectoSeguro,
            CultureInfo.InvariantCulture);
    }

    /// <summary>El umbral de efectivo desde el que se exige la declaración de origen de fondos (<c>Efectivo.UmbralDeclaracion</c>); nulo = no se exige.</summary>
    public static async Task<decimal?> UmbralDeOrigenDeFondosAsync(ReglasDeParametrosDeCartera parametros, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.EfectivoUmbralDeclaracion, fecha, ct: ct);
        if (leido.IsFailure) return null;
        return leido.Value.Valor switch
        {
            decimal d => d,
            _ => decimal.TryParse(leido.Value.Texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null,
        };
    }

    /// <summary>La política de exención del GMF que rige (<c>Gmf.PoliticaDeExencion</c>); sin lectura, la de una cuenta marcada (P25).</summary>
    public static async Task<GmfExemptionPolicy> PoliticaDeGmfAsync(ReglasDeParametrosDeCartera parametros, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.GmfPoliticaDeExencion, fecha, ct: ct);
        return leido.IsSuccess && Enum.TryParse<GmfExemptionPolicy>(leido.Value.Texto, ignoreCase: false, out var politica)
            ? politica
            : GmfExemptionPolicy.SingleMarkedAccount;
    }

    private static int? Entero(ParametroDeCartera p) => p.Valor switch
    {
        int i => i,
        long l => (int)l,
        decimal d => (int)d,
        _ => int.TryParse(p.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null,
    };
}
