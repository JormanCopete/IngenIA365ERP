using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Pila;

/// <summary>
/// El motor puro de la planilla PILA (feature 010, US5; research R9): de las fichas, los
/// catálogos, las políticas, los parámetros vigentes y lo que las corridas aprobadas del mes
/// dicen de cada cotizante, produce los registros tipo 2 y la cabecera, con la explicación de
/// cada valor. Reglas del anexo: una línea base por cotizante y una adicional por cada
/// novedad con IBC propio (IGE, LMA, VAC, SLN, IRL); ING y RET del mismo mes en la misma
/// línea; los días por subsistema suman 30 salvo ING/RET; IBC ≥ 1 SMMLV proporcional y ≤ el
/// tope, integral al porcentaje del parámetro, sin auxilio, al peso superior; aportes al
/// múltiplo superior; ARL 0 en IGE/LMA; SLN sólo tarifas del empleador y FSP 0; exoneración
/// = política <b>y</b> IBC bajo el umbral, con los campos 54/66/68/76 coherentes. <b>Ningún
/// valor legal vive aquí</b>: todo sale del <see cref="ParameterSet"/> y de la política.
/// </summary>
public static class PilaBuilder
{
    private const string Calc = "Calculation";
    private const string Profile = "Profile";

    public static PilaResult Build(PilaInput input, PayrollLegalParameter? transitionSolidarityTable = null)
    {
        var result = new PilaResult();
        result.Issues.AddRange(PilaValidator.Validate(input));
        // Sin los parámetros del proceso no hay cálculo posible: la validación ya lo dijo como bloqueante.
        if (input.Parameters.Missing(PilaParameterCodes.Required).Count > 0) { Cabecera(result, input); return result; }

        var p = input.Parameters;
        var smmlv = p.Value(PilaParameterCodes.Smmlv);
        var ibcMinimoSmmlv = p.Value(PilaParameterCodes.ContributionBaseMinimumSmmlv);
        var ibcTopeSmmlv = p.Value(PilaParameterCodes.ContributionBaseCapSmmlv);
        var redondeoIbc = Math.Max(1m, p.Value(PilaParameterCodes.ContributionBaseRounding));
        var redondeoAporte = Math.Max(1m, p.Value(PilaParameterCodes.ContributionRoundingMultiple));
        var horasMes = p.Value(PilaParameterCodes.HoursPerMonth);
        var umbralExoneracion = p.Value(PilaParameterCodes.PayrollExemptionThresholdSmmlv);
        var umbralFsp = p.Value(PilaParameterCodes.SolidarityFundThresholdSmmlv);
        var pctIntegral = p.Fraction(PilaParameterCodes.IntegralSalaryBasePct);

        var pensionRate = p.Fraction(PilaParameterCodes.PensionEmployeePct) + p.Fraction(PilaParameterCodes.PensionEmployerPct);
        var pensionEmployerRate = p.Fraction(PilaParameterCodes.PensionEmployerPct);
        var healthFullRate = p.Fraction(PilaParameterCodes.HealthEmployeePct) + p.Fraction(PilaParameterCodes.HealthEmployerPct);
        var healthEmployeeRate = p.Fraction(PilaParameterCodes.HealthEmployeePct);
        var healthApprenticeRate = p.Fraction(PilaParameterCodes.HealthApprenticePct);
        var ccfRate = p.Fraction(PilaParameterCodes.FamilyCompensationPct);
        var senaRate = p.Fraction(PilaParameterCodes.SenaPct);
        var icbfRate = p.Fraction(PilaParameterCodes.IcbfPct);

        var contributors = new HashSet<int>();
        foreach (var c in input.Contributors.OrderBy(c => c.FirstLastName).ThenBy(c => c.FirstName).ThenBy(c => c.Document))
        {
            var ventana = Ventana(input, c);
            if (ventana is null) continue;
            var (desde, hasta) = ventana.Value;
            var diasTotales = CalendarConventions.Days(desde, hasta);
            if (diasTotales <= 0) continue;
            contributors.Add(c.EmployeeId);

            var ing = c.HireDate.Date >= input.MonthStart && c.HireDate.Date <= input.MonthEnd;
            var ret = c.TerminationDate is { } t && t.Date >= input.MonthStart && t.Date <= input.MonthEnd;

            // --- segmentos: la base y cada novedad con IBC propio, recortadas a la ventana y a los días ---
            var segmentos = new List<(PilaNoveltyKind Kind, DateTime From, DateTime To, int Days, PilaNovelty Novelty)>();
            var diasNovedad = 0;
            foreach (var n in c.Novelties.OrderBy(n => n.Start))
            {
                var o = CalendarConventions.Overlap(n.Start, n.End, desde, hasta);
                if (o is null) continue;
                var d = Math.Min(CalendarConventions.Days(o.Value.From, o.Value.To), diasTotales - diasNovedad);
                if (d <= 0) continue;
                segmentos.Add((n.Kind, o.Value.From, o.Value.To, d, n));
                diasNovedad += d;
            }
            var diasBase = diasTotales - diasNovedad;

            var (tipo, subtipo) = TipoDeCotizante(c);
            var salarioMes = c.BasicSalary;
            var salarioDiasTrabajados = c.DaysWorked > 0 ? salarioMes * c.DaysWorked / CalendarConventions.DaysPerMonth : 0m;
            // La nómina paga al peso: el salario proporcional se compara redondeado hacia arriba para que un centavo no sea «devengo variable».
            var variable = Math.Max(0m, c.ContributionEarnings - Math.Ceiling(salarioDiasTrabajados));

            // La línea base: días que no son novedad, salario proporcional más los devengos variables del mes.
            var lineas = new List<PilaLine>();
            if (diasBase > 0 || segmentos.Count == 0)
            {
                var ibcBase = salarioMes * diasBase / CalendarConventions.DaysPerMonth + variable;
                var l = NuevaLinea(c, null, tipo, subtipo, diasBase, salarioMes);
                l.Explain(42, Calc, $"Salario {Fmt.Money(salarioMes)} × {diasBase}/{CalendarConventions.DaysPerMonth} días" + (variable > 0m ? $" + devengos variables que forman IBC {Fmt.Money(variable)}" : string.Empty), ibcBase);
                if (variable > 0m) { l.Flags.Add("VST"); l.Values[23] = true; l.Explain(23, Calc, "VST: el IBC del mes superó el salario básico por devengos variables"); }
                Ibc(l, ibcBase, c, p, smmlv, ibcMinimoSmmlv, ibcTopeSmmlv, redondeoIbc, pctIntegral);
                lineas.Add(l);
            }
            foreach (var s in segmentos)
            {
                var ibc = salarioMes * s.Days / CalendarConventions.DaysPerMonth;
                var l = NuevaLinea(c, s.Kind, tipo, subtipo, s.Days, salarioMes);
                l.Flags.Add(s.Kind == PilaNoveltyKind.VAC ? "VAC-LR" : s.Kind.ToString());
                l.Explain(42, Calc, $"{Nombre(s.Kind)} del {Fmt.Date(s.From)} al {Fmt.Date(s.To)}: salario {Fmt.Money(salarioMes)} × {s.Days}/{CalendarConventions.DaysPerMonth} días", ibc);
                Ibc(l, ibc, c, p, smmlv, ibcMinimoSmmlv, ibcTopeSmmlv, redondeoIbc, pctIntegral);
                Fechas(l, s.Kind, s.From, s.To, s.Novelty);
                lineas.Add(l);
            }

            // Novedades de ingreso, retiro y cambio de salario van en la primera línea del cotizante.
            var primera = lineas[0];
            if (ing) { primera.Flags.Insert(0, "ING"); primera.Values[15] = true; primera.Values[80] = c.HireDate.Date; primera.Explain(80, Profile, "Fecha de ingreso de la ficha"); }
            if (ret) { primera.Flags.Add("RET"); primera.Values[16] = true; primera.Values[81] = c.TerminationDate!.Value.Date; primera.Explain(81, Profile, "Fecha de retiro de la ficha"); }
            if (c.SalaryChangeDate is { } vsp && vsp.Date >= input.MonthStart && vsp.Date <= input.MonthEnd && !ing)
            {
                primera.Flags.Add("VSP"); primera.Values[21] = true; primera.Values[82] = vsp.Date;
                primera.Explain(82, Calc, "VSP: cambio permanente de salario registrado en el mes");
            }
            if (c.VoluntaryPensionEmployee > 0m)
            {
                primera.Flags.Add("AVP"); primera.Values[28] = true;
                primera.VoluntaryEmployee = c.VoluntaryPensionEmployee;
                primera.Explain(48, Calc, "Aporte voluntario del afiliado a pensión obligatoria registrado como novedad en el mes", c.VoluntaryPensionEmployee);
            }
            if (!ing && !ret && lineas.Sum(l => l.DaysPension) != CalendarConventions.DaysPerMonth)
                result.Issues.Add(new PilaIssueItem(PilaIssueSeverity.Blocking, "Pila.DiasNoSuman30",
                    $"{c.FullName}: los días de las líneas suman {lineas.Sum(l => l.DaysPension)} y sin ingreso ni retiro deben sumar {CalendarConventions.DaysPerMonth}.",
                    36, c.EmployeeId, c.EmployeePublicId, c.FullName, $"/nomina/empleados/{c.EmployeePublicId}"));

            // --- tarifas y aportes de cada línea ---
            foreach (var l in lineas)
            {
                var segmento = l.Segment;
                var esSln = segmento == PilaNoveltyKind.SLN;
                var aprendizLectiva = tipo == "19";
                var pensionado = subtipo == "01";
                var sinPension = pensionado || c.ForeignNotRequiredToContributePension || aprendizLectiva;

                var ibcMensual = l.DaysPension > 0 ? l.IbcFamilyCompensation * CalendarConventions.DaysPerMonth / l.DaysPension : l.IbcFamilyCompensation;
                // ET art. 114-1: exonerado quien DEVENGA menos del umbral; se mira lo devengado del mes (antes del 70 % integral), no el IBC.
                var devengoMensual = l.DaysPension > 0 ? l.GrossEarnings * CalendarConventions.DaysPerMonth / l.DaysPension : l.GrossEarnings;
                l.Exempt = input.Policies.Exonerada114_1 && !aprendizLectiva && devengoMensual < umbralExoneracion * smmlv;
                l.Values[76] = l.Exempt ? "S" : "N";
                l.Explain(76, Calc, input.Policies.Exonerada114_1
                    ? $"Política Exonerada114_1 vigente; devengo mensual equivalente {Fmt.Money(devengoMensual)} {(l.Exempt ? "<" : "≥")} {Fmt.Num(umbralExoneracion)} SMMLV ({Fmt.Money(umbralExoneracion * smmlv)}) → {(l.Exempt ? "exonerado" : "no exonerado")}"
                    : "La empresa no tiene la política Exonerada114_1 vigente: aporta completo");

                // Pensión
                l.PensionRate = esSln ? pensionEmployerRate : sinPension ? 0m : pensionRate;
                l.Explain(46, Calc, esSln ? $"SLN: sólo la tarifa del empleador ({PilaParameterCodes.PensionEmployerPct})"
                    : sinPension ? (pensionado ? "Pensionado activo (subtipo 01): tarifa de pensión 0" : aprendizLectiva ? "Aprendiz en etapa lectiva (tipo 19): sin pensión" : "Extranjero no obligado a cotizar a pensiones")
                    : $"{PilaParameterCodes.PensionEmployeePct} + {PilaParameterCodes.PensionEmployerPct} vigentes al {Fmt.Date(input.MonthStart)}", l.PensionRate);
                l.Pension = Aporte(l.IbcPension, l.PensionRate, redondeoAporte);
                l.PensionTotal = l.Pension + l.VoluntaryEmployee + l.VoluntaryEmployer;
                l.Explain(47, Calc, $"IBC pensión {Fmt.Money(l.IbcPension)} × {Fmt.Pct(l.PensionRate)}, al múltiplo de {Fmt.Num(redondeoAporte)} superior", l.Pension);

                // Fondo de solidaridad pensional
                if (!esSln && !sinPension && l.IbcPension > 0m)
                {
                    var tabla = TablaFsp(input, c, p, transitionSolidarityTable);
                    var ibcEnSmmlv = ibcMensual / smmlv;
                    if (ibcEnSmmlv >= umbralFsp && tabla is not null)
                    {
                        var tramo = RangeTableLookup.Find(tabla, ibcMensual, p);
                        var tarifa = tramo.Rate;
                        var total = Aporte(l.IbcPension, tarifa, redondeoAporte);
                        var tarifaBase = tabla.Ranges.Where(r => !r.IsDeleted).OrderBy(r => r.FromValue).First().Rate ?? 0m;
                        var solidaridad = Aporte(l.IbcPension, tarifaBase / 100m * 0.5m, redondeoAporte);
                        l.SolidarityFund = Math.Min(solidaridad, total);
                        l.SubsistenceFund = total - l.SolidarityFund;
                        l.Explain(51, Calc, $"IBC mensual equivalente {Fmt.Num(ibcEnSmmlv)} SMMLV ≥ {Fmt.Num(umbralFsp)}: {tramo.RangeText()} ({tabla.Code} vigente desde {Fmt.Date(tabla.ValidFrom)}); la mitad del tramo base va a solidaridad. Según el anexo v30 los liquida el operador: una diferencia es alerta.", l.SolidarityFund);
                        l.Explain(52, Calc, "El resto del fondo de solidaridad pensional (subsistencia)", l.SubsistenceFund);
                    }
                    else
                        l.Explain(51, Calc, $"IBC mensual equivalente {Fmt.Num(ibcEnSmmlv)} SMMLV < {Fmt.Num(umbralFsp)} ({PilaParameterCodes.SolidarityFundThresholdSmmlv}): sin fondo de solidaridad", 0m);
                }
                else l.Explain(51, Calc, esSln ? "SLN: fondo de solidaridad 0" : "Sin cotización a pensión: fondo de solidaridad 0", 0m);

                // Salud
                l.HealthRate = esSln ? 0m : aprendizLectiva ? healthApprenticeRate : l.Exempt ? healthEmployeeRate : healthFullRate;
                l.Explain(54, Calc, esSln ? "SLN: sin cotización a salud" : aprendizLectiva ? $"Aprendiz: {PilaParameterCodes.HealthApprenticePct}"
                    : l.Exempt ? $"Exonerado: sólo {PilaParameterCodes.HealthEmployeePct}" : $"{PilaParameterCodes.HealthEmployeePct} + {PilaParameterCodes.HealthEmployerPct}", l.HealthRate);
                l.Health = Aporte(l.IbcHealth, l.HealthRate, redondeoAporte);
                l.Explain(55, Calc, $"IBC salud {Fmt.Money(l.IbcHealth)} × {Fmt.Pct(l.HealthRate)}, al múltiplo de {Fmt.Num(redondeoAporte)} superior", l.Health);

                // Riesgos laborales
                var sinArl = esSln || segmento is PilaNoveltyKind.IGE or PilaNoveltyKind.LMA || (segmento == PilaNoveltyKind.VAC && !input.Policies.CotizaArlEnVacaciones);
                var codigoClase = c.WorkRiskClass is { } clase ? CodigoClaseArl(clase) : null;
                l.WorkRiskRate = sinArl || codigoClase is null || !p.Has(codigoClase) ? 0m : p.Fraction(codigoClase);
                l.Explain(61, Calc, sinArl ? $"{(segmento is null ? "SLN" : Nombre(segmento.Value))}: tarifa de riesgos laborales 0"
                    : codigoClase is null ? "Ficha sin clase de riesgo ARL" : $"Clase {c.WorkRiskClass}: {codigoClase} vigente al {Fmt.Date(input.MonthStart)}", l.WorkRiskRate);
                l.WorkRisk = Aporte(l.IbcWorkRisk, l.WorkRiskRate, redondeoAporte);
                l.Explain(63, Calc, $"IBC ARL {Fmt.Money(l.IbcWorkRisk)} × {(l.WorkRiskRate * 100m).ToString("0.###")} %, al múltiplo de {Fmt.Num(redondeoAporte)} superior", l.WorkRisk);

                // Parafiscales: CCF completa aun exonerado; SENA e ICBF sólo si no exonerado.
                var sinParafiscales = esSln || aprendizLectiva;
                l.FamilyCompensationRate = sinParafiscales ? 0m : ccfRate;
                l.FamilyCompensation = Aporte(l.IbcFamilyCompensation, l.FamilyCompensationRate, redondeoAporte);
                l.Explain(64, Calc, sinParafiscales ? "Sin caja de compensación en esta línea" : $"{PilaParameterCodes.FamilyCompensationPct}: la CCF se paga completa aun exonerado", l.FamilyCompensationRate);
                l.Explain(65, Calc, $"IBC CCF {Fmt.Money(l.IbcFamilyCompensation)} × {Fmt.Pct(l.FamilyCompensationRate)}", l.FamilyCompensation);
                l.SenaRate = sinParafiscales || l.Exempt ? 0m : senaRate;
                l.IcbfRate = sinParafiscales || l.Exempt ? 0m : icbfRate;
                l.IbcOtherParafiscal = l.SenaRate > 0m || l.IcbfRate > 0m ? l.IbcFamilyCompensation : 0m;
                l.Sena = Aporte(l.IbcOtherParafiscal, l.SenaRate, redondeoAporte);
                l.Icbf = Aporte(l.IbcOtherParafiscal, l.IcbfRate, redondeoAporte);
                l.Explain(66, Calc, l.Exempt ? "Exonerado (art. 114-1): SENA 0" : sinParafiscales ? "Sin SENA en esta línea" : PilaParameterCodes.SenaPct, l.SenaRate);
                l.Explain(68, Calc, l.Exempt ? "Exonerado (art. 114-1): ICBF 0" : sinParafiscales ? "Sin ICBF en esta línea" : PilaParameterCodes.IcbfPct, l.IcbfRate);
                l.Explain(95, Calc, l.IbcOtherParafiscal == 0m ? "Base de SENA e ICBF en cero (exonerado o sin parafiscales)" : "Base de SENA e ICBF = IBC CCF", l.IbcOtherParafiscal);

                // Horas
                l.Hours = (int)Math.Round(l.DaysPension * horasMes / CalendarConventions.DaysPerMonth, MidpointRounding.AwayFromZero);
                l.Explain(96, Calc, $"{l.DaysPension} días × {Fmt.Num(horasMes)} h ({PilaParameterCodes.HoursPerMonth}) / {CalendarConventions.DaysPerMonth}", l.Hours);

                Valores(l, input, c);
            }

            result.Lines.AddRange(lineas);
        }

        for (var i = 0; i < result.Lines.Count; i++)
        {
            result.Lines[i].LineNumber = i + 1;
            result.Lines[i].Values[2] = i + 1;
        }
        result.ContributorCount = contributors.Count;
        Cabecera(result, input);
        return result;
    }

    // ------------------------------------------------------------------ piezas --

    /// <summary>La ventana del cotizante en el mes: desde el ingreso o el 1.º hasta el retiro o el último día; nula si no estuvo vinculado.</summary>
    private static (DateTime From, DateTime To)? Ventana(PilaInput input, PilaContributor c)
    {
        var desde = c.HireDate.Date > input.MonthStart ? c.HireDate.Date : input.MonthStart;
        var hasta = c.TerminationDate is { } t && t.Date < input.MonthEnd ? t.Date : input.MonthEnd;
        return hasta < desde ? null : (desde, hasta);
    }

    /// <summary>Tipo y subtipo: la ficha manda; si no, la clase y la etapa (Ley 2466/2025: aprendiz lectiva = 19; pensionado activo = 1 con subtipo 01).</summary>
    public static (string Type, string SubType) TipoDeCotizante(PilaContributor c)
    {
        var tipo = !string.IsNullOrWhiteSpace(c.ContributorTypeOverride) ? c.ContributorTypeOverride.Trim().PadLeft(2, '0')
            : c.Class is EmployeeClass.Apprentice or EmployeeClass.Intern && c.ApprenticeStage == Enums.Payroll.ApprenticeStage.Lective ? "19"
            : "01";
        var sub = !string.IsNullOrWhiteSpace(c.ContributorSubTypeOverride) ? c.ContributorSubTypeOverride.Trim().PadLeft(2, '0')
            : c.Class == EmployeeClass.Pensioner ? "01" : "00";
        return (tipo, sub);
    }

    private static PilaLine NuevaLinea(PilaContributor c, PilaNoveltyKind? segmento, string tipo, string subtipo, int dias, decimal salario)
    {
        var l = new PilaLine { Contributor = c, Segment = segmento, ContributorType = tipo, ContributorSubType = subtipo, Salary = salario, IntegralSalary = c.Class == EmployeeClass.IntegralSalary };
        l.DaysPension = l.DaysHealth = l.DaysWorkRisk = l.DaysFamilyCompensation = dias;
        return l;
    }

    private static void Ibc(PilaLine l, decimal bruto, PilaContributor c, ParameterSet p, decimal smmlv, decimal minimoSmmlv, decimal topeSmmlv, decimal redondeo, decimal pctIntegral)
    {
        var ibc = bruto;
        l.GrossEarnings = bruto;
        if (c.Class == EmployeeClass.IntegralSalary)
        {
            ibc *= pctIntegral;
            l.Explain(42, Calc, $"Salario integral: IBC al {Fmt.Pct(pctIntegral)} ({PilaParameterCodes.IntegralSalaryBasePct})", ibc);
        }
        var dias = l.DaysPension;
        var minimo = smmlv * minimoSmmlv * dias / CalendarConventions.DaysPerMonth;
        var tope = smmlv * topeSmmlv * dias / CalendarConventions.DaysPerMonth;
        if (l.Segment != PilaNoveltyKind.SLN && dias > 0 && ibc < minimo)
        {
            l.Explain(42, Calc, $"IBC mínimo: {Fmt.Num(minimoSmmlv)} SMMLV × {Fmt.Money(smmlv)} × {dias}/{CalendarConventions.DaysPerMonth} ({PilaParameterCodes.ContributionBaseMinimumSmmlv})", minimo);
            ibc = minimo;
        }
        if (ibc > tope)
        {
            l.Explain(42, Calc, $"Tope del IBC: {Fmt.Num(topeSmmlv)} SMMLV × {Fmt.Money(smmlv)} × {dias}/{CalendarConventions.DaysPerMonth} ({PilaParameterCodes.ContributionBaseCapSmmlv})", tope);
            ibc = tope;
        }
        var redondeado = Math.Ceiling(ibc / redondeo) * redondeo;
        if (redondeado != ibc) l.Explain(42, Calc, $"Al peso superior ({PilaParameterCodes.ContributionBaseRounding} = {Fmt.Num(redondeo)})", redondeado);
        l.IbcPension = l.IbcHealth = l.IbcWorkRisk = l.IbcFamilyCompensation = redondeado;
    }

    private static decimal Aporte(decimal ibc, decimal tarifa, decimal multiplo)
    {
        var bruto = ibc * tarifa;
        return bruto <= 0m ? 0m : Math.Ceiling(bruto / multiplo) * multiplo;
    }

    private static PayrollLegalParameter? TablaFsp(PilaInput input, PilaContributor c, ParameterSet p, PayrollLegalParameter? transicion)
    {
        if (!p.Has(PilaParameterCodes.SolidarityFundTable)) return transicion;
        var vigente = p.Table(PilaParameterCodes.SolidarityFundTable);
        // Régimen de transición (Ley 2381/2024): sigue con la tabla que regía antes del cambio, si la aplicación la trajo.
        return c.TransitionRegime == PensionTransitionRegime.Yes && transicion is not null && !ReferenceEquals(transicion, vigente) ? transicion : vigente;
    }

    private static string CodigoClaseArl(int clase) => clase switch
    {
        1 => PilaParameterCodes.WorkRiskClass1Pct,
        2 => PilaParameterCodes.WorkRiskClass2Pct,
        3 => PilaParameterCodes.WorkRiskClass3Pct,
        4 => PilaParameterCodes.WorkRiskClass4Pct,
        _ => PilaParameterCodes.WorkRiskClass5Pct,
    };

    private static void Fechas(PilaLine l, PilaNoveltyKind kind, DateTime from, DateTime to, PilaNovelty n)
    {
        switch (kind)
        {
            case PilaNoveltyKind.SLN: l.Values[24] = true; l.Values[83] = from; l.Values[84] = to; break;
            case PilaNoveltyKind.IGE: l.Values[25] = true; l.Values[85] = from; l.Values[86] = to; l.Values[57] = n.AuthorizationNumber; break;
            case PilaNoveltyKind.LMA: l.Values[26] = true; l.Values[87] = from; l.Values[88] = to; l.Values[59] = n.AuthorizationNumber; break;
            case PilaNoveltyKind.VAC: l.Values[27] = true; l.Values[89] = from; l.Values[90] = to; break;
            case PilaNoveltyKind.IRL: l.IrlDays = l.DaysPension; l.Values[30] = l.IrlDays; l.Values[93] = from; l.Values[94] = to; break;
        }
        l.Explain(kind switch { PilaNoveltyKind.SLN => 83, PilaNoveltyKind.IGE => 85, PilaNoveltyKind.LMA => 87, PilaNoveltyKind.VAC => 89, _ => 93 }, Calc,
            $"{Nombre(kind)} registrada en la nómina{(n.NoveltyPublicId is { } id ? $" (novedad {id})" : string.Empty)}: del {Fmt.Date(from)} al {Fmt.Date(to)}");
    }

    private static string Nombre(PilaNoveltyKind k) => k switch
    {
        PilaNoveltyKind.IGE => "Incapacidad por enfermedad general",
        PilaNoveltyKind.LMA => "Licencia de maternidad o paternidad",
        PilaNoveltyKind.VAC => "Vacaciones o licencia remunerada",
        PilaNoveltyKind.SLN => "Licencia no remunerada o suspensión",
        _ => "Incapacidad por riesgo laboral",
    };

    /// <summary>Los valores por número de campo del registro tipo 2.</summary>
    private static void Valores(PilaLine l, PilaInput input, PilaContributor c)
    {
        var v = l.Values;
        v[1] = "2";
        v[3] = c.DocumentType; v[4] = c.Document;
        v[5] = l.ContributorType; v[6] = l.ContributorSubType;
        v[7] = c.ForeignNotRequiredToContributePension; v[8] = c.ColombianAbroad;
        var dane = c.MunicipalityDaneCode ?? input.Employer.DefaultMunicipalityDaneCode;
        v[9] = dane is { Length: >= 5 } ? dane[..2] : null;
        v[10] = dane is { Length: >= 5 } ? dane[2..5] : null;
        l.Explain(9, Profile, c.MunicipalityDaneCode is null ? "DIVIPOLA de la sede (datos del aportante): la ficha no trae ubicación laboral" : "DIVIPOLA de la ficha");
        v[11] = c.FirstLastName; v[12] = c.SecondLastName; v[13] = c.FirstName; v[14] = c.OtherNames;
        v[31] = c.PensionPilaCode; v[33] = c.HealthPilaCode; v[35] = c.FamilyCompensationPilaCode; v[77] = c.WorkRiskPilaCode;
        v[36] = l.DaysPension; v[37] = l.DaysHealth; v[38] = l.DaysWorkRisk; v[39] = l.DaysFamilyCompensation;
        v[40] = l.Salary; v[41] = l.IntegralSalary;
        v[42] = l.IbcPension; v[43] = l.IbcHealth; v[44] = l.IbcWorkRisk; v[45] = l.IbcFamilyCompensation;
        v[46] = l.PensionRate; v[47] = l.Pension; v[48] = l.VoluntaryEmployee; v[49] = l.VoluntaryEmployer; v[50] = l.PensionTotal;
        v[51] = l.SolidarityFund; v[52] = l.SubsistenceFund; v[53] = 0m;
        v[54] = l.HealthRate; v[55] = l.Health; v[56] = 0m; v[58] = 0m; v[60] = 0m;
        v[61] = l.WorkRiskRate; v[62] = c.WorkCenterCode; v[63] = l.WorkRisk;
        v[64] = l.FamilyCompensationRate; v[65] = l.FamilyCompensation;
        v[66] = l.SenaRate; v[67] = l.Sena; v[68] = l.IcbfRate; v[69] = l.Icbf;
        v[70] = 0m; v[71] = 0m; v[72] = 0m; v[73] = 0m;
        v[78] = c.WorkRiskClass; v[79] = c.HighRiskPension ? "X" : null;
        v[95] = l.IbcOtherParafiscal; v[96] = l.Hours;
        v[98] = c.EconomicActivityCode ?? input.Employer.DefaultEconomicActivityCode;
        l.Explain(98, Profile, c.EconomicActivityCode is null ? "Actividad económica de la empresa (datos del aportante)" : "Actividad económica de la ficha");
        l.Explain(5, Profile, c.ContributorTypeOverride is not null ? "Tipo de cotizante fijado en la ficha" : $"Derivado de la clase {c.Class}{(c.ApprenticeStage is { } e ? $" en etapa {e}" : string.Empty)}");
        l.Explain(40, Profile, "Salario mensual vigente al último día del mes", l.Salary);
        l.Explain(36, Calc, $"Días de la línea ({(l.Segment is { } s ? Nombre(s) : "base")}); entre las líneas del cotizante suman {CalendarConventions.DaysPerMonth} salvo ingreso o retiro", l.DaysPension);
    }

    private static void Cabecera(PilaResult r, PilaInput input)
    {
        var e = input.Employer;
        var v = r.HeaderValues;
        v[1] = "1"; v[2] = "1"; v[3] = "0001";
        v[4] = e.Name; v[5] = "NI"; v[6] = e.Nit; v[7] = e.CheckDigit; v[8] = e.PlanillaType;
        v[11] = e.PresentationForm; v[12] = e.BranchCode; v[13] = e.BranchName; v[14] = e.ArlPilaCode;
        v[15] = input.MonthStart; v[16] = input.HealthPeriod;
        v[19] = r.ContributorCount; v[20] = r.TotalIbcFamilyCompensation;
        v[21] = e.ContributorType; v[22] = e.OperatorCode;
    }
}
