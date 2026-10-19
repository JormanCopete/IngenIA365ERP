using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Columnas;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>Lo que dejó la validación entre partes: los errores (todo o nada), los avisos y los conteos de la carga. (nuevo)</summary>
public sealed record ValidacionDeCaptaciones(IReadOnlyList<ErrorEntrePartes> Errores, IReadOnlyList<ErrorEntrePartes> Avisos, int Cuentas, int Personas);

/// <summary>
/// La validación entre partes de una carga de aportes o captaciones (feature 014, US9, T153; plantillas-y-archivos.md §4.9; data-model §7.1), todo o nada:
/// <list type="bullet">
/// <item>la parte obligatoria de la naturaleza tiene filas (<c>Lending.Migration.PartMissing</c>: cuentas de aportes en aportes, cuentas de ahorro en
/// captaciones);</item>
/// <item>toda referencia a una cuenta (partes de la cuenta, cuotas, exención del GMF, acumulado tributario) existe en la carga
/// (<c>Lending.Migration.AccountNotInLoad</c>), y ningún número se repite entre las dos partes de cuentas ni frente a la Cartera
/// (<c>Lending.Migration.AccountNumberDuplicate</c>);</item>
/// <item>la titularidad de cada cuenta con las reglas de la apertura (<see cref="ReglasDeTitularidad"/>, con lo que la versión de la línea al corte admite):
/// cotitulares, proporciones, manejo conjunto, beneficiarios al 100 %; un titular menor o persona jurídica con exactamente un representante legal
/// (<c>Lending.Migration.MinorWithoutRepresentative</c>), cuyo soporte se sube después;</item>
/// <item>cuotas atrasadas sólo en aportes y ahorro permanente, y lo pagado de ellas no supera el saldo del concepto;</item>
/// <item>la exención del GMF con la política «una cuenta marcada» (<c>Lending.Gmf.PolicyWithoutMark</c>), sobre una cuenta de ahorro de la que la persona
/// es titular y sin otra marca vigente en la Cartera (<c>Lending.Gmf.AlreadyMarked</c>);</item>
/// <item>el acumulado tributario de un titular o cotitular de la cuenta;</item>
/// <item>las personas de la carga siguen vivas, y la de aportes no tiene ya una cuenta de aportes viva;</item>
/// <item>US6 (pendiente de la fase 10): cada CDAT regenera su plan con <see cref="PlanDeCdat"/> —el vencimiento es el del plan (o su día hábil), los períodos
/// pagados son los de fecha de pago hasta el corte y el interés causado no difiere en más de un peso del que espera el plan
/// (<c>Lending.TermDeposit.ScheduleMismatch</c> con los dos valores)— y su cuenta destino es una cuenta a la vista del mismo titular, de la carga o viva
/// (<c>Lending.TermDeposit.DestinationInvalid</c>).</item>
/// </list>
/// Sólo lee. (nuevo)
/// </summary>
public sealed class ValidacionEntrePartesDeCaptaciones(IApplicationDbContext db, ReglasDeParametrosDeCartera parametros,
    IngenIA365ERP.Application.Common.Calendar.ICalendarioHabil? calendario = null)
{
    public async Task<ValidacionDeCaptaciones> ValidarAsync(FilasDeLaCarga filas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filas);
        var carga = filas.Carga;
        var corte = carga.CutoffDate;
        var errores = new List<ErrorEntrePartes>();
        var avisos = new List<ErrorEntrePartes>();
        void Error(string parte, int tanda, int fila, string columna, string codigo, string mensaje) =>
            errores.Add(new ErrorEntrePartes(parte, tanda, fila, columna, codigo, mensaje));

        var obligatoria = carga.Nature == LineNature.Contribution ? P.ContributionAccounts : P.MemberAccounts;
        if (filas.Cuentas.Count == 0)
            errores.Add(new ErrorEntrePartes(obligatoria, null, 0, string.Empty, ErroresDeMigracion.CodigoParteFaltante, ErroresDeMigracion.ParteFaltante(obligatoria)));

        var cuentas = filas.Cuentas;
        var porNumero = new Dictionary<string, CuentaDeLaCarga>(StringComparer.Ordinal);
        foreach (var c in cuentas.Where(c => c.Numero is not null))
            if (!porNumero.TryAdd(c.Numero!, c))
                Error(c.Parte, c.Tanda, c.Fila, C.Numero, ErroresDeMigracionDeCaptaciones.CodigoNumeroDuplicado,
                    $"El número {c.Numero} ya lo tiene otra cuenta de la carga ({porNumero[c.Numero!].Parte}, fila {porNumero[c.Numero!].Fila}).");
        var numeros = porNumero.Keys.ToList();
        var enCartera = numeros.Count == 0 ? [] : (await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => numeros.Contains(a.Number))
            .Select(a => a.Number).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        foreach (var n in enCartera)
            Error(porNumero[n].Parte, porNumero[n].Tanda, porNumero[n].Fila, C.Numero, ErroresDeMigracionDeCaptaciones.CodigoNumeroDuplicado,
                $"Ya hay una cuenta de la Cartera con el número {n}.");

        // Las personas de la carga: vivas, con lo que la titularidad necesita (nacimiento, persona jurídica).
        var idsDePersonas = cuentas.Select(c => c.TitularId)
            .Concat(filas.PartesDeCuenta.Select(p => p.Fila.PersonId))
            .Concat(filas.Exenciones.Select(e => e.Fila.PersonId))
            .Concat(filas.Tributario.Select(t => t.Fila.PersonId))
            .Concat(filas.Bloqueos.Where(b => b.Fila.CourtPersonId != null).Select(b => b.Fila.CourtPersonId!.Value))
            .Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => idsDePersonas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        void PersonaViva(string parte, int tanda, int fila, string columna, int id)
        {
            if (!personas.TryGetValue(id, out var p) || p.IsDeleted)
                Error(parte, tanda, fila, columna, IngenIA365ERP.Application.Common.Imports.ImportErrors.CellNotFound,
                    "La persona se dio de baja en el maestro después de cargar la fila: restáurela o retire la fila.");
        }
        foreach (var c in cuentas) PersonaViva(c.Parte, c.Tanda, c.Fila, c.Parte == P.ContributionAccounts ? C.Asociado : C.Titular, c.TitularId);

        if (carga.Nature == LineNature.Contribution)
        {
            var titulares = cuentas.Select(c => c.TitularId).ToList();
            var conAportes = (await db.MemberAccounts.AsNoTracking()
                    .Where(a => titulares.Contains(a.HolderPersonId) && a.Nature == LineNature.Contribution && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
                    .Select(a => a.HolderPersonId).ToListAsync(ct))
                .ToHashSet();
            foreach (var c in cuentas.Where(c => conAportes.Contains(c.TitularId)))
                Error(c.Parte, c.Tanda, c.Fila, C.Asociado, "Lending.Contributions.AccountAlreadyOpen", "La persona ya tiene una cuenta de aportes viva en la Cartera.");
        }

        CuentaDeLaCarga? Cuenta(string parte, int tanda, int fila, string columna, string numero)
        {
            if (porNumero.TryGetValue(numero, out var c)) return c;
            Error(parte, tanda, fila, columna, ErroresDeMigracionDeCaptaciones.CodigoCuentaNoEnLaCarga,
                $"La cuenta {numero} no está en la carga (cuentas de ahorro o de aportes con número): cárguela o corrija el número.");
            return null;
        }

        // ------------------------------------------------------------------------------------------------ titularidad --
        var lineas = await LineasDeLaCarga.LeerAsync(db, carga.Nature, corte, cuentas.Select(c => c.LineaCodigo).Distinct().ToList(), ct);
        var mayoria = await LectorDeCuentas.MayoriaDeEdadAsync(parametros, corte, ct);
        var partesPorCuenta = new Dictionary<string, List<FilaConTanda<Domain.Entities.Lending.Deposits.Migration.MigrationAccountPartyRow>>>(StringComparer.Ordinal);
        foreach (var pa in filas.PartesDeCuenta)
        {
            var f = pa.Fila;
            PersonaViva(P.MemberAccountParties, pa.Tanda, f.RowNumber, C.Persona, f.PersonId);
            var c = Cuenta(P.MemberAccountParties, pa.Tanda, f.RowNumber, C.Cuenta, f.SourceAccountNumber);
            if (c is null) continue;
            if (c.Naturaleza == LineNature.Contribution && f.Role != MemberAccountPartyRole.Beneficiary)
            {
                Error(P.MemberAccountParties, pa.Tanda, f.RowNumber, C.Rol, ReglasDeTitularidad.CodigoSoloTitularYBeneficiarios,
                    "Una cuenta de aportes sólo tiene titular y beneficiarios.");
                continue;
            }
            if (!partesPorCuenta.TryGetValue(c.Llave, out var lista)) partesPorCuenta[c.Llave] = lista = [];
            lista.Add(pa);
        }
        var manejoDe = filas.CuentasDeAhorro.ToDictionary(r => r.Fila.SourceAccountNumber, r => r.Fila.HandlingMode, StringComparer.Ordinal);
        foreach (var c in cuentas)
        {
            if (!personas.TryGetValue(c.TitularId, out var titular)) continue;
            var propias = partesPorCuenta.GetValueOrDefault(c.Llave) ?? [];
            var version = lineas.GetValueOrDefault(c.LineaCodigo)?.DeCaptacion;
            var juridica = LectorDeCuentas.EsJuridica(titular);
            var menor = !juridica && ReglasDeTitularidad.EsMenor(titular.DateOfBirth, corte, mayoria);
            var contexto = new ContextoDeTitularidad(c.Naturaleza, c.TitularId,
                AdmiteCotitulares: version?.AllowsCoHolders ?? false, AdmiteMenores: version?.AllowsMinors ?? true,
                AdmitePersonasJuridicas: version?.AllowsLegalEntities ?? true, TitularMenor: c.Naturaleza == LineNature.Deposit && menor,
                TitularJuridico: c.Naturaleza == LineNature.Deposit && juridica, SoporteDiferido: true);
            var manejo = c.Numero is { } n && manejoDe.TryGetValue(n, out var m) ? m : HandlingMode.Indistinct;
            var solicitadas = propias.Where(p => p.Fila.Role != MemberAccountPartyRole.Beneficiary)
                .Select(p => new ParteSolicitada(p.Fila.PersonId, p.Fila.Role, p.Fila.Role == MemberAccountPartyRole.CoHolder ? p.Fila.Share : null,
                    p.Fila.SignatureLimit, p.Fila.RepresentationBasis)).ToList();
            var (_, problema) = ReglasDeTitularidad.Resolver(contexto, manejo, solicitadas);
            if (problema is not null)
            {
                var codigo = problema.Codigo == ReglasDeTitularidad.CodigoRepresentanteRequerido ? ErroresDeMigracionDeCaptaciones.CodigoMenorSinRepresentante
                    : problema.Codigo;
                var mensaje = problema.Codigo == ReglasDeTitularidad.CodigoRepresentanteRequerido
                    ? $"La cuenta {c.Numero} es de un titular {(menor ? "menor de edad" : "persona jurídica")}: cargue exactamente un representante legal con su fundamento en la parte de partes de la cuenta."
                    : $"La cuenta {c.Numero ?? c.Llave}: {problema.Mensaje}";
                Error(c.Parte, c.Tanda, c.Fila, c.Parte == P.ContributionAccounts ? C.Asociado : C.Titular, codigo, mensaje);
            }
            var beneficiarios = propias.Where(p => p.Fila.Role == MemberAccountPartyRole.Beneficiary)
                .Select(p => new BeneficiarioSolicitado(p.Fila.PersonId, (p.Fila.Share ?? 0m) * 100m)).ToList();
            var (_, deBeneficiarios) = ReglasDeTitularidad.ResolverBeneficiarios(c.TitularId, beneficiarios);
            if (deBeneficiarios is not null)
            {
                var primera = propias.First(p => p.Fila.Role == MemberAccountPartyRole.Beneficiary);
                Error(P.MemberAccountParties, primera.Tanda, primera.Fila.RowNumber, C.PorcentajeBeneficiario, deBeneficiarios.Codigo,
                    $"La cuenta {c.Numero ?? c.Llave}: {deBeneficiarios.Mensaje}");
            }
            if (titular.IsDeceased)
                avisos.Add(new ErrorEntrePartes(c.Parte, c.Tanda, c.Fila, C.Titular, "Lending.Savings.HolderDeceased",
                    $"El titular de la cuenta {c.Numero ?? c.Llave} figura fallecido: la cuenta se migra y espera su trámite de fallecimiento."));
        }
        foreach (var (llave, propias) in partesPorCuenta)
            if (propias.Any(p => p.Fila.Role == MemberAccountPartyRole.LegalRepresentative))
            {
                var r = propias.First(p => p.Fila.Role == MemberAccountPartyRole.LegalRepresentative);
                avisos.Add(new ErrorEntrePartes(P.MemberAccountParties, r.Tanda, r.Fila.RowNumber, C.Soporte, "Lending.Migration.SupportPending",
                    $"La cuenta {llave} queda con la actualización de datos pendiente hasta subir el soporte de la representación."));
            }

        // ------------------------------------------------------------------------------------------------------ cuotas --
        foreach (var grupo in filas.Cuotas.GroupBy(q => q.Fila.SourceAccountNumber, StringComparer.Ordinal))
        {
            var primera = grupo.First();
            var c = Cuenta(P.AccountInstallments, primera.Tanda, primera.Fila.RowNumber, C.Cuenta, grupo.Key);
            if (c is null) continue;
            // US7 (T182): el contractual también tiene cuotas exigibles (las atrasadas al corte, en su vencimiento del plan).
            if (c.Naturaleza == LineNature.Deposit && c.Clase is not (DepositClass.Permanent or DepositClass.Contractual))
            {
                foreach (var q in grupo)
                    Error(P.AccountInstallments, q.Tanda, q.Fila.RowNumber, C.Cuenta, "Lending.Migration.InstallmentsNotAllowed",
                        $"La cuenta {grupo.Key} es de ahorro a la vista: no tiene cuotas exigibles (sólo aportes, ahorro permanente y contractual).");
                continue;
            }
            if (c.Clase == DepositClass.Contractual && lineas.GetValueOrDefault(c.LineaCodigo)?.DeCaptacion is { } lineaDelContrato
                && ContractualMigrado.Plan(filas, grupo.Key, lineaDelContrato.InstallmentPeriodicity ?? RepaymentPeriodicity.Monthly) is { } plan)
                foreach (var q in grupo.Where(q => plan.Cuotas.All(p => p.Vencimiento != q.Fila.DueDate)))
                    Error(P.AccountInstallments, q.Tanda, q.Fila.RowNumber, C.FechaDeVencimiento, "Lending.Migration.ContractualInstallmentNotInPlan",
                        $"La cuota del {q.Fila.DueDate:yyyy-MM-dd} no es una cuota del plan del contractual {grupo.Key} (vencen el {plan.Cuotas[0].Vencimiento:dd} de "
                        + "cada período desde el inicio del contrato).");
            var pagado = grupo.Sum(q => q.Fila.PaidAmount);
            var saldo = c.Saldos.GetValueOrDefault(c.ConceptoDeCuotas);
            if (pagado > saldo)
                Error(P.AccountInstallments, primera.Tanda, primera.Fila.RowNumber, C.Pagado, "Lending.Migration.InstallmentsExceedBalance",
                    $"Lo pagado de las cuotas de la cuenta {grupo.Key} ({Pesos(pagado)}) supera su saldo de {c.ConceptoDeCuotas} ({Pesos(saldo)}).");
        }

        // ------------------------------------------------------------------------------------------------------ GMF --
        if (filas.Exenciones.Count > 0)
        {
            var politica = await LectorDeCuentas.PoliticaDeGmfAsync(parametros, corte, ct);
            var personasConMarca = filas.Exenciones.Select(e => e.Fila.PersonId).ToList();
            var marcadas = (await db.GmfExemptAccounts.AsNoTracking().Where(g => personasConMarca.Contains(g.PersonId) && g.ValidTo == null && !g.IsDeleted)
                .Select(g => g.PersonId).ToListAsync(ct)).ToHashSet();
            foreach (var e in filas.Exenciones)
            {
                var f = e.Fila;
                PersonaViva(P.GmfExemptions, e.Tanda, f.RowNumber, C.Persona, f.PersonId);
                if (politica != GmfExemptionPolicy.SingleMarkedAccount)
                {
                    Error(P.GmfExemptions, e.Tanda, f.RowNumber, C.CuentaExenta, "Lending.Gmf.PolicyWithoutMark",
                        $"La política del GMF vigente al corte es {politica}: no se marcan cuentas exentas.");
                    continue;
                }
                if (marcadas.Contains(f.PersonId))
                    Error(P.GmfExemptions, e.Tanda, f.RowNumber, C.Persona, Domain.Entities.Lending.Deposits.Accounts.GmfExemptAccount.CodigoYaMarcada,
                        "La persona ya tiene una cuenta exenta del GMF vigente en la Cartera.");
                var c = Cuenta(P.GmfExemptions, e.Tanda, f.RowNumber, C.CuentaExenta, f.SourceAccountNumber);
                if (c is null) continue;
                if (c.Naturaleza != LineNature.Deposit || c.Clase == DepositClass.TermDeposit)
                    Error(P.GmfExemptions, e.Tanda, f.RowNumber, C.CuentaExenta, Domain.Entities.Lending.Deposits.Accounts.GmfExemptAccount.CodigoNoEsAhorro,
                        $"La cuenta {f.SourceAccountNumber} no es de ahorro: sólo una cuenta de ahorro se marca exenta del GMF.");
                else if (c.TitularId != f.PersonId)
                    Error(P.GmfExemptions, e.Tanda, f.RowNumber, C.CuentaExenta, "Lending.Gmf.NotHolder",
                        $"La persona no es la titular de la cuenta {f.SourceAccountNumber}: la cuenta exenta es una del titular.");
            }
        }

        // ----------------------------------------------------------------------------------------- bloqueos y embargos --
        // US11 (T189; §4.6, research E166): la cuenta está en la carga; un embargo no recae sobre aportes y retiene a lo sumo su saldo; un embargo de varias
        // cuentas es de una sola persona; la garantía nombra un crédito vivo de la Cartera.
        if (filas.Bloqueos.Count > 0)
        {
            var creditos = filas.Bloqueos.Where(b => b.Fila.LoanNumber is not null).Select(b => b.Fila.LoanNumber!).Distinct().ToList();
            var vivos = creditos.Count == 0
                ? []
                : (await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Number) && !l.IsDeleted
                                                            && (l.Status == LoanStatus.Active || l.Status == LoanStatus.PendingDisbursement))
                    .Select(l => l.Number).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var titularDelOficio = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in filas.Bloqueos)
            {
                var f = b.Fila;
                var c = Cuenta(P.HoldsAndGarnishments, b.Tanda, f.RowNumber, C.Cuenta, f.SourceAccountNumber);
                if (c is null) continue;
                if (f.Kind == "Garnishment")
                {
                    if (c.Naturaleza == LineNature.Contribution)
                    {
                        Error(P.HoldsAndGarnishments, b.Tanda, f.RowNumber, C.Tipo, Holds.ErroresDeEmbargos.CodigoAportesInembargables,
                            $"La cuenta {f.SourceAccountNumber} es de aportes: los aportes no se embargan (Ley 79 de 1988, art. 49).");
                        continue;
                    }
                    var saldo = LectorDeCuentas.SaldoDelTitular(c.Naturaleza, c.Saldos);
                    if ((f.RetainedAmount ?? 0m) > saldo)
                        Error(P.HoldsAndGarnishments, b.Tanda, f.RowNumber, C.Retenido, Holds.ErroresDeEmbargos.CodigoExcedeRetenido,
                            $"Lo retenido ({Pesos(f.RetainedAmount ?? 0m)}) supera el saldo de la cuenta {f.SourceAccountNumber} ({Pesos(saldo)}).");
                    if (f.CourtPersonId is { } juzgado) PersonaViva(P.HoldsAndGarnishments, b.Tanda, f.RowNumber, C.Juzgado, juzgado);
                    if (titularDelOficio.TryGetValue(f.Reference, out var otro) && otro != c.TitularId)
                        Error(P.HoldsAndGarnishments, b.Tanda, f.RowNumber, C.Referencia, "Lending.Migration.GarnishmentOfAnotherPerson",
                            $"El oficio {f.Reference} ya viene sobre una cuenta de otra persona: un embargo es de una sola persona.");
                    else titularDelOficio[f.Reference] = c.TitularId;
                }
                else
                {
                    if (c.Naturaleza == LineNature.Contribution && f.Reason != HoldReason.LoanCollateral)
                        Error(P.HoldsAndGarnishments, b.Tanda, f.RowNumber, C.Motivo, Holds.ErroresDeEmbargos.CodigoBloqueoInvalido,
                            "Sobre aportes sólo va el bloqueo de garantía de un crédito (LoanCollateral).");
                    if (f.Reason == HoldReason.LoanCollateral && f.LoanNumber is { } n && !vivos.Contains(n))
                        Error(P.HoldsAndGarnishments, b.Tanda, f.RowNumber, C.Credito, Holds.ErroresDeEmbargos.CodigoBloqueoInvalido,
                            $"No hay un crédito vivo de la Cartera con el número {n}.");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- tributario --
        if (filas.Tributario.Count > 0)
        {
            var titularesDe = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            foreach (var c in cuentas.Where(c => c.Numero is not null))
                titularesDe[c.Numero!] = [c.TitularId];
            foreach (var p in filas.PartesDeCuenta.Where(p => p.Fila.Role == MemberAccountPartyRole.CoHolder))
                if (titularesDe.TryGetValue(p.Fila.SourceAccountNumber, out var conjunto)) conjunto.Add(p.Fila.PersonId);
            foreach (var t in filas.Tributario)
            {
                var f = t.Fila;
                PersonaViva(P.TaxYearToDate, t.Tanda, f.RowNumber, C.Titular, f.PersonId);
                var c = Cuenta(P.TaxYearToDate, t.Tanda, f.RowNumber, C.Cuenta, f.SourceAccountNumber);
                if (c is null) continue;
                if (c.Naturaleza != LineNature.Deposit)
                    Error(P.TaxYearToDate, t.Tanda, f.RowNumber, C.Cuenta, "Lending.Migration.TaxYearNotSavings",
                        $"La cuenta {f.SourceAccountNumber} es de aportes: el acumulado tributario es de las cuentas de ahorro.");
                else if (!titularesDe[f.SourceAccountNumber].Contains(f.PersonId))
                    Error(P.TaxYearToDate, t.Tanda, f.RowNumber, C.Titular, "Lending.Migration.TaxYearNotHolder",
                        $"La persona no es titular ni cotitular de la cuenta {f.SourceAccountNumber}.");
            }
        }

        // ------------------------------------------------------------------------------------------------------ CDAT --
        if (filas.Cdat.Count > 0)
        {
            var cal = calendario is null
                ? CdatMigrado.SinFestivos
                : await calendario.ObtenerAsync(filas.Cdat.Min(c => c.Fila.StartDate), filas.Cdat.Max(c => c.Fila.MaturityDate).AddDays((int)RepaymentPeriodicity.Monthly),
                    IngenIA365ERP.Application.Common.Calendar.DiasDeDescanso.SabadoYDomingo, null, ct);
            var destinos = filas.Cdat.Select(c => c.Fila.DestinationAccountNumber).OfType<string>().Where(n => !porNumero.ContainsKey(n)).Distinct().ToList();
            var vivas = destinos.Count == 0 ? [] : await db.MemberAccounts.AsNoTracking()
                .Where(a => destinos.Contains(a.Number) && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
                .Select(a => new { a.Number, a.HolderPersonId, a.DepositClass }).ToListAsync(ct);
            foreach (var x in filas.Cdat)
            {
                var f = x.Fila;
                if (lineas.GetValueOrDefault(f.CreditLineCode)?.DeCaptacion is not { DepositClass: DepositClass.TermDeposit } version)
                {
                    Error(P.TermDeposits, x.Tanda, f.RowNumber, C.Linea, IngenIA365ERP.Application.Common.Imports.ImportErrors.CellNotFound,
                        $"La línea {f.CreditLineCode} no tiene versión de CDAT vigente al corte.");
                    continue;
                }
                var plan = PlanDeCdat.Generar(CdatMigrado.Condiciones(f, version), cal);
                if (f.MaturityDate != plan.Vencimiento && f.MaturityDate != plan.FechaDePagoAlVencimiento)
                    Error(P.TermDeposits, x.Tanda, f.RowNumber, C.FechaDeVencimiento, CdatMigrado.CodigoPlanDistinto,
                        string.Create(CultureInfo.InvariantCulture,
                            $"El CDAT {f.SourceAccountNumber} vence el {f.MaturityDate:yyyy-MM-dd} y su inicio más el plazo da el {plan.Vencimiento:yyyy-MM-dd}."));
                var pagados = CdatMigrado.Pagados(plan, corte).Count;
                if (pagados != f.PaidPeriods)
                    Error(P.TermDeposits, x.Tanda, f.RowNumber, C.PeriodosPagados, CdatMigrado.CodigoPlanDistinto,
                        string.Create(CultureInfo.InvariantCulture,
                            $"El CDAT {f.SourceAccountNumber} trae {f.PaidPeriods} período(s) pagado(s) y su plan tiene {pagados} con fecha de pago hasta el corte."));
                var esperado = CdatMigrado.EsperadoAlCorte(plan, corte);
                if (Math.Abs(esperado - f.InterestPayable) > CdatMigrado.Tolerancia)
                    Error(P.TermDeposits, x.Tanda, f.RowNumber, C.InteresCausadoNoPagado, CdatMigrado.CodigoPlanDistinto,
                        $"El CDAT {f.SourceAccountNumber} trae {Pesos(f.InterestPayable)} de interés causado y su plan regenerado da {Pesos(esperado)} al corte.");
                if (f.DestinationAccountNumber is { } destino)
                {
                    var valida = porNumero.TryGetValue(destino, out var deLaCarga)
                        ? deLaCarga.Clase == DepositClass.Demand && deLaCarga.TitularId == f.PersonId
                        : vivas.Any(v => v.Number == destino && v.DepositClass == DepositClass.Demand && v.HolderPersonId == f.PersonId);
                    if (!valida)
                        Error(P.TermDeposits, x.Tanda, f.RowNumber, C.CuentaDestino, "Lending.TermDeposit.DestinationInvalid",
                            $"La cuenta destino {destino} del CDAT {f.SourceAccountNumber} no es una cuenta a la vista del mismo titular, ni en la carga ni viva en la Cartera.");
                }
            }
        }

        var personasDeLaCarga = cuentas.Select(c => c.TitularId).Concat(filas.PartesDeCuenta.Select(p => p.Fila.PersonId)).Distinct().Count();
        return new ValidacionDeCaptaciones(errores, avisos, cuentas.Count, personasDeLaCarga);
    }

    private static string Pesos(decimal valor) => valor.ToString("N2", CultureInfo.GetCultureInfo("es-CO"));
}
